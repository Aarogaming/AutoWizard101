"""Resume only a deferred, never-started slow-Dodge holdout evaluation."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import time

from benchmark_dodge_scores import summarize
from dodge_holdout import DEVELOPMENT_SEEDS, LEGACY_VALIDATION_SEEDS
from dodge_training_profiles import PROFILES, load_profile
from run_dodge_benchmark import GAME_TIMEOUT_SECONDS, fingerprints, run_benchmark
from train_dodge_offline import choose, exclusive_study, write_status


def verify_development(workspace, application, curriculum):
    """Reconstruct the complete development selection from its saved reports."""
    profile = curriculum.get("development_profile", "v1")
    manifest = load_profile(workspace, profile, "development")
    directory = Path(curriculum["development"]).resolve(strict=True)
    training_root = (workspace / ".local/dodge-training").resolve(strict=True)
    if directory == training_root or not directory.is_relative_to(training_root):
        raise ValueError("Development study is outside the workspace")
    study = json.loads((directory / "study.json").read_text(encoding="utf-8"))
    if study.get("status") != "complete" or study.get("manifest") != manifest:
        raise ValueError("Development study is incomplete or its profile changed")
    planned, results = study.get("planned_packs"), study.get("results")
    if (
        not isinstance(planned, list)
        or not isinstance(results, list)
        or len(planned) < 2
        or len(planned) != len(results)
    ):
        raise ValueError("Development study did not complete all candidates")
    source = Path(planned[0]).resolve(strict=True)
    if not source.is_relative_to(workspace):
        raise ValueError("Development source is outside the workspace")
    if fingerprints(workspace, application, source) != study.get("shared_files_sha256"):
        raise ValueError("Game or controller files changed since development")
    sources = study.get("sources_sha256")
    required_sources = {
        str(source), str(workspace / "tools/train_dodge_offline.py"),
        str(workspace / "tools/generate_dodge_candidates.py"),
        str(workspace / "tools/dodge_training_profiles.py"),
        str(workspace / "tools/dodge_selection.py"),
        str(workspace / "tools/fixtures" / PROFILES[profile]["file"]),
    }
    if not isinstance(sources, dict) or not required_sources.issubset(sources):
        raise ValueError("Development source fingerprints are missing")
    for path, digest in sources.items():
        if hashlib.sha256(Path(path).read_bytes()).hexdigest() != digest:
            raise ValueError("Training or selection files changed since development")
    run_root = (workspace / ".local/wsl-dodge-runs").resolve(strict=True)
    rows = []
    for index, (planned_pack, recorded) in enumerate(zip(planned, results)):
        pack = Path(planned_pack).resolve(strict=True)
        if str(pack) != recorded.get("pack") or (
            index and not pack.is_relative_to(directory / "packs")
        ):
            raise ValueError("Development pack is outside its declared study")
        digest = hashlib.sha256(pack.read_bytes()).hexdigest()
        if study.get("pack_sha256", {}).get(str(pack)) != digest:
            raise ValueError("Development pack changed")
        benchmark = Path(recorded["benchmark"]).resolve(strict=True)
        if not benchmark.is_relative_to(directory / "benchmarks"):
            raise ValueError("Development benchmark is outside its study")
        if (
            json.loads((benchmark / "manifest.json").read_text(encoding="utf-8"))
            != manifest
        ):
            raise ValueError("Development benchmark manifest changed")
        saved = json.loads((benchmark / "summary.json").read_text(encoding="utf-8"))
        reports = [
            Path(game["report"]).resolve(strict=True)
            for game in saved.get("games", [])
            if game.get("report")
        ]
        if any(not report.is_relative_to(run_root) for report in reports):
            raise ValueError("Development report is outside the private run folder")
        result = summarize(manifest, reports)
        if result["pack"] is None or result["pack"]["Sha256"].lower() != digest:
            raise ValueError("Development report does not match its pack")
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
        row.update(pack=str(pack), scores=[game["score"] for game in result["games"]])
        rows.append(row)
    method = study.get("selection_method")
    if method not in (None, "capped_mean_v1"):
        raise ValueError("Unknown selection method")
    winner = choose(rows, resilient=method == "capped_mean_v1")
    if (
        winner is None
        or winner["pack"] != (study.get("selected_for_development") or {}).get("pack")
        or winner["mean_score"] < 50
    ):
        raise ValueError("Complete development results do not qualify for validation")
    return winner


def resume_validation(
    workspace, application, directory, *, minutes=240, runner=run_benchmark
):
    workspace = Path(workspace).resolve(strict=True)
    application = Path(application).resolve(strict=True)
    directory = Path(directory).resolve(strict=True)
    root = (workspace / ".local/dodge-curricula").resolve(strict=True)
    if directory == root or not directory.is_relative_to(root):
        raise ValueError("Curriculum must be inside this workspace")
    if (
        type(minutes) is not int
        or not 210 <= minutes <= 300
        or minutes * 60 < 10 * GAME_TIMEOUT_SECONDS
    ):
        raise ValueError("Choose a 210–300 minute validation budget")
    path = directory / "curriculum.json"
    with exclusive_study(workspace):
        status = json.loads(path.read_text(encoding="utf-8"))
        if (
            status.get("status") != "budget_before_validation"
            or (directory / "STOP").exists()
            or (directory / "validation").exists()
        ):
            raise ValueError("Only a deferred, never-started validation can be resumed")
        winner = verify_development(workspace, application, status)
        manifest_path = directory / "validation-manifest.json"
        if hashlib.sha256(manifest_path.read_bytes()).hexdigest() != status.get(
            "validation_manifest_sha256"
        ):
            raise ValueError("Reserved validation manifest changed")
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        seeds = manifest.get("seeds")
        profile = status.get("development_profile", "v1")
        if (
            profile not in PROFILES
            or manifest.get("schema_version") != 1
            or manifest.get("target_mean_score") != 50
            or manifest.get("mode") != "stepped"
            or manifest.get("speed") != 0.25
            or not isinstance(seeds, list)
            or len(seeds) != 10
            or any(type(seed) is not int or seed < 1 for seed in seeds)
            or len(set(seeds)) != 10
            or not set(seeds).isdisjoint(DEVELOPMENT_SEEDS | LEGACY_VALIDATION_SEEDS)
        ):
            raise ValueError("Reserved validation conditions changed")
        registry = json.loads((workspace / ".local/dodge-holdouts/registry.json").read_text(encoding="utf-8"))
        if registry.get("schema_version") != 1 or not set(seeds).issubset(registry.get("used_seeds", [])):
            raise ValueError("Reserved validation seeds are missing from the registry")
        output = directory / "validation"
        status["status"] = "validation_running"
        write_status(path, status)
        try:
            result = runner(
                workspace,
                application,
                Path(winner["pack"]),
                manifest,
                output,
                deadline=time.monotonic() + minutes * 60,
                stop_requested=lambda: (directory / "STOP").exists(),
            )
            status["validation"] = str(output)
            status["validation_mean"] = result["mean_score"]
            status["status"] = (
                "validation_incomplete"
                if result["mean_score"] is None
                else (
                    "slow_validation_met"
                    if result["mean_score"] >= 50
                    else "slow_validation_below_target"
                )
            )
        except BaseException as exc:
            status["status"] = (
                "validation_interrupted"
                if isinstance(exc, KeyboardInterrupt)
                else "validation_error"
            )
            status["error"] = f"{type(exc).__name__}: {exc}"
            raise
        finally:
            write_status(path, status)
    return status


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workspace", type=Path)
    parser.add_argument("application", type=Path)
    parser.add_argument("curriculum", type=Path)
    parser.add_argument("--minutes", type=int, default=240)
    args = parser.parse_args()
    status = resume_validation(
        args.workspace, args.application, args.curriculum, minutes=args.minutes
    )
    print(status["status"], flush=True)


if __name__ == "__main__":
    main()
