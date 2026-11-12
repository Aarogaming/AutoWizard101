"""Resume a deferred, never-started final outcome-policy validation."""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import time

from compare_dodge_benchmarks import compare
from analyze_dodge_distribution import distribution
from game_packs import load_pack
from run_dodge_benchmark import GAME_TIMEOUT_SECONDS, fingerprints, run_benchmark
from train_dodge_offline import exclusive_study, write_status


def verify_qualified(workspace, directory, status):
    """Recheck complete development evidence and untouched final seeds."""
    workspace, directory = Path(workspace).resolve(strict=True), Path(
        directory
    ).resolve(strict=True)
    application = Path(status["comparison_application"]).resolve(strict=True)
    baseline_pack = Path(status["baseline_pack"]).resolve(strict=True)
    rows = status.get("rounds") or []
    eligible = [
        row
        for row in rows
        if row.get("status") == "compared"
        and type(row.get("candidate_mean")) in (int, float)
        and row["candidate_mean"] >= 50
    ]
    if not eligible:
        raise ValueError("No completed candidate qualifies for final validation")
    row = eligible[-1]
    folder = directory / f"round-{row['round']}"
    candidate_pack = Path(row["candidate_pack"]).resolve(strict=True)
    if not candidate_pack.is_relative_to(
        folder / "packs"
    ) or not baseline_pack.is_relative_to(workspace):
        raise ValueError("Pack is outside the recorded study")
    candidate_dir = Path(
        row.get("candidate_benchmark")
        or folder
        / (
            "turn-candidate"
            if (folder / "turn-candidate").is_dir()
            and load_pack(candidate_pack)["context"]["avoidance_policy"]
            == "directional-outcome-turn-v36"
            else "candidate"
        )
    ).resolve(strict=True)
    baseline_dir = Path(row.get("baseline_benchmark") or folder / "baseline").resolve(
        strict=True
    )
    if not candidate_dir.is_relative_to(folder) or not baseline_dir.is_relative_to(
        folder
    ):
        raise ValueError("Development evidence is outside the recorded round")
    run_root = (workspace / ".local/wsl-dodge-runs").resolve(strict=True)
    for benchmark, pack in (
        (candidate_dir, candidate_pack),
        (baseline_dir, baseline_pack),
    ):
        saved = json.loads((benchmark / "summary.json").read_text(encoding="utf-8"))
        if any(
            not Path(game["report"]).resolve(strict=True).is_relative_to(run_root)
            for game in saved["games"]
            if game.get("report")
        ):
            raise ValueError("A run report is outside the private evidence folder")
        if fingerprints(workspace, application, pack) != json.loads(
            (benchmark / "fingerprints.json").read_text(encoding="utf-8")
        ):
            raise ValueError(
                "Application, game, bridge or pack changed after development"
            )
    comparison = compare(baseline_dir, candidate_dir)
    if (
        comparison["candidate"]["mean_score"] < 50
        or comparison["candidate"]["mean_score"] != row["candidate_mean"]
    ):
        raise ValueError("Rechecked candidate does not meet its recorded target")
    model_dir = Path(row["selected_model"]).resolve(strict=True)
    model_root = (workspace / ".local/dodge-survival-models").resolve(strict=True)
    if model_dir == model_root or not model_dir.is_relative_to(model_root):
        raise ValueError("Model is outside the local outcome-model folder")
    model_raw, report_raw = (model_dir / "model.json").read_bytes(), (
        model_dir / "report.json"
    ).read_bytes()
    if len(model_raw) > 200_000 or len(report_raw) > 1_000_000:
        raise ValueError("Model evidence exceeds its size limit")
    model, training = json.loads(model_raw), json.loads(report_raw)
    pack = load_pack(candidate_pack)
    provenance = pack.get("experimental_outcome_provenance") or {}
    if (
        model != pack.get("experimental_outcome_model")
        or hashlib.sha256(model_raw).hexdigest() != training.get("model_sha256")
        or hashlib.sha256(model_raw).hexdigest() != provenance.get("model_sha256")
        or hashlib.sha256(report_raw).hexdigest()
        != provenance.get("training_report_sha256")
    ):
        raise ValueError("Candidate model or training evidence changed")
    final_path = directory / "final/validation-manifest.json"
    if hashlib.sha256(final_path.read_bytes()).hexdigest() != status.get(
        "final_manifest_sha256"
    ):
        raise ValueError("Reserved final manifest changed")
    manifest = json.loads(final_path.read_text(encoding="utf-8"))
    seeds = manifest.get("seeds")
    if (
        manifest.get("schema_version") != 1
        or manifest.get("mode") != "stepped"
        or manifest.get("speed") != 0.25
        or manifest.get("target_mean_score") != 50
        or not isinstance(seeds, list)
        or len(seeds) != 10
        or len(set(seeds)) != 10
        or any(
            type(seed) is not int or not 1 <= seed <= 2_147_483_646 for seed in seeds
        )
    ):
        raise ValueError("Invalid reserved final set")
    used = set(
        json.loads((candidate_dir / "manifest.json").read_text(encoding="utf-8"))[
            "seeds"
        ]
    )
    for recording in (training.get("provenance") or {}).get("recordings") or []:
        used.update(recording["manifest"]["seeds"])
    if not set(seeds).isdisjoint(used):
        raise ValueError("Final seeds overlap model training or development")
    registry = json.loads(
        (workspace / ".local/dodge-holdouts/registry.json").read_text(encoding="utf-8")
    )
    if registry.get("schema_version") != 1 or not set(seeds).issubset(
        registry.get("used_seeds", [])
    ):
        raise ValueError("Reserved seeds are missing from the registry")
    return application, candidate_pack, folder, manifest


