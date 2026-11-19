"""Validate advisory workstreams without running applications or game operations."""

import argparse
import json
from pathlib import Path, PurePosixPath
import re

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_BOARD = ROOT / "docs/coordination/workstreams.json"
MAX_BYTES = 1024 * 1024
STATUSES = {
    "backlog",
    "in_progress",
    "integration_pending",
    "blocked",
    "complete",
    "deferred",
}
ARCHIVE_SUFFIXES = (".zip", ".7z", ".rar", ".tar", ".tgz", ".tar.gz")
ID_PATTERN = re.compile(r"[A-Z][A-Z0-9]*(?:-[A-Z0-9]+)+\Z")


def unique_keys(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON key: " + key)
        result[key] = value
    return result


def load_board(path):
    with Path(path).open("rb") as stream:
        raw = stream.read(MAX_BYTES + 1)
    if len(raw) > MAX_BYTES:
        raise ValueError("Board exceeds 1 MiB.")
    return json.loads(raw.decode("utf-8"), object_pairs_hook=unique_keys)


def validate(board, root=ROOT, allow_missing_local=False):
    """Return errors; existing evidence must resolve inside root, planned outputs need not exist.

    Dependencies are checked for both offline tasks and later runtime composition.
    A completed offline component does not require live-game evidence. This checks
    record integrity and paths, not whether prose claims or reported tests are true.

    allow_missing_local lets a clean checkout (CI) skip .local/ evidence: those
    paths are gitignored workspace artifacts, so they cannot exist there. The
    default remains strict for the full workspace where they are expected.
    """
    root = Path(root).resolve(strict=True)
    errors = []

    def text(value):
        return isinstance(value, str) and bool(value.strip())

    def path_check(value, label, existing=True):
        if not text(value):
            errors.append(label + ": missing path")
            return
        parts = PurePosixPath(value).parts
        if (
            not parts
            or PurePosixPath(value).is_absolute()
            or "\\" in value
            or ":" in value
            or any(p in (".", "..") for p in parts)
            or "/".join(parts) != value
        ):
            errors.append(label + ": must be a normalized repository-relative path")
            return
        try:
            resolved = (root / value).resolve(strict=False)
            if not resolved.is_relative_to(root):
                errors.append(label + ": path escapes repository")
            elif existing and not resolved.is_file():
                if allow_missing_local and value.startswith(".local/"):
                    return None  # Gitignored workspace evidence; absent on clean checkouts.
                errors.append(label + ": existing evidence is missing: " + value)
            else:
                return resolved
        except (OSError, RuntimeError) as exc:
            errors.append(label + ": cannot resolve path: " + str(exc))

    def string_list(value, label, nonempty=False):
        if not isinstance(value, list) or (nonempty and not value):
            errors.append(
                label + ": expected " + ("nonempty " if nonempty else "") + "list"
            )
            return []
        if any(not text(x) for x in value):
            errors.append(label + ": entries must be nonempty strings")
            return []
        if len(value) != len(set(value)):
            errors.append(label + ": duplicate entries")
        return value

    if not isinstance(board, dict):
        return ["Board must be an object."]
    if type(board.get("schema_version")) is not int or board["schema_version"] != 1:
        errors.append("Unsupported schema_version.")
    if board.get("kind") != "maelstrom-project-workstreams":
        errors.append("Unsupported board kind.")
    if board.get("executable") is not False:
        errors.append("Board must explicitly be non-executable.")
    vocabulary = string_list(board.get("status_vocabulary"), "status_vocabulary")
    if set(vocabulary) != STATUSES:
        errors.append("status_vocabulary must match the supported statuses.")
    if "summary_document" in board:
        path_check(board["summary_document"], "summary_document")
    claim_path = None
    coordination = board.get("coordination", {})
    if not isinstance(coordination, dict):
        errors.append("coordination must be an object")
    elif "claim_registry" in coordination:
        claim_path = path_check(
            coordination["claim_registry"], "coordination.claim_registry"
        )
    for value in string_list(board.get("source_lineage", []), "source_lineage"):
        path_check(value, "source_lineage")

    rows = board.get("workstreams")
    if not isinstance(rows, list) or not 1 <= len(rows) <= 500:
        return errors + ["workstreams must contain 1 to 500 records."]
    by_id = {}
    graph = {}
    for index, row in enumerate(rows):
        label = "workstreams[" + str(index) + "]"
        if not isinstance(row, dict):
            errors.append(label + ": expected object")
            continue
        identity = row.get("id")
        if not text(identity) or not ID_PATTERN.fullmatch(identity):
            errors.append(label + ": invalid stable id")
            continue
        label = identity
        if identity in by_id:
            errors.append(label + ": duplicate id")
        by_id[identity] = row
        for field in ("title", "track", "owner", "next_task"):
            if not text(row.get(field)):
                errors.append(label + ": missing " + field)
        if not text(row.get("status")) or row["status"] not in STATUSES:
            errors.append(label + ": invalid status")
        if row.get("status") == "in_progress" and row.get("owner") == "unassigned":
            errors.append(label + ": in_progress needs an assigned owner")
        string_list(
            row.get("acceptance_criteria"), label + ".acceptance_criteria", True
        )
        graph[identity] = []
        for field in ("depends_on", "runtime_requires"):
            graph[identity].extend(string_list(row.get(field, []), label + "." + field))
        verification = row.get("verification")
        if not isinstance(verification, dict) or any(
            not text(verification.get(k)) for k in ("review", "code", "offline", "live")
        ):
            errors.append(
                label + ": verification needs review/code/offline/live scopes"
            )
        evidence = row.get("evidence")
        if not isinstance(evidence, list) or not evidence:
            errors.append(label + ": nonempty evidence required")
        else:
            for item in evidence:
                if not isinstance(item, dict):
                    errors.append(label + ": evidence must be an object")
                    continue
                if not text(item.get("kind")) or not text(item.get("scope")):
                    errors.append(label + ": evidence needs kind and scope")
                if item.get("path_status") != "existing":
                    errors.append(
                        label
                        + ": evidence must be existing; use planned_outputs for future files"
                    )
                path_check(item.get("path"), label + ".evidence")
        for value in string_list(
            row.get("planned_outputs", []), label + ".planned_outputs"
        ):
            path_check(value, label + ".planned_outputs", existing=False)

    for identity, dependencies in graph.items():
        for dependency in dependencies:
            if dependency not in by_id:
                errors.append(identity + ": unknown dependency " + dependency)
    visiting, visited = set(), set()

    def visit(identity):
        if identity in visiting:
            errors.append("Dependency cycle includes " + identity)
            return
        if identity in visited:
            return
        visiting.add(identity)
        for dependency in graph.get(identity, []):
            if dependency in by_id:
                visit(dependency)
        visiting.remove(identity)
        visited.add(identity)

    for identity in graph:
        visit(identity)

    if claim_path is not None:
        try:
            registry = load_board(claim_path)
            if (
                not isinstance(registry, dict)
                or type(registry.get("schema_version")) is not int
                or registry["schema_version"] != 1
                or registry.get("kind") != "maelstrom-task-claims"
                or registry.get("executable") is not False
                or not isinstance(registry.get("claims"), list)
            ):
                errors.append("Unsupported claim registry schema")
            else:
                tasks, active_paths = set(), {}
                for claim in registry["claims"]:
                    if not isinstance(claim, dict):
                        errors.append("Claim must be an object")
                        continue
                    task = claim.get("task_id")
                    if not text(task) or task in tasks:
                        errors.append("Missing or duplicate claim task_id")
                    else:
                        tasks.add(task)
                    if not text(claim.get("owner")):
                        errors.append("Claim needs an owner")
                    if (
                        not text(claim.get("workstream_id"))
                        or claim["workstream_id"] not in by_id
                    ):
                        errors.append("Claim references unknown workstream")
                    state = claim.get("state")
                    if state not in ("active", "released"):
                        errors.append("Invalid claim state")
                    for value in string_list(
                        claim.get("owned_paths"), "claim.owned_paths", True
                    ):
                        resolved = path_check(
                            value, "claim.owned_paths", existing=False
                        )
                        if any(char in value for char in "*?[]") or (
                            resolved is not None and resolved.is_dir()
                        ):
                            errors.append(
                                "Claims must name exact files, not globs or directories"
                            )
                        if state == "active":
                            key = value.casefold()
                            if key in active_paths:
                                errors.append("Overlapping active claim: " + value)
                            active_paths[key] = task
        except (OSError, ValueError, RuntimeError) as exc:
            errors.append("Cannot read claim registry: " + str(exc))

    inventory = board.get("archive_inventory")
    if inventory is not None:
        if not isinstance(inventory, dict):
            errors.append("archive_inventory must be an object")
        else:
            entries = inventory.get("root_archives")
            nested = inventory.get("nested_archives")
            if not isinstance(entries, list) or not isinstance(nested, list):
                errors.append(
                    "archive_inventory needs root_archives and nested_archives lists"
                )
            else:
                names, families, projects = [], set(), set()
                historical = inventory.get("historical_distribution")
                # Imported archives live outside the product tree. "archive_root"
                # (relative to the repository root, default ".") says where.
                archive_root = inventory.get("archive_root", ".")
                archive_dir = None
                if (
                    not text(archive_root)
                    or "\\" in archive_root
                    or ":" in archive_root
                ):
                    errors.append("archive_root must be a relative POSIX path")
                else:
                    archive_dir = (root / archive_root).resolve()
                for entry in entries:
                    if not isinstance(entry, dict):
                        errors.append("Invalid root archive record")
                        continue
                    name = entry.get("archive")
                    if (
                        not text(name)
                        or "/" in name
                        or "\\" in name
                        or not name.lower().endswith(ARCHIVE_SUFFIXES)
                    ):
                        errors.append("Invalid root archive name")
                        continue
                    names.append(name)
                    path_check(name, "root archive", existing=False)
                    project = entry.get("project_id")
                    if not text(project):
                        errors.append("Missing or invalid root archive project_id")
                    elif project in projects:
                        errors.append("Missing or duplicate root archive project_id")
                    else:
                        projects.add(project)
                    if name != historical:
                        family = entry.get("family_id")
                        if not text(family):
                            errors.append("Missing root archive family_id")
                        else:
                            families.add(family)
                    for identity in string_list(
                        entry.get("workstream_ids"), "archive workstream_ids", True
                    ):
                        if identity not in by_id:
                            errors.append(
                                "Archive references unknown workstream " + identity
                            )
                if len(names) != len(set(names)):
                    errors.append("Duplicate root archive names")
                if archive_dir is not None:
                    if not archive_dir.is_dir():
                        # The archive folder is outside git: absent on clean checkouts.
                        if not allow_missing_local:
                            errors.append(
                                "Archive folder is missing: " + str(archive_root)
                            )
                    else:
                        actual = {
                            p.name
                            for p in archive_dir.iterdir()
                            if p.is_file() and p.name.lower().endswith(ARCHIVE_SUFFIXES)
                        }
                        if set(names) != actual:
                            errors.append(
                                "Root archive inventory differs from current root files"
                            )
                if historical not in names:
                    errors.append("Historical distribution is not in root inventory")
                nested_names = []
                for entry in nested:
                    if not isinstance(entry, dict):
                        errors.append("Invalid nested archive record")
                        continue
                    path = entry.get("path")
                    path_check(path, "nested archive")
                    if text(path):
                        nested_names.append(path)
                if len(nested_names) != len(set(nested_names)):
                    errors.append("Duplicate nested archive names")
                expected = {
                    "root_archive_count": len(names),
                    "non_release_root_candidate_count": len(names)
                    - (historical in names),
                    "non_release_root_family_count": len(families),
                    "nested_non_release_count": len(nested),
                }
                releases = inventory.get("generated_release_count")
                if type(releases) is not int or releases < 0:
                    errors.append("Invalid generated_release_count")
                else:
                    expected["total_compressed_count"] = (
                        len(names) + len(nested) + releases
                    )
                for key, count in expected.items():
                    if type(inventory.get(key)) is not int or inventory[key] != count:
                        errors.append("Archive count mismatch: " + key)

    games = board.get("game_inventory")
    if games is not None:
        if not isinstance(games, dict):
            errors.append("game_inventory must be an object")
        else:
            identities = string_list(
                games.get("workstream_ids"), "game_inventory.workstream_ids"
            )
            if type(games.get("count")) is not int or games["count"] != len(identities):
                errors.append("Game inventory count mismatch")
            for identity in identities:
                if identity not in by_id:
                    errors.append(
                        "Game inventory references unknown workstream " + identity
                    )
            for track in ("fairground", "pet"):
                count = sum(
                    by_id.get(identity, {}).get("track") == track
                    for identity in identities
                )
                if (
                    type(games.get(track + "_count")) is not int
                    or games[track + "_count"] != count
                ):
                    errors.append("Game inventory track count mismatch: " + track)
    return errors


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("board", nargs="?", type=Path, default=DEFAULT_BOARD)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument(
        "--allow-missing-local",
        action="store_true",
        help="clean checkouts (CI) may lack gitignored .local/ evidence",
    )
    args = parser.parse_args(argv)
    try:
        board = load_board(args.board)
        errors = validate(
            board, args.root, allow_missing_local=args.allow_missing_local
        )
    except (OSError, ValueError, RuntimeError) as exc:
        print("Project board invalid: " + str(exc))
        return 1
    if errors:
        print("Project board invalid: " + str(len(errors)) + " issue(s)")
        for error in errors:
            print("- " + error)
        return 1
    print(
        "Project board valid: "
        + str(len(board["workstreams"]))
        + " workstreams; evidence paths and dependency graph checked."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
