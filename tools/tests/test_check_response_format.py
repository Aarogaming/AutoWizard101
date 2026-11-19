"""Offline tests for the response-format checker; no apps, network or game access."""

from contextlib import redirect_stdout
import io
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import check_response_format as checker

MARKER = "<!-- response-format: 5w1h-bda -->\n"
COMPLETE = "".join("**" + label + ".** text. " for label in checker.LABELS)


class ResponseFormatTests(unittest.TestCase):
    def setUp(self):
        local = checker.ROOT / ".local"
        local.mkdir(exist_ok=True)
        temporary = tempfile.TemporaryDirectory(prefix="response-format-", dir=local)
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)

    def run_cli(self, *args):
        out = io.StringIO()
        with redirect_stdout(out):
            code = checker.main(["--root", str(self.root), *args])
        return code, out.getvalue()

    def test_complete_section_passes(self):
        text = MARKER + "# Title\n\n## R1. Topic\n\n" + COMPLETE + "\n"
        self.assertEqual(checker.check_text(text), [])

    def test_missing_labels_are_named_per_section(self):
        text = (
            MARKER
            + "## R1. Good\n"
            + COMPLETE
            + "\n## R2. Bad\n**Who.** a. **What.** b.\n"
        )
        found = checker.check_text(text)
        self.assertEqual(len(found), 1)
        self.assertIn("R2. Bad", found[0])
        self.assertIn("Before, During, After", found[0])

    def test_marked_file_without_sections_is_reported(self):
        found = checker.check_text(MARKER + "# Just a title\n")
        self.assertEqual(len(found), 1)
        self.assertIn("no '## R<n>'", found[0])

    def test_marker_must_be_near_the_top(self):
        self.assertTrue(checker.is_marked(MARKER + "body"))
        self.assertFalse(checker.is_marked("\n" * 10 + MARKER))

    def test_unmarked_and_skipped_files_are_ignored(self):
        (self.root / "plain.md").write_text("## R1. Bad\n**Who.** a.\n", "utf-8")
        skipped = self.root / "private"
        skipped.mkdir()
        (skipped / "note.md").write_text(MARKER + "# empty\n", "utf-8")
        code, output = self.run_cli("--strict")
        self.assertEqual(code, 0)
        self.assertIn("0 marked file(s), 0 warning(s)", output)

    def test_cli_strict_exit_code(self):
        (self.root / "report.md").write_text(
            MARKER + "## R1. Bad\n**Who.** a.\n", "utf-8"
        )
        code, output = self.run_cli()
        self.assertEqual(code, 0)
        self.assertIn("report.md: ## R1. Bad: missing", output)
        code, _ = self.run_cli("--strict")
        self.assertEqual(code, 1)


if __name__ == "__main__":
    unittest.main()
