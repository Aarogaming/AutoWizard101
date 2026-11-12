"""Check a partial, non-executable source audit; never import or run the mapped code."""

import argparse
from datetime import date
import json
from pathlib import Path, PurePosixPath
import re

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_INVENTORY = ROOT / "research/maelstrom-capabilities.json"
MAX_BYTES = 256 * 1024
MAX_SOURCE_BYTES = 1024 * 1024
MAX_TOTAL_SOURCE_BYTES = 8 * 1024 * 1024
ID_PATTERN = re.compile(r"audit\.[a-z][a-z0-9]*(?:-[a-z0-9]+)*\Z")
DECK_STATUSES = {
    "not-applicable",
    "not-connected",
    "library-only",
    "bound-limited",
    "shared-service",
}


def unique_keys(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON key: " + key)
        result[key] = value
    return result


def reject_constant(value):
    raise ValueError("Non-finite JSON constant: " + value)


def load_inventory(path):
    with Path(path).open("rb") as stream:
        raw = stream.read(MAX_BYTES + 1)
    if len(raw) > MAX_BYTES:
        raise ValueError("Inventory exceeds 256 KiB.")
    return json.loads(
        raw.decode("utf-8"),
        object_pairs_hook=unique_keys,
        parse_constant=reject_constant,
    )


def validate(inventory, root=ROOT):
    """Validate structure and existing references, not the truth of capability claims.

    Anchors are literal source substrings, never executable expressions. Installed
    and live status remain not-assessed: this inventory cannot grant qualification.
    """
    root = Path(root).resolve(strict=True)
    errors, cache = [], {}
    source_bytes = 0

    def text(value, maximum=1200):
        return isinstance(value, str) and 0 < len(value.strip()) <= maximum

    def shape(value, keys, label):
        if not isinstance(value, dict) or set(value) != set(keys.split()):
            errors.append(label + ": invalid fields/object")
            return False
        return True

    def path_check(value, label):
        if not text(value, 240):
            errors.append(label + ": invalid path")
            return None
        parts = PurePosixPath(value).parts
        if (
            not parts
            or PurePosixPath(value).is_absolute()
            or "\\" in value
            or ":" in value
            or any(p in (".", "..") for p in parts)
            or "/".join(parts) != value
            or any(ord(c) < 32 for c in value)
        ):
            errors.append(label + ": path must be normalized and repository-relative")
            return None
        try:
            resolved = (root / value).resolve(strict=True)
            if not resolved.is_relative_to(root):
                errors.append(label + ": path escapes repository")
            elif not resolved.is_file():
                errors.append(label + ": path is not a file")
            else:
                return resolved
        except (OSError, RuntimeError, ValueError):
            errors.append(label + ": missing or invalid reference: " + value)
        return None

    def source_check(reference, label):
        nonlocal source_bytes
        if not shape(reference, "path anchor", label):
            return
        path = path_check(reference["path"], label)
        anchor = reference["anchor"]
        if not text(anchor, 256):
            errors.append(label + ": invalid anchor")
            return
        if path is None:
            return
        if path not in cache:
            if source_bytes >= MAX_TOTAL_SOURCE_BYTES:
                errors.append(label + ": total source-read budget exceeded")
                return
            try:
                allowance = min(MAX_SOURCE_BYTES, MAX_TOTAL_SOURCE_BYTES - source_bytes)
                with path.open("rb") as stream:
                    raw = stream.read(allowance + 1)
                source_bytes += len(raw)
                if len(raw) > allowance:
                    errors.append(label + ": source-read budget exceeded")
                    cache[path] = None
                    return
                cache[path] = raw.decode("utf-8-sig")
            except (OSError, UnicodeError):
                cache[path] = None
                errors.append(label + ": cannot read UTF-8 source")
                return
        if cache[path] is not None and anchor not in cache[path]:
            errors.append(label + ": source anchor missing: " + anchor)

    def strings(value, label, minimum=1, maximum=8):
        if not isinstance(value, list) or not minimum <= len(value) <= maximum:
            errors.append(label + ": invalid list size/type")
            return []
        if any(not text(item) for item in value):
            errors.append(label + ": expected nonempty strings")
            return []
        if len(set(value)) != len(value):
            errors.append(label + ": duplicate entries")
        return value

    if not shape(
        inventory,
        "schema_version kind executable audit_date coverage verification_boundary capabilities",
        "inventory",
    ):
        return errors
    if type(inventory["schema_version"]) is not int or inventory["schema_version"] != 1:
        errors.append("Unsupported schema_version.")
    if inventory["kind"] != "maelstrom-capability-audit":
        errors.append("Unsupported inventory kind.")
    if inventory["executable"] is not False:
        errors.append("Inventory must explicitly be non-executable.")
    try:
        value = inventory["audit_date"]
        if not isinstance(value, str) or date.fromisoformat(value).isoformat() != value:
            raise ValueError()
    except (TypeError, ValueError):
        errors.append("Invalid audit_date.")
    if inventory["coverage"] != "selected-workflow-families-not-exhaustive":
        errors.append("Coverage must explicitly remain partial.")
    if not text(inventory["verification_boundary"]):
        errors.append("Missing verification_boundary.")
    rows = inventory["capabilities"]
    if not isinstance(rows, list) or not 1 <= len(rows) <= 64:
        return errors + ["capabilities must contain 1 to 64 records."]
    ids, links = set(), []
    for index, row in enumerate(rows):
        label = "capabilities[" + str(index) + "]"
        if not shape(
            row,
            "id title scope source_refs callable_surfaces limits deck_binding verification evidence next_gap related",
            label,
        ):
            continue
        identity = row["id"]
        if not text(identity, 80) or not ID_PATTERN.fullmatch(identity):
            errors.append(label + ": invalid audit id")
        else:
            label = identity
            if identity in ids:
                errors.append(label + ": duplicate id")
            ids.add(identity)
        for field in ("title", "scope", "next_gap"):
            if not text(row[field]):
                errors.append(label + ": invalid " + field)
        strings(row["callable_surfaces"], label + ".callable_surfaces")
        strings(row["limits"], label + ".limits")
        refs = row["source_refs"]
        if not isinstance(refs, list) or not 1 <= len(refs) <= 6:
            errors.append(label + ": expected 1 to 6 source_refs")
        else:
            for ref in refs:
                source_check(ref, label)
        deck = row["deck_binding"]
        if shape(deck, "status detail", label + ".deck_binding"):
            if (
                not isinstance(deck["status"], str)
                or deck["status"] not in DECK_STATUSES
            ):
                errors.append(label + ": invalid deck status")
            if not text(deck["detail"]):
                errors.append(label + ": missing deck detail")
        verification = row["verification"]
        if shape(verification, "source installed live", label + ".verification"):
            if verification != {
                "source": "present",
                "installed": "not-assessed",
                "live": "not-assessed",
            }:
                errors.append(
                    label
                    + ": source audit cannot grant installation/live qualification"
                )
        evidence = row["evidence"]
        if not isinstance(evidence, list) or not 1 <= len(evidence) <= 8:
            errors.append(label + ": expected 1 to 8 evidence entries")
        else:
            for item in evidence:
                if shape(item, "path scope", label + ".evidence"):
                    path_check(item["path"], label + ".evidence")
                    if not text(item["scope"]):
                        errors.append(label + ": missing evidence scope")
        for related in strings(row["related"], label + ".related", minimum=0):
            links.append((label, related))
    for identity, target in links:
        if target not in ids or identity == target:
            errors.append(identity + ": invalid related reference: " + target)
    return errors


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inventory", nargs="?", type=Path, default=DEFAULT_INVENTORY)
    args = parser.parse_args(argv)
    try:
        inventory = load_inventory(args.inventory)
        errors = validate(inventory)
    except (OSError, ValueError, RecursionError) as exc:
        errors = [str(exc)]
    if errors:
        for error in errors:
            print("ERROR: " + error)
        return 1
    print(
        f"Capability audit valid: {len(inventory['capabilities'])} selected workflow families; "
        "non-executable, partial coverage, source references only."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
