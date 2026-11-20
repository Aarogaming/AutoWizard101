import random
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from conjuration_memory import Board, Card, DispatchReceipt, MemoryMatch


class Game:
    def __init__(self, labels, size=2, preview=False):
        self.labels = labels
        self.states = ["face_up" if preview else "hidden"] * len(labels)
        self.solver = MemoryMatch("board-1", size)
        self.revision = 0
        self.history = []
        self.observe("preview" if preview else "active")

    def snapshot(self, phase="active"):
        return Board(
            "board-1",
            self.revision,
            phase,
            tuple(
                Card(
                    str(i),
                    10 + i % 4 * 50,
                    10 + i // 4 * 60,
                    state,
                    self.labels[i] if state == "face_up" else None,
                )
                for i, state in enumerate(self.states)
            ),
        )

    def observe(self, phase="active"):
        self.revision += 1
        outcome = self.solver.observe(self.snapshot(phase))
        if outcome:
            self.history.append(outcome)
        return outcome

    def flip(self):
        proposal = self.solver.propose()
        if proposal is None:
            raise AssertionError(self.solver.reason)
        self.solver.record_dispatch(DispatchReceipt(proposal.proposal_id, "dispatched"))
        self.states[int(proposal.slot)] = "face_up"
        self.observe("animation")
        if self.solver.propose() is not None:
            raise AssertionError("Animation must not permit another flip")
        self.observe()
        return proposal

    def settle(self):
        selected = [i for i, state in enumerate(self.states) if state == "face_up"]
        if len(selected) != self.solver.group_size:
            raise AssertionError("Cannot settle an incomplete group")
        match = len({self.labels[i] for i in selected}) == 1
        for i in selected:
            self.states[i] = "removed" if match else "hidden"
        return self.observe()


