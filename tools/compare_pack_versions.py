"""Compare two draft packs on the same recorded observations, without input."""

from __future__ import annotations

import hashlib
import math
import re
from collections import Counter
from pathlib import Path

from game_data import decode, read_bytes
from game_packs import validate_pack, propose_for_observation
from game_session import read_session_snapshot


def compare(first_path, second_path, session_path):
    def read_pack(path):
        path = Path(path)
        if path.is_dir():
            path = path / "pack.json"
        raw = read_bytes(path)
        return validate_pack(decode(raw)), hashlib.sha256(raw).hexdigest()

    first, first_hash = read_pack(first_path)
    second, second_hash = read_pack(second_path)
    if first["game_id"] != second["game_id"] or first["pack_id"] != second["pack_id"]:
        raise ValueError("Compare versions of the same pack and game")
    manifest, events, hashes = read_session_snapshot(session_path)
    changes = []
    counts = Counter()
    for event in events:
        if event.kind != "observation":
            continue
        left = propose_for_observation(first, event.payload)
        right = propose_for_observation(second, event.payload)
        counts["observations"] += 1
        if left == right:
            counts["unchanged"] += 1
            continue
        counts["different"] += 1
        if len(changes) < 50:
            changes.append(
                {
                    "event_id": event.event_id,
                    "time_ms": event.time_ms,
                    "first": left,
                    "second": right,
                }
            )
    return {
        "report_schema": 1,
        "mode": "offline_pack_comparison",
        "first": {
            "id": first["pack_id"],
            "version": first["version"],
            "sha256": first_hash,
        },
        "second": {
            "id": second["pack_id"],
            "version": second["version"],
            "sha256": second_hash,
        },
        "session": {"id": manifest["session_id"], **hashes},
        "observations": counts["observations"],
        "unchanged": counts["unchanged"],
        "different": counts["different"],
        "examples": changes,
        "examples_truncated": counts["different"] > len(changes),
        "dispatches": 0,
        "verified_outcomes": 0,
        "note": "Different proposals do not establish that either pack plays better.",
    }


def compare_runs(first_path, second_path):
    """Summarize two bounded test reports; neither scores game success."""

    def load(path):
        path = Path(path)
        raw = read_bytes(path)
        data = decode(raw)
        if (
            not isinstance(data, dict)
            or data.get("schema_version") != 1
            or data.get("mode") not in ("bounded_dodge_test", "godot-native-test")
        ):
            raise ValueError("Choose a Dodge test report")
        pack, actions = data.get("pack"), data.get("actions")
        if not isinstance(pack, dict) or not all(
            isinstance(pack.get(field), str) and pack[field]
            for field in ("Id", "Version", "Sha256")
        ):
            raise ValueError("Test report lacks pack identity")
        if (
            not isinstance(actions, list)
            or len(actions) > 10
            or type(data.get("action_count")) is not int
            or data["action_count"] != len(actions)
        ):
            raise ValueError("Invalid test action count")
        counts = Counter()
        for action in actions:
            if not isinstance(action, dict) or action.get("outcome") not in (
                "unknown",
                "movement-observed",
                "movement-not-observed",
            ):
                raise ValueError("Invalid test outcome")
            outcome = action["outcome"]
            if data["mode"] == "godot-native-test":
                check_native_action(path, action)
                counts[outcome] += 1
                continue
            if outcome != "unknown":
                times = [
                    action.get(name)
                    for name in (
                        "source_capture_ms",
                        "down_ms",
                        "up_ms",
                        "after_capture_ms",
                    )
                ]
                if (
                    action.get("receipt") != "dispatched"
                    or any(
                        isinstance(t, bool)
                        or not isinstance(t, (int, float))
                        or not math.isfinite(t)
                        for t in times
                    )
                    or times != sorted(times)
                ):
                    raise ValueError("Movement outcome has invalid timing or receipt")
                for prefix in ("before", "after"):
                    filename, expected = action.get(prefix + "_file"), action.get(
                        prefix + "_sha256"
                    )
                    if (
                        not isinstance(filename, str)
                        or Path(filename).name != filename
                        or any(c in filename for c in "/\\:")
                        or not isinstance(expected, str)
                    ):
                        raise ValueError("Movement outcome has invalid frame evidence")
                    frame = path.parent / filename
                    if frame.is_symlink() or not frame.resolve().is_relative_to(
                        path.parent.resolve()
                    ):
                        raise ValueError("Frame evidence escapes the run folder")
                    if (
                        hashlib.sha256(read_bytes(frame, 16 * 1024 * 1024))
                        .hexdigest()
                        .upper()
                        != expected.upper()
                    ):
                        raise ValueError("Frame evidence hash does not match")
            counts[outcome] += 1
        return data, hashlib.sha256(raw).hexdigest(), counts

    first, first_hash, first_counts = load(first_path)
    second, second_hash, second_counts = load(second_path)
    if first["pack"]["Id"] != second["pack"]["Id"]:
        raise ValueError("Choose test runs of the same pack")
    if first["mode"] != second["mode"]:
        raise ValueError("Compare runs that use the same observation and input mode")

    def summary(data, digest, counts):
        return {
            "run_id": data.get("run_id"),
            "report_sha256": digest,
            "mode": data["mode"],
            "pack": data["pack"],
            "stop_reason": data.get("stop_reason"),
            "action_count": data["action_count"],
            "outcomes": dict(sorted(counts.items())),
        }

    return {
        "report_schema": 1,
        "mode": "dodge_run_comparison",
        "first": summary(first, first_hash, first_counts),
        "second": summary(second, second_hash, second_counts),
        "note": "Movement observations do not prove survival, causation, or that one pack plays better. Runs may differ in game state and random events.",
    }


