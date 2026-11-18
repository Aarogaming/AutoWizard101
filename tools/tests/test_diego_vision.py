import json
from pathlib import Path
import sys
import tempfile
import unittest

try:
    import numpy as np
    from PIL import Image
except ImportError:
    np = Image = None

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
try:
    from diego_vision import (
        Detection,
        Examples,
        SceneTracker,
        demo,
        report,
        suggestions,
    )
except ImportError:
    Detection = Examples = SceneTracker = None
    demo = report = suggestions = None

VISION_REQUIRED = unittest.skipIf(
    np is None or Image is None or demo is None,
    "Existing offline Pillow/NumPy/OpenCV environment required",
)


@VISION_REQUIRED
class DiegoVisionTests(unittest.TestCase):
    def test_matches_new_position_and_survives_pack_reload(self):
        with tempfile.TemporaryDirectory() as folder:
            pack, frames = demo(folder)
            loaded = Examples(folder)
            result = report(frames[1], loaded)
            matches = {item["label"]: item for item in result["detections"]}
            self.assertEqual(matches["Barrel"]["box"], (330, 261, 45, 45))
            self.assertEqual(matches["Diego"]["box"], (120, 230, 55, 76))
            self.assertFalse(result["inputSent"])
            self.assertFalse(result["spawnSequenceAssumed"])
            self.assertEqual(len(pack.entries), 2)

    def test_plain_or_unseen_image_does_not_invent_enemies(self):
        with tempfile.TemporaryDirectory() as folder:
            pack, frames = demo(folder)
            for image in (
                Image.new("RGB", frames[0].size, "black"),
                Image.fromarray(
                    np.random.default_rng(77).integers(
                        0, 255, (420, 760, 3), dtype=np.uint8
                    )
                ),
            ):
                result = report(image, pack)
                self.assertEqual(result["detections"], [])
                self.assertIn("Unknown", result["suggestions"][0])

    def test_rejects_size_change_and_invalid_or_background_examples(self):
        with tempfile.TemporaryDirectory() as folder:
            pack, frames = demo(folder)
            for action in (
                lambda: pack.detect(frames[0].resize((380, 210))),
                lambda: pack.add(frames[0], (1, 1, 20, 20), "Barrel"),
                lambda: pack.add(frames[0], (25, 65, 20, 20), "Barrel"),
                lambda: pack.add(frames[0], (120, 230, 55, 76), "made up"),
                lambda: pack.configure(frames[0].size, (10, 10, 700, 300)),
            ):
                with self.assertRaises(ValueError):
                    action()

    def test_multiple_examples_and_repeated_subjects_are_deduplicated(self):
        with tempfile.TemporaryDirectory() as folder:
            pack, frames = demo(folder)
            pack.add(frames[0], (550, 261, 45, 45), "Barrel")
            image = frames[0].copy()
            image.paste(image.crop((550, 261, 595, 306)), (350, 261))
            matches = [match for match in pack.detect(image) if match.label == "Barrel"]
            self.assertEqual(len(matches), 2)

    def test_example_paths_cannot_escape_pack(self):
        with tempfile.TemporaryDirectory() as folder:
            pack, _ = demo(folder)
            path = Path(folder) / "examples.json"
            data = json.loads(path.read_text())
            data["examples"][0]["file"] = "../outside.png"
            path.write_text(json.dumps(data))
            with self.assertRaises(ValueError):
                Examples(folder)

    def test_conflicting_labels_are_unknown_instead_of_arbitrary(self):
        with tempfile.TemporaryDirectory() as folder:
            pack, frames = demo(folder)
            pack.add(frames[0], (550, 261, 45, 45), "Projectile")
            labels = {match.label for match in pack.detect(frames[0])}
            self.assertNotIn("Barrel", labels)
            self.assertNotIn("Projectile", labels)

    def test_tracks_relative_approach_without_inventing_timing(self):
        with tempfile.TemporaryDirectory() as folder:
            pack, frames = demo(folder)
            tracker = SceneTracker()
            first = report(frames[0], pack, tracker=tracker, frame_id=0)
            self.assertIn("second consecutive", first["suggestions"][0])
            # Re-inspecting a single frame does not count as corroboration.
            repeated = report(frames[0], pack, tracker=tracker, frame_id=0)
            self.assertEqual(repeated["tracks"][0]["consecutiveFrames"], 1)
            second = report(frames[1], pack, tracker=tracker, frame_id=1)
            # The demo moves the barrel too far for reliable association: reacquire it.
            barrel = next(
                track for track in second["tracks"] if track["label"] == "Barrel"
            )
            self.assertEqual(barrel["consecutiveFrames"], 1)
            image = frames[1].copy()
            image.paste((30, 43, 61), (330, 261, 375, 306))
            image.paste(frames[1].crop((330, 261, 375, 306)), (300, 261))
            third = report(image, pack, tracker=tracker, frame_id=2)
            self.assertIn("approaching", third["suggestions"][0])
            self.assertIn("uncalibrated", third["suggestions"][0])

    def test_random_enemy_orders_and_disappearance_use_only_current_scene(self):
        player = Detection("Diego", (100, 200, 50, 70), 0.99)
        tracker = SceneTracker()
        rng = np.random.default_rng(14)
        names = ["Barrel", "Fire Elf", "Pirate", "Roberto", "Projectile"]
        for frame_id, name in enumerate(rng.permutation(names)):
            threat = Detection(str(name), (300, 220, 40, 40), 0.99)
            tracks = tracker.update([player, threat], frame_id)
            self.assertEqual(
                {track["detection"].label for track in tracks}, {"Diego", name}
            )
            prompt = " ".join(suggestions([player, threat], tracks))
            if frame_id:
                self.assertIn(name, prompt)
                self.assertIn("new match", prompt)
        tracks = tracker.update([player], 5)
        self.assertEqual(len(tracks), 1)
        self.assertIn("No known threat", suggestions([player], tracks)[0])

    def test_backwards_or_skipped_frames_reset_confirmation(self):
        tracker = SceneTracker()
        items = [Detection("Diego", (100, 200, 50, 70), 1)]
        tracker.update(items, 0)
        self.assertEqual(tracker.update(items, 1)[0]["seen"], 2)
        self.assertEqual(tracker.update(items, 4)[0]["seen"], 1)
        self.assertEqual(tracker.update(items, 3)[0]["seen"], 1)

    def test_ambiguous_player_suppresses_suggestions(self):
        items = [
            Detection("Diego", (100, 200, 50, 70), 1),
            Detection("Diego", (200, 200, 50, 70), 1),
            Detection("Barrel", (300, 220, 40, 40), 1),
        ]
        self.assertIn("exactly one", suggestions(items)[0])


if __name__ == "__main__":
    unittest.main()
