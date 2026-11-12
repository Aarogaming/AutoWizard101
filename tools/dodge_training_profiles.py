"""Declared development openings; previously inspected validation is training data."""

from __future__ import annotations

import json
from pathlib import Path

PROFILES = {
    "v1": {
        "file": "dodge-benchmark-development.json",
        "seeds": [118, 247, 781, 997],
        "focus": [247, 781],
    },
    "v2": {
        "file": "dodge-benchmark-development-v2.json",
        "seeds": [118, 247, 781, 997, 26871, 99316, 90595, 32261],
        "focus": [247, 781, 99316, 90595],
    },
}


def load_profile(workspace, profile, scope):
    if profile not in PROFILES or scope not in ("focus", "development"):
        raise ValueError("Unknown Dodge development profile")
    spec = PROFILES[profile]
    manifest = json.loads(
        (Path(workspace) / "tools/fixtures" / spec["file"]).read_text(encoding="utf-8")
    )
    if (
        manifest.get("schema_version") != 1
        or manifest.get("speed") != 0.25
        or manifest.get("mode") != "stepped"
        or manifest.get("seeds") != spec["seeds"]
        or manifest.get("target_mean_score") != 50
    ):
        raise ValueError("The declared slow development scenarios changed")
    if scope == "focus":
        manifest = dict(
            manifest,
            id=f"dodge-focus-development-{profile}-25pct",
            seeds=spec["focus"],
            purpose="Focused development only; not independent validation.",
        )
    return manifest
