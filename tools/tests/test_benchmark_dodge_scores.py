import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from benchmark_dodge_scores import summarize

MANIFEST = {
    "schema_version": 1,
    "id": "test",
    "purpose": "test",
    "seeds": [1, 2],
    "mode": "stepped",
    "speed": 0.25,
    "target_mean_score": 50,
}


def write_report(root, seed, score, stop="game-over", alive=False, **changes):
    folder = Path(root) / str(seed)
    folder.mkdir()
    with (folder / "game-over.bmp").open("wb") as image:
        image.write(b"BM")
        image.truncate(54 + 480 * 720 * 4)
    data = {
        "schema_version": 1,
        "mode": "wsl-private-endurance",
        "seed": seed,
        "policy": "test-policy",
        "training": {
            "mode": "stepped",
            "speed": 0.25,
            "fixed_fps": 30,
            "clock": "simulated",
            "action_limit": 1200,
            "request_limit": 4800,
            "benchmark": True,
        },
        "pack": {"Id": "test", "Version": "1", "Sha256": "a" * 64},
        "evaluation": {
            "score": score,
            "alive": alive,
            "source": "private-game-evaluator-not-policy",
        },
        "stop_reason": stop,
        "survival_observation": (
            "game-over-observed" if stop == "game-over" else "censored-at-stop"
        ),
        "last_unrecognized_frame": "game-over.bmp",
        "actions": [],
    }
    data.update(changes)
    path = folder / "report.json"
    path.write_text(json.dumps(data), encoding="utf-8")
    return path


class DodgeBenchmarkTests(unittest.TestCase):
    def test_mean_requires_all_games_to_end(self):
        with tempfile.TemporaryDirectory() as temp:
            first = write_report(temp, 1, 40)
            second = write_report(temp, 2, 60)
            result = summarize(MANIFEST, [first, second])
            self.assertEqual(result["mean_score"], 50)
            self.assertEqual(result["median_score"], 50)
            self.assertEqual(result["minimum_score"], 40)
            self.assertEqual(result["games_at_or_above_target"], 1)
            self.assertTrue(result["target_met"])
            self.assertIn("only to this declared set", result["note"])
            self.assertIsNone(summarize(MANIFEST, [first])["mean_score"])

    def test_stopped_at_fifty_or_timeout_is_not_a_completed_game(self):
        with tempfile.TemporaryDirectory() as temp:
            first = write_report(temp, 1, 50, "score-goal", True)
            second = write_report(temp, 2, 12, "response-timeout", True)
            result = summarize(MANIFEST, [first, second])
            self.assertEqual(result["completed_games"], 0)
            self.assertIsNone(result["mean_score"])
            self.assertIsNone(result["target_met"])

    def test_rejects_duplicates_and_non_benchmark_or_mixed_versions(self):
        with tempfile.TemporaryDirectory() as temp:
            first = write_report(temp, 1, 40)
            with self.assertRaises(ValueError):
                summarize(MANIFEST, [first, first])
            second = write_report(
                temp, 2, 60, pack={"Id": "test", "Version": "2", "Sha256": "b" * 64}
            )
            with self.assertRaises(ValueError):
                summarize(MANIFEST, [first, second])
            data = json.loads(first.read_text())
            data["training"]["benchmark"] = False
            first.write_text(json.dumps(data))
            with self.assertRaises(ValueError):
                summarize(MANIFEST, [first])

    def test_rejects_mixed_observation_cadences(self):
        with tempfile.TemporaryDirectory() as temp:
            first = write_report(temp, 1, 40)
            second = write_report(temp, 2, 60)
            data = json.loads(second.read_text())
            data["training"]["observation_cadence"] = "reuse-action-frame"
            second.write_text(json.dumps(data))
            with self.assertRaises(ValueError):
                summarize(MANIFEST, [first, second])

    def test_accepts_extended_limits_but_never_mixes_them_with_old_runs(self):
        with tempfile.TemporaryDirectory() as temp:
            first = write_report(temp, 1, 40)
            second = write_report(temp, 2, 60)
            data = json.loads(second.read_text())
            data["training"].update(
                action_limit=2400, request_limit=9600, time_limit_seconds=1200
            )
            second.write_text(json.dumps(data))
            with self.assertRaises(ValueError):
                summarize(MANIFEST, [first, second])
            data = json.loads(first.read_text())
            data["training"].update(
                action_limit=2400, request_limit=9600, time_limit_seconds=1200
            )
            first.write_text(json.dumps(data))
            result = summarize(MANIFEST, [first, second])
            self.assertEqual(result["mean_score"], 50)
            self.assertEqual(
                result["run_limits"],
                {"actions": 2400, "requests": 9600, "seconds": 1200},
            )

    def test_requires_game_over_frame_evidence(self):
        with tempfile.TemporaryDirectory() as temp:
            first = write_report(temp, 1, 40)
            (first.parent / "game-over.bmp").unlink()
            with self.assertRaises(ValueError):
                summarize(MANIFEST, [first])


if __name__ == "__main__":
    unittest.main()
