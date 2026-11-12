import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from check_diego_report import MAX_BYTES, load_report

ROOT = Path(__file__).resolve().parents[2]
FIXTURES = ROOT / "tools/fixtures/diego_observer/v1"


class DiegoCoordinationTests(unittest.TestCase):
    def test_pinned_fixture_bytes_and_expected_dispositions(self):
        manifest = json.loads((FIXTURES / "manifest.json").read_text())
        self.assertEqual(manifest["fixtureSetVersion"], 1)
        self.assertTrue(manifest["syntheticOnly"])
        self.assertIsNone(manifest["payloadVersionField"])
        for name, expected in manifest["files"].items():
            raw = (FIXTURES / name).read_bytes()
            with self.subTest(file=name):
                self.assertEqual(len(raw), expected["bytes"])
                self.assertEqual(hashlib.sha256(raw).hexdigest(), expected["sha256"])
        for case in manifest["cases"]:
            with self.subTest(case=case["case"]):
                if case["expect"] == "accept":
                    data = load_report(FIXTURES / case["path"])
                    self.assertFalse(data["inputSent"])
                else:
                    with self.assertRaises(ValueError):
                        load_report(FIXTURES / case["path"])

    def test_headless_cli_needs_no_tk_and_matches_pinned_single_frame(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / "report.json"
            command = [
                sys.executable,
                str(ROOT / "tools/diego_report.py"),
                "--pack",
                str(FIXTURES / "pack"),
                "--frame",
                str(FIXTURES / "frames/frame-0000.png"),
                "--output",
                str(output),
            ]
            subprocess.run(command, check=True, capture_output=True, timeout=30)
            actual = load_report(output)
            expected = load_report(FIXTURES / "valid/single-frame.json")
            expected["syntheticDemo"] = (
                False  # CLI cannot infer a user-supplied image's provenance.
            )
            self.assertEqual(actual, expected)
            check = "import pathlib,runpy,sys;sys.path.insert(0,str(pathlib.Path(sys.argv[1]).parent));runpy.run_path(sys.argv[1],run_name='report_import');assert 'tkinter' not in sys.modules"
            subprocess.run(
                [sys.executable, "-c", check, str(ROOT / "tools/diego_report.py")],
                check=True,
                capture_output=True,
                timeout=30,
            )

    def test_unknown_and_additional_fields_remain_explicit(self):
        unknown = load_report(FIXTURES / "valid/unknown.json")
        self.assertEqual(unknown["detections"], [])
        self.assertIn("Unknown", unknown["suggestions"][0])
        extra = load_report(FIXTURES / "valid/additional-field.json")
        self.assertIn("futureReadOnlyMetadata", extra)

    def test_size_limit_is_bounded_before_parsing(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "large.json"
            path.write_bytes(b" " * (MAX_BYTES + 1))
            with self.assertRaisesRegex(ValueError, "exceeds"):
                load_report(path)


if __name__ == "__main__":
    unittest.main()
