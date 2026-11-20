import json
from pathlib import Path
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from run_dodge_outcome_curriculum import run_curriculum, select_model


class OutcomeCurriculumTests(unittest.TestCase):
    def test_balanced_selection_treats_deaths_and_survival_equally(self):
        models = [
            {
                "name": "overall",
                "held_out_recorded_games": {"brier_score": 0.05, "balanced_brier": 0.4},
            },
            {
                "name": "balanced",
                "held_out_recorded_games": {"brier_score": 0.06, "balanced_brier": 0.2},
            },
        ]
        self.assertEqual(select_model(models, "brier")["name"], "overall")
        self.assertEqual(select_model(models, "balanced-brier")["name"], "balanced")
        models[0]["held_out_recorded_games"]["game_balanced_brier"] = 0.1
        models[1]["held_out_recorded_games"]["game_balanced_brier"] = 0.3
        self.assertEqual(select_model(models, "game-balanced-brier")["name"], "overall")
        self.assertIsNone(
            select_model(
                [{"held_out_recorded_games": {"balanced_brier": None}}],
                "balanced-brier",
            )
        )

    def test_low_disk_stops_before_any_game(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            app, explore, baseline = [
                root / name for name in ("app.exe", "explore.json", "base.json")
            ]
            for path in (app, explore, baseline):
                path.write_text("{}")

            def fake_pack(path):
                return {
                    "context": {
                        "avoidance_policy": (
                            "directional-explore-v35"
                            if path == explore
                            else "directional-v26"
                        )
                    }
                }

            with patch(
                "run_dodge_outcome_curriculum.load_pack", side_effect=fake_pack
            ), patch(
                "run_dodge_outcome_curriculum.shutil.disk_usage",
                return_value=SimpleNamespace(free=0),
            ), patch(
                "run_dodge_outcome_curriculum.run_benchmark"
            ) as games:
                _, status = run_curriculum(
                    root, app, explore, baseline, minutes=300, rounds=1
                )
            self.assertEqual(status["status"], "resource_limit")
            games.assert_not_called()

    def run_fake(
        self,
        candidate_score,
        baseline_score,
        final_score,
        skip_baseline=False,
        separate_collector=False,
        retain_reference=False,
        experience_games=8,
        heldout_games=2,
        compare_balanced_training=False,
        action_advantage=False,
    ):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            app, explore, baseline = [
                root / name for name in ("app.exe", "explore.json", "base.json")
            ]
            for path in (app, explore, baseline):
                path.write_text("{}")
            collector = root / "collector.exe"
            collector.write_text("{}")
            candidate = root / "candidate"
            candidate.mkdir()
            (candidate / "pack.json").write_text("{}")
            calls = []

            def fake_pack(path):
                return {
                    "context": {
                        "avoidance_policy": (
                            "directional-explore-v35"
                            if path == explore
                            else "directional-v26"
                        )
                    }
                }

            def fake_games(workspace, application, pack, manifest, output, **kwargs):
                calls.append((pack, manifest, output, application))
                output.mkdir(parents=True)
                (output / "manifest.json").write_text(json.dumps(manifest))
                score = (
                    final_score
                    if output.name == "final"
                    else (
                        candidate_score
                        if output.name == "candidate"
                        else baseline_score
                    )
                )
                reports = []
                for seed in manifest["seeds"]:
                    report = output / f"report-{seed}.json"
                    report.write_text(
                        json.dumps(
                            {
                                "policy": "directional-outcome-v34",
                                "stop_reason": "game-over",
                                "decisions": [],
                            }
                        )
                    )
                    reports.append({"score": score, "report": str(report)})
                return {
                    "mean_score": score,
                    "games": reports,
                }

            model_result = {
                "output": str(root / "model"),
                "held_out_recorded_games": {"brier_score": 0.2},
                "held_out_examples_sha256": "same-heldout",
            }
            response = subprocess.CompletedProcess([], 0, json.dumps(model_result), "")
            with patch(
                "run_dodge_outcome_curriculum.load_pack", side_effect=fake_pack
            ), patch(
                "run_dodge_outcome_curriculum.run_benchmark", side_effect=fake_games
            ), patch(
                "run_dodge_outcome_curriculum.subprocess.run", return_value=response
            ) as train, patch(
                "run_dodge_outcome_curriculum.train_action_advantage_locked",
                return_value=(
                    root / "action-model",
                    {
                        "training_examples": 100,
                        "held_out_action_support": {"matching_observations": 2},
                    },
                ),
            ) as action_train, patch(
                "run_dodge_outcome_curriculum.build", return_value=candidate
            ) as build_pack, patch(
                "run_dodge_outcome_curriculum.compare", return_value={"matched": True}
            ), patch(
                "run_dodge_outcome_curriculum.environment_fingerprint",
                return_value={"same": "yes"},
            ):
                output, status = run_curriculum(
                    root,
                    app,
                    explore,
                    baseline,
                    minutes=300,
                    rounds=1,
                    player_speed=400,
                    skip_baseline_below_target=skip_baseline,
                    horizon_ms=1000,
                    experience_app=collector if separate_collector else None,
                    retain_reference=retain_reference,
                    experience_games=experience_games,
                    heldout_games=heldout_games,
                    compare_balanced_training=compare_balanced_training,
                    action_advantage=action_advantage,
                )
            if retain_reference:
                self.assertTrue(build_pack.call_args.kwargs["retain_reference"])
            if action_advantage:
                train.assert_not_called()
                self.assertEqual(action_train.call_count, 1)
                self.assertEqual(
                    action_train.call_args.args[2],
                    calls[0][1]["seeds"][-heldout_games:],
                )
            for call in train.call_args_list:
                command = call.args[0]
                start = command.index("--test-seeds") + 1
                stop = command.index("--contextual")
                self.assertEqual(
                    [int(value) for value in command[start:stop]],
                    calls[0][1]["seeds"][-heldout_games:],
                )
            self.assertEqual(
                sum(
                    "--balance-outcomes" in call.args[0]
                    for call in train.call_args_list
                ),
                2 if compare_balanced_training else 0,
            )
            self.assertEqual(
                json.loads((output / "curriculum.json").read_text()), status
            )
            self.assertEqual(status["feature_player_speed"], 400)
            self.assertEqual(status["horizon_ms"], 1000)
            return status, [
                (p.name, m["seeds"], o.name, app.name) for p, m, o, app in calls
            ]

    def test_below_target_does_not_run_final_games(self):
        status, calls = self.run_fake(40, 20, 60)
        self.assertEqual(status["status"], "development_below_target")
        self.assertIsNone(status["final"])
        self.assertEqual([x[2] for x in calls], ["experience", "candidate", "baseline"])
        self.assertFalse(set(calls[0][1]) & set(calls[1][1]))

    def test_only_a_qualified_candidate_reaches_fresh_final_games(self):
        status, calls = self.run_fake(55, 30, 51)
        self.assertEqual(status["status"], "final_target_met")
        self.assertEqual(len(status["final"]["scores"]), 10)
        self.assertEqual(status["final"]["distribution"]["games"], 10)
        self.assertEqual(status["rounds"][0]["candidate_distribution"]["games"], 4)
        self.assertEqual(status["rounds"][0]["baseline_distribution"]["games"], 4)
        final_seeds = set(calls[-1][1])
        self.assertFalse(final_seeds & set(calls[0][1]))
        self.assertFalse(final_seeds & set(calls[1][1]))

    def test_target_candidate_can_be_validated_even_if_baseline_is_higher(self):
        status, calls = self.run_fake(55, 60, 51)
        self.assertEqual(status["status"], "final_target_met")
        self.assertEqual(calls[-1][2], "final")

    def test_baseline_can_be_skipped_only_after_complete_candidate_failure(self):
        status, calls = self.run_fake(40, 20, 60, skip_baseline=True)
        self.assertEqual(status["status"], "development_below_target")
        self.assertEqual([x[2] for x in calls], ["experience", "candidate"])
        self.assertEqual(
            status["rounds"][0]["status"], "candidate_below_target_baseline_skipped"
        )

    def test_experience_can_use_a_fixed_separate_collector(self):
        status, calls = self.run_fake(40, 20, 60, separate_collector=True)
        self.assertEqual(
            [row[3] for row in calls], ["collector.exe", "app.exe", "app.exe"]
        )
        self.assertTrue(status["experience_application"].endswith("collector.exe"))

    def test_reference_policy_is_built_and_compared_to_baseline(self):
        status, calls = self.run_fake(40, 20, 60, retain_reference=True)
        self.assertTrue(status["retain_reference"])
        self.assertEqual([x[2] for x in calls], ["experience", "candidate", "baseline"])

    def test_larger_experience_batch_reserves_whole_game_holdouts(self):
        status, calls = self.run_fake(40, 20, 60, experience_games=16, heldout_games=8)
        self.assertEqual(len(calls[0][1]), 16)
        self.assertEqual(status["experience_games"], 16)
        self.assertEqual(status["heldout_games"], 8)
        self.assertFalse(set(calls[0][1]) & set(calls[1][1]))

    def test_outcome_balanced_models_are_optional_and_compared_on_same_games(self):
        status, _ = self.run_fake(40, 20, 60, compare_balanced_training=True)
        self.assertTrue(status["compare_balanced_training"])
        self.assertEqual(len(status["rounds"][0]["models"]), 6)
        self.assertEqual(
            sum(model["balance_outcomes"] for model in status["rounds"][0]["models"]), 2
        )

    def test_action_advantage_runs_inside_study_and_uses_separate_games(self):
        status, calls = self.run_fake(
            40, 20, 60, retain_reference=True, action_advantage=True
        )
        self.assertEqual(status["training_mode"], "action-advantage")
        self.assertIsNone(status["selection_metric"])
        self.assertEqual(
            status["rounds"][0]["models"][0]["held_out_action_support"][
                "matching_observations"
            ],
            2,
        )
        self.assertFalse(set(calls[0][1]) & set(calls[1][1]))

    def test_fallbacks_share_model_and_openings_before_selection(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            app, explore, baseline = [
                root / name for name in ("app.exe", "explore.json", "base.json")
            ]
            for item in (app, explore, baseline):
                item.write_text("{}")
            normal, turning = root / "normal", root / "turning"
            normal.mkdir()
            turning.mkdir()
            (normal / "pack.json").write_text("{}")
            (turning / "pack.json").write_text("{}")
            common = {
                "context": {
                    "avoidance_policy": "directional-outcome-v34",
                    "goal": "survive",
                },
                "experimental_outcome_model": {"weights": [1]},
                "version": "one",
            }
            turn = {
                **common,
                "context": {
                    "avoidance_policy": "directional-outcome-turn-v36",
                    "goal": "survive",
                },
                "version": "two",
            }

            def fake_pack(path):
                if path == explore:
                    return {"context": {"avoidance_policy": "directional-explore-v35"}}
                if path == baseline:
                    return {"context": {"avoidance_policy": "directional-v26"}}
                return turn if path.parent == turning else common

            calls = []

            def fake_games(workspace, application, pack, manifest, output, **kwargs):
                calls.append((pack, manifest, output))
                output.mkdir(parents=True)
                score = 40 if output.name == "turn-candidate" else 20
                policy = (
                    "directional-outcome-turn-v36"
                    if output.name == "turn-candidate"
                    else "directional-outcome-v34"
                )
                reports = []
                for seed in manifest["seeds"]:
                    report = output / f"report-{seed}.json"
                    report.write_text(
                        json.dumps(
                            {
                                "policy": policy,
                                "stop_reason": "game-over",
                                "decisions": [],
                            }
                        )
                    )
                    reports.append({"score": score, "report": str(report)})
                return {"mean_score": score, "games": reports}

            response = subprocess.CompletedProcess(
                [],
                0,
                json.dumps(
                    {
                        "output": str(root / "model"),
                        "held_out_recorded_games": {"brier_score": 0.2},
                        "held_out_examples_sha256": "same",
                    }
                ),
                "",
            )
            with patch(
                "run_dodge_outcome_curriculum.load_pack", side_effect=fake_pack
            ), patch(
                "run_dodge_outcome_curriculum.run_benchmark", side_effect=fake_games
            ), patch(
                "run_dodge_outcome_curriculum.subprocess.run", return_value=response
            ), patch(
                "run_dodge_outcome_curriculum.build",
                side_effect=lambda *a, **kw: (
                    turning if kw.get("turn_aware") else normal
                ),
            ), patch(
                "run_dodge_outcome_curriculum.compare", return_value={"paired": True}
            ), patch(
                "run_dodge_outcome_curriculum.environment_fingerprint",
                return_value={"same": "yes"},
            ):
                _, status = run_curriculum(
                    root,
                    app,
                    explore,
                    baseline,
                    minutes=300,
                    rounds=1,
                    compare_fallbacks=True,
                    skip_baseline_below_target=True,
                )
            self.assertEqual(
                [row[2].name for row in calls],
                ["experience", "candidate", "turn-candidate"],
            )
            self.assertEqual(calls[1][1], calls[2][1])
            self.assertEqual(status["rounds"][0]["candidate_mean"], 40)
            self.assertEqual(
                status["rounds"][0]["candidate_pack"], str(turning / "pack.json")
            )


if __name__ == "__main__":
    unittest.main()
