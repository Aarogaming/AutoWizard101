"""Repository text files must stay valid UTF-8 and free of mojibake."""

import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))

import check_text_encoding  # noqa: E402


class TextEncodingTests(unittest.TestCase):
    def test_tracked_text_files_are_valid_utf8_without_mojibake(self):
        violations, _ = check_text_encoding.scan(ROOT)
        self.assertEqual(
            [],
            [(name, reason) for name, reason in violations],
            "tracked text files must be valid UTF-8 without mojibake; "
            "repair the characters rather than deleting content",
        )

    def test_checker_reports_failure_for_broken_file(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "broken.md").write_bytes(b"valid utf-8 \x96 raw cp1252 dash")
            violations, _ = check_text_encoding.scan(root)
            self.assertEqual(1, len(violations))
            self.assertEqual("broken.md", violations[0][0])

    def test_vendored_directories_are_excluded_from_rewrites(self):
        _, excluded = check_text_encoding.scan(ROOT)
        for name in excluded:
            self.assertTrue(
                name.startswith(check_text_encoding.EXCLUDED_PREFIXES),
                f"{name} must stay outside the repair scope",
            )


if __name__ == "__main__":
    unittest.main()
