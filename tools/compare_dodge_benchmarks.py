"""Compare complete, paired Dodge benchmark suites without dropping games."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from benchmark_dodge_scores import summarize


def compare(baseline_directory, candidate_directory):
    directories = [Path(baseline_directory), Path(candidate_directory)]
    manifests = [
        json.loads((directory / "manifest.json").read_text(encoding="utf-8"))
        for directory in directories
    ]
    if manifests[0] != manifests[1]:
        raise ValueError("Benchmark manifests differ")
    summaries = []
    for directory in directories:
        existing = json.loads((directory / "summary.json").read_text(encoding="utf-8"))
        paths = [
            row["report"] for row in existing["games"] if row.get("report") is not None
        ]
        current = summarize(manifests[0], paths)
        if current["mean_score"] is None:
            raise ValueError("Cannot compare an incomplete benchmark")
        summaries.append(current)
    baseline, candidate = summaries
    if baseline["observation_cadence"] != candidate["observation_cadence"]:
        raise ValueError(
            "Observation cadences differ; compare them as separate experiments"
        )
    if baseline["run_limits"] != candidate["run_limits"]:
        raise ValueError("Run limits differ; compare them as separate experiments")
    differences = [
        {
            "seed": before["seed"],
            "baseline_score": before["score"],
            "candidate_score": after["score"],
            "difference": after["score"] - before["score"],
        }
        for before, after in zip(baseline["games"], candidate["games"])
    ]
    return {
        "schema_version": 1,
        "benchmark_id": baseline["benchmark_id"],
        "mode": baseline["mode"],
        "speed": baseline["speed"],
        "observation_cadence": baseline["observation_cadence"],
        "baseline": {
            k: baseline[k]
            for k in (
                "policy",
                "pack",
                "mean_score",
                "median_score",
                "minimum_score",
                "games_at_or_above_target",
                "target_met",
            )
        },
        "candidate": {
            k: candidate[k]
            for k in (
                "policy",
                "pack",
                "mean_score",
                "median_score",
                "minimum_score",
                "games_at_or_above_target",
                "target_met",
            )
        },
        "mean_difference": candidate["mean_score"] - baseline["mean_score"],
        "games": differences,
        "note": "Paired development results use the same declared seeds and timing mode. Policy choices can change subsequent game events, and changed code or game files can confound a comparison. A development mean does not establish validation or real-time performance.",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("baseline", type=Path)
    parser.add_argument("candidate", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = compare(args.baseline, args.candidate)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(args.output)


if __name__ == "__main__":
    main()
