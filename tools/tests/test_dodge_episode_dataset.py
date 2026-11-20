import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from dodge_episode_dataset import load_episode_dataset
from dodge_audit_integrity import verify_audit_digest
from train_dodge_offline import exclusive_study


def make_run(root, seed, split):
    folder = root / str(seed)
    folder.mkdir()
    report = {
        "training": {"mode": "stepped", "speed": 1.0, "fixed_fps": 30},
        "stop_reason": "game-over",
        "actions": [
            {
                "action": "move-up",
                "state": "playing",
                "before_capture_ms": 0,
                "before_player": {"X": 0.5, "Y": 0.5},
                "threats": [],
                "outcome": "unknown",
                "after_state": "game_over",
            }
        ],
        "decisions": [{"action": "move-up", "capture_ms": 0}],
    }
    path = folder / "report.json"
    raw = json.dumps(report).encode()
    path.write_bytes(raw)
    (folder / "evaluation-audit.jsonl").write_text(
        "\n".join(
            json.dumps(row)
            for row in [{"ticks": 0, "alive": True}, {"ticks": 3, "alive": False}]
        )
    )
    return {
        "seed": seed,
        "report": str(path),
        "sha256": hashlib.sha256(raw).hexdigest(),
        "split": split,
    }


class DodgeEpisodeDatasetTests(unittest.TestCase):
    def test_missing_legacy_digest_is_distinct_from_failed_new_seal(self):
        digest, sealed = verify_audit_digest({}, b"audit")
        self.assertFalse(sealed)
        self.assertEqual(len(digest), 64)
        with self.assertRaises(ValueError):
            verify_audit_digest({"evaluation_audit_sha256": None}, b"audit")

    def test_capture_time_audit_hash_is_verified(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            items = [make_run(root, 1, "train"), make_run(root, 2, "test")]
            report_path = Path(items[0]["report"])
            audit_path = report_path.parent / "evaluation-audit.jsonl"
            report = json.loads(report_path.read_bytes())
            report["evaluation_audit_sha256"] = hashlib.sha256(
                audit_path.read_bytes()
            ).hexdigest()
            report_path.write_text(json.dumps(report))
            items[0]["sha256"] = hashlib.sha256(report_path.read_bytes()).hexdigest()
            with patch(
                "dodge_episode_dataset.load_examples",
                return_value=([], [], {"reports": items}),
            ):
                _, _, evidence = load_episode_dataset(root, root, [2])
                self.assertTrue(
                    evidence["audit_hashes_at_load"][0]["sealed_at_capture"]
                )
                self.assertFalse(
                    evidence["audit_hashes_at_load"][1]["sealed_at_capture"]
                )
                audit_path.write_bytes(audit_path.read_bytes() + b" ")
                with self.assertRaisesRegex(ValueError, "capture-time digest"):
                    load_episode_dataset(root, root, [2])

    def test_whole_games_remain_split_and_audit_bytes_are_identified(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            items = [make_run(root, 1, "train"), make_run(root, 2, "test")]
            with patch(
                "dodge_episode_dataset.load_examples",
                return_value=([], [], {"reports": items}),
            ):
                train, test, provenance = load_episode_dataset(root, root, [2])
            self.assertEqual((len(train), len(test)), (1, 1))
            self.assertTrue(train[0][-1]["terminated"])
            self.assertEqual(
                [entry["split"] for entry in provenance["audit_hashes_at_load"]],
                ["train", "test"],
            )
            self.assertEqual(len(provenance["audit_hashes_at_load"][0]["sha256"]), 64)

    def test_changed_report_is_rejected_after_validation(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            items = [make_run(root, 1, "train"), make_run(root, 2, "test")]
            Path(items[0]["report"]).write_text("{}")
            with patch(
                "dodge_episode_dataset.load_examples",
                return_value=([], [], {"reports": items}),
            ):
                with self.assertRaisesRegex(ValueError, "changed after validation"):
                    load_episode_dataset(root, root, [2])

    def test_active_study_prevents_loading_audits(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            with exclusive_study(root):
                with self.assertRaisesRegex(RuntimeError, "already running"):
                    load_episode_dataset(root, root, [2])


if __name__ == "__main__":
    unittest.main()