def resume(workspace, directory, *, minutes=240, disk_limit_mb=2048):
    workspace, directory = Path(workspace).resolve(strict=True), Path(
        directory
    ).resolve(strict=True)
    root = (workspace / ".local/dodge-curricula").resolve(strict=True)
    if directory == root or not directory.is_relative_to(root):
        raise ValueError("Choose a curriculum inside the workspace")
    if (
        type(minutes) is not int
        or not 210 <= minutes <= 300
        or minutes * 60 < 10 * GAME_TIMEOUT_SECONDS
        or type(disk_limit_mb) is not int
        or not 256 <= disk_limit_mb <= 16384
    ):
        raise ValueError("Choose a bounded validation time and disk budget")
    path = directory / "curriculum.json"
    with exclusive_study(workspace):
        status = json.loads(path.read_text(encoding="utf-8"))
        if (
            status.get("status") != "budget_or_stop_before_final"
            or status.get("final") is not None
            or (directory / "STOP").exists()
        ):
            raise ValueError("Only a deferred, never-started validation can be resumed")
        application, pack, folder, manifest = verify_qualified(
            workspace, directory, status
        )
        output = folder / "final"
        if output.exists():
            raise ValueError("Final validation has already started")
        root_runs = workspace / ".local/wsl-dodge-runs"
        existing = (
            {p.name for p in root_runs.iterdir() if p.is_dir()}
            if root_runs.is_dir()
            else set()
        )

        def stopped():
            if (directory / "STOP").exists() or shutil.disk_usage(
                workspace
            ).free < 2 * 1024**3:
                return True
            total = 0
            for run in root_runs.iterdir():
                if not run.is_dir() or run.is_symlink() or run.name in existing:
                    continue
                for item in run.rglob("*"):
                    if item.is_file() and not item.is_symlink():
                        total += item.stat().st_size
                        if total >= disk_limit_mb * 1024**2:
                            return True
            return False

        status["status"] = "final_running"
        write_status(path, status)
        try:
            result = run_benchmark(
                workspace,
                application,
                pack,
                manifest,
                output,
                deadline=time.monotonic() + minutes * 60,
                stop_requested=stopped,
            )
            status["final"] = {
                "benchmark": str(output),
                "mean_score": result["mean_score"],
                "scores": [game["score"] for game in result["games"]],
            }
            if result["mean_score"] is not None:
                status["final"]["distribution"] = distribution(
                    status["final"]["scores"]
                )
            status["status"] = (
                "final_incomplete"
                if result["mean_score"] is None
                else (
                    "final_target_met"
                    if result["mean_score"] >= 50
                    else "final_below_target"
                )
            )
            write_status(path, status)
            return status
        except Exception as error:
            status["status"], status["error"] = (
                "final_incomplete",
                f"{type(error).__name__}: {error}",
            )
            write_status(path, status)
            raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workspace", type=Path)
    parser.add_argument("curriculum", type=Path)
    parser.add_argument("--minutes", type=int, default=240)
    parser.add_argument("--disk-limit-mb", type=int, default=2048)
    args = parser.parse_args()
    print(
        json.dumps(
            resume(
                args.workspace,
                args.curriculum,
                minutes=args.minutes,
                disk_limit_mb=args.disk_limit_mb,
            ),
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
