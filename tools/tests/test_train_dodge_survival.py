import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from benchmark_dodge_scores import summarize
from train_dodge_survival import (
    balance_outcome_weights,
    contextual_features,
    evaluate,
    evaluate_by_game,
    fit_model,
    label_examples,
    load_survival_examples,
    probability,
    sample_game_examples,
)
from tools.tests.test_benchmark_dodge_scores import MANIFEST, write_report
from tools.tests.test_train_dodge_imitation import action


def example_report():
    rows = []
    for timestamp in (4000, 5050, 5200):
        row = action(0.5, "move-left")
        row.update(state="playing", before_capture_ms=timestamp)
        rows.append(row)
    return {
        "training": {"mode": "stepped", "speed": 0.25, "fixed_fps": 30},
        "actions": rows,
    }


class OfflineSurvivalTests(unittest.TestCase):
    def test_per_game_sampling_preserves_the_end_of_a_long_game(self):
        self.assertEqual(sample_game_examples(list(range(10)), 4), [0, 3, 6, 9])
        self.assertEqual(sample_game_examples(list(range(10)), 1), [9])
        self.assertEqual(sample_game_examples([1, 2], 4), [1, 2])
        with self.assertRaises(ValueError):
            sample_game_examples([1], 0)

    def test_game_balanced_metric_needs_both_outcomes_in_every_game(self):
        model = {"means": [0] * 60, "scales": [1] * 60, "weights": [0] * 61}
        examples = [([0.0] * 60, y) for y in (1, 1, 1, 0, 1, 0)]
        rows = [
            {"seed": 1, "split": "test", "examples": 4},
            {"seed": 2, "split": "test", "examples": 2},
        ]
        result = evaluate_by_game(model, examples, rows, 0.5)
        self.assertEqual(result["held_out_games"], 2)
        self.assertEqual(result["games_with_both_outcomes"], 2)
        self.assertEqual(result["game_balanced_brier"], 0.25)
        missing = evaluate_by_game(
            model,
            examples[:2],
            [
                {"seed": 1, "split": "test", "examples": 1},
                {"seed": 2, "split": "test", "examples": 1},
            ],
            0.5,
        )
        self.assertIsNone(missing["game_balanced_brier"])
        with self.assertRaisesRegex(ValueError, "boundaries"):
            evaluate_by_game(model, examples, rows[:1], 0.5)

    def test_outcome_balancing_weights_each_observed_class_equally(self):
        examples = [([0.0] * 60, y) for y in (1, 1, 1, 0)]
        weights, positive_factor, negative_factor = balance_outcome_weights(
            examples, [1.0] * 4
        )
        self.assertAlmostEqual(sum(weights[:3]), weights[3])
        self.assertAlmostEqual(positive_factor, 2 / 3)
        self.assertAlmostEqual(negative_factor, 2)
        with self.assertRaisesRegex(ValueError, "both"):
            balance_outcome_weights(examples[:3], [1.0] * 3)

    def test_disjoint_experience_sets_keep_seed_holdouts_separate(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            runs = root / ".local/wsl-dodge-runs"
            runs.mkdir(parents=True)
            fingerprint = {
                str(root / ".local/wsl-dodge-step-game/project.godot"): "game",
                str(root / "tools/wsl_dodge_guest.py"): "bridge",
                str(root / "application/ProjectMaelstrom.exe"): "app",
                str(root / "application/ProjectMaelstrom.dll"): "dll",
            }
            benchmarks = []
            for index, seeds in enumerate(((1, 2), (3, 4))):
                folder = root / f".local/dodge-benchmarks/{index}"
                folder.mkdir(parents=True)
                run_folder = runs / str(index)
                run_folder.mkdir()
                reports = [write_report(run_folder, seed, 20) for seed in seeds]
                for path in reports:
                    data = json.loads(path.read_text())
                    data["actions"] = example_report()["actions"]
                    data["actions"][0]["exploration"] = {
                        "Advice": "Left",
                        "Explored": True,
                        "Probability": 0.025,
                        "EligibleCount": 8,
                    }
                    path.write_text(json.dumps(data))
                    (path.parent / "evaluation-audit.jsonl").write_text(
                        "\n".join(
                            json.dumps(x)
                            for x in [
                                {"ticks": 480, "alive": True},
                                {"ticks": 606, "alive": True},
                                {"ticks": 624, "alive": True},
                                {"ticks": 1200, "alive": True},
                                {"ticks": 1212, "alive": False},
                            ]
                        )
                    )
                manifest = {**MANIFEST, "seeds": list(seeds)}
                (folder / "manifest.json").write_text(json.dumps(manifest))
                (folder / "summary.json").write_text(
                    json.dumps(summarize(manifest, reports))
                )
                (folder / "fingerprints.json").write_text(json.dumps(fingerprint))
                benchmarks.append(folder)
            train, test, evidence = load_survival_examples(
                root, benchmarks[0], [3, 4], [benchmarks[1]]
            )
            self.assertEqual(len(train), 4)
            self.assertEqual(len(test), 4)
            random_train, same_test, _ = load_survival_examples(
                root,
                benchmarks[0],
                [3, 4],
                [benchmarks[1]],
                exploration_training_only=True,
            )
            self.assertEqual(len(random_train), 2)
            self.assertEqual(same_test, test)
            self.assertEqual(
                {r["seed"] for r in evidence["outcome_audits"] if r["split"] == "test"},
                {3, 4},
            )
            with self.assertRaisesRegex(ValueError, "held-out"):
                load_survival_examples(root, benchmarks[0], [3, 4, 5], [benchmarks[1]])

    def test_contextual_model_can_learn_opposite_actions_in_opposite_states(self):
        examples = []
        for threat_x in (-0.8, 0.8):
            for action_index, direction in ((2, -1), (3, 1)):
                features = [0.0] * 60
                features[2] = threat_x
                features[52 + action_index] = 1
                # In this synthetic example, moving away is always safer.
                label = int(threat_x * direction < 0)
                examples.extend([(contextual_features(features), label)] * 20)
        model = fit_model(examples, epochs=40)
        self.assertEqual(
            model["feature_version"], "dodge-visual-state-action-context-v2"
        )
        self.assertLess(evaluate(model, examples, 0.5)["brier_score"], 0.1)
        with self.assertRaisesRegex(ValueError, "one recorded"):
            contextual_features([0.0] * 60)

    def test_calibrated_speed_changes_predicted_clearance(self):
        report = example_report()
        report["actions"][0]["threats"] = [
            {
                "position": {"X": 0.8, "Y": 0.5},
                "velocity_px_s": {"X": 0, "Y": 0},
                "velocity_known": True,
            }
        ]
        audit = [
            {"ticks": 480, "alive": True},
            {"ticks": 1200, "alive": True},
            {"ticks": 1212, "alive": False},
        ]
        old = label_examples(report, audit, player_speed=190)[0][0]
        corrected = label_examples(report, audit, player_speed=400)[0][0]
        self.assertNotEqual(
            old[32], corrected[32]
        )  # Rightward forecast at 0.2 seconds.
        with self.assertRaises(ValueError):
            label_examples(report, audit, player_speed=123)

    def test_hold_outcome_requires_fresh_visual_state_and_aligned_alive_capture(self):
        report = example_report()
        state = action(0.5, "move-left")
        state.update(
            advice="Hold",
            action=None,
            state="playing",
            observation_fresh=True,
            capture_ms=4000,
        )
        report["decisions"] = [
            state,
            {**state, "capture_ms": 4100, "observation_fresh": False},
        ]
        audit = [
            {"ticks": 480, "alive": True},
            {"ticks": 606, "alive": True},
            {"ticks": 624, "alive": True},
            {"ticks": 1200, "alive": True},
            {"ticks": 1212, "alive": False},
        ]
        labeled = label_examples(report, audit, include_holds=True)
        holds = [(features, label) for features, label in labeled if features[60] == 1]
        self.assertEqual(len(holds), 1)
        self.assertEqual(holds[0][1], 1)
        self.assertEqual(len(contextual_features(holds[0][0])), 71)
        state["capture_ms"] = 4100  # Not an observed tick in the audit.
        self.assertFalse(
            any(
                features[60]
                for features, _ in label_examples(report, audit, include_holds=True)
            )
        )

    def test_exploration_filter_keeps_only_recorded_random_choices(self):
        report = example_report()
        report["actions"][0]["exploration"] = {
            "Advice": "Left",
            "Explored": True,
            "Probability": 0.025,
            "EligibleCount": 8,
        }
        report["actions"][1]["exploration"] = {
            "Advice": "Left",
            "Explored": False,
            "Probability": 0.9,
            "EligibleCount": 8,
        }
        report["actions"][2]["exploration"] = {
            "Advice": "Left",
            "Explored": True,
            "Probability": 0.0,
            "EligibleCount": 8,
        }
        audit = [
            {"ticks": 480, "alive": True},
            {"ticks": 606, "alive": True},
            {"ticks": 624, "alive": True},
            {"ticks": 1200, "alive": True},
            {"ticks": 1212, "alive": False},
        ]
        examples = label_examples(report, audit, exploration_only=True)
        self.assertEqual([label for _, label in examples], [1])

    def test_terminal_input_can_be_a_negative_without_claiming_movement(self):
        report = example_report()
        final = action(0.5, "move-left")
        final.update(
            state="playing",
            outcome="unknown",
            after_state="game_over",
            before_capture_ms=9991.666666666666,
            after_capture_ms=10100,
        )
        report["actions"] = [final]
        audit = [{"ticks": 1199, "alive": True}, {"ticks": 1212, "alive": False}]
        labeled = label_examples(report, audit)
        self.assertEqual([label for _, label in labeled], [0])
        final["after_capture_ms"] = 10000  # Not an observed dead capture.
        self.assertEqual(label_examples(report, audit), [])

    def test_multiple_policies_keep_seed_splits_and_require_the_same_game(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            runs = root / ".local/wsl-dodge-runs"
            runs.mkdir(parents=True)
            benchmarks = []
            fingerprint = {
                str(root / ".local/wsl-dodge-step-game/project.godot"): "game",
                str(root / "tools/wsl_dodge_guest.py"): "bridge",
                str(root / "application/ProjectMaelstrom.exe"): "app",
                str(root / "application/ProjectMaelstrom.dll"): "dll",
            }
            for index in range(2):
                folder = root / f".local/dodge-training/study/benchmarks/0{index}"
                folder.mkdir(parents=True)
                run_folder = runs / str(index)
                run_folder.mkdir()
                identity = {
                    "Id": "test",
                    "Version": str(index),
                    "Sha256": ("a" if index == 0 else "b") * 64,
                }
                reports = [
                    write_report(run_folder, seed, 20, pack=identity) for seed in (1, 2)
                ]
                for path in reports:
                    data = json.loads(path.read_text())
                    data["actions"] = example_report()["actions"]
                    path.write_text(json.dumps(data))
                    (path.parent / "evaluation-audit.jsonl").write_text(
                        "\n".join(
                            json.dumps(x)
                            for x in [
                                {"ticks": 480, "alive": True},
                                {"ticks": 606, "alive": True},
                                {"ticks": 624, "alive": True},
                                {"ticks": 1200, "alive": True},
                                {"ticks": 1212, "alive": False},
                            ]
                        )
                    )
                (folder / "manifest.json").write_text(json.dumps(MANIFEST))
                (folder / "summary.json").write_text(
                    json.dumps(summarize(MANIFEST, reports))
                )
                (folder / "fingerprints.json").write_text(json.dumps(fingerprint))
                benchmarks.append(folder)
            train, test, provenance = load_survival_examples(
                root, benchmarks[0], [2], [benchmarks[1]]
            )
            self.assertEqual([y for _, y in train], [1, 0, 1, 0])
            self.assertEqual([y for _, y in test], [1, 0, 1, 0])
            self.assertEqual(
                {row["teacher_pack_sha256"] for row in provenance["outcome_audits"]},
                {"a" * 64, "b" * 64},
            )
            (benchmarks[1] / "fingerprints.json").write_text(
                json.dumps(
                    {**fingerprint, str(root / "tools/wsl_dodge_guest.py"): "changed"}
                )
            )
            with self.assertRaisesRegex(ValueError, "different game"):
                load_survival_examples(root, benchmarks[0], [2], [benchmarks[1]])

    def test_only_unambiguous_captured_outcomes_are_labeled(self):
        audit = [
            {"ticks": 480, "alive": True},
            {"ticks": 606, "alive": True},
            {"ticks": 624, "alive": True},
            {"ticks": 1200, "alive": True, "player": {"x": 1}},
            {"ticks": 1212, "alive": False, "player": {"x": 999}},
        ]
        labeled = label_examples(example_report(), audit)
        self.assertEqual([label for _, label in labeled], [1, 0])
        self.assertEqual(len(labeled[0][0]), 60)
        audit[3]["player"]["x"] = -1234
        self.assertEqual(label_examples(example_report(), audit), labeled)
        with self.assertRaises(ValueError):
            label_examples(example_report(), audit[:-1])
        with self.assertRaises(ValueError):
            label_examples(example_report(), list(reversed(audit)))

    def test_small_logistic_model_learns_observed_outcome_pattern(self):
        low = [0.0] * 60
        high = [0.0] * 60
        low[0], high[0] = -0.8, 0.8
        examples = [(low, 0)] * 20 + [(high, 1)] * 20
        model = fit_model(examples)
        self.assertEqual(model, fit_model(examples))
        self.assertLess(probability(model, low), probability(model, high))
        self.assertLess(evaluate(model, examples, 0.5)["brier_score"], 0.25)
        metrics = evaluate(model, examples, 0.5)
        self.assertAlmostEqual(metrics["balanced_brier"], metrics["brier_score"])
        self.assertEqual(metrics["deaths_within_horizon"], 20)
        self.assertEqual(metrics["deaths_flagged_below_half"], 20)
        self.assertEqual(metrics["false_danger_flags"], 0)
        self.assertEqual(metrics["death_recall_at_half"], 1)
        self.assertEqual(metrics["lowest_survival_fifth_count"], 8)
        self.assertEqual(metrics["deaths_in_lowest_survival_fifth"], 8)
        with self.assertRaisesRegex(ValueError, "weights"):
            fit_model(examples, sample_weights=[1])
        with self.assertRaisesRegex(ValueError, "training data"):
            fit_model(examples, l2=0.2)
        with self.assertRaisesRegex(ValueError, "training data"):
            fit_model(examples, horizon_ms=999)
        self.assertEqual(fit_model(examples, horizon_ms=1000)["horizon_ms"], 1000)
        balanced = fit_model(examples, sample_weights=[1.0] * len(examples))
        self.assertEqual(model, balanced)

    def test_completed_recordings_are_split_by_game(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            benchmark = root / ".local/dodge-training/study/benchmarks/00"
            benchmark.mkdir(parents=True)
            runs = root / ".local/wsl-dodge-runs"
            runs.mkdir(parents=True)
            reports = [write_report(runs, seed, 20) for seed in (1, 2)]
            for path in reports:
                data = json.loads(path.read_text())
                data["actions"] = example_report()["actions"]
                path.write_text(json.dumps(data))
                (path.parent / "evaluation-audit.jsonl").write_text(
                    "\n".join(
                        json.dumps(x)
                        for x in [
                            {"ticks": 480, "alive": True},
                            {"ticks": 606, "alive": True},
                            {"ticks": 624, "alive": True},
                            {"ticks": 1200, "alive": True},
                            {"ticks": 1212, "alive": False},
                        ]
                    )
                )
            (benchmark / "manifest.json").write_text(json.dumps(MANIFEST))
            (benchmark / "summary.json").write_text(
                json.dumps(summarize(MANIFEST, reports))
            )
            train, test, provenance = load_survival_examples(root, benchmark, [2])
            self.assertEqual([y for _, y in train], [1, 0])
            self.assertEqual([y for _, y in test], [1, 0])
            self.assertEqual(provenance["audit_fields_used"], ["ticks", "alive"])
            (reports[0].parent / "evaluation-audit.jsonl").unlink()
            with self.assertRaisesRegex(ValueError, "audit"):
                load_survival_examples(root, benchmark, [2])


if __name__ == "__main__":
    unittest.main()
