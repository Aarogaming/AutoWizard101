from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from dodge_training_profiles import load_profile


class TrainingProfileTests(unittest.TestCase):
    def test_expanded_focus_is_a_subset_of_development_and_includes_failed_openings(
        self,
    ):
        workspace = Path(__file__).resolve().parents[2]
        focus = load_profile(workspace, "v2", "focus")
        development = load_profile(workspace, "v2", "development")
        self.assertEqual(focus["seeds"], [247, 781, 99316, 90595])
        self.assertTrue(set(focus["seeds"]).issubset(development["seeds"]))
        self.assertEqual(development["speed"], 0.25)
        with self.assertRaises(ValueError):
            load_profile(workspace, "missing", "focus")


if __name__ == "__main__":
    unittest.main()
