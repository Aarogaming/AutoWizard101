import unittest

from tools.analyze_dodge_tracking import analyze


class DodgeTrackingAnalysisTests(unittest.TestCase):
    def test_checks_game_movement_in_requested_direction(self):
        report = {
            "training": {"mode": "stepped", "speed": 0.25, "fixed_fps": 30},
            "actions": [
                {
                    "action": "move-right",
                    "before_capture_ms": 1000,
                    "after_capture_ms": 1100,
                    "threats": [],
                }
            ],
        }
        audit = [
            {"ticks": 120, "player": {"x": 240, "y": 360}, "mobs": []},
            {"ticks": 132, "player": {"x": 250, "y": 360}, "mobs": []},
        ]
        result = analyze(report, audit)
        self.assertEqual(result["movement_pairs_aligned"], 1)
        self.assertEqual(result["forward_movement_median_px"], 10)
        self.assertEqual(result["movements_under_5_px_forward"], 0)
        audit[1]["player"]["x"] = 239
        self.assertEqual(analyze(report, audit)["movements_under_5_px_forward"], 1)

    def test_one_visual_detection_cannot_match_two_enemies(self):
        report = {
            "training": {"mode": "stepped", "speed": 0.25, "fixed_fps": 30},
            "actions": [
                {
                    "before_capture_ms": 1000,
                    "before_player": {"X": 0.5, "Y": 0.5},
                    "threats": [
                        {
                            "position": {"X": 0.5, "Y": 0.5},
                            "velocity_known": True,
                            "velocity_px_s": {"X": 20, "Y": 0},
                        }
                    ],
                }
            ],
        }
        audit = [
            {
                "ticks": 120,
                "player": {"x": 240, "y": 360},
                "mobs": [
                    {"x": 240, "y": 360, "vx": 20, "vy": 0},
                    {"x": 250, "y": 360, "vx": 200, "vy": 0},
                ],
            }
        ]
        result = analyze(report, audit)
        self.assertEqual(result["approximate_one_to_one_matches"], 1)
        self.assertEqual(result["near_player_without_match"], 1)
        self.assertEqual(result["velocity_error_median_px_s"], 0)
        self.assertEqual(result["player_position_error_max_px"], 0)
        audit[0]["mobs"][0]["vx"] = 200
        result = analyze(report, audit)
        self.assertEqual(result["near_player_known_velocity_matches"], 1)
        self.assertEqual(result["near_player_velocity_errors_over_100_px_s"], 1)

    def test_missing_audit_is_not_counted_as_a_successful_match(self):
        report = {
            "training": {"mode": "stepped", "speed": 0.25, "fixed_fps": 30},
            "actions": [{"before_capture_ms": 1000}],
        }
        result = analyze(report, [])
        self.assertEqual(result["action_frames_aligned"], 0)
        self.assertEqual(result["action_frames_without_audit"], 1)
        self.assertIsNone(result["player_position_error_median_px"])

    def test_rejects_other_clock_modes_and_duplicate_ticks(self):
        with self.assertRaises(ValueError):
            analyze({"training": {"mode": "realtime", "speed": 1, "fixed_fps": 30}}, [])
        with self.assertRaises(ValueError):
            analyze(
                {"training": {"mode": "stepped", "speed": 0.25, "fixed_fps": 30}},
                [{"ticks": 1}, {"ticks": 1}],
            )


if __name__ == "__main__":
    unittest.main()
