"""Fit a tiny action-contrast model from randomized, completed Dodge games.

This is a contextual, one-action observational experiment, not a long-term
reinforcement-learning agent. It uses only recorded visual features and the
offline survival labels. The subsequent behavior is the collector's policy.
No game or desktop input is started by this tool.
"""

import argparse
import hashlib
import json
import math
from pathlib import Path
import uuid

from analyze_dodge_action_support import assess, eligible_actions, validate_collector
from train_dodge_offline import exclusive_study
from train_dodge_survival import contextual_features, load_survival_examples

DIMENSIONS = 19


def action_features(state, index):
    return contextual_features(state + [int(i == index) for i in range(9)])[52:]


def centered_action_features(features):
    if len(features) != 61 or any(
        type(x) not in (int, float) or not math.isfinite(x) for x in features
    ):
        raise ValueError("Invalid randomized state-action features")
    one_hot = features[52:]
    if any(x not in (0, 1) for x in one_hot) or sum(one_hot) != 1:
        raise ValueError("Expected a single randomized action")
    state, index = features[:52], one_hot.index(1)
    choices = eligible_actions(state)
    if index not in choices:
        raise ValueError("Recorded action is outside the collector boundary")
    chosen = action_features(state, index)
    alternatives = [action_features(state, choice) for choice in choices]
    return [
        value - sum(row[i] for row in alternatives) / len(alternatives)
        for i, value in enumerate(chosen)
    ]


def solve(matrix, vector):
    """Small pivoted linear solve; the ridge term makes the system definite."""
    size = len(vector)
    rows = [list(row) + [value] for row, value in zip(matrix, vector)]
    for column in range(size):
        pivot = max(range(column, size), key=lambda i: abs(rows[i][column]))
        if abs(rows[pivot][column]) < 1e-12:
            raise ValueError("Action model system is singular")
        rows[column], rows[pivot] = rows[pivot], rows[column]
        divisor = rows[column][column]
        for j in range(column, size + 1):
            rows[column][j] /= divisor
        for i in range(column + 1, size):
            factor = rows[i][column]
            for j in range(column, size + 1):
                rows[i][j] -= factor * rows[column][j]
    answer = [0.0] * size
    for i in range(size - 1, -1, -1):
        answer[i] = rows[i][-1] - sum(
            rows[i][j] * answer[j] for j in range(i + 1, size)
        )
    return answer


def fit_action_advantage(examples, *, ridge=1.0, horizon_ms=1000):
    if (
        not examples
        or len(examples) > 5000
        or type(ridge) not in (int, float)
        or not math.isfinite(ridge)
        or not 0.01 <= ridge <= 1000
        or horizon_ms not in (1000, 2000, 5000)
    ):
        raise ValueError("Choose bounded randomized examples and a positive ridge")
    if any(type(y) is not int or y not in (0, 1) for _, y in examples):
        raise ValueError("Invalid observed survival label")
    rate = sum(y for _, y in examples) / len(examples)
    matrix = [
        [float(i == j) * ridge for j in range(DIMENSIONS)] for i in range(DIMENSIONS)
    ]
    vector = [0.0] * DIMENSIONS
    for features, outcome in examples:
        x = centered_action_features(features)
        for i in range(DIMENSIONS):
            vector[i] += x[i] * (outcome - rate)
            for j in range(DIMENSIONS):
                matrix[i][j] += x[i] * x[j]
    weights = solve(matrix, vector)
    if any(not math.isfinite(value) or abs(value) > 10000 for value in weights):
        raise ValueError("Action model weights are invalid")
    # Subtracting the action-set mean is constant across choices in a state.
    # Uncentered action features therefore retain the same preference ordering.
    return {
        "schema_version": 1,
        "feature_version": "dodge-visual-state-action-context-v2-with-hold-speed400",
        "feature_player_speed": 400,
        "algorithm": "linear-observational-action-advantage",
        "horizon_ms": horizon_ms,
        "means": [0.0] * 71,
        "scales": [1.0] * 71,
        "weights": [0.0] * 52 + weights + [0.0],
        "ridge": ridge,
    }


