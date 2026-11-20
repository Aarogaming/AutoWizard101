"""Load verified private Dodge games into whole-game episode splits offline.

This research loader is not a game controller. It takes the same study lock as
collection and reads audit files only after a private study has stopped.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from dodge_audit_integrity import verify_audit_digest
from dodge_episode_transitions import build_transitions
from train_dodge_imitation import load_examples
from train_dodge_offline import exclusive_study


def load_episode_dataset(workspace, benchmark, held_out_seeds):
    """Validate a complete benchmark and keep each game in exactly one split.

    Report hashes are checked against the already verified provenance. Audit
    hashes document the bytes used, but existing reports did not seal those
    hashes at collection time; this is not a tamper-proof attestation.
    """
    workspace = Path(workspace).resolve(strict=True)
    with exclusive_study(workspace):
        return _load_locked(workspace, benchmark, held_out_seeds)


def _load_locked(workspace, benchmark, held_out_seeds):
    _, _, provenance = load_examples(
        workspace, benchmark, held_out_seeds, include_holds=True
    )
    train, held_out, audit_hashes = [], [], []
    total_audit_bytes = 0
    for item in provenance["reports"]:
        path = Path(item["report"])
        raw = path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != item["sha256"]:
            raise ValueError("Report changed after validation")
        audit_path = path.parent / "evaluation-audit.jsonl"
        if audit_path.is_symlink() or not audit_path.is_file():
            raise ValueError("Missing or unsafe episode audit")
        size = audit_path.stat().st_size
        total_audit_bytes += size
        if size > 10_000_000 or total_audit_bytes > 64_000_000:
            raise ValueError("Episode audits exceed the offline resource limit")
        audit_raw = audit_path.read_bytes()
        if len(audit_raw) != size:
            raise ValueError("Episode audit changed during reading")
        try:
            report = json.loads(raw)
            actual_audit_hash, sealed_at_capture = verify_audit_digest(report, audit_raw)
            audit = [
                json.loads(line) for line in audit_raw.splitlines() if line.strip()
            ]
            episode = build_transitions(report, audit)
        except (json.JSONDecodeError, UnicodeError, ValueError) as exc:
            raise ValueError(f"Invalid episode for seed {item['seed']}: {exc}") from exc
        (held_out if item["split"] == "test" else train).append(episode)
        audit_hashes.append(
            {
                "seed": item["seed"],
                "sha256": actual_audit_hash,
                "sealed_at_capture": sealed_at_capture,
                "split": item["split"],
            }
        )
    if not train or not held_out:
        raise ValueError("Both whole-game episode splits are required")
    return train, held_out, {**provenance, "audit_hashes_at_load": audit_hashes}
