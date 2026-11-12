"""Unit tests for Potion Motion board evaluator and move planner."""

import json
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))

from potion_motion_board import PotionMotionBoard, MoveProposal


class PotionMotionBoardTests(unittest.TestCase):
    def test_dimensions_and_cell_access(self):
        grid = [
            ["red", "blue"],
            ["green", "yellow"],
        ]
        board = PotionMotionBoard(grid)
        self.assertEqual(board.rows, 2)
        self.assertEqual(board.cols, 2)
        self.assertEqual(board.get_cell(0, 0), "red")
        self.assertEqual(board.get_cell(1, 1), "yellow")
        with self.assertRaises(IndexError):
            board.get_cell(2, 0)

    def test_bent_l_shape_group_counted(self):
        # 4 yellow potions in an L shape: (0,0), (1,0), (2,0), (2,1)
        grid = [
            ["yellow", "blue", "green"],
            ["yellow", "purple", "red"],
            ["yellow", "yellow", "blue"],
        ]
        board = PotionMotionBoard(grid)
        groups = board.find_connected_groups(min_size=3)
        self.assertEqual(len(groups), 1)
        self.assertEqual(groups[0], [(0, 0), (1, 0), (2, 0), (2, 1)])
        score, _ = board.evaluate_board()
        self.assertGreater(score, 0)

    def test_diagonal_contact_does_not_count(self):
        # 3 red potions placed diagonally: (0,0), (1,1), (2,2)
        grid = [
            ["red", "blue", "green"],
            ["blue", "red", "yellow"],
            ["green", "yellow", "red"],
        ]
        board = PotionMotionBoard(grid)
        groups = board.find_connected_groups(min_size=3)
        self.assertEqual(len(groups), 0)
        score, _ = board.evaluate_board()
        self.assertEqual(score, 0)

    def test_unknown_cells_never_match(self):
        grid = [
            ["unknown", "unknown", "unknown"],
            ["blue", "yellow", "red"],
            ["green", "purple", "blue"],
        ]
        board = PotionMotionBoard(grid)
        groups = board.find_connected_groups(min_size=3)
        self.assertEqual(len(groups), 0)

    def test_row_shift_non_wrapping(self):
        grid = [
            ["red", "blue", "green", "yellow"],
            ["purple", "purple", "purple", "purple"],
        ]
        board = PotionMotionBoard(grid)
        shifted = board.shift_row(0, delta=1, allow_wrapping=False)
        # Shifted right by 1: first cell becomes "unknown", cells 0..2 shifted to 1..3
        self.assertEqual(shifted.get_cell(0, 0), "unknown")
        self.assertEqual(shifted.get_cell(0, 1), "red")
        self.assertEqual(shifted.get_cell(0, 2), "blue")
        self.assertEqual(shifted.get_cell(0, 3), "green")
        # Row 1 is untouched
        self.assertEqual(shifted.get_cell(1, 0), "purple")

    def test_row_shift_wrapping_preserves_multiset(self):
        grid = [
            ["red", "blue", "green", "yellow"],
            ["purple", "purple", "purple", "purple"],
        ]
        board = PotionMotionBoard(grid)
        shifted = board.shift_row(0, delta=1, allow_wrapping=True)
        # Shifted right by 1 with wrap: last cell "yellow" wraps to index 0
        self.assertEqual(shifted.get_cell(0, 0), "yellow")
        self.assertEqual(shifted.get_cell(0, 1), "red")
        self.assertEqual(shifted.get_cell(0, 2), "blue")
        self.assertEqual(shifted.get_cell(0, 3), "green")

    def test_column_shift_non_wrapping(self):
        grid = [
            ["red", "blue"],
            ["green", "yellow"],
            ["purple", "orange"],
        ]
        board = PotionMotionBoard(grid)
        shifted = board.shift_col(0, delta=1, allow_wrapping=False)
        # Shifted down by 1: (0,0) is unknown, (1,0) is red, (2,0) is green
        self.assertEqual(shifted.get_cell(0, 0), "unknown")
        self.assertEqual(shifted.get_cell(1, 0), "red")
        self.assertEqual(shifted.get_cell(2, 0), "green")
        # Column 1 untouched
        self.assertEqual(shifted.get_cell(0, 1), "blue")

    def test_find_best_moves_proposal_generation(self):
        # Setup board where shifting row 0 right by 1 creates a match of 3 reds at row 0 (cols 1,2,3)
        grid = [
            ["red", "red", "blue", "green"],
            ["blue", "yellow", "red", "purple"],
            ["green", "yellow", "purple", "blue"],
            ["purple", "blue", "green", "yellow"],
        ]
        board = PotionMotionBoard(grid)
        proposals = board.find_best_moves(allow_wrapping=False, limit=5)
        self.assertTrue(isinstance(proposals, list))
        # At least one proposal exists
        for p in proposals:
            self.assertTrue(isinstance(p, MoveProposal))
            self.assertGreater(p.score, 0)
            self.assertIn(p.move_type, ("row", "column"))

    def test_synthetic_cases_fixture(self):
        fixture_path = ROOT / "tools/fixtures/potion_motion/synthetic-cases.json"
        self.assertTrue(fixture_path.exists())
        data = json.loads(fixture_path.read_text(encoding="utf-8"))
        for case in data.get("cases", []):
            if "expected_groups" in case:
                board = PotionMotionBoard(case["grid"])
                groups = board.find_connected_groups(min_size=3)
                # Formatted as coordinate lists
                expected = [
                    sorted([tuple(coord) for coord in g])
                    for g in case["expected_groups"]
                ]
                self.assertEqual(groups, expected, f"Failed case {case['id']}")


if __name__ == "__main__":
    unittest.main()
