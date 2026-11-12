import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from tools.run_dodge_learning_study import run_study


class OfflineLearningStudyTests(unittest.TestCase):
    def test_runs_bounded_offline_trainers_sequentially_and_saves_results(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            benchmark = root / "benchmark"
            benchmark.mkdir()
            other = root / "other"
            other.mkdir()
            response = subprocess.CompletedProcess(
                [], 0, json.dumps({"output": "model"}), ""
            )
            with patch(
                "tools.run_dodge_learning_study.subprocess.run", return_value=response
            ) as execute:
                output, report = run_study(root, benchmark, [1, 2], [other], True)
            self.assertEqual(report["status"], "complete")
            self.assertEqual(len(execute.call_args_list), 4)
            self.assertEqual(json.loads((output / "report.json").read_text()), report)
            for call in execute.call_args_list:
                self.assertEqual(call.kwargs["timeout"], 90)
                self.assertNotIn("shell", call.kwargs)
                self.assertIn("--test-seeds", call.args[0])
            self.assertIn("--include-holds", execute.call_args_list[1].args[0])
            self.assertIn("--also-benchmark", execute.call_args_list[2].args[0])
            self.assertIn("--balance-games", execute.call_args_list[3].args[0])

    def test_failure_is_recorded_and_never_marked_complete(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            failed = subprocess.CompletedProcess([], 1, "", "bad evidence")
            with patch(
                "tools.run_dodge_learning_study.subprocess.run", return_value=failed
            ):
                output, report = run_study(root, root, [3])
            self.assertEqual(report["status"], "incomplete")
            self.assertTrue(all(x["status"] == "failed" for x in report["experiments"]))
            self.assertEqual(
                json.loads((output / "report.json").read_text())["status"], "incomplete"
            )

    def test_duplicate_test_seeds_are_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaisesRegex(ValueError, "distinct"):
                run_study(temp, temp, [1, 1])


if __name__ == "__main__":
    unittest.main()
