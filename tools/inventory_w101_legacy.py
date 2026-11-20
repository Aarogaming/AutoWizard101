"""Read legacy archives as data; never extract or execute their entry points."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import tarfile
import zipfile

TEXT = {".py", ".cs", ".rs", ".js", ".ts", ".ahk", ".lua", ".md", ".txt"}
SIGNALS = {
    "visual": r"matchTemplate|locateOnScreen|locateCenterOnScreen|screenshot|ImageSearch",
    "input": r"send_key|keyDown|keyUp|SendInput|PostMessage|pyautogui\.press",
    "memory": r"ReadProcessMemory|WriteProcessMemory|pymem|SimpleHook|read_current_dance_game_moves",
    "routine_language": r"compile_if_stmt|compile_loop_stmt|wait_until_visible|perform_movements",
    "pet_or_minigame": r"dance.?game|pet.?train|conjuration|potion.?motion|dueling.?diego",
}


def digest(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def inspect(path: Path) -> dict:
    result = {
        "archive": path.name,
        "sha256": digest(path),
        "bytes": path.stat().st_size,
        "mode": "static_read_only",
        "images": 0,
        "licenses": [],
        "readmes": [],
        "signals": [],
        "text_bytes_read": 0,
        "truncated": False,
    }

    def collect(name: str, size: int, read):
        suffix, base = Path(name).suffix.lower(), Path(name).name.lower()
        if suffix in {".png", ".jpg", ".jpeg", ".bmp"}:
            result["images"] += 1
        if base.startswith(("license", "copying")):
            result["licenses"].append(name)
        if base.startswith("readme"):
            result["readmes"].append(name)
        if (
            suffix not in TEXT
            or size > 300_000
            or any(
                p in name.lower()
                for p in ("site-packages/", "node_modules/", "_internal/")
            )
        ):
            return
        if result["text_bytes_read"] + size > 16 * 1024 * 1024:
            result["truncated"] = True
            return
        raw = read(300_001)
        if len(raw) > 300_000:
            result["truncated"] = True
            return
        result["text_bytes_read"] += len(raw)
        text = raw.decode("utf-8", errors="replace")
        for kind, pattern in SIGNALS.items():
            matches = [
                {"line": i, "text": line.strip()[:180]}
                for i, line in enumerate(text.splitlines(), 1)
                if re.search(pattern, line, re.IGNORECASE)
            ]
            if matches:
                result["signals"].append(
                    {
                        "file": name,
                        "kind": kind,
                        "matches": matches[:6],
                        "count": len(matches),
                    }
                )

    if zipfile.is_zipfile(path):
        result["format"] = "zip"
        with zipfile.ZipFile(path) as archive:
            members = archive.infolist()
            result["entries"] = len(members)
            for member in members:
                if member.is_dir():
                    continue

                def read(limit, member=member):
                    with archive.open(member) as stream:
                        return stream.read(limit)

                collect(member.filename, member.file_size, read)
    elif tarfile.is_tarfile(path):
        result["format"] = "tar"
        with tarfile.open(path) as archive:
            result["entries"] = 0
            for member in archive:
                result["entries"] += 1
                if not member.isfile():
                    continue

                def read(limit, member=member):
                    with archive.extractfile(member) as stream:
                        return stream.read(limit)

                collect(member.name, member.size, read)
    else:
        result["format"] = "unrecognized"
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--root", type=Path, default=Path(__file__).resolve().parents[1]
    )
    parser.add_argument(
        "--paths",
        type=Path,
        required=True,
        help="Recursive archive path inventory, one relative path per line",
    )
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    root = args.root.resolve()
    records = []
    for line in args.paths.read_text(encoding="utf-8-sig").splitlines():
        path = (root / line).resolve()
        if not path.is_relative_to(root) or not path.is_file():
            raise ValueError("Archive path must be a file under the workspace")
        relative = path.relative_to(root)
        if relative.parts[0] == ".local":
            role = (
                "generated_build"
                if relative.parts[1] == "releases"
                else "prior_download_or_nested_reference"
            )
            records.append(
                {
                    "path": relative.as_posix(),
                    "bytes": path.stat().st_size,
                    "mode": "catalogued_only",
                    "role": role,
                }
            )
        else:
            record = inspect(path)
            record["path"] = relative.as_posix()
            record["role"] = "user_supplied_legacy"
            records.append(record)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    report = {
        "schema_version": 1,
        "archives": records,
        "note": "Static signals are review leads, not working features. No legacy code or binary was launched. Nested prior downloads/builds are catalogued separately.",
    }
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(
        json.dumps(
            {
                "catalogued": len(records),
                "inspected": sum(r["mode"] == "static_read_only" for r in records),
                "report": str(args.output),
            }
        )
    )


if __name__ == "__main__":
    main()
