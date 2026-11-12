import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from model_event_decoding import is_next_screen_boundary


class ModelEventDecodingTests(unittest.TestCase):
    def test_only_exact_screen_boundaries_are_abstentions(self):
        self.assertTrue(is_next_screen_boundary([10, 20], 10, 20))
        self.assertTrue(is_next_screen_boundary([20], 10, 20))
        for tokens in ([], [10], [10, 21], [10, 99, 20], [10, 20, 30]):
            self.assertFalse(is_next_screen_boundary(tokens, 10, 20))


if __name__ == "__main__":
    unittest.main()
