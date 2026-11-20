"""Reserve a fresh, disjoint slow-Dodge validation set for an offline study."""

from __future__ import annotations

import json
from pathlib import Path
import secrets

from dodge_training_profiles import PROFILES
from train_dodge_offline import exclusive_study, write_status

DEVELOPMENT_SEEDS = {seed for profile in PROFILES.values() for seed in profile["seeds"]}
# The first predeclared set has already been exposed to validation.
LEGACY_VALIDATION_SEEDS = {
    26871,
    99316,
    29795,
    89248,
    22879,
    63762,
    90595,
    92997,
    32261,
    65532,
}


def reserve_holdout(workspace, curriculum, *, draw=None, count=10):
    """Reserve before tuning; never reuse seeds, even if later steps fail."""
    workspace = Path(workspace).resolve(strict=True)
    if type(count) is not int or not 1 <= count <= 32:
        raise ValueError("Choose 1–32 distinct reserved seeds")
    curriculum = Path(curriculum).resolve(strict=True)
    root = (workspace / ".local/dodge-curricula").resolve(strict=True)
    if curriculum == root or not curriculum.is_relative_to(root):
        raise ValueError("Curriculum must be inside this workspace")
    destination = curriculum / "validation-manifest.json"
    registry_path = workspace / ".local/dodge-holdouts/registry.json"
    draw = draw or (lambda: secrets.randbelow(2_147_483_646) + 1)
    with exclusive_study(workspace):
        if destination.exists():
            raise ValueError("This curriculum already has a reserved holdout")
        if registry_path.exists():
            registry = json.loads(registry_path.read_text(encoding="utf-8"))
            if (
                not isinstance(registry, dict)
                or registry.get("schema_version") != 1
                or not isinstance(registry.get("used_seeds"), list)
                or len(registry["used_seeds"]) > 100_000
                or any(
                    type(seed) is not int or not 1 <= seed <= 2_147_483_646
                    for seed in registry["used_seeds"]
                )
            ):
                raise ValueError("Invalid holdout registry")
        else:
            registry = {"schema_version": 1, "used_seeds": []}
        forbidden = (
            set(registry["used_seeds"]) | DEVELOPMENT_SEEDS | LEGACY_VALIDATION_SEEDS
        )
        selected = []
        for _ in range(10_000):
            seed = draw()
            if type(seed) is not int or not 1 <= seed <= 2_147_483_646:
                raise ValueError("Invalid holdout seed")
            if seed not in forbidden:
                selected.append(seed)
                forbidden.add(seed)
                if len(selected) == count:
                    break
        if len(selected) != count:
            raise ValueError("Could not reserve the requested distinct seeds")
        manifest = {
            "schema_version": 1,
            "id": "dodge-validation-" + curriculum.name,
            "purpose": "Predeclared holdout reserved before this curriculum; never used for tuning.",
            "seeds": selected,
            "mode": "stepped",
            "speed": 0.25,
            "target_mean_score": 50,
        }
        registry["used_seeds"].extend(selected)
        registry_path.parent.mkdir(parents=True, exist_ok=True)
        # Reserve first: a crash can waste seeds but cannot accidentally free them.
        write_status(registry_path, registry)
        write_status(destination, manifest)
    return destination
