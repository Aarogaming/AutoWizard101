"""Fit a small observational action model from complete visual episodes.

This experimental in-memory trainer is not connected to live play. Callers must
validate provenance and split whole games before passing episodes. It learns
associations with the collector's later behavior, not causal action values.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import shutil
import statistics
import tempfile
import uuid
from pathlib import Path

from analyze_dodge_action_support import eligible_actions
from dodge_episode_dataset import _load_locked
from dodge_episode_transitions import discounted_returns
from train_dodge_action_advantage import DIMENSIONS, centered_action_features, solve
from train_dodge_imitation import ACTIONS_WITH_HOLD
from train_dodge_offline import exclusive_study
from train_dodge_survival import CONTEXTUAL_FEATURE_VERSION


def randomized_examples(episode, discount_per_second):
    """Select verified random-branch decisions with observed complete returns."""
    returns = discounted_returns(episode, discount_per_second=discount_per_second)
    if not episode[-1]["terminated"]:
        return []
    if episode[-1]["reward"] >= 0 or any(row["terminated"] for row in episode[:-1]):
        raise ValueError("Invalid terminal reward or episode boundary")
    examples = []
    for row, outcome in zip(episode, returns):
        choice = row.get("exploration")
        if outcome is None or choice is None:
            continue
        if not isinstance(choice, dict) or type(choice.get("Explored")) is not bool:
            raise ValueError("Invalid randomized action evidence")
        if not choice["Explored"]:
            continue
        count, probability, action = (
            choice.get("EligibleCount"),
            choice.get("Probability"),
            row.get("action"),
        )
        if (
            type(count) is not int
            or not 1 <= count <= 9
            or type(probability) not in (int, float)
            or not math.isfinite(probability)
            or not any(
                abs(probability - value) < 1e-10
                for value in (0.2 / count, 0.2 / count + 0.8)
            )
            or action not in ACTIONS_WITH_HOLD
            or not isinstance(row.get("state"), list)
            or not isinstance(choice.get("Advice"), str)
            or (
                "hold"
                if choice["Advice"].lower() == "hold"
                else "move-" + choice["Advice"].lower()
            )
            != action
        ):
            raise ValueError("Invalid randomized action evidence")
        choices = eligible_actions(row["state"])
        index = ACTIONS_WITH_HOLD.index(action)
        if len(choices) != count or index not in choices:
            raise ValueError("Randomized action set does not match the visible state")
        examples.append((row["state"], index, choices, max(-10.0, min(60.0, outcome))))
    return examples


def fit_episode_advantage(episodes, *, ridge=1.0, discount_per_second=0.99):
    """Fit centered action features to bounded, complete behavior returns.

    Each usable game contributes the same total sample weight. Only decisions
    recorded as uniformly randomized exploratory choices are admitted. Returns
    are clipped to [-10, 60] and scaled by 60; they are not probabilities.
    """
    if (
        not isinstance(episodes, list)
        or not 2 <= len(episodes) <= 32
        or any(not isinstance(episode, list) for episode in episodes)
        or sum(len(episode) for episode in episodes) > 20000
        or type(ridge) not in (int, float)
        or not math.isfinite(ridge)
        or not 0.01 <= ridge <= 1000
    ):
        raise ValueError("Choose bounded episodes and ridge regularization")
    games = []
    for episode in episodes:
        examples = []
        for state, action, _, outcome in randomized_examples(
            episode, discount_per_second
        ):
            one_hot = [int(index == action) for index in range(len(ACTIONS_WITH_HOLD))]
            features = centered_action_features(state + one_hot)
            examples.append((features, outcome / 60.0))
        if examples:
            games.append(examples)
    if len(games) < 2 or sum(len(game) for game in games) > 5000:
        raise ValueError(
            "Need bounded randomized observations in at least two complete games"
        )

    # Centered features have zero mean over the collector's eligible actions;
    # this does not eliminate confounding from the subsequent behavior policy.
    matrix = [
        [float(i == j) * ridge for j in range(DIMENSIONS)] for i in range(DIMENSIONS)
    ]
    vector = [0.0] * DIMENSIONS
    for game in games:
        weight = 1.0 / len(game)
        for features, outcome in game:
            for i in range(DIMENSIONS):
                vector[i] += weight * features[i] * outcome
                for j in range(DIMENSIONS):
                    matrix[i][j] += weight * features[i] * features[j]
    coefficients = solve(matrix, vector)
    if any(not math.isfinite(value) or abs(value) > 10000 for value in coefficients):
        raise ValueError("Invalid learned coefficients")
    return {
        "algorithm": "experimental-observational-episode-advantage",
        "coefficients": coefficients,
        "games": len(games),
        "randomized_observations": sum(len(game) for game in games),
        "discount_per_second": discount_per_second,
        "return_clip": [-10.0, 60.0],
        "limitations": "Recorded behavior returns include later actions; scores are not probabilities or causal counterfactuals. No deployment or fresh-game validation is implied.",
    }


def assess_episode_advantage(model, episodes):
    """Describe action-match support on separate recorded whole games."""
    if (
        not isinstance(model, dict)
        or model.get("algorithm") != "experimental-observational-episode-advantage"
        or not isinstance(model.get("coefficients"), list)
        or len(model["coefficients"]) != DIMENSIONS
        or any(
            type(v) not in (int, float) or not math.isfinite(v)
            for v in model["coefficients"]
        )
        or not isinstance(episodes, list)
        or not 1 <= len(episodes) <= 32
        or any(not isinstance(episode, list) for episode in episodes)
        or sum(len(episode) for episode in episodes) > 20000
    ):
        raise ValueError("Invalid held-out episode assessment")
    all_returns, matched_returns, match_weights, matched_games = [], [], [], 0
    for episode in episodes:
        game_matches = 0
        for state, actual, choices, outcome in randomized_examples(
            episode, model["discount_per_second"]
        ):

            def score(action):
                one_hot = [int(i == action) for i in range(len(ACTIONS_WITH_HOLD))]
                values = centered_action_features(state + one_hot)
                return sum(
                    value * weight
                    for value, weight in zip(values, model["coefficients"])
                )

            predicted = max(choices, key=score)
            all_returns.append(outcome)
            if predicted == actual:
                matched_returns.append(outcome)
                match_weights.append(len(choices))
                game_matches += 1
        matched_games += int(game_matches > 0)
    return {
        "held_out_games": len(episodes),
        "randomized_observations": len(all_returns),
        "matching_observations": len(matched_returns),
        "games_with_matches": matched_games,
        "mean_clipped_return_for_matches": (
            statistics.mean(matched_returns) if matched_returns else None
        ),
        "weighted_mean_clipped_return_for_matches": (
            sum(value * weight for value, weight in zip(matched_returns, match_weights))
            / sum(match_weights)
            if match_weights
            else None
        ),
        "effective_matched_observations": (
            sum(match_weights) ** 2 / sum(weight * weight for weight in match_weights)
            if match_weights
            else 0
        ),
        "mean_clipped_return_for_all_random": (
            statistics.mean(all_returns) if all_returns else None
        ),
        "limitations": "Raw action matches omit runtime guards. Observations within games are correlated, later behavior affects returns, and this is not a causal estimate or full-game evaluation.",
    }


def runtime_model(model):
    """Encode the fitted action ranking for a private, draft-only comparison."""
    if (
        not isinstance(model, dict)
        or model.get("algorithm") != "experimental-observational-episode-advantage"
        or not isinstance(model.get("coefficients"), list)
        or len(model["coefficients"]) != DIMENSIONS
        or any(
            type(value) not in (int, float)
            or not math.isfinite(value)
            or abs(value) > 10000
            for value in model["coefficients"]
        )
    ):
        raise ValueError("Invalid episode-advantage model")
    return {
        "schema_version": 1,
        "algorithm": "linear-observational-episode-advantage",
        "feature_version": CONTEXTUAL_FEATURE_VERSION + "-with-hold-speed400",
        "feature_player_speed": 400,
        "horizon_ms": 0,
        "means": [0.0] * 71,
        "scales": [1.0] * 71,
        "weights": [0.0] * 52 + model["coefficients"] + [0.0],
    }


def run_study(workspace, benchmark, *, heldout_games=8):
    """Fit and assess a research model, preserving its exact local evidence."""
    workspace = Path(workspace).resolve(strict=True)
    benchmark = Path(benchmark).resolve(strict=True)
    if type(heldout_games) is not int or not 2 <= heldout_games <= 16:
        raise ValueError("Choose 2–16 whole held-out games")
    source_names = (
        "train_dodge_episode_advantage.py",
        "dodge_episode_dataset.py",
        "dodge_episode_transitions.py",
        "dodge_audit_integrity.py",
        "train_dodge_imitation.py",
        "train_dodge_action_advantage.py",
        "analyze_dodge_action_support.py",
        "train_dodge_survival.py",
        "train_dodge_offline.py",
        "benchmark_dodge_scores.py",
    )
    sources = [Path(__file__).parent / name for name in source_names]

    def source_hashes():
        return {
            path.name: hashlib.sha256(path.read_bytes()).hexdigest() for path in sources
        }

    with exclusive_study(workspace):
        before = source_hashes()
        manifest_path = benchmark / "manifest.json"
        manifest_raw = manifest_path.read_bytes()
        manifest = json.loads(manifest_raw)
        seeds = manifest.get("seeds")
        if not isinstance(seeds, list) or len(seeds) <= heldout_games:
            raise ValueError("Not enough distinct recorded games for the split")
        train, heldout, provenance = _load_locked(
            workspace, benchmark, seeds[-heldout_games:]
        )
        model = fit_episode_advantage(train)
        evaluation = assess_episode_advantage(model, heldout)
        if source_hashes() != before or manifest_path.read_bytes() != manifest_raw:
            raise ValueError("Training source or manifest changed during the study")
        model_raw = (json.dumps(model, indent=2, sort_keys=True) + "\n").encode("utf-8")
        runtime_raw = (
            json.dumps(runtime_model(model), indent=2, sort_keys=True) + "\n"
        ).encode("utf-8")
        report = {
            "schema_version": 1,
            "status": "research-only",
            "model_sha256": hashlib.sha256(model_raw).hexdigest(),
            "runtime_model_sha256": hashlib.sha256(runtime_raw).hexdigest(),
            "manifest_sha256": hashlib.sha256(manifest_raw).hexdigest(),
            "training_games": len(train),
            "held_out_games": len(heldout),
            "training_transitions": sum(map(len, train)),
            "held_out_transitions": sum(map(len, heldout)),
            "held_out_assessment": evaluation,
            "provenance": provenance,
            "source_sha256": before,
            "note": "Recorded-game assessment only. No runtime packaging, game inputs, causal claim or fresh-game qualification.",
        }
        parent = workspace / ".local" / "dodge-episode-models"
        parent.mkdir(parents=True, exist_ok=True)
        if not parent.resolve().is_relative_to(workspace):
            raise ValueError("Research model folder escapes the workspace")
        temporary = Path(tempfile.mkdtemp(prefix=".episode-", dir=parent))
        destination = parent / uuid.uuid4().hex
        try:
            (temporary / "model.json").write_bytes(model_raw)
            (temporary / "runtime_model.json").write_bytes(runtime_raw)
            (temporary / "training_report.json").write_text(
                json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8"
            )
            temporary.replace(destination)
        except Exception:
            shutil.rmtree(temporary, ignore_errors=True)
            raise
    return destination, report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workspace", type=Path)
    parser.add_argument("benchmark", type=Path)
    parser.add_argument("--heldout-games", type=int, default=8)
    args = parser.parse_args()
    output, report = run_study(
        args.workspace, args.benchmark, heldout_games=args.heldout_games
    )
    print(
        json.dumps(
            {
                "output": str(output),
                "status": report["status"],
                "held_out_assessment": report["held_out_assessment"],
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
