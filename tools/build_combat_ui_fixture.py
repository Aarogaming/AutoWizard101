"""Build a versioned combat-UI vocabulary fixture from saved UI-tree observations.

Reads ONLY local observation JSONL files (ignored .local artifacts). Emits names,
visibility counts and source hashes — no pixels, no text, no memory addresses, no
input records. The output is offline candidate structure, not gameplay evidence.
"""

from __future__ import annotations

import hashlib
import json
import sys
from collections import Counter
from pathlib import Path
from typing import Any

SCHEMA_VERSION = 1
KIND = "maelstrom-combat-ui-vocabulary-v1"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def extract(paths: list[Path]) -> dict[str, Any]:
    """Deterministically extract a node vocabulary from observation JSONL files."""
    sources: list[dict[str, Any]] = []
    totals: Counter[str] = Counter()
    visible: Counter[str] = Counter()
    hidden: Counter[str] = Counter()
    tree_samples = 0
    for path in paths:
        if not path.is_file():
            raise FileNotFoundError(str(path))
        lines = path.read_text(encoding="utf-8").splitlines()
        sources.append(
            {
                "file": path.name,
                "sha256": digest(path),
                "bytes": path.stat().st_size,
                "lines": len(lines),
            }
        )
        for line in lines:
            try:
                record = json.loads(line)
            except json.JSONDecodeError:
                continue
            if record.get("kind") != "tree":
                continue
            tree_samples += 1
            for node in record.get("nodes", []):
                if type(node) is not dict:
                    continue
                name = node.get("name")
                if type(name) is not str or not name:
                    continue
                totals[name] += 1
                if node.get("visible") is True:
                    visible[name] += 1
                elif node.get("visible") is False:
                    hidden[name] += 1
    nodes = [
        {
            "name": name,
            "observations": count,
            "visible": visible[name],
            "hidden": hidden[name],
        }
        for name, count in sorted(totals.items(), key=lambda pair: (-pair[1], pair[0]))
    ]
    return {
        "schemaVersion": SCHEMA_VERSION,
        "kind": KIND,
        "treeSamples": tree_samples,
        "sources": sources,
        "nodes": nodes,
        "note": "Candidate UI names/visibility from saved local observations. Not screen coordinates, not combat facts, not an action authority.",
    }


def main() -> int:
    if len(sys.argv) < 3:
        print(
            "usage: build_combat_ui_fixture.py <output.json> <observation.jsonl> [...]",
            file=sys.stderr,
        )
        return 2
    out = Path(sys.argv[1])
    fixture = extract([Path(p) for p in sys.argv[2:]])
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(
        json.dumps(fixture, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    print(
        f"wrote {out}: {len(fixture['nodes'])} names from {fixture['treeSamples']} trees"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
