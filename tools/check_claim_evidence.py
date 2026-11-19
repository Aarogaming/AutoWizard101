"""Advisory evidence checks for docs/coordination/results/*.md; offline, read-only.

The project board gate checks structure. This checks that result reports do not cite
things the checkout cannot support:

* repo-relative file paths in backticks must exist (paths under .local/ are skipped);
* backticked test_* names must exist in a tracked .rs or .py file;
* "cargo ... --test NAME ...: N passed" must match the number of #[test] items in
  crates/*/tests/NAME.rs;
* absolute phrases ("100% reliable", "immutably proven", "proven fact") are flagged.

A report that carries an "Audit corrections", "Audit note", "Update note" or
"Integration review correction" section has acknowledged its defects and is skipped,
as are gap audits that list proposed tests. Warnings do not change the exit code
unless --strict is given.
"""

import argparse
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
RESULTS = "docs/coordination/results"
ACKNOWLEDGED = re.compile(
    "audit corrections|audit note|update note|integration review correction",
    re.IGNORECASE,
)
PATH_TOKEN = re.compile(
    "^(crates|tools|docs|ProjectMaelstrom|scripts|integrations|MaelstromToolkit"
    "|TestData|third_party)/[A-Za-z0-9_./-]+[.][A-Za-z0-9]+$"
)
TEST_TOKEN = re.compile("^test_[A-Za-z0-9_]+$")
TEST_DEF = re.compile("(?:fn|def) (test_[A-Za-z0-9_]+)")
COUNT_CLAIM = re.compile("--test ([A-Za-z0-9_]+).*?([0-9]+) passed")
# Git-ignored build output; never expected to be committed.
BUILD_OUTPUT = ("/bin/", "/obj/", "/target/")
BANNED = (
    "100% reliable",
    "immutably proven",
    "proven fact",
    "guaranteed optimal",
)


def tracked_files(root=ROOT):
    """Return committed file names, or None when git is unavailable."""
    try:
        result = subprocess.run(
            ["git", "-C", str(root), "ls-files", "-z"],
            capture_output=True,
            check=True,
            timeout=30,
        )
    except (OSError, subprocess.SubprocessError):
        return None
    return {
        name.decode("utf-8", "replace")
        for name in result.stdout.split(bytes([0]))
        if name
    }


def known_test_names(root, files):
    names = set()
    for name in files:
        if name.endswith((".rs", ".py")):
            try:
                text = (root / name).read_text(encoding="utf-8", errors="ignore")
            except OSError:
                continue
            names.update(TEST_DEF.findall(text))
        stem = name.rsplit("/", 1)[-1].rsplit(".", 1)[0]
        if stem.startswith("test_"):
            names.add(stem)
    return names


def rust_test_count(root, files, test_name):
    for name in files:
        if name.endswith("/tests/" + test_name + ".rs") and name.startswith("crates/"):
            text = (root / name).read_text(encoding="utf-8", errors="ignore")
            return text.count("#[test]")
    return None


def check_report(text, root, files, test_names):
    """Return warnings for one report body; empty when acknowledged or clean."""
    if ACKNOWLEDGED.search(text) or "architectural gap audit" in text:
        return []
    warnings = []
    for token in sorted(set(re.findall("`([^`]+)`", text))):
        if any(part in token for part in BUILD_OUTPUT):
            continue
        if PATH_TOKEN.match(token) and "..." not in token:
            if token not in files and not (root / token).exists():
                warnings.append("cited path does not exist: " + token)
        elif TEST_TOKEN.match(token) and token not in test_names:
            warnings.append("cited test does not exist: " + token)
    for line in text.splitlines():
        match = COUNT_CLAIM.search(line)
        if match:
            actual = rust_test_count(root, files, match.group(1))
            if actual is not None and actual != int(match.group(2)):
                warnings.append(
                    "claims "
                    + match.group(2)
                    + " passed for --test "
                    + match.group(1)
                    + " but the file has "
                    + str(actual)
                    + " #[test] items"
                )
    for phrase in BANNED:
        # A phrase inside quotation marks is being quoted (usually to refute it).
        pattern = "(?<![" + chr(34) + chr(39) + "])" + re.escape(phrase)
        if re.search(pattern, text, re.IGNORECASE):
            warnings.append("absolute claim: " + phrase)
    return warnings


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--strict", action="store_true", help="exit 1 on any warning")
    args = parser.parse_args(argv)
    files = tracked_files(args.root)
    if files is None:
        files = set()
        for path in args.root.rglob("*"):
            if (
                path.is_file()
                and ".git" not in path.parts
                and "target" not in path.parts
            ):
                files.add(path.relative_to(args.root).as_posix())
    test_names = known_test_names(args.root, files)
    total = 0
    for report in sorted((args.root / RESULTS).glob("*.md")):
        found = check_report(
            report.read_text(encoding="utf-8", errors="ignore"),
            args.root,
            files,
            test_names,
        )
        total += len(found)
        for warning in found:
            print(report.name + ": " + warning)
    print("Claim evidence check: " + str(total) + " warning(s)")
    return 1 if total and args.strict else 0


if __name__ == "__main__":
    raise SystemExit(main())
