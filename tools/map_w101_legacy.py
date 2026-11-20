"""Build Maelstrom's read-only legacy capability catalogue from test evidence.

Alternatives retain independent source IDs and qualification. A catalogue entry
never grants execution, enables a routine, or installs an upstream application.
"""

from __future__ import annotations

import argparse
from collections import Counter
from datetime import datetime, timezone
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
# archive, stable adapter candidate ID, areas, Maelstrom destination, constraints
MAPPING = [
    (
        "afk-wizard-main.zip",
        "afk-visual",
        ["Auto-Idle", "Combat"],
        "Conditional battle cues and motion proposals",
        "Missing book is treated as battle; obscured frames can trigger false decisions. Unbounded loops and global input need replacement.",
    ),
    (
        "Aurorium-main.zip",
        "aurorium-assets",
        ["Assets", "Versioning"],
        "Client asset/version provenance",
        "Downloader/server, not a game player. Offline build lacks roxmltree and potentially further dependencies.",
    ),
    (
        "Automatus-v2-master.zip",
        "automatus-v2",
        ["Navigation", "Experiments"],
        "Waypoint route adapter and typed run statistics",
        "Project marked discontinued. Route following is not enemy avoidance. append_point/remove_point reference os without importing it.",
    ),
    (
        "cheat-engine-master.zip",
        "cheat-engine-reference",
        ["Developer tools"],
        "Inspection UX and diagnostic plugin patterns",
        "Lazarus/FPC build unavailable; drivers and invasive components are not part of Maelstrom's gameplay loop.",
    ),
    (
        "Deimos-Wizard101-main.zip",
        "deimos-main",
        ["Pets", "Quests", "Combat", "Navigation", "Routine language"],
        "Branching routine compiler and optional privileged teacher adapter",
        "Memory-backed dance labels must be tagged separately from pixel observations. Coordinate proximity and malformed deck handling have reproducible defects; client hooks unqualified.",
    ),
    (
        "Deimos-Wizard101-master.zip",
        "deimos-master",
        ["Pets", "Quests", "Combat", "Navigation"],
        "Independent older workflow/navigation comparison",
        "Keep separate from main: dependencies, code and behavior differ. Coordinate proximity defect reproduced. Current-client hooks unqualified.",
    ),
    (
        "gardening-bot-main.zip",
        "gardening-visual",
        ["Farming", "Navigation", "Routine language"],
        "Plant states, visual waits and home movement chains",
        "Use Maelstrom's action owner for execution: interrupted legacy key presses can remain held. Energy, planting and harvesting outcomes need separate qualification.",
    ),
    (
        "GrubNinja-master.zip",
        "grubninja-reference",
        ["Pets", "Game context"],
        "Grub Guardian maps and UI/state vocabulary",
        "validateSession.js contains a syntax error (stray closing bracket). Reviewed reward path generates scores and submits service calls; it is not a visual gameplay demonstration. Do not use reward responses as training outcomes.",
    ),
    (
        "libkiwad-master.zip",
        "libkiwad-reader",
        ["Assets"],
        "Alternative native asset reader",
        "Plain-entry read tested; compressed reads explicitly unimplemented. Initializer compares a non-terminated magic buffer and requires repair before use.",
    ),
    (
        "On-Wiz-master.zip",
        "on-wiz-reference",
        ["Developer tools", "Game context"],
        "Client/protocol research notes",
        "Documentation only. Historical command-line/service notes are not current compatibility evidence.",
    ),
    (
        "petdance.zip",
        "petdance-visual",
        ["Pets", "Visual recognition"],
        "Calibrated visual Pet Dance teacher and draft action pack",
        "Pet Dance teacher is available under Observe & teach: imports eight pinned images, calibrates scale and reviews recordings into proposal packs. Archive is TAR despite its name. Current W101 Watch/Go/results and live input remain unqualified.",
    ),
    (
        "ProjectMaelstrom_win-x64_portable.zip",
        "maelstrom-historical",
        ["Application UX"],
        "Historical portable application comparison",
        "Preserved binary distribution; not executed or substituted for the current app.",
    ),
    (
        "raid-utility-tools-main.zip",
        "raid-specialists",
        ["Quests", "Navigation", "Game context"],
        "Raid activity vocabulary, location context and themes",
        "Four specialist variants retained. Qt/memobj/fork dependencies and client hooks not qualified; no raid operation or teleport tested.",
    ),
    (
        "w101-proto-go-master.zip",
        "protocol-go",
        ["Developer tools", "Game context"],
        "Typed protocol vocabulary, including minigame messages",
        "Build requires unavailable w101-client-go module. No traffic replay, login or reward calls run; schemas are not visual players.",
    ),
    (
        "wiz-packet-map-main.zip",
        "packet-map-reference",
        ["Developer tools"],
        "Historical packet schema research",
        "Legacy x86 injection and client-specific offsets; MSVC/Crypto++ setup unavailable. No interception or game attachment performed.",
    ),
    (
        "wizAPI-master.zip",
        "wizapi-visual",
        ["Combat", "Farming", "Navigation", "Visual recognition"],
        "Visual spell and UI action alternative",
        "Non-square template center is wrong. Fixed 800x600/Regular UI/DPI assumptions, global activation and unbounded waits require adaptation.",
    ),
    (
        "Wizard101-Bazaar-Tc-Farming-master.zip",
        "bazaar-visual",
        ["Farming", "Visual recognition"],
        "Item/menu templates and conditional page-search proposals",
        "Image matching tested on fixtures only. Purchase loops are not enabled; missing-image coordinates and RGB/BGR conversion need adapter checks.",
    ),
    (
        "Wizard101-Damage-Calculator-master.zip",
        "damage-evaluator",
        ["Combat", "Game context"],
        "Candidate consequence evaluator",
        "Pure library compiles under net9, but repeated calculations mutate damage; enchant/resistance/boost checks fail. Do not use it as decision truth yet.",
    ),
    (
        "Wizard101-Farming-main.zip",
        "farming-opencv",
        ["Farming", "Combat", "Visual recognition"],
        "Card matcher and enchant/attack proposal sequence",
        "Basic visual fixtures pass. Matching returns window-relative coordinates while input expects screen coordinates; fixed deck/order and indefinite loops remain.",
    ),
    (
        "Wizard101-Utilities-master.zip",
        "utilities-context",
        ["Game context", "Pets", "Combat"],
        "Gear/pet data and research schema helpers",
        "Historical data requires freshness checks. Scrapers and database population scripts not run against external services.",
    ),
    (
        "WizGallery-dev.zip",
        "gallery-context",
        ["Observations", "Game context"],
        "Screenshot library and location metadata patterns",
        "Database-backed web app. Dependencies/services absent; import-time database connections and migration scripts not executed.",
    ),
    (
        "wizSDK-master.zip",
        "wizsdk-visual",
        ["Visual recognition", "Combat", "Navigation"],
        "Visual utility alternative and client control vocabulary",
        "Non-square visual match and blank fixture pass. Capture/input integration, resolution assumptions and client dependencies remain unqualified.",
    ),
    (
        "wizwad-main.zip",
        "wizwad-reader",
        ["Assets"],
        "Python asset indexing alternative",
        "V1/V2 plain/compressed reads pass independent fixtures. Invalid magic is accepted; add strict header/size checks. Writer dependency absent; writer not tested.",
    ),
    (
        "wizwalker-master.zip",
        "wizwalker-client",
        ["Navigation", "Combat", "Pets", "Assets"],
        "Optional client adapter and WAD metadata reader",
        "V1/V2 journals tested. Client memory offsets and live hooks are a separate compatibility gate; no attachment performed.",
    ),
    (
        "WizWikiAPI-main.zip",
        "wiki-context",
        ["Game research", "Game context"],
        "Game categories, world context and guide schema",
        "Historical world data parsed; README says API no longer works. TypeScript/compiler dependencies absent. No access-control workarounds attempted.",
    ),
]


