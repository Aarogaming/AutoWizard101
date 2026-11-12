import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from dodge_episode_transitions import build_transitions, discounted_returns


def visual_action(
    time_ms, *, action="move-up", outcome="movement-observed", after_state="playing"
):
    return {
        "action": action,
        "state": "playing",
        "before_capture_ms": time_ms,
        "before_player": {"X": 0.5, "Y": 0.5},
        "threats": [],
        "outcome": outcome,
        "after_state": after_state,
    }


def audit(*states):
    return [{"ticks": tick, "alive": alive} for tick, alive in states]


class DodgeEpisodeTransitionTests(unittest.TestCase):
    def test_survival_and_one_collision_penalty_follow_the_episode(self):
        report = {
            "training": {"mode": "stepped", "speed": 1.0, "fixed_fps": 30},
            "stop_reason": "game-over",
            "actions": [
                visual_action(0),
                visual_action(200, outcome="unknown", after_state="game_over"),
            ],
            "decisions": [
                {"action": "move-up", "capture_ms": 0},
                {
                    "advice": "Hold",
                    "action": None,
                    "capture_ms": 100,
                    "observation_fresh": True,
                    "state": "playing",
                    "before_player": {"X": 0.5, "Y": 0.5},
                    "threats": [],
                },
                {"action": "move-up", "capture_ms": 200},
            ],
        }
        report["actions"][0]["exploration"] = {
            "Explored": True,
            "EligibleCount": 4,
            "Probability": 0.05,
            "Advice": "Up",
        }
        rows = build_transitions(
            report, audit((0, True), (3, True), (6, True), (9, False))
        )
        self.assertEqual([r["action"] for r in rows], ["move-up", "hold", "move-up"])
        self.assertEqual([r["terminated"] for r in rows], [False, False, True])
        self.assertEqual([r["truncated"] for r in rows], [False, False, False])
        self.assertAlmostEqual(rows[0]["reward"], 0.1)
        self.assertAlmostEqual(rows[1]["reward"], 0.1)
        self.assertAlmostEqual(rows[2]["reward"], -5.0)
        self.assertEqual(rows[0]["next_action"], "hold")
        self.assertEqual(len(rows[0]["state"]), 52)
        self.assertTrue(rows[0]["exploration"]["Explored"])
        self.assertIsNone(rows[-1]["next_state"])
        smaller_penalty = build_transitions(
            report, audit((0, True), (3, True), (6, True), (9, False)), death_penalty=0.01
        )
        self.assertLess(smaller_penalty[-1]["reward"], 0)
        returns = discounted_returns(rows, discount_per_second=1.0)
        for actual, expected in zip(returns, [-4.8, -4.9, -5.0]):
            self.assertAlmostEqual(actual, expected)

    def test_incomplete_and_unobserved_gaps_do_not_invent_a_collision(self):
        report = {
            "training": {"mode": "stepped", "speed": 1.0, "fixed_fps": 30},
            "stop_reason": "player-not-recognized",
            "actions": [visual_action(0), visual_action(1000)],
            "decisions": [
                {"action": "move-up", "capture_ms": 0},
                {"action": "move-up", "capture_ms": 1000},
            ],
        }
        rows = build_transitions(report, audit((0, True), (30, True), (33, True)))
        self.assertTrue(rows[0]["truncated"])
        self.assertEqual(rows[0]["reward"], 0.0)
        self.assertIsNone(rows[0]["next_state"])
        self.assertTrue(rows[1]["truncated"])
        self.assertFalse(rows[1]["terminated"])
        self.assertAlmostEqual(rows[1]["reward"], 0.1)
        self.assertEqual(discounted_returns(rows), [None, None])

    def test_terminal_claim_must_match_audit(self):
        report = {
            "training": {"mode": "stepped", "speed": 1.0, "fixed_fps": 30},
            "stop_reason": "game-over",
            "actions": [visual_action(0)],
            "decisions": [{"action": "move-up", "capture_ms": 0}],
        }
        with self.assertRaises(ValueError):
            build_transitions(report, audit((0, True), (3, True)))
        with self.assertRaises(ValueError):
            build_transitions(report, audit((0, True), (3, False), (6, True)))

    def test_unconfirmed_action_or_stale_hold_cannot_be_bridged(self):
        report = {
            "training": {"mode": "stepped", "speed": 1.0, "fixed_fps": 30},
            "stop_reason": "player-not-recognized",
            "actions": [visual_action(0)],
            "decisions": [
                {"action": "move-up", "capture_ms": 0},
                {"action": "move-right", "capture_ms": 100},
            ],
        }
        with self.assertRaises(ValueError):
            build_transitions(report, audit((0, True), (3, True)))
        report["decisions"][-1] = {
            "advice": "Hold",
            "action": None,
            "capture_ms": 100,
            "observation_fresh": False,
        }
        with self.assertRaises(ValueError):
            build_transitions(report, audit((0, True), (3, True)))

    def test_return_calculation_rejects_fabricated_transition_links(self):
        rows = [
            {
                "reward": 0.1,
                "elapsed_ms": 100,
                "terminated": False,
                "truncated": False,
                "next_state": [1],
                "next_action": "hold",
            },
            {
                "state": [2],
                "action": "hold",
                "reward": -5,
                "elapsed_ms": 100,
                "terminated": True,
                "truncated": False,
            },
        ]
        with self.assertRaises(ValueError):
            discounted_returns(rows)

    def test_invalid_exploration_probability_is_rejected(self):
        first = visual_action(0)
        first["exploration"] = {
            "Explored": True,
            "EligibleCount": 4,
            "Probability": 0,
            "Advice": "Up",
        }
        report = {
            "training": {"mode": "stepped", "speed": 1.0, "fixed_fps": 30},
            "stop_reason": "game-over",
            "actions": [first],
            "decisions": [{"action": "move-up", "capture_ms": 0}],
        }
        with self.assertRaises(ValueError):
            build_transitions(report, audit((0, True), (3, False)))


if __name__ == "__main__":
    unittest.main()
