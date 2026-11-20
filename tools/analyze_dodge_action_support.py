"""Assess support for raw Dodge model choices from randomized recorded moves.

This offline diagnostic evaluates one randomized action followed by the
recorded collection policy. It does not evaluate the deployed clearance guard,
repeated model control, complete-game score, or real-time play. Never use game
audit data as policy input or run this diagnostic during a game.
"""

import argparse
import hashlib
import json
import math
from pathlib import Path

from train_dodge_imitation import ACTION_VECTORS
from train_dodge_survival import contextual_features, load_survival_examples
from build_dodge_outcome_pack import validate
from game_packs import load_pack
from train_dodge_offline import exclusive_study


def allowed_axis(current, nxt, lower, upper):
    if current < lower:
        return current <= nxt <= upper
    if current > upper:
        return lower <= nxt <= current
    return lower <= nxt <= upper


def eligible_actions(state):
    """Recreate the fixed 480x720 collector's boundary-constrained choices."""
    if len(state) != 52 or any(
        type(value) not in (int, float) or not math.isfinite(value) for value in state
    ):
        raise ValueError("Invalid visual state")
    px, py = state[0] * 480, state[1] * 720
    if not 0 <= px <= 480 or not 0 <= py <= 720:
        raise ValueError("Visual player is outside the viewport")
    choices = []
    for index, (dx, dy) in enumerate((*ACTION_VECTORS, (0.0, 0.0))):
        nx, ny = px + dx * 24, py + dy * 24  # 400 px/s, 60 ms.
        if allowed_axis(px, nx, 25, 455) and allowed_axis(py, ny, 25, 695):
            choices.append(index)
    return choices


def raw_preference(model, state, choices):
    """Score the model before the runtime visual-clearance filter."""
    px, py = state[0] * 480, state[1] * 720
    best, best_score = None, -math.inf
    for action in choices:
        values = contextual_features(state + [int(i == action) for i in range(9)])
        score = model["weights"][-1] + sum(
            weight * max(-5, min(5, (value - mean) / scale))
            for value, mean, scale, weight in zip(
                values, model["means"], model["scales"], model["weights"]
            )
        )
        dx, dy = (*ACTION_VECTORS, (0.0, 0.0))[action]
        x, y = px + dx * 24, py + dy * 24
        edge = min(x, 480 - x, y, 720 - y)
        score -= max(0, 90 - edge) * 0.03
        if score > best_score:
            best, best_score = action, score
    if best is None:
        raise ValueError("No eligible randomized actions")
    return best


def assess(model, groups):
    """Use random-branch action matches, weighting by inverse action probability."""
    validate(model)
    if (
        model.get("feature_player_speed") != 400
        or not groups
        or len(groups) > 600
        or sum(len(examples) for _, examples in groups) > 600
    ):
        raise ValueError("Expected a calibrated model and held-out games")
    observations = matches = matched_games = survival = weighted_survival = 0
    weights = []
    details = []
    for seed, examples in groups:
        hits = 0
        for features, label in examples:
            if len(features) != 61 or label not in (0, 1):
                raise ValueError("Invalid randomized state-action outcome")
            one_hot = features[52:]
            if any(x not in (0, 1) for x in one_hot) or sum(one_hot) != 1:
                raise ValueError("Expected a single recorded action")
            state = features[:52]
            choices = eligible_actions(state)
            recorded = one_hot.index(1)
            if recorded not in choices:
                raise ValueError(
                    "Recorded randomized action is outside the collector boundary"
                )
            preferred = raw_preference(model, state, choices)
            observations += 1
            survival += label
            if preferred == recorded:
                hits += 1
                matches += 1
                weights.append(len(choices))
                weighted_survival += len(choices) * label
        matched_games += hits > 0
        details.append(
            {"seed": seed, "randomized_observations": len(examples), "matches": hits}
        )
    total_weight = sum(weights)
    return {
        "outcome_horizon_ms": model["horizon_ms"],
        "held_out_games": len(groups),
        "randomized_observations": observations,
        "matching_observations": matches,
        "games_with_matches": matched_games,
        "effective_matched_observations": (
            (total_weight**2 / sum(w * w for w in weights)) if weights else 0
        ),
        "weighted_survival_after_matching_raw_choice": (
            weighted_survival / total_weight if weights else None
        ),
        "survival_after_random_action": (
            survival / observations if observations else None
        ),
        "games": details,
        "limitations": "Randomized single-action diagnostic with the collector's subsequent behavior, not a full policy or causal estimate of game score. It describes the sampled, per-game-capped held-out observations. Raw choices omit the deployed clearance and reference guards. Adjacent observations are correlated; low match counts are inconclusive.",
    }


