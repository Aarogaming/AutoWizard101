import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from analyze_dodge_action_support import raw_preference
from train_dodge_action_advantage import centered_action_features, fit_action_advantage


def state(threat_x):
    values = [0.0] * 52
    values[0] = values[1] = 0.5
    values[2] = threat_x
    return values


def row(threat_x, action, outcome):
    return (state(threat_x) + [int(i == action) for i in range(9)], outcome)


class ActionAdvantageTests(unittest.TestCase):
    def test_learns_opposite_action_preferences_from_randomized_examples(self):
        examples = []
        for threat_x in (-0.8, 0.8):
            for index in range(9):
                outcome = int(
                    (threat_x > 0 and index == 2) or (threat_x < 0 and index == 3)
                )
                examples.extend([row(threat_x, index, outcome)] * 20)
        model = fit_action_advantage(examples, ridge=1.0)
        self.assertEqual(raw_preference(model, state(0.8), [2, 3]), 2)
        self.assertEqual(raw_preference(model, state(-0.8), [2, 3]), 3)
        self.assertEqual(model["algorithm"], "linear-observational-action-advantage")
        self.assertTrue(all(weight == 0 for weight in model["weights"][:52]))

    def test_no_outcome_variation_creates_no_action_weights(self):
        model = fit_action_advantage([row(0.4, i, 1) for i in range(9)])
        self.assertTrue(all(abs(weight) < 1e-12 for weight in model["weights"]))
        with self.assertRaises(ValueError):
            fit_action_advantage([], ridge=1)
        with self.assertRaises(ValueError):
            fit_action_advantage([row(0.4, 0, 1)], ridge=0)

    def test_invalid_action_and_boundary_are_rejected(self):
        features, _ = row(0.4, 0, 1)
        features[52:] = [0] * 9
        with self.assertRaisesRegex(ValueError, "single randomized"):
            centered_action_features(features)
        features, _ = row(0.4, 2, 1)
        features[0] = 0.01
        with self.assertRaisesRegex(ValueError, "boundary"):
            centered_action_features(features)


if __name__ == "__main__":
    unittest.main()
