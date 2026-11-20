"""Fail on tracked text files that are not valid UTF-8 or contain mojibake.

Usage:
    python tools/check_text_encoding.py            # report violations, exit 1 if any
    python tools/check_text_encoding.py --quiet    # only print a summary

Scope rules:
  * Vendored upstream sources (ProjectMaelstrom/**/Scripts/Library/**) and tracked
    junk (ProjectMaelstrom/tmp_master/**) are excluded: third-party or disposable
    content is never rewritten by this repository. They are reported separately.
  * Only text-like extensions are inspected.
"""

import argparse
import re
import subprocess
import sys
from pathlib import Path

TEXT_EXTENSIONS = {
    ".md",
    ".py",
    ".cs",
    ".json",
    ".txt",
    ".yml",
    ".yaml",
    ".ps1",
    ".cmd",
    ".sh",
    ".toml",
    ".cfg",
    ".sql",
    ".xml",
    ".html",
    ".js",
    ".css",
    ".rs",
    ".csv",
    ".ini",
    ".gitignore",
    ".gitattributes",
}
EXCLUDED_PREFIXES = (
    "ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/",
    "ProjectMaelstrom/tmp_master/",
)
# Sequences that indicate text was decoded with the wrong code page and re-encoded.
MOJIBAKE_PATTERNS = (
    ("replacement character", re.compile("\ufffd")),
    ("mangled em/en dash or quote", re.compile("\u00e2\u20ac")),
    (
        "mangled latin-1 sequence",
        re.compile("\u00c3[\u00a0-\u00bf\u2013\u2014\u2018\u2019\u201c\u201d\u2020]"),
    ),
    ("mangled middle dot or punctuation", re.compile("\u00c2[\u00a0-\u00bf]")),
)


def tracked_files(root: Path):
    try:
        out = subprocess.run(
            ["git", "ls-files"], cwd=root, capture_output=True, text=True, check=True
        ).stdout.splitlines()
    except (subprocess.CalledProcessError, FileNotFoundError):
        return sorted(
            str(p.relative_to(root)).replace("\\", "/")
            for p in root.rglob("*")
            if p.is_file()
        )
    return out


def is_text_file(name: str) -> bool:
    suffix = Path(name).suffix.lower()
    if suffix in TEXT_EXTENSIONS:
        return True
    return Path(name).name in TEXT_EXTENSIONS


def scan(root: Path):
    violations = []
    excluded = []
    for name in tracked_files(root):
        if not is_text_file(name):
            continue
        path = root / name
        if not path.exists():
            continue
        raw = path.read_bytes()
        if name.startswith(EXCLUDED_PREFIXES):
            try:
                raw.decode("utf-8")
            except UnicodeDecodeError:
                excluded.append(name)
            continue
        try:
            text = raw.decode("utf-8")
        except UnicodeDecodeError as error:
            violations.append((name, f"invalid utf-8 at byte {error.start}"))
            continue
        for label, pattern in MOJIBAKE_PATTERNS:
            match = pattern.search(text)
            if match:
                violations.append((name, f"{label} at offset {match.start()}"))
                break
    return violations, excluded


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--quiet", action="store_true", help="summary only")
    parser.add_argument("--root", default=str(Path(__file__).resolve().parent.parent))
    args = parser.parse_args()
    root = Path(args.root)
    violations, excluded = scan(root)
    if not args.quiet:
        for name, reason in violations:
            print(
                f"violation: {name}: {reason}".encode(
                    "ascii", "backslashreplace"
                ).decode()
            )
        for name in excluded:
            print(f"excluded (vendored/junk, not rewritten): {name}")
    print(
        f"text-encoding check: {len(violations)} violation(s), "
        f"{len(excluded)} excluded file(s)"
    )
    return 1 if violations else 0


if __name__ == "__main__":
    sys.exit(main())
