"""Boundary contract tests for Conjuration Concentration memory decisions.

Covers missing reveal evidence, excess visible selections, board reset invariants,
and contradictory pair reveals during in-flight active selection.
Pure offline stdlib unittest; no desktop access, capture, or external dependencies.
"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from conjuration_memory import Board, Card, DispatchReceipt, MemoryMatch


class ConjurationBoundaryContractTests(unittest.TestCase):
    def test_pending_flip_with_missing_or_unknown_reveal_withholds_further_action(self):
        """Ambiguous or unrevealed cards must not clear pending flip or admit new proposals."""
        solver = MemoryMatch("board-missing-reveal", group_size=2)
        initial_cards = (
            Card("0", 10, 10, "hidden"),
            Card("1", 60, 10, "hidden"),
            Card("2", 10, 70, "hidden"),
            Card("3", 60, 70, "hidden"),
        )
        solver.observe(Board("board-missing-reveal", 1, "active", initial_cards))

        proposal = solver.propose()
        self.assertIsNotNone(proposal)
        self.assertEqual("0", proposal.slot)

        # Dispatch the proposal
        solver.record_dispatch(DispatchReceipt(proposal.proposal_id, "dispatched"))

        # Case A: Next frame arrives, but slot "0" is still hidden (missing reveal evidence)
        still_hidden = (
            Card("0", 10, 10, "hidden"),
            Card("1", 60, 10, "hidden"),
            Card("2", 10, 70, "hidden"),
            Card("3", 60, 70, "hidden"),
        )
        solver.observe(Board("board-missing-reveal", 2, "active", still_hidden))
        self.assertEqual("waiting-for-dispatched-flip", solver.reason)
        self.assertIsNone(solver.propose())

        # Case B: Slot "0" is partially occluded / unknown
        unknown_frame = (
            Card("0", 10, 10, "unknown"),
            Card("1", 60, 10, "hidden"),
            Card("2", 10, 70, "hidden"),
            Card("3", 60, 70, "hidden"),
        )
        solver.observe(Board("board-missing-reveal", 3, "active", unknown_frame))
        self.assertEqual("waiting-for-stable-active-board", solver.reason)
        self.assertIsNone(solver.propose())

        # Case C: Clean, positive reveal arrives
        revealed_frame = (
            Card("0", 10, 10, "face_up", identity="Star"),
            Card("1", 60, 10, "hidden"),
            Card("2", 10, 70, "hidden"),
            Card("3", 60, 70, "hidden"),
        )
        solver.observe(Board("board-missing-reveal", 4, "active", revealed_frame))
        self.assertEqual("ready", solver.reason)
        self.assertEqual({"0": "Star"}, solver.memory)

        # Engine admits the next action
        next_proposal = solver.propose()
        self.assertIsNotNone(next_proposal)
        self.assertEqual("1", next_proposal.slot)

    def test_too_many_visible_face_up_cards_blocks_engine(self):
        """Observing more face-up cards than group_size latches an irrecoverable error."""
        solver = MemoryMatch("board-excess-cards", group_size=2)
        # 3 cards face up simultaneously on a group_size=2 board
        cards = (
            Card("0", 10, 10, "face_up", identity="Moon"),
            Card("1", 60, 10, "face_up", identity="Sun"),
            Card("2", 10, 70, "face_up", identity="Star"),
            Card("3", 60, 70, "hidden"),
        )
        outcome = solver.observe(Board("board-excess-cards", 1, "active", cards))
        self.assertIsNone(outcome)
        self.assertEqual("too-many-visible-selected-cards", solver.reason)
        self.assertIsNone(solver.propose())

        # Subsequent frames cannot clear the block without explicit reset
        hidden_cards = (
            Card("0", 10, 10, "hidden"),
            Card("1", 60, 10, "hidden"),
            Card("2", 10, 70, "hidden"),
            Card("3", 60, 70, "hidden"),
        )
        solver.observe(Board("board-excess-cards", 2, "active", hidden_cards))
        self.assertEqual("too-many-visible-selected-cards", solver.reason)
        self.assertIsNone(solver.propose())

    def test_board_reset_clears_blocked_error_and_rejects_identical_id(self):
        """Reset clears blocked failure state and memory, but strictly requires a new board ID."""
        solver = MemoryMatch("board-reset-test", group_size=2)
        cards = (
            Card("0", 10, 10, "face_up", identity="Moon"),
            Card("1", 60, 10, "face_up", identity="Sun"),
            Card("2", 10, 70, "face_up", identity="Star"),
            Card("3", 60, 70, "hidden"),
        )
        solver.observe(Board("board-reset-test", 1, "active", cards))
        self.assertEqual("too-many-visible-selected-cards", solver.reason)

        # Attempting to reuse identical board ID is rejected
        with self.assertRaises(ValueError):
            solver.reset("board-reset-test", 2)

        # Invalid board identifiers are rejected
        for invalid_id in ("", "   ", None, "a" * 129):
            with self.subTest(invalid_id=invalid_id), self.assertRaises(
                (ValueError, TypeError)
            ):
                solver.reset(invalid_id, 2)

        # Valid reset with a new board ID clears blocked state and initializes clean slate
        solver.reset("board-reset-clean", 2)
        self.assertEqual("waiting-for-board", solver.reason)
        self.assertEqual({}, solver.memory)

        clean_cards = (
            Card("0", 10, 10, "hidden"),
            Card("1", 60, 10, "hidden"),
            Card("2", 10, 70, "hidden"),
            Card("3", 60, 70, "hidden"),
        )
        solver.observe(Board("board-reset-clean", 1, "active", clean_cards))
        self.assertEqual("ready", solver.reason)
        self.assertIsNotNone(solver.propose())

    def test_known_pair_with_contradictory_second_card_reveal_blocks_solver(self):
        """Revealing an unexpected identity on a remembered pair member blocks solver."""
        solver = MemoryMatch("board-contradictory-pair", group_size=2)

        # 1. Preview teaches slots 0 and 1 are both 'A'
        preview_cards = (
            Card("0", 10, 10, "face_up", identity="A"),
            Card("1", 60, 10, "face_up", identity="A"),
            Card("2", 10, 70, "hidden"),
            Card("3", 60, 70, "hidden"),
        )
        solver.observe(Board("board-contradictory-pair", 1, "preview", preview_cards))
        self.assertEqual({"0": "A", "1": "A"}, solver.memory)

        # 2. Board becomes active with all cards hidden
        hidden_cards = (
            Card("0", 10, 10, "hidden"),
            Card("1", 60, 10, "hidden"),
            Card("2", 10, 70, "hidden"),
            Card("3", 60, 70, "hidden"),
        )
        solver.observe(Board("board-contradictory-pair", 2, "active", hidden_cards))

        # 3. Propose first card of known group
        p1 = solver.propose()
        self.assertIsNotNone(p1)
        self.assertEqual("0", p1.slot)
        self.assertEqual("complete-known-group", p1.reason)
        solver.record_dispatch(DispatchReceipt(p1.proposal_id, "dispatched"))

        # Slot 0 reveals as expected: identity 'A'
        step3_cards = (
            Card("0", 10, 10, "face_up", identity="A"),
            Card("1", 60, 10, "hidden"),
            Card("2", 10, 70, "hidden"),
            Card("3", 60, 70, "hidden"),
        )
        solver.observe(Board("board-contradictory-pair", 3, "active", step3_cards))

        # 4. Propose second card of known group (slot 1)
        p2 = solver.propose()
        self.assertIsNotNone(p2)
        self.assertEqual("1", p2.slot)
        self.assertEqual("complete-known-group", p2.reason)
        solver.record_dispatch(DispatchReceipt(p2.proposal_id, "dispatched"))

        # 5. Slot 1 reveals contradictory identity 'C' instead of expected 'A'
        contradictory_cards = (
            Card("0", 10, 10, "face_up", identity="A"),
            Card("1", 60, 10, "face_up", identity="C"),
            Card("2", 10, 70, "hidden"),
            Card("3", 60, 70, "hidden"),
        )
        outcome = solver.observe(
            Board("board-contradictory-pair", 4, "active", contradictory_cards)
        )
        self.assertIsNone(outcome)
        self.assertEqual("conflicting-card-identity", solver.reason)
        self.assertIsNone(solver.propose())


if __name__ == "__main__":
    unittest.main()
