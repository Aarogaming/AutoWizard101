import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from benchmark_dodge_scores import summarize
from dodge_baseline_cache import verified_baseline
from tools.tests.test_benchmark_dodge_scores import MANIFEST, write_report


class BaselineReuseTests(unittest.TestCase):
    def prepare(self, temp):
        root = Path(temp).resolve()
        app = root / "application.exe"
        pack = root / "pack.json"
        app.write_text("application")
        pack.write_text("pack")
        benchmark = root / ".local/dodge-training/study/benchmarks/00"
        benchmark.mkdir(parents=True)
        runs = root / ".local/wsl-dodge-runs"
        runs.mkdir(parents=True)
        digest = hashlib.sha256(pack.read_bytes()).hexdigest()
        identity = {"Id": "test", "Version": "1", "Sha256": digest}
        reports = [
            write_report(runs, seed, score, pack=identity)
            for seed, score in [(1, 40), (2, 60)]
        ]
        (benchmark / "manifest.json").write_text(json.dumps(MANIFEST))
        (benchmark / "fingerprints.json").write_text(json.dumps({"runtime": "same"}))
        (benchmark / "summary.json").write_text(
            json.dumps(summarize(MANIFEST, reports))
        )
        return root, app, pack, benchmark, reports

    @patch("dodge_baseline_cache.fingerprints", return_value={"runtime": "same"})
    def test_reuses_only_unchanged_complete_evidence(self, unused):
        with tempfile.TemporaryDirectory() as temp:
            root, app, pack, benchmark, reports = self.prepare(temp)
            self.assertEqual(
                verified_baseline(root, app, pack, MANIFEST, benchmark)["mean_score"],
                50,
            )
            changed = json.loads(reports[0].read_text())
            changed["evaluation"]["score"] = 41
            reports[0].write_text(json.dumps(changed))
            with self.assertRaisesRegex(ValueError, "evidence"):
                verified_baseline(root, app, pack, MANIFEST, benchmark)

    @patch("dodge_baseline_cache.fingerprints", return_value={"runtime": "changed"})
    def test_rejects_a_changed_runtime(self, unused):
        with tempfile.TemporaryDirectory() as temp:
            root, app, pack, benchmark, _ = self.prepare(temp)
            with self.assertRaisesRegex(ValueError, "changed"):
                verified_baseline(root, app, pack, MANIFEST, benchmark)

    @patch("dodge_baseline_cache.fingerprints", return_value={"runtime": "same"})
    def test_rejects_different_conditions_and_a_different_pack(self, unused):
        with tempfile.TemporaryDirectory() as temp:
            root, app, pack, benchmark, _ = self.prepare(temp)
            with self.assertRaisesRegex(ValueError, "conditions"):
                verified_baseline(root, app, pack, dict(MANIFEST, seeds=[1]), benchmark)
            pack.write_text("different pack")
            with self.assertRaisesRegex(ValueError, "evidence"):
                verified_baseline(root, app, pack, MANIFEST, benchmark)


if __name__ == "__main__":
    unittest.main()
