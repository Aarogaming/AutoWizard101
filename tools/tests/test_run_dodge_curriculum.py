import json
import hashlib
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from benchmark_dodge_scores import summarize
from run_dodge_curriculum import load_completed_focus, run_curriculum
from tools.tests.test_benchmark_dodge_scores import write_report


class DodgeCurriculumTests(unittest.TestCase):
    def test_baseline_reuse_is_passed_only_to_focus(self):
        with tempfile.TemporaryDirectory() as temp:
            root, candidate = self.prepare(temp)
            calls = []

            def train(_workspace, _app, _source, **kwargs):
                calls.append(kwargs)
                winner = {
                    "pack": str(candidate),
                    "mean_score": 49,
                    "policy": "directional-v26",
                }
                return root / kwargs["scope"], {
                    "status": "complete",
                    "selected_for_development": winner,
                    "results": [winner],
                }

            baseline = root / "baseline"
            _, status = run_curriculum(
                root,
                root / "application.exe",
                root / "source.json",
                horizons=[700],
                radii=[50],
                policy="directional-v26",
                minutes=90,
                train_fn=train,
                baseline_from=baseline,
            )
            self.assertEqual(status["status"], "development_below_target")
            self.assertEqual(calls[0]["baseline_from"], baseline)
            self.assertNotIn("baseline_from", calls[1])

    def test_selected_planning_weights_are_preserved_for_development(self):
        with tempfile.TemporaryDirectory() as temp:
            root, candidate = self.prepare(temp)
            pack = json.loads(candidate.read_text())
            pack["context"]["avoidance_policy"] = "directional-configurable-v30"
            pack["context"]["constraints"].update(
                risk_weight_percent=200, edge_weight_percent=50
            )
            pack["context"]["constraints"]["edge_margin_px"] = 80
            pack["version"] = "0.8.0-h700-r50-q200-e50-m80"
            candidate.write_text(json.dumps(pack))
            seen = []

            def train(_workspace, _app, _source, **kwargs):
                seen.append(kwargs)
                winner = {
                    "pack": str(candidate),
                    "mean_score": 49,
                    "policy": "directional-configurable-v30",
                }
                return root / kwargs["scope"], {
                    "status": "complete",
                    "selected_for_development": winner,
                    "results": [winner],
                }

            _, status = run_curriculum(
                root,
                root / "application.exe",
                root / "source.json",
                horizons=[700],
                radii=[50],
                policy="directional-configurable-v30",
                risk_weights=[50, 200],
                edge_weights=[50, 200],
                minutes=90,
                train_fn=train,
            )
            self.assertEqual(status["status"], "development_below_target")
            self.assertEqual(seen[1]["risk_weights"], [200])
            self.assertEqual(seen[1]["edge_weights"], [50])
            self.assertEqual(seen[1]["margins"], [80])

    def test_from_focus_skips_replaying_the_focused_games(self):
        with tempfile.TemporaryDirectory() as temp:
            root, candidate = self.prepare(temp)
            focus_path = root / "saved-focus"
            focus_path.mkdir()
            train, calls = self.fake_train(root, candidate, 49)
            focus = {
                "status": "complete",
                "selected_for_development": {"pack": str(candidate)},
            }
            with patch("run_dodge_curriculum.load_completed_focus", return_value=focus):
                _, status = run_curriculum(
                    root,
                    root / "application.exe",
                    root / "source.json",
                    horizons=[700],
                    radii=[50],
                    policy="directional-v26",
                    minutes=90,
                    train_fn=train,
                    from_focus=focus_path,
                )
            self.assertEqual(calls, ["development"])
            self.assertTrue(status["focus_reused_for_screening_only"])
            self.assertEqual(status["status"], "development_below_target")

    def test_reuses_only_a_complete_unchanged_focus_study(self):
        with tempfile.TemporaryDirectory() as temp:
            root, candidate_unused = self.prepare(temp)
            study = root / ".local/dodge-training/study"
            (study / "packs").mkdir(parents=True)
            source = (root / "source.json").resolve()
            candidate = study / "packs/candidate.json"
            candidate.write_text(candidate_unused.read_text())
            run_root = root / ".local/wsl-dodge-runs"
            manifest = {
                "schema_version": 1,
                "id": "focus",
                "purpose": "focus",
                "seeds": [247, 781],
                "mode": "stepped",
                "speed": 0.25,
                "target_mean_score": 50,
            }
            rows, hashes = [], {}
            for index, (pack, scores) in enumerate(
                ((source, [6, 13]), (candidate, [41, 157]))
            ):
                digest = hashlib.sha256(pack.read_bytes()).hexdigest()
                hashes[str(pack)] = digest
                folder = run_root / str(index)
                folder.mkdir(parents=True)
                reports = [
                    write_report(
                        folder,
                        seed,
                        score,
                        pack={"Id": "test", "Version": str(index), "Sha256": digest},
                    )
                    for seed, score in zip(manifest["seeds"], scores)
                ]
                benchmark = study / "benchmarks" / str(index)
                benchmark.mkdir(parents=True)
                (benchmark / "manifest.json").write_text(json.dumps(manifest))
                (benchmark / "summary.json").write_text(
                    json.dumps(summarize(manifest, reports))
                )
                rows.append({"pack": str(pack), "benchmark": str(benchmark)})
            (study / "study.json").write_text(
                json.dumps(
                    {
                        "status": "complete",
                        "scope": "focus",
                        "manifest": manifest,
                        "planned_packs": [str(source), str(candidate)],
                        "pack_sha256": hashes,
                        "results": rows,
                        "selected_for_development": {"pack": str(candidate)},
                    }
                )
            )
            result = load_completed_focus(root, source, study)
            self.assertEqual(result["selected_for_development"]["mean_score"], 99)
            candidate.write_text("changed")
            with self.assertRaisesRegex(ValueError, "pack changed"):
                load_completed_focus(root, source, study)

    def prepare(self, root):
        root = Path(root)
        (root / "application.exe").write_text("application")
        (root / "source.json").write_text("source")
        candidate = root / "candidate.json"
        candidate.write_text(
            json.dumps(
                {
                    "context": {
                        "avoidance_policy": "directional-v26",
                        "constraints": {
                            "prediction_horizon_ms": 700,
                            "threat_radius_px": 50,
                            "edge_margin_px": 25,
                        },
                    },
                    "version": "0.4.0-h700-r50",
                }
            )
        )
        return root, candidate

    def fake_train(self, root, candidate, development_score):
        calls = []

        def train(_workspace, _app, _source, **kwargs):
            calls.append(kwargs["scope"])
            winner = {
                "pack": str(candidate),
                "mean_score": 35 if kwargs["scope"] == "focus" else development_score,
                "policy": "directional-v26",
            }
            return root / kwargs["scope"], {
                "status": "complete",
                "selected_for_development": winner,
                "results": [winner],
            }

        return train, calls

    def test_never_reads_validation_when_development_is_below_target(self):
        with tempfile.TemporaryDirectory() as temp:
            root, candidate = self.prepare(temp)
            train, calls = self.fake_train(root, candidate, 49)

            def validation(*args, **kwargs):
                self.fail("Validation must not run")

            _, status = run_curriculum(
                root,
                root / "application.exe",
                root / "source.json",
                horizons=[700],
                radii=[50],
                policy="directional-v26",
                minutes=300,
                train_fn=train,
                benchmark_fn=validation,
            )
            self.assertEqual(calls, ["focus", "development"])
            self.assertEqual(status["status"], "development_below_target")
            self.assertIsNone(status["validation"])

    def test_runs_reserved_validation_only_after_development_target(self):
        with tempfile.TemporaryDirectory() as temp:
            root, candidate = self.prepare(temp)
            folder = root / "tools/fixtures"
            folder.mkdir(parents=True)
            original = (
                Path(__file__).resolve().parents[1]
                / "fixtures/dodge-benchmark-validation.json"
            )
            (folder / "dodge-benchmark-validation.json").write_text(
                original.read_text()
            )
            train, calls = self.fake_train(root, candidate, 55)
            seen = []

            def validation(_workspace, _app, pack, manifest, _output, **kwargs):
                seen.append((pack, manifest["seeds"], kwargs["stop_requested"]()))
                return {"mean_score": 52}

            _, status = run_curriculum(
                root,
                root / "application.exe",
                root / "source.json",
                horizons=[700],
                radii=[50],
                policy="directional-v26",
                minutes=300,
                train_fn=train,
                benchmark_fn=validation,
            )
            self.assertEqual(calls, ["focus", "development"])
            self.assertEqual(len(seen), 1)
            self.assertEqual(len(seen[0][1]), 10)
            self.assertFalse(seen[0][2])
            self.assertEqual(status["status"], "slow_validation_met")
            self.assertIn("No installation", status["note"])

    def test_does_not_partially_spend_reserved_validation_when_budget_is_too_short(
        self,
    ):
        with tempfile.TemporaryDirectory() as temp:
            root, candidate = self.prepare(temp)
            train, _ = self.fake_train(root, candidate, 55)
            _, status = run_curriculum(
                root,
                root / "application.exe",
                root / "source.json",
                horizons=[700],
                radii=[50],
                policy="directional-v26",
                minutes=90,
                train_fn=train,
            )
            self.assertEqual(status["status"], "budget_before_validation")
            self.assertIsNone(status["validation"])

    def test_rejects_a_changed_holdout_before_validation(self):
        with tempfile.TemporaryDirectory() as temp:
            root, candidate = self.prepare(temp)
            original_train, _ = self.fake_train(root, candidate, 55)

            def train(*args, **kwargs):
                result = original_train(*args, **kwargs)
                if kwargs["scope"] == "development":
                    manifest = next(
                        (root / ".local/dodge-curricula").glob(
                            "*/validation-manifest.json"
                        )
                    )
                    manifest.write_text("{}")
                return result

            with self.assertRaisesRegex(ValueError, "manifest changed"):
                run_curriculum(
                    root,
                    root / "application.exe",
                    root / "source.json",
                    horizons=[700],
                    radii=[50],
                    policy="directional-v26",
                    minutes=300,
                    train_fn=train,
                )

    def test_incomplete_focus_does_not_proceed(self):
        with tempfile.TemporaryDirectory() as temp:
            root, _ = self.prepare(temp)
            calls = []

            def train(*args, **kwargs):
                calls.append(kwargs["scope"])
                return root / "focus", {
                    "status": "incomplete",
                    "selected_for_development": None,
                }

            _, status = run_curriculum(
                root,
                root / "application.exe",
                root / "source.json",
                horizons=[700],
                radii=[50],
                policy="directional-v26",
                minutes=90,
                train_fn=train,
            )
            self.assertEqual(calls, ["focus"])
            self.assertEqual(status["status"], "focus_incomplete")

    def test_study_stop_at_final_game_prevents_the_next_stage(self):
        with tempfile.TemporaryDirectory() as temp:
            root, candidate = self.prepare(temp)
            calls = []

            def train(_workspace, _application, _source, **kwargs):
                calls.append(kwargs["scope"])
                self.assertTrue(callable(kwargs["stop_requested"]))
                path = root / "focus"
                path.mkdir()
                (path / "STOP").write_text("")
                winner = {
                    "pack": str(candidate),
                    "mean_score": 70,
                    "policy": "directional-v26",
                }
                return path, {
                    "status": "complete",
                    "selected_for_development": winner,
                    "results": [winner],
                }

            _, status = run_curriculum(
                root,
                root / "application.exe",
                root / "source.json",
                horizons=[700],
                radii=[50],
                policy="directional-v26",
                minutes=90,
                train_fn=train,
            )
            self.assertEqual(calls, ["focus"])
            self.assertEqual(status["status"], "stopped")


if __name__ == "__main__":
    unittest.main()
