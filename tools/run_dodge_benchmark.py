"""Run a predeclared Dodge benchmark sequentially in the private WSL display."""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import time
import uuid

from benchmark_dodge_scores import summarize


def fingerprints(workspace, application, pack):
    game = workspace / ".local" / "wsl-dodge-step-game"
    files = [
        application,
        application.with_suffix(".dll"),
        pack,
        *(
            workspace / "tools" / name
            for name in (
                "wsl_dodge_launch.sh",
                "wsl_dodge_session.sh",
                "wsl_dodge_guest.py",
                "run_dodge_benchmark.py",
                "benchmark_dodge_scores.py",
            )
        ),
        *(
            game / name
            for name in (
                "project.godot",
                "main.gd",
                "main.tscn",
                "player.gd",
                "player.tscn",
                "mob.gd",
                "mob.tscn",
                "hud.gd",
                "hud.tscn",
                "maelstrom_step_controller.gd",
            )
        ),
        *(
            path
            for folder in ("art", "fonts")
            for path in sorted((game / folder).rglob("*"))
            if path.is_file()
        ),
    ]
    return {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in files}


GAME_TIMEOUT_SECONDS = 1245


def run_benchmark(
    workspace,
    application,
    pack,
    manifest,
    output,
    *,
    deadline=None,
    stop_requested=None,
):
    """Run once; an incomplete or interrupted game is never replaced silently."""
    workspace = Path(workspace).resolve(strict=True)
    application = Path(application).resolve(strict=True)
    pack = Path(pack).resolve(strict=True)
    summarize(manifest, [])  # Validate the complete plan before launching a game.
    runs = workspace / ".local" / "wsl-dodge-runs"
    runs.mkdir(parents=True, exist_ok=True)
    output = Path(output)
    output.mkdir(parents=True, exist_ok=False)
    (output / "manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    expected_files = fingerprints(workspace, application, pack)
    (output / "fingerprints.json").write_text(
        json.dumps(expected_files, indent=2) + "\n", encoding="utf-8"
    )
    reports = []
    summary = summarize(manifest, reports)
    (output / "summary.json").write_text(
        json.dumps(summary, indent=2) + "\n", encoding="utf-8"
    )
    for seed in manifest["seeds"]:
        # Never launch a game unless its entire bounded runtime fits the budget.
        if (stop_requested is not None and stop_requested()) or (
            deadline is not None and time.monotonic() + GAME_TIMEOUT_SECONDS > deadline
        ):
            break
        if fingerprints(workspace, application, pack) != expected_files:
            raise RuntimeError(
                f"Benchmark files changed before the next game; prior summary={output}"
            )
        before = {item.name for item in runs.iterdir() if item.is_dir()}
        result = subprocess.run(
            [
                str(application),
                "--benchmark-wsl-dodge",
                str(workspace),
                str(pack),
                str(seed),
                str(manifest["speed"]),
            ],
            timeout=GAME_TIMEOUT_SECONDS,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
        if fingerprints(workspace, application, pack) != expected_files:
            raise RuntimeError(
                f"Benchmark files changed during a game; prior summary={output}"
            )
        created = [
            item
            for item in runs.iterdir()
            if item.is_dir()
            and item.name not in before
            and (item / "report.json").is_file()
        ]
        if result.returncode or len(created) != 1:
            raise RuntimeError(
                f"Private run failed or multiple runs appeared: exit={result.returncode}; reports={len(created)}; prior summary={output}"
            )
        path = created[0] / "report.json"
        reports.append(path)
        summary = summarize(manifest, reports)
        (output / "summary.json").write_text(
            json.dumps(summary, indent=2) + "\n", encoding="utf-8"
        )
        row = next(game for game in summary["games"] if game["seed"] == seed)
        print(json.dumps(row), flush=True)
        if row["status"] != "complete":
            break
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workspace", type=Path)
    parser.add_argument("application", type=Path)
    parser.add_argument("pack", type=Path)
    parser.add_argument("manifest", type=Path)
    args = parser.parse_args()
    output = (
        args.workspace.resolve(strict=True)
        / ".local"
        / "dodge-benchmarks"
        / uuid.uuid4().hex
    )
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    run_benchmark(args.workspace, args.application, args.pack, manifest, output)
    print("Benchmark summary:", output / "summary.json", flush=True)


if __name__ == "__main__":
    main()
