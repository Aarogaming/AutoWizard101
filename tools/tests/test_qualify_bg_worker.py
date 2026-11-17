import json
import subprocess
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import qualify_bg_worker as harness  # noqa: E402


class QualificationHarnessTests(unittest.TestCase):
    def test_plan_is_bounded_and_has_unfocused_gate(self):
        result = harness.plan(3)
        self.assertEqual(3, result["maxCycles"])
        self.assertEqual(7, len(result["steps"]))
        self.assertTrue(any("not foreground" in s["gate"] for s in result["steps"]))
        self.assertTrue(any("hook restored" in s["gate"] for s in result["steps"]))
        self.assertIn("PLAN ONLY", result["note"])

    def test_bad_cycles_rejected(self):
        with self.assertRaises(ValueError):
            harness.plan(2)
        with self.assertRaises(ValueError):
            harness.plan(6)

    def test_live_refused_without_owner_token_and_reviewed_pin(self):
        self.assertEqual("missing-owner-token", harness.refuse_live(None, True))
        self.assertEqual(
            "no-reviewed-pin", harness.refuse_live(harness.LIVE_TOKEN, False)
        )
        self.assertEqual("", harness.refuse_live(harness.LIVE_TOKEN, True))

    def test_cli_live_never_executes(self):
        script = Path(__file__).resolve().parents[1] / "qualify_bg_worker.py"
        proc = subprocess.run(
            [
                sys.executable,
                str(script),
                "--live",
                "--token",
                harness.LIVE_TOKEN,
                "--pin-reviewed",
            ],
            capture_output=True,
            text=True,
            timeout=15,
        )
        self.assertEqual(2, proc.returncode)
        self.assertEqual(
            "live-execution-not-implemented", json.loads(proc.stdout)["reason"]
        )

    def test_cli_dry_run_is_deterministic(self):
        script = Path(__file__).resolve().parents[1] / "qualify_bg_worker.py"
        cmd = [sys.executable, str(script), "--cycles", "3"]
        first = subprocess.run(cmd, capture_output=True, text=True, timeout=15)
        second = subprocess.run(cmd, capture_output=True, text=True, timeout=15)
        self.assertEqual(0, first.returncode)
        self.assertEqual(first.stdout, second.stdout)
        self.assertEqual(
            "maelstrom-bg-worker-qualification-plan-v1",
            json.loads(first.stdout)["kind"],
        )


if __name__ == "__main__":
    unittest.main()
