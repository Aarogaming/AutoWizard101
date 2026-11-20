"""Tiny offline imports; no backend, application, internet or game access."""

from contextlib import closing
import hashlib
import json
from pathlib import Path
import sqlite3
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import import_w101_knowledge as importer


class ImportW101KnowledgeTests(unittest.TestCase):
    def setUp(self):
        local = importer.ROOT / ".local"
        local.mkdir(exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(
            prefix="knowledge-test-", dir=local
        )
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.output = self.root / "output"
        self.source = self.root / "ledger.json"
        self.original = importer.DEFAULT_LEDGER.read_bytes()
        self.source.write_bytes(self.original)

    def import_fixture(self):
        return importer.import_ledger(self.source, self.output, "snacks")

    def modified(self, mutation):
        ledger = json.loads(self.original)
        mutation(ledger)
        self.source.write_text(json.dumps(ledger), encoding="utf-8")

    def assert_no_bundle(self):
        self.assertFalse((self.output / "snacks").exists())
        if self.output.exists():
            self.assertEqual(list(self.output.iterdir()), [])

    def test_actual_ledger_maps_all_sources_claims_and_scope_without_runtime(self):
        result = self.import_fixture()
        receipt = json.loads(
            (result / "import-receipt.json").read_text(encoding="utf-8")
        )
        database = result / "knowledge.sqlite"
        self.assertEqual(
            receipt["source_ledger_sha256"], hashlib.sha256(self.original).hexdigest()
        )
        self.assertEqual(
            receipt["database_sha256"],
            hashlib.sha256(database.read_bytes()).hexdigest(),
        )
        self.assertEqual(
            receipt["schema_sha256"],
            hashlib.sha256(importer.SCHEMA.read_bytes()).hexdigest(),
        )
        self.assertFalse(receipt["runtime_enabled"])
        self.assertFalse(receipt["current_client_qualified"])
        self.assertEqual(self.source.read_bytes(), self.original)
        self.assertEqual(
            set(p.name for p in result.iterdir()),
            {"knowledge.sqlite", "import-receipt.json"},
        )
        with closing(sqlite3.connect(database.as_uri() + "?mode=ro", uri=True)) as db:
            for table, expected in {
                "games": 1,
                "domains": 1,
                "entities": 8,
                "sources": 8,
                "source_revisions": 8,
                "claim_series": 9,
                "claims": 9,
                "claim_evidence": 12,
            }.items():
                self.assertEqual(
                    db.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0], expected
                )
                self.assertEqual(receipt["row_counts"][table], expected)
            for table in (
                "observations",
                "qualifications",
                "runtime_candidates",
                "candidate_claims",
            ):
                self.assertEqual(
                    db.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0], 0
                )
            self.assertEqual(db.execute("PRAGMA foreign_key_check").fetchall(), [])
            self.assertEqual(db.execute("PRAGMA integrity_check").fetchone()[0], "ok")
            self.assertEqual(
                db.execute(
                    "SELECT COUNT(*) FROM claims WHERE game_version IS NULL AND region IS NULL AND platform IS NULL AND realm='unknown' AND effective_from IS NULL AND confidence IS NULL"
                ).fetchone()[0],
                9,
            )
            bibliography = db.execute(
                "SELECT published_on, retrieved_on, retrieved_timezone, content_sha256 FROM source_revisions WHERE source_id LIKE '%inventory-item-feed'"
            ).fetchone()
            self.assertEqual(
                bibliography, ("2011-11-18", "2026-11-08", "America/New_York", None)
            )
            notes = json.loads(
                db.execute(
                    "SELECT revision_note FROM source_revisions WHERE source_id LIKE '%happiness-guide'"
                ).fetchone()[0]
            )
            self.assertIn("Test Realm", notes["release_scope"])
            self.assertEqual(notes["ledger_sha256"], receipt["source_ledger_sha256"])
            self.assertEqual(
                db.execute("SELECT DISTINCT uncertainty FROM claims").fetchall(),
                [("documented",)],
            )
            self.assertEqual(
                db.execute(
                    "SELECT COUNT(*) FROM claims WHERE revision_no=1 AND supersedes_claim_id IS NULL"
                ).fetchone()[0],
                9,
            )

    def test_missing_provenance_and_unknown_source_fail_before_publication(self):
        mutations = [
            lambda data: data["sources"][0].pop("retrieved_on"),
            lambda data: data["claims"][0].update(sources=[]),
            lambda data: data["claims"][0].update(sources=["invented-source"]),
            lambda data: data["sources"][0].update(retrieved_on="2026-02-30"),
        ]
        for mutation in mutations:
            with self.subTest(mutation=mutation):
                self.modified(mutation)
                with self.assertRaises(ValueError):
                    self.import_fixture()
                self.assert_no_bundle()

    def test_unknown_schema_or_execution_claims_are_not_silently_imported(self):
        for mutation in (
            lambda data: data.update(subsystem="quests"),
            lambda data: data.update(schema_version=True),
            lambda data: data.update(runtime_feeding_enabled=True),
            lambda data: data["claims"][0].update(runtime_qualified=True),
            lambda data: data["sources"][0].update(http_status=404),
            lambda data: data["sources"][0].update(
                url="https://secret:password@example.invalid/"
            ),
        ):
            with self.subTest(mutation=mutation):
                self.modified(mutation)
                with self.assertRaises(ValueError):
                    self.import_fixture()
                self.assert_no_bundle()

    def test_reimport_refuses_existing_bundle_and_preserves_database_and_receipt(self):
        bundle = self.import_fixture()
        before = {p.name: p.read_bytes() for p in bundle.iterdir()}
        with self.assertRaises(FileExistsError):
            self.import_fixture()
        self.assertEqual(before, {p.name: p.read_bytes() for p in bundle.iterdir()})
        self.assertFalse((self.output / ".snacks.import.lock").exists())

    def test_changed_ledger_gets_new_revision_identity_without_replacing_history(self):
        first = self.import_fixture()
        self.modified(
            lambda data: data["claims"][0].update(
                statement="Revised research statement; current client still unqualified.",
                evidence=["tentative"],
            )
        )
        second = importer.import_ledger(self.source, self.output, "revised")
        with closing(sqlite3.connect(first / "knowledge.sqlite")) as old, closing(
            sqlite3.connect(second / "knowledge.sqlite")
        ) as new:
            query = "SELECT claim_id, series_id, uncertainty FROM claims WHERE series_id LIKE '%training-context'"
            old_row, new_row = (
                old.execute(query).fetchone(),
                new.execute(query).fetchone(),
            )
            self.assertNotEqual(old_row[0], new_row[0])
            self.assertEqual(old_row[1], new_row[1])
            self.assertEqual(new_row[2], "tentative")
            with self.assertRaises(sqlite3.IntegrityError):
                new.execute("UPDATE claims SET statement='overwrite'")

    def test_database_failure_rolls_back_and_removes_only_its_staging_bundle(self):
        populate = importer._populate

        def fail_after_inserts(db, ledger, source_hash):
            populate(db, ledger, source_hash)
            raise sqlite3.IntegrityError("controlled fixture failure")

        with patch.object(importer, "_populate", side_effect=fail_after_inserts):
            with self.assertRaises(sqlite3.IntegrityError):
                self.import_fixture()
        self.assert_no_bundle()

    def test_receipt_failure_does_not_publish_an_unreceipted_database(self):
        with patch.object(
            importer,
            "_write_receipt",
            side_effect=OSError("controlled receipt failure"),
        ):
            with self.assertRaises(OSError):
                self.import_fixture()
        self.assert_no_bundle()

    def test_publication_failure_leaves_no_partial_database(self):
        with patch.object(
            importer.os, "rename", side_effect=OSError("controlled publication failure")
        ):
            with self.assertRaises(OSError):
                self.import_fixture()
        self.assert_no_bundle()

    def test_claim_text_is_data_and_cannot_execute_sql(self):
        statement = (
            "Research text: '); DROP TABLE claims; -- remains a literal statement."
        )
        self.modified(lambda data: data["claims"][0].update(statement=statement))
        bundle = self.import_fixture()
        with closing(sqlite3.connect(bundle / "knowledge.sqlite")) as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM claims").fetchone()[0], 9)
            self.assertEqual(
                db.execute(
                    "SELECT statement FROM claims WHERE series_id LIKE '%training-context'"
                ).fetchone()[0],
                statement,
            )

    def test_changed_source_during_staging_is_not_published(self):
        write = importer._write_receipt

        def change_source(path, receipt):
            write(path, receipt)
            self.source.write_bytes(self.original + b"\n")

        with patch.object(importer, "_write_receipt", side_effect=change_source):
            with self.assertRaisesRegex(ValueError, "changed during"):
                self.import_fixture()
        self.assert_no_bundle()

    def test_other_import_lock_is_not_removed_or_overwritten(self):
        self.output.mkdir()
        lock = self.output / ".snacks.import.lock"
        lock.write_text("other-import", encoding="utf-8")
        with self.assertRaises(FileExistsError):
            self.import_fixture()
        self.assertEqual(lock.read_text(encoding="utf-8"), "other-import")
        self.assertFalse((self.output / "snacks").exists())

    def test_bounds_duplicate_keys_and_relative_or_external_paths_are_rejected(self):
        self.source.write_bytes(b" " * (importer.MAX_LEDGER_BYTES + 1))
        with self.assertRaises(ValueError):
            self.import_fixture()
        self.assert_no_bundle()
        self.source.write_bytes(b'{"schema_version":1,"schema_version":1}')
        with self.assertRaisesRegex(ValueError, "Duplicate JSON"):
            self.import_fixture()
        self.source.write_bytes(self.original)
        for output in (Path("relative"), importer.ROOT.parent / "outside-import"):
            with self.assertRaises(ValueError):
                importer.import_ledger(self.source, output, "snacks")
        with self.assertRaises(ValueError):
            importer.import_ledger(self.source, self.output, "../escape")
        self.assert_no_bundle()


if __name__ == "__main__":
    unittest.main()
