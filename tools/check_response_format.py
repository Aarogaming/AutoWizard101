"""Check that marked plan/report documents follow the repo response format; offline, read-only.

AGENTS.md requires every plan, proposal, issue response and report to cover Who, What,
When, Where, Why and How, plus the Before, During and After of the work. A Markdown file
opts in by putting this marker anywhere in its first 5 lines:

    <!-- response-format: 5w1h-bda -->

Each level-2 heading of the form "## R<number>" (for example "## R3. Title") is one
response and must contain every label as bold text, for example **Who.** or **Before**.
A marked file with no such headings is also reported. Warnings do not change the exit
code unless --strict is given.
"""

import argparse
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
MARKER = re.compile("<!--\\s*response-format:\\s*5w1h-bda\\s*-->")
SECTION = re.compile("^## R[0-9]+\\b", re.MULTILINE)
LABELS = ("Who", "What", "When", "Where", "Why", "How", "Before", "During", "After")
SKIP_DIRS = {".git", "target", "node_modules", "private", ".local"}


def is_marked(text):
    """True when the opt-in marker appears in the first 5 lines."""
    return bool(MARKER.search("\n".join(text.splitlines()[:5])))


def check_text(text):
    """Return one warning per problem found in a marked document."""
    starts = [m.start() for m in SECTION.finditer(text)]
    if not starts:
        return ["marked file has no '## R<n>' response sections"]
    found = []
    for index, start in enumerate(starts):
        end = starts[index + 1] if index + 1 < len(starts) else len(text)
        body = text[start:end]
        title = body.splitlines()[0].strip()
        missing = [
            label for label in LABELS if not re.search("\\*\\*" + label + "\\b", body)
        ]
        if missing:
            found.append(title + ": missing " + ", ".join(missing))
    return found


def marked_documents(root):
    """Yield (relative path, text) for Markdown files under root that carry the marker."""
    for path in sorted(root.rglob("*.md")):
        relative = path.relative_to(root)
        if SKIP_DIRS.intersection(relative.parts):
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        if is_marked(text):
            yield relative.as_posix(), text


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--strict", action="store_true", help="exit 1 on any warning")
    args = parser.parse_args(argv)
    total = 0
    checked = 0
    for name, text in marked_documents(args.root):
        checked += 1
        for warning in check_text(text):
            total += 1
            print(name + ": " + warning)
    print(
        "Response format check: "
        + str(checked)
        + " marked file(s), "
        + str(total)
        + " warning(s)"
    )
    return 1 if total and args.strict else 0


if __name__ == "__main__":
    raise SystemExit(main())
