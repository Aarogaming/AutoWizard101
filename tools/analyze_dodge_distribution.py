"""Summarize complete Dodge scores and their sampling uncertainty offline.

The bootstrap describes variation among the supplied games, assuming their
openings are independent and representative. It does not validate real-time
play or certify reliability, especially with few games.
"""

import argparse
import json
import math
from pathlib import Path
import random
import statistics

from benchmark_dodge_scores import summarize


def distribution(scores, *, resamples=5000, seed=5105):
    if (
        not isinstance(scores, list)
        or not 2 <= len(scores) <= 1000
        or any(type(score) is not int or score < 0 for score in scores)
        or type(resamples) is not int
        or not 100 <= resamples <= 10000
        or type(seed) is not int
    ):
        raise ValueError(
            "Choose 2–1000 nonnegative game scores and 100–10000 resamples"
        )
    rng = random.Random(seed)
    count = len(scores)
    samples = sorted(
        sum(scores[rng.randrange(count)] for _ in range(count)) / count
        for _ in range(resamples)
    )
    lower = samples[math.floor((resamples - 1) * 0.025)]
    upper = samples[math.ceil((resamples - 1) * 0.975)]
    return {
        "games": count,
        "scores": scores,
        "mean": statistics.mean(scores),
        "median": statistics.median(scores),
        "minimum": min(scores),
        "maximum": max(scores),
        "games_at_or_above_50": sum(score >= 50 for score in scores),
        "sample_standard_deviation": statistics.stdev(scores),
        "bootstrap_mean_interval_95": [lower, upper],
        "bootstrap_lower_bound_at_least_50": lower >= 50,
        "resamples": resamples,
        "seed": seed,
        "note": "Descriptive bootstrap assuming independent, representative games; a small or biased sample cannot establish reliability or real-time performance.",
    }


def analyze(directory):
    directory = Path(directory)
    manifest = json.loads((directory / "manifest.json").read_text(encoding="utf-8"))
    saved = json.loads((directory / "summary.json").read_text(encoding="utf-8"))
    reports = [row["report"] for row in saved["games"] if row.get("report")]
    verified = summarize(manifest, reports)
    if verified["mean_score"] is None:
        raise ValueError("The entire planned benchmark must finish before analysis")
    return distribution([row["score"] for row in verified["games"]])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("benchmark", type=Path)
    args = parser.parse_args()
    print(json.dumps(analyze(args.benchmark), indent=2))


if __name__ == "__main__":
    main()
