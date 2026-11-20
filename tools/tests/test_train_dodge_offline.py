import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import train_dodge_offline as trainer
from run_dodge_benchmark import fingerprints, run_benchmark


class OfflineTrainingTests(unittest.TestCase):
    def test_fingerprints_detect_hud_and_visual_asset_changes(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            game = root / ".local/wsl-dodge-step-game"
            game.mkdir(parents=True)
            for name in (
                "project.godot",
                "main.gd",
                "main.tscn",
                "player.gd",
                "player.tscn",
                "mob.gd",
                "mob.tscn",
                "hud.gd",
                "hud.tscn",
                "maelstrom_step_controller.gd",
            ):
                (game / name).write_text(name)
            for folder in ("art", "fonts"):
                (game / folder).mkdir()
            (game / "art/eye.png").write_bytes(b"eye")
            (root / "tools").mkdir()
            for name in (
                "wsl_dodge_launch.sh",
                "wsl_dodge_session.sh",
                "wsl_dodge_guest.py",
                "run_dodge_benchmark.py",
                "benchmark_dodge_scores.py",
            ):
                (root / "tools" / name).write_text(name)
            app = root / "app.exe"
            app.write_bytes(b"app")
            app.with_suffix(".dll").write_bytes(b"dll")
            pack = root / "pack.json"
            pack.write_bytes(b"pack")
            before = fingerprints(root, app, pack)
            (game / "hud.gd").write_text("changed")
            after_hud = fingerprints(root, app, pack)
            self.assertNotEqual(before, after_hud)
            (game / "art/new.png").write_bytes(b"new")
            self.assertNotEqual(after_hud, fingerprints(root, app, pack))

    def test_exclusive_study_lock_rejects_overlapping_training(self):
        with tempfile.TemporaryDirectory() as temp:
            with trainer.exclusive_study(temp):
                with self.assertRaises(RuntimeError):
                    with trainer.exclusive_study(temp):
                        pass
            with trainer.exclusive_study(temp):
                pass

    def workspace(self, root):
        root = Path(root)
        (root / "tools/fixtures").mkdir(parents=True)
        fixture = (
            Path(__file__).resolve().parents[1]
            / "fixtures/dodge-benchmark-development.json"
        )
        (root / "tools/fixtures/dodge-benchmark-development.json").write_text(
            fixture.read_text()
        )
        (root / "tools/generate_dodge_candidates.py").write_text("fixture")
        (root / "application.exe").write_text("application")
        (root / "source.json").write_text("source")
        return root

    def test_choose_never_hides_incomplete_candidates(self):
        complete = {
            "mean_score": 60,
            "minimum_score": 20,
            "completed_games": 2,
            "expected_games": 2,
            "observation_cadence": "advance-before-decision",
            "run_limits": {"actions": 2400, "requests": 9600, "seconds": 1200},
        }
        self.assertIsNone(trainer.choose([complete, {"mean_score": None}]))
        lesser = dict(complete, mean_score=50, minimum_score=50)
        self.assertIs(trainer.choose([complete, lesser]), complete)
        self.assertIsNone(
            trainer.choose(
                [complete, dict(lesser, observation_cadence="reuse-action-frame")]
            )
        )
        outlier = dict(
            complete,
            mean_score=52.5,
            minimum_score=5,
            completed_games=4,
            expected_games=4,
            scores=[41, 157, 5, 7],
        )
        steadier = dict(
            outlier, mean_score=47.5, minimum_score=20, scores=[35, 110, 20, 25]
        )
        self.assertIs(trainer.choose([outlier, steadier]), outlier)
        self.assertIs(trainer.choose([outlier, steadier], resilient=True), steadier)

    def test_focus_study_runs_sequentially_without_validation_or_promotion(self):
        with tempfile.TemporaryDirectory() as temp:
            root = self.workspace(temp)
            calls = []

            def generate(
                source,
                library,
                horizons,
                radii,
                policy,
                risk_weights,
                edge_weights,
                margins,
            ):
                path = Path(library) / "candidate"
                path.mkdir(parents=True)
                (path / "pack.json").write_text("candidate")
                return [path]

            def run(workspace, application, pack, manifest, output, **kwargs):
                calls.append((str(pack), list(manifest["seeds"]), manifest["speed"]))
                return {
                    "mean_score": 10 if len(calls) == 1 else 60,
                    "median_score": 10,
                    "minimum_score": 5,
                    "completed_games": 2,
                    "expected_games": 2,
                    "policy": "candidate",
                    "observation_cadence": "advance-before-decision",
                    "run_limits": {"actions": 2400, "requests": 9600, "seconds": 1200},
                    "games": [
                        {"score": 10 if len(calls) == 1 else 60},
                        {"score": 10 if len(calls) == 1 else 60},
                    ],
                }

            with patch.object(trainer, "generate", generate), patch.object(
                trainer, "fingerprints", return_value={"game": "fixed"}
            ):
                output, status = trainer.train(
                    root,
                    root / "application.exe",
                    root / "source.json",
                    horizons=[700],
                    radii=[50],
                    scope="focus",
                    minutes=45,
                    runner=run,
                )
            self.assertEqual([item[1] for item in calls], [[247, 781], [247, 781]])
            self.assertEqual([item[2] for item in calls], [0.25, 0.25])
            self.assertEqual(status["status"], "complete")
            self.assertEqual(status["selected_for_development"]["mean_score"], 60)
            self.assertIn("No automatic validation", status["note"])
            self.assertEqual(
                json.loads((output / "study.json").read_text())["scope"], "focus"
            )

    def test_focus_can_reuse_verified_baseline_but_development_cannot(self):
        with tempfile.TemporaryDirectory() as temp:
            root = self.workspace(temp)
            calls = []

            def generate(
                source,
                library,
                horizons,
                radii,
                policy,
                risk_weights,
                edge_weights,
                margins,
            ):
                candidate = Path(library) / "candidate"
                candidate.mkdir(parents=True)
                (candidate / "pack.json").write_text("candidate")
                return [candidate]

            summary = {
                "mean_score": 10,
                "median_score": 10,
                "minimum_score": 10,
                "completed_games": 2,
                "expected_games": 2,
                "policy": "candidate",
                "observation_cadence": "advance-before-decision",
                "run_limits": {"actions": 2400, "requests": 9600, "seconds": 1200},
                "games": [{"score": 10}, {"score": 10}],
            }

            def run(*args, **kwargs):
                calls.append(args[2])
                return summary

            baseline = root / "old-baseline"
            with patch.object(trainer, "generate", generate), patch.object(
                trainer, "fingerprints", return_value={}
            ), patch.object(
                trainer, "verified_baseline", return_value=summary
            ) as verify:
                output, status = trainer.train(
                    root,
                    root / "application.exe",
                    root / "source.json",
                    horizons=[700],
                    radii=[50],
                    scope="focus",
                    minutes=45,
                    runner=run,
                    baseline_from=baseline,
                )
                self.assertEqual(len(calls), 1)
                verify.assert_called_once()
                self.assertEqual(status["status"], "complete")
                self.assertEqual(
                    status["baseline_reused_from"], str(baseline.resolve())
                )
                self.assertTrue((output / "benchmarks/00/summary.json").is_file())
                with self.assertRaisesRegex(ValueError, "only available"):
                    trainer.train(
                        root,
                        root / "application.exe",
                        root / "source.json",
                        horizons=[700],
                        radii=[50],
                        scope="development",
                        minutes=45,
                        runner=run,
                        baseline_from=baseline,
                    )

    def test_incomplete_game_stops_study_without_a_winner(self):
        with tempfile.TemporaryDirectory() as temp:
            root = self.workspace(temp)

            def generate(
                source,
                library,
                horizons,
                radii,
                policy,
                risk_weights,
                edge_weights,
                margins,
            ):
                path = Path(library) / "candidate"
                path.mkdir(parents=True)
                (path / "pack.json").write_text("candidate")
                return [path]

            calls = []

            def run(*args, **kwargs):
                calls.append(1)
                return {"mean_score": None, "completed_games": 0, "expected_games": 2}

            with patch.object(trainer, "generate", generate), patch.object(
                trainer, "fingerprints", return_value={}
            ):
                _, status = trainer.train(
                    root,
                    root / "application.exe",
                    root / "source.json",
                    horizons=[700],
                    radii=[50],
                    scope="focus",
                    minutes=45,
                    runner=run,
                )
            self.assertEqual(len(calls), 1)
            self.assertEqual(status["status"], "incomplete")
            self.assertIsNone(status["selected_for_development"])

    def test_stop_requested_during_a_completed_game_prevents_the_next_game(self):
        with tempfile.TemporaryDirectory() as temp:
            root = self.workspace(temp)
            stop = [False]
            calls = []

            def generate(
                source,
                library,
                horizons,
                radii,
                policy,
                risk_weights,
                edge_weights,
                margins,
            ):
                path = Path(library) / "candidate"
                path.mkdir(parents=True)
                (path / "pack.json").write_text("candidate")
                return [path]

            def run(*args, **kwargs):
                calls.append(1)
                stop[0] = True
                return {
                    "mean_score": 60,
                    "minimum_score": 60,
                    "completed_games": 2,
                    "expected_games": 2,
                    "observation_cadence": "advance-before-decision",
                    "run_limits": {"actions": 2400, "requests": 9600, "seconds": 1200},
                }

            with patch.object(trainer, "generate", generate), patch.object(
                trainer, "fingerprints", return_value={}
            ):
                _, status = trainer.train(
                    root,
                    root / "application.exe",
                    root / "source.json",
                    horizons=[700],
                    radii=[50],
                    scope="focus",
                    minutes=45,
                    runner=run,
                    stop_requested=lambda: stop[0],
                )
            self.assertEqual(len(calls), 1)
            self.assertEqual(status["status"], "stopped_before_next_game")
            self.assertIsNone(status["selected_for_development"])

    def test_study_stops_if_a_planned_pack_changes_during_a_game(self):
        with tempfile.TemporaryDirectory() as temp:
            root = self.workspace(temp)
            candidate = []

            def generate(
                source,
                library,
                horizons,
                radii,
                policy,
                risk_weights,
                edge_weights,
                margins,
            ):
                path = Path(library) / "candidate"
                path.mkdir(parents=True)
                (path / "pack.json").write_text("candidate")
                candidate.append(path / "pack.json")
                return [path]

            def run(*args, **kwargs):
                candidate[0].write_text("changed")
                return {
                    "mean_score": 60,
                    "minimum_score": 60,
                    "completed_games": 2,
                    "expected_games": 2,
                    "observation_cadence": "advance-before-decision",
                    "run_limits": {"actions": 2400, "requests": 9600, "seconds": 1200},
                }

            with patch.object(trainer, "generate", generate), patch.object(
                trainer, "fingerprints", return_value={}
            ):
                with self.assertRaisesRegex(RuntimeError, "planned pack changed"):
                    trainer.train(
                        root,
                        root / "application.exe",
                        root / "source.json",
                        horizons=[700],
                        radii=[50],
                        scope="focus",
                        minutes=45,
                        runner=run,
                    )
            records = list((root / ".local/dodge-training").glob("*/study.json"))
            self.assertEqual(len(records), 1)
            status = json.loads(records[0].read_text())
            self.assertEqual(status["status"], "error")
            self.assertIsNone(status["selected_for_development"])

    def test_benchmark_does_not_start_if_stopped_or_budget_cannot_fit_a_game(self):
        with tempfile.TemporaryDirectory() as temp:
            root = self.workspace(temp)
            manifest = json.loads(
                (root / "tools/fixtures/dodge-benchmark-development.json").read_text()
            )
            with patch("run_dodge_benchmark.fingerprints", return_value={}), patch(
                "run_dodge_benchmark.subprocess.run"
            ) as launch:
                result = run_benchmark(
                    root,
                    root / "application.exe",
                    root / "source.json",
                    manifest,
                    root / "out1",
                    stop_requested=lambda: True,
                )
                self.assertIsNone(result["mean_score"])
                self.assertTrue((root / "out1/summary.json").exists())
                result = run_benchmark(
                    root,
                    root / "application.exe",
                    root / "source.json",
                    manifest,
                    root / "out2",
                    deadline=0,
                )
                self.assertIsNone(result["mean_score"])
                launch.assert_not_called()


if __name__ == "__main__":
    unittest.main()
