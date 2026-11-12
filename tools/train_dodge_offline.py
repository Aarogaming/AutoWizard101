"""Bounded local parameter search for the isolated visual Dodge player.

This is score-based parameter search, not neural-network training. It cannot
install a pack, promote a policy, use held-out validation, or start real-time play.
"""

from __future__ import annotations

import argparse
from contextlib import contextmanager
import hashlib
import json
import math
import os
from pathlib import Path
import time
import uuid

from generate_dodge_candidates import POLICY_PREFIXES, generate
from dodge_training_profiles import PROFILES, load_profile
from dodge_selection import rank_screened
from dodge_baseline_cache import verified_baseline
from run_dodge_benchmark import GAME_TIMEOUT_SECONDS, fingerprints, run_benchmark


@contextmanager
def exclusive_study(workspace):
    """Prevent overlapping studies; the OS releases the lock after a crash."""
    path = Path(workspace) / ".local" / "dodge-training.lock"
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a+b") as lock:
        lock.seek(0, os.SEEK_END)
        if lock.tell() == 0:
            lock.write(b"0")
            lock.flush()
        lock.seek(0)
        if os.name == "nt":
            import msvcrt

            try:
                msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
            except OSError as exc:
                raise RuntimeError(
                    "Another offline Dodge study is already running"
                ) from exc
        else:
            import fcntl

            try:
                fcntl.flock(lock.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
            except OSError as exc:
                raise RuntimeError(
                    "Another offline Dodge study is already running"
                ) from exc
        try:
            yield
        finally:
            lock.seek(0)
            if os.name == "nt":
                msvcrt.locking(lock.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(lock.fileno(), fcntl.LOCK_UN)


def write_status(path, data):
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def choose(results, *, resilient=False):
    """Rank only after every declared candidate completes every declared game."""
    if (
        not results
        or any(
            type(row.get("mean_score")) not in (int, float)
            or not math.isfinite(row["mean_score"])
            or type(row.get("minimum_score")) is not int
            or row.get("completed_games") != row.get("expected_games")
            or type(row.get("expected_games")) is not int
            or row["expected_games"] < 1
            or not row.get("observation_cadence")
            or not isinstance(row.get("run_limits"), dict)
            for row in results
        )
        or len({row["observation_cadence"] for row in results}) != 1
        or len({json.dumps(row["run_limits"], sort_keys=True) for row in results}) != 1
    ):
        return None
    if resilient:
        return rank_screened(results)
    return max(results, key=lambda row: (row["mean_score"], row["minimum_score"]))


def train(
    workspace,
    application,
    source,
    *,
    horizons,
    radii,
    scope,
    minutes,
    policy="center-configurable-v20",
    profile="v1",
    risk_weights=None,
    edge_weights=None,
    margins=None,
    baseline_from=None,
    runner=run_benchmark,
    stop_requested=None,
):
    with exclusive_study(Path(workspace).resolve(strict=True)):
        return _train(
            workspace,
            application,
            source,
            horizons=horizons,
            radii=radii,
            scope=scope,
            minutes=minutes,
            policy=policy,
            profile=profile,
            risk_weights=risk_weights,
            edge_weights=edge_weights,
            margins=margins,
            baseline_from=baseline_from,
            runner=runner,
            stop_requested=stop_requested,
        )


def _train(
    workspace,
    application,
    source,
    *,
    horizons,
    radii,
    scope,
    minutes,
    policy,
    profile,
    risk_weights,
    edge_weights,
    margins,
    baseline_from,
    runner,
    stop_requested,
):
    workspace = Path(workspace).resolve(strict=True)
    application = Path(application).resolve(strict=True)
    source = Path(source).resolve(strict=True)
    if (
        scope not in ("focus", "development")
        or type(minutes) is not int
        or not 25 <= minutes <= 180
    ):
        raise ValueError("Choose focus or development and a 25–180 minute budget")
    if baseline_from is not None and scope != "focus":
        raise ValueError("Baseline reuse is only available for preliminary focus")
    risk_weights = [100] if risk_weights is None else risk_weights
    edge_weights = [100] if edge_weights is None else edge_weights
    if (
        not horizons
        or not radii
        or not risk_weights
        or not edge_weights
        or margins == []
        or len(horizons)
        * len(radii)
        * len(risk_weights)
        * len(edge_weights)
        * (len(margins) if margins is not None else 1)
        > 4
    ):
        raise ValueError("Choose at most four candidates")
    manifest = load_profile(workspace, profile, scope)
    output = workspace / ".local" / "dodge-training" / uuid.uuid4().hex
    output.mkdir(parents=True, exist_ok=False)
    candidates = generate(
        source,
        output / "packs",
        horizons,
        radii,
        policy,
        risk_weights,
        edge_weights,
        margins,
    )
    packs = [source] + [path / "pack.json" for path in candidates]
    pack_hashes = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in packs}
    fingerprint_paths = [
        source,
        Path(__file__),
        workspace / "tools/generate_dodge_candidates.py",
        Path(__file__).with_name("dodge_training_profiles.py"),
        Path(__file__).with_name("dodge_selection.py"),
        Path(__file__).with_name("dodge_baseline_cache.py"),
        workspace / "tools/fixtures" / PROFILES[profile]["file"],
    ]
    shared_files = fingerprints(workspace, application, source)
    status = {
        "schema_version": 1,
        "status": "running",
        "scope": scope,
        "development_profile": profile,
        "selection_method": "capped_mean_v1",
        "candidate_policy": policy,
        "budget_minutes": minutes,
        "game_timeout_seconds": GAME_TIMEOUT_SECONDS,
        "manifest": manifest,
        "sources_sha256": {
            str(p): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in fingerprint_paths
        },
        "shared_files_sha256": shared_files,
        "planned_packs": [str(p) for p in packs],
        "pack_sha256": pack_hashes,
        "results": [],
        "selected_for_development": None,
        "baseline_reused_from": (
            str(Path(baseline_from).resolve()) if baseline_from is not None else None
        ),
        "note": "Private 25% stepped games; visual inputs only. No automatic validation, real-time play, installation, or promotion.",
    }
    status_path = output / "study.json"
    write_status(status_path, status)
    print(f"Study: {output}", flush=True)
    print(
        "To stop before the next game, create a file named STOP in the study folder.",
        flush=True,
    )
    deadline = time.monotonic() + minutes * 60

    def should_stop():
        return (output / "STOP").exists() or (
            stop_requested is not None and stop_requested()
        )

    try:
        for index, pack in enumerate(packs):
            if should_stop() or time.monotonic() + GAME_TIMEOUT_SECONDS > deadline:
                status["status"] = "stopped_before_next_game"
                break
            benchmark = output / "benchmarks" / f"{index:02d}"
            if fingerprints(workspace, application, source) != shared_files:
                raise RuntimeError(
                    "Shared game or controller files changed during the study"
                )
            if any(
                hashlib.sha256(p.read_bytes()).hexdigest() != pack_hashes[str(p)]
                for p in packs
            ):
                raise RuntimeError("A planned pack changed during the study")
            status["active_pack"] = str(pack)
            write_status(status_path, status)
            if index == 0 and baseline_from is not None:
                summary = verified_baseline(
                    workspace, application, pack, manifest, baseline_from
                )
                benchmark.mkdir(parents=True, exist_ok=False)
                for name, value in (
                    ("manifest.json", manifest),
                    ("fingerprints.json", shared_files),
                    ("summary.json", summary),
                ):
                    (benchmark / name).write_text(
                        json.dumps(value, indent=2) + "\n", encoding="utf-8"
                    )
            else:
                summary = runner(
                    workspace,
                    application,
                    pack,
                    manifest,
                    benchmark,
                    deadline=deadline,
                    stop_requested=should_stop,
                )
            if fingerprints(workspace, application, source) != shared_files:
                raise RuntimeError(
                    "Shared game or controller files changed during the study"
                )
            if any(
                hashlib.sha256(p.read_bytes()).hexdigest() != pack_hashes[str(p)]
                for p in packs
            ):
                raise RuntimeError("A planned pack changed during the study")
            row = {
                key: summary.get(key)
                for key in (
                    "mean_score",
                    "median_score",
                    "minimum_score",
                    "completed_games",
                    "expected_games",
                    "policy",
                    "observation_cadence",
                    "run_limits",
                )
            }
            row.update(pack=str(pack), benchmark=str(benchmark))
            row["scores"] = [game["score"] for game in summary.get("games", [])]
            status["results"].append(row)
            write_status(status_path, status)
            if should_stop():
                status["status"] = "stopped_before_next_game"
                break
            if choose([row], resilient=True) is None:
                status["status"] = "incomplete"
                break
        else:
            best = choose(status["results"], resilient=True)
            status["status"] = "complete" if best is not None else "incomplete"
            status["selected_for_development"] = best
    except BaseException as exc:
        status["status"] = (
            "interrupted" if isinstance(exc, KeyboardInterrupt) else "error"
        )
        status["error"] = f"{type(exc).__name__}: {exc}"
        raise
    finally:
        status.pop("active_pack", None)
        write_status(status_path, status)
    return output, status


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workspace", type=Path)
    parser.add_argument("application", type=Path)
    parser.add_argument("source", type=Path)
    parser.add_argument("--horizons", nargs="+", type=int, default=[700])
    parser.add_argument("--radii", nargs="+", type=int, default=[50])
    parser.add_argument("--risk-weights", nargs="+", type=int, default=[100])
    parser.add_argument("--edge-weights", nargs="+", type=int, default=[100])
    parser.add_argument("--margins", nargs="+", type=int)
    parser.add_argument("--baseline-from", type=Path)
    parser.add_argument(
        "--policy",
        choices=tuple(POLICY_PREFIXES),
        default="center-configurable-v20",
    )
    parser.add_argument("--scope", choices=("focus", "development"), default="focus")
    parser.add_argument("--profile", choices=tuple(PROFILES), default="v1")
    parser.add_argument("--minutes", type=int, default=45)
    args = parser.parse_args()
    output, status = train(
        args.workspace,
        args.application,
        args.source,
        horizons=args.horizons,
        radii=args.radii,
        policy=args.policy,
        profile=args.profile,
        risk_weights=args.risk_weights,
        edge_weights=args.edge_weights,
        margins=args.margins,
        baseline_from=args.baseline_from,
        scope=args.scope,
        minutes=args.minutes,
    )
    print(f"{status['status']}: {output / 'study.json'}", flush=True)


if __name__ == "__main__":
    main()
