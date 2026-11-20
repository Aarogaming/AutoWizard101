import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from analyze_dodge_timing import summarize_reports


class DodgeTimingTests(unittest.TestCase):
    def test_missing_timings_are_not_counted_as_zero(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "report.json"
            report = {
                "mode": "wsl-private-endurance",
                "stop_reason": "game-over",
                "actions": [
                    {
                        "observation_wait_wall_ms": 20,
                        "decision_work_wall_ms": 3,
                        "action_wait_wall_ms": 40,
                        "observation_reused": False,
                    },
                    {
                        "observation_wait_wall_ms": 10,
                        "decision_work_wall_ms": 5,
                        "action_wait_wall_ms": 60,
                        "observation_reused": True,
                    },
                    {},
                ],
            }
            path.write_text(json.dumps(report))
            result = summarize_reports([path])
            self.assertEqual(result["action_records"], 3)
            self.assertEqual(result["reused_observations"], 1)
            self.assertEqual(
                result["timings"]["observation_wait_wall_ms"]["samples"], 2
            )
            self.assertEqual(
                result["timings"]["observation_wait_wall_ms"]["median_ms"], 15
            )
            self.assertEqual(result["timings"]["action_wait_wall_ms"]["p95_ms"], 60)
            report["actions"][0]["decision_work_wall_ms"] = -1
            path.write_text(json.dumps(report))
            with self.assertRaisesRegex(ValueError, "wall-clock"):
                summarize_reports([path])


if __name__ == "__main__":
    unittest.main()
