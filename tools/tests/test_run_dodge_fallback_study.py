from copy import deepcopy
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from run_dodge_fallback_study import run_study, verify_pair, verify_model_pair


def packs():
    model = {"feature_version": "example", "weights": [1]}
    standard = {
        "context": {"avoidance_policy": "directional-outcome-v34", "goal": "survive"},
        "experimental_outcome_model": model,
        "version": "one",
        "title": "one",
    }
    turn = deepcopy(standard)
    turn["context"]["avoidance_policy"] = "directional-outcome-turn-v36"
    turn["version"] = "two"
    return standard, turn


class FallbackStudyTests(unittest.TestCase):
    def test_model_comparison_rejects_other_changes(self):
        base, candidate = packs()
        base["context"]["avoidance_policy"] = candidate["context"][
            "avoidance_policy"
        ] = "directional-outcome-reference-v37"
        model = {
            "schema_version": 1,
            "feature_version": "dodge-visual-state-action-context-v2-with-hold-speed400",
            "feature_player_speed": 400,
            "algorithm": "binary-logistic-observational-survival",
            "horizon_ms": 1000,
            "means": [0] * 71,
            "scales": [1] * 71,
            "weights": [0] * 72,
        }
        base["experimental_outcome_model"] = deepcopy(model)
        candidate["experimental_outcome_model"] = {
            **model,
            "algorithm": "linear-observational-action-advantage",
        }
        verify_model_pair(base, candidate)
        candidate["context"]["goal"] = "changed"
        with self.assertRaisesRegex(ValueError, "beyond"):
            verify_model_pair(base, candidate)
        candidate["context"]["goal"] = base["context"]["goal"]
        candidate["experimental_outcome_model"]["horizon_ms"] = 2000
        with self.assertRaisesRegex(ValueError, "compatible"):
            verify_model_pair(base, candidate)
        base["experimental_outcome_model"][
            "algorithm"
        ] = "linear-observational-action-advantage"
        candidate["experimental_outcome_model"][
            "algorithm"
        ] = "linear-observational-episode-advantage"
        candidate["experimental_outcome_model"]["horizon_ms"] = 0
        verify_model_pair(base, candidate)
        candidate["experimental_outcome_model"]["feature_version"] = "different"
        with self.assertRaises(ValueError):
            verify_model_pair(base, candidate)

    def test_only_fallback_may_differ(self):
        standard, turn = packs()
        verify_pair(standard, turn)
        reference, escape = deepcopy(standard), deepcopy(turn)
        reference["context"]["avoidance_policy"] = "directional-outcome-reference-v37"
        escape["context"]["avoidance_policy"] = "directional-outcome-escape-v38"
        verify_pair(reference, escape)
        turn["experimental_outcome_model"]["weights"][0] = 2
        with self.assertRaisesRegex(ValueError, "identical"):
            verify_pair(standard, turn)
        standard, turn = packs()
        turn["context"]["goal"] = "different"
        with self.assertRaisesRegex(ValueError, "beyond"):
            verify_pair(standard, turn)

    def test_below_target_does_not_touch_final_games(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            app, first, second = [
                root / name for name in ("app.exe", "first.json", "second.json")
            ]
            for path in (app, first, second):
                path.write_text("{}")
            standard, turn = packs()
            calls = []

            def fake_games(workspace, application, pack, manifest, directory, **kwargs):
                calls.append((pack, manifest, directory))
                return {
                    "mean_score": 30 if pack == first else 40,
                    "games": [{"score": 30} for _ in manifest["seeds"]],
                }

            with patch(
                "run_dodge_fallback_study.load_pack",
                side_effect=lambda p: standard if p == first else turn,
            ), patch(
                "run_dodge_fallback_study.run_benchmark", side_effect=fake_games
            ), patch(
                "run_dodge_fallback_study.compare", return_value={"paired": True}
            ), patch(
                "run_dodge_fallback_study.environment_fingerprint",
                return_value={"same": "yes"},
            ):
                output, status = run_study(root, app, first, second)
            self.assertEqual(status["status"], "development_below_target")
            self.assertEqual(len(calls), 2)
            self.assertEqual(calls[0][1], calls[1][1])
            self.assertEqual(json.loads((output / "study.json").read_text()), status)

    def test_candidate_only_does_not_revalidate_a_worse_candidate(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            app, first, second = [
                root / name for name in ("app.exe", "first.json", "second.json")
            ]
            for path in (app, first, second):
                path.write_text("{}")
            standard, turn = packs()
            calls = []

            def fake_games(workspace, application, pack, manifest, directory, **kwargs):
                calls.append(pack)
                return {
                    "mean_score": 60 if pack == first else 55,
                    "games": [{"score": 55} for _ in manifest["seeds"]],
                }

            with patch(
                "run_dodge_fallback_study.load_pack",
                side_effect=lambda p: standard if p == first else turn,
            ), patch(
                "run_dodge_fallback_study.run_benchmark", side_effect=fake_games
            ), patch(
                "run_dodge_fallback_study.compare", return_value={"paired": True}
            ), patch(
                "run_dodge_fallback_study.environment_fingerprint",
                return_value={"same": "yes"},
            ):
                _, status = run_study(root, app, first, second, candidate_only=True)
            self.assertEqual(status["status"], "candidate_not_better")
            self.assertEqual(calls, [first, second])
            self.assertIsNone(status["final"])

    def test_qualified_winner_gets_a_separate_final_set(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            app, first, second = [
                root / name for name in ("app.exe", "first.json", "second.json")
            ]
            for path in (app, first, second):
                path.write_text("{}")
            standard, turn = packs()
            calls = []

            def fake_games(workspace, application, pack, manifest, directory, **kwargs):
                calls.append((pack, manifest, directory))
                score = 55 if pack == second else 40
                return {
                    "mean_score": score,
                    "games": [{"score": score} for _ in manifest["seeds"]],
                }

            with patch(
                "run_dodge_fallback_study.load_pack",
                side_effect=lambda p: standard if p == first else turn,
            ), patch(
                "run_dodge_fallback_study.run_benchmark", side_effect=fake_games
            ), patch(
                "run_dodge_fallback_study.compare", return_value={"paired": True}
            ), patch(
                "run_dodge_fallback_study.environment_fingerprint",
                return_value={"same": "yes"},
            ):
                _, status = run_study(root, app, first, second)
            self.assertEqual(status["status"], "final_target_met")
            self.assertEqual(calls[-1][0], second)
            self.assertEqual(len(calls[-1][1]["seeds"]), 10)
            self.assertFalse(set(calls[-1][1]["seeds"]) & set(calls[0][1]["seeds"]))


if __name__ == "__main__":
    unittest.main()
