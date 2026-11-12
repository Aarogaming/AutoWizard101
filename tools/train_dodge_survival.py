"""Fit an offline short-term survival predictor from completed Dodge runs.

This observational model is not a causal action-value model and is not used
for gameplay. Audit files supply only alive/dead timestamps as training labels;
features come from recorded visual observations and the recorded action.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import uuid

from dodge_audit_integrity import verify_audit_digest
from train_dodge_imitation import (
    ACTIONS,
    ACTIONS_WITH_HOLD,
    ACTION_VECTORS,
    MAX_ACTIONS_PER_GAME,
    feature_vector,
    load_examples,
)

HORIZON_MS = 5000
MAX_OUTCOME_TRAIN_EXAMPLES = 5000
MAX_OUTCOME_TEST_EXAMPLES = 600
FEATURE_VERSION = "dodge-visual-state-action-v1"
CONTEXTUAL_FEATURE_VERSION = "dodge-visual-state-action-context-v2"


def sample_game_examples(examples, limit):
    """Cap a timeline while retaining its first and last labeled decisions."""
    if type(limit) is not int or limit < 1:
        raise ValueError("The per-game sample limit must be positive")
    if len(examples) <= limit:
        return examples
    if limit == 1:
        return [examples[-1]]
    last = len(examples) - 1
    return [examples[i * last // (limit - 1)] for i in range(limit)]


def contextual_features(features):
    """Add visual features that depend on the recorded action direction.

    These are associations, not estimates of counterfactual outcomes.
    """
    if len(features) not in (60, 61) or any(not math.isfinite(v) for v in features):
        raise ValueError("Invalid state-action features")
    selected = features[52:]
    if any(value not in (0, 1) for value in selected) or sum(selected) != 1:
        raise ValueError("Expected one recorded movement action")
    index = selected.index(1)
    ax, ay = ACTION_VECTORS[index] if index < len(ACTION_VECTORS) else (0.0, 0.0)
    # A held position has no corresponding movement-clearance estimate.
    clearance = features[20 + 4 * index : 24 + 4 * index] if index < 8 else [0.0] * 4
    result = list(features) + list(clearance)
    for offset in (2, 8, 14):
        result.extend(
            [
                ax * features[offset] + ay * features[offset + 1],
                ax * features[offset + 2] + ay * features[offset + 3],
            ]
        )
    return result


def label_examples(
    report,
    audit,
    horizon_ms=HORIZON_MS,
    *,
    include_holds=False,
    exploration_only=False,
    player_speed=190,
):
    """Label only when capture timestamps bracket the horizon unambiguously."""
    training = report.get("training", {})
    speed, fps = training.get("speed"), training.get("fixed_fps")
    if (
        training.get("mode") != "stepped"
        or speed not in (0.25, 0.5, 1)
        or fps != 30
        or type(horizon_ms) is not int
        or not 1 <= horizon_ms <= 30000
    ):
        raise ValueError("Unsupported recorded timing")
    previous = -1
    last_alive = first_dead = None
    observed_alive = {}
    for row in audit:
        ticks, alive = row.get("ticks"), row.get("alive")
        if type(ticks) is not int or ticks < previous or type(alive) is not bool:
            raise ValueError("Invalid audit timestamps or alive state")
        if ticks in observed_alive and observed_alive[ticks] != alive:
            raise ValueError("Conflicting audit states at one timestamp")
        observed_alive[ticks] = alive
        previous = ticks
        if alive:
            if first_dead is not None:
                raise ValueError("Audit unexpectedly returned to alive")
            last_alive = ticks * 1000 * speed / fps
        elif first_dead is None:
            first_dead = ticks * 1000 * speed / fps
    if last_alive is None or first_dead is None or first_dead <= last_alive:
        raise ValueError("Audit does not bracket an observed game over")
    examples = []
    actions = ACTIONS_WITH_HOLD if include_holds else ACTIONS
    decisions = list(report.get("actions", []))
    if include_holds:
        decisions.extend(
            {
                **decision,
                "action": "hold",
                "outcome": "hold-observed",
                "before_capture_ms": decision.get("capture_ms"),
            }
            for decision in report.get("decisions", [])
            if decision.get("advice") == "Hold"
            and decision.get("action") is None
            and decision.get("observation_fresh") is True
        )
    for action in decisions:
        name, timestamp = action.get("action"), action.get("before_capture_ms")
        exploration = action.get("exploration")
        if exploration_only:
            if (
                not isinstance(exploration, dict)
                or exploration.get("Explored") is not True
                or type(exploration.get("EligibleCount")) is not int
                or not 1 <= exploration["EligibleCount"] <= 9
                or type(exploration.get("Probability")) not in (int, float)
                or not math.isfinite(exploration["Probability"])
                or not isinstance(exploration.get("Advice"), str)
            ):
                continue
            advice = exploration["Advice"].lower()
            if ("hold" if advice == "hold" else "move-" + advice) != name:
                continue
            low = 0.2 / exploration["EligibleCount"]
            if not any(
                abs(exploration["Probability"] - value) < 1e-10
                for value in (low, low + 0.8)
            ):
                continue
        after_timestamp = action.get("after_capture_ms")
        terminal_input = (
            name != "hold"
            and action.get("after_state") == "game_over"
            and type(after_timestamp) in (int, float)
            and math.isfinite(after_timestamp)
            and observed_alive.get(round(after_timestamp * fps / (1000 * speed)))
            is False
        )
        if (
            name not in actions
            or (
                action.get("outcome")
                != ("hold-observed" if name == "hold" else "movement-observed")
                and not terminal_input
            )
            or action.get("state") != "playing"
            or type(timestamp) not in (int, float)
            or not math.isfinite(timestamp)
            or timestamp < 0
        ):
            continue
        observed_tick = round(timestamp * fps / (1000 * speed))
        if observed_alive.get(observed_tick) is not True:
            continue
        features = feature_vector(action, player_speed=player_speed)
        if features is None:
            continue
        horizon = timestamp + horizon_ms
        if horizon <= last_alive:
            label = 1
        elif horizon > first_dead:
            label = 0
        else:
            continue
        examples.append(
            (
                timestamp,
                features + [int(name == candidate) for candidate in actions],
                label,
            )
        )
    examples.sort(key=lambda row: row[0])
    return [(features, label) for _, features, label in examples]


def environment_fingerprint(benchmark):
    """Compare game, application and bridge files, allowing different packs."""
    data = json.loads(
        (Path(benchmark) / "fingerprints.json").read_text(encoding="utf-8")
    )
    selected = {
        path.replace("\\", "/").lower(): digest
        for path, digest in data.items()
        if "/.local/wsl-dodge-step-game/" in path.replace("\\", "/").lower()
        or path.replace("\\", "/")
        .lower()
        .endswith(("/projectmaelstrom.exe", "/projectmaelstrom.dll"))
        or any(
            path.replace("\\", "/").lower().endswith("/tools/" + name)
            for name in (
                "wsl_dodge_guest.py",
                "wsl_dodge_launch.sh",
                "wsl_dodge_session.sh",
            )
        )
    }
    if (
        not any("/.local/wsl-dodge-step-game/" in path for path in selected)
        or not any(path.endswith("/tools/wsl_dodge_guest.py") for path in selected)
        or not any(path.endswith("/projectmaelstrom.exe") for path in selected)
        or not any(path.endswith("/projectmaelstrom.dll") for path in selected)
    ):
        raise ValueError("Missing game, application or bridge fingerprints")
    return selected


def load_survival_examples(
    workspace,
    benchmark,
    test_seeds,
    other_benchmarks=(),
    *,
    include_holds=False,
    exploration_only=False,
    exploration_training_only=False,
    player_speed=190,
    horizon_ms=HORIZON_MS,
):
    """Combine verified policies while keeping every seed in one split."""
    benchmarks = [Path(benchmark), *(Path(path) for path in other_benchmarks)]
    if len(benchmarks) > 8 or len({str(path.resolve()) for path in benchmarks}) != len(
        benchmarks
    ):
        raise ValueError("Choose at most eight distinct benchmark sets")
    manifests = [
        json.loads((path / "manifest.json").read_text(encoding="utf-8"))
        for path in benchmarks
    ]
    all_seeds = {seed for manifest in manifests for seed in manifest.get("seeds", [])}
    selected = set(test_seeds)
    if len(selected) != len(test_seeds) or not selected or not selected < all_seeds:
        raise ValueError("Choose distinct held-out seeds across the benchmark sets")
    sources = []
    reference_environment = (
        environment_fingerprint(benchmarks[0]) if len(benchmarks) > 1 else None
    )
    for path, manifest in zip(benchmarks, manifests):
        local_test = [seed for seed in manifest["seeds"] if seed in selected]
        _, _, provenance = load_examples(
            workspace, path, local_test, allow_empty_split=True
        )
        if sources and provenance["conditions"] != sources[0]["conditions"]:
            raise ValueError("Benchmarks use different timing or limits")
        if (
            reference_environment is not None
            and environment_fingerprint(path) != reference_environment
        ):
            raise ValueError(
                "Benchmarks use different game, application or bridge files"
            )
        sources.append(provenance)
    rows, seen = [], set()
    for source in sources:
        for row in source["reports"]:
            if row["report"] not in seen:
                seen.add(row["report"])
                rows.append((row, source["pack"]["Sha256"], source["benchmark"]))
    train, test, audits = [], [], []
    total_bytes = 0
    train_count = sum(row["split"] == "train" for row, _, _ in rows)
    test_count = len(rows) - train_count
    if (
        train_count > MAX_OUTCOME_TRAIN_EXAMPLES
        or test_count > MAX_OUTCOME_TEST_EXAMPLES
    ):
        raise ValueError("Too many separate games for the offline resource limits")
    for row, pack_sha, source_benchmark in rows:
        report_path = Path(row["report"])
        raw = report_path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != row["sha256"]:
            raise ValueError("Recorded report changed during outcome labeling")
        audit_path = report_path.parent / "evaluation-audit.jsonl"
        if (
            audit_path.is_symlink()
            or not audit_path.is_file()
            or audit_path.stat().st_size > 10_000_000
        ):
            raise ValueError("Missing or oversized local outcome audit")
        audit_raw = audit_path.read_bytes()
        total_bytes += len(audit_raw)
        if total_bytes > 64_000_000:
            raise ValueError("Outcome audits exceed the offline resource limit")
        report_data = json.loads(raw)
        audit_digest, audit_sealed = verify_audit_digest(report_data, audit_raw)
        audit = [json.loads(line) for line in audit_raw.splitlines() if line.strip()]
        random_only = exploration_only or (
            exploration_training_only and row["split"] == "train"
        )
        examples = label_examples(
            report_data,
            audit,
            include_holds=include_holds,
            exploration_only=random_only,
            player_speed=player_speed,
            horizon_ms=horizon_ms,
        )
        if not examples and not random_only:
            raise ValueError(f"No unambiguous outcome labels for seed {row['seed']}")
        limit = min(
            MAX_ACTIONS_PER_GAME,
            (
                MAX_OUTCOME_TEST_EXAMPLES // test_count
                if row["split"] == "test"
                else MAX_OUTCOME_TRAIN_EXAMPLES // train_count
            ),
        )
        examples = sample_game_examples(examples, limit)
        (test if row["split"] == "test" else train).extend(examples)
        audits.append(
            {
                "seed": row["seed"],
                "sha256": audit_digest,
                "sealed_at_capture": audit_sealed,
                "examples": len(examples),
                "split": row["split"],
                "teacher_pack_sha256": pack_sha,
                "benchmark": source_benchmark,
            }
        )
    if not train or not test:
        raise ValueError("Both seed splits need usable outcome labels")
    return (
        train,
        test,
        {
            "recordings": sources,
            "outcome_audits": audits,
            "audit_fields_used": ["ticks", "alive"],
        },
    )


def balance_outcome_weights(examples, weights):
    if len(examples) != len(weights) or not examples:
        raise ValueError("Outcome balancing needs matching weights")
    positive = sum(y * weight for (_, y), weight in zip(examples, weights))
    negative = sum((1 - y) * weight for (_, y), weight in zip(examples, weights))
    if positive <= 0 or negative <= 0:
        raise ValueError("Outcome balancing requires both survival and death examples")
    total = positive + negative
    positive_factor, negative_factor = total / (2 * positive), total / (2 * negative)
    return (
        [
            weight * (positive_factor if y else negative_factor)
            for (_, y), weight in zip(examples, weights)
        ],
        positive_factor,
        negative_factor,
    )


def fit_model(
    examples, epochs=12, sample_weights=None, l2=0.0001, horizon_ms=HORIZON_MS
):
    size = len(examples[0][0]) if examples else 0
    if (
        not examples
        or len(examples) > MAX_OUTCOME_TRAIN_EXAMPLES
        or type(epochs) is not int
        or not 1 <= epochs <= 40
        or type(l2) not in (int, float)
        or not math.isfinite(l2)
        or not 0 <= l2 <= 0.1
        or type(horizon_ms) is not int
        or horizon_ms not in (1000, 2000, 5000)
        or size not in (60, 61, 70, 71)
        or any(
            len(x) != size or y not in (0, 1) or any(not math.isfinite(v) for v in x)
            for x, y in examples
        )
    ):
        raise ValueError("Invalid bounded survival training data")
    if sample_weights is None:
        sample_weights = [1.0] * len(examples)
    if len(sample_weights) != len(examples) or any(
        type(w) not in (int, float) or not math.isfinite(w) or not 0 < w <= 100
        for w in sample_weights
    ):
        raise ValueError("Invalid survival sample weights")
    means = [sum(x[j] for x, _ in examples) / len(examples) for j in range(size)]
    scales = [
        max(
            0.05,
            math.sqrt(sum((x[j] - means[j]) ** 2 for x, _ in examples) / len(examples)),
        )
        for j in range(size)
    ]
    weights = [0.0] * (size + 1)
    indices = list(range(len(examples)))
    rng = random.Random(5105)
    for _ in range(epochs):
        rng.shuffle(indices)
        for i in indices:
            x, y = examples[i]
            z = [
                max(-5, min(5, (x[j] - means[j]) / scales[j])) for j in range(size)
            ] + [1.0]
            probability = 1 / (
                1 + math.exp(-max(-30, min(30, sum(a * b for a, b in zip(weights, z)))))
            )
            for j in range(len(weights)):
                weights[j] -= 0.02 * (
                    sample_weights[i] * (probability - y) * z[j] + l2 * weights[j]
                )
    return {
        "schema_version": 1,
        "feature_version": (
            CONTEXTUAL_FEATURE_VERSION if size in (70, 71) else FEATURE_VERSION
        )
        + ("-with-hold" if size in (61, 71) else ""),
        "horizon_ms": horizon_ms,
        "means": means,
        "scales": scales,
        "weights": weights,
        "algorithm": "binary-logistic-observational-survival",
        "epochs": epochs,
        "l2": l2,
        "seed": 5105,
    }


def probability(model, features):
    if len(features) not in (60, 61, 70, 71) or len(features) != len(model["means"]):
        raise ValueError("Invalid state-action feature count")
    z = [
        max(-5, min(5, (v - m) / s))
        for v, m, s in zip(features, model["means"], model["scales"])
    ] + [1.0]
    return 1 / (
        1
        + math.exp(-max(-30, min(30, sum(a * b for a, b in zip(model["weights"], z)))))
    )


def evaluate(model, examples, training_positive_rate):
    predictions = [(probability(model, x), y) for x, y in examples]
    deaths = sum(y == 0 for _, y in predictions)
    survivors = len(predictions) - deaths
    death_brier = (
        sum(p * p for p, y in predictions if y == 0) / deaths if deaths else None
    )
    survival_brier = (
        sum((1 - p) ** 2 for p, y in predictions if y == 1) / survivors
        if survivors
        else None
    )
    detected = sum(p < 0.5 and y == 0 for p, y in predictions)
    lowest_count = max(1, math.ceil(len(predictions) * 0.2))
    lowest = sorted(predictions, key=lambda row: row[0])[:lowest_count]
    lowest_deaths = sum(y == 0 for _, y in lowest)
    return {
        "examples": len(examples),
        "survived_horizon": sum(y for _, y in examples),
        "brier_score": sum((p - y) ** 2 for p, y in predictions) / len(examples),
        "death_brier": death_brier,
        "survival_brier": survival_brier,
        "balanced_brier": (
            (death_brier + survival_brier) / 2
            if death_brier is not None and survival_brier is not None
            else None
        ),
        "constant_training_rate_brier": sum(
            (training_positive_rate - y) ** 2 for _, y in predictions
        )
        / len(examples),
        "accuracy_at_half": sum((p >= 0.5) == bool(y) for p, y in predictions)
        / len(examples),
        "deaths_within_horizon": deaths,
        "deaths_flagged_below_half": detected,
        "death_recall_at_half": detected / deaths if deaths else None,
        "false_danger_flags": sum(p < 0.5 and y == 1 for p, y in predictions),
        "lowest_survival_fifth_count": lowest_count,
        "deaths_in_lowest_survival_fifth": lowest_deaths,
        "death_recall_in_lowest_survival_fifth": (
            lowest_deaths / deaths if deaths else None
        ),
    }


def evaluate_by_game(model, examples, audit_rows, training_positive_rate):
    """Summarize whole held-out games without counting long games as more games."""
    rows = [row for row in audit_rows if row["split"] == "test"]
    offset, details = 0, []
    for row in rows:
        count = row["examples"]
        if type(count) is not int or count < 0:
            raise ValueError("Invalid held-out game size")
        group = examples[offset : offset + count]
        if len(group) != count:
            raise ValueError("Held-out game boundaries do not match examples")
        offset += count
        metrics = evaluate(model, group, training_positive_rate) if group else None
        details.append(
            {
                "seed": row["seed"],
                "examples": count,
                "balanced_brier": metrics["balanced_brier"] if metrics else None,
            }
        )
    if offset != len(examples) or not rows:
        raise ValueError("Held-out game boundaries do not match examples")
    scores = [
        row["balanced_brier"] for row in details if row["balanced_brier"] is not None
    ]
    return {
        "held_out_games": len(details),
        "games_with_both_outcomes": len(scores),
        "game_balanced_brier": (
            sum(scores) / len(scores)
            if len(scores) == len(details) and len(scores) >= 2
            else None
        ),
        "per_game": details,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workspace", type=Path)
    parser.add_argument("benchmark", type=Path)
    parser.add_argument("--also-benchmark", type=Path, action="append", default=[])
    parser.add_argument("--test-seeds", type=int, nargs="+", required=True)
    parser.add_argument(
        "--balance-games",
        action="store_true",
        help="Give each training game equal total weight",
    )
    parser.add_argument(
        "--contextual",
        action="store_true",
        help="Add interactions between the observed state and recorded action",
    )
    parser.add_argument(
        "--include-holds",
        action="store_true",
        help="Include fresh recorded Hold decisions",
    )
    parser.add_argument(
        "--exploration-only",
        action="store_true",
        help="Use only recorded random-exploration choices",
    )
    parser.add_argument(
        "--exploration-training-only",
        action="store_true",
        help="Train on random choices but evaluate on all held-out actions",
    )
    parser.add_argument(
        "--player-speed",
        type=int,
        choices=(190, 400),
        default=190,
        help="Calibrated player speed for clearance features",
    )
    parser.add_argument("--epochs", type=int, default=12)
    parser.add_argument("--l2", type=float, default=0.0001)
    parser.add_argument(
        "--balance-outcomes",
        action="store_true",
        help="Weight rare deaths and adjust the model intercept back to the recorded training rate",
    )
    parser.add_argument(
        "--horizon-ms", type=int, choices=(1000, 2000, 5000), default=HORIZON_MS
    )
    args = parser.parse_args()
    if args.exploration_only and args.exploration_training_only:
        parser.error("Choose only one exploration filtering mode")
    workspace = args.workspace.resolve(strict=True)
    train, test, provenance = load_survival_examples(
        workspace,
        args.benchmark,
        args.test_seeds,
        args.also_benchmark,
        include_holds=args.include_holds,
        exploration_only=args.exploration_only,
        exploration_training_only=args.exploration_training_only,
        player_speed=args.player_speed,
        horizon_ms=args.horizon_ms,
    )
    if args.contextual:
        train = [(contextual_features(x), y) for x, y in train]
        test = [(contextual_features(x), y) for x, y in test]
    if args.balance_games:
        rows = [
            row
            for row in provenance["outcome_audits"]
            if row["split"] == "train" and row["examples"] > 0
        ]
        sample_weights = [
            len(train) / (len(rows) * row["examples"])
            for row in rows
            for _ in range(row["examples"])
        ]
    else:
        sample_weights = [1.0] * len(train)
    positive_weight = sum(y * weight for (_, y), weight in zip(train, sample_weights))
    negative_weight = sum(
        (1 - y) * weight for (_, y), weight in zip(train, sample_weights)
    )
    rate = positive_weight / (positive_weight + negative_weight)
    positive_factor = negative_factor = 1.0
    if args.balance_outcomes:
        sample_weights, positive_factor, negative_factor = balance_outcome_weights(
            train, sample_weights
        )
    model = fit_model(
        train,
        epochs=args.epochs,
        sample_weights=sample_weights,
        l2=args.l2,
        horizon_ms=args.horizon_ms,
    )
    if args.balance_outcomes:
        # Correct the class-prior shift; this is an approximation, not a
        # guarantee of calibrated probabilities on unseen games.
        model["weights"][-1] -= math.log(positive_factor / negative_factor)
    model["game_balanced"] = args.balance_games
    model["outcome_balanced"] = args.balance_outcomes
    if args.player_speed == 400:
        model["feature_version"] += "-speed400"
    model["feature_player_speed"] = args.player_speed
    model_bytes = (json.dumps(model, indent=2) + "\n").encode("utf-8")
    held_out_metrics = evaluate(model, test, rate)
    held_out_metrics.update(
        evaluate_by_game(model, test, provenance["outcome_audits"], rate)
    )
    result = {
        "schema_version": 1,
        "purpose": "offline observational survival prediction",
        "game_balanced": args.balance_games,
        "outcome_balanced": args.balance_outcomes,
        "contextual": args.contextual,
        "include_holds": args.include_holds,
        "exploration_only": args.exploration_only,
        "exploration_training_only": args.exploration_training_only,
        "feature_player_speed": args.player_speed,
        "horizon_ms": args.horizon_ms,
        "training_positive_rate": rate,
        "train": evaluate(model, train, rate),
        "held_out_recorded_games": held_out_metrics,
        "held_out_examples_sha256": hashlib.sha256(
            json.dumps(test, separators=(",", ":")).encode("utf-8")
        ).hexdigest(),
        "model_sha256": hashlib.sha256(model_bytes).hexdigest(),
        "trainer_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "feature_extractor_sha256": hashlib.sha256(
            (Path(__file__).parent / "train_dodge_imitation.py").read_bytes()
        ).hexdigest(),
        "audit_verifier_sha256": hashlib.sha256(
            (Path(__file__).parent / "dodge_audit_integrity.py").read_bytes()
        ).hexdigest(),
        "provenance": provenance,
        "limitations": "Predicts observed survival for actions the teacher actually took, not the causal effect of alternative actions. Prior project work may have used these seeds. No gameplay or controls were run, and this model is not a qualified player.",
    }
    output = workspace / ".local/dodge-survival-models" / uuid.uuid4().hex
    output.mkdir(parents=True, exist_ok=False)
    (output / "model.json").write_bytes(model_bytes)
    (output / "report.json").write_text(
        json.dumps(result, indent=2) + "\n", encoding="utf-8"
    )
    print(
        json.dumps(
            {
                "output": str(output),
                "train": result["train"],
                "held_out_recorded_games": result["held_out_recorded_games"],
                "held_out_examples_sha256": result["held_out_examples_sha256"],
                "limitations": result["limitations"],
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
