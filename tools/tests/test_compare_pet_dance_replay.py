import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from compare_pet_dance_replay import compare


class PetDanceReplayComparisonTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.addCleanup(self.temp.cleanup)
        (self.root / "capture.mp4").write_bytes(b"local fixture video identity")
        digest = (
            hashlib.sha256((self.root / "capture.mp4").read_bytes()).hexdigest().upper()
        )
        self.inputs = {
            "Version": 1,
            "Kind": "observed-game-input",
            "VideoVerified": True,
            "Truncated": False,
            "VideoFile": "capture.mp4",
            "VideoSha256": digest,
            "VideoCaptureOriginMs": 1000,
            "CaptureEndMs": 6000,
            "KeyAllowlist": ["Up", "Down", "Left", "Right"],
            "Events": [{"TimeMs": 0, "Type": "focus_gained"}],
        }
        self.replay = {
            "schema_version": 1,
            "source_video_sha256": digest,
            "proposals": [
                {
                    "time_ms": 100,
                    "actions": ["dance-right", "dance-left", "dance-up"],
                    "dispatched": False,
                }
            ],
            "outcomes": [{"time_ms": 1000, "outcome": "Success"}],
        }
        self.press(200, "Right")
        self.press(400, "Left")
        self.press(600, "Up")

    def press(self, time, control, injected=False, release=True):
        self.inputs["Events"].append(
            {
                "TimeMs": time,
                "Type": "key_down",
                "Control": control,
                "Injected": injected,
            }
        )
        if release:
            self.inputs["Events"].append(
                {
                    "TimeMs": time + 80,
                    "Type": "key_up",
                    "Control": control,
                    "Injected": injected,
                }
            )

    def run_compare(self):
        input_path, replay_path = (
            self.root / "input-events.json",
            self.root / "replay.json",
        )
        input_path.write_text(json.dumps(self.inputs))
        replay_path.write_text(json.dumps(self.replay))
        return compare(input_path, replay_path)

    def test_sorts_events_and_collapses_os_repeat_without_collapsing_later_same_key(
        self,
    ):
        self.inputs["Events"].append(
            {"TimeMs": 220, "Type": "key_down", "Control": "Right"}
        )
        self.inputs["Events"].reverse()
        result = self.run_compare()
        row = result["rounds"][0]
        self.assertEqual(row["human_input_sequence"], ["Right", "Left", "Up"])
        self.assertEqual(row["observed_presses"][0]["repeat_events"], 1)
        self.assertTrue(row["sequence_agrees"])
        self.assertFalse(result["exact_input_video_alignment_verified"])
        self.assertFalse(result["autonomous_success_claimed"])
        self.press(800, "Up")
        self.replay["proposals"][0]["actions"].append("dance-up")
        self.assertTrue(self.run_compare()["rounds"][0]["sequence_agrees"])

    def test_failed_round_preserves_difference_and_observed_result(self):
        self.replay["outcomes"][0]["outcome"] = "Failure"
        self.replay["proposals"][0]["actions"][-1] = "dance-down"
        row = self.run_compare()["rounds"][0]
        self.assertEqual(row["status"], "compared")
        self.assertTrue(row["actual_failed"])
        self.assertFalse(row["sequence_agrees"])
        self.assertEqual(row["human_input_sequence"][-1], "Up")
        self.assertEqual(row["visual_cue_sequence"][-1], "Down")

    def test_missing_and_multiple_results_are_not_scored(self):
        self.replay["outcomes"] = []
        row = self.run_compare()["rounds"][0]
        self.assertEqual(row["status"], "incomplete")
        self.assertIsNone(row["sequence_agrees"])
        self.replay["outcomes"] = [
            {"time_ms": 1000, "outcome": "Success"},
            {"time_ms": 1100, "outcome": "Failure"},
        ]
        row = self.run_compare()["rounds"][0]
        self.assertEqual(row["status"], "unscorable")
        self.assertIn("multiple_results", row["reasons"])

    def test_focus_loss_and_missing_release_are_unscorable(self):
        self.inputs["Events"].extend(
            [
                {"TimeMs": 500, "Type": "focus_lost"},
                {"TimeMs": 550, "Type": "focus_gained"},
            ]
        )
        row = self.run_compare()["rounds"][0]
        self.assertIn("round_not_continuously_focused", row["reasons"])
        self.assertIsNone(row["sequence_agrees"])
        self.inputs["Events"] = [
            e for e in self.inputs["Events"] if e["TimeMs"] not in (500, 550, 680)
        ]
        row = self.run_compare()["rounds"][0]
        self.assertIn("missing_observed_release", row["reasons"])

    def test_injected_input_is_excluded_and_contaminated_round_unscorable(self):
        self.press(800, "Down", injected=True)
        row = self.run_compare()["rounds"][0]
        self.assertNotIn("Down", row["human_input_sequence"])
        self.assertIn("injected_directional_input", row["reasons"])
        self.assertEqual(row["status"], "unscorable")

    def test_incomplete_start_and_missing_key_allowlist_are_explicit(self):
        self.press(50, "Down", release=False)
        row = self.run_compare()["rounds"][0]
        self.assertIn("directional_key_already_held_at_go", row["reasons"])
        self.inputs["KeyAllowlist"].remove("Down")
        self.assertIn(
            "directional_keys_not_all_recorded",
            self.run_compare()["rounds"][0]["reasons"],
        )

    def test_rejects_invalid_source_hash_and_modified_video(self):
        self.replay["source_video_sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "same source video"):
            self.run_compare()
        self.replay["source_video_sha256"] = self.inputs["VideoSha256"]
        (self.root / "capture.mp4").write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "does not match"):
            self.run_compare()

    def test_rejects_truncation_paths_nonfinite_and_out_of_coverage_times(self):
        self.inputs["Truncated"] = True
        with self.assertRaisesRegex(ValueError, "untruncated"):
            self.run_compare()
        self.inputs["Truncated"] = False
        self.inputs["VideoFile"] = "../capture.mp4"
        with self.assertRaisesRegex(ValueError, "filename"):
            self.run_compare()
        self.inputs["VideoFile"] = "capture.mp4"
        for bad in (float("nan"), float("inf"), -1, True, 6000):
            with self.subTest(bad=bad):
                self.inputs["Events"][1]["TimeMs"] = bad
                with self.assertRaises(ValueError):
                    self.run_compare()

    def test_rejects_duplicate_proposals_unknown_actions_and_dispatched_replays(self):
        self.replay["proposals"].append(dict(self.replay["proposals"][0]))
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            self.run_compare()
        self.replay["proposals"].pop()
        self.replay["proposals"][0]["actions"][0] = "feed-snack"
        with self.assertRaisesRegex(ValueError, "direction proposal"):
            self.run_compare()
        self.replay["proposals"][0]["actions"][0] = "dance-right"
        self.replay["proposals"][0]["dispatched"] = True
        with self.assertRaisesRegex(ValueError, "undispatched"):
            self.run_compare()

    def test_results_are_associated_only_before_the_next_go(self):
        self.replay["proposals"].append(
            {"time_ms": 1500, "actions": ["dance-down"] * 3, "dispatched": False}
        )
        self.replay["outcomes"] = [{"time_ms": 2500, "outcome": "Success"}]
        for time in (1600, 1800, 2000):
            self.press(time, "Down")
        result = self.run_compare()
        self.assertEqual(result["rounds"][0]["status"], "incomplete")
        self.assertTrue(result["rounds"][1]["sequence_agrees"])
        self.assertEqual(result["summary"]["compared_rounds"], 1)


if __name__ == "__main__":
    unittest.main()
