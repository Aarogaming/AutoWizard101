import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from build_dodge_outcome_pack import build
from game_packs import load_pack
from train_dodge_episode_advantage import runtime_model

SOURCE = (
    Path(__file__).resolve().parents[2]
    / "tools/fixtures/game_packs/dodge-context-v12/pack.json"
)


class OutcomePackTests(unittest.TestCase):
    def test_builds_a_research_episode_model_only_with_matching_provenance(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            folder = root / ".local/dodge-episode-models/example"
            folder.mkdir(parents=True)
            model = runtime_model(
                {
                    "algorithm": "experimental-observational-episode-advantage",
                    "coefficients": [0] * 19,
                }
            )
            path = folder / "runtime_model.json"
            path.write_text(json.dumps(model))
            report = {
                "status": "research-only",
                "runtime_model_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                "provenance": {
                    "pack": {"Sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest()}
                },
            }
            (folder / "training_report.json").write_text(json.dumps(report))
            pack = load_pack(
                build(
                    root, SOURCE, folder, root / ".local/packs", retain_reference=True
                )
            )
            self.assertEqual(pack["experimental_outcome_model"], model)
            self.assertEqual(
                pack["context"]["avoidance_policy"], "directional-outcome-reference-v37"
            )
            model["horizon_ms"] = 5000
            path.write_text(json.dumps(model))
            with self.assertRaisesRegex(ValueError, "Unsupported"):
                build(root, SOURCE, folder, root / ".local/packs")
            model["horizon_ms"] = 0
            model["weights"][52] = 1
            path.write_text(json.dumps(model))
            with self.assertRaisesRegex(ValueError, "report"):
                build(root, SOURCE, folder, root / ".local/packs")

    def test_builds_only_when_model_and_source_match_the_report(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            folder = root / ".local/dodge-survival-models/example"
            folder.mkdir(parents=True)
            model = {
                "schema_version": 1,
                "feature_version": "dodge-visual-state-action-context-v2-with-hold",
                "algorithm": "binary-logistic-observational-survival",
                "horizon_ms": 5000,
                "means": [0] * 71,
                "scales": [1] * 71,
                "weights": [0] * 72,
            }
            model_file = folder / "model.json"
            model_file.write_text(json.dumps(model))
            report = {
                "model_sha256": hashlib.sha256(model_file.read_bytes()).hexdigest(),
                "provenance": {
                    "recordings": [
                        {
                            "pack": {
                                "Sha256": hashlib.sha256(
                                    SOURCE.read_bytes()
                                ).hexdigest()
                            }
                        }
                    ]
                },
            }
            (folder / "report.json").write_text(json.dumps(report))
            pack = load_pack(build(root, SOURCE, folder, root / ".local/packs"))
            self.assertEqual(
                pack["context"]["avoidance_policy"], "directional-outcome-v34"
            )
            self.assertEqual(pack["experimental_outcome_model"], model)
            turn = load_pack(
                build(root, SOURCE, folder, root / ".local/packs", turn_aware=True)
            )
            self.assertEqual(
                turn["context"]["avoidance_policy"], "directional-outcome-turn-v36"
            )
            reference = load_pack(
                build(
                    root, SOURCE, folder, root / ".local/packs", retain_reference=True
                )
            )
            self.assertEqual(
                reference["context"]["avoidance_policy"],
                "directional-outcome-reference-v37",
            )
            self.assertNotEqual(reference["version"], turn["version"])
            escape = load_pack(
                build(root, SOURCE, folder, root / ".local/packs", early_escape=True)
            )
            self.assertEqual(
                escape["context"]["avoidance_policy"], "directional-outcome-escape-v38"
            )
            self.assertNotEqual(escape["version"], reference["version"])
            model["scales"][0] = 0
            model_file.write_text(json.dumps(model))
            with self.assertRaisesRegex(ValueError, "values"):
                build(root, SOURCE, folder, root / ".local/packs")
            model["scales"][0] = 1
            model["weights"][0] = 1
            model_file.write_text(json.dumps(model))
            with self.assertRaisesRegex(ValueError, "report"):
                build(root, SOURCE, folder, root / ".local/packs")
            model["feature_version"] += "-speed400"
            model["feature_player_speed"] = 400
            model_file.write_text(json.dumps(model))
            report["model_sha256"] = hashlib.sha256(model_file.read_bytes()).hexdigest()
            (folder / "report.json").write_text(json.dumps(report))
            calibrated = load_pack(build(root, SOURCE, folder, root / ".local/packs"))
            self.assertEqual(
                calibrated["experimental_outcome_model"]["feature_player_speed"], 400
            )
            model["weights"][0] = 0
            model["algorithm"] = "linear-observational-action-advantage"
            model_file.write_text(json.dumps(model))
            report["model_sha256"] = hashlib.sha256(model_file.read_bytes()).hexdigest()
            (folder / "report.json").write_text(json.dumps(report))
            advantage = load_pack(build(root, SOURCE, folder, root / ".local/packs"))
            self.assertEqual(
                advantage["experimental_outcome_model"]["algorithm"], model["algorithm"]
            )
            model["weights"][0] = 1
            model_file.write_text(json.dumps(model))
            with self.assertRaisesRegex(ValueError, "action-contrast"):
                build(root, SOURCE, folder, root / ".local/packs")
            model["weights"][0] = 0
            model_file.write_text(json.dumps(model))
            other_source = root / "other.json"
            other = load_pack(SOURCE)
            other["context"]["constraints"]["player_radius_px"] = 35
            other_source.write_text(json.dumps(other))
            report["provenance"]["recordings"][0]["pack"]["Sha256"] = hashlib.sha256(
                other_source.read_bytes()
            ).hexdigest()
            (folder / "report.json").write_text(json.dumps(report))
            with self.assertRaisesRegex(ValueError, "calibration"):
                build(root, other_source, folder, root / ".local/packs")
            model["feature_player_speed"] = 190
            model_file.write_text(json.dumps(model))
            with self.assertRaisesRegex(ValueError, "speed"):
                build(root, SOURCE, folder, root / ".local/packs")


if __name__ == "__main__":
    unittest.main()
