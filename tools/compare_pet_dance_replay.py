"""Compare Pet Dance visual proposals with a source-bound human input recording.

Usage: python tools/compare_pet_dance_replay.py input-events.json replay.json
       [--output new-comparison.json]

This is an offline comparison, never an input sender. Identical sequences do not
establish autonomous success or precise input/video alignment. A failed human
round is retained as failed, never promoted to correct cue-sequence training data.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path

DIRECTIONS = {"Up", "Down", "Left", "Right"}
ACTIONS = {"dance-" + name.lower(): name for name in DIRECTIONS}


def _number(value, name):
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"Invalid {name}")
    if not math.isfinite(value) or value < 0:
        raise ValueError(f"Invalid {name}")
    return float(value)


def _json(path):
    if path.stat().st_size > 32 * 1024 * 1024:
        raise ValueError("Comparison input exceeds 32 MiB")
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(data, dict):
        raise ValueError("Expected an object")
    return data


def _sha(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def _digest(value):
    if not isinstance(value, str) or len(value) != 64:
        raise ValueError("Invalid video SHA-256")
    if any(c not in "0123456789abcdefABCDEF" for c in value):
        raise ValueError("Invalid video SHA-256")
    return value.upper()


def _timed(rows, field, duration, label):
    if not isinstance(rows, list) or len(rows) > 150_000:
        raise ValueError(f"Invalid {label} list")
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError(f"Invalid {label} entry")
        if _number(row.get(field), label + " time") > duration:
            raise ValueError(f"{label} time lies outside input coverage")
    return sorted(rows, key=lambda row: row[field])


def _observe_inputs(events, duration):
    held, presses, focus_ranges, uncertain = {}, [], [], []
    focused, focus_start = False, None
    for event in events:
        time, kind = event["TimeMs"], event.get("Type")
        if kind == "focus_gained":
            if not focused:
                focus_start, focused = time, True
        elif kind == "focus_lost":
            if focused:
                focus_ranges.append((focus_start, time))
            focused = False
            for press in held.values():
                press["uncertain_end_ms"] = time
            held.clear()
        elif kind in ("key_down", "key_up") and event.get("Control") in DIRECTIONS:
            control = event["Control"]
            injected = event.get("Injected", False)
            if not isinstance(injected, bool):
                raise ValueError("Invalid injected-input flag")
            if not focused:
                uncertain.append((time, "directional_event_outside_focus"))
                continue
            if injected:
                uncertain.append((time, "injected_directional_input"))
            if kind == "key_down":
                if control in held:
                    held[control]["repeat_events"] += 1
                else:
                    press = {
                        "control": control,
                        "time_ms": time,
                        "injected": injected,
                        "repeat_events": 0,
                        "release_ms": None,
                    }
                    held[control] = press
                    presses.append(press)
            elif control in held:
                held.pop(control)["release_ms"] = time
            else:
                uncertain.append((time, "release_without_observed_press"))
    if focused:
        focus_ranges.append((focus_start, duration))
    return presses, focus_ranges, uncertain


def compare(input_path, replay_path):
    """Return sequence comparisons; raise ValueError for invalid source evidence.

    Missing/ambiguous outcomes and interrupted inputs produce explicitly
    incomplete or unscorable rows instead of assumed successes or mismatches.
    """
    input_path, replay_path = Path(input_path), Path(replay_path)
    inputs, replay = _json(input_path), _json(replay_path)
    if (
        inputs.get("Version") != 1
        or inputs.get("Kind") != "observed-game-input"
        or inputs.get("VideoVerified") is not True
        or inputs.get("Truncated") is not False
    ):
        raise ValueError("A complete, verified, untruncated input log is required")
    if inputs.get("VideoFile") != "capture.mp4":
        raise ValueError("Unexpected video filename")
    digest = _digest(inputs.get("VideoSha256"))
    if digest != _sha(input_path.parent / "capture.mp4"):
        raise ValueError("Video does not match the input log")
    if (
        replay.get("schema_version") != 1
        or _digest(replay.get("source_video_sha256")) != digest
    ):
        raise ValueError("Replay must reference the same source video")
    duration = _number(inputs.get("CaptureEndMs"), "capture end") - _number(
        inputs.get("VideoCaptureOriginMs"), "capture origin"
    )
    if duration <= 0:
        raise ValueError("Input coverage must be positive")
    events = _timed(inputs.get("Events"), "TimeMs", duration, "input")
    proposals = _timed(replay.get("proposals"), "time_ms", duration, "proposal")
    outcomes = _timed(replay.get("outcomes"), "time_ms", duration, "outcome")
    if len({p["time_ms"] for p in proposals}) != len(proposals):
        raise ValueError("Duplicate proposal times are ambiguous")
    for proposal in proposals:
        actions = proposal.get("actions")
        if (
            not isinstance(actions, list)
            or not 3 <= len(actions) <= 7
            or any(
                not isinstance(action, str) or action not in ACTIONS
                for action in actions
            )
            or proposal.get("dispatched") is not False
        ):
            raise ValueError("Expected an undispatched 3–7 direction proposal")
    if any(
        outcome.get("outcome") not in ("Success", "Failure") for outcome in outcomes
    ):
        raise ValueError("Expected independently observed Success or Failure")
    presses, focus_ranges, uncertain = _observe_inputs(events, duration)
    allowlist = inputs.get("KeyAllowlist")
    if not isinstance(allowlist, list) or any(
        not isinstance(key, str) for key in allowlist
    ):
        raise ValueError("Invalid input key allowlist")
    rows, used_outcomes = [], set()
    for index, proposal in enumerate(proposals):
        start = proposal["time_ms"]
        next_start = (
            proposals[index + 1]["time_ms"] if index + 1 < len(proposals) else duration
        )
        candidates = [
            (i, o) for i, o in enumerate(outcomes) if start < o["time_ms"] < next_start
        ]
        row = {
            "round": index + 1,
            "go_ms": start,
            "visual_cue_sequence": [ACTIONS[action] for action in proposal["actions"]],
            "human_input_sequence": [],
            "observed_result": None,
            "actual_failed": None,
            "sequence_agrees": None,
            "status": "incomplete",
            "reasons": [],
        }
        rows.append(row)
        if len(candidates) != 1:
            row["status"] = "incomplete" if not candidates else "unscorable"
            row["reasons"] = [
                "missing_result" if not candidates else "multiple_results"
            ]
            continue
        outcome_index, outcome = candidates[0]
        used_outcomes.add(outcome_index)
        end = outcome["time_ms"]
        row.update(
            result_ms=end,
            observed_result=outcome["outcome"],
            actual_failed=outcome["outcome"] == "Failure",
        )
        selected = [
            p for p in presses if start <= p["time_ms"] <= end and not p["injected"]
        ]
        row["human_input_sequence"] = [p["control"] for p in selected]
        row["observed_presses"] = selected
        if not any(left <= start and end < right for left, right in focus_ranges):
            row["reasons"].append("round_not_continuously_focused")
        if not DIRECTIONS.issubset(allowlist):
            row["reasons"].append("directional_keys_not_all_recorded")
        if any(p["release_ms"] is None for p in selected):
            row["reasons"].append("missing_observed_release")
        if any(
            p["time_ms"] < start
            and (p["release_ms"] is None or p["release_ms"] > start)
            and p.get("uncertain_end_ms", duration) > start
            for p in presses
        ):
            row["reasons"].append("directional_key_already_held_at_go")
        row["reasons"].extend(
            reason for time, reason in uncertain if start <= time <= end
        )
        row["reasons"] = sorted(set(row["reasons"]))
        if row["reasons"]:
            row["status"] = "unscorable"
        else:
            row["status"] = "compared"
            row["sequence_agrees"] = (
                row["visual_cue_sequence"] == row["human_input_sequence"]
            )
    compared = [row for row in rows if row["status"] == "compared"]
    return {
        "schema_version": 1,
        "source_video_sha256": digest,
        "input_log_sha256": _sha(input_path),
        "replay_sha256": _sha(replay_path),
        "rounds": rows,
        "summary": {
            "proposed_rounds": len(rows),
            "compared_rounds": len(compared),
            "matching_sequences": sum(row["sequence_agrees"] for row in compared),
            "different_sequences": sum(not row["sequence_agrees"] for row in compared),
            "incomplete_rounds": sum(row["status"] == "incomplete" for row in rows),
            "unscorable_rounds": sum(row["status"] == "unscorable" for row in rows),
        },
        "unassociated_outcomes": [
            o for i, o in enumerate(outcomes) if i not in used_outcomes
        ],
        "exact_input_video_alignment_verified": False,
        "autonomous_success_claimed": False,
        "inputs_sent": 0,
        "scope": "Source-bound provisional sequence comparison using decoded Go/result timestamps and observed human key presses. Exact decoded-video/input alignment remains unverified. Agreement is not autonomous success; failed human inputs are not correct-cue labels.",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input_log", type=Path)
    parser.add_argument("replay", type=Path)
    parser.add_argument(
        "--output",
        type=Path,
        help="New output file; existing evidence is never overwritten",
    )
    args = parser.parse_args()
    report = compare(args.input_log, args.replay)
    if args.output:
        with args.output.open("x", encoding="utf-8") as destination:
            json.dump(report, destination, indent=2)
            destination.write("\n")
        print(json.dumps(report["summary"], indent=2))
    else:
        print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
