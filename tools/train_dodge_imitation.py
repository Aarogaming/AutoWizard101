"""Fit a small Dodge action model using completed local recordings only.

This is offline behavioral cloning. Agreement with recorded actions is not
evidence of survival or proof that the model can safely control a game.
No game, capture, input, network or external model is started by this tool.
"""

from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import heapq
import json
import math
from pathlib import Path
import random
import uuid

from benchmark_dodge_scores import summarize

FEATURE_VERSION = "dodge-relative-and-clearance-v2"
MAX_ACTIONS_PER_GAME = 250
MAX_TRAIN_EXAMPLES = 2000
MAX_TEST_EXAMPLES = 300
MAX_REPORT_BYTES = 12_000_000
MAX_REPORT_TOTAL_BYTES = 64_000_000
EPOCHS = 12
LEARNING_RATE = 0.025
DIAGONAL = 2**-0.5
ACTION_VECTORS = (
    (0, -1),
    (0, 1),
    (-1, 0),
    (1, 0),
    (-DIAGONAL, -DIAGONAL),
    (DIAGONAL, -DIAGONAL),
    (-DIAGONAL, DIAGONAL),
    (DIAGONAL, DIAGONAL),
)


def feature_vector(action, *, player_speed=190):
    """Extract bounded visual features; no game audit data is read."""
    if player_speed not in (190, 400):
        raise ValueError("Unsupported calibrated player speed")
    player = action.get("before_player")
    threats = action.get("threats")
    if (
        not isinstance(player, dict)
        or not isinstance(threats, list)
        or len(threats) > 32
    ):
        return None
    try:
        px, py = float(player["X"]), float(player["Y"])
        if not all(math.isfinite(v) and 0 <= v <= 1 for v in (px, py)):
            return None
        candidates = []
        for threat in threats:
            x, y = float(threat["position"]["X"]), float(threat["position"]["Y"])
            vx, vy = float(threat["velocity_px_s"]["X"]), float(
                threat["velocity_px_s"]["Y"]
            )
            known = threat["velocity_known"]
            if type(known) is not bool or not all(
                math.isfinite(v) for v in (x, y, vx, vy)
            ):
                return None
            dx, dy = x - px, y - py
            candidates.append((math.hypot(dx * 480, dy * 720), dx, dy, vx, vy, known))
        candidates.sort(key=lambda t: t[0])
        features = [px, py]
        for index in range(3):
            if index >= len(candidates):
                features.extend([0.0] * 6)
                continue
            _, dx, dy, vx, vy, known = candidates[index]
            features.extend(
                [
                    max(-1, min(1, dx)),
                    max(-1, min(1, dy)),
                    max(-1, min(1, vx / 350)),
                    max(-1, min(1, vy / 350)),
                    float(known),
                    1.0,
                ]
            )
        # Action-relative clearance and room estimates provide nonlinear cues
        # to a tiny linear model. They use only visible positions and velocities.
        for ax, ay in ACTION_VECTORS:
            for seconds in (0.2, 0.6, 1.0):
                distances = []
                for _, dx, dy, vx, vy, known in candidates[:3]:
                    tx = dx * 480 + (vx if known else 0) * seconds
                    ty = dy * 720 + (vy if known else 0) * seconds
                    distances.append(
                        math.hypot(
                            ax * player_speed * seconds - tx,
                            ay * player_speed * seconds - ty,
                        )
                        - 84
                    )
                features.append(
                    max(-1, min(1, min(distances) / 200)) if distances else 1.0
                )
            x = px * 480 + ax * player_speed
            y = py * 720 + ay * player_speed
            edge = min(x, 480 - x, y, 720 - y)
            features.append(max(-1, min(1, edge / 120)))
        return features
    except (KeyError, TypeError, ValueError, OverflowError):
        return None