def validate_collector(workspace, benchmark, expected_hash):
    """Verify the exact collector geometry used by the importance weights."""
    fingerprints = json.loads(
        (Path(benchmark) / "fingerprints.json").read_text(encoding="utf-8")
    )
    matches = [
        Path(path)
        for path, digest in fingerprints.items()
        if path.replace("\\", "/").lower().endswith("/pack.json")
        and digest.lower() == expected_hash.lower()
    ]
    if len(matches) != 1:
        raise ValueError("Missing collector pack fingerprint")
    source = matches[0].resolve(strict=True)
    if (
        not source.is_relative_to(workspace)
        or hashlib.sha256(source.read_bytes()).hexdigest() != expected_hash.lower()
    ):
        raise ValueError("Collector pack changed after the recorded game")
    pack = load_pack(source)
    constraints = pack.get("context", {}).get("constraints", {})
    required = {
        "viewport_width": 480,
        "viewport_height": 720,
        "player_speed_px_s": 400,
        "edge_margin_px": 25,
    }
    expected = {
        "move-up",
        "move-down",
        "move-left",
        "move-right",
        "move-up-left",
        "move-up-right",
        "move-down-left",
        "move-down-right",
    }
    actions = pack.get("actions", [])
    if (
        pack.get("context", {}).get("avoidance_policy") != "directional-explore-v35"
        or any(constraints.get(key) != value for key, value in required.items())
        or len(actions) != 8
        or {action.get("id") for action in actions} != expected
        or any(action.get("duration_ms") != 60 for action in actions)
    ):
        raise ValueError(
            "Collector geometry or action timing does not match this diagnostic"
        )


def analyze(workspace, model_directory):
    workspace = Path(workspace).resolve(strict=True)
    model_directory = Path(model_directory).resolve(strict=True)
    model_root = (workspace / ".local/dodge-survival-models").resolve(strict=True)
    if model_directory == model_root or not model_directory.is_relative_to(model_root):
        raise ValueError("Choose a local outcome model")
    model_raw = (model_directory / "model.json").read_bytes()
    report_raw = (model_directory / "report.json").read_bytes()
    if len(model_raw) > 200_000 or len(report_raw) > 1_000_000:
        raise ValueError("Model evidence exceeds its size limit")
    model, report = json.loads(model_raw), json.loads(report_raw)
    validate(model)
    if report.get("model_sha256") != hashlib.sha256(model_raw).hexdigest():
        raise ValueError("Model and training report do not match")
    audits = report["provenance"]["outcome_audits"]
    test_seeds = [row["seed"] for row in audits if row["split"] == "test"]
    train_seeds = {row["seed"] for row in audits if row["split"] == "train"}
    if (
        not test_seeds
        or len(set(test_seeds)) != len(test_seeds)
        or train_seeds.intersection(test_seeds)
    ):
        raise ValueError("Held-out games overlap training or are missing")
    benchmarks = list(dict.fromkeys(row["benchmark"] for row in audits))
    if not benchmarks or len(benchmarks) > 8:
        raise ValueError("Invalid recorded benchmark set")
    with exclusive_study(workspace):
        for row in audits:
            if row["split"] == "test":
                validate_collector(
                    workspace, row["benchmark"], row["teacher_pack_sha256"]
                )
        _, examples, provenance = load_survival_examples(
            workspace,
            benchmarks[0],
            test_seeds,
            benchmarks[1:],
            include_holds=True,
            exploration_only=True,
            player_speed=400,
            horizon_ms=model["horizon_ms"],
        )
        original = {
            row["seed"]: row["sha256"] for row in audits if row["split"] == "test"
        }
        old_reports = {
            row["seed"]: row["sha256"]
            for source in report["provenance"]["recordings"]
            for row in source["reports"]
            if row["split"] == "test"
        }
        current_reports = {
            row["seed"]: row["sha256"]
            for source in provenance["recordings"]
            for row in source["reports"]
            if row["split"] == "test"
        }
        if old_reports != current_reports:
            raise ValueError("Held-out visual recordings changed after training")
        groups, offset = [], 0
        for row in provenance["outcome_audits"]:
            if row["split"] != "test":
                continue
            if original.get(row["seed"]) != row["sha256"]:
                raise ValueError("Held-out outcome audit changed after training")
            count = row["examples"]
            groups.append((row["seed"], examples[offset : offset + count]))
            offset += count
        if offset != len(examples):
            raise ValueError("Held-out game boundaries do not match the examples")
        return assess(model, groups)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workspace", type=Path)
    parser.add_argument("model_directory", type=Path)
    args = parser.parse_args()
    print(json.dumps(analyze(args.workspace, args.model_directory), indent=2))


if __name__ == "__main__":
    main()
