"""Offline injected moving-pose windows; no process access or input."""

import asyncio
from dataclasses import replace
import math
from pathlib import Path
import struct
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from w101_moving_pose import (  # noqa: E402
    LAYOUT_ID,
    MovingPoseLimits,
    MovingPoseStreamLimits,
    stream_moving_pose,
    PoseContext,
    read_moving_pose,
)


class MovingPoseTests(unittest.IsolatedAsyncioTestCase):
    def setUp(self):
        self.time = 0.0
        self.context = PoseContext("target", "session", "Commons", "wizard")
        self.expected = self.context
        self.limits = MovingPoseLimits(100, 100, 10)
        self.poses = [(0, 0, 0, 0, 0, 0), (1, 0, 0, 0, 0, 0.1), (2, 0, 0, 0, 0, 0.2)]
        self.index = 0
        self.calls = 0
        self.on_read = None

    async def read(self, address, count):
        self.calls += 1
        if self.on_read:
            self.on_read()
        if address == 0x10000 + 72:
            return struct.pack("<Q", 0x20000)
        if address == 0x10000 + 0x474:
            return struct.pack("<I", 8)
        if address == 0x10000 + 88:
            pose = self.poses[self.index]
            self.index += 1
            return struct.pack("<6f", *pose)
        raise AssertionError("Unexpected address")

    async def delay(self, seconds):
        self.time += seconds

    async def run_window(self, **overrides):
        args = dict(
            layout_id=LAYOUT_ID,
            expected_context=self.expected,
            get_context=lambda: self.context,
            limits=self.limits,
            clock=lambda: self.time,
            delay=self.delay,
        )
        args.update(overrides)
        return await read_moving_pose(self.read, 0x10000, **args)

    async def test_legitimate_moving_trace_is_candidate_not_authority(self):
        result = await self.run_window()
        self.assertEqual("candidate", result["status"])
        self.assertEqual(15, result["readCalls"])
        self.assertEqual(144, result["readBytes"])
        self.assertFalse(result["liveQualified"])
        self.assertFalse(result["movementAuthorized"])
        self.assertFalse(result["atomicityVerified"])
        self.assertEqual(2, result["samples"][-1]["position"]["x"])
        self.assertEqual(0.1, result["samples"][-1]["observed"])
        self.assertEqual(5, len(result["samples"][0]["reads"]))

    async def test_context_changes_invalidate_all_previous_samples(self):
        for field in ("target", "session", "zone", "wizard", "loading"):
            with self.subTest(field=field):
                self.setUp()

                def change():
                    if self.calls == 7:
                        self.context = replace(
                            self.context,
                            **{field: True if field == "loading" else "changed"}
                        )

                self.on_read = change
                result = await self.run_window()
                self.assertEqual("unknown", result["status"])
                self.assertEqual([], result["samples"])
                self.assertEqual(
                    "loading" if field == "loading" else "context-changed",
                    result["reason"],
                )

    async def test_parent_changes_between_samples(self):
        original = self.read

        async def changed(address, count):
            value = await original(address, count)
            if self.calls >= 6 and address == 0x10000 + 72:
                return struct.pack("<Q", 0x30000)
            return value

        self.read = changed
        self.assertEqual("actor-identity-changed", (await self.run_window())["reason"])

    async def test_marker_changes_inside_sample(self):
        original = self.read

        async def changed(address, count):
            value = await original(address, count)
            return struct.pack("<I", 9) if self.calls == 5 else value

        self.read = changed
        self.assertEqual("actor-identity-changed", (await self.run_window())["reason"])

    async def test_jump_bounds_are_independently_enforced(self):
        for component, value, reason in (
            (0, 20, "planar-jump"),
            (2, 20, "vertical-jump"),
            (5, 2, "angular-jump"),
        ):
            with self.subTest(reason=reason):
                self.setUp()
                pose = list(self.poses[1])
                pose[component] = value
                self.poses[1] = pose
                result = await self.run_window()
                self.assertEqual(reason, result["reason"])
                self.assertEqual([], result["samples"])

    async def test_angular_wrap_uses_shortest_difference(self):
        self.poses = [(0, 0, 0, 0, 0, yaw) for yaw in (math.tau - 0.1, 0, 0.1)]
        self.assertEqual("candidate", (await self.run_window())["status"])

    async def test_nonfinite_fields_including_unused_angles(self):
        for index in range(6):
            self.setUp()
            pose = list(self.poses[0])
            pose[index] = float("nan")
            self.poses[0] = pose
            self.assertEqual("nonfinite-pose", (await self.run_window())["reason"])

    async def test_backward_clock(self):
        def rewind():
            self.time -= 0.01

        self.on_read = rewind
        self.assertEqual("clock-moved-backward", (await self.run_window())["reason"])

    async def test_stale_read(self):
        self.on_read = lambda: setattr(self, "time", self.time + 0.11)
        self.assertEqual("stale-read", (await self.run_window())["reason"])

    async def test_sample_age_budget(self):
        self.on_read = lambda: setattr(self, "time", self.time + 0.05)
        self.assertEqual("stale-sample", (await self.run_window())["reason"])

    async def test_gap_and_window_expiration(self):
        for gap, reason in ((0.3, "sample-gap"), (2, "window-expired")):
            self.setUp()

            async def delayed(_):
                self.time += gap

            self.assertEqual(reason, (await self.run_window(delay=delayed))["reason"])

    async def test_invalid_calibration_never_reads(self):
        for field, value in (
            ("sample_count", 9),
            ("max_planar_speed", float("inf")),
            ("max_vertical_speed", -1),
            ("max_duration_seconds", 2),
        ):
            self.setUp()
            result = await self.run_window(
                limits=replace(self.limits, **{field: value})
            )
            self.assertEqual("invalid-limits", result["reason"])
            self.assertEqual(0, self.calls)

    async def test_wrong_initial_identity_never_reads(self):
        self.context = replace(self.context, wizard="different")
        self.assertEqual("context-changed", (await self.run_window())["reason"])
        self.assertEqual(0, self.calls)

    async def test_nonadvancing_clock_rejects_duplicate_sample_time(self):
        async def no_time(_):
            pass

        self.assertEqual("sample-gap", (await self.run_window(delay=no_time))["reason"])

    async def test_context_errors_do_not_leak_external_details(self):
        def fail():
            raise RuntimeError("private context")

        result = await self.run_window(get_context=fail)
        self.assertEqual("observation-failed", result["reason"])
        self.assertNotIn("private", str(result))

    async def test_configured_real_deadline_bounds_a_stuck_delay(self):
        async def stuck(_):
            await asyncio.Event().wait()

        limits = replace(
            self.limits,
            interval_seconds=0.001,
            max_gap_seconds=0.01,
            max_read_seconds=0.005,
            max_sample_seconds=0.01,
            max_duration_seconds=0.02,
        )
        result = await self.run_window(delay=stuck, limits=limits)
        self.assertEqual("window-timeout", result["reason"])
        self.assertEqual([], result["samples"])

    async def test_transport_errors_are_sanitized(self):
        async def fail(*_):
            raise OSError("secret pointer")

        self.read = fail
        result = await self.run_window()
        self.assertEqual("read-failed", result["reason"])
        self.assertNotIn("secret", str(result))

    async def test_cancellation_propagates(self):
        async def cancel(*_):
            raise asyncio.CancelledError()

        self.read = cancel
        with self.assertRaises(asyncio.CancelledError):
            await self.run_window()


