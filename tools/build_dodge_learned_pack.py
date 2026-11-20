"""Embed a small offline-trained model in a new experimental Dodge pack.

The resulting draft is for the private, stepped test environment. Building it
does not install it, start a game, or grant Windows-desktop control.
"""

from __future__ import annotations

import argparse
from copy import deepcopy
import hashlib
import json
import math
from pathlib import Path

from game_packs import load_pack, save_pack
from train_dodge_imitation import ACTIONS, ACTIONS_WITH_HOLD, FEATURE_VERSION


def validate_model(model):
    if (
        not isinstance(model, dict)
        or type(model.get("schema_version")) is not int
        or model.get("schema_version") != 1
        or model.get("feature_version") != FEATURE_VERSION
        or model.get("actions") not in (list(ACTIONS), list(ACTIONS_WITH_HOLD))
    ):
        raise ValueError("Unsupported offline model or action registry")
    means, scales, weights = (
        model.get("means"),
        model.get("scales"),
        model.get("weights"),
    )
    if (
        not isinstance(means, list)
        or len(means) != 52
        or not isinstance(scales, list)
        or len(scales) != 52
        or not isinstance(weights, list)
        or len(weights) != len(model["actions"])
        or any(not isinstance(row, list) or len(row) != 53 for row in weights)
    ):
        raise ValueError("Unsupported offline model dimensions")
    values = means + scales + [v for row in weights for v in row]
    if any(
        type(v) not in (int, float) or not math.isfinite(v) or abs(v) > 10000
        for v in values
    ) or any(v < 0.000001 for v in scales):
        raise ValueError("Invalid offline model values")
    return model


def build(workspace, source, model_directory, library):
    workspace = Path(workspace).resolve(strict=True)
    source = Path(source).resolve(strict=True)
    model_directory = Path(model_directory).resolve(strict=True)
    library = Path(library).resolve()
    model_root = (workspace / ".local/dodge-offline-models").resolve(strict=True)
    if (
        not model_directory.is_relative_to(model_root)
        or model_directory == model_root
        or not library.is_relative_to(workspace)
    ):
        raise ValueError(
            "Choose a local offline model and a library inside this workspace"
        )
    model_path = model_directory / "model.json"
    raw = model_path.read_bytes()
    if len(raw) > 200_000:
        raise ValueError("Offline model is too large for a lightweight game pack")
    model = validate_model(json.loads(raw))
    report_path = model_directory / "report.json"
    if report_path.stat().st_size > 1_000_000:
        raise ValueError("Training report is too large")
    report_bytes = report_path.read_bytes()
    report = json.loads(report_bytes)
    if report.get("model_sha256") != hashlib.sha256(raw).hexdigest():
        raise ValueError("The model does not match its training report")
    pack = deepcopy(load_pack(source))
    if pack.get("game_id") != "godot-dodge-the-creeps" or "context" not in pack:
        raise ValueError("Choose a context-aware Dodge source pack")
    teacher = (report.get("provenance") or {}).get("pack") or {}
    if (
        teacher.get("Sha256", "").lower()
        != hashlib.sha256(source.read_bytes()).hexdigest()
    ):
        raise ValueError("The model was trained from a different source pack")
    digest = hashlib.sha256(raw).hexdigest()
    pack["version"] = "0.11.0-" + digest[:12]
    pack["title"] = "Dodge learned policy prototype"
    pack["context"]["avoidance_policy"] = "directional-learned-v33"
    pack["context"]["constraints"].pop("risk_weight_percent", None)
    pack["context"]["constraints"].pop("edge_weight_percent", None)
    pack["experimental_model"] = model
    pack["experimental_model_provenance"] = {
        "model_sha256": digest,
        "training_report_sha256": hashlib.sha256(report_bytes).hexdigest(),
    }
    return save_pack(pack, library, source_directory=source.parent)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workspace", type=Path)
    parser.add_argument("source", type=Path)
    parser.add_argument("model_directory", type=Path)
    parser.add_argument("library", type=Path)
    args = parser.parse_args()
    print(build(args.workspace, args.source, args.model_directory, args.library))


if __name__ == "__main__":
    main()
