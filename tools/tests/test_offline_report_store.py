import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from check_diego_report import load_report
from offline_report_store import save_report


class ReportStoreTests(unittest.TestCase):
    def setUp(self):
        fixtures = (
            Path(__file__).resolve().parents[1] / "fixtures/diego_observer/v1/valid"
        )
        self.report = json.loads(next(fixtures.glob("*.json")).read_text())

    def test_valid_report_replaces_existing_and_leaves_no_temporary_files(self):
        with tempfile.TemporaryDirectory() as folder:
            target = Path(folder) / "report.json"
            target.write_text("old report")
            save_report(target, self.report)
            self.assertEqual(load_report(target), self.report)
            self.assertEqual(list(Path(folder).iterdir()), [target])

    def test_invalid_nonfinite_and_oversized_reports_preserve_existing(self):
        for changes in (
            {"inputSent": True},
            {"extra": float("nan")},
            {"extra": "x" * 262144},
        ):
            with self.subTest(
                changes=list(changes)
            ), tempfile.TemporaryDirectory() as folder:
                target = Path(folder) / "report.json"
                target.write_bytes(b"original")
                with self.assertRaises(ValueError):
                    save_report(target, dict(self.report, **changes))
                self.assertEqual(target.read_bytes(), b"original")
                self.assertEqual(list(Path(folder).iterdir()), [target])

    def test_locked_destination_preserves_existing_and_cleans_temporary(self):
        with tempfile.TemporaryDirectory() as folder:
            target = Path(folder) / "report.json"
            target.write_bytes(b"original")
            with patch.object(Path, "replace", side_effect=PermissionError("locked")):
                with self.assertRaises(PermissionError):
                    save_report(target, self.report)
            self.assertEqual(target.read_bytes(), b"original")
            self.assertEqual(list(Path(folder).iterdir()), [target])
