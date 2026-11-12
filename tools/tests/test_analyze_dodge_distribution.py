import sys
import json
from pathlib import Path
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from analyze_dodge_distribution import analyze, distribution
from benchmark_dodge_scores import summarize
from tools.tests.test_benchmark_dodge_scores import MANIFEST, write_report


class DodgeDistributionTests(unittest.TestCase):
    def test_outlier_does_not_hide_the_median_or_uncertainty(self):
        result = distribution([46, 11, 8, 169], resamples=1000)
        self.assertEqual(result["mean"], 58.5)
        self.assertEqual(result["median"], 28.5)
        self.assertEqual(result["games_at_or_above_50"], 1)
        self.assertLess(result["bootstrap_mean_interval_95"][0], 50)
        self.assertFalse(result["bootstrap_lower_bound_at_least_50"])
        self.assertEqual(result, distribution([46, 11, 8, 169], resamples=1000))

    def test_identical_games_and_invalid_inputs(self):
        result = distribution([60] * 10, resamples=100)
        self.assertEqual(result["bootstrap_mean_interval_95"], [60, 60])
        self.assertTrue(result["bootstrap_lower_bound_at_least_50"])
        self.assertEqual(result["sample_standard_deviation"], 0)
        for scores in ([], [1], [1, -1], [1, True], [1, 1.5]):
            with self.assertRaises(ValueError):
                distribution(scores)

    def test_only_complete_benchmarks_are_analyzed(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            first = write_report(root, 1, 40)
            (root / "manifest.json").write_text(json.dumps(MANIFEST))
            partial = summarize(MANIFEST, [first])
            (root / "summary.json").write_text(json.dumps(partial))
            with self.assertRaisesRegex(ValueError, "entire planned benchmark"):
                analyze(root)
            second = write_report(root, 2, 60)
            complete = summarize(MANIFEST, [first, second])
            (root / "summary.json").write_text(json.dumps(complete))
            self.assertEqual(analyze(root)["mean"], 50)


if __name__ == "__main__":
    unittest.main()
