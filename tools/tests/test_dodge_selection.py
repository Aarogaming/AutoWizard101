import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from dodge_selection import rank_screened, screening_score


class DodgeSelectionTests(unittest.TestCase):
    def test_screening_does_not_let_a_single_outlier_dominate(self):
        outlier = {"scores": [41, 157, 5, 7], "expected_games": 4}
        steadier = {"scores": [35, 110, 20, 25], "expected_games": 4}
        self.assertGreater(sum(outlier["scores"]), sum(steadier["scores"]))
        self.assertLess(
            screening_score(outlier["scores"]), screening_score(steadier["scores"])
        )
        self.assertIs(rank_screened([outlier, steadier]), steadier)

    def test_incomplete_or_invalid_scores_never_produce_a_winner(self):
        valid = {"scores": [40, 60], "expected_games": 2}
        self.assertIsNone(
            rank_screened([valid, {"scores": [100], "expected_games": 2}])
        )
        self.assertIsNone(screening_score([40, None]))
        self.assertIsNone(screening_score([True, 50]))
        self.assertIsNone(screening_score([40, -1]))

    def test_exact_ties_keep_declared_order(self):
        first = {"scores": [40, 60], "expected_games": 2}
        second = {"scores": [40, 60], "expected_games": 2}
        self.assertIs(rank_screened([first, second]), first)


if __name__ == "__main__":
    unittest.main()