def build_catalog(audit, components, builds, damage=None):
    reviewed = {p["archive"]: p for p in audit["projects"]}
    expected = {m[0] for m in MAPPING}
    if set(reviewed) != expected or len(reviewed) != len(audit["projects"]):
        raise ValueError(
            "Archive collection changed; review and map every new or missing item explicitly"
        )
    if not components.get("complete"):
        raise ValueError("Component test run is incomplete")
    all_checks = components["checks"] + builds["checks"]
    if damage:
        all_checks += [{**c, "archive": damage["archive"]} for c in damage["checks"]]
    entries = []
    for archive, key, areas, destination, limits in MAPPING:
        project = reviewed[archive]
        checks = [c for c in all_checks if c["archive"] == archive]
        failed = [c["test"] for c in checks if c["status"] == "failed"]
        behavior = [
            c
            for c in checks
            if c["test"]
            not in {
                "image_assets_decode",
                "application_readiness",
                "javascript_parse",
                "literal_theme_data",
                "world_context_schema",
            }
        ]
        qualification = (
            "Repair required"
            if failed
            else (
                "Offline components tested"
                if any(c["status"] == "passed" for c in behavior)
                else "Reference reviewed"
            )
        )
        evidence = []
        for check in checks:
            detail = check.get("detail", "")
            if isinstance(detail, dict):
                if "decoded_images" in detail:
                    detail = f"{detail['decoded_images']} images opened; recognition accuracy not measured"
                elif "programs" in detail:
                    detail = f"{detail['programs']} programs compiled; no gameplay instructions executed"
                else:
                    detail = json.dumps(detail, ensure_ascii=False)
            label = check["test"].replace("_", " ").replace("-", " ").capitalize()
            status = {
                "passed": "Passed",
                "failed": "Failed",
                "blocked": "Build blocked",
                "not_qualified": "Not qualified",
                "not_applicable": "Not applicable",
                "partial": "Partly checked",
            }.get(check["status"], check["status"])
            evidence.append(f"{label}: {status}" + (f" — {detail}" if detail else ""))
        evidence.insert(
            0,
            f"Archive integrity: {project['integrity']}; parsed files: {project['static_passed']}; parse failures: {project['static_failed']}",
        )
        if project["integrity"] != "passed" or project["static_failed"]:
            qualification = "Review required"
        entries.append(
            {
                "id": key,
                "archive": archive,
                "sha256": project["sha256"],
                "capabilities": areas,
                "destination": destination,
                "qualification": qualification,
                "integration": "Catalogue only",
                "liveQualified": False,
                "limitations": limits,
                "evidence": evidence,
            }
        )
    return {
        "schemaVersion": 1,
        "reviewedUtc": datetime.now(timezone.utc).isoformat(),
        "scope": "25 supplied root archives. Recursive inventory also lists generated releases, dependency fixtures and prior reference downloads separately.",
        "entries": entries,
    }