class MovingPoseStreamTests(unittest.IsolatedAsyncioTestCase):
    setUp = MovingPoseTests.setUp
    read = MovingPoseTests.read
    delay = MovingPoseTests.delay

    async def run_stream(self, **overrides):
        self.emitted = []
        self.invalidations = []

        async def sample(value):
            self.emitted.append(value)

        async def invalidated(value):
            self.invalidations.append(value)

        args = dict(
            layout_id=LAYOUT_ID,
            expected_context=self.expected,
            get_context=lambda: self.context,
            limits=MovingPoseStreamLimits(100, 100, 10, sample_count=3),
            on_sample=sample,
            on_invalidated=invalidated,
            clock=lambda: self.time,
            delay=self.delay,
        )
        args.update(overrides)
        return await stream_moving_pose(self.read, 0x10000, **args)

    async def test_stream_emits_ordered_samples_without_retaining_them(self):
        result = await self.run_stream()
        self.assertEqual("candidate", result["status"])
        self.assertEqual([], result["samples"])
        self.assertEqual(3, result["sampleCount"])
        self.assertEqual([0, 1, 2], [s["position"]["x"] for s in self.emitted])
        self.assertEqual(15, result["readCalls"])
        self.assertEqual([], self.invalidations)

    async def test_stream_checks_refresh_before_and_after_each_sample(self):
        refreshes = 0

        async def refresh():
            nonlocal refreshes
            refreshes += 1
            if refreshes == 4:
                self.context = replace(self.context, loading=True)

        result = await self.run_stream(refresh_context=refresh)
        self.assertEqual("loading", result["reason"])
        self.assertEqual(1, len(self.emitted))
        self.assertEqual(1, self.invalidations[0]["emittedSamples"])
        self.assertEqual(4, refreshes)

    async def test_stream_cannot_mutate_comparison_sample(self):
        async def mutate(sample):
            sample["position"]["x"] = 1000000
            sample["yawRadians"] = 1000000

        result = await self.run_stream(on_sample=mutate)
        self.assertEqual("candidate", result["status"])

    async def test_stream_sink_failure_invalidates_previous_evidence(self):
        async def fail(sample):
            if sample["position"]["x"] == 1:
                raise RuntimeError("private callback detail")

        result = await self.run_stream(on_sample=fail)
        self.assertEqual("observation-failed", result["reason"])
        self.assertEqual(1, self.invalidations[0]["emittedSamples"])
        self.assertNotIn("private", str(result))

    async def test_stream_backpressure_has_real_deadline(self):
        async def stuck(_):
            await asyncio.Event().wait()

        limits = MovingPoseStreamLimits(
            100,
            100,
            10,
            sample_count=3,
            interval_seconds=0.001,
            max_gap_seconds=0.01,
            max_read_seconds=0.005,
            max_sample_seconds=0.01,
            max_duration_seconds=0.02,
        )
        result = await self.run_stream(limits=limits, on_sample=stuck)
        self.assertEqual("window-timeout", result["reason"])
        self.assertEqual(1, len(self.invalidations))
        self.assertEqual(5, result["readCalls"])

    async def test_stream_cancellation_notifies_and_propagates(self):
        async def cancel(_):
            raise asyncio.CancelledError()

        with self.assertRaises(asyncio.CancelledError):
            await self.run_stream(on_sample=cancel)
        self.assertEqual("cancelled", self.invalidations[0]["reason"])

    async def test_stream_caps_reject_before_reads(self):
        for limits in (
            MovingPoseStreamLimits(100, 100, 10, sample_count=1201),
            MovingPoseStreamLimits(100, 100, 10, max_duration_seconds=61),
        ):
            self.setUp()
            result = await self.run_stream(limits=limits)
            self.assertEqual("invalid-limits", result["reason"])
            self.assertEqual(0, self.calls)

    async def test_stream_does_not_reset_rate_checks_at_window_boundary(self):
        self.poses = [(i, 0, 0, 0, 0, 0) for i in range(9)]
        self.poses[8] = (1000, 0, 0, 0, 0, 0)
        result = await self.run_stream(
            limits=MovingPoseStreamLimits(
                100,
                100,
                10,
                sample_count=9,
            )
        )
        self.assertEqual("planar-jump", result["reason"])
        self.assertEqual(8, self.invalidations[0]["emittedSamples"])

    async def test_stream_refresh_rejects_loading_before_first_read(self):
        async def refresh():
            self.context = replace(self.context, loading=True)

        result = await self.run_stream(refresh_context=refresh)
        self.assertEqual("loading", result["reason"])
        self.assertEqual(0, self.calls)
        self.assertEqual([], self.emitted)

    async def test_stream_extends_beyond_eight_sample_window(self):
        # One rate chain spans the full stream rather than restarting per window.
        self.poses = [(i, 0, 0, 0, 0, 0) for i in range(60)]
        result = await self.run_stream(
            limits=MovingPoseStreamLimits(
                100,
                100,
                10,
                sample_count=60,
                max_duration_seconds=5,
            )
        )
        self.assertEqual("candidate", result["status"])
        self.assertEqual(60, result["sampleCount"])
        self.assertEqual(300, result["readCalls"])
        self.assertEqual(2880, result["readBytes"])


if __name__ == "__main__":
    unittest.main()
