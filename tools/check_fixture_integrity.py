"""Check every file in an offline fixture manifest, without loading game tools.

Inspired by Workbench's artifact digest verification; implemented for Maelstrom's
existing fixture format. See docs/REPOSITORY_REUSE_REVIEW.md for source provenance.
Hashes detect changed bytes, not authenticity or successful gameplay.
"""

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re

MAX_MANIFEST = 1024 * 1024
MAX_FILE = 64 * 1024 * 1024


def unique_keys(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate manifest key: " + key)
        result[key] = value
    return result


def verify(manifest):
    manifest = Path(manifest).resolve(strict=True)
    root = manifest.parent
    with manifest.open("rb") as stream:
        raw = stream.read(MAX_MANIFEST + 1)
    if len(raw) > MAX_MANIFEST:
        raise ValueError("Fixture manifest exceeds 1 MiB.")
    data = json.loads(raw.decode("utf-8"), object_pairs_hook=unique_keys)
    if (
        not isinstance(data, dict)
        or type(data.get("fixtureSetVersion")) is not int
        or data["fixtureSetVersion"] != 1
        or data.get("kind") != "autowizard101-offline-observer-fixtures"
    ):
        raise ValueError("Unsupported fixture manifest.")
    files = data.get("files")
    if not isinstance(files, dict) or not 1 <= len(files) <= 10000:
        raise ValueError("Manifest needs a nonempty bounded file map.")
    errors = []
    checked = 0
    identities = set()
    for name, expected in files.items():
        try:
            parts = PurePosixPath(name).parts
            if (
                not parts
                or PurePosixPath(name).is_absolute()
                or any(p in (".", "..") for p in parts)
                or "\\" in name
                or ":" in name
                or "/".join(parts) != name
            ):
                raise ValueError("invalid relative path")
            path = (root / name).resolve(strict=True)
            # Resolve before reading so a symlink/junction cannot escape the pack.
            # This is an offline check, not a sandbox against concurrent mutations.
            if not path.is_relative_to(root) or path == manifest or not path.is_file():
                raise ValueError("not a contained fixture file")
            if path in identities:
                raise ValueError("duplicate resolved fixture path")
            identities.add(path)
            if (
                not isinstance(expected, dict)
                or type(expected.get("bytes")) is not int
                or not 0 <= expected["bytes"] <= MAX_FILE
                or not isinstance(expected.get("sha256"), str)
                or re.fullmatch(r"[0-9a-f]{64}", expected["sha256"]) is None
            ):
                raise ValueError("invalid size or SHA-256")
            digest = hashlib.sha256()
            size = 0
            with path.open("rb") as stream:
                while chunk := stream.read(1024 * 1024):
                    size += len(chunk)
                    if size > expected["bytes"]:
                        raise ValueError("size mismatch")
                    digest.update(chunk)
            if size != expected["bytes"] or digest.hexdigest() != expected["sha256"]:
                raise ValueError("size or digest mismatch")
            checked += 1
        except (OSError, ValueError) as exc:
            errors.append(f"{name}: {exc}")
    return {
        "kind": "maelstrom.fixture-integrity.v1",
        "passed": not errors,
        "checked": checked,
        "expected": len(files),
        "errors": errors,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    args = parser.parse_args()
    try:
        result = verify(args.manifest)
    except (OSError, ValueError) as exc:
        result = {"passed": False, "errors": [str(exc)]}
    print(json.dumps(result, indent=2))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