def check_native_action(report_path, action):
    """Check a native outcome against the saved, hashed engine response."""
    if action.get("input_status") == "issued" and action.get("outcome") == "unknown":
        return
    if action.get("input_status") != "acknowledged":
        raise ValueError("Invalid native action status")
    relative, digest = action.get("evidence"), action.get("evidence_sha256")
    if (
        not isinstance(relative, str)
        or not re.fullmatch(r"channel/response-[0-9]{4}\.json", relative)
        or not isinstance(digest, str)
    ):
        raise ValueError("Invalid native action evidence")
    evidence = report_path.parent / relative
    if (
        evidence.is_symlink()
        or evidence.parent.is_symlink()
        or not evidence.resolve().is_relative_to(report_path.parent.resolve())
    ):
        raise ValueError("Native evidence escapes the run folder")
    raw = read_bytes(evidence, 64 * 1024)
    if hashlib.sha256(raw).hexdigest().upper() != digest.upper():
        raise ValueError("Native evidence hash does not match")
    response = decode(raw)
    if (
        not isinstance(response, dict)
        or response.get("id") != action.get("request_id")
        or response.get("kind") != "action-result"
        or response.get("action") != action.get("action")
        or type(response.get("visible")) is not bool
    ):
        raise ValueError("Native action response does not match the report")
    before, after = response.get("before"), response.get("player")
    if any(
        not isinstance(point, list)
        or len(point) != 2
        or any(
            isinstance(n, bool)
            or not isinstance(n, (int, float))
            or not math.isfinite(n)
            for n in point
        )
        for point in (before, after)
    ):
        raise ValueError("Invalid native movement positions")
    deltas = {
        "move-left": before[0] - after[0],
        "move-right": after[0] - before[0],
        "move-up": before[1] - after[1],
        "move-down": after[1] - before[1],
    }
    if action.get("action") not in deltas:
        raise ValueError("Invalid native action")
    expected = (
        "unknown"
        if not response["visible"]
        else (
            "movement-observed"
            if deltas[action["action"]] >= 4
            else "movement-not-observed"
        )
    )
    if (
        action.get("outcome") != expected
        or action.get("visible") != response["visible"]
        or action.get("score") != response.get("score")
    ):
        raise ValueError("Native movement result does not match engine evidence")
