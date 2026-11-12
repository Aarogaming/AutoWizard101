"""Integrity checks for a non-executable audit; no application imports or game access."""

from contextlib import redirect_stdout
from copy import deepcopy
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import check_capability_inventory as checker


class CapabilityInventoryTests(unittest.TestCase):
    def setUp(self):
        local = checker.ROOT / ".local"
        local.mkdir(exist_ok=True)
        temporary = tempfile.TemporaryDirectory(prefix="capability-tests-", dir=local)
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        (self.root / "source.py").write_text(
            "raise RuntimeError('This source must never execute')\n"
            "def source_anchor(): pass\n",
            encoding="utf-8",
        )
        (self.root / "evidence.md").write_text("Scoped evidence.\n", encoding="utf-8")
        self.row = {
            "id": "audit.example",
            "title": "Example",
            "scope": "Shared framework",
            "source_refs": [{"path": "source.py", "anchor": "def source_anchor("}],
            "callable_surfaces": ["Example callable, not invoked"],
            "limits": ["Source only"],
            "deck_binding": {
                "status": "not-connected",
                "detail": "Not a deck adapter.",
            },
            "verification": {
                "source": "present",
                "installed": "not-assessed",
                "live": "not-assessed",
            },
            "evidence": [{"path": "evidence.md", "scope": "Historical scope only."}],
            "next_gap": "Qualify independently.",
            "related": [],
        }
        self.inventory = {
            "schema_version": 1,
            "kind": "maelstrom-capability-audit",
            "executable": False,
            "audit_date": "2026-11-11",
            "coverage": "selected-workflow-families-not-exhaustive",
            "verification_boundary": "Source only; tests and live outcomes not assessed.",
            "capabilities": [self.row],
        }

    def errors(self):
        return checker.validate(self.inventory, self.root)

    def test_valid_source_is_read_not_executed(self):
        self.assertEqual([], self.errors())

    def test_partial_nonexecutable_boundary_is_required(self):
        self.inventory["executable"] = True
        self.inventory["coverage"] = "everything"
        self.assertEqual(2, len(self.errors()))

    def test_unknown_fields_cannot_smuggle_dispatch_instructions(self):
        self.row["execute"] = "arbitrary-command"
        self.assertTrue(any("invalid fields" in e for e in self.errors()))

    def test_duplicate_ids_and_unknown_related_ids_are_rejected(self):
        self.inventory["capabilities"].append(deepcopy(self.row))
        self.row["related"] = ["audit.missing"]
        self.assertTrue(any("duplicate id" in e for e in self.errors()))
        self.assertTrue(any("invalid related" in e for e in self.errors()))

    def test_valid_cross_reference_is_accepted(self):
        second = deepcopy(self.row)
        second["id"] = "audit.second"
        self.inventory["capabilities"].append(second)
        self.row["related"] = ["audit.second"]
        self.assertEqual([], self.errors())

    def test_path_escape_and_non_normalized_paths_are_rejected(self):
        for path in (
            "../source.py",
            "/source.py",
            "C:/source.py",
            "./source.py",
            "a//b",
            "a\\b",
        ):
            with self.subTest(path=path):
                self.row["source_refs"][0]["path"] = path
                self.assertTrue(any("normalized" in e for e in self.errors()))

    def test_resolved_escape_is_rejected(self):
        original = Path.resolve
        source = self.root / "source.py"

        def redirected(path, *args, **kwargs):
            if path == source:
                return self.root.parent / "outside.py"
            return original(path, *args, **kwargs)

        with patch.object(Path, "resolve", redirected):
            self.assertTrue(any("escapes repository" in e for e in self.errors()))

    def test_missing_reference_and_anchor_are_rejected(self):
        self.row["source_refs"][0]["anchor"] = "no_such_function"
        self.row["evidence"][0]["path"] = "missing.md"
        errors = self.errors()
        self.assertTrue(any("anchor missing" in e for e in errors))
        self.assertTrue(any("missing or invalid" in e for e in errors))

    def test_directory_is_not_an_evidence_file(self):
        (self.root / "folder").mkdir()
        self.row["evidence"][0]["path"] = "folder"
        self.assertTrue(any("not a file" in e for e in self.errors()))

    def test_source_read_limits_are_enforced(self):
        with patch.object(checker, "MAX_SOURCE_BYTES", 8):
            self.assertTrue(any("budget exceeded" in e for e in self.errors()))
        with patch.object(checker, "MAX_TOTAL_SOURCE_BYTES", 8):
            self.assertTrue(any("budget exceeded" in e for e in self.errors()))

    def test_source_audit_cannot_promote_installation_or_live_claims(self):
        self.row["verification"]["installed"] = "installed"
        self.row["verification"]["live"] = "qualified"
        self.assertTrue(any("cannot grant" in e for e in self.errors()))

    def test_malformed_types_report_errors_without_crashing(self):
        for field, value in (
            ("id", []),
            ("source_refs", "bad"),
            ("related", [{}]),
            ("deck_binding", {"status": [], "detail": "bad"}),
            ("evidence", [None]),
            ("verification", None),
        ):
            with self.subTest(field=field):
                broken = deepcopy(self.inventory)
                broken["capabilities"][0][field] = value
                self.assertTrue(checker.validate(broken, self.root))
        self.assertTrue(checker.validate([], self.root))
        self.inventory["schema_version"] = True
        self.assertTrue(self.errors())

    def test_invalid_date_and_record_limit(self):
        self.inventory["audit_date"] = "2026-99-99"
        self.assertTrue(self.errors())
        self.inventory["capabilities"] = [self.row] * 65
        self.assertTrue(any("1 to 64" in e for e in self.errors()))

    def test_json_duplicate_keys_malformed_and_nonfinite_rejected(self):
        path = self.root / "inventory.json"
        for content in ('{"x":1,"x":2}', '{"x":NaN}', '{"x":Infinity}', "{"):
            with self.subTest(content=content):
                path.write_text(content, encoding="utf-8")
                with self.assertRaises(ValueError):
                    checker.load_inventory(path)

    def test_oversized_inventory_is_rejected_before_json_parse(self):
        path = self.root / "inventory.json"
        path.write_bytes(b" " * (checker.MAX_BYTES + 1))
        with self.assertRaisesRegex(ValueError, "exceeds"):
            checker.load_inventory(path)

    def test_cli_failure_returns_nonzero(self):
        path = self.root / "broken.json"
        path.write_text("{", encoding="utf-8")
        with redirect_stdout(io.StringIO()) as output:
            self.assertEqual(1, checker.main([str(path)]))
        self.assertIn("ERROR", output.getvalue())

    def test_repository_inventory_references_and_boundaries(self):
        inventory = checker.load_inventory(checker.DEFAULT_INVENTORY)
        self.assertEqual([], checker.validate(inventory))
        rows = {row["id"]: row for row in inventory["capabilities"]}
        self.assertGreaterEqual(len(rows), 20)
        self.assertEqual(
            "library-only", rows["audit.pet-dance"]["deck_binding"]["status"]
        )
        self.assertEqual("library-only", rows["audit.combat"]["deck_binding"]["status"])
        self.assertEqual(
            "not-connected", rows["audit.sign-in"]["deck_binding"]["status"]
        )


if __name__ == "__main__":
    unittest.main()
