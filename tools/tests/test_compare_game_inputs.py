import hashlib
import json
import sys
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from compare_game_inputs import compare, _read_log


class CompareGameInputsTests(unittest.TestCase):
    def test_bounded_log_read_preserves_bom_and_rejects_large_or_nonobject(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "log.json"
            path.write_bytes(b'\xef\xbb\xbf{"ok":true}')
            self.assertEqual(_read_log(path), {"ok": True})
            with patch("compare_game_inputs.MAX_LOG_BYTES", 8):
                with self.assertRaisesRegex(ValueError, "budget"):
                    _read_log(path)
            for raw in (b"[]", b"null", b"1", b'"text"'):
                path.write_bytes(raw)
                with self.assertRaisesRegex(ValueError, "JSON object"):
                    _read_log(path)
            path.write_bytes(b"{}")
            with patch("compare_game_inputs.MAX_LOG_BYTES", 2):
                self.assertEqual(_read_log(path), {})

    def test_focus_coverage_and_one_to_one_matching(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "capture.mp4").write_bytes(b"example")
            digest = hashlib.sha256(b"example").hexdigest().upper()
            truth = {
                "Version": 1,
                "Kind": "observed-game-input",
                "VideoVerified": True,
                "Truncated": False,
                "VideoFile": "capture.mp4",
                "VideoSha256": digest,
                "VideoCaptureOriginMs": 1000,
                "CaptureEndMs": 2000,
                "KeyAllowlist": ["W", "A"],
                "Events": [
                    {"TimeMs": 50, "Type": "focus_gained"},
                    {"TimeMs": 100, "Type": "key_down", "Control": "W"},
                    {"TimeMs": 300, "Type": "key_down", "Control": "A"},
                    {
                        "TimeMs": 400,
                        "Type": "key_down",
                        "Control": "W",
                        "Injected": True,
                    },
                    {"TimeMs": 500, "Type": "focus_lost"},
                ],
            }
            guesses = {
                "video_sha256": digest,
                "coverage_start_ms": 0,
                "coverage_end_ms": 1000,
                "events": [
                    {"time_ms": 110, "type": "key_down", "control": "W"},
                    {"time_ms": 120, "type": "key_down", "control": "W"},
                    {"time_ms": 400, "type": "key_down", "control": "W"},
                    {"time_ms": 700, "type": "key_down", "control": "W"},
                    {"time_ms": 200, "type": "key_down", "control": "Q"},
                ],
            }
            (root / "input-events.json").write_text(json.dumps(truth))
            (root / "predictions.json").write_text(json.dumps(guesses))
            report = compare(root / "input-events.json", root / "predictions.json", 50)
            self.assertEqual(
                (
                    report["matched_presses"],
                    report["unmatched_predicted_presses"],
                    report["missed_observed_presses"],
                ),
                (1, 1, 1),
            )
            self.assertEqual(report["excluded_predictions"], 3)
            self.assertEqual(report["mean_absolute_timing_error_ms"], 10)
            automated = compare(
                root / "input-events.json",
                root / "predictions.json",
                50,
                include_injected=True,
            )
            self.assertEqual(
                (
                    automated["matched_presses"],
                    automated["unmatched_predicted_presses"],
                    automated["missed_observed_presses"],
                ),
                (2, 1, 1),
            )
            self.assertTrue(automated["includes_automated_inputs"])

    def test_presses_cannot_match_across_focus_loss(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "capture.mp4").write_bytes(b"fixture")
            digest = hashlib.sha256(b"fixture").hexdigest()
            truth = {
                "Version": 1,
                "Kind": "observed-game-input",
                "VideoVerified": True,
                "Truncated": False,
                "VideoFile": "capture.mp4",
                "VideoSha256": digest,
                "VideoCaptureOriginMs": 0,
                "CaptureEndMs": 1000,
                "KeyAllowlist": ["W"],
                "Events": [
                    {"TimeMs": 0, "Type": "focus_gained"},
                    {"TimeMs": 100, "Type": "key_down", "Control": "W"},
                    {"TimeMs": 110, "Type": "focus_lost"},
                    {"TimeMs": 120, "Type": "focus_gained"},
                ],
            }
            guesses = {
                "video_sha256": digest,
                "coverage_start_ms": 0,
                "coverage_end_ms": 1000,
                "events": [{"time_ms": 130, "type": "key_down", "control": "W"}],
            }
            inp, pred = root / "inputs.json", root / "predictions.json"
            inp.write_text(json.dumps(truth))
            pred.write_text(json.dumps(guesses))
            result = compare(inp, pred, tolerance_ms=50)
            self.assertEqual(result["matched_presses"], 0)
            self.assertEqual(result["missed_observed_presses"], 1)
            self.assertEqual(result["unmatched_predicted_presses"], 1)
            truth["Events"][1]["Injected"] = True
            inp.write_text(json.dumps(truth))
            result = compare(inp, pred, tolerance_ms=50)
            self.assertEqual(result["excluded_predictions"], 0)
            self.assertEqual(result["unmatched_predicted_presses"], 1)

    def test_rejects_wrong_or_changed_video_and_truncated_logs(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "capture.mp4").write_bytes(b"example")
            digest = hashlib.sha256(b"example").hexdigest().upper()
            truth = {
                "Version": 1,
                "Kind": "observed-game-input",
                "VideoVerified": True,
                "Truncated": False,
                "VideoFile": "capture.mp4",
                "VideoSha256": digest,
                "VideoCaptureOriginMs": 0,
                "CaptureEndMs": 100,
                "KeyAllowlist": [],
                "Events": [],
            }
            guess = {
                "video_sha256": "0" * 64,
                "coverage_start_ms": 0,
                "coverage_end_ms": 100,
                "events": [],
            }
            inp, pred = root / "input-events.json", root / "predictions.json"
            inp.write_text(json.dumps(truth))
            pred.write_text(json.dumps(guess))
            with self.assertRaisesRegex(ValueError, "same video"):
                compare(inp, pred)
            guess["video_sha256"] = digest
            guess["model_output_status"] = "invalid"
            pred.write_text(json.dumps(guess))
            with self.assertRaisesRegex(ValueError, "incomplete or invalid"):
                compare(inp, pred)
            guess["model_output_status"] = "complete"
            pred.write_text(json.dumps(guess))
            (root / "capture.mp4").write_bytes(b"changed")
            with self.assertRaisesRegex(ValueError, "does not match"):
                compare(inp, pred)
            truth["Truncated"] = True
            inp.write_text(json.dumps(truth))
            with self.assertRaisesRegex(ValueError, "complete"):
                compare(inp, pred)


if __name__ == "__main__":
    unittest.main()
