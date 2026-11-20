"""Check a manually supplied desktop-use trace; this never controls a device."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys


def validate_trace(trace):
    issues = []

    def problem(code, message):
        issues.append({"code": code, "message": message})

    if (
        not isinstance(trace, dict)
        or type(trace.get("schema_version")) is not int
        or trace["schema_version"] != 1
    ):
        return [
            {"code": "invalid_trace", "message": "Expected a schema 1 trace object."}
        ]
    mode, target, events = (
        trace.get("mode"),
        trace.get("target_id"),
        trace.get("events"),
    )
    if (
        mode not in ("observe", "assist", "desktop_test", "native_test")
        or not isinstance(target, str)
        or not target.strip()
        or type(trace.get("target_identity_verified")) is not bool
        or not isinstance(events, list)
        or len(events) > 10000
    ):
        return [
            {"code": "invalid_trace", "message": "Invalid mode, target, or events."}
        ]
    for field, minimum in (
        ("max_actions", 0),
        ("max_duration_ms", 1),
        ("max_action_duration_ms", 1),
        ("max_observation_age_ms", 1),
    ):
        if (
            type(trace.get(field)) is not int
            or not minimum <= trace[field] <= 3_600_000
        ):
            return [{"code": "invalid_trace", "message": f"Invalid {field}."}]
    known, observations, actions, receipts, outcomes, verifications = (
        set(),
        {},
        {},
        {},
        {},
        {},
    )
    released, held, focus, stopped, last_time, count = set(), set(), False, False, -1, 0

    def get(mapping, value):
        return mapping.get(value) if isinstance(value, str) else None

    for event in events:
        if not isinstance(event, dict):
            problem("invalid_event", "Event must be an object.")
            continue
        eid, kind, time = event.get("id"), event.get("kind"), event.get("time_ms")
        if (
            not isinstance(eid, str)
            or not eid.strip()
            or eid in known
            or type(time) is not int
            or time < 0
            or time < last_time
        ):
            problem(
                "invalid_event",
                "Event IDs must be unique and timestamps nondecreasing.",
            )
            continue
        known.add(eid)
        last_time = time
        if (
            kind in ("observation", "focus", "dispatch")
            and event.get("target_id") != target
        ):
            problem("wrong_target", f"{eid}: event targets a different window or game.")
        if kind == "observation":
            if event.get("source") not in ("visual", "engine", "unknown") or event.get(
                "state"
            ) not in ("known", "unknown"):
                problem("invalid_observation", f"{eid}: invalid observation.")
            observations[eid] = event
        elif kind == "focus":
            if type(event.get("focused")) is not bool:
                problem("invalid_focus", f"{eid}: focus must be true or false.")
                focus = False
            else:
                focus = event["focused"] and event.get("target_id") == target
        elif kind == "dispatch":
            count += 1
            action_id, mechanism = eid, event.get("mechanism")
            actions[action_id] = event
            if (
                mode in ("observe", "assist")
                or (mode == "desktop_test" and mechanism != "desktop")
                or (mode == "native_test" and mechanism != "native")
            ):
                problem(
                    "mode_violation", f"{eid}: dispatch is not allowed in this mode."
                )
            if stopped:
                problem("action_after_stop", f"{eid}: dispatch occurred after stop.")
            if not trace["target_identity_verified"]:
                problem(
                    "target_unverified", f"{eid}: target identity was not verified."
                )
            if count > trace["max_actions"] or time > trace["max_duration_ms"]:
                problem(
                    "budget_exceeded", f"{eid}: action count or run duration exceeded."
                )
            duration = event.get("duration_ms")
            if (
                type(duration) is not int
                or duration < 0
                or duration > trace["max_action_duration_ms"]
                or type(event.get("requires_release")) is not bool
            ):
                problem(
                    "invalid_action", f"{eid}: invalid action duration or release flag."
                )
            observation = get(observations, event.get("observation_id"))
            if (
                observation is None
                or observation.get("target_id") != target
                or observation.get("state") != "known"
                or observation.get("source") == "unknown"
            ):
                problem("unknown_observation", f"{eid}: no known target observation.")
            elif time - observation["time_ms"] > trace["max_observation_age_ms"]:
                problem("stale_observation", f"{eid}: observation is too old.")
            if mechanism == "desktop" and not focus:
                problem("focus_missing", f"{eid}: target focus was not established.")
            if held:
                problem(
                    "overlapping_input",
                    f"{eid}: a previous held input has not been released.",
                )
            if event.get("requires_release") is True:
                held.add(action_id)
        elif kind == "receipt":
            action_id = event.get("action_id")
            if (
                not isinstance(action_id, str)
                or action_id not in actions
                or action_id in receipts
                or event.get("status") not in ("accepted", "rejected", "unknown")
            ):
                problem(
                    "invalid_receipt", f"{eid}: invalid or duplicate action receipt."
                )
            else:
                receipts[action_id] = event
        elif kind == "release":
            action_id = event.get("action_id")
            if (
                not isinstance(action_id, str)
                or action_id not in actions
                or action_id in released
                or actions[action_id].get("requires_release") is not True
            ):
                problem("invalid_release", f"{eid}: invalid or duplicate release.")
            else:
                released.add(action_id)
                held.discard(action_id)
                if (
                    time - actions[action_id]["time_ms"]
                    > trace["max_action_duration_ms"]
                ):
                    problem(
                        "hold_too_long",
                        f"{eid}: held input exceeded the duration limit.",
                    )
        elif kind == "outcome":
            action_id = event.get("action_id")
            observation = get(observations, event.get("observation_id"))
            status = event.get("status")
            if (
                not isinstance(action_id, str)
                or action_id not in actions
                or status not in ("observed", "not_observed", "unknown")
            ):
                problem("invalid_outcome", f"{eid}: invalid action outcome.")
            elif status != "unknown" and (
                observation is None
                or observation.get("target_id") != target
                or observation.get("state") != "known"
                or observation.get("source") == "unknown"
                or observation["time_ms"] <= actions[action_id]["time_ms"]
            ):
                problem(
                    "outcome_without_evidence",
                    f"{eid}: outcome lacks a later known target observation.",
                )
            outcomes[eid] = event
        elif kind == "verification":
            observation = get(observations, event.get("observation_id"))
            if (
                event.get("scope") not in ("visual", "game_success")
                or event.get("status") not in ("passed", "failed", "unknown")
                or not isinstance(event.get("artifact"), str)
                or not event["artifact"].strip()
                or observation is None
                or observation.get("target_id") != target
                or observation.get("state") != "known"
            ):
                problem(
                    "invalid_verification",
                    f"{eid}: verification lacks an artifact or observation.",
                )
            elif event["scope"] == "visual" and observation.get("source") != "visual":
                problem(
                    "wrong_observation_source",
                    f"{eid}: engine state does not verify visual recognition.",
                )
            else:
                verifications[eid] = event
        elif kind == "claim":
            claim, evidence = event.get("claim"), event.get("evidence_id")
            if claim == "movement_observed":
                valid = (
                    isinstance(evidence, str)
                    and evidence in outcomes
                    and outcomes[evidence].get("status") == "observed"
                )
            elif claim in ("visual_verified", "game_success"):
                required = "visual" if claim == "visual_verified" else "game_success"
                valid = (
                    isinstance(evidence, str)
                    and evidence in verifications
                    and verifications[evidence].get("scope") == required
                    and verifications[evidence].get("status") == "passed"
                )
            else:
                valid = False
            if not valid:
                problem("unsupported_claim", f"{eid}: claim lacks matching evidence.")
        elif kind == "stop":
            if not isinstance(event.get("reason"), str) or not event["reason"].strip():
                problem("invalid_stop", f"{eid}: stop reason is required.")
            stopped = True
        else:
            problem("unknown_event", f"{eid}: unsupported event kind.")
    for aid, action in actions.items():
        if aid not in receipts:
            problem("missing_receipt", f"{aid}: dispatch has no receipt event.")
        if action.get("requires_release") is True and aid not in released:
            problem("missing_release", f"{aid}: held input has no release event.")
    return issues


def _unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON field")
        result[key] = value
    return result


def _reject_constant(value):
    raise ValueError("Nonfinite JSON")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("trace", type=Path)
    args = parser.parse_args()
    try:
        with args.trace.open("rb") as source:
            content = source.read(2 * 1024 * 1024 + 1)
        if len(content) > 2 * 1024 * 1024:
            raise ValueError("Trace is too large")
        trace = json.loads(
            content.decode("utf-8-sig"),
            object_pairs_hook=_unique,
            parse_constant=_reject_constant,
        )
        issues = validate_trace(trace)
    except (OSError, UnicodeError, ValueError, RecursionError) as exc:
        issues = [{"code": "invalid_trace", "message": str(exc)}]
    print(
        json.dumps(
            {"passed": not issues, "issues": issues, "verifies_truth": False}, indent=2
        )
    )
    return 1 if issues else 0


if __name__ == "__main__":
    sys.exit(main())
