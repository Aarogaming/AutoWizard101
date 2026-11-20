"""Summarize private stepped-game wall timings from completed reports.

These timings measure the isolated research bridge, not desktop capture latency
or full-speed gameplay. They exclude hold-only decisions and reobservation work.
"""

import argparse
import json
import math
from pathlib import Path
import statistics

FIELDS = ("observation_wait_wall_ms", "decision_work_wall_ms", "action_wait_wall_ms")


def summarize_reports(paths):
    paths = [Path(path) for path in paths]
    if not 1 <= len(paths) <= 100:
        raise ValueError("Choose 1–100 completed private game reports")
    values = {name: [] for name in FIELDS}
    actions = reused = 0
    for path in paths:
        if path.stat().st_size > 12_000_000:
            raise ValueError("Report exceeds the analysis size limit")
        report = json.loads(path.read_text(encoding="utf-8"))
        if (
            report.get("mode") != "wsl-private-endurance"
            or report.get("stop_reason") != "game-over"
        ):
            raise ValueError("Only complete private endurance games can be analyzed")
        rows = report.get("actions")
        if not isinstance(rows, list) or len(rows) > 2400:
            raise ValueError("Invalid bounded action log")
        for row in rows:
            actions += 1
            flag = row.get("observation_reused")
            if flag is not None and type(flag) is not bool:
                raise ValueError("Invalid observation reuse flag")
            reused += flag is True
            for name in FIELDS:
                value = row.get(name)
                if value is None:
                    continue
                if (
                    type(value) not in (int, float)
                    or not math.isfinite(value)
                    or value < 0
                ):
                    raise ValueError("Invalid wall-clock timing")
                values[name].append(value)
    summaries = {}
    for name, samples in values.items():
        ordered = sorted(samples)
        summaries[name] = {
            "samples": len(ordered),
            "median_ms": statistics.median(ordered) if ordered else None,
            "p95_ms": ordered[math.ceil(0.95 * len(ordered)) - 1] if ordered else None,
            "maximum_ms": ordered[-1] if ordered else None,
        }
    return {
        "reports": len(paths),
        "action_records": actions,
        "reused_observations": reused,
        "timings": summaries,
        "note": "Private stepped bridge only; missing fields are not zero. Decision work includes visual analysis and record preparation; action wait excludes later reobservations. Not real-time gameplay latency.",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reports", type=Path, nargs="+")
    args = parser.parse_args()
    print(json.dumps(summarize_reports(args.reports), indent=2))


if __name__ == "__main__":
    main()
