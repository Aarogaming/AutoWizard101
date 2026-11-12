"""Run bounded offline Dodge learning experiments from completed local games.

This does not start a game, control the desktop, install models, or promote a
policy. The underlying trainers validate the recordings and split whole seeds.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import subprocess
import sys
import uuid

TIMEOUT_SECONDS = 90
MAX_OTHER_BENCHMARKS = 7


def run_study(
    workspace,
    benchmark,
    test_seeds,
    others=(),
    include_holds=False,
    contextual_outcomes=False,
    exploration_only=False,
):
    workspace = Path(workspace).resolve(strict=True)
    benchmark = Path(benchmark).resolve(strict=True)
    others = [Path(path).resolve(strict=True) for path in others]
    if len(others) > MAX_OTHER_BENCHMARKS:
        raise ValueError("Choose at most seven additional benchmarks")
    if not test_seeds or len(set(test_seeds)) != len(test_seeds):
        raise ValueError("Choose distinct held-out seeds")

    tools = Path(__file__).resolve().parent
    common = [str(workspace), str(benchmark), "--test-seeds", *map(str, test_seeds)]
    imitation = [sys.executable, str(tools / "train_dodge_imitation.py"), *common]
    survival = [sys.executable, str(tools / "train_dodge_survival.py"), *common]
    if include_holds:
        survival.append("--include-holds")
    if contextual_outcomes:
        survival.append("--contextual")
    if exploration_only:
        survival.append("--exploration-only")
    for other in others:
        survival.extend(["--also-benchmark", str(other)])
    jobs = [("imitation", imitation)]
    if include_holds:
        jobs.append(("imitation_with_holds", [*imitation, "--include-holds"]))
    jobs.extend(
        [("survival", survival), ("survival_balanced", [*survival, "--balance-games"])]
    )

    output = workspace / ".local/dodge-learning-studies" / uuid.uuid4().hex
    output.mkdir(parents=True, exist_ok=False)
    results = []
    for name, command in jobs:
        try:
            completed = subprocess.run(
                command,
                capture_output=True,
                text=True,
                timeout=TIMEOUT_SECONDS,
                check=False,
            )
            if completed.returncode:
                results.append(
                    {
                        "name": name,
                        "status": "failed",
                        "error": completed.stderr[-2000:],
                    }
                )
                continue
            data = json.loads(completed.stdout)
            results.append({"name": name, "status": "complete", "result": data})
        except (subprocess.TimeoutExpired, json.JSONDecodeError) as error:
            results.append(
                {"name": name, "status": "failed", "error": str(error)[:2000]}
            )
    report = {
        "schema_version": 1,
        "status": (
            "complete"
            if all(x["status"] == "complete" for x in results)
            else "incomplete"
        ),
        "test_seeds": list(test_seeds),
        "benchmark": str(benchmark),
        "additional_benchmarks": [str(path) for path in others],
        "contextual_outcomes": contextual_outcomes,
        "exploration_only": exploration_only,
        "experiments": results,
        "limitations": "Offline research only. Imitation agreement and observational survival predictions do not establish gameplay performance or causal action values. No policy is selected, installed, or promoted.",
    }
    (output / "report.json").write_text(
        json.dumps(report, indent=2) + "\n", encoding="utf-8"
    )
    return output, report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workspace", type=Path)
    parser.add_argument("benchmark", type=Path)
    parser.add_argument("--test-seeds", nargs="+", type=int, required=True)
    parser.add_argument("--also-benchmark", type=Path, action="append", default=[])
    parser.add_argument("--include-holds", action="store_true")
    parser.add_argument("--contextual-outcomes", action="store_true")
    parser.add_argument("--exploration-only", action="store_true")
    args = parser.parse_args()
    output, report = run_study(
        args.workspace,
        args.benchmark,
        args.test_seeds,
        args.also_benchmark,
        args.include_holds,
        args.contextual_outcomes,
        args.exploration_only,
    )
    print(
        json.dumps(
            {
                "output": str(output),
                "status": report["status"],
                "experiments": [
                    {"name": x["name"], "status": x["status"]}
                    for x in report["experiments"]
                ],
            },
            indent=2,
        )
    )
    if report["status"] != "complete":
        raise SystemExit(1)


if __name__ == "__main__":
    main()
