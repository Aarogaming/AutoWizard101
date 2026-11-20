import importlib.util
import io
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest import mock

SCRIPT = Path(__file__).resolve().parents[1] / "wsl_dodge_guest.py"
SPEC = importlib.util.spec_from_file_location("wsl_dodge_guest", SCRIPT)
guest = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(guest)


def framebuffer():
    header = [
        160,
        7,
        2,
        24,
        480,
        720,
        0,
        0,
        32,
        0,
        32,
        32,
        1920,
        4,
        0xFF0000,
        0xFF00,
        0xFF,
        8,
        256,
        256,
        480,
        720,
        0,
        0,
        0,
    ]
    return struct.pack(">25I", *header) + bytes(60 + 256 * 12) + bytes(480 * 720 * 4)


class FramebufferTests(unittest.TestCase):
    def test_slow_step_waits_for_the_same_ack_without_resending(self):
        with tempfile.TemporaryDirectory() as temp:
            channel = Path(temp)
            fb = channel / "screen.xwd"
            fb.write_bytes(framebuffer())
            clock = [0.0]
            seen = set()

            def sleep(duration):
                clock[0] += duration
                for request_path in channel.glob("step-request-*.json"):
                    seen.add(request_path.name)
                    if clock[0] >= 13:
                        request = json.loads(request_path.read_text())
                        (channel / f"step-ack-{request['id']:04d}.json").write_text(
                            json.dumps(
                                {"id": request["id"], "ticks": request["frames"]}
                            )
                        )

            with (
                mock.patch.object(
                    guest.sys,
                    "argv",
                    ["guest", temp, str(fb), "123", "xdotool", "stepped", "0.25"],
                ),
                mock.patch.object(
                    guest.sys,
                    "stdin",
                    io.StringIO(json.dumps({"id": 0, "kind": "observe"})),
                ),
                mock.patch.object(guest.sys, "stdout", io.StringIO()),
                mock.patch.object(
                    guest.subprocess, "run", return_value=mock.Mock(stdout="123\n")
                ),
                mock.patch.object(guest.time, "sleep", side_effect=sleep),
                mock.patch.object(
                    guest.time, "monotonic", side_effect=lambda: clock[0]
                ),
                mock.patch.dict(guest.os.environ, {"MAELSTROM_DODGE_EVAL": "0"}),
            ):
                self.assertEqual(guest.main(), 0)
            self.assertEqual(seen, {"step-request-0000.json"})
            self.assertGreaterEqual(clock[0], 13)

    def test_atomic_publication_retries_a_transient_windows_lock(self):
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / "new"
            target = Path(temp) / "current"
            source.write_bytes(b"new")
            target.write_bytes(b"old")
            real_replace = guest.os.replace
            calls = []

            def replace(a, b):
                calls.append(1)
                if len(calls) < 3:
                    raise PermissionError("temporarily locked")
                real_replace(a, b)

            with mock.patch.object(
                guest.os, "replace", side_effect=replace
            ), mock.patch.object(guest.time, "sleep"):
                guest.replace_with_retry(source, target)
            self.assertEqual(target.read_bytes(), b"new")
            self.assertEqual(len(calls), 3)

    def test_atomic_publication_does_not_hide_a_persistent_lock(self):
        with mock.patch.object(
            guest.os, "replace", side_effect=PermissionError("locked")
        ) as replace, mock.patch.object(guest.time, "sleep"):
            with self.assertRaises(PermissionError):
                guest.replace_with_retry("source", "target")
            self.assertEqual(replace.call_count, 25)

    def test_step_counts_preserve_simulated_duration_at_each_speed(self):
        self.assertEqual(guest.frames_for_simulated_ms(1000, 1), 30)
        self.assertEqual(guest.frames_for_simulated_ms(1000, 0.5), 60)
        self.assertEqual(guest.frames_for_simulated_ms(1000, 0.25), 120)
        for speed in (0, 0.1, 2):
            with self.assertRaises(ValueError):
                guest.frames_for_simulated_ms(100, speed)

    def test_private_clock_channel_is_separate_from_persisted_evaluation(self):
        with tempfile.TemporaryDirectory() as temp:
            host = Path(temp) / "host"
            step = Path(temp) / "step"
            host.mkdir()
            step.mkdir()
            fb = host / "screen.xwd"
            fb.write_bytes(framebuffer())

            def sleep(_duration):
                for path in step.glob("step-request-*.json"):
                    request = json.loads(path.read_text())
                    (step / "evaluation.json").write_text(
                        json.dumps(
                            {"score": 3, "alive": True, "ticks": request["frames"]}
                        )
                    )
                    (step / f"step-ack-{request['id']:04d}.json").write_text(
                        json.dumps({"id": request["id"], "ticks": request["frames"]})
                    )

            with (
                mock.patch.object(
                    guest.sys,
                    "argv",
                    [
                        "guest",
                        str(host),
                        str(fb),
                        "123",
                        "xdotool",
                        "stepped",
                        "0.25",
                        str(step),
                    ],
                ),
                mock.patch.object(
                    guest.sys,
                    "stdin",
                    io.StringIO(json.dumps({"id": 0, "kind": "observe"})),
                ),
                mock.patch.object(guest.sys, "stdout", io.StringIO()),
                mock.patch.object(
                    guest.subprocess, "run", return_value=mock.Mock(stdout="123\n")
                ),
                mock.patch.object(guest.time, "sleep", side_effect=sleep),
                mock.patch.dict(guest.os.environ, {"MAELSTROM_DODGE_EVAL": "1"}),
            ):
                self.assertEqual(guest.main(), 0)
            self.assertEqual(
                json.loads((host / "evaluation.json").read_text())["score"], 3
            )
            self.assertEqual(list(step.glob("step-request-*.json")), [])
            self.assertEqual(list(host.glob("step-request-*.json")), [])

    def test_stepped_actions_keep_game_time_and_release_input(self):
        with tempfile.TemporaryDirectory() as temp:
            channel = Path(temp)
            fb = channel / "screen.xwd"
            fb.write_bytes(framebuffer())
            ticks = 0
            calls = []

            def sleep(_duration):
                nonlocal ticks
                for request_path in sorted(channel.glob("step-request-*.json")):
                    request = json.loads(request_path.read_text())
                    ack = channel / f"step-ack-{request['id']:04d}.json"
                    if not ack.exists():
                        ticks += request["frames"]
                        ack.write_text(
                            json.dumps({"id": request["id"], "ticks": ticks})
                        )

            def run(args, **_kwargs):
                calls.append(args[1:])
                return mock.Mock(stdout="123\n")

            requests = [
                {"id": 0, "kind": "observe"},
                {"id": 1, "kind": "action", "key": "LEFT", "duration_ms": 120},
                {"id": 2, "kind": "observe"},
                {"id": 3, "kind": "stop"},
            ]
            output = io.StringIO()
            with (
                mock.patch.object(
                    guest.sys,
                    "argv",
                    ["guest", temp, str(fb), "123", "xdotool", "stepped", "0.25"],
                ),
                mock.patch.object(
                    guest.sys,
                    "stdin",
                    io.StringIO("\n".join(map(json.dumps, requests))),
                ),
                mock.patch.object(guest.sys, "stdout", output),
                mock.patch.object(guest.subprocess, "run", side_effect=run),
                mock.patch.object(guest.time, "sleep", side_effect=sleep),
            ):
                self.assertEqual(guest.main(), 0)
            replies = [json.loads(line) for line in output.getvalue().splitlines()]
            self.assertEqual(replies[1]["capture_ms"], 1200)
            self.assertAlmostEqual(
                replies[2]["capture_ms"] - replies[1]["capture_ms"], 116.6666666667
            )
            self.assertAlmostEqual(
                replies[3]["capture_ms"] - replies[2]["capture_ms"], 66.6666666667
            )
            self.assertIn(["keydown", "Left"], calls)
            self.assertIn(["keyup", "Left"], calls)

    def test_valid_frame_becomes_top_down_bitmap(self):
        result = guest.xwd_to_bmp(framebuffer())
        self.assertEqual(result[:2], b"BM")
        self.assertEqual(len(result), 54 + 480 * 720 * 4)
        self.assertEqual(struct.unpack_from("<ii", result, 18), (480, -720))

    def test_rejects_wrong_format_and_truncation(self):
        data = bytearray(framebuffer())
        struct.pack_into(">I", data, 16, 999)
        with self.assertRaises(ValueError):
            guest.xwd_to_bmp(data)
        with self.assertRaises(ValueError):
            guest.xwd_to_bmp(framebuffer()[:-1])

    def test_guest_releases_an_action_and_rejects_unknown_keys(self):
        with tempfile.TemporaryDirectory() as temp:
            fb = Path(temp) / "screen.xwd"
            fb.write_bytes(framebuffer())
            calls = []

            def run(args, **kwargs):
                calls.append(args[1:])
                return mock.Mock(stdout="123\n")

            requests = [
                json.dumps(
                    {"id": 0, "kind": "action", "key": "LEFT", "duration_ms": 1}
                ),
                json.dumps(
                    {"id": 1, "kind": "action", "key": "ENTER", "duration_ms": 1}
                ),
            ]
            with (
                mock.patch.object(
                    guest.sys, "argv", ["guest", temp, str(fb), "123", "xdotool"]
                ),
                mock.patch.object(guest.sys, "stdin", io.StringIO("\n".join(requests))),
                mock.patch.object(guest.sys, "stdout", io.StringIO()),
                mock.patch.object(guest.subprocess, "run", side_effect=run),
                mock.patch.object(guest.time, "sleep"),
            ):
                self.assertEqual(guest.main(), 1)
            self.assertIn(["keydown", "Left"], calls)
            self.assertIn(["keyup", "Left"], calls)
            self.assertNotIn(["keydown", "ENTER"], calls)
            self.assertTrue((Path(temp) / "frame.bmp").is_file())

    def test_diagonal_action_releases_both_keys(self):
        with tempfile.TemporaryDirectory() as temp:
            fb = Path(temp) / "screen.xwd"
            fb.write_bytes(framebuffer())
            calls = []

            def run(args, **_kwargs):
                calls.append(args[1:])
                return mock.Mock(stdout="123\n")

            request = json.dumps(
                {"id": 0, "kind": "action", "key": "UP_LEFT", "duration_ms": 1}
            )
            with (
                mock.patch.object(
                    guest.sys, "argv", ["guest", temp, str(fb), "123", "xdotool"]
                ),
                mock.patch.object(guest.sys, "stdin", io.StringIO(request)),
                mock.patch.object(guest.sys, "stdout", io.StringIO()),
                mock.patch.object(guest.subprocess, "run", side_effect=run),
                mock.patch.object(guest.time, "sleep"),
            ):
                self.assertEqual(guest.main(), 0)
            self.assertEqual(
                [call for call in calls if call[0] in ("keydown", "keyup")],
                [
                    ["keydown", "Up"],
                    ["keydown", "Left"],
                    ["keyup", "Left"],
                    ["keyup", "Up"],
                ],
            )

    def test_diagonal_action_releases_first_key_if_second_press_fails(self):
        with tempfile.TemporaryDirectory() as temp:
            fb = Path(temp) / "screen.xwd"
            fb.write_bytes(framebuffer())
            calls = []

            def run(args, **_kwargs):
                calls.append(args[1:])
                if args[1:] == ["keydown", "Left"]:
                    raise guest.subprocess.CalledProcessError(1, args)
                return mock.Mock(stdout="123\n")

            request = json.dumps(
                {"id": 0, "kind": "action", "key": "UP_LEFT", "duration_ms": 1}
            )
            with (
                mock.patch.object(
                    guest.sys, "argv", ["guest", temp, str(fb), "123", "xdotool"]
                ),
                mock.patch.object(guest.sys, "stdin", io.StringIO(request)),
                mock.patch.object(guest.sys, "stdout", io.StringIO()),
                mock.patch.object(guest.subprocess, "run", side_effect=run),
                mock.patch.object(guest.time, "sleep"),
            ):
                self.assertEqual(guest.main(), 1)
            self.assertIn(["keyup", "Up"], calls)


if __name__ == "__main__":
    unittest.main()
