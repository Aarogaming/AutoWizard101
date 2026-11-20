"""Verify a completed focus baseline before reusing it in a later study.

Only the preliminary development screen may reuse this evidence. Full
development and validation games must still be run separately.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from benchmark_dodge_scores import summarize
from run_dodge_benchmark import fingerprints


def verified_baseline(workspace, application, pack, manifest, benchmark):
    """Return a rechecked complete summary or reject changed/missing evidence."""
    workspace = Path(workspace).resolve(strict=True)
    application = Path(application).resolve(strict=True)
    pack = Path(pack).resolve(strict=True)
    benchmark = Path(benchmark).resolve(strict=True)
    study_root = (workspace / ".local/dodge-training").resolve(strict=True)
    run_root = (workspace / ".local/wsl-dodge-runs").resolve(strict=True)
    if not benchmark.is_relative_to(study_root) or benchmark == study_root:
        raise ValueError("Baseline benchmark must belong to a local training study")
    recorded_manifest = json.loads(
        (benchmark / "manifest.json").read_text(encoding="utf-8")
    )
    if recorded_manifest != manifest:
        raise ValueError("Baseline used different openings or conditions")
    recorded_files = json.loads(
        (benchmark / "fingerprints.json").read_text(encoding="utf-8")
    )
    if recorded_files != fingerprints(workspace, application, pack):
        raise ValueError("Baseline game, executable, pack or bridge changed")
    saved = json.loads((benchmark / "summary.json").read_text(encoding="utf-8"))
    reports = []
    for game in saved.get("games", []):
        if not game.get("report"):
            raise ValueError("Baseline is incomplete")
        report = Path(game["report"]).resolve(strict=True)
        if not report.is_relative_to(run_root):
            raise ValueError("Baseline report is outside the private run folder")
        reports.append(report)
    checked = summarize(manifest, reports)
    digest = hashlib.sha256(pack.read_bytes()).hexdigest()
    if (
        checked != saved
        or checked.get("completed_games") != checked.get("expected_games")
        or (checked.get("pack") or {}).get("Sha256", "").lower() != digest
    ):
        raise ValueError("Baseline report evidence does not match its saved summary")
    return checked
