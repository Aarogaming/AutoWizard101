import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from compare_wsl_dodge_runs import compare
from game_pack_workshop import compare_test_reports


def report(directory, policy, seed, seconds, stop):
    folder = Path(directory) / f"{policy}-{seed}"
    folder.mkdir()
    for name in ("before.bmp", "after.bmp"):
        with (folder / name).open("wb") as file:
            file.write(b"BM")
            file.truncate(54 + 480 * 720 * 4)
    value = {
        "schema_version": 1,
        "mode": "wsl-private-visual",
        "policy": policy,
        "seed": seed,
        "pack": {
            "Id": "dodge-context",
            "Version": "0.1" if policy == "motion-v1" else "0.2",
            "Sha256": "a" * 64,
        },
        "action_count": 1,
        "actions": [
            {
                "outcome": "movement-observed",
                "before_file": "before.bmp",
                "after_file": "after.bmp",
            }
        ],
        "observed_play_seconds": seconds,
        "stop_reason": stop,
        "survival_observation": (
            "game-over-observed" if stop == "game-over" else "censored-at-stop"
        ),
    }
    path = folder / "report.json"
    path.write_text(json.dumps(value), encoding="utf-8")
    return path


class ComparisonTests(unittest.TestCase):
    def test_predictive_candidate_keeps_the_baseline_game_context(self):
        packs = Path(__file__).resolve().parents[1] / "fixtures" / "game_packs"
        baseline = json.loads((packs / "dodge-context" / "pack.json").read_text())
        candidate = json.loads(
            (packs / "dodge-context-predictive" / "pack.json").read_text()
        )
        candidate_context = dict(candidate["context"])
        self.assertEqual(candidate_context.pop("avoidance_policy"), "resilient-v2")
        self.assertEqual(candidate_context, baseline["context"])
        self.assertEqual(candidate["actions"], baseline["actions"])
        self.assertEqual(candidate["skills"], baseline["skills"])

    def test_rejects_mixed_speeds_and_accepts_matching_steps(self):
        with tempfile.TemporaryDirectory() as temp:
            first = report(temp, "motion-v1", 1, 4, "game-over")
            second = report(temp, "resilient-v2", 1, 6, "game-over")
            data = json.loads(second.read_text())
            data["training"] = {
                "mode": "stepped",
                "speed": 0.25,
                "fixed_fps": 30,
                "clock": "simulated",
            }
            second.write_text(json.dumps(data))
            with self.assertRaises(ValueError):
                compare([first, second])
            first_data = json.loads(first.read_text())
            first_data["training"] = data["training"]
            first.write_text(json.dumps(first_data))
            self.assertEqual(compare([first, second])["training"]["speed"], 0.25)
            first_data["training"]["action_limit"] = 40
            first_data["training"]["request_limit"] = 180
            first.write_text(json.dumps(first_data))
            with self.assertRaises(ValueError):
                compare([first, second])

    def test_reports_complete_and_censored_pairs_separately(self):
        with tempfile.TemporaryDirectory() as temp:
            paths = [
                report(temp, "motion-v1", 1, 4, "game-over"),
                report(temp, "resilient-v2", 1, 6, "game-over"),
                report(temp, "motion-v1", 2, 7, "action-limit"),
                report(temp, "resilient-v2", 2, 9, "game-over"),
            ]
            pairs = compare(paths)["pairs"]
            self.assertEqual(pairs[0]["observed_difference_seconds"], 2)
            self.assertIsNone(pairs[1]["observed_difference_seconds"])

    def test_rejects_missing_pairs_and_unsafe_or_missing_frames(self):
        with tempfile.TemporaryDirectory() as temp:
            first = report(temp, "motion-v1", 1, 4, "game-over")
            second = report(temp, "resilient-v2", 1, 6, "game-over")
            with self.assertRaises(ValueError):
                compare([first])
            data = json.loads(second.read_text())
            data["actions"][0]["before_file"] = "../escape.bmp"
            second.write_text(json.dumps(data))
            with self.assertRaises(ValueError):
                compare([first, second])

    def test_guarded_candidate_is_compared_as_its_own_policy(self):
        with tempfile.TemporaryDirectory() as temp:
            baseline = report(temp, "motion-v1", 7, 3, "game-over")
            candidate = report(temp, "guarded-v4", 7, 5, "game-over")
            result = compare([candidate, baseline])
            self.assertEqual(result["candidate_policy"], "guarded-v4")
            self.assertEqual(result["pairs"][0]["observed_difference_seconds"], 2)
            self.assertEqual(
                compare_test_reports(candidate, baseline)["candidate_policy"],
                "guarded-v4",
            )

    def test_learned_candidate_is_recognized_in_private_comparisons(self):
        with tempfile.TemporaryDirectory() as temp:
            baseline = report(temp, "motion-v1", 7, 3, "game-over")
            candidate = report(temp, "directional-learned-v33", 7, 5, "game-over")
            self.assertEqual(
                compare([candidate, baseline])["candidate_policy"],
                "directional-learned-v33",
            )


if __name__ == "__main__":
    unittest.main()
