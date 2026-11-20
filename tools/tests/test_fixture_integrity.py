import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from check_fixture_integrity import verify


class FixtureIntegrityTests(unittest.TestCase):
    def make_manifest(self, root):
        (root / "frame.bin").write_bytes(b"frame")
        data = {
            "fixtureSetVersion": 1,
            "kind": "autowizard101-offline-observer-fixtures",
            "files": {
                "frame.bin": {
                    "bytes": 5,
                    "sha256": hashlib.sha256(b"frame").hexdigest(),
                }
            },
        }
        path = root / "manifest.json"
        path.write_text(json.dumps(data))
        return path, data

    def test_actual_fixture_pack(self):
        root = Path(__file__).resolve().parents[1]
        result = verify(root / "fixtures/diego_observer/v1/manifest.json")
        self.assertTrue(result["passed"], result)
        self.assertEqual(result["checked"], 23)

    def test_changed_same_size_missing_and_growing_files_fail(self):
        for mutation in (b"other", None, b"longer"):
            with self.subTest(
                mutation=mutation
            ), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                path, _ = self.make_manifest(root)
                if mutation is None:
                    (root / "frame.bin").unlink()
                else:
                    (root / "frame.bin").write_bytes(mutation)
                self.assertFalse(verify(path)["passed"])

    def test_escape_absolute_and_invalid_metadata_fail(self):
        for name, change in (
            ("../outside", {}),
            ("C:/outside", {}),
            ("/outside", {}),
            ("frame.bin", {"bytes": True}),
            ("frame.bin", {"sha256": "bad"}),
        ):
            with self.subTest(
                name=name, change=change
            ), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                path, data = self.make_manifest(root)
                data["files"] = {name: dict(data["files"]["frame.bin"], **change)}
                path.write_text(json.dumps(data))
                self.assertFalse(verify(path)["passed"])

    def test_empty_duplicate_and_unknown_version_fail(self):
        with tempfile.TemporaryDirectory() as folder:
            path, data = self.make_manifest(Path(folder))
            for changes in (
                {"files": {}},
                {"fixtureSetVersion": True},
                {"fixtureSetVersion": 2},
            ):
                path.write_text(json.dumps(dict(data, **changes)))
                with self.assertRaises(ValueError):
                    verify(path)
            path.write_text('{"files":{},"files":{}}')
            with self.assertRaisesRegex(ValueError, "Duplicate"):
                verify(path)
