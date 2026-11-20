"""Synthetic owner-lifecycle checks; no process reads, hooks, or game inputs."""

import asyncio
from pathlib import Path
import struct
import sys
import time
from types import SimpleNamespace
import unittest
from unittest.mock import AsyncMock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import w101_ui_tree as ui
from w101_moving_pose import MovingPoseLimits, MovingPoseStreamLimits, PoseContext


class PoseStreamOwnerTests(unittest.IsolatedAsyncioTestCase):
    def setUp(self):
        self.trace = []
        self.events = []
        self.finished = False
        self.loading_calls = 0
        self.loading_at = None
        self.final_failure = None
        self.stream_failure = None
        self.end_failure = False
        self.current = True
        self.reader = object.__new__(ui.UiClient)

        async def root():
            self.trace.append("root")
            return (
                0x11000 if self.finished and self.final_failure == "export" else 0x10000
            )

        async def close():
            self.trace.append("close")

        async def old_close():
            self.trace.append("old-close")

        async def loading():
            self.loading_calls += 1
            self.trace.append("loading")
            return self.loading_calls == self.loading_at

        async def read(address, count):
            self.assertEqual((0x10000 + 72, 8), (address, count))
            self.trace.append("parent")
            return struct.pack("<Q", 0x20000)

        async def zone(*_, **__):
            self.trace.append("zone")
            return dict(
                status="candidate",
                zoneName=(
                    "Other"
                    if self.finished and self.final_failure == "zone"
                    else "Commons"
                ),
            )

        async def ready():
            self.trace.append("ready")

        async def emit(value):
            self.trace.append(value["kind"])
            self.events.append(value)
            if self.end_failure and value["kind"] == "moving-pose-ended":
                raise RuntimeError("synthetic sink failure")

        async def stream(*_, **kwargs):
            self.trace.append("stream")
            if self.stream_failure:
                raise self.stream_failure
            for x in (1, 2):
                await kwargs["refresh_context"]()
                self.assertEqual(kwargs["expected_context"], kwargs["get_context"]())
                await kwargs["refresh_context"]()
                await kwargs["on_sample"](
                    dict(
                        started=time.perf_counter(),
                        observed=time.perf_counter(),
                        position=dict(x=x, y=0, z=0),
                        yawRadians=0,
                    )
                )
            self.finished = True
            return dict(status="candidate", samples=[], sampleCount=2)

        self.old = SimpleNamespace(close=AsyncMock(side_effect=old_close))
        self.scope = SimpleNamespace(
            open=AsyncMock(side_effect=lambda: self.trace.append("open")),
            close=AsyncMock(side_effect=close),
            root=AsyncMock(side_effect=root),
        )
        self.reader.scope = self.old
        self.reader.client = SimpleNamespace(
            process_id=123,
            hook_handler=SimpleNamespace(read_bytes=AsyncMock(side_effect=read)),
        )
        self.reader.loading = AsyncMock(side_effect=loading)
        self.reader.pose_owner = AsyncMock(
            return_value=dict(
                status="observed",
                ownerMatched=True,
                statsMatched=True,
                characterKey="a" * 64,
                readCalls=29,
                readBytes=298,
            )
        )
        self.reader.pose_window_ready = ready
        self.reader.moving_pose_seconds = 1
        self.reader.pose_stream_current = lambda: self.current
        self.reader.pose_stream_emit = emit
        self.reader.pose_stream_binding = dict(
            runId="test-run", target=dict(processId=123)
        )
        self.pose_reader = SimpleNamespace(
            LAYOUT_ID="archived-wizwalker-actor-body",
            player_hook=lambda value: value,
            read_pose=AsyncMock(return_value=dict(status="candidate")),
        )
        self.zone_reader = SimpleNamespace(LAYOUT_ID="zone", read_zone=zone)
        self.moving_reader = SimpleNamespace(
            PoseContext=PoseContext,
            MovingPoseLimits=MovingPoseLimits,
            MovingPoseStreamLimits=MovingPoseStreamLimits,
            stream_moving_pose=stream,
        )

    async def invoke(self):
        modules = {
            "w101_zone_reader": self.zone_reader,
            "w101_moving_pose": self.moving_reader,
        }
        with patch.dict(
            sys.modules, {"wizwalker.memory.hooks": SimpleNamespace(PlayerHook=object)}
        ), patch.object(ui, "RootScope", return_value=self.scope), patch.object(
            ui, "pose_reader_module", return_value=self.pose_reader
        ), patch.object(
            ui, "navigation_reader_module", side_effect=lambda name: modules[name]
        ), patch.object(
            ui.asyncio, "sleep", new=AsyncMock()
        ):
            return await self.reader.pose()

    def assert_ended_and_closed(self):
        self.assertEqual("moving-pose-ended", self.events[-1]["kind"])
        self.assertFalse(self.events[-1]["currentAuthority"])
        self.scope.close.assert_awaited_once()
        self.old.close.assert_awaited_once()
        self.assertEqual("close", self.trace[-1])

    async def test_success_refreshes_before_readiness_and_emits_samples_then_end(self):
        result = await self.invoke()
        before_ready = self.trace[: self.trace.index("ready")]
        self.assertEqual(2, before_ready.count("loading"))
        self.assertEqual(2, before_ready.count("zone"))
        self.assertEqual(3, before_ready.count("root"))
        self.assertLess(self.trace.index("ready"), self.trace.index("stream"))
        self.assertEqual(
            ["moving-pose-candidate", "moving-pose-candidate", "moving-pose-ended"],
            [event["kind"] for event in self.events],
        )
        for event in self.events[:-1]:
            self.assertEqual("Commons", event["zone"])
            self.assertEqual("a" * 64, event["wizardKey"])
            self.assertTrue(event["wizardOwnerChecked"])
            self.assertFalse(event["contextQualified"])
            self.assertGreaterEqual(event["sampleAgeMs"], 0)
            self.assertEqual("test-run", event["binding"]["runId"])
            self.assertEqual(32, len(event["streamId"]))
        self.assertEqual([1, 2], [event["sequence"] for event in self.events[:-1]])
        self.assertEqual(self.events[0]["streamId"], self.events[1]["streamId"])
        self.assertEqual(2, result["movingWindowCandidate"]["sampleCount"])
        self.assertFalse(result["movingWindowCandidate"]["contextQualified"])
        self.assert_ended_and_closed()

    async def test_loading_after_hook_setup_blocks_readiness(self):
        self.loading_at = 1
        with self.assertRaisesRegex(ValueError, "Navigation readiness changed"):
            await self.invoke()
        self.assertNotIn("ready", self.trace)
        self.assertNotIn("stream", self.trace)
        self.assert_ended_and_closed()

    async def test_wizard_change_blocks_stream_before_sample(self):
        initial = self.reader.pose_owner.return_value
        self.reader.pose_owner.side_effect = [
            initial,
            dict(initial, characterKey="b" * 64),
        ]
        with self.assertRaisesRegex(ValueError, "wizard changed"):
            await self.invoke()
        self.assertNotIn("ready", self.trace)
        self.assert_ended_and_closed()

    async def test_loading_during_initial_refresh_blocks_readiness(self):
        self.loading_at = 2
        with self.assertRaisesRegex(ValueError, "Navigation readiness changed"):
            await self.invoke()
        self.assertNotIn("ready", self.trace)
        self.assertNotIn("stream", self.trace)
        self.assert_ended_and_closed()

    async def test_changed_owner_blocks_readiness(self):
        self.current = False
        with self.assertRaisesRegex(ValueError, "Navigation owner changed"):
            await self.invoke()
        self.assertNotIn("ready", self.trace)
        self.assert_ended_and_closed()

    async def test_final_zone_failure_invalidates_after_samples_and_closes(self):
        self.final_failure = "zone"
        with self.assertRaisesRegex(ValueError, "Navigation context changed"):
            await self.invoke()
        self.assertEqual(
            2, sum(e["kind"] == "moving-pose-candidate" for e in self.events)
        )
        self.assert_ended_and_closed()

    async def test_final_export_failure_invalidates_after_samples_and_closes(self):
        self.final_failure = "export"
        with self.assertRaisesRegex(ValueError, "Player export changed"):
            await self.invoke()
        self.assertEqual(
            2, sum(e["kind"] == "moving-pose-candidate" for e in self.events)
        )
        self.assert_ended_and_closed()

    async def test_stream_exception_still_ends_and_closes(self):
        self.stream_failure = RuntimeError("synthetic read failure")
        with self.assertRaisesRegex(RuntimeError, "synthetic read failure"):
            await self.invoke()
        self.assert_ended_and_closed()

    async def test_stream_cancellation_still_ends_and_closes(self):
        self.stream_failure = asyncio.CancelledError()
        with self.assertRaises(asyncio.CancelledError):
            await self.invoke()
        self.assert_ended_and_closed()

    async def test_changed_export_blocks_readiness(self):
        self.scope.root.side_effect = [0x10000, 0x11000]
        with self.assertRaisesRegex(ValueError, "Navigation readiness changed"):
            await self.invoke()
        self.assertNotIn("ready", self.trace)
        self.assertNotIn("stream", self.trace)
        self.assert_ended_and_closed()

    async def test_terminal_sink_failure_cannot_skip_scope_cleanup(self):
        self.end_failure = True
        with self.assertRaisesRegex(RuntimeError, "synthetic sink failure"):
            await self.invoke()
        self.assert_ended_and_closed()


if __name__ == "__main__":
    unittest.main()
