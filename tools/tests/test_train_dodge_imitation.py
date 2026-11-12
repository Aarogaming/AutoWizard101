import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from benchmark_dodge_scores import summarize
from train_dodge_imitation import (
    agreement,
    evaluate_saved_model,
    feature_vector,
    fit_model,
    fit_neighbors,
    load_examples,
    predict,
    predict_neighbors,
)
from tools.tests.test_benchmark_dodge_scores import MANIFEST, write_report


def action(x, label):
    return {
        "action": label,
        "outcome": "movement-observed",
        "after_state": "playing",
        "before_player": {"X": x, "Y": 0.5},
        "threats": [],
    }


class OfflineImitationTests(unittest.TestCase):
    def test_features_use_only_visual_observation_and_pad_missing_threats(self):
        row = action(0.3, "move-left")
        row["threats"] = [
            {
                "position": {"X": 0.5, "Y": 0.6},
                "velocity_px_s": {"X": 175, "Y": -350},
                "velocity_known": True,
            }
        ]
        features = feature_vector(row)
        self.assertEqual(len(features), 52)
        self.assertAlmostEqual(features[2], 0.2)
        self.assertEqual(features[4:8], [0.5, -1, 1, 1])
        self.assertEqual(features[8:20], [0] * 12)
        self.assertTrue(all(-1 <= item <= 1 for item in features[20:]))
        row["before_player"]["X"] = float("nan")
        self.assertIsNone(feature_vector(row))
        row["before_player"]["X"] = 0.3
        row["threats"] *= 33
        self.assertIsNone(feature_vector(row))

    def test_model_is_deterministic_and_learns_a_simple_separation(self):
        examples = [(feature_vector(action(0.1, "move-right")), "move-right")] * 20 + [
            (feature_vector(action(0.9, "move-left")), "move-left")
        ] * 20
        model = fit_model(examples)
        self.assertEqual(model, fit_model(examples))
        self.assertEqual(
            predict(model, feature_vector(action(0.1, "move-right"))), "move-right"
        )
        self.assertEqual(
            predict(model, feature_vector(action(0.9, "move-left"))), "move-left"
        )
        self.assertEqual(agreement(model, examples)["agreement"], 1)
        nearest = fit_neighbors(examples)
        self.assertEqual(
            predict_neighbors(nearest, feature_vector(action(0.1, "move-right"))),
            "move-right",
        )
        self.assertEqual(
            predict_neighbors(nearest, feature_vector(action(0.9, "move-left"))),
            "move-left",
        )
        with_hold = [(feature_vector(action(0.1, "hold")), "hold")] * 20 + [
            (feature_vector(action(0.9, "move-left")), "move-left")
        ] * 20
        hold_model = fit_model(with_hold)
        self.assertEqual(hold_model["actions"][-1], "hold")
        self.assertEqual(
            predict(hold_model, feature_vector(action(0.1, "hold"))), "hold"
        )
        self.assertEqual(
            predict_neighbors(
                fit_neighbors(with_hold), feature_vector(action(0.1, "hold"))
            ),
            "hold",
        )

    def test_split_is_by_recorded_seed_and_incomplete_data_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            benchmark = root / ".local/dodge-training/study/benchmarks/00"
            benchmark.mkdir(parents=True)
            (root / ".local/dodge-benchmarks").mkdir(parents=True)
            runs = root / ".local/wsl-dodge-runs"
            runs.mkdir(parents=True)
            reports = [write_report(runs, seed, 20) for seed in (1, 2)]
            for seed, path in zip((1, 2), reports):
                data = json.loads(path.read_text())
                data["actions"] = [action(0.1 if seed == 1 else 0.9, "move-right")]
                hold = {
                    "advice": "Hold",
                    "action": None,
                    "observation_fresh": True,
                    "state": "playing",
                    "capture_ms": 100,
                    "before_player": {"X": 0.5, "Y": 0.5},
                    "threats": [],
                }
                data["decisions"] = [hold, dict(hold, observation_fresh=False)]
                path.write_text(json.dumps(data))
            (benchmark / "manifest.json").write_text(json.dumps(MANIFEST))
            (benchmark / "summary.json").write_text(
                json.dumps(summarize(MANIFEST, reports))
            )
            train, test, provenance = load_examples(root, benchmark, [2])
            self.assertEqual(len(train), 1)
            self.assertEqual(len(test), 1)
            with patch(
                "train_dodge_imitation.MAX_REPORT_BYTES", 1
            ), self.assertRaisesRegex(ValueError, "resource limits"):
                load_examples(root, benchmark, [2])
            hold_train, hold_test, _ = load_examples(
                root, benchmark, [2], include_holds=True
            )
            self.assertEqual([label for _, label in hold_train].count("hold"), 1)
            self.assertEqual([label for _, label in hold_test].count("hold"), 1)
            self.assertEqual(
                [r["split"] for r in provenance["reports"]], ["train", "test"]
            )
            model_dir = root / ".local/dodge-offline-models/model"
            model_dir.mkdir(parents=True)
            (model_dir / "model.json").write_text(json.dumps(fit_model(train)))
            (model_dir / "model-neighbors.json").write_text(
                json.dumps(fit_neighbors(train))
            )
            (model_dir / "report.json").write_text(
                json.dumps(
                    {
                        "provenance": provenance,
                        "model_sha256": hashlib.sha256(
                            (model_dir / "model.json").read_bytes()
                        ).hexdigest(),
                        "neighbor_model_sha256": hashlib.sha256(
                            (model_dir / "model-neighbors.json").read_bytes()
                        ).hexdigest(),
                    }
                )
            )
            shadow = evaluate_saved_model(root, benchmark, [2], model_dir)
            self.assertEqual(shadow["linear"]["examples"], 1)
            with self.assertRaisesRegex(ValueError, "overlap"):
                evaluate_saved_model(root, benchmark, [1], model_dir)
            previous = json.loads((model_dir / "report.json").read_text())
            previous["provenance"]["pack"]["Sha256"] = "b" * 64
            (model_dir / "report.json").write_text(json.dumps(previous))
            with self.assertRaisesRegex(ValueError, "different teacher"):
                evaluate_saved_model(root, benchmark, [2], model_dir)
            with self.assertRaises(ValueError):
                load_examples(root, benchmark, [1, 2])
            modified = json.loads(reports[0].read_text())
            modified["stop_reason"] = "response-timeout"
            reports[0].write_text(json.dumps(modified))
            with self.assertRaises(ValueError):
                load_examples(root, benchmark, [2])


if __name__ == "__main__":
    unittest.main()
