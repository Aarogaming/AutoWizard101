import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from benchmark_dodge_scores import summarize
from resume_dodge_validation import resume_validation, verify_development
from tools.tests.test_benchmark_dodge_scores import write_report


class ResumeValidationTests(unittest.TestCase):
    def test_development_is_reconstructed_from_every_game(self):
        with tempfile.TemporaryDirectory() as temp:
            root, app, source, _ = self.setup_curriculum(temp)
            fixture_dir = root / "tools/fixtures"
            fixture_dir.mkdir(parents=True)
            fixture = (
                Path(__file__).resolve().parents[1]
                / "fixtures/dodge-benchmark-development.json"
            )
            (fixture_dir / fixture.name).write_text(fixture.read_text())
            source_files = [source, fixture_dir / fixture.name]
            for name in ("train_dodge_offline.py", "generate_dodge_candidates.py", "dodge_training_profiles.py", "dodge_selection.py"):
                path = root / "tools" / name
                path.write_text(name)
                source_files.append(path)
            source_hashes = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in source_files}
            manifest = json.loads(fixture.read_text())
            study = root / ".local/dodge-training/study"
            candidate = study / "packs/candidate.json"
            candidate.parent.mkdir(parents=True)
            candidate.write_text("candidate")
            rows, hashes = [], {}
            for index, (pack, score) in enumerate(((source, 10), (candidate, 60))):
                digest = hashlib.sha256(pack.read_bytes()).hexdigest()
                hashes[str(pack)] = digest
                folder = root / ".local/wsl-dodge-runs" / str(index)
                folder.mkdir(parents=True)
                reports = [
                    write_report(
                        folder,
                        seed,
                        score,
                        pack={"Id": "test", "Version": str(index), "Sha256": digest},
                    )
                    for seed in manifest["seeds"]
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
                        "manifest": manifest,
                        "planned_packs": [str(source), str(candidate)],
                        "results": rows,
                        "pack_sha256": hashes,
                        "shared_files_sha256": {},
                        "sources_sha256": source_hashes,
                        "selection_method": "capped_mean_v1",
                        "selected_for_development": {"pack": str(candidate)},
                    }
                )
            )
            with patch("resume_dodge_validation.fingerprints", return_value={}):
                winner = verify_development(
                    root, app, {"development": str(study), "development_profile": "v1"}
                )
                self.assertEqual(winner["mean_score"], 60)
                (root / ".local/wsl-dodge-runs/1/118/game-over.bmp").unlink()
                with self.assertRaises(ValueError):
                    verify_development(
                        root,
                        app,
                        {"development": str(study), "development_profile": "v1"},
                    )

    def setup_curriculum(self, temp):
        root = Path(temp).resolve()
        app = root / "application.exe"
        app.write_text("application")
        pack = root / "pack.json"
        pack.write_text("pack")
        directory = root / ".local/dodge-curricula/test"
        directory.mkdir(parents=True)
        manifest = {
            "schema_version": 1,
            "id": "fresh",
            "purpose": "test",
            "seeds": list(range(1001, 1011)),
            "mode": "stepped",
            "speed": 0.25,
            "target_mean_score": 50,
        }
        manifest_path = directory / "validation-manifest.json"
        manifest_path.write_text(json.dumps(manifest))
        registry = root / ".local/dodge-holdouts/registry.json"
        registry.parent.mkdir(parents=True)
        registry.write_text(json.dumps({"schema_version": 1, "used_seeds": manifest["seeds"]}))
        status = {
            "status": "budget_before_validation",
            "development_profile": "v2",
            "validation_manifest_sha256": hashlib.sha256(
                manifest_path.read_bytes()
            ).hexdigest(),
        }
        (directory / "curriculum.json").write_text(json.dumps(status))
        return root, app, pack, directory

    def test_deferred_holdout_runs_once_and_records_result(self):
        with tempfile.TemporaryDirectory() as temp:
            root, app, pack, directory = self.setup_curriculum(temp)
            calls = []

            def runner(_workspace, _app, selected, manifest, output, **kwargs):
                calls.append((selected, manifest["seeds"], kwargs["stop_requested"]()))
                output.mkdir()
                return {"mean_score": 54}

            with patch(
                "resume_dodge_validation.verify_development",
                return_value={"pack": str(pack)},
            ):
                result = resume_validation(root, app, directory, runner=runner)
                self.assertEqual(result["status"], "slow_validation_met")
                self.assertEqual(len(calls), 1)
                with self.assertRaises(ValueError):
                    resume_validation(root, app, directory, runner=runner)
            self.assertEqual(len(calls), 1)

    def test_changed_manifest_cannot_be_run(self):
        with tempfile.TemporaryDirectory() as temp:
            root, app, pack, directory = self.setup_curriculum(temp)
            (directory / "validation-manifest.json").write_text("{}")
            with patch(
                "resume_dodge_validation.verify_development",
                return_value={"pack": str(pack)},
            ):
                with self.assertRaisesRegex(ValueError, "manifest changed"):
                    resume_validation(root, app, directory)
            self.assertEqual(
                json.loads((directory / "curriculum.json").read_text())["status"],
                "budget_before_validation",
            )

    def test_incomplete_validation_is_not_silently_retried(self):
        with tempfile.TemporaryDirectory() as temp:
            root, app, pack, directory = self.setup_curriculum(temp)

            def runner(*args, **kwargs):
                return {"mean_score": None}

            with patch(
                "resume_dodge_validation.verify_development",
                return_value={"pack": str(pack)},
            ):
                result = resume_validation(root, app, directory, runner=runner)
                self.assertEqual(result["status"], "validation_incomplete")
                with self.assertRaises(ValueError):
                    resume_validation(root, app, directory, runner=runner)


if __name__ == "__main__":
    unittest.main()
