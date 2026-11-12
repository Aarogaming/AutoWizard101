"""Create a private-test Dodge pack containing an observational outcome model.

This creates an unqualified draft; it never installs or runs the model.
"""

import argparse
from copy import deepcopy
import hashlib
import json
import math
from pathlib import Path

from game_packs import load_pack, save_pack
from train_dodge_survival import CONTEXTUAL_FEATURE_VERSION


def validate(model):
    if (
        not isinstance(model, dict)
        or model.get("schema_version") != 1
        or model.get("feature_version")
        not in (
            CONTEXTUAL_FEATURE_VERSION + "-with-hold",
            CONTEXTUAL_FEATURE_VERSION + "-with-hold-speed400",
        )
        or model.get("algorithm")
        not in (
            "binary-logistic-observational-survival",
            "linear-observational-action-advantage",
            "linear-observational-episode-advantage",
        )
        or (
            model.get("horizon_ms") != 0
            if model.get("algorithm") == "linear-observational-episode-advantage"
            else model.get("horizon_ms") not in (1000, 2000, 5000)
        )
    ):
        raise ValueError("Unsupported outcome model")
    expected_speed = 400 if model["feature_version"].endswith("-speed400") else 190
    if model.get("feature_player_speed", 190) != expected_speed:
        raise ValueError("Outcome model speed does not match its feature version")
    if (
        model["algorithm"]
        in (
            "linear-observational-action-advantage",
            "linear-observational-episode-advantage",
        )
        and expected_speed != 400
    ):
        raise ValueError(
            "Action advantage models require the calibrated feature version"
        )
    means, scales, weights = (model.get(k) for k in ("means", "scales", "weights"))
    if not all(isinstance(v, list) for v in (means, scales, weights)) or (
        len(means),
        len(scales),
        len(weights),
    ) != (71, 71, 72):
        raise ValueError("Invalid outcome model dimensions")
    if any(
        type(x) not in (int, float) or not math.isfinite(x) or abs(x) > 10000
        for x in means + scales + weights
    ) or any(x < 0.000001 for x in scales):
        raise ValueError("Invalid outcome model values")
    if model["algorithm"] in (
        "linear-observational-action-advantage",
        "linear-observational-episode-advantage",
    ) and (
        any(value != 0 for value in means)
        or any(value != 1 for value in scales)
        or any(value != 0 for value in weights[:52])
        or weights[-1] != 0
    ):
        raise ValueError("Invalid action-contrast model representation")
    return model


def build(
    workspace,
    source,
    model_directory,
    library,
    *,
    turn_aware=False,
    retain_reference=False,
    early_escape=False,
):
    workspace = Path(workspace).resolve(strict=True)
    source = Path(source).resolve(strict=True)
    model_directory = Path(model_directory).resolve(strict=True)
    library = Path(library).resolve()
    roots = [
        (workspace / ".local" / name).resolve()
        for name in ("dodge-survival-models", "dodge-episode-models")
    ]
    if (
        any(not root.is_relative_to(workspace) for root in roots)
        or not any(
            model_directory != root and model_directory.is_relative_to(root)
            for root in roots
        )
        or not library.is_relative_to(workspace)
    ):
        raise ValueError(
            "Choose a local outcome model and a library inside this workspace"
        )
    episode = model_directory.is_relative_to(roots[1])
    model_path = model_directory / ("runtime_model.json" if episode else "model.json")
    report_path = model_directory / (
        "training_report.json" if episode else "report.json"
    )
    if model_path.stat().st_size > 200_000 or report_path.stat().st_size > 1_000_000:
        raise ValueError("Outcome model or report exceeds size limits")
    raw, report_bytes = model_path.read_bytes(), report_path.read_bytes()
    model, report = validate(json.loads(raw)), json.loads(report_bytes)
    if episode != (model["algorithm"] == "linear-observational-episode-advantage") or (
        episode and report.get("status") != "research-only"
    ):
        raise ValueError("Outcome model type does not match its research report")
    digest = hashlib.sha256(raw).hexdigest()
    if report.get("runtime_model_sha256" if episode else "model_sha256") != digest:
        raise ValueError("Outcome model does not match its report")
    teachers = (report.get("provenance") or {}).get("recordings") or []
    teacher_hashes = [
        (row.get("pack") or {}).get("Sha256", "").lower() for row in teachers
    ]
    if episode:
        teacher_hashes.append(
            ((report.get("provenance") or {}).get("pack") or {})
            .get("Sha256", "")
            .lower()
        )
    if hashlib.sha256(source.read_bytes()).hexdigest() not in teacher_hashes:
        raise ValueError("Source pack was not among the recorded policies")
    pack = deepcopy(load_pack(source))
    if pack.get("game_id") != "godot-dodge-the-creeps" or "context" not in pack:
        raise ValueError("Choose a context-aware Dodge source pack")
    if model.get("feature_player_speed", 190) == 400:
        constraints = pack["context"]["constraints"]
        required = {
            "player_speed_px_s": 400,
            "player_radius_px": 34,
            "threat_radius_px": 50,
            "max_threat_speed_px_s": 350,
        }
        if any(constraints.get(key) != value for key, value in required.items()):
            raise ValueError(
                "Outcome model calibration does not match its game context"
            )
    pack.pop("experimental_model", None)
    pack.pop("experimental_model_provenance", None)
    pack.pop("exploration_provenance", None)
    prefix = (
        "0.16.0-"
        if early_escape
        else "0.15.0-" if retain_reference else "0.14.0-" if turn_aware else "0.12.0-"
    )
    pack["version"] = prefix + digest[:12]
    pack["title"] = "Dodge outcome policy research prototype"
    pack["context"]["avoidance_policy"] = (
        "directional-outcome-escape-v38"
        if early_escape
        else (
            "directional-outcome-reference-v37"
            if retain_reference
            else (
                "directional-outcome-turn-v36"
                if turn_aware
                else "directional-outcome-v34"
            )
        )
    )
    pack["context"]["constraints"].pop("risk_weight_percent", None)
    pack["context"]["constraints"].pop("edge_weight_percent", None)
    pack["experimental_outcome_model"] = model
    pack["experimental_outcome_provenance"] = {
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
    parser.add_argument(
        "--turn-aware",
        action="store_true",
        help="Use the experimental two-phase fallback when no immediate option clears the guard",
    )
    parser.add_argument(
        "--retain-reference",
        action="store_true",
        help="Keep the directional reference when its visual clearance is acceptable; use turn-aware fallback otherwise",
    )
    parser.add_argument(
        "--early-escape",
        action="store_true",
        help="Consider a two-phase escape when three or fewer immediate options remain",
    )
    args = parser.parse_args()
    print(
        build(
            args.workspace,
            args.source,
            args.model_directory,
            args.library,
            turn_aware=args.turn_aware,
            retain_reference=args.retain_reference,
            early_escape=args.early_escape,
        )
    )


if __name__ == "__main__":
    main()
