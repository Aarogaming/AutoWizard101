import sys
import json
import hashlib
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from analyze_dodge_action_support import (
    analyze,
    assess,
    eligible_actions,
    validate_collector,
)
from benchmark_dodge_scores import summarize
from train_dodge_survival import load_survival_examples
from train_dodge_action_advantage import train
from build_dodge_outcome_pack import build
from game_packs import load_pack
from tools.tests.test_benchmark_dodge_scores import MANIFEST, write_report
from tools.tests.test_train_dodge_imitation import action


def model():
    return {
        "schema_version": 1,
        "feature_version": "dodge-visual-state-action-context-v2-with-hold-speed400",
        "feature_player_speed": 400,
        "algorithm": "binary-logistic-observational-survival",
        "horizon_ms": 1000,
        "means": [0] * 71,
        "scales": [1] * 71,
        "weights": [0] * 72,
    }


def example(action, survived, x=0.5, y=0.5):
    state = [0.0] * 52
    state[0], state[1] = x, y
    return (state + [int(i == action) for i in range(9)], survived)


class ActionSupportTests(unittest.TestCase):
    def test_only_random_matches_contribute_to_preferred_outcome(self):
        result = assess(model(), [(10, [example(0, 1), example(1, 0)]), (11, [])])
        self.assertEqual(result["held_out_games"], 2)
        self.assertEqual(result["randomized_observations"], 2)
        self.assertEqual(result["matching_observations"], 1)
        self.assertEqual(result["games_with_matches"], 1)
        self.assertEqual(result["weighted_survival_after_matching_raw_choice"], 1)
        self.assertEqual(result["survival_after_random_action"], 0.5)
        self.assertEqual(result["effective_matched_observations"], 1)

    def test_no_matches_is_unknown_not_zero(self):
        result = assess(model(), [(10, [example(1, 0)])])
        self.assertEqual(result["matching_observations"], 0)
        self.assertIsNone(result["weighted_survival_after_matching_raw_choice"])

    def test_edge_excludes_outward_actions(self):
        state = example(0, 1, x=0.01)[0][:52]
        self.assertNotIn(2, eligible_actions(state))
        with self.assertRaisesRegex(ValueError, "outside the collector boundary"):
            assess(model(), [(10, [example(2, 1, x=0.01)])])

    def test_collector_geometry_and_recorded_hash_are_required(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            pack_file, benchmark = root / "pack.json", root / "benchmark"
            benchmark.mkdir()
            fixture = (
                Path(__file__).resolve().parents[1]
                / "fixtures/game_packs/dodge-context-v12/pack.json"
            )
            pack = json.loads(fixture.read_text())
            pack["context"]["avoidance_policy"] = "directional-explore-v35"
            pack_file.write_text(json.dumps(pack))
            digest = hashlib.sha256(pack_file.read_bytes()).hexdigest()
            (benchmark / "fingerprints.json").write_text(
                json.dumps({str(pack_file): digest})
            )
            validate_collector(root, benchmark, digest)
            pack_file.write_text(json.dumps({**pack, "title": "changed"}))
            with self.assertRaisesRegex(ValueError, "changed"):
                validate_collector(root, benchmark, digest)
            pack["context"]["constraints"]["edge_margin_px"] = 30
            pack_file.write_text(json.dumps(pack))
            digest = hashlib.sha256(pack_file.read_bytes()).hexdigest()
            (benchmark / "fingerprints.json").write_text(
                json.dumps({str(pack_file): digest})
            )
            with self.assertRaisesRegex(ValueError, "geometry"):
                validate_collector(root, benchmark, digest)

    def test_analysis_requires_unchanged_held_out_evidence(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            folder = root / ".local/dodge-survival-models/example"
            folder.mkdir(parents=True)
            model_file = folder / "model.json"
            model_file.write_text(json.dumps(model()))
            test_audit = {
                "seed": 10,
                "split": "test",
                "sha256": "audit",
                "benchmark": "heldout",
                "teacher_pack_sha256": "pack",
            }
            training_audit = {
                "seed": 20,
                "split": "train",
                "sha256": "train",
                "benchmark": "training",
            }
            records = [{"reports": [{"seed": 10, "split": "test", "sha256": "visual"}]}]
            report = {
                "model_sha256": hashlib.sha256(model_file.read_bytes()).hexdigest(),
                "provenance": {
                    "outcome_audits": [test_audit, training_audit],
                    "recordings": records,
                },
            }
            (folder / "report.json").write_text(json.dumps(report))
            provenance = {
                "outcome_audits": [{**test_audit, "examples": 2}],
                "recordings": records,
            }
            rows = [example(0, 1), example(1, 0)]
            with patch("analyze_dodge_action_support.validate_collector"), patch(
                "analyze_dodge_action_support.load_survival_examples",
                return_value=([], rows, provenance),
            ):
                self.assertEqual(analyze(root, folder)["matching_observations"], 1)
                provenance["outcome_audits"][0]["sha256"] = "changed"
                with self.assertRaisesRegex(ValueError, "audit changed"):
                    analyze(root, folder)

    def test_analysis_uses_complete_synthetic_recordings_end_to_end(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            pack_file = root / "pack.json"
            fixture = (
                Path(__file__).resolve().parents[1]
                / "fixtures/game_packs/dodge-context-v12/pack.json"
            )
            pack = json.loads(fixture.read_text())
            pack["context"]["avoidance_policy"] = "directional-explore-v35"
            pack_file.write_text(json.dumps(pack))
            digest = hashlib.sha256(pack_file.read_bytes()).hexdigest()
            runs = root / ".local/wsl-dodge-runs"
            benchmark = root / ".local/dodge-benchmarks/example"
            runs.mkdir(parents=True)
            benchmark.mkdir(parents=True)
            reports = [write_report(runs, seed, 20) for seed in (1, 2)]
            for path in reports:
                data = json.loads(path.read_text())
                data["pack"]["Sha256"] = digest
                row = action(0.5, "move-left")
                row.update(
                    state="playing",
                    before_capture_ms=4000,
                    exploration={
                        "Advice": "Left",
                        "Explored": True,
                        "Probability": 0.2 / 9,
                        "EligibleCount": 9,
                    },
                )
                not_random = {
                    **row,
                    "before_capture_ms": 5050,
                    "exploration": {
                        **row["exploration"],
                        "Explored": False,
                        "Probability": 0.8 + 0.2 / 9,
                    },
                }
                data["actions"] = [row, not_random]
                path.write_text(json.dumps(data))
                (path.parent / "evaluation-audit.jsonl").write_text(
                    "\n".join(
                        json.dumps(item)
                        for item in [
                            {"ticks": 480, "alive": True},
                            {"ticks": 606, "alive": True},
                            {"ticks": 1200, "alive": True},
                            {"ticks": 1212, "alive": False},
                        ]
                    )
                )
            (benchmark / "manifest.json").write_text(json.dumps(MANIFEST))
            (benchmark / "summary.json").write_text(
                json.dumps(summarize(MANIFEST, reports))
            )
            (benchmark / "fingerprints.json").write_text(
                json.dumps({str(pack_file): digest})
            )
            _, heldout, provenance = load_survival_examples(
                root,
                benchmark,
                [2],
                include_holds=True,
                exploration_only=True,
                player_speed=400,
                horizon_ms=1000,
            )
            self.assertEqual(len(heldout), 1)
            folder = root / ".local/dodge-survival-models/example"
            folder.mkdir(parents=True)
            model_file = folder / "model.json"
            model_file.write_text(json.dumps(model()))
            (folder / "report.json").write_text(
                json.dumps(
                    {
                        "model_sha256": hashlib.sha256(
                            model_file.read_bytes()
                        ).hexdigest(),
                        "provenance": provenance,
                    }
                )
            )
            result = analyze(root, folder)
            self.assertEqual(result["held_out_games"], 1)
            self.assertEqual(result["randomized_observations"], 1)
            self.assertIsNone(result["weighted_survival_after_matching_raw_choice"])
            trained, report = train(root, [benchmark], [2])
            self.assertEqual(report["training_examples"], 1)
            pack_dir = build(
                root, pack_file, trained, root / ".local/packs", retain_reference=True
            )
            output = load_pack(pack_dir)
            self.assertEqual(
                output["experimental_outcome_model"]["algorithm"],
                "linear-observational-action-advantage",
            )


if __name__ == "__main__":
    unittest.main()
