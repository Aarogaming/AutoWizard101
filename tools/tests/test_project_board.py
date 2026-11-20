"""Small offline integrity checks; no apps, network, models or game access."""

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
import check_project_board as checker


class ProjectBoardTests(unittest.TestCase):
    def setUp(self):
        local = checker.ROOT / ".local"
        local.mkdir(exist_ok=True)
        temporary = tempfile.TemporaryDirectory(prefix="board-tests-", dir=local)
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        (self.root / "evidence.md").write_text(
            "Offline result only.\n", encoding="utf-8"
        )
        self.board = {
            "schema_version": 1,
            "kind": "maelstrom-project-workstreams",
            "executable": False,
            "status_vocabulary": sorted(checker.STATUSES),
            "workstreams": [self.row("TEST-ONE")],
        }

    @staticmethod
    def row(identity):
        return {
            "id": identity,
            "title": "Small offline task",
            "track": "test",
            "status": "complete",
            "owner": "test-worker",
            "next_task": "Coordinator reviews this bounded result.",
            "acceptance_criteria": ["Recorded offline checks pass."],
            "depends_on": [],
            "runtime_requires": [],
            "verification": {
                "review": "available",
                "code": "present",
                "offline": "passed",
                "live": "not_qualified",
            },
            "evidence": [
                {
                    "kind": "offline_report",
                    "path": "evidence.md",
                    "path_status": "existing",
                    "scope": "Pure offline component only.",
                }
            ],
        }

    def errors(self):
        return checker.validate(self.board, self.root)

    def test_completed_offline_component_does_not_require_live_proof(self):
        self.assertEqual([], self.errors())

    def test_missing_dependency_is_reported(self):
        self.board["workstreams"][0]["depends_on"] = ["MISSING-TASK"]
        self.assertTrue(any("unknown dependency" in e for e in self.errors()))

    def test_cycle_across_offline_and_runtime_dependencies_is_reported(self):
        self.board["workstreams"].append(self.row("TEST-TWO"))
        self.board["workstreams"][0]["depends_on"] = ["TEST-TWO"]
        self.board["workstreams"][1]["runtime_requires"] = ["TEST-ONE"]
        self.assertTrue(any("cycle" in e for e in self.errors()))

    def test_invalid_status_is_reported(self):
        self.board["workstreams"][0]["status"] = "probably-finished"
        self.assertTrue(any("invalid status" in e for e in self.errors()))

    def test_wrong_status_type_produces_error_without_crashing(self):
        self.board["workstreams"][0]["status"] = ["complete"]
        self.assertTrue(any("invalid status" in e for e in self.errors()))

    def test_empty_task_acceptance_and_owner_are_reported(self):
        row = self.board["workstreams"][0]
        row.update(next_task="", owner="", acceptance_criteria=[])
        self.assertEqual(3, len(self.errors()))

    def test_path_escape_and_absolute_paths_are_rejected(self):
        for path in (
            "../evidence.md",
            "/evidence.md",
            "C:/evidence.md",
            "\\\\host\\share",
            "folder/../evidence.md",
        ):
            with self.subTest(path=path):
                self.board["workstreams"][0]["evidence"][0]["path"] = path
                self.assertTrue(any("repository-relative" in e for e in self.errors()))

    def test_missing_existing_evidence_is_reported(self):
        self.board["workstreams"][0]["evidence"][0]["path"] = "not-created.md"
        self.assertTrue(any("existing evidence is missing" in e for e in self.errors()))

    def test_local_evidence_missing_is_still_strict_by_default(self):
        self.board["workstreams"][0]["evidence"][0]["path"] = ".local/absent.md"
        self.assertTrue(any("existing evidence is missing" in e for e in self.errors()))

    def test_allow_missing_local_skips_only_gitignored_local_evidence(self):
        self.board["workstreams"][0]["evidence"][0]["path"] = ".local/absent.md"
        strict = checker.validate(self.board, self.root)
        self.assertTrue(any("existing evidence is missing" in e for e in strict))
        relaxed = checker.validate(self.board, self.root, allow_missing_local=True)
        self.assertFalse(any("existing evidence is missing" in e for e in relaxed))
        # Non-.local evidence remains required even when the flag is set.
        self.board["workstreams"][0]["evidence"][0]["path"] = "not-created.md"
        still = checker.validate(self.board, self.root, allow_missing_local=True)
        self.assertTrue(any("existing evidence is missing" in e for e in still))

    def test_supplied_claim_registry_must_exist_inside_repository(self):
        self.board["coordination"] = {"claim_registry": "claims.json"}
        self.assertTrue(any("coordination.claim_registry" in e for e in self.errors()))
        self.claim_registry([])
        self.assertEqual([], self.errors())
        self.board["coordination"]["claim_registry"] = "../claims.json"
        self.assertTrue(any("repository-relative" in e for e in self.errors()))

    def claim_registry(self, claims):
        self.board["coordination"] = {"claim_registry": "claims.json"}
        registry = {
            "schema_version": 1,
            "kind": "maelstrom-task-claims",
            "executable": False,
            "claims": claims,
        }
        (self.root / "claims.json").write_text(json.dumps(registry), encoding="utf-8")

    @staticmethod
    def claim(identity, path="future.py", state="active"):
        return {
            "task_id": identity,
            "owner": "worker",
            "workstream_id": "TEST-ONE",
            "state": state,
            "owned_paths": [path],
        }

    def test_active_claims_cannot_overlap_case_insensitively(self):
        self.claim_registry([self.claim("first"), self.claim("second", "FUTURE.py")])
        self.assertTrue(any("Overlapping active claim" in e for e in self.errors()))

    def test_released_claim_does_not_block_new_owner(self):
        self.claim_registry(
            [self.claim("first", state="released"), self.claim("second")]
        )
        self.assertEqual([], self.errors())

    def test_claims_require_known_workstream_and_exact_files(self):
        claim = self.claim("first", "tools/*.py")
        claim["workstream_id"] = "MISSING-TASK"
        self.claim_registry([claim])
        errors = self.errors()
        self.assertTrue(any("unknown workstream" in e for e in errors))
        self.assertTrue(any("exact files" in e for e in errors))

    def released(self, identity, path="evidence.md", note="done"):
        claim = self.claim(identity, path, state="released")
        if note is not None:
            claim["release_note"] = note
        return claim

    def warnings(self, claims, tracked=None):
        registry = {"claims": claims}
        return checker.claim_record_warnings(registry, self.root, tracked)

    def test_released_claim_without_release_note_warns(self):
        self.assertEqual([], self.warnings([self.released("ok")]))
        found = self.warnings([self.released("bare", note=None)])
        self.assertEqual(1, len(found))
        self.assertIn("no release_note", found[0])

    def test_active_claim_needs_neither_note_nor_files(self):
        self.assertEqual([], self.warnings([self.claim("wip", "not-yet.py")]))

    def test_released_claim_with_missing_file_warns_but_local_is_skipped(self):
        found = self.warnings([self.released("ghost", "tools/gone.py")])
        self.assertTrue(any("missing path" in w for w in found))
        self.assertEqual(
            [], self.warnings([self.released("local", ".local/evidence.json")])
        )

    def test_released_claim_with_uncommitted_file_warns_when_tracked_known(self):
        self.assertEqual(
            [], self.warnings([self.released("ok")], tracked={"evidence.md"})
        )
        found = self.warnings([self.released("loose")], tracked=set())
        self.assertTrue(any("uncommitted path" in w for w in found))

    def test_cli_reports_claim_warnings_without_failing_unless_strict(self):
        path = self.root / "board.json"
        self.claim_registry([self.released("bare", note=None)])
        path.write_text(json.dumps(self.board), encoding="utf-8")
        with patch.object(checker, "git_tracked_files", return_value=None):
            with redirect_stdout(io.StringIO()) as output:
                result = checker.main([str(path), "--root", str(self.root)])
            self.assertEqual(0, result)
            self.assertIn("Claim record warnings: 1 (advisory)", output.getvalue())
            with redirect_stdout(io.StringIO()):
                strict = checker.main(
                    [str(path), "--root", str(self.root), "--strict-claims"]
                )
        self.assertEqual(1, strict)

    def test_resolved_link_escape_is_rejected_without_reading_target(self):
        original = Path.resolve
        evidence = self.root / "evidence.md"

        def resolve(path, strict=False):
            if path == evidence:
                return self.root.parent / "outside.md"
            return original(path, strict=strict)

        with patch.object(Path, "resolve", autospec=True, side_effect=resolve):
            self.assertTrue(any("path escapes repository" in e for e in self.errors()))

    def test_planned_outputs_need_not_exist_but_cannot_escape(self):
        row = self.board["workstreams"][0]
        row["planned_outputs"] = ["future/new-file.py"]
        self.assertEqual([], self.errors())
        row["planned_outputs"] = ["../elsewhere.py"]
        self.assertTrue(self.errors())

    def test_planned_output_cannot_masquerade_as_evidence(self):
        self.board["workstreams"][0]["evidence"][0]["path_status"] = "planned"
        self.assertTrue(any("use planned_outputs" in e for e in self.errors()))

    def test_duplicate_ids_are_reported(self):
        self.board["workstreams"].append(deepcopy(self.board["workstreams"][0]))
        self.assertTrue(any("duplicate id" in e for e in self.errors()))

    def test_schema_bool_and_execution_flag_are_rejected(self):
        self.board.update(schema_version=True, executable=True)
        self.assertEqual(2, len(self.errors()))

    def archive_inventory(self):
        for name in ("legacy.zip", "release.zip"):
            (self.root / name).write_bytes(
                b"filename fixture, not an archive parse test"
            )
        self.board["archive_inventory"] = {
            "root_archive_count": 2,
            "non_release_root_candidate_count": 1,
            "non_release_root_family_count": 1,
            "nested_non_release_count": 0,
            "generated_release_count": 3,
            "total_compressed_count": 5,
            "historical_distribution": "release.zip",
            "nested_archives": [],
            "root_archives": [
                {
                    "project_id": name,
                    "archive": name + ".zip",
                    "family_id": name,
                    "workstream_ids": ["TEST-ONE"],
                }
                for name in ("legacy", "release")
            ],
        }

    def test_root_archive_count_and_snapshot_arithmetic_reconcile(self):
        self.archive_inventory()
        self.assertEqual([], self.errors())
        self.board["archive_inventory"]["total_compressed_count"] = 9
        self.assertTrue(any("count mismatch" in e for e in self.errors()))

    def test_new_root_archive_requires_inventory_update(self):
        self.archive_inventory()
        (self.root / "new.zip").write_bytes(b"new filename")
        self.assertTrue(any("differs from current root" in e for e in self.errors()))

    def test_archive_root_moves_inventory_check_outside_repository_root(self):
        self.archive_inventory()
        shelf = self.root / "shelf"
        shelf.mkdir()
        for name in ("legacy.zip", "release.zip"):
            (self.root / name).replace(shelf / name)
        self.board["archive_inventory"]["archive_root"] = "shelf"
        self.assertEqual([], self.errors())
        (shelf / "new.zip").write_bytes(b"new filename")
        self.assertTrue(any("differs from current root" in e for e in self.errors()))

    def test_missing_archive_folder_is_error_unless_local_evidence_allowed(self):
        self.archive_inventory()
        self.board["archive_inventory"]["archive_root"] = "absent-shelf"
        self.assertTrue(any("Archive folder is missing" in e for e in self.errors()))
        errors = checker.validate(self.board, self.root, allow_missing_local=True)
        self.assertEqual([], errors)

    def test_archive_root_must_be_relative_posix(self):
        self.archive_inventory()
        for bad in ("C:/x", "a\\b", ""):
            self.board["archive_inventory"]["archive_root"] = bad
            self.assertTrue(
                any("archive_root must be" in e for e in self.errors()), bad
            )

    def test_game_count_and_reference_reconciliation(self):
        self.board["game_inventory"] = {
            "count": 2,
            "fairground_count": 0,
            "pet_count": 0,
            "workstream_ids": ["TEST-ONE", "MISSING-GAME"],
        }
        self.assertTrue(
            any("Game inventory references unknown" in e for e in self.errors())
        )

    def test_duplicate_json_key_rejected(self):
        path = self.root / "board.json"
        path.write_text('{"schema_version": 1, "schema_version": 2}', encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Duplicate JSON key"):
            checker.load_board(path)

    def test_cli_returns_nonzero_for_invalid_board(self):
        path = self.root / "board.json"
        self.board["workstreams"][0]["depends_on"] = ["MISSING-TASK"]
        path.write_text(json.dumps(self.board), encoding="utf-8")
        with redirect_stdout(io.StringIO()) as output:
            result = checker.main([str(path), "--root", str(self.root)])
        self.assertEqual(1, result)
        self.assertIn("unknown dependency", output.getvalue())


if __name__ == "__main__":
    unittest.main()