def load_examples(
    workspace, benchmark, test_seeds, *, include_holds=False, allow_empty_split=False
):
    """Verify a complete benchmark and split examples by whole game seed."""
    workspace = Path(workspace).resolve(strict=True)
    benchmark = Path(benchmark).resolve(strict=True)
    roots = [
        (workspace / ".local/dodge-training").resolve(),
        (workspace / ".local/dodge-benchmarks").resolve(),
        (workspace / ".local/dodge-curricula").resolve(),
    ]
    if not any(benchmark.is_relative_to(root) and benchmark != root for root in roots):
        raise ValueError("Benchmark must be inside the local Dodge evidence folders")
    manifest = json.loads((benchmark / "manifest.json").read_text(encoding="utf-8"))
    saved = json.loads((benchmark / "summary.json").read_text(encoding="utf-8"))
    seeds = manifest.get("seeds", [])
    test = set(test_seeds)
    if (
        len(test) != len(test_seeds)
        or not test <= set(seeds)
        or (not allow_empty_split and (not test or not test < set(seeds)))
    ):
        raise ValueError(
            "Choose distinct held-out seeds from the benchmark, leaving training seeds"
        )
    train_limit = min(
        MAX_ACTIONS_PER_GAME, MAX_TRAIN_EXAMPLES // max(1, len(seeds) - len(test))
    )
    test_limit = min(MAX_ACTIONS_PER_GAME, MAX_TEST_EXAMPLES // max(1, len(test)))
    run_root = (workspace / ".local/wsl-dodge-runs").resolve(strict=True)
    reports = []
    total_bytes = 0
    for game in saved.get("games", []):
        if not game.get("report"):
            raise ValueError("Benchmark contains incomplete games")
        path = Path(game["report"]).resolve(strict=True)
        if not path.is_relative_to(run_root):
            raise ValueError("Report is outside the private run folder")
        size = path.stat().st_size
        total_bytes += size
        if size > MAX_REPORT_BYTES or total_bytes > MAX_REPORT_TOTAL_BYTES:
            raise ValueError("Recorded reports exceed offline resource limits")
        reports.append(path)
    checked = summarize(manifest, reports)
    if checked != saved or checked["completed_games"] != checked["expected_games"]:
        raise ValueError("Benchmark is incomplete or its saved scores changed")
    train, heldout, provenance = [], [], []
    for report in reports:
        raw = report.read_bytes()
        data = json.loads(raw)
        seed = data["seed"]
        examples = []
        for index, action in enumerate(data.get("actions", [])):
            label = action.get("action")
            if (
                action.get("outcome") != "movement-observed"
                or action.get("after_state") != "playing"
                or label not in ACTIONS
            ):
                continue
            vector = feature_vector(action)
            if vector is not None:
                timestamp = action.get("before_capture_ms", index)
                if (
                    type(timestamp) in (int, float)
                    and math.isfinite(timestamp)
                    and timestamp >= 0
                ):
                    examples.append((timestamp, vector, label))
        if include_holds:
            for decision in data.get("decisions", []):
                if (
                    decision.get("advice") != "Hold"
                    or decision.get("action") is not None
                    or decision.get("observation_fresh") is not True
                    or decision.get("state") != "playing"
                ):
                    continue
                timestamp = decision.get("capture_ms")
                vector = feature_vector(decision)
                if (
                    vector is not None
                    and type(timestamp) in (int, float)
                    and math.isfinite(timestamp)
                    and timestamp >= 0
                ):
                    examples.append((timestamp, vector, "hold"))
        if not examples:
            raise ValueError(f"No usable actions for seed {seed}")
        examples.sort(key=lambda row: row[0])
        # Equal per-game cap prevents one unusually long run dominating.
        per_game_limit = test_limit if seed in test else train_limit
        if len(examples) > per_game_limit:
            examples = [
                examples[i * len(examples) // per_game_limit]
                for i in range(per_game_limit)
            ]
        (heldout if seed in test else train).extend(
            (vector, label) for _, vector, label in examples
        )
        provenance.append(
            {
                "seed": seed,
                "report": str(report),
                "sha256": hashlib.sha256(raw).hexdigest(),
                "examples": len(examples),
                "split": "test" if seed in test else "train",
            }
        )
    if not allow_empty_split and (not train or not heldout):
        raise ValueError("Both recorded-game splits need usable actions")
    return (
        train,
        heldout,
        {
            "benchmark": str(benchmark),
            "manifest": manifest,
            "pack": checked["pack"],
            "conditions": {
                "mode": checked["mode"],
                "speed": checked["speed"],
                "observation_cadence": checked["observation_cadence"],
                "run_limits": checked["run_limits"],
            },
            "reports": provenance,
        },
    )


ACTIONS = (
    "move-up",
    "move-down",
    "move-left",
    "move-right",
    "move-up-left",
    "move-up-right",
    "move-down-left",
    "move-down-right",
)
ACTIONS_WITH_HOLD = ACTIONS + ("hold",)


def fit_model(examples, epochs=EPOCHS):
    """Train a deterministic, tiny multiclass linear classifier on CPU."""
    if (
        not examples
        or len(examples) > MAX_TRAIN_EXAMPLES
        or type(epochs) is not int
        or not 1 <= epochs <= 40
    ):
        raise ValueError("Training requires examples and 1–40 bounded epochs")
    size = len(examples[0][0])
    if size != 52 or any(
        len(x) != size
        or y not in ACTIONS_WITH_HOLD
        or any(not math.isfinite(v) for v in x)
        for x, y in examples
    ):
        raise ValueError("Invalid training features or action")
    actions = ACTIONS_WITH_HOLD if any(y == "hold" for _, y in examples) else ACTIONS
    means = [sum(x[j] for x, _ in examples) / len(examples) for j in range(size)]
    scales = [
        max(
            0.05,
            math.sqrt(sum((x[j] - means[j]) ** 2 for x, _ in examples) / len(examples)),
        )
        for j in range(size)
    ]
    weights = [[0.0] * (size + 1) for _ in actions]
    order = list(range(len(examples)))
    rng = random.Random(1741)
    for _ in range(epochs):
        rng.shuffle(order)
        for i in order:
            x, label = examples[i]
            z = [
                max(-5, min(5, (x[j] - means[j]) / scales[j])) for j in range(size)
            ] + [1.0]
            logits = [sum(a * b for a, b in zip(row, z)) for row in weights]
            offset = max(logits)
            exp = [math.exp(value - offset) for value in logits]
            total = sum(exp)
            target = actions.index(label)
            for k, row in enumerate(weights):
                gradient = exp[k] / total - (k == target)
                for j in range(len(row)):
                    row[j] -= LEARNING_RATE * (gradient * z[j] + 0.0001 * row[j])
    return {
        "schema_version": 1,
        "feature_version": FEATURE_VERSION,
        "actions": list(actions),
        "means": means,
        "scales": scales,
        "weights": weights,
        "training": {
            "algorithm": "multiclass-linear-behavior-cloning",
            "epochs": epochs,
            "learning_rate": LEARNING_RATE,
            "seed": 1741,
        },
    }


def predict(model, x):
    if len(x) != len(model["means"]):
        raise ValueError("Unexpected feature length")
    z = [
        max(-5, min(5, (v - m) / s))
        for v, m, s in zip(x, model["means"], model["scales"])
    ] + [1.0]
    scores = [sum(a * b for a, b in zip(row, z)) for row in model["weights"]]
    return model["actions"][max(range(len(scores)), key=scores.__getitem__)]


def fit_neighbors(examples, neighbors=9):
    """Retain a bounded set of standardized demonstrations for local k-NN."""
    if (
        not examples
        or len(examples) > MAX_TRAIN_EXAMPLES
        or type(neighbors) is not int
        or not 1 <= neighbors <= 25
    ):
        raise ValueError("Choose 1–2,000 examples and 1–25 neighbors")
    size = len(examples[0][0])
    if size != 52 or any(
        len(x) != size
        or y not in ACTIONS_WITH_HOLD
        or any(not math.isfinite(v) for v in x)
        for x, y in examples
    ):
        raise ValueError("Invalid training features or action")
    actions = ACTIONS_WITH_HOLD if any(y == "hold" for _, y in examples) else ACTIONS
    means = [sum(x[j] for x, _ in examples) / len(examples) for j in range(size)]
    scales = [
        max(
            0.05,
            math.sqrt(sum((x[j] - means[j]) ** 2 for x, _ in examples) / len(examples)),
        )
        for j in range(size)
    ]
    vectors = [
        [max(-5, min(5, (x[j] - means[j]) / scales[j])) for j in range(size)]
        for x, _ in examples
    ]
    return {
        "schema_version": 1,
        "feature_version": FEATURE_VERSION,
        "algorithm": "weighted-nearest-demonstrations",
        "neighbors": neighbors,
        "actions": list(actions),
        "means": means,
        "scales": scales,
        "vectors": vectors,
        "labels": [actions.index(y) for _, y in examples],
    }


def predict_neighbors(model, x):
    if len(x) != len(model["means"]):
        raise ValueError("Unexpected feature length")
    z = [
        max(-5, min(5, (v - m) / s))
        for v, m, s in zip(x, model["means"], model["scales"])
    ]
    nearest = heapq.nsmallest(
        model["neighbors"],
        (
            (sum((a - b) ** 2 for a, b in zip(z, row)), index)
            for index, row in enumerate(model["vectors"])
        ),
    )
    votes = [0.0] * len(model["actions"])
    for distance, index in nearest:
        votes[model["labels"][index]] += 1 / (0.1 + math.sqrt(distance))
    return model["actions"][max(range(len(votes)), key=votes.__getitem__)]


def agreement(model, examples, predictor=predict):
    correct = sum(predictor(model, x) == label for x, label in examples)
    counts = Counter(label for _, label in examples)
    return {
        "examples": len(examples),
        "matching_actions": correct,
        "agreement": correct / len(examples),
        "largest_single_action_baseline": max(counts.values()) / len(examples),
        "recorded_action_counts": dict(sorted(counts.items())),
    }


def evaluate_saved_model(workspace, benchmark, test_seeds, model_directory):
    """Compare a saved model on different recorded seeds, without game control."""
    workspace = Path(workspace).resolve(strict=True)
    directory = Path(model_directory).resolve(strict=True)
    model_root = (workspace / ".local/dodge-offline-models").resolve(strict=True)
    if not directory.is_relative_to(model_root) or directory == model_root:
        raise ValueError("Choose a model inside the local offline model folder")
    report_path = directory / "report.json"
    if report_path.stat().st_size > 1_000_000:
        raise ValueError("Training report exceeds offline resource limits")
    previous = json.loads(report_path.read_text(encoding="utf-8"))
    training_seeds = {
        row["seed"]
        for row in previous["provenance"]["reports"]
        if row["split"] == "train"
    }
    if training_seeds.intersection(test_seeds):
        raise ValueError("Evaluation seeds overlap the model's training games")
    model_path = directory / "model.json"
    neighbor_path = directory / "model-neighbors.json"
    if model_path.stat().st_size > 200_000 or neighbor_path.stat().st_size > 5_000_000:
        raise ValueError("Saved model exceeds offline resource limits")
    model_bytes = model_path.read_bytes()
    neighbor_bytes = neighbor_path.read_bytes()
    if hashlib.sha256(model_bytes).hexdigest() != previous.get(
        "model_sha256"
    ) or hashlib.sha256(neighbor_bytes).hexdigest() != previous.get(
        "neighbor_model_sha256"
    ):
        raise ValueError("Saved model does not match its training report")
    model = json.loads(model_bytes)
    neighbors = json.loads(neighbor_bytes)
    if (
        model.get("feature_version") != FEATURE_VERSION
        or neighbors.get("feature_version") != FEATURE_VERSION
    ):
        raise ValueError("Model feature version is incompatible")
    _, test, provenance = load_examples(
        workspace,
        benchmark,
        test_seeds,
        include_holds="hold" in model.get("actions", []),
    )
    if provenance["pack"]["Sha256"] != previous["provenance"]["pack"]["Sha256"]:
        raise ValueError("Recorded actions came from a different teacher pack")
    return {
        "schema_version": 1,
        "purpose": "offline replay of previously recorded decisions",
        "linear": agreement(model, test),
        "nearest_demonstrations": agreement(neighbors, test, predict_neighbors),
        "model_directory": str(directory),
        "model_sha256": hashlib.sha256(model_path.read_bytes()).hexdigest(),
        "provenance": provenance,
        "limitations": "These games were not used to fit this model, but may have informed earlier project work. Action agreement is not survival or independent final validation; no gameplay or controls were run.",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workspace", type=Path)
    parser.add_argument("benchmark", type=Path)
    parser.add_argument("--test-seeds", nargs="+", required=True, type=int)
    parser.add_argument("--evaluate-model", type=Path)
    parser.add_argument(
        "--include-holds",
        action="store_true",
        help="Include fresh, recorded Hold decisions when present",
    )
    args = parser.parse_args()
    workspace = args.workspace.resolve(strict=True)
    if args.evaluate_model is not None:
        result = evaluate_saved_model(
            workspace, args.benchmark, args.test_seeds, args.evaluate_model
        )
        output = workspace / ".local/dodge-offline-evaluations" / uuid.uuid4().hex
        output.mkdir(parents=True, exist_ok=False)
        (output / "report.json").write_text(
            json.dumps(result, indent=2) + "\n", encoding="utf-8"
        )
        print(
            json.dumps(
                {
                    "output": str(output),
                    "linear": result["linear"],
                    "nearest_demonstrations": result["nearest_demonstrations"],
                    "limitations": result["limitations"],
                },
                indent=2,
            )
        )
        return
    train, test, provenance = load_examples(
        workspace, args.benchmark, args.test_seeds, include_holds=args.include_holds
    )
    if args.include_holds and not any(label == "hold" for _, label in train):
        raise ValueError("The training games have no fresh recorded Hold decisions")
    model = fit_model(train)
    neighbors = fit_neighbors(train)
    model_bytes = (json.dumps(model, indent=2) + "\n").encode("utf-8")
    neighbor_bytes = (json.dumps(neighbors) + "\n").encode("utf-8")
    result = {
        "schema_version": 1,
        "purpose": "offline action imitation only",
        "include_holds": args.include_holds,
        "train": agreement(model, train),
        "held_out_recorded_games": agreement(model, test),
        "nearest_demonstrations_held_out": agreement(
            neighbors, test, predict_neighbors
        ),
        "trainer_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "model_sha256": hashlib.sha256(model_bytes).hexdigest(),
        "neighbor_model_sha256": hashlib.sha256(neighbor_bytes).hexdigest(),
        "provenance": provenance,
        "limitations": "Decision agreement is not gameplay success. Movement examples exclude unverified actions and can include bad decisions; these seeds may already have informed game development. "
        + (
            "Hold decisions are imitated, not proven safe. "
            if args.include_holds
            else "Hold actions are not learned. "
        )
        + "This training step does not run gameplay or install a model.",
    }
    output = workspace / ".local/dodge-offline-models" / uuid.uuid4().hex
    output.mkdir(parents=True, exist_ok=False)
    (output / "model.json").write_bytes(model_bytes)
    (output / "model-neighbors.json").write_bytes(neighbor_bytes)
    (output / "report.json").write_text(
        json.dumps(result, indent=2) + "\n", encoding="utf-8"
    )
    print(
        json.dumps(
            {
                "output": str(output),
                "train": result["train"],
                "held_out_recorded_games": result["held_out_recorded_games"],
                "nearest_demonstrations_held_out": result[
                    "nearest_demonstrations_held_out"
                ],
                "limitations": result["limitations"],
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
