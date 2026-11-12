"""Compare bounded private-display Dodge reports without claiming causality."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
from game_data import decode


def compare(paths):
    rows = {}
    pack_id = None
    versions = {}
    training = None
    for supplied in paths:
        path = Path(supplied)
        if path.is_symlink() or path.stat().st_size > 2 * 1024 * 1024:
            raise ValueError("Invalid report file")
        raw = path.read_bytes()
        report = decode(raw)
        policy, seed, pack = (
            report.get("policy"),
            report.get("seed"),
            report.get("pack"),
        )
        if (
            report.get("mode") != "wsl-private-visual"
            or report.get("schema_version") != 1
            or policy
            not in (
                "motion-v1",
                "resilient-v2",
                "guarded-v3",
                "guarded-v4",
                "geometry-v5",
                "edge-aware-v6",
                "safe-edge-v7",
                "diagonal-v8",
                "diagonal-v9",
                "lookahead-v10",
                "center-guard-v11",
                "center-first-v12",
                "adaptive-center-v13",
                "trajectory-v14",
                "center-track-v15",
                "center-continuous-v16",
                "center-continuous-track-v17",
                "center-consensus-v18",
                "center-anticipate-v19",
                "center-configurable-v20",
                "adaptive-horizon-v21",
                "rollout-v22",
                "velocity-guard-v23",
                "edge-fallback-v24",
                "guarded-edge-v25",
                "directional-v26",
                "directional-guard-v27",
                "directional-carry-v28",
                "directional-smooth-v29",
                "directional-configurable-v30",
                "directional-two-phase-v31",
                "directional-pulse-v32",
                "directional-learned-v33",
                "directional-outcome-v34",
                "directional-explore-v35",
                "directional-outcome-turn-v36",
                "directional-outcome-reference-v37",
                "directional-outcome-escape-v38",
            )
            or type(seed) is not int
            or seed < 1
            or not isinstance(pack, dict)
        ):
            raise ValueError("Not a comparable private Dodge report")
        if not all(
            isinstance(pack.get(k), str) and pack[k]
            for k in ("Id", "Version", "Sha256")
        ):
            raise ValueError("Invalid pack identity")
        if len(pack["Sha256"]) != 64 or any(
            c not in "0123456789abcdefABCDEF" for c in pack["Sha256"]
        ):
            raise ValueError("Invalid pack hash")
        if pack_id is None:
            pack_id = pack["Id"]
        if pack["Id"] != pack_id:
            raise ValueError("Reports are from different packs")
        run_training = report.get(
            "training",
            {"mode": "realtime", "speed": 1, "fixed_fps": None, "clock": "monotonic"},
        )
        if not isinstance(run_training, dict):
            raise ValueError("Invalid training settings")
        if (
            type(run_training.get("speed")) not in (int, float)
            or type(run_training.get("action_limit", 20)) is not int
            or type(run_training.get("request_limit", 120)) is not int
        ):
            raise ValueError("Invalid training settings")
        settings = (
            run_training.get("mode"),
            run_training.get("speed"),
            run_training.get("fixed_fps"),
            run_training.get("clock"),
            run_training.get("action_limit", 20),
            run_training.get("request_limit", 120),
        )
        if (
            settings[:4]
            not in (
                ("realtime", 1, None, "monotonic"),
                ("stepped", 0.25, 30, "simulated"),
                ("stepped", 0.5, 30, "simulated"),
                ("stepped", 1, 30, "simulated"),
            )
            or settings[4:] not in ((20, 120), (40, 180))
            or (settings[0] == "realtime" and settings[4:] != (20, 120))
        ):
            raise ValueError("Invalid training settings")
        if training is None:
            training = settings
        elif training != settings:
            raise ValueError("Reports use different training settings")
        identity = (pack["Version"], pack["Sha256"])
        if policy in versions and versions[policy] != identity:
            raise ValueError("A policy uses multiple pack versions")
        versions[policy] = identity
        actions = report.get("actions")
        if (
            not isinstance(actions, list)
            or len(actions) > training[4]
            or type(report.get("action_count")) is not int
            or len(actions) != report["action_count"]
        ):
            raise ValueError("Invalid action count")
        outcomes = {"movement-observed", "movement-not-observed", "unknown"}
        for action in actions:
            if not isinstance(action, dict) or action.get("outcome") not in outcomes:
                raise ValueError("Invalid action result")
            for field in ("before_file", "after_file"):
                name = action.get(field)
                if (
                    not isinstance(name, str)
                    or Path(name).name != name
                    or any(c in name for c in "/\\:")
                ):
                    raise ValueError("Invalid frame path")
                frame = path.parent / name
                if (
                    frame.is_symlink()
                    or not frame.is_file()
                    or not frame.resolve().is_relative_to(path.parent.resolve())
                ):
                    raise ValueError("Missing or unsafe frame evidence")
                if frame.stat().st_size != 54 + 480 * 720 * 4:
                    raise ValueError("Unexpected frame size")
                with frame.open("rb") as source:
                    if source.read(2) != b"BM":
                        raise ValueError("Unexpected frame format")
        seconds, observed, stop = (
            report.get("observed_play_seconds"),
            report.get("survival_observation"),
            report.get("stop_reason"),
        )
        if observed not in (
            "game-over-observed",
            "censored-at-stop",
            "not-observed",
        ) or not isinstance(stop, str):
            raise ValueError("Invalid survival observation")
        if observed == "not-observed":
            if seconds is not None:
                raise ValueError("Unexpected play duration")
        elif (
            type(seconds) not in (int, float)
            or not math.isfinite(seconds)
            or not 0 <= seconds <= 45
        ):
            raise ValueError("Invalid play duration")
        if (observed == "game-over-observed") != (
            stop == "game-over" and seconds is not None
        ):
            raise ValueError("Game-over observation disagrees with stop reason")
        key = (seed, policy)
        if key in rows:
            raise ValueError("Duplicate seed and policy")
        decisions = report.get("decisions")
        if decisions is not None and (
            not isinstance(decisions, list)
            or len(decisions) > training[5]
            or any(
                not isinstance(d, dict) or type(d.get("policy_override")) is not bool
                for d in decisions
            )
        ):
            raise ValueError("Invalid decision records")
        rows[key] = {
            "seconds": seconds,
            "observation": observed,
            "stop": stop,
            "actions": len(actions),
            "movements_observed": sum(
                a["outcome"] == "movement-observed" for a in actions
            ),
            "decision_count": len(decisions) if decisions is not None else None,
            "decision_overrides": (
                sum(d["policy_override"] for d in decisions)
                if decisions is not None
                else None
            ),
            "report": str(path),
            "report_sha256": hashlib.sha256(raw).hexdigest(),
        }
    seeds = sorted({seed for seed, _ in rows})
    candidates = set(versions) - {"motion-v1"}
    if (
        not seeds
        or "motion-v1" not in versions
        or len(candidates) != 1
        or any((seed, policy) not in rows for seed in seeds for policy in versions)
    ):
        raise ValueError("Each seed needs one report for each policy")
    candidate_policy = candidates.pop()
    pairs = []
    for seed in seeds:
        baseline, candidate = rows[seed, "motion-v1"], rows[seed, candidate_policy]
        complete = (
            baseline["observation"] == candidate["observation"] == "game-over-observed"
        )
        pairs.append(
            {
                "seed": seed,
                "baseline": baseline,
                "candidate": candidate,
                "observed_difference_seconds": (
                    candidate["seconds"] - baseline["seconds"] if complete else None
                ),
            }
        )
    return {
        "schema_version": 1,
        "mode": "paired-wsl-dodge-comparison",
        "pack_id": pack_id,
        "candidate_policy": candidate_policy,
        "training": dict(
            zip(
                (
                    "mode",
                    "speed",
                    "fixed_fps",
                    "clock",
                    "action_limit",
                    "request_limit",
                ),
                training,
            )
        ),
        "versions": {k: {"version": v[0], "sha256": v[1]} for k, v in versions.items()},
        "pairs": pairs,
        "note": "Durations begin at first recognized playing frame. Action limits, time limits, and recognition failures are censored. Controlled starting conditions do not force identical gameplay; policies may encounter different situations. Screenshots and reports are not authenticated. Inspect both pack versions for configuration changes; these runs do not establish causality.",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reports", nargs="+", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    result = compare(args.reports)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(args.output)


if __name__ == "__main__":
    main()
