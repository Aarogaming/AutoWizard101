import sys
import json
import hashlib
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from train_dodge_action_advantage import centered_action_features
from train_dodge_episode_advantage import (
    assess_episode_advantage,
    fit_episode_advantage,
    run_study,
)
from train_dodge_imitation import ACTIONS_WITH_HOLD


def episode(first_action, steps):
    state = [0.5, 0.5] + [0.0] * 50
    choices = [first_action] + ["hold"] * (steps - 1)
    rows = []
    for index, action in enumerate(choices):
        last = index == steps - 1
        rows.append(
            {
                "state": state,
                "action": action,
                "next_state": None if last else state,
                "next_action": None if last else choices[index + 1],
                "reward": -5.0 if last else 0.1,
                "elapsed_ms": 100,
                "terminated": last,
                "truncated": False,
                "exploration": (
                    {
                        "Explored": True,
                        "EligibleCount": 9,
                        "Probability": 0.2 / 9,
                        "Advice": first_action.removeprefix("move-").title(),
                    }
                    if index == 0
                    else None
                ),
            }
        )
    return rows


class EpisodeAdvantageTests(unittest.TestCase):
    def test_longer_recorded_survival_can_change_action_ranking(self):
        good, bad = episode("move-up", 70), episode("move-down", 2)
        model = fit_episode_advantage([good, bad], ridge=0.01, discount_per_second=1.0)
        state = good[0]["state"]
        scores = []
        for action in ("move-up", "move-down"):
            index = ACTIONS_WITH_HOLD.index(action)
            features = centered_action_features(
                state + [int(i == index) for i in range(9)]
            )
            scores.append(sum(x * w for x, w in zip(features, model["coefficients"])))
        self.assertGreater(scores[0], scores[1])
        self.assertEqual(model["games"], 2)
        self.assertEqual(model["randomized_observations"], 2)
        support = assess_episode_advantage(model, [good, bad])
        self.assertEqual(support["randomized_observations"], 2)
        self.assertEqual(support["matching_observations"], 1)
        self.assertEqual(support["games_with_matches"], 1)

    def test_incomplete_episodes_are_not_treated_as_success(self):
        incomplete = episode("move-up", 2)
        incomplete[-1].update(terminated=False, truncated=True, reward=0.1)
        with self.assertRaises(ValueError):
            fit_episode_advantage([incomplete, episode("move-down", 2)])

    def test_invalid_randomization_evidence_is_rejected(self):
        bad = episode("move-up", 2)
        bad[0]["exploration"]["Probability"] = 0.4
        with self.assertRaises(ValueError):
            fit_episode_advantage([bad, episode("move-down", 2)])

    def test_study_saves_hashed_research_model_and_heldout_report(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            benchmark = root / "benchmark"
            benchmark.mkdir()
            (benchmark / "manifest.json").write_text(
                json.dumps({"seeds": [1, 2, 3, 4]})
            )
            train = [episode("move-up", 70), episode("move-down", 2)]
            heldout = [episode("move-up", 3), episode("move-down", 4)]
            with patch(
                "train_dodge_episode_advantage._load_locked",
                return_value=(train, heldout, {"reports": []}),
            ):
                output, report = run_study(root, benchmark, heldout_games=2)
            self.assertEqual(report["status"], "research-only")
            self.assertEqual(report["held_out_games"], 2)
            self.assertEqual(
                hashlib.sha256((output / "model.json").read_bytes()).hexdigest(),
                report["model_sha256"],
            )
            self.assertEqual(
                json.loads((output / "training_report.json").read_text())[
                    "model_sha256"
                ],
                report["model_sha256"],
            )


if __name__ == "__main__":
    unittest.main()