def train(workspace, benchmarks, test_seeds, *, ridge=1.0, horizon_ms=1000):
    workspace = Path(workspace).resolve(strict=True)
    benchmarks = [Path(path).resolve(strict=True) for path in benchmarks]
    if not benchmarks or len(benchmarks) > 8:
        raise ValueError("Choose one to eight recorded benchmarks")
    with exclusive_study(workspace):
        return _train_locked(
            workspace, benchmarks, test_seeds, ridge=ridge, horizon_ms=horizon_ms
        )


def _train_locked(workspace, benchmarks, test_seeds, *, ridge=1.0, horizon_ms=1000):
    """Fit only while the caller owns the exclusive study lock."""
    training, test, provenance = load_survival_examples(
        workspace,
        benchmarks[0],
        test_seeds,
        benchmarks[1:],
        include_holds=True,
        exploration_only=True,
        player_speed=400,
        horizon_ms=horizon_ms,
    )
    checked = set()
    for row in provenance["outcome_audits"]:
        key = (row["benchmark"], row["teacher_pack_sha256"])
        if row["examples"] and key not in checked:
            validate_collector(workspace, *key)
            checked.add(key)
    model = fit_action_advantage(training, ridge=ridge, horizon_ms=horizon_ms)
    groups, offset = [], 0
    for row in provenance["outcome_audits"]:
        if row["split"] == "test":
            count = row["examples"]
            groups.append((row["seed"], test[offset : offset + count]))
            offset += count
    if offset != len(test):
        raise ValueError("Held-out game boundaries do not match examples")
    support = assess(model, groups)
    raw = (json.dumps(model, indent=2) + "\n").encode("utf-8")
    result = {
        "schema_version": 1,
        "purpose": "offline randomized single-action contrast",
        "model_sha256": hashlib.sha256(raw).hexdigest(),
        "trainer_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "feature_extractor_sha256": hashlib.sha256(
            (Path(__file__).parent / "train_dodge_imitation.py").read_bytes()
        ).hexdigest(),
        "outcome_labeler_sha256": hashlib.sha256(
            (Path(__file__).parent / "train_dodge_survival.py").read_bytes()
        ).hexdigest(),
        "audit_verifier_sha256": hashlib.sha256(
            (Path(__file__).parent / "dodge_audit_integrity.py").read_bytes()
        ).hexdigest(),
        "training_examples": len(training),
        "held_out_action_support": support,
        "provenance": provenance,
        "limitations": "Randomized one-action outcomes are followed by the collector's later actions. Scores are not probabilities, and action matching is not a full-game evaluation. An experimental pack requires fresh private games.",
    }
    output = workspace / ".local/dodge-survival-models" / uuid.uuid4().hex
    output.mkdir(parents=True, exist_ok=False)
    (output / "model.json").write_bytes(raw)
    (output / "report.json").write_text(
        json.dumps(result, indent=2) + "\n", encoding="utf-8"
    )
    return output, result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workspace", type=Path)
    parser.add_argument("benchmark", type=Path)
    parser.add_argument("--also-benchmark", type=Path, action="append", default=[])
    parser.add_argument("--test-seeds", type=int, nargs="+", required=True)
    parser.add_argument("--ridge", type=float, default=1.0)
    parser.add_argument(
        "--horizon-ms", type=int, choices=(1000, 2000, 5000), default=1000
    )
    args = parser.parse_args()
    output, report = train(
        args.workspace,
        [args.benchmark, *args.also_benchmark],
        args.test_seeds,
        ridge=args.ridge,
        horizon_ms=args.horizon_ms,
    )
    print(
        json.dumps(
            {
                "output": str(output),
                "training_examples": report["training_examples"],
                "held_out_action_support": report["held_out_action_support"],
                "limitations": report["limitations"],
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
