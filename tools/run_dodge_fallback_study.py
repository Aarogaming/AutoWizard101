"""Compare bounded Dodge decision methods or compatible learned models.

Fresh private slow games are sequential. Final games are reserved in advance
and are not touched unless a complete development mean reaches 50.
"""

import argparse
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import shutil
import time
import uuid

from compare_dodge_benchmarks import compare
from build_dodge_outcome_pack import validate as validate_model
from dodge_holdout import reserve_holdout
from game_packs import load_pack
from run_dodge_benchmark import GAME_TIMEOUT_SECONDS, run_benchmark
from train_dodge_offline import exclusive_study, write_status
from train_dodge_survival import environment_fingerprint


def verify_pair(standard, turn):
    standard, turn = deepcopy(standard), deepcopy(turn)
    pair = (
        standard.get("context", {}).get("avoidance_policy"),
        turn.get("context", {}).get("avoidance_policy"),
    )
    if pair not in (
        ("directional-outcome-v34", "directional-outcome-turn-v36"),
        ("directional-outcome-reference-v37", "directional-outcome-escape-v38"),
    ):
        raise ValueError("Choose an approved pair of outcome decision policies")
    if not standard.get("experimental_outcome_model") or standard.get(
        "experimental_outcome_model"
    ) != turn.get("experimental_outcome_model"):
        raise ValueError("Both packs must contain identical learned model weights")
    for pack in (standard, turn):
        pack.pop("version", None)
        pack.pop("title", None)
        pack["context"].pop("avoidance_policy")
    if standard != turn:
        raise ValueError("The packs differ beyond their decision method")


def verify_model_pair(standard, candidate):
    """Require identical context and action behavior apart from model weights."""
    standard, candidate = deepcopy(standard), deepcopy(candidate)
    if any(
        pack.get("context", {}).get("avoidance_policy")
        != "directional-outcome-reference-v37"
        for pack in (standard, candidate)
    ):
        raise ValueError("Model comparison requires the reference-guarded policy")
    first = validate_model(standard.get("experimental_outcome_model"))
    second = validate_model(candidate.get("experimental_outcome_model"))
    pair = first["algorithm"], second["algorithm"]
    comparable = (
        pair
        == (
            "binary-logistic-observational-survival",
            "linear-observational-action-advantage",
        )
        and first["horizon_ms"] == second["horizon_ms"]
    ) or pair == (
        "linear-observational-action-advantage",
        "linear-observational-episode-advantage",
    )
    if not comparable or first["feature_version"] != second["feature_version"]:
        raise ValueError(
            "Choose compatible calibrated outcome and action-advantage models"
        )
    for pack in (standard, candidate):
        for name in (
            "version",
            "title",
            "experimental_outcome_model",
            "experimental_outcome_provenance",
        ):
            pack.pop(name, None)
    if standard != candidate:
        raise ValueError("The packs differ beyond their learned model")


