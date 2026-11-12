"""Unit tests for the Maelstrom Read and Repeat Macro Pipeline."""

import math
import unittest
from tools.macro_pipeline import (
    MacroCompiler,
    MacroExecutor,
    MacroNode,
    MacroRecorder,
    RawInputEvent,
    TelemetrySnapshot,
    macro_from_dict,
    macro_to_dict,
    simplify_path_rdp,
)
import json
import tempfile
from pathlib import Path


class TestMacroPipeline(unittest.TestCase):
    def test_simplify_path_rdp(self):
        # Straight line of 5 points: should simplify to start and end
        straight = [(float(i * 10), 0.0, 0.0) for i in range(5)]
        simplified = simplify_path_rdp(straight, epsilon=10.0)
        self.assertEqual(len(simplified), 2)
        self.assertEqual(simplified[0], (0.0, 0.0, 0.0))
        self.assertEqual(simplified[-1], (40.0, 0.0, 0.0))

        # Path with a 90 degree turn
        corner = [
            (0.0, 0.0, 0.0),
            (50.0, 0.0, 0.0),
            (100.0, 0.0, 0.0),
            (100.0, 50.0, 0.0),
            (100.0, 100.0, 0.0),
        ]
        simplified_corner = simplify_path_rdp(corner, epsilon=20.0)
        self.assertEqual(len(simplified_corner), 3)
        self.assertEqual(simplified_corner[1], (100.0, 0.0, 0.0))

    def test_recorder_stream_pairing(self):
        recorder = MacroRecorder()
        # Telemetry samples every 50ms
        for t in range(0, 300, 50):
            recorder.record_telemetry(
                TelemetrySnapshot(time_ms=float(t), x=float(t), y=0.0, z=0.0, yaw=0.0)
            )

        # Input event at 108ms (nearest is 100ms)
        recorder.record_input(time_ms=108.0, event_type="key_down", control="W")
        # Input event at 240ms (nearest is 250ms)
        recorder.record_input(time_ms=240.0, event_type="key_down", control="X")

        paired = recorder.pair_streams()
        self.assertEqual(len(paired), 2)
        self.assertEqual(paired[0].input_event.control, "W")
        self.assertEqual(paired[0].telemetry.time_ms, 100.0)
        self.assertEqual(paired[1].input_event.control, "X")
        self.assertEqual(paired[1].telemetry.time_ms, 250.0)

    def test_compiler_synthesizes_macro_nodes(self):
        recorder = MacroRecorder()
        # Simulate walking along a path
        recorder.record_telemetry(TelemetrySnapshot(time_ms=0, x=0, y=0, z=0, yaw=0))
        recorder.record_input(time_ms=10, event_type="key_down", control="W")

        recorder.record_telemetry(TelemetrySnapshot(time_ms=100, x=50, y=0, z=0, yaw=0))
        recorder.record_input(time_ms=110, event_type="key_down", control="W")

        recorder.record_telemetry(
            TelemetrySnapshot(time_ms=200, x=100, y=50, z=0, yaw=0.5)
        )
        recorder.record_input(time_ms=210, event_type="key_down", control="W")

        # Then press X to interact
        recorder.record_telemetry(
            TelemetrySnapshot(time_ms=300, x=100, y=50, z=0, yaw=0.5, dialog_open=True)
        )
        recorder.record_input(time_ms=310, event_type="key_down", control="X")

        # Then click UI dialog
        recorder.record_telemetry(
            TelemetrySnapshot(
                time_ms=400,
                x=100,
                y=50,
                z=0,
                yaw=0.5,
                dialog_open=True,
                active_window="wndDialogMain",
            )
        )
        recorder.record_input(
            time_ms=410, event_type="click", control="Left", x=540, y=680
        )

        paired = recorder.pair_streams()
        compiler = MacroCompiler(waypoint_epsilon=10.0)
        nodes = compiler.compile(paired)

        # Expected: 1 navigation node, 1 interact key node, 1 click UI node
        self.assertEqual(len(nodes), 3)
        self.assertEqual(nodes[0].kind, "navigate_path")
        self.assertGreaterEqual(len(nodes[0].payload["waypoints"]), 2)

        self.assertEqual(nodes[1].kind, "interact_key")
        self.assertEqual(nodes[1].payload["key"], "X")

        self.assertEqual(nodes[2].kind, "click_ui")
        self.assertEqual(nodes[2].payload["client_x"], 540)
        self.assertEqual(nodes[2].payload["client_y"], 680)
        self.assertEqual(nodes[2].payload["ui_window"], "wndDialogMain")

    def test_compiler_inserts_combat_gate(self):
        recorder = MacroRecorder()
        recorder.record_telemetry(
            TelemetrySnapshot(time_ms=0, x=0, y=0, z=0, yaw=0, in_combat=False)
        )
        recorder.record_input(time_ms=10, event_type="key_down", control="W")

        # Combat starts
        recorder.record_telemetry(
            TelemetrySnapshot(time_ms=100, x=10, y=0, z=0, yaw=0, in_combat=True)
        )
        recorder.record_input(time_ms=110, event_type="key_down", control="W")

        paired = recorder.pair_streams()
        compiler = MacroCompiler()
        nodes = compiler.compile(paired)

        # Expect combat gate inserted
        combat_nodes = [n for n in nodes if n.kind == "combat_gate"]
        self.assertEqual(len(combat_nodes), 1)

    def test_executor_playback_navigation_and_interactions(self):
        nodes = [
            MacroNode(
                node_id="nav_001",
                kind="navigate_path",
                payload={
                    "waypoints": [{"x": 100.0, "y": 0.0, "z": 0.0}],
                    "tolerance": 10.0,
                },
            ),
            MacroNode(
                node_id="interact_002",
                kind="interact_key",
                payload={"key": "X"},
            ),
            MacroNode(
                node_id="click_003",
                kind="click_ui",
                payload={"client_x": 500, "client_y": 600, "ui_window": "dialog"},
            ),
        ]

        executor = MacroExecutor(nodes)

        # Step 1: Player is at (0, 0), yaw = 0 (facing +X). Waypoint is (100, 0).
        snap1 = TelemetrySnapshot(time_ms=0, x=0.0, y=0.0, z=0.0, yaw=0.0)
        res1 = executor.tick(snap1)
        self.assertEqual(res1["action"], "STEER_WALK")
        self.assertEqual(res1["keys"], ["W"])
        self.assertEqual(res1["distance"], 100.0)

        # Step 2: Player reaches waypoint (100, 0)
        snap2 = TelemetrySnapshot(time_ms=1000, x=98.0, y=0.0, z=0.0, yaw=0.0)
        res2 = executor.tick(snap2)
        # Reached within tolerance 10.0 -> path completes
        self.assertEqual(res2["action"], "PATH_COMPLETE")

        # Step 3: Next node is interact_key "X"
        snap3 = TelemetrySnapshot(time_ms=1100, x=98.0, y=0.0, z=0.0, yaw=0.0)
        res3 = executor.tick(snap3)
        self.assertEqual(res3["action"], "SEND_KEY")
        self.assertEqual(res3["key"], "X")

        # Step 4: Next node is click_ui
        snap4 = TelemetrySnapshot(time_ms=1200, x=98.0, y=0.0, z=0.0, yaw=0.0)
        res4 = executor.tick(snap4)
        self.assertEqual(res4["action"], "SEND_CLICK")
        self.assertEqual(res4["x"], 500)
        self.assertEqual(res4["y"], 600)

        # Step 5: Finished
        snap5 = TelemetrySnapshot(time_ms=1300, x=98.0, y=0.0, z=0.0, yaw=0.0)
        res5 = executor.tick(snap5)
        self.assertEqual(res5["action"], "IDLE")
        self.assertEqual(res5["status"], "FINISHED")
        self.assertTrue(executor.is_finished)

    def test_executor_combat_interception(self):
        nodes = [
            MacroNode(
                node_id="nav_001",
                kind="navigate_path",
                payload={
                    "waypoints": [{"x": 100.0, "y": 0.0, "z": 0.0}],
                    "tolerance": 10.0,
                },
            )
        ]
        executor = MacroExecutor(nodes)

        # In combat unexpectedly during nav
        snap_combat = TelemetrySnapshot(
            time_ms=0, x=10.0, y=0.0, z=0.0, yaw=0.0, in_combat=True
        )
        res = executor.tick(snap_combat)
        self.assertEqual(res["action"], "INTERCEPT_COMBAT")
        self.assertEqual(res["routine"], "combat-turn-decision")

    def test_serialization_roundtrip(self):
        nodes = [
            MacroNode(
                "n1", "navigate_path", {"waypoints": [{"x": 1.0, "y": 2.0, "z": 0.0}]}
            ),
            MacroNode("n2", "interact_key", {"key": "X"}),
            MacroNode(
                "n3",
                "click_ui",
                {"client_x": 100, "client_y": 200, "ui_window": "dialog"},
            ),
            MacroNode("n4", "combat_gate", {"resume_on_peace": True}),
        ]
        as_dict = macro_to_dict(nodes, title="test-macro")
        self.assertEqual(as_dict["schema"], "maelstrom.macro-graph.v1")
        self.assertEqual(as_dict["title"], "test-macro")
        self.assertEqual(as_dict["node_count"], 4)

        restored = macro_from_dict(as_dict)
        self.assertEqual(len(restored), 4)
        self.assertEqual(restored[0].kind, "navigate_path")
        self.assertEqual(restored[1].payload["key"], "X")
        self.assertEqual(restored[2].payload["ui_window"], "dialog")
        self.assertEqual(restored[3].payload["resume_on_peace"], True)

    def test_cli_compile(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            tmp = Path(tmpdir)
            inp_file = tmp / "input-events.json"
            tel_file = tmp / "telemetry.json"
            out_file = tmp / "macro.json"

            inp_data = {
                "Events": [
                    {"TimeMs": 10.0, "Type": "key_down", "Control": "W"},
                    {"TimeMs": 100.0, "Type": "key_down", "Control": "X"},
                ]
            }
            tel_data = [
                {"time_ms": 10.0, "x": 0.0, "y": 0.0, "z": 0.0, "yaw": 0.0},
                {"time_ms": 100.0, "x": 50.0, "y": 0.0, "z": 0.0, "yaw": 0.0},
            ]
            inp_file.write_text(json.dumps(inp_data))
            tel_file.write_text(json.dumps(tel_data))

            from tools.macro_pipeline import main
            import sys

            old_argv = sys.argv
            try:
                sys.argv = [
                    "macro_pipeline.py",
                    "compile",
                    "--inputs",
                    str(inp_file),
                    "--telemetry",
                    str(tel_file),
                    "--out",
                    str(out_file),
                ]
                code = main()
                self.assertEqual(code, 0)
                self.assertTrue(out_file.exists())
                compiled = json.loads(out_file.read_text())
                self.assertEqual(compiled["schema"], "maelstrom.macro-graph.v1")
            finally:
                sys.argv = old_argv


if __name__ == "__main__":
    unittest.main()
