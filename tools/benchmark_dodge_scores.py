"""Summarize a predeclared set of complete private Dodge games.

A stopped or missing game is never silently omitted from a reported mean.
"""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
import statistics


def summarize(manifest, reports):
    if not isinstance(manifest, dict) or manifest.get("schema_version") != 1:
        raise ValueError("Invalid benchmark manifest")
    seeds = manifest.get("seeds")
    speed = manifest.get("speed")
    if (
        not isinstance(seeds, list)
        or not 1 <= len(seeds) <= 100
        or any(type(seed) is not int or seed < 1 for seed in seeds)
        or len(set(seeds)) != len(seeds)
        or manifest.get("mode") != "stepped"
        or type(speed) not in (int, float)
        or speed not in (0.25, 0.5, 1)
        or type(manifest.get("target_mean_score")) not in (int, float)
        or not math.isfinite(manifest["target_mean_score"])
        or manifest["target_mean_score"] <= 0
    ):
        raise ValueError("Invalid benchmark conditions")
    rows = {}
    identity = None
    for supplied in reports:
        path = Path(supplied)
        if path.is_symlink() or not path.is_file() or path.stat().st_size > 20_000_000:
            raise ValueError("Invalid report path")
        report = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(report, dict):
            raise ValueError("Invalid report")
        seed = report.get("seed")
        training, pack, evaluation = (
            report.get("training"),
            report.get("pack"),
            report.get("evaluation"),
        )
        if (
            report.get("schema_version") != 1
            or report.get("mode") != "wsl-private-endurance"
            or type(seed) is not int
            or seed not in seeds
            or seed in rows
            or not isinstance(training, dict)
            or training.get("mode") != "stepped"
            or type(training.get("speed")) not in (int, float)
            or training.get("speed") != speed
            or training.get("fixed_fps") != 30
            or training.get("clock") != "simulated"
            or (
                training.get("action_limit"),
                training.get("request_limit"),
                training.get("time_limit_seconds", 690),
            )
            not in ((1200, 4800, 690), (2400, 9600, 1200))
            or training.get("benchmark") is not True
            or not isinstance(pack, dict)
            or not isinstance(evaluation, dict)
            or evaluation.get("source") != "private-game-evaluator-not-policy"
        ):
            raise ValueError("Report does not match the benchmark conditions")
        cadence = training.get("observation_cadence", "advance-before-decision")
        if cadence not in ("advance-before-decision", "reuse-action-frame"):
            raise ValueError("Invalid observation cadence")
        current = (
            report.get("policy"),
            pack.get("Id"),
            pack.get("Version"),
            pack.get("Sha256"),
            cadence,
            training["action_limit"],
            training["request_limit"],
            training.get("time_limit_seconds", 690),
        )
        if (
            not all(isinstance(item, str) and item for item in current[:5])
            or len(current[3]) != 64
            or any(c not in "0123456789abcdefABCDEF" for c in current[3])
        ):
            raise ValueError("Invalid policy or pack identity")
        if identity is None:
            identity = current
        elif identity != current:
            raise ValueError("Reports use different policy or pack versions")
        score, alive, stop = (
            evaluation.get("score"),
            evaluation.get("alive"),
            report.get("stop_reason"),
        )
        if score is not None and (type(score) is not int or score < 0 or score > 1000):
            raise ValueError("Invalid score")
        if alive is not None and type(alive) is not bool:
            raise ValueError("Invalid alive state")
        if not isinstance(stop, str):
            raise ValueError("Invalid stop reason")
        completed = (
            stop == "game-over"
            and alive is False
            and score is not None
            and report.get("survival_observation") == "game-over-observed"
        )
        evidence = report.get("last_unrecognized_frame")
        if completed and evidence is None:
            actions = report.get("actions")
            evidence = (
                actions[-1].get("after_file")
                if isinstance(actions, list)
                and actions
                and isinstance(actions[-1], dict)
                else None
            )
        if completed:
            if (
                not isinstance(evidence, str)
                or Path(evidence).name != evidence
                or any(c in evidence for c in "/\\:")
            ):
                raise ValueError("Missing game-over evidence")
            frame = path.parent / evidence
            if (
                frame.is_symlink()
                or not frame.is_file()
                or frame.stat().st_size != 54 + 480 * 720 * 4
            ):
                raise ValueError("Invalid game-over evidence")
            with frame.open("rb") as image:
                if image.read(2) != b"BM":
                    raise ValueError("Invalid game-over evidence")
        rows[seed] = {
            "seed": seed,
            "score": score,
            "status": "complete" if completed else "incomplete",
            "stop_reason": stop,
            "report": str(path),
            "game_over_frame": evidence if completed else None,
        }
    ordered = [
        rows.get(
            seed,
            {
                "seed": seed,
                "score": None,
                "status": "missing",
                "stop_reason": None,
                "report": None,
                "game_over_frame": None,
            },
        )
        for seed in seeds
    ]
    complete = all(row["status"] == "complete" for row in ordered)
    mean = sum(row["score"] for row in ordered) / len(ordered) if complete else None
    return {
        "schema_version": 1,
        "benchmark_id": manifest.get("id"),
        "purpose": manifest.get("purpose"),
        "mode": "stepped",
        "speed": speed,
        "target_mean_score": manifest["target_mean_score"],
        "policy": identity[0] if identity else None,
        "observation_cadence": identity[4] if identity else None,
        "run_limits": (
            {"actions": identity[5], "requests": identity[6], "seconds": identity[7]}
            if identity
            else None
        ),
        "pack": (
            {"Id": identity[1], "Version": identity[2], "Sha256": identity[3]}
            if identity
            else None
        ),
        "completed_games": sum(row["status"] == "complete" for row in ordered),
        "expected_games": len(ordered),
        "mean_score": mean,
        "median_score": (
            statistics.median(row["score"] for row in ordered) if complete else None
        ),
        "minimum_score": min(row["score"] for row in ordered) if complete else None,
        "games_at_or_above_target": (
            sum(row["score"] >= manifest["target_mean_score"] for row in ordered)
            if complete
            else None
        ),
        "target_met": (
            mean >= manifest["target_mean_score"] if mean is not None else None
        ),
        "games": ordered,
        "note": "Mean is available only when every predeclared game reaches visually recognized game over with an independent score. Missing, interrupted and stopped-at-goal games remain incomplete. Target applies only to this declared set, not repeatability on unseen games. Stepped results are not real-time performance.",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("reports", nargs="*", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    result = summarize(
        json.loads(args.manifest.read_text(encoding="utf-8")), args.reports
    )
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(args.output)


if __name__ == "__main__":
    main()
