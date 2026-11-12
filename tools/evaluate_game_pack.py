"""Create deterministic, read-only replay evidence for a data-only pack."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile
from collections import Counter

from game_packs import new_pack, validate_pack, propose_for_observation
from game_session import read_session_snapshot
from game_data import decode, read_bytes


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def evaluate(
    pack_path: str | Path,
    session_path: str | Path,
    expected_path: str | Path | None = None,
) -> dict:
    pack_path, session_path = Path(pack_path), Path(session_path)
    pack_file = pack_path / "pack.json" if pack_path.is_dir() else pack_path
    pack_raw = read_bytes(pack_file)
    pack = validate_pack(decode(pack_raw))
    manifest, events, hashes = read_session_snapshot(session_path)
    event_counts: Counter = Counter()
    proposal_counts: Counter = Counter()
    unmatched_states: Counter = Counter()
    unmatched = 0
    total = 0
    actual = []
    for event in events:
        event_counts[event.kind] += 1
        if event.kind != "observation":
            continue
        total += 1
        matches = propose_for_observation(pack, event.payload)
        if not matches:
            unmatched += 1
            state = event.payload.get("state")
            label = (
                state
                if isinstance(state, str) and len(state) <= 120
                else "(missing or invalid)"
            )
            if label in unmatched_states or len(unmatched_states) < 20:
                unmatched_states[label] += 1
            else:
                unmatched_states["(other)"] += 1
        for match in matches:
            if len(actual) >= 50000:
                raise ValueError("Replay exceeds the 50000-proposal limit")
            proposal_counts[match["skill_id"]] += 1
            actual.append({"event_id": event.event_id, **match})
    report = {
        "report_schema": 1,
        "mode": "offline_replay_read_only",
        "pack": {
            "id": pack["pack_id"],
            "version": pack["version"],
            "sha256": hashlib.sha256(pack_raw).hexdigest(),
        },
        "session": {"id": manifest["session_id"], **hashes},
        "events": dict(sorted(event_counts.items())),
        "observations": total,
        "unmatched_observations": unmatched,
        "unmatched_states": dict(sorted(unmatched_states.items())),
        "proposals_by_skill": dict(sorted(proposal_counts.items())),
        "proposal_examples": actual[:50],
        "proposal_examples_truncated": len(actual) > 50,
        "dispatches": 0,
        "verified_outcomes": 0,
        "qualification": "draft_replay_evidence_only",
    }
    if expected_path is not None:
        raw = read_bytes(expected_path)
        expected = decode(raw)
        if not isinstance(expected, list):
            raise ValueError("Expected proposals must be a JSON array")
        for item in expected:
            item_fields = {"event_id", "skill_id", "proposal"}
            if (
                isinstance(item, dict)
                and isinstance(item.get("proposal"), dict)
                and item["proposal"].get("type") == "action"
            ):
                item_fields.add("resolved_action")
            if (
                not isinstance(item, dict)
                or set(item) != item_fields
                or not isinstance(item["event_id"], str)
                or not item["event_id"].strip()
            ):
                raise ValueError("Malformed expected proposal")
            check = new_pack("expectation", "Expected proposal", "fixture")
            if "resolved_action" in item:
                check["schema_version"] = 2
                check["actions"] = data_actions = pack.get("actions", [])
                if item["resolved_action"] not in data_actions:
                    raise ValueError("Expected action does not match the pack registry")
            check["skills"] = [
                {
                    "id": item["skill_id"],
                    "when": {"state": "fixture"},
                    "propose": item["proposal"],
                }
            ]
            validate_pack(check)
        report["expectations"] = {
            "sha256": hashlib.sha256(raw).hexdigest(),
            "passed": total > 0 and actual == expected,
            "expected": expected,
            "actual": actual,
        }
    return report


def write_report(path: str | Path, report: dict) -> None:
    destination = Path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(prefix=".replay-", dir=destination.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as stream:
            json.dump(report, stream, indent=2, sort_keys=True, allow_nan=False)
            stream.write("\n")
        os.replace(temporary, destination)
    except Exception:
        try:
            os.unlink(temporary)
        except OSError:
            pass
        raise


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("pack")
    parser.add_argument("session")
    parser.add_argument("--report", required=True)
    parser.add_argument(
        "--expected", help="JSON array of expected proposals in event order"
    )
    args = parser.parse_args()
    try:
        # Reports must not overwrite any of the evidence inputs.
        output = Path(args.report).resolve()
        pack_file = (
            Path(args.pack) / "pack.json"
            if Path(args.pack).is_dir()
            else Path(args.pack)
        )
        manifest, _, _ = read_session_snapshot(args.session)
        inputs = [
            pack_file,
            Path(args.session),
            Path(args.session).parent / manifest["events_file"],
        ]
        if args.expected:
            inputs.append(Path(args.expected))
        if output in {path.resolve() for path in inputs}:
            raise ValueError("Report destination must differ from input files")
        report = evaluate(args.pack, args.session, args.expected)
        write_report(args.report, report)
    except (ValueError, OSError) as exc:
        parser.error(str(exc))
    print(f"Offline replay evidence saved: {args.report}")
    print(
        f"Pack {report['pack']['id']}@{report['pack']['version']}; observations={report['observations']}; proposals={sum(report['proposals_by_skill'].values())}; unmatched={report['unmatched_observations']}"
    )
    print("dispatches=0 verified_outcomes=0 qualification=draft_replay_evidence_only")
    return 0 if report.get("expectations", {}).get("passed", True) else 1


if __name__ == "__main__":
    raise SystemExit(main())
