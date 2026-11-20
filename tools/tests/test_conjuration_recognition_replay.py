from copy import deepcopy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from conjuration_recognition_replay import replay_images
import conjuration_recognition_replay
from conjuration_vision import VisionProfile
from test_conjuration_vision import (
    Image,
    np,
    PROFILE,
    synthetic_board,
    synthetic_gallery,
)


def make_image_episode(directory, scale=1):
    """Explicit synthetic images and supplied simulated receipts; no game access."""
    directory = Path(directory)
    data = json.loads(PROFILE.read_text())
    profile, gallery = VisionProfile.from_dict(data), synthetic_gallery()
    refs = {}
    for key, image in gallery.items():
        name = f"reference-{key}.png"
        image.save(directory / name)
        refs[key] = {
            "image": name,
            "sha256": hashlib.sha256((directory / name).read_bytes()).hexdigest(),
        }
    manifest = {
        "schema_version": 1,
        "session_id": "synthetic-image-episode",
        "synthetic": True,
        "board_id": "board-1",
        "group_size": 2,
        "profile": data,
        "references": refs,
        "events": [],
    }
    revision, now, image_index = 0, 0, 0

    def frames(keys, phase="active"):
        nonlocal revision, now, image_index
        image_index += 1
        name = f"frame-{image_index}.png"
        with synthetic_board(profile, gallery, keys, scale) as image:
            image.save(directory / name)
        digest = hashlib.sha256((directory / name).read_bytes()).hexdigest()
        for _ in range(2):
            revision += 1
            now += 60
            manifest["events"].append(
                {
                    "kind": "frame",
                    "time_ms": now,
                    "revision": revision,
                    "phase": phase,
                    "image": name,
                    "sha256": digest,
                }
            )

    def receipt(ordinal):
        nonlocal now
        now += 1
        manifest["events"].append(
            {
                "kind": "action_receipt",
                "time_ms": now,
                "proposal_id": f"board-1:{ordinal}",
                "status": "dispatched",
            }
        )

    try:
        frames(["face-a", "face-b", "face-a", "face-b"], "preview")
        frames(["back"] * 4)
        receipt(1)
        frames(["face-a", "back", "back", "back"])
        receipt(2)
        frames(["face-a", "back", "face-a", "back"])
        frames(["inactive", "back", "inactive", "back"])
        receipt(3)
        frames(["inactive", "face-b", "inactive", "back"])
        receipt(4)
        frames(["inactive", "face-b", "inactive", "face-b"])
        frames(["inactive"] * 4)
    finally:
        for image in gallery.values():
            image.close()
    path = directory / "manifest.json"
    path.write_text(json.dumps(manifest), encoding="utf-8")
    return path, manifest


@unittest.skipIf(
    Image is None or np is None, "Existing offline Pillow/NumPy environment required"
)
class ConjurationRecognitionReplayTests(unittest.TestCase):
    def test_hashes_bind_the_immutable_bytes_actually_decoded(self):
        with tempfile.TemporaryDirectory() as directory:
            path, data = make_image_episode(directory)
            source = Path(directory) / data["events"][0]["image"]
            digest = data["events"][0]["sha256"]
            decode = conjuration_recognition_replay.decode_image
            changed = False

            def swap_after_snapshot(payload):
                nonlocal changed
                if not changed and hashlib.sha256(payload).hexdigest() == digest:
                    source.write_bytes(b"replaced-after-verified-snapshot")
                    changed = True
                return decode(payload)

            with patch.object(
                conjuration_recognition_replay,
                "decode_image",
                side_effect=swap_after_snapshot,
            ):
                report = replay_images(path)
            self.assertTrue(changed)
            self.assertTrue(report["decision_report"]["last_board_cleared"])
            self.assertEqual(digest, report["source_image_hashes"][source.name])
            with self.assertRaises(ValueError):
                replay_images(path)

    def test_pixels_feed_memory_and_preserve_proposals_receipts_and_outcomes(self):
        for scale in (1, 2):
            with self.subTest(scale=scale), tempfile.TemporaryDirectory() as directory:
                path, _ = make_image_episode(directory, scale)
                report = replay_images(path)
                decisions = report["decision_report"]
                self.assertEqual(2, decisions["matched_groups_observed"])
                self.assertTrue(decisions["last_board_cleared"])
                self.assertEqual(4, decisions["proposal_count"])
                self.assertEqual(4, decisions["supplied_receipt_count"])
                self.assertEqual(0, report["desktop_dispatch_count"])
                self.assertFalse(report["live_qualified"])
                self.assertFalse(report["phase_detection_implemented"])
                self.assertEqual(16, report["recognition_frame_count"])
                self.assertEqual(report, replay_images(path))

    def test_missing_receipts_are_not_invented_by_recognition(self):
        with tempfile.TemporaryDirectory() as directory:
            path, data = make_image_episode(directory)
            data["events"] = [
                e for e in data["events"] if e["kind"] != "action_receipt"
            ]
            path.write_text(json.dumps(data))
            decisions = replay_images(path)["decision_report"]
            self.assertEqual(0, decisions["supplied_receipt_count"])
            self.assertEqual(1, decisions["proposal_count"])
            self.assertFalse(decisions["last_board_cleared"])

    def test_image_hash_and_synthetic_profile_cannot_be_relabelled_recorded(self):
        with tempfile.TemporaryDirectory() as directory:
            path, data = make_image_episode(directory)
            bad = deepcopy(data)
            bad["events"][0]["sha256"] = "0" * 64
            path.write_text(json.dumps(bad))
            with self.assertRaises(ValueError):
                replay_images(path)
            data["synthetic"] = False
            path.write_text(json.dumps(data))
            with self.assertRaises(ValueError):
                replay_images(path)

    def test_bad_reset_and_stale_pixels_fail_before_a_report_exists(self):
        with tempfile.TemporaryDirectory() as directory:
            path, data = make_image_episode(directory)
            bad = deepcopy(data)
            bad["events"][1]["revision"] = bad["events"][0]["revision"]
            path.write_text(json.dumps(bad))
            with self.assertRaises(ValueError):
                replay_images(path)
            data["events"].insert(
                2,
                {
                    "kind": "reset",
                    "time_ms": 121,
                    "board_id": "board-1",
                    "group_size": 2,
                },
            )
            path.write_text(json.dumps(data))
            with self.assertRaises(ValueError):
                replay_images(path)

    def test_explicit_frame_budget_and_reference_manifest_mismatch(self):
        with tempfile.TemporaryDirectory() as directory:
            path, data = make_image_episode(directory)
            data["events"] = [data["events"][0]] * 257
            path.write_text(json.dumps(data))
            with self.assertRaises(ValueError):
                replay_images(path)
            data["events"] = data["events"][:1]
            data["references"]["unexpected"] = data["references"]["back"]
            path.write_text(json.dumps(data))
            with self.assertRaises(ValueError):
                replay_images(path)


if __name__ == "__main__":
    unittest.main()