def render_report(catalog):
    lines = [
        "# Wizard101 legacy qualification matrix",
        "",
        "Generated from local integrity, component, build and data checks. This is a",
        "review baseline, not a claim that 25 complete applications work with current W101.",
        "No upstream bot, game hook, account service or reward endpoint was started.",
        "W101 was left in the user's native Idle mode throughout offline testing.",
        "",
        "Each source remains a distinct candidate. Catalogue entries do not execute code",
        "or make game packs playable. Live qualification and redistribution review remain",
        "separate gates. Pet Dance's earlier synthetic rehearsal is documented in",
        "[W101_LEGACY_TEACHERS.md](W101_LEGACY_TEACHERS.md).",
        "",
        "| Project | Maelstrom areas | Qualification |",
        "| --- | --- | --- |",
    ]
    for entry in catalog["entries"]:
        lines.append(
            f"| `{entry['archive']}` | {', '.join(entry['capabilities'])} | {entry['qualification']} |"
        )
    lines += ["", "## Overlapping approaches", ""]
    for capability, count in sorted(
        Counter(c for e in catalog["entries"] for c in e["capabilities"]).items()
    ):
        if count > 1:
            alternatives = [
                e["id"] for e in catalog["entries"] if capability in e["capabilities"]
            ]
            lines.append(
                f"- **{capability}**: "
                + ", ".join(f"`{a}`" for a in alternatives)
                + "."
            )
    for entry in catalog["entries"]:
        lines += [
            "",
            f"## {entry['archive']}",
            "",
            f"Candidate ID: `{entry['id']}`. SHA-256: `{entry['sha256']}`.",
            "",
            entry["destination"] + ".",
            "",
            entry["limitations"],
            "",
            *["- " + e for e in entry["evidence"]],
        ]
    lines += [
        "",
        "## Reproduction and limits",
        "",
        "Run `tools/audit_w101_legacy.py`, then `tools/verify_w101_legacy_components.py`,",
        "`tools/verify_w101_legacy_builds.py`, and `tools/map_w101_legacy.py` using the",
        "existing isolated Dance environment. Reports live in `.local/legacy-validation`.",
        "Sources remain in hash-named directories; originals are untouched. Function",
        "probes enforce `research/w101-legacy-reviewed-files.json` hashes. Build probes",
        "operate on the reviewed local source copies; review changes before rerunning.",
        "",
        "C/C# probes use constructed data, not the upstream desktop entry points. The C#",
        "library is built under net9; this does not qualify its original net5 UI. A passing",
        "probe process can report failing behavioral assertions; those are included above.",
        "The Python WAD writer and full client integration are not qualified. Historical",
        "documents/data have no gameplay runtime to test. JSX/TypeScript need their own",
        "compiler dependencies; Node syntax checks are not web-app builds.",
        "",
    ]
    return "\n".join(lines)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--evidence", type=Path, default=ROOT / ".local/legacy-validation"
    )
    parser.add_argument("--pet-dance-evidence", type=Path)
    args = parser.parse_args()
    load = lambda name: json.loads((args.evidence / name).read_text(encoding="utf8"))
    damage_lines = (
        (args.evidence / "damage-library-probe.log")
        .read_text(encoding="utf8")
        .splitlines()
    )
    damage = next(
        (json.loads(line) for line in damage_lines if line.startswith('{"archive":')),
        None,
    )
    components = load("components.json")
    rehearsal_path = args.evidence.parent / "legacy-teachers/dance-bootstrap-check.json"
    if rehearsal_path.exists():
        rehearsal = json.loads(rehearsal_path.read_text(encoding="utf8"))
        if (
            rehearsal.get("mode") == "offline_teacher_rehearsal"
            and rehearsal.get("total_sequences", 0) > 0
        ):
            components["checks"].append(
                {
                    "archive": "petdance.zip",
                    "test": "reviewed_source_synthetic_sequences",
                    "status": (
                        "passed"
                        if rehearsal["exact_sequences"] == rehearsal["total_sequences"]
                        else "failed"
                    ),
                    "detail": f"{rehearsal['exact_sequences']}/{rehearsal['total_sequences']} with cue deduplication; earlier source rehearsal, no live frames or bundled EXE execution",
                }
            )
    if args.pet_dance_evidence:
        dance = json.loads(args.pet_dance_evidence.read_text(encoding="utf8"))
        if dance.get("status") != "passed" or dance.get("inputs_sent") != 0:
            raise ValueError(
                "Expected a completed offline Pet Dance verification report"
            )
        components["checks"].append(
            {
                "archive": "petdance.zip",
                "test": "native_app_teacher_pipeline",
                "status": "passed",
                "detail": (
                    f"{dance['scale_correct']}/{dance['scale_cases']} synthetic scaled cues; "
                    f"{dance['varied_rounds']} exact varied round proposals; "
                    f"{dance['sparse_proposals']} proposals on sparse footage; "
                    "no inputs, model, or live-game accuracy claim"
                ),
            }
        )
    catalog = build_catalog(load("audit.json"), components, load("builds.json"), damage)
    (
        ROOT / "ProjectMaelstrom/ProjectMaelstrom/Resources/LegacyW101Catalog.json"
    ).write_text(
        json.dumps(catalog, indent=2, ensure_ascii=False) + "\n", encoding="utf8"
    )
    (ROOT / "docs/W101_LEGACY_QUALIFICATION.md").write_text(
        render_report(catalog), encoding="utf8"
    )
    print(
        f"Mapped {len(catalog['entries'])} distinct candidates; none live-qualified or auto-enabled."
    )
