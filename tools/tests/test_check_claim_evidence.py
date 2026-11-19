"""Offline tests for the claim-evidence checker; no apps, network or game access."""

from contextlib import redirect_stdout
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import check_claim_evidence as checker


class ClaimEvidenceTests(unittest.TestCase):
    def setUp(self):
        local = checker.ROOT / ".local"
        local.mkdir(exist_ok=True)
        temporary = tempfile.TemporaryDirectory(prefix="evidence-tests-", dir=local)
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        tests = self.root / "crates" / "demo" / "tests"
        tests.mkdir(parents=True)
        (tests / "demo_tests.rs").write_text(
            "#[test]\nfn test_alpha() {}\n#[test]\nfn test_beta() {}\n",
            encoding="utf-8",
        )
        self.files = {"crates/demo/tests/demo_tests.rs"}
        self.names = checker.known_test_names(self.root, self.files)

    def check(self, text):
        return checker.check_report(text, self.root, self.files, self.names)

    def test_known_test_and_existing_path_pass(self):
        text = "See `crates/demo/tests/demo_tests.rs` and `test_alpha`."
        self.assertEqual([], self.check(text))

    def test_missing_path_and_test_are_flagged(self):
        found = self.check("`crates/demo/src/gone.rs` and `test_missing`.")
        self.assertEqual(2, len(found))
        self.assertTrue(any("path does not exist" in w for w in found))
        self.assertTrue(any("test does not exist" in w for w in found))

    def test_local_and_external_names_are_not_checked(self):
        self.assertEqual([], self.check("`.local/replays/out.json` `other/x.py`"))
        self.assertEqual([], self.check("`ProjectMaelstrom/App/bin/Release/App.exe`"))

    def test_test_count_must_match_file(self):
        good = "`cargo test --test demo_tests`: 2 passed"
        bad = "`cargo test --test demo_tests`: 5 passed"
        self.assertEqual([], self.check(good))
        found = self.check(bad)
        self.assertEqual(1, len(found))
        self.assertIn("file has 2", found[0])

    def test_absolute_claims_flagged_unless_quoted(self):
        self.assertTrue(self.check("This is 100% reliable."))
        self.assertEqual([], self.check('A claim called "Proven Fact" is refuted.'))

    def test_acknowledged_reports_are_skipped(self):
        text = "`test_missing`\n\n## Audit note (2026-11-19)\nKnown."
        self.assertEqual([], self.check(text))

    def test_main_is_advisory_unless_strict(self):
        results = self.root / checker.RESULTS
        results.mkdir(parents=True)
        (results / "R.md").write_text("`test_missing`\n", encoding="utf-8")
        with redirect_stdout(io.StringIO()) as output:
            self.assertEqual(0, checker.main(["--root", str(self.root)]))
        self.assertIn("1 warning(s)", output.getvalue())
        with redirect_stdout(io.StringIO()):
            self.assertEqual(1, checker.main(["--root", str(self.root), "--strict"]))


class RegistryChecks(unittest.TestCase):
    """Released claims must carry an evidenced release_note and existing owned paths."""

    def setUp(self):
        local = checker.ROOT / ".local"
        local.mkdir(exist_ok=True)
        temporary = tempfile.TemporaryDirectory(prefix="registry-tests-", dir=local)
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)

    def write_claims(self, claims):
        path = self.root / checker.CLAIMS
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps({"claims": claims}), encoding="utf-8")

    def write_baseline(self, ids):
        path = self.root / checker.BASELINE
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("\n".join(ids) + "\n", encoding="utf-8")

    @staticmethod
    def released(note=None, paths=None):
        claim = {"task_id": "T-1", "state": "released"}
        if note is not None:
            claim["release_note"] = note
        if paths is not None:
            claim["owned_paths"] = paths
        return claim

    def run_main(self, argv=None):
        with redirect_stdout(io.StringIO()) as output:
            code = checker.main(["--root", str(self.root)] + (argv or []))
        return code, output.getvalue()

    def test_released_without_note_is_a_violation(self):
        self.write_claims([self.released()])
        found = checker.claim_violations(self.root)
        self.assertEqual(1, len(found))
        self.assertIn("without release_note", found[0])

    def test_note_without_evidence_marker_is_a_violation(self):
        self.write_claims([self.released(note="Done, tests passed.")])
        found = checker.claim_violations(self.root)
        self.assertEqual(1, len(found))
        self.assertIn("no evidence marker", found[0])

    def test_note_with_commit_hash_passes(self):
        self.write_claims([self.released(note="Fixed and released in 1babc96.")])
        self.assertEqual([], checker.claim_violations(self.root))

    def test_missing_owned_path_is_a_violation(self):
        self.write_claims(
            [self.released(note="evidence 1babc96", paths=["docs/gone.md"])]
        )
        found = checker.claim_violations(self.root)
        self.assertEqual(1, len(found))
        self.assertIn("owned path missing", found[0])

    def test_local_owned_paths_are_skipped(self):
        self.write_claims(
            [self.released(note="evidence 1babc96", paths=[".local/data/x.json"])]
        )
        self.assertEqual([], checker.claim_violations(self.root))

    def test_active_claims_are_not_checked(self):
        self.write_claims([{"task_id": "T-2", "state": "active"}])
        self.assertEqual([], checker.claim_violations(self.root))

    def test_baseline_grandfathers_a_legacy_violation(self):
        self.write_claims([self.released()])
        self.write_baseline(["T-1"])
        code, output = self.run_main()
        self.assertEqual(0, code)
        self.assertIn("0 new registry violation(s)", output)

    def test_new_violation_fails_even_without_strict(self):
        self.write_claims([self.released()])
        code, output = self.run_main()
        self.assertEqual(1, code)
        self.assertIn("REGISTRY: T-1", output)

    def test_stale_baseline_entry_fails(self):
        self.write_claims([self.released(note="evidence 1babc96")])
        self.write_baseline(["T-1"])
        code, output = self.run_main()
        self.assertEqual(1, code)
        self.assertIn("BASELINE STALE: T-1", output)

    def test_unreadable_claims_fails_instead_of_passing(self):
        path = self.root / checker.CLAIMS
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("not json", encoding="utf-8")
        code, output = self.run_main()
        self.assertEqual(1, code)
        self.assertIn("unreadable", output)


if __name__ == "__main__":
    unittest.main()
