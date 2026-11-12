import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from compare_dodge_benchmarks import compare
from tools.tests.test_benchmark_dodge_scores import MANIFEST, write_report


def suite(parent, name, scores):
    root = Path(parent) / name
    root.mkdir()
    reports = [write_report(root, index + 1, score) for index, score in enumerate(scores)]
    (root / "manifest.json").write_text(json.dumps(MANIFEST), encoding="utf-8")
    (root / "summary.json").write_text(json.dumps({"games": [{"report": str(path)} for path in reports]}), encoding="utf-8")
    return root


class ComparisonTests(unittest.TestCase):
    def test_compares_every_predeclared_game(self):
        with tempfile.TemporaryDirectory() as temp:
            baseline = suite(temp, "baseline", [40, 20])
            candidate = suite(temp, "candidate", [60, 30])
            result = compare(baseline, candidate)
            self.assertEqual(result["mean_difference"], 15)
            self.assertEqual([row["difference"] for row in result["games"]], [20, 10])

    def test_rejects_incomplete_or_different_conditions(self):
        with tempfile.TemporaryDirectory() as temp:
            baseline = suite(temp, "baseline", [40, 20])
            candidate = suite(temp, "candidate", [60])
            with self.assertRaises(ValueError):
                compare(baseline, candidate)
            manifest = dict(MANIFEST, speed=0.5)
            (candidate / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
            with self.assertRaises(ValueError):
                compare(baseline, candidate)


if __name__ == "__main__":
    unittest.main()
