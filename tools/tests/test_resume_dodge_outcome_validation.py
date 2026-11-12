import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from resume_dodge_outcome_validation import resume, verify_qualified


class OutcomeResumeTests(unittest.TestCase):
    def test_does_not_repeat_a_validation_that_has_started(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            study = root / ".local/dodge-curricula/example"
            study.mkdir(parents=True)
            (study / "curriculum.json").write_text(
                json.dumps({"status": "final_running", "final": None})
            )
            with self.assertRaisesRegex(ValueError, "never-started"):
                resume(root, study)

    def test_rechecks_evidence_and_refuses_changed_holdout(self):
        with tempfile.TemporaryDirectory() as temp:
            workspace = Path(temp)
            directory = workspace / ".local/dodge-curricula/example"
            folder = directory / "round-1"
            candidate, baseline = folder / "candidate", folder / "baseline"
            candidate.mkdir(parents=True)
            baseline.mkdir()
            (folder / "packs").mkdir()
            app, base_pack, candidate_pack = (
                workspace / "app.exe",
                workspace / "base.json",
                folder / "packs/pack.json",
            )
            for item in (app, base_pack, candidate_pack):
                item.write_text("{}")
            run_root = workspace / ".local/wsl-dodge-runs/game"
            run_root.mkdir(parents=True)
            report = run_root / "report.json"
            report.write_text("{}")
            for benchmark in (candidate, baseline):
                (benchmark / "summary.json").write_text(
                    json.dumps({"games": [{"report": str(report)}]})
                )
                (benchmark / "fingerprints.json").write_text("{}")
                (benchmark / "manifest.json").write_text(json.dumps({"seeds": [3, 4]}))
            model_dir = workspace / ".local/dodge-survival-models/example"
            model_dir.mkdir(parents=True)
            model = {"weights": [1]}
            model_raw = json.dumps(model).encode()
            (model_dir / "model.json").write_bytes(model_raw)
            training = {
                "model_sha256": hashlib.sha256(model_raw).hexdigest(),
                "provenance": {"recordings": [{"manifest": {"seeds": [1, 2]}}]},
            }
            training_raw = json.dumps(training).encode()
            (model_dir / "report.json").write_bytes(training_raw)
            pack = {
                "context": {"avoidance_policy": "directional-outcome-v34"},
                "experimental_outcome_model": model,
                "experimental_outcome_provenance": {
                    "model_sha256": hashlib.sha256(model_raw).hexdigest(),
                    "training_report_sha256": hashlib.sha256(training_raw).hexdigest(),
                },
            }
            final_dir = directory / "final"
            final_dir.mkdir()
            manifest = {
                "schema_version": 1,
                "mode": "stepped",
                "speed": 0.25,
                "target_mean_score": 50,
                "seeds": list(range(11, 21)),
            }
            final_path = final_dir / "validation-manifest.json"
            final_path.write_text(json.dumps(manifest))
            registry = workspace / ".local/dodge-holdouts/registry.json"
            registry.parent.mkdir(parents=True)
            registry.write_text(
                json.dumps({"schema_version": 1, "used_seeds": manifest["seeds"]})
            )
            status = {
                "comparison_application": str(app),
                "baseline_pack": str(base_pack),
                "final_manifest_sha256": hashlib.sha256(
                    final_path.read_bytes()
                ).hexdigest(),
                "rounds": [
                    {
                        "round": 1,
                        "status": "compared",
                        "candidate_mean": 55,
                        "candidate_pack": str(candidate_pack),
                        "selected_model": str(model_dir),
                    }
                ],
            }
            with patch(
                "resume_dodge_outcome_validation.fingerprints", return_value={}
            ), patch(
                "resume_dodge_outcome_validation.compare",
                return_value={"candidate": {"mean_score": 55}},
            ), patch(
                "resume_dodge_outcome_validation.load_pack", return_value=pack
            ):
                self.assertEqual(
                    verify_qualified(workspace, directory, status)[-1], manifest
                )
                final_path.write_text(
                    json.dumps({**manifest, "seeds": list(range(12, 22))})
                )
                with self.assertRaisesRegex(ValueError, "manifest changed"):
                    verify_qualified(workspace, directory, status)


if __name__ == "__main__":
    unittest.main()