class ConjurationMemoryTests(unittest.TestCase):
    def test_preview_teaches_groups_but_never_proposes_a_click(self):
        game = Game(["A", "B", "B", "A"], preview=True)
        self.assertIsNone(game.solver.propose())
        self.assertEqual(4, len(game.solver.memory))
        game.states = ["hidden"] * 4
        game.observe()
        first, second = game.flip(), game.flip()
        self.assertEqual(game.labels[int(first.slot)], game.labels[int(second.slot)])
        self.assertEqual("complete-known-group", first.reason)
        self.assertEqual("match_observed", game.settle().kind)

    def test_seeded_random_pairs_and_triples_are_solved_from_reveals(self):
        for size in (2, 3):
            for seed in range(30):
                with self.subTest(size=size, seed=seed):
                    labels = [label for label in "ABCD" for _ in range(size)]
                    random.Random(seed).shuffle(labels)
                    game = Game(labels, size)
                    flips = 0
                    while "hidden" in game.states:
                        for _ in range(size):
                            game.flip()
                            flips += 1
                        game.settle()
                        self.assertLess(flips, 160, "Memory policy must make progress")
                    self.assertTrue(all(state == "removed" for state in game.states))
                    self.assertEqual(
                        4, sum(o.kind == "match_observed" for o in game.history)
                    )
                    self.assertIsNone(game.solver.propose())
                    self.assertEqual("board-cleared-observed", game.solver.reason)

    def test_proposal_receipt_and_observed_removal_are_separate(self):
        game = Game(["A", "A"])
        proposal = game.solver.propose()
        self.assertIsNotNone(proposal)
        self.assertEqual({}, game.solver.memory)
        self.assertIsNone(game.solver.propose())
        game.solver.record_dispatch(DispatchReceipt(proposal.proposal_id, "dispatched"))
        for _ in range(8):
            game.observe()
            self.assertIsNone(game.solver.propose())
        self.assertEqual([], game.history)
        game.states[int(proposal.slot)] = "face_up"
        game.observe()
        game.flip()
        self.assertIsNone(game.solver.propose())
        self.assertEqual([], game.history)
        self.assertEqual("match_observed", game.settle().kind)

    def test_uncertain_dispatch_does_not_retry_hidden_card(self):
        game = Game(["A", "A"])
        proposal = game.solver.propose()
        game.solver.record_dispatch(DispatchReceipt(proposal.proposal_id, "unknown"))
        for _ in range(5):
            game.observe()
            self.assertIsNone(game.solver.propose())
        game.states[int(proposal.slot)] = "face_up"
        game.observe()
        self.assertIsNotNone(game.solver.propose())

    def test_rejected_dispatch_needs_a_new_observation_before_retry(self):
        game = Game(["A", "A"])
        proposal = game.solver.propose()
        game.solver.record_dispatch(DispatchReceipt(proposal.proposal_id, "rejected"))
        self.assertIsNone(game.solver.propose())
        game.observe()
        retry = game.solver.propose()
        self.assertEqual(proposal.slot, retry.slot)
        self.assertNotEqual(proposal.proposal_id, retry.proposal_id)

    def test_stale_or_wrong_dispatch_receipt_cannot_silently_select_a_card(self):
        game = Game(["A", "A"])
        proposal = game.solver.propose()
        with self.assertRaises(ValueError):
            game.solver.record_dispatch(DispatchReceipt("other", "dispatched"))
        game.observe()
        game.solver.record_dispatch(DispatchReceipt(proposal.proposal_id, "dispatched"))
        self.assertIsNone(game.solver.propose())
        self.assertEqual("dispatch-based-on-outdated-observation", game.solver.reason)

    def test_unknown_and_animation_do_not_count_as_a_match(self):
        game = Game(["A", "A"])
        game.flip()
        game.flip()
        game.states = ["unknown", "unknown"]
        for phase in ("animation", "unknown", "active"):
            self.assertIsNone(game.observe(phase))
            self.assertIsNone(game.solver.propose())
        game.states = ["removed", "removed"]
        self.assertEqual("match_observed", game.observe().kind)

    def test_known_pair_flipping_back_is_not_reported_as_success_or_retried(self):
        game = Game(["A", "A"])
        game.flip()
        game.flip()
        game.states = ["hidden", "hidden"]
        self.assertEqual("expected_match_not_observed", game.observe().kind)
        game.observe()
        self.assertIsNone(game.solver.propose())

    def test_different_cards_removed_is_a_conflict_not_a_success(self):
        game = Game(["A", "B"])
        game.flip()
        game.flip()
        game.states = ["removed", "removed"]
        self.assertEqual("conflicting_removal", game.observe().kind)
        self.assertIsNone(game.solver.propose())

    def test_partial_outcome_waits_for_consistent_group_resolution(self):
        game = Game(["A", "A"])
        game.flip()
        game.flip()
        game.states = ["removed", "face_up"]
        self.assertIsNone(game.observe())
        self.assertIsNone(game.solver.propose())
        game.states = ["removed", "removed"]
        self.assertEqual("match_observed", game.observe().kind)

    def test_explicit_new_board_reset_discards_prior_memory_and_actions(self):
        game = Game(["A", "A"], preview=True)
        game.states = ["hidden", "hidden"]
        game.observe()
        previous = game.solver.propose()
        game.solver.reset("board-2", 2)
        self.assertEqual({}, game.solver.memory)
        with self.assertRaises(ValueError):
            game.solver.record_dispatch(
                DispatchReceipt(previous.proposal_id, "dispatched")
            )
        with self.assertRaises(ValueError):
            game.solver.observe(game.snapshot())
        with self.assertRaises(ValueError):
            game.solver.reset("board-2", 2)

    def test_conflicting_identity_waits_for_requalification(self):
        game = Game(["A", "A"], preview=True)
        game.labels[0] = "B"
        game.observe("preview")
        self.assertEqual("A", game.solver.memory["0"])
        game.states = ["hidden", "hidden"]
        game.observe()
        self.assertIsNone(game.solver.propose())
        self.assertEqual("conflicting-card-identity", game.solver.reason)

    def test_card_positions_revision_and_invisible_labels_are_validated(self):
        solver = MemoryMatch("test", 2)
        cards = (Card("0", 10, 10, "hidden"), Card("1", 60, 10, "hidden"))
        valid = Board("test", 0, "active", cards)
        solver.observe(valid)
        for bad in (
            valid,
            Board("different", 1, "active", cards),
            Board("test", 1, "active", (cards[0], Card("1", 99, 10, "hidden"))),
            Board(
                "test",
                1,
                "active",
                (cards[0], Card("1", 60, 10, "hidden", "future-label")),
            ),
            Board("test", 1, "active", (cards[0], cards[0])),
        ):
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                solver.observe(bad)
        self.assertEqual({}, solver.memory)

    def test_group_size_must_be_explicit_and_within_offline_bound(self):
        for size in (1, 9, True, 2.5):
            with self.subTest(size=size), self.assertRaises(ValueError):
                MemoryMatch("board", size)

    def test_removal_without_revealed_group_is_not_a_confirmed_match(self):
        game = Game(["A", "A"])
        proposal = game.solver.propose()
        game.solver.record_dispatch(DispatchReceipt(proposal.proposal_id, "dispatched"))
        game.states[int(proposal.slot)] = "removed"
        self.assertIsNone(game.observe())
        self.assertIsNone(game.solver.propose())
        self.assertEqual("flip-removed-without-observed-group", game.solver.reason)

    def test_incomplete_selection_disappearing_requires_requalification(self):
        game = Game(["A", "A"])
        proposal = game.flip()
        game.states[int(proposal.slot)] = "hidden"
        self.assertIsNone(game.observe())
        self.assertIsNone(game.solver.propose())
        self.assertEqual("selection-disappeared-without-outcome", game.solver.reason)

    def test_match_outcome_is_not_repeated_on_unchanged_removed_frames(self):
        game = Game(["A", "A"])
        game.flip()
        game.flip()
        self.assertEqual("match_observed", game.settle().kind)
        for _ in range(4):
            self.assertIsNone(game.observe())
        self.assertEqual(1, len(game.history))

    def test_result_phase_never_continues_play_or_invents_a_win(self):
        game = Game(["A", "A"])
        self.assertIsNone(game.observe("result"))
        self.assertIsNone(game.solver.propose())
        self.assertEqual([], game.history)

    def test_late_receipts_cannot_erase_an_independent_identity_conflict(self):
        for status in ("rejected", "dispatched", "unknown"):
            with self.subTest(status=status):
                game = Game(["A", "A"], preview=True)
                game.states = ["hidden", "hidden"]
                game.observe()
                proposal = game.solver.propose()
                game.labels[0] = "B"
                game.states = ["face_up", "face_up"]
                game.observe("preview")
                self.assertEqual("conflicting-card-identity", game.solver.reason)
                game.solver.record_dispatch(
                    DispatchReceipt(proposal.proposal_id, status)
                )
                game.states = ["hidden", "hidden"]
                game.observe()
                self.assertIsNone(game.solver.propose())
                self.assertEqual("conflicting-card-identity", game.solver.reason)
                self.assertEqual({"0": "A", "1": "A"}, game.solver.memory)

    def test_removed_cards_cannot_reappear_under_the_same_board_id(self):
        for partial in (False, True):
            for state in ("hidden", "face_up"):
                with self.subTest(partial=partial, state=state):
                    game = Game(["A", "A", "B", "B"] if partial else ["A", "A"])
                    game.flip()
                    game.flip()
                    self.assertEqual("match_observed", game.settle().kind)
                    game.states[0] = state
                    game.observe()
                    self.assertIsNone(game.solver.propose())
                    self.assertEqual(
                        "removed-card-reappeared-without-reset", game.solver.reason
                    )

    def test_temporarily_unknown_removed_card_does_not_invent_resurrection(self):
        game = Game(["A", "A", "B", "B"])
        game.flip()
        game.flip()
        game.settle()
        game.states[0] = "unknown"
        game.observe()
        self.assertIsNone(game.solver.propose())
        game.states[0] = "removed"
        game.observe()
        self.assertIsNotNone(game.solver.propose())


if __name__ == "__main__":
    unittest.main()
