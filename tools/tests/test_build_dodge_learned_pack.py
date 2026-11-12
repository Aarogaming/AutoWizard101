import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from build_dodge_learned_pack import build
from game_packs import load_pack

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "tools/fixtures/game_packs/dodge-context-v12/pack.json"
MODEL = (
    ROOT / "ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/DodgeLearned/model.json"
)


class LearnedPackTests(unittest.TestCase):
    def prepare(self, temp):
        root = Path(temp)
        model = root / ".local/dodge-offline-models/model"
        model.mkdir(parents=True)
        (model / "model.json").write_text(MODEL.read_text())
        (model / "report.json").write_text(
            json.dumps(
                {
                    "model_sha256": hashlib.sha256(
                        (model / "model.json").read_bytes()
                    ).hexdigest(),
                    "provenance": {
                        "pack": {
                            "Sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest()
                        }
                    },
                }
            )
        )
        return root, model

    def test_creates_a_draft_with_an_embedded_versioned_model(self):
        with tempfile.TemporaryDirectory() as temp:
            root, model = self.prepare(temp)
            path = build(root, SOURCE, model, root / ".local/packs")
            pack = load_pack(path)
            self.assertEqual(
                pack["context"]["avoidance_policy"], "directional-learned-v33"
            )
            self.assertEqual(
                pack["experimental_model"]["feature_version"],
                "dodge-relative-and-clearance-v2",
            )
            self.assertEqual(
                pack["experimental_model_provenance"]["model_sha256"],
                hashlib.sha256((model / "model.json").read_bytes()).hexdigest(),
            )
            self.assertEqual(pack["status"], "draft")
            with self.assertRaises(ValueError):
                build(root, SOURCE, model, root / ".local/packs")

    def test_rejects_a_bad_model_and_different_teacher(self):
        with tempfile.TemporaryDirectory() as temp:
            root, model = self.prepare(temp)
            raw = json.loads((model / "model.json").read_text())
            raw["scales"][0] = 0
            (model / "model.json").write_text(json.dumps(raw))
            with self.assertRaisesRegex(ValueError, "values"):
                build(root, SOURCE, model, root / ".local/packs")
            changed = json.loads(MODEL.read_text())
            changed["weights"][0][0] += 0.1
            (model / "model.json").write_text(json.dumps(changed))
            with self.assertRaisesRegex(ValueError, "training report"):
                build(root, SOURCE, model, root / ".local/packs")
            (model / "model.json").write_text(MODEL.read_text())
            (model / "report.json").write_text(
                json.dumps(
                    {
                        "model_sha256": hashlib.sha256(
                            (model / "model.json").read_bytes()
                        ).hexdigest(),
                        "provenance": {"pack": {"Sha256": "b" * 64}},
                    }
                )
            )
            with self.assertRaisesRegex(ValueError, "different source"):
                build(root, SOURCE, model, root / ".local/packs")

    def test_model_with_a_learned_hold_action_can_be_packaged(self):
        with tempfile.TemporaryDirectory() as temp:
            root, model = self.prepare(temp)
            data = json.loads((model / "model.json").read_text())
            data["actions"].append("hold")
            data["weights"].append([0] * 53)
            (model / "model.json").write_text(json.dumps(data))
            report = json.loads((model / "report.json").read_text())
            report["model_sha256"] = hashlib.sha256(
                (model / "model.json").read_bytes()
            ).hexdigest()
            (model / "report.json").write_text(json.dumps(report))
            pack = load_pack(build(root, SOURCE, model, root / ".local/packs"))
            self.assertEqual(pack["experimental_model"]["actions"][-1], "hold")


if __name__ == "__main__":
    unittest.main()
