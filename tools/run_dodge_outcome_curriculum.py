"""Bounded local collect/train/test loop for experimental Dodge outcomes.

It runs private 25% stepped games sequentially, trains from completed records,
and reserves a distinct final holdout before fitting. It installs nothing.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil
import subprocess
import sys
import time
import uuid

from build_dodge_outcome_pack import build
from analyze_dodge_distribution import distribution
from analyze_dodge_outcomes import summarize_reports
from compare_dodge_benchmarks import compare
from dodge_holdout import reserve_holdout
from game_packs import load_pack
from run_dodge_benchmark import GAME_TIMEOUT_SECONDS, run_benchmark
from run_dodge_fallback_study import verify_pair
from train_dodge_offline import exclusive_study, write_status
from train_dodge_survival import environment_fingerprint
from train_dodge_action_advantage import _train_locked as train_action_advantage_locked


def _manifest_from_reservation(workspace, folder, name, count):
    folder.mkdir(parents=True)
    reserved = (
        reserve_holdout(workspace, folder, count=count)
        if count > 10
        else reserve_holdout(workspace, folder)
    )
    manifest = json.loads(reserved.read_text(encoding="utf-8"))
    manifest["id"] = f"dodge-outcome-{name}-{folder.parent.name}"
    manifest["purpose"] = f"Predeclared {name} games; separate from final validation."
    manifest["seeds"] = manifest["seeds"][:count]
    return manifest


def select_model(models, selection_metric):
    metric_key = {
        "brier": "brier_score",
        "balanced-brier": "balanced_brier",
        "game-balanced-brier": "game_balanced_brier",
    }[selection_metric]
    scored = [
        model
        for model in models
        if type(model["held_out_recorded_games"].get(metric_key)) in (int, float)
        and math.isfinite(model["held_out_recorded_games"][metric_key])
    ]
    return (
        min(scored, key=lambda x: x["held_out_recorded_games"][metric_key])
        if scored
        else None
    )


def run_curriculum(
    workspace,
    application,
    exploration_pack,
    baseline_pack,
    *,
    minutes=180,
    rounds=2,
    initial=None,
    player_speed=190,
    disk_limit_mb=2048,
    prior=(),
    skip_baseline_below_target=False,
    horizon_ms=5000,
    experience_app=None,
    turn_aware=False,
    compare_fallbacks=False,
    selection_metric="brier",
    retain_reference=False,
    experience_games=8,
    heldout_games=2,
    compare_balanced_training=False,
    action_advantage=False,
):
    workspace = Path(workspace).resolve(strict=True)
    application = Path(application).resolve(strict=True)
    experience_app = (
        Path(experience_app).resolve(strict=True)
        if experience_app is not None
        else application
    )
    exploration_pack = Path(exploration_pack).resolve(strict=True)
    baseline_pack = Path(baseline_pack).resolve(strict=True)
    initial = Path(initial).resolve(strict=True) if initial is not None else None
    prior = [Path(path).resolve(strict=True) for path in prior]
    if (
        type(minutes) is not int
        or not 45 <= minutes <= 300
        or type(rounds) is not int
        or not 1 <= rounds <= 7
        or player_speed not in (190, 400)
        or type(disk_limit_mb) is not int
        or not 256 <= disk_limit_mb <= 16384
        or len(prior) > 7
        or len(set(prior)) != len(prior)
        or initial in prior
        or horizon_ms not in (1000, 2000, 5000)
        or (turn_aware and compare_fallbacks)
        or (retain_reference and compare_fallbacks)
        or selection_metric not in ("brier", "balanced-brier", "game-balanced-brier")
        or type(experience_games) is not int
        or not 4 <= experience_games <= 32
        or type(heldout_games) is not int
        or not 2 <= heldout_games < experience_games
        or (
            action_advantage
            and (
                player_speed != 400
                or not retain_reference
                or compare_balanced_training
                or compare_fallbacks
                or turn_aware
            )
        )
    ):
        raise ValueError(
            "Choose bounded time, rounds, calibrated speed and 256–16384 MB disk budget"
        )
    if (
        load_pack(exploration_pack).get("context", {}).get("avoidance_policy")
        != "directional-explore-v35"
    ):
        raise ValueError("Choose a bounded Dodge exploration pack")
    if (
        load_pack(baseline_pack).get("context", {}).get("avoidance_policy")
        != "directional-v26"
    ):
        raise ValueError("Choose a directional Dodge baseline pack")
    output = workspace / ".local/dodge-curricula" / ("outcome-auto-" + uuid.uuid4().hex)
    output.mkdir(parents=True, exist_ok=False)
    final_folder = output / "final"
    final_folder.mkdir()
    final_path = reserve_holdout(workspace, final_folder)
    final_hash = hashlib.sha256(final_path.read_bytes()).hexdigest()
    plans = []
    for index in range(rounds):
        round_folder = output / f"round-{index + 1}"
        round_folder.mkdir()
        experience = (
            None
            if index == 0 and initial is not None
            else _manifest_from_reservation(
                workspace,
                round_folder / "experience-seeds",
                "experience",
                experience_games,
            )
        )
        development = _manifest_from_reservation(
            workspace, round_folder / "development-seeds", "development", 4
        )
        plans.append((round_folder, experience, development))
    if initial is not None or prior:
        existing_sets = [
            json.loads((path / "manifest.json").read_text(encoding="utf-8"))
            for path in ([initial] if initial else []) + prior
        ]
        initial_seeds = {
            seed for manifest in existing_sets for seed in manifest.get("seeds", [])
        }
        reserved_final = set(
            json.loads(final_path.read_text(encoding="utf-8"))["seeds"]
        )
        reserved_development = {
            seed for _, _, manifest in plans for seed in manifest["seeds"]
        }
        reserved_experience = {
            seed for _, manifest, _ in plans if manifest for seed in manifest["seeds"]
        }
        if not initial_seeds or initial_seeds & (
            reserved_final | reserved_development | reserved_experience
        ):
            raise ValueError("Initial experience overlaps newly reserved games")
    status = {
        "schema_version": 1,
        "status": "running",
        "budget_minutes": minutes,
        "feature_player_speed": player_speed,
        "horizon_ms": horizon_ms,
        "turn_aware_fallback": turn_aware,
        "compare_fallbacks": compare_fallbacks,
        "retain_reference": retain_reference,
        "experience_games": experience_games,
        "heldout_games": heldout_games,
        "compare_balanced_training": compare_balanced_training,
        "training_mode": (
            "action-advantage" if action_advantage else "survival-prediction"
        ),
        "selection_metric": None if action_advantage else selection_metric,
        "experience_application": str(experience_app),
        "comparison_application": str(application),
        "exploration_pack": str(exploration_pack),
        "baseline_pack": str(baseline_pack),
        "disk_limit_mb": disk_limit_mb,
        "prior_benchmarks": [str(path) for path in prior],
        "skip_baseline_below_target": skip_baseline_below_target,
        "rounds": [],
        "final_manifest_sha256": final_hash,
        "final": None,
        "note": "Private 25% stepped games; no installation or real-time play. Final games remain untouched unless development reaches 50 and a matched baseline completes.",
    }
    status_path = output / "curriculum.json"
    deadline = time.monotonic() + minutes * 60
    run_root = workspace / ".local/wsl-dodge-runs"
    existing_runs = (
        {p.name for p in run_root.iterdir() if p.is_dir()}
        if run_root.is_dir()
        else set()
    )
    resource_stop = None

    def update(state):
        status["status"] = state
        write_status(status_path, status)
        print(
            json.dumps(
                {
                    "curriculum": str(output),
                    "status": state,
                    "rounds": len(status["rounds"]),
                }
            ),
            flush=True,
        )

    def stopped():
        nonlocal resource_stop
        if (output / "STOP").exists():
            return True
        if shutil.disk_usage(workspace).free < 2 * 1024**3:
            resource_stop = "less than 2 GiB free"
            return True
        total = 0
        if run_root.is_dir():
            for directory in run_root.iterdir():
                if (
                    not directory.is_dir()
                    or directory.is_symlink()
                    or directory.name in existing_runs
                ):
                    continue
                for path in directory.rglob("*"):
                    if path.is_file() and not path.is_symlink():
                        total += path.stat().st_size
                        if total >= disk_limit_mb * 1024**2:
                            resource_stop = (
                                "new game evidence reached the study disk budget"
                            )
                            return True
        return False

    def games(pack, manifest, path, *, executable=None):
        result = run_benchmark(
            workspace,
            executable or application,
            pack,
            manifest,
            path,
            deadline=deadline,
            stop_requested=stopped,
        )
        if result.get("mean_score") is None:
            raise RuntimeError(
                "A planned game set was incomplete; its results were not ranked"
            )
        return result

    update("running")
    try:
        with exclusive_study(workspace):
            previous = list(prior)
            for index, (folder, experience_manifest, development_manifest) in enumerate(
                plans
            ):
                if stopped():
                    status["stop_reason"] = resource_stop
                    update("resource_limit" if resource_stop else "stopped")
                    return output, status
                row = {"round": index + 1, "status": "collecting"}
                status["rounds"].append(row)
                update("collecting")
                if index == 0 and initial is not None:
                    experience_path = initial
                    experience_manifest = json.loads(
                        (experience_path / "manifest.json").read_text(encoding="utf-8")
                    )
                else:
                    experience_path = folder / "experience"
                    games(
                        exploration_pack,
                        experience_manifest,
                        experience_path,
                        executable=experience_app,
                    )
                row["experience"] = str(experience_path)
                seeds = experience_manifest["seeds"]
                if len(seeds) < 4 or len(seeds) <= heldout_games:
                    raise ValueError(
                        "At least four distinct experience seeds are required"
                    )
                heldout = seeds[-heldout_games:]
                commands = []
                profiles = [
                    (False, 12, 0.0001, False),
                    (True, 12, 0.0001, False),
                    (True, 3, 0.01, False),
                    (True, 1, 0.05, False),
                ]
                if compare_balanced_training:
                    profiles.extend([(False, 12, 0.0001, True), (True, 3, 0.01, True)])
                if action_advantage:
                    profiles = []
                for exploration_only, epochs, l2, balance_outcomes in profiles:
                    command = [
                        sys.executable,
                        str(workspace / "tools/train_dodge_survival.py"),
                        str(workspace),
                        str(experience_path),
                        "--test-seeds",
                        *map(str, heldout),
                        "--contextual",
                        "--include-holds",
                        "--player-speed",
                        str(player_speed),
                        "--horizon-ms",
                        str(horizon_ms),
                        "--epochs",
                        str(epochs),
                        "--l2",
                        str(l2),
                    ]
                    for earlier in previous[-7:]:
                        command.extend(["--also-benchmark", str(earlier)])
                    if exploration_only:
                        command.append("--exploration-training-only")
                    if balance_outcomes:
                        command.append("--balance-outcomes")
                    commands.append(
                        (exploration_only, epochs, l2, balance_outcomes, command)
                    )
                row["status"] = "training"
                update("training")
                models = []
                for exploration_only, epochs, l2, balance_outcomes, command in commands:
                    if stopped():
                        status["stop_reason"] = resource_stop
                        update("resource_limit" if resource_stop else "stopped")
                        return output, status
                    completed = subprocess.run(
                        command, capture_output=True, text=True, timeout=90, check=False
                    )
                    if completed.returncode:
                        row.setdefault("training_errors", []).append(
                            {
                                "exploration_only": exploration_only,
                                "epochs": epochs,
                                "l2": l2,
                                "balance_outcomes": balance_outcomes,
                                "error": completed.stderr[-2000:],
                            }
                        )
                        continue
                    result = json.loads(completed.stdout)
                    models.append(
                        {
                            "exploration_only": exploration_only,
                            "epochs": epochs,
                            "l2": l2,
                            "balance_outcomes": balance_outcomes,
                            "output": result["output"],
                            "held_out_recorded_games": result[
                                "held_out_recorded_games"
                            ],
                            "held_out_examples_sha256": result[
                                "held_out_examples_sha256"
                            ],
                        }
                    )
                if action_advantage:
                    if stopped():
                        status["stop_reason"] = resource_stop
                        update("resource_limit" if resource_stop else "stopped")
                        return output, status
                    try:
                        model_output, action_report = train_action_advantage_locked(
                            workspace,
                            [experience_path, *previous[-7:]],
                            heldout,
                            horizon_ms=horizon_ms,
                        )
                        models.append(
                            {
                                "algorithm": "linear-observational-action-advantage",
                                "output": str(model_output),
                                "training_examples": action_report["training_examples"],
                                "held_out_action_support": action_report[
                                    "held_out_action_support"
                                ],
                            }
                        )
                    except ValueError as error:
                        row.setdefault("training_errors", []).append(
                            {
                                "algorithm": "linear-observational-action-advantage",
                                "error": str(error),
                            }
                        )
                previous.append(experience_path)
                if not models:
                    row["status"] = "no_model"
                    continue
                if (
                    not action_advantage
                    and len({model["held_out_examples_sha256"] for model in models})
                    != 1
                ):
                    raise ValueError(
                        "Outcome models were evaluated on different held-out examples"
                    )
                row["models"] = models
                winner = (
                    models[0]
                    if action_advantage
                    else select_model(models, selection_metric)
                )
                if winner is None:
                    row["status"] = "no_model_with_selection_metric"
                    continue
                row["selected_model"] = winner["output"]
                candidate_directory = build(
                    workspace,
                    exploration_pack,
                    winner["output"],
                    folder / "packs",
                    turn_aware=turn_aware,
                    **({"retain_reference": True} if retain_reference else {}),
                )
                candidate = Path(candidate_directory) / "pack.json"
                if not candidate.is_file():
                    raise ValueError("Built outcome pack is missing its manifest")
                row["status"] = "development"
                update("development")
                candidate_path, baseline_path = (
                    folder / "candidate",
                    folder / "baseline",
                )
                candidate_result = games(
                    candidate, development_manifest, candidate_path
                )
                if compare_fallbacks:
                    turn_directory = build(
                        workspace,
                        exploration_pack,
                        winner["output"],
                        folder / "packs",
                        turn_aware=True,
                    )
                    turn_candidate = Path(turn_directory) / "pack.json"
                    if not turn_candidate.is_file():
                        raise ValueError(
                            "Built turn-aware pack is missing its manifest"
                        )
                    verify_pair(load_pack(candidate), load_pack(turn_candidate))
                    turn_path = folder / "turn-candidate"
                    turn_result = games(turn_candidate, development_manifest, turn_path)
                    if environment_fingerprint(
                        candidate_path
                    ) != environment_fingerprint(turn_path):
                        raise ValueError("Fallback comparison environment changed")
                    fallback_comparison = compare(candidate_path, turn_path)
                    write_status(
                        folder / "fallback-comparison.json", fallback_comparison
                    )
                    row["fallback_comparison"] = str(
                        folder / "fallback-comparison.json"
                    )
                    if turn_result["mean_score"] > candidate_result["mean_score"]:
                        candidate, candidate_path, candidate_result = (
                            turn_candidate,
                            turn_path,
                            turn_result,
                        )
                row["candidate_pack"] = str(candidate)
                row["candidate_benchmark"] = str(candidate_path)
                row["candidate_mean"] = candidate_result["mean_score"]
                row["candidate_distribution"] = distribution(
                    [game["score"] for game in candidate_result["games"]]
                )
                row["decision_diagnostics"] = summarize_reports(
                    [game["report"] for game in candidate_result["games"]]
                )
                if skip_baseline_below_target and candidate_result["mean_score"] < 50:
                    row["status"] = "candidate_below_target_baseline_skipped"
                    update("candidate_below_target_baseline_skipped")
                    continue
                baseline_result = games(
                    baseline_pack, development_manifest, baseline_path
                )
                row["baseline_benchmark"] = str(baseline_path)
                if environment_fingerprint(candidate_path) != environment_fingerprint(
                    baseline_path
                ):
                    raise ValueError("Comparison environment changed")
                comparison = compare(baseline_path, candidate_path)
                write_status(folder / "comparison.json", comparison)
                row["status"] = "compared"
                row["candidate_mean"] = candidate_result["mean_score"]
                row["baseline_mean"] = baseline_result["mean_score"]
                row["baseline_distribution"] = distribution(
                    [game["score"] for game in baseline_result["games"]]
                )
                update("compared")
                if candidate_result["mean_score"] < 50:
                    continue
                if stopped() or time.monotonic() + GAME_TIMEOUT_SECONDS * 10 > deadline:
                    status["stop_reason"] = resource_stop
                    update("budget_or_stop_before_final")
                    return output, status
                if hashlib.sha256(final_path.read_bytes()).hexdigest() != final_hash:
                    raise ValueError("Reserved final manifest changed")
                final_manifest = json.loads(final_path.read_text(encoding="utf-8"))
                final_result = games(candidate, final_manifest, folder / "final")
                status["final"] = {
                    "benchmark": str(folder / "final"),
                    "mean_score": final_result["mean_score"],
                    "scores": [game["score"] for game in final_result["games"]],
                    "distribution": distribution(
                        [game["score"] for game in final_result["games"]]
                    ),
                }
                update(
                    "final_target_met"
                    if final_result["mean_score"] >= 50
                    else "final_below_target"
                )
                return output, status
            update(
                "development_below_target"
                if any("candidate_mean" in row for row in status["rounds"])
                else "no_model"
            )
            return output, status
    except Exception as error:
        if stopped():
            status["stop_reason"] = resource_stop
            update("resource_limit" if resource_stop else "stopped")
            return output, status
        if time.monotonic() + GAME_TIMEOUT_SECONDS > deadline:
            update("budget_exhausted")
            return output, status
        status["error"] = f"{type(error).__name__}: {error}"
        update("incomplete")
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workspace", type=Path)
    parser.add_argument("application", type=Path)
    parser.add_argument("exploration_pack", type=Path)
    parser.add_argument("baseline_pack", type=Path)
    parser.add_argument("--minutes", type=int, default=180)
    parser.add_argument("--rounds", type=int, default=2)
    parser.add_argument(
        "--experience-games",
        type=int,
        default=8,
        help="Fresh randomized games per collection round (4–32)",
    )
    parser.add_argument(
        "--heldout-games",
        type=int,
        default=2,
        help="Whole experience games held out from training for model selection",
    )
    parser.add_argument("--initial", type=Path)
    parser.add_argument("--player-speed", type=int, choices=(190, 400), default=190)
    parser.add_argument("--disk-limit-mb", type=int, default=2048)
    parser.add_argument("--prior", type=Path, action="append", default=[])
    parser.add_argument(
        "--skip-baseline-below-target",
        action="store_true",
        help="Skip the paired baseline if the complete candidate mean is already below 50",
    )
    parser.add_argument(
        "--horizon-ms", type=int, choices=(1000, 2000, 5000), default=5000
    )
    parser.add_argument(
        "--experience-app",
        type=Path,
        help="Use a fixed compatible collector build for experience games",
    )
    parser.add_argument(
        "--turn-aware",
        action="store_true",
        help="Use the experimental two-phase fallback when no immediate move clears the guard",
    )
    parser.add_argument(
        "--compare-fallbacks",
        action="store_true",
        help="Compare both fallback methods on identical development games",
    )
    parser.add_argument(
        "--retain-reference",
        action="store_true",
        help="Keep the directional reference when its visual clearance is acceptable",
    )
    parser.add_argument(
        "--selection",
        choices=("brier", "balanced-brier", "game-balanced-brier"),
        default="brier",
        help="Model-selection error on the shared held-out decisions",
    )
    parser.add_argument(
        "--compare-balanced-training",
        action="store_true",
        help="Also train two small models that balance observed losses and survival",
    )
    parser.add_argument(
        "--action-advantage",
        action="store_true",
        help="Train the experimental randomized action-contrast model instead of survival predictors",
    )
    args = parser.parse_args()
    run_curriculum(
        args.workspace,
        args.application,
        args.exploration_pack,
        args.baseline_pack,
        minutes=args.minutes,
        rounds=args.rounds,
        initial=args.initial,
        player_speed=args.player_speed,
        disk_limit_mb=args.disk_limit_mb,
        prior=args.prior,
        skip_baseline_below_target=args.skip_baseline_below_target,
        horizon_ms=args.horizon_ms,
        experience_app=args.experience_app,
        turn_aware=args.turn_aware,
        compare_fallbacks=args.compare_fallbacks,
        selection_metric=args.selection,
        retain_reference=args.retain_reference,
        experience_games=args.experience_games,
        heldout_games=args.heldout_games,
        compare_balanced_training=args.compare_balanced_training,
        action_advantage=args.action_advantage,
    )


if __name__ == "__main__":
    main()
