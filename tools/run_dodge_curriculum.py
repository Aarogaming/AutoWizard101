"""Run a bounded focus, development, and held-out Dodge curriculum offline.

Only completed games are ranked. Held-out games run once, and only after the
chosen policy reaches the development target. No pack is installed or promoted.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import time
import uuid

from benchmark_dodge_scores import summarize
from dodge_holdout import reserve_holdout
from dodge_training_profiles import PROFILES
from generate_dodge_candidates import POLICY_PREFIXES
from run_dodge_benchmark import GAME_TIMEOUT_SECONDS, run_benchmark
from train_dodge_offline import choose, exclusive_study, train, write_status


def load_completed_focus(workspace, source, directory, profile="v1"):
    """Recheck every focused game and pack before reusing a selection."""
    directory = Path(directory).resolve(strict=True)
    root = (workspace / ".local/dodge-training").resolve(strict=True)
    if not directory.is_relative_to(root) or directory == root:
        raise ValueError(
            "Focused study must be inside this workspace's training folder"
        )
    study = json.loads((directory / "study.json").read_text(encoding="utf-8"))
    manifest = study.get("manifest", {})
    if (
        study.get("status") != "complete"
        or study.get("scope") != "focus"
        or profile not in PROFILES
        or (study.get("development_profile") or "v1") != profile
        or manifest.get("seeds") != PROFILES[profile]["focus"]
        or manifest.get("speed") != 0.25
        or manifest.get("mode") != "stepped"
        or manifest.get("target_mean_score") != 50
    ):
        raise ValueError("Focused study is incomplete or uses different conditions")
    planned, results = study.get("planned_packs"), study.get("results")
    if (
        not isinstance(planned, list)
        or not isinstance(results, list)
        or len(planned) < 2
        or len(planned) != len(results)
    ):
        raise ValueError("Focused study did not complete all planned candidates")
    rows = []
    run_root = (workspace / ".local/wsl-dodge-runs").resolve(strict=True)
    for planned_pack, recorded in zip(planned, results):
        pack = Path(planned_pack).resolve(strict=True)
        if str(pack) != recorded.get("pack") or (
            pack != source and not pack.is_relative_to(directory / "packs")
        ):
            raise ValueError("Focused pack is outside its declared study")
        digest = hashlib.sha256(pack.read_bytes()).hexdigest()
        if study.get("pack_sha256", {}).get(str(pack)) != digest:
            raise ValueError("Focused pack changed")
        benchmark = Path(recorded["benchmark"]).resolve(strict=True)
        if not benchmark.is_relative_to(directory / "benchmarks"):
            raise ValueError("Focused benchmark is outside its declared study")
        if (
            json.loads((benchmark / "manifest.json").read_text(encoding="utf-8"))
            != manifest
        ):
            raise ValueError("Focused benchmark manifest changed")
        saved = json.loads((benchmark / "summary.json").read_text(encoding="utf-8"))
        reports = [
            Path(row["report"]).resolve(strict=True)
            for row in saved.get("games", [])
            if row.get("report")
        ]
        if any(not report.is_relative_to(run_root) for report in reports):
            raise ValueError("Focused report is outside the private run folder")
        result = summarize(manifest, reports)
        if result["pack"] is None or result["pack"]["Sha256"].lower() != digest:
            raise ValueError("Focused report does not match its pack")
        row = {
            key: result.get(key)
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
        row["scores"] = [game["score"] for game in result["games"]]
        rows.append(row)
    method = study.get("selection_method")
    if method not in (None, "capped_mean_v1"):
        raise ValueError("Unknown focused-study selection method")
    winner = choose(rows, resilient=method == "capped_mean_v1")
    if winner is None or winner["pack"] != (
        study.get("selected_for_development") or {}
    ).get("pack"):
        raise ValueError("Focused results do not support the saved selection")
    return {"status": "complete", "selected_for_development": winner, "results": rows}


def run_curriculum(
    workspace,
    application,
    source,
    *,
    horizons,
    radii,
    policy,
    minutes,
    profile="v1",
    risk_weights=None,
    edge_weights=None,
    margins=None,
    baseline_from=None,
    from_focus=None,
    train_fn=train,
    benchmark_fn=run_benchmark,
):
    workspace = Path(workspace).resolve(strict=True)
    application = Path(application).resolve(strict=True)
    source = Path(source).resolve(strict=True)
    if type(minutes) is not int or not 45 <= minutes <= 300:
        raise ValueError("Choose a total budget from 45 to 300 minutes")
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
    if profile not in PROFILES or policy not in POLICY_PREFIXES:
        raise ValueError("Unknown development profile or policy")
    if from_focus is not None and baseline_from is not None:
        raise ValueError("Choose a focused study or a baseline, not both")
    output = workspace / ".local" / "dodge-curricula" / uuid.uuid4().hex
    output.mkdir(parents=True, exist_ok=False)
    holdout_path = reserve_holdout(workspace, output)
    holdout_hash = hashlib.sha256(holdout_path.read_bytes()).hexdigest()
    status = {
        "schema_version": 1,
        "status": "running",
        "budget_minutes": minutes,
        "policy": policy,
        "development_profile": profile,
        "validation_manifest_sha256": holdout_hash,
        "focus": None,
        "development": None,
        "validation": None,
        "note": "Private slow stepped games only. Validation is run once only after a complete development mean of at least 50. No installation, promotion, or real-time play.",
    }
    path = output / "curriculum.json"
    write_status(path, status)
    print(f"Curriculum: {output}", flush=True)
    deadline = time.monotonic() + minutes * 60

    def remaining_minutes():
        return max(0, math.floor((deadline - time.monotonic()) / 60))

    try:
        if from_focus is not None:
            focus_path = Path(from_focus).resolve(strict=True)
            focus = load_completed_focus(workspace, source, focus_path, profile)
            status["focus_reused_for_screening_only"] = True
        else:
            focus_path, focus = train_fn(
                workspace,
                application,
                source,
                horizons=horizons,
                radii=radii,
                policy=policy,
                profile=profile,
                risk_weights=risk_weights,
                edge_weights=edge_weights,
                margins=margins,
                baseline_from=baseline_from,
                scope="focus",
                minutes=min(180, remaining_minutes()),
                stop_requested=lambda: (output / "STOP").exists(),
            )
        status["focus"] = str(focus_path)
        write_status(path, status)
        choice = focus.get("selected_for_development")
        if (output / "STOP").exists() or (focus_path / "STOP").exists():
            status["status"] = "stopped"
        elif focus.get("status") != "complete" or not choice:
            status["status"] = "focus_incomplete"
        elif Path(choice["pack"]).resolve() == source:
            status["status"] = "no_focus_improvement"
        elif remaining_minutes() < 25:
            status["status"] = "budget_before_development"
        else:
            focus_pack = Path(choice["pack"])
            candidate = json.loads(focus_pack.read_text(encoding="utf-8"))
            if candidate["context"]["avoidance_policy"] != policy:
                raise ValueError("Focused candidate does not match the declared policy")
            constraints = candidate["context"]["constraints"]
            horizon, radius = (
                constraints["prediction_horizon_ms"],
                constraints["threat_radius_px"],
            )
            risk = constraints.get("risk_weight_percent", 100)
            edge = constraints.get("edge_weight_percent", 100)
            margin = constraints["edge_margin_px"]
            selected_margins = (
                [margin] if candidate["version"].endswith(f"-m{margin}") else None
            )
            development_path, development = train_fn(
                workspace,
                application,
                source,
                horizons=[horizon],
                radii=[radius],
                policy=policy,
                profile=profile,
                risk_weights=[risk],
                edge_weights=[edge],
                margins=selected_margins,
                scope="development",
                minutes=min(180, remaining_minutes()),
                stop_requested=lambda: (output / "STOP").exists(),
            )
            status["development"] = str(development_path)
            write_status(path, status)
            expected_hash = hashlib.sha256(focus_pack.read_bytes()).hexdigest()
            matched = [
                row
                for row in development.get("results", [])
                if row.get("policy") == policy
                and hashlib.sha256(Path(row["pack"]).read_bytes()).hexdigest()
                == expected_hash
            ]
            if development.get("status") == "complete" and len(matched) != 1:
                raise ValueError(
                    "Development candidate differs from the focused candidate"
                )
            winner = development.get("selected_for_development")
            if (output / "STOP").exists() or (development_path / "STOP").exists():
                status["status"] = "stopped"
            elif development.get("status") != "complete" or not winner:
                status["status"] = "development_incomplete"
            elif winner["mean_score"] < 50:
                status["status"] = "development_below_target"
            elif time.monotonic() + 10 * GAME_TIMEOUT_SECONDS > deadline:
                status["status"] = "budget_before_validation"
            else:
                # This file is deliberately not read during focus or development.
                if (
                    hashlib.sha256(holdout_path.read_bytes()).hexdigest()
                    != holdout_hash
                ):
                    raise ValueError("Reserved validation manifest changed")
                manifest = json.loads(holdout_path.read_text(encoding="utf-8"))
                if (
                    manifest.get("mode") != "stepped"
                    or manifest.get("speed") != 0.25
                    or len(manifest.get("seeds", [])) != 10
                    or not set(manifest["seeds"]).isdisjoint(PROFILES[profile]["seeds"])
                ):
                    raise ValueError("The reserved validation conditions changed")
                validation_output = output / "validation"
                with exclusive_study(workspace):
                    validation = benchmark_fn(
                        workspace,
                        application,
                        Path(winner["pack"]),
                        manifest,
                        validation_output,
                        deadline=deadline,
                        stop_requested=lambda: (output / "STOP").exists(),
                    )
                status["validation"] = str(validation_output)
                status["validation_mean"] = validation["mean_score"]
                status["status"] = (
                    "validation_incomplete"
                    if validation["mean_score"] is None
                    else (
                        "slow_validation_met"
                        if validation["mean_score"] >= 50
                        else "slow_validation_below_target"
                    )
                )
    except BaseException as exc:
        status["status"] = (
            "interrupted" if isinstance(exc, KeyboardInterrupt) else "error"
        )
        status["error"] = f"{type(exc).__name__}: {exc}"
        raise
    finally:
        write_status(path, status)
    return output, status


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workspace", type=Path)
    parser.add_argument("application", type=Path)
    parser.add_argument("source", type=Path)
    parser.add_argument("--horizons", nargs="+", type=int, default=[350, 700])
    parser.add_argument("--radii", nargs="+", type=int, default=[50])
    parser.add_argument("--risk-weights", nargs="+", type=int, default=[100])
    parser.add_argument("--edge-weights", nargs="+", type=int, default=[100])
    parser.add_argument("--margins", nargs="+", type=int)
    parser.add_argument("--baseline-from", type=Path)
    parser.add_argument(
        "--policy",
        choices=tuple(POLICY_PREFIXES),
        default="directional-v26",
    )
    parser.add_argument("--minutes", type=int, default=300)
    parser.add_argument("--profile", choices=tuple(PROFILES), default="v1")
    parser.add_argument(
        "--from-focus",
        type=Path,
        help="Reuse a fully completed focused study for candidate screening only",
    )
    args = parser.parse_args()
    output, status = run_curriculum(
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
        minutes=args.minutes,
        from_focus=args.from_focus,
    )
    print(f"{status['status']}: {output / 'curriculum.json'}", flush=True)


if __name__ == "__main__":
    main()
