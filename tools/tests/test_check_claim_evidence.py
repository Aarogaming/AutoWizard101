"""Offline tests for the claim-evidence checker; no apps, network or game access."""

from contextlib import redirect_stdout
import io
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


if __name__ == "__main__":
    unittest.main()
