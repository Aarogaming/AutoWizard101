"""Static data-supply inventory. Never imports game readers or accesses a process."""

import ast
import hashlib
import json
from pathlib import Path


def inventory(root):
    library = (
        root
        / "ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/wizwalker/wizwalker-master/wizwalker"
    )
    files = sorted((library / "memory/memory_objects").glob("*.py")) + [
        library / "client.py"
    ]
    entries = []
    for path in files:
        source = path.read_text(encoding="utf-8-sig")
        tree = ast.parse(source)
        for cls in (node for node in tree.body if isinstance(node, ast.ClassDef)):
            for method in cls.body:
                if not isinstance(
                    method, (ast.FunctionDef, ast.AsyncFunctionDef)
                ) or method.name.startswith("_"):
                    continue
                calls = [
                    node.func.attr
                    for node in ast.walk(method)
                    if isinstance(node, ast.Call)
                    and isinstance(node.func, ast.Attribute)
                ]
                action = method.name.startswith(
                    (
                        "write_",
                        "set_",
                        "activate",
                        "close",
                        "send",
                        "teleport",
                        "goto",
                        "click",
                    )
                ) or any(c.startswith(("write_", "send_")) for c in calls)
                entries.append(
                    {
                        "id": f"{path.stem}.{cls.name}.{method.name}",
                        "category": path.stem,
                        "type": (
                            ast.unparse(method.returns)
                            if method.returns
                            else "unspecified"
                        ),
                        "role": (
                            "action-excluded"
                            if action
                            else "reader-or-helper-candidate"
                        ),
                        "qualification": "legacy-source-only-not-current-build-verified",
                        "source": path.relative_to(root).as_posix(),
                        "line": method.lineno,
                        "source_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                        "description": ast.get_docstring(method) or "",
                        "dependencies": sorted(set(calls)),
                    }
                )
    return {
        "schema_version": 1,
        "scope": "Imported wizwalker public methods; existing archive index reviewed separately. Not exhaustive client introspection.",
        "client_access": False,
        "entries": entries,
    }


if __name__ == "__main__":
    root = Path(__file__).resolve().parents[1]
    data = inventory(root)
    output = root / "research/w101-data-supply.json"
    output.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print(f"Inventoried {len(data['entries'])} declarations without executing readers.")
