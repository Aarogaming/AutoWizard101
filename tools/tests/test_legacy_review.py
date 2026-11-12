import io
import json
from pathlib import Path
import stat
import sys
import tarfile
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from audit_w101_legacy import audit, safe_member, source_check
from map_w101_legacy import MAPPING, build_catalog
from verify_w101_legacy_components import definitions


class LegacyReviewTests(unittest.TestCase):
    def test_rejects_archive_escape_windows_aliases_and_empty_paths(self):
        for name in (
            "../escape.py",
            "/root.py",
            "C:/x.py",
            "a/../b.py",
            "a/NUL.txt",
            "a./b.py",
            ".",
            "",
            "a/file.py:stream",
        ):
            with self.subTest(name=name), self.assertRaises(ValueError):
                safe_member(name)
        self.assertEqual(safe_member("project\\source.py"), "project/source.py")

    def test_parsing_failure_is_not_mistaken_for_archive_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            archive = root / "source.zip"
            with zipfile.ZipFile(archive, "w") as bundle:
                bundle.writestr("project/bad.py", "def broken(:\n")
                bundle.writestr("project/good.json", "{}")
            report = audit(archive, root / "sources")
            self.assertEqual(report["integrity"], "passed")
            self.assertEqual((report["static_passed"], report["static_failed"]), (1, 1))
            self.assertFalse(report["application_launched"])
            self.assertEqual(report["live_test"], "not_run")

    def test_symlinks_skipped_and_existing_reviewed_source_preserved(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            archive = root / "source.zip"
            with zipfile.ZipFile(archive, "w") as bundle:
                bundle.writestr("project/code.py", "x = 1\n")
                link = zipfile.ZipInfo("link.py")
                link.create_system = 3
                link.external_attr = (stat.S_IFLNK | 0o777) << 16
                bundle.writestr(link, "../elsewhere")
            report = audit(archive, root / "sources")
            self.assertEqual(report["links_skipped"], ["link.py"])
            source = Path(report["source_copy"]) / "project/code.py"
            source.write_text("reviewed edit\n")
            repeated = audit(archive, root / "sources")
            self.assertEqual(repeated["integrity"], "review_failed")
            self.assertEqual(source.read_text(), "reviewed edit\n")

    def test_disguised_binary_bundle_copies_assets_only(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            archive = root / "petdance.zip"
            with tarfile.open(archive, "w") as bundle:
                for name, data in (
                    ("assets/up.png", b"fixture"),
                    ("start.py", b"raise RuntimeError()"),
                    ("bot.exe", b"binary"),
                ):
                    entry = tarfile.TarInfo(name)
                    entry.size = len(data)
                    bundle.addfile(entry, io.BytesIO(data))
            report = audit(archive, root / "sources")
            self.assertEqual(report["format"], "tar")
            self.assertEqual(report["members"], 3)
            self.assertEqual(report["source_files"], 1)
            self.assertFalse((Path(report["source_copy"]) / "start.py").exists())

    def test_jsonc_is_not_falsely_reported_as_broken_json(self):
        result = source_check("tsconfig.json", b'{ // comment\n"compilerOptions": {} }')
        self.assertEqual(result["status"], "not_run")
        self.assertEqual(result["kind"], "jsonc")

    def test_definition_probe_does_not_execute_entrypoint(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            file = root / "reviewed.py"
            file.write_text(
                "raise RuntimeError('entrypoint must not run')\ndef decide():\n    return 'left'\n"
            )
            with patch(
                "verify_w101_legacy_components.checked_source", return_value=file
            ):
                self.assertEqual(
                    definitions(root, "reviewed.py", ["decide"], {})["decide"](), "left"
                )

    def test_catalogue_preserves_failed_probes_and_never_promotes_live(self):
        audit_result = {
            "projects": [
                {
                    "archive": row[0],
                    "sha256": "a" * 64,
                    "integrity": "passed",
                    "static_passed": 1,
                    "static_failed": 0,
                }
                for row in MAPPING
            ]
        }
        components = {
            "complete": True,
            "checks": [
                {"archive": MAPPING[0][0], "test": "behavior", "status": "failed"}
            ],
        }
        catalog = build_catalog(audit_result, components, {"checks": []})
        self.assertEqual(len(catalog["entries"]), 25)
        self.assertEqual(catalog["entries"][0]["qualification"], "Repair required")
        self.assertTrue(all(not e["liveQualified"] for e in catalog["entries"]))
        self.assertGreater(
            sum("Navigation" in e["capabilities"] for e in catalog["entries"]), 1
        )
        audit_result["projects"].append({"archive": "new.zip"})
        with self.assertRaises(ValueError):
            build_catalog(audit_result, components, {"checks": []})
        with self.assertRaises(ValueError):
            build_catalog(
                {"projects": audit_result["projects"][:-1]},
                {"complete": False},
                {"checks": []},
            )

    def test_shipped_catalogue_contains_all_reviewed_archives(self):
        root = Path(__file__).resolve().parents[2]
        data = json.loads(
            (
                root
                / "ProjectMaelstrom/ProjectMaelstrom/Resources/LegacyW101Catalog.json"
            ).read_text(encoding="utf8")
        )
        self.assertEqual(
            {entry["archive"] for entry in data["entries"]}, {row[0] for row in MAPPING}
        )


if __name__ == "__main__":
    unittest.main()
