"""Check the W101 integration plan without running tools or registering jobs."""

import argparse
from collections import Counter
import json
from pathlib import Path, PureWindowsPath

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_PLAN = ROOT / "research/w101-integration-map.json"
STRATEGIES = {
    "native-reuse",
    "repair-native",
    "adapt-teacher",
    "keep-optional-bridge",
    "data-import",
    "reference-only",
    "defer",
}


class PlanError(ValueError):
    """A coverage, reference or planning-boundary requirement was violated."""


def require(condition, message):
    if not condition:
        raise PlanError(message)


def local_file(root, value):
    """Accept only existing files inside the checkout, including resolved links."""
    require(isinstance(value, str) and value, "File reference must be a string")
    relative = PureWindowsPath(value)
    require(
        not relative.drive and not relative.root and ".." not in relative.parts,
        f"File reference must be repository-relative: {value}",
    )
    resolved = (root / value).resolve()
    require(resolved.is_relative_to(root.resolve()), f"File escapes checkout: {value}")
    require(resolved.is_file(), f"Missing evidence file: {value}")
    return resolved


def rows_by_id(rows, name):
    require(isinstance(rows, list) and rows, f"{name} must be a nonempty list")
    result = {}
    for row in rows:
        require(isinstance(row, dict), f"Invalid {name} entry")
        identity = row.get("id")
        require(isinstance(identity, str) and identity, f"Missing {name} ID")
        require(identity not in result, f"Duplicate {name} ID: {identity}")
        result[identity] = row
    return result


def references(row, field, known, allow_empty=False):
    values = row.get(field)
    require(isinstance(values, list), f"{row['id']}: {field} must be a list")
    require(allow_empty or values, f"{row['id']}: empty {field}")
    require(all(isinstance(v, str) for v in values), f"Invalid {field} reference")
    require(len(values) == len(set(values)), f"{row['id']}: duplicate {field}")
    require(
        set(values) <= set(known),
        f"{row['id']}: unknown {field}: {sorted(set(values) - set(known))}",
    )
    return values


def validate(plan, catalog, root=ROOT):
    """Return a topological work order; reject omissions and premature readiness."""
    require(isinstance(plan, dict), "Plan must be an object")
    require(plan.get("schema_version") == 1, "Unsupported plan schema")
    require(plan.get("kind") == "integration-plan", "Not an integration plan")
    require(plan.get("executable") is False, "Plan must remain non-executable")
    local_file(root, plan.get("catalog"))
    require(isinstance(catalog, dict), "Catalog must be an object")
    archive_ids = rows_by_id(catalog.get("entries"), "catalog")
    capabilities = rows_by_id(plan.get("capabilities"), "capabilities")
    sources = rows_by_id(plan.get("sources"), "sources")
    operations = rows_by_id(plan.get("operations"), "operations")
    work = rows_by_id(plan.get("work_packages"), "work packages")

    covered = []
    for source in sources.values():
        references(source, "strategy", STRATEGIES)
        references(source, "capabilities", capabilities)
        catalog_id = source.get("catalog_id")
        if catalog_id is None:
            evidence = source.get("evidence")
            require(
                isinstance(evidence, list) and evidence, "Extra source needs evidence"
            )
            for path in evidence:
                local_file(root, path)
        else:
            require(catalog_id in archive_ids, f"Unknown catalog ID: {catalog_id}")
            covered.append(catalog_id)
    require(
        Counter(covered) == Counter(archive_ids.keys()),
        "Every catalog archive must have exactly one source mapping",
    )
    for capability in capabilities.values():
        paths = capability.get("existing")
        require(isinstance(paths, list) and paths, "Capability needs existing evidence")
        for path in paths:
            local_file(root, path)

    dependencies = {}
    for identity, row in work.items():
        dependencies[identity] = set(references(row, "depends_on", work, True))
        criteria = row.get("done_when")
        require(
            isinstance(criteria, list)
            and criteria
            and all(isinstance(c, str) and c.strip() for c in criteria),
            f"{identity}: missing acceptance criteria",
        )
    order = []
    ancestors = {}
    while len(order) < len(work):
        ready = sorted(
            identity
            for identity, deps in dependencies.items()
            if identity not in ancestors and deps <= ancestors.keys()
        )
        require(ready, "Work dependencies contain a cycle")
        for identity in ready:
            inherited = {identity}
            for dependency in dependencies[identity]:
                inherited.update(ancestors[dependency])
            ancestors[identity] = inherited
            order.append(identity)
    for operation in operations.values():
        references(operation, "providers", sources)
        references(operation, "capabilities", capabilities)
        references(
            operation, "backends", {"native", "legacy-teacher", "optional-worker"}
        )
        gate = operation.get("gate")
        require(gate in work, f"{operation['id']}: unknown gate {gate}")
        require(
            operation.get("scheduler_ready") is False,
            f"{operation['id']}: plan cannot assert scheduler readiness",
        )
        require(
            type(operation.get("writes_game")) is bool, "writes_game must be boolean"
        )
        if operation["writes_game"]:
            require(
                {"w01", "w02", "w03"} <= ancestors[gate],
                f"{operation['id']}: live gate needs ownership, capture and Idle cleanup",
            )
            require(
                "target-input" in operation["capabilities"],
                f"{operation['id']}: live operation needs target-input",
            )
        outcome = operation.get("outcome")
        require(
            isinstance(outcome, str) and outcome.strip(), "Missing outcome criterion"
        )

    defaults = plan.get("defaults")
    require(isinstance(defaults, dict), "Missing resource defaults")
    for field, value in {
        "live_input_owners": 1,
        "analysis_workers": 1,
        "models_loaded_by_default": 0,
        "training_during_live_gameplay": False,
        "unbounded_repeats": False,
        "live_restart_resume": "require-new-preflight-and-explicit-resume",
        "between_live_w101_tests": "verify-native-idle-through-screen-menu",
        "unknown_outcome": "preserve-evidence-and-pause-dependent-live-work",
    }.items():
        require(
            type(defaults.get(field)) is type(value) and defaults[field] == value,
            f"Unexpected lightweight/ownership default: {field}",
        )
    limit = defaults.get("queue_limit_proposed")
    require(
        type(limit) is int and 0 < limit <= 32, "Proposed queue must be bounded at 32"
    )
    return order


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plan", type=Path, default=DEFAULT_PLAN)
    args = parser.parse_args()
    try:
        plan = json.loads(args.plan.read_text(encoding="utf-8"))
        catalog = json.loads(
            local_file(ROOT, plan.get("catalog")).read_text(encoding="utf-8")
        )
        order = validate(plan, catalog)
    except (OSError, ValueError, TypeError, AttributeError) as error:
        parser.exit(1, f"Integration map failed: {error}\n")
    print(
        f"Valid plan: {len(catalog['entries'])} archives, {len(plan['sources'])} sources, "
        f"{len(plan['capabilities'])} capabilities, {len(plan['operations'])} operations."
    )
    print("Dependency order: " + " -> ".join(order))
    print("Planning only: no jobs registered, programs executed or game inputs sent.")


if __name__ == "__main__":
    main()