def run_study(
    workspace,
    application,
    standard,
    turn,
    *,
    minutes=300,
    disk_limit_mb=2048,
    candidate_only=False,
    compare_models=False,
):
    workspace, application, standard, turn = (
        Path(p).resolve(strict=True) for p in (workspace, application, standard, turn)
    )
    if (
        type(minutes) is not int
        or not 45 <= minutes <= 300
        or type(disk_limit_mb) is not int
        or not 256 <= disk_limit_mb <= 16384
    ):
        raise ValueError("Choose a bounded time and disk budget")
    if not standard.is_relative_to(workspace) or not turn.is_relative_to(workspace):
        raise ValueError("Packs must be inside the workspace")
    first_pack, second_pack = load_pack(standard), load_pack(turn)
    (verify_model_pair if compare_models else verify_pair)(first_pack, second_pack)
    output = workspace / ".local/dodge-curricula" / ("fallback-" + uuid.uuid4().hex)
    output.mkdir(parents=True, exist_ok=False)
    dev_folder, final_folder = output / "development-seeds", output / "final-seeds"
    dev_folder.mkdir()
    final_folder.mkdir()
    dev_path = reserve_holdout(workspace, dev_folder)
    final_path = reserve_holdout(workspace, final_folder)
    development = json.loads(dev_path.read_text(encoding="utf-8"))
    development["id"] = "dodge-fallback-development-" + output.name
    development["purpose"] = (
        "Fresh paired development games, separate from final validation."
    )
    development["seeds"] = development["seeds"][:4]
    final_hash = hashlib.sha256(final_path.read_bytes()).hexdigest()
    status = {
        "schema_version": 1,
        "status": "running",
        "comparison_application": str(application),
        "standard_pack": str(standard),
        "turn_pack": str(turn),
        "disk_limit_mb": disk_limit_mb,
        "candidate_only": candidate_only,
        "compare_models": compare_models,
        "final_manifest_sha256": final_hash,
        "comparison": None,
        "final": None,
        "note": "Fresh private 25% stepped games. No model is installed or promoted.",
    }
    path = output / "study.json"
    deadline = time.monotonic() + minutes * 60
    run_root = workspace / ".local/wsl-dodge-runs"
    existing_runs = (
        {p.name for p in run_root.iterdir() if p.is_dir()}
        if run_root.is_dir()
        else set()
    )
    resource_stop = None

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
                for item in directory.rglob("*"):
                    if item.is_file() and not item.is_symlink():
                        total += item.stat().st_size
                        if total >= disk_limit_mb * 1024**2:
                            resource_stop = (
                                "new game evidence reached the study disk budget"
                            )
                            return True
        return False

    def update(value):
        status["status"] = value
        write_status(path, status)
        print(json.dumps({"study": str(output), "status": value}), flush=True)

    def run(pack, manifest, directory):
        result = run_benchmark(
            workspace,
            application,
            pack,
            manifest,
            directory,
            deadline=deadline,
            stop_requested=stopped,
        )
        if result.get("mean_score") is None:
            raise RuntimeError("An incomplete game set cannot be ranked")
        return result

    update("running")
    try:
        with exclusive_study(workspace):
            update("standard_development")
            first = run(standard, development, output / "standard")
            update("turn_development")
            second = run(turn, development, output / "turn")
            if environment_fingerprint(output / "standard") != environment_fingerprint(
                output / "turn"
            ):
                raise ValueError("Comparison environment changed")
            comparison = compare(output / "standard", output / "turn")
            write_status(output / "comparison.json", comparison)
            status["comparison"] = str(output / "comparison.json")
            if candidate_only and second["mean_score"] <= first["mean_score"]:
                update("candidate_not_better")
                return output, status
            selected, selected_result = (
                (turn, second)
                if second["mean_score"] > first["mean_score"]
                else (standard, first)
            )
            status["selected_pack"] = str(selected)
            status["development_mean"] = selected_result["mean_score"]
            if selected_result["mean_score"] < 50:
                update("development_below_target")
                return output, status
            if stopped() or time.monotonic() + 10 * GAME_TIMEOUT_SECONDS > deadline:
                status["stop_reason"] = resource_stop
                update("budget_or_stop_before_final")
                return output, status
            if hashlib.sha256(final_path.read_bytes()).hexdigest() != final_hash:
                raise ValueError("Reserved final manifest changed")
            update("final_validation")
            result = run(
                selected,
                json.loads(final_path.read_text(encoding="utf-8")),
                output / "final",
            )
            status["final"] = {
                "benchmark": str(output / "final"),
                "mean_score": result["mean_score"],
                "scores": [game["score"] for game in result["games"]],
            }
            update(
                "final_target_met"
                if result["mean_score"] >= 50
                else "final_below_target"
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
    parser.add_argument("standard", type=Path)
    parser.add_argument("turn", type=Path)
    parser.add_argument("--minutes", type=int, default=300)
    parser.add_argument("--disk-limit-mb", type=int, default=2048)
    parser.add_argument(
        "--candidate-only",
        action="store_true",
        help="Validate the second policy only if its paired development mean improves on the first",
    )
    parser.add_argument(
        "--compare-models",
        action="store_true",
        help="Compare compatible outcome or episode-advantage models with the same guarded policy",
    )
    args = parser.parse_args()
    run_study(
        args.workspace,
        args.application,
        args.standard,
        args.turn,
        minutes=args.minutes,
        disk_limit_mb=args.disk_limit_mb,
        candidate_only=args.candidate_only,
        compare_models=args.compare_models,
    )


if __name__ == "__main__":
    main()
