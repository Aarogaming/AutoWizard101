"""Synthetic local map/duel fixtures, never current-client qualification."""

import asyncio
import json
import struct
import unittest
from unittest.mock import patch

from test_w101_combat_reader import Memory
import w101_duel_reader as reader


class DuelMemory(Memory):
    def duel(self, duel_id, owners, *, phase=2, round_number=3):
        address = self.typed("Duel")
        vector = self.alloc()
        participants = []
        for i, owner in enumerate(owners):
            participant = self.typed("CombatParticipant")
            self.pack(participant + 112, "<Q", owner)
            self.pack(vector + i * 16, "<QQ", participant, 0)
            participants.append(participant)
        self.pack(address + 72, "<QQQ", duel_id, vector, vector + len(owners) * 16)
        self.pack(address + 192, "<ii", round_number, phase)
        return address, participants

    def manager(self, duels):
        manager = self.typed("ClientDuelManager")
        root = self.alloc()
        nodes = [self.alloc() for _ in duels]
        self.pack(manager + 8, "<Q", root)
        self.pack(root + 8, "<Q", nodes[0] if nodes else root)
        self.put(root + 25, b"\x01")
        for i, ((key, duel), node) in enumerate(zip(duels, nodes)):
            self.pack(
                node, "<QQQ", root, root, nodes[i + 1] if i + 1 < len(nodes) else root
            )
            self.pack(node + 32, "<QQ", key, duel)
        return manager, root, nodes


class DuelReaderTests(unittest.IsolatedAsyncioTestCase):
    async def observe(self, memory, manager, owners=("10", "20")):
        return await reader.read_duel_context(
            memory.read, manager, list(owners), layout_id=reader.LAYOUT_ID
        )

    async def test_unique_duel_matches_all_rooted_owners_not_first_duel(self):
        memory = DuelMemory()
        other, _ = memory.duel(100, [30, 40])
        selected, participants = memory.duel(200, [10, 20, 25])
        manager, root, nodes = memory.manager([(100, other), (200, selected)])
        result = await self.observe(memory, manager)
        self.assertEqual("matched", result["status"])
        self.assertEqual(
            ("200", "planning", 3), (result["duelId"], result["phase"], result["round"])
        )
        self.assertEqual(["10", "20", "25"], result["participantOwnerIds"])
        self.assertFalse(result["metadataQualified"])
        self.assertEqual("source-layout-only", result["qualification"])
        self.assertIsNone(result["outcome"])
        self.assertIsNone(result["winnerTeamId"])
        data = json.dumps(result)
        for address in [manager, root, selected] + nodes + participants:
            self.assertNotIn(str(address), data)
        self.assertLess(result["bytesRead"], 4096)
        self.assertLess(result["readCalls"], 100)

    async def test_single_shared_owner_does_not_match_when_other_owner_missing(self):
        memory = DuelMemory()
        duel, _ = memory.duel(100, [10, 30])
        manager, _, _ = memory.manager([(100, duel)])
        result = await self.observe(memory, manager)
        self.assertEqual("no-matching-duel", result["reason"])
        self.assertIsNone(result["phase"])

    async def test_ambiguous_owner_match_remains_unknown(self):
        memory = DuelMemory()
        first, _ = memory.duel(100, [10, 20])
        second, _ = memory.duel(200, [10, 20])
        manager, _, _ = memory.manager([(100, first), (200, second)])
        result = await self.observe(memory, manager)
        self.assertEqual("ambiguous-duel", result["reason"])
        self.assertIsNone(result["duelId"])

    async def test_empty_map_is_unknown_never_victory(self):
        memory = DuelMemory()
        manager, _, _ = memory.manager([])
        result = await self.observe(memory, manager)
        self.assertEqual("unknown", result["status"])
        self.assertEqual("no-matching-duel", result["reason"])
        self.assertIsNone(result["outcome"])
        self.assertIsNone(result["phase"])

    async def test_phase_victory_or_ended_does_not_identify_player_win(self):
        for phase in (6, 7):
            with self.subTest(phase=phase):
                memory = DuelMemory()
                duel, _ = memory.duel(100, [10, 20], phase=phase)
                manager, _, _ = memory.manager([(100, duel)])
                result = await self.observe(memory, manager)
                self.assertEqual(reader.PHASES[phase], result["phase"])
                self.assertIsNone(result["outcome"])
                self.assertIsNone(result["winnerTeamId"])

    async def test_unknown_enum_or_negative_round_rejected(self):
        for phase, round_number in ((8, 1), (10, 1), (2, -1), (2, 10001)):
            with self.subTest(phase=phase, round_number=round_number):
                memory = DuelMemory()
                duel, _ = memory.duel(
                    100, [10, 20], phase=phase, round_number=round_number
                )
                manager, _, _ = memory.manager([(100, duel)])
                result = await self.observe(memory, manager)
                self.assertEqual("duel-phase-or-round", result["reason"])
                self.assertIsNone(result["phase"])

    async def test_duplicate_or_inconsistent_map_values_fail(self):
        for case in ("cycle", "key", "duel", "mismatch", "leaf"):
            with self.subTest(case=case):
                memory = DuelMemory()
                duel, _ = memory.duel(100, [10, 20])
                second, _ = memory.duel(200, [30, 40])
                manager, root, nodes = memory.manager([(100, duel), (200, second)])
                if case == "cycle":
                    memory.pack(nodes[0], "<Q", nodes[0])
                elif case == "key":
                    memory.pack(nodes[1] + 32, "<Q", 100)
                elif case == "duel":
                    memory.pack(nodes[1] + 40, "<Q", duel)
                elif case == "mismatch":
                    memory.pack(nodes[0] + 32, "<Q", 101)
                else:
                    memory.put(root + 25, b"\x00")
                result = await self.observe(memory, manager)
                self.assertEqual("unknown", result["status"])
                self.assertIn(
                    result["reason"],
                    (
                        "map-cycle-or-budget",
                        "duplicate-duel",
                        "duel-id-mismatch",
                        "map-sentinel",
                    ),
                )

    async def test_changed_phase_discards_match(self):
        memory = DuelMemory()
        duel, _ = memory.duel(100, [10, 20])
        manager, _, _ = memory.manager([(100, duel)])

        def mutate(address, count, reads):
            if address == duel + 192 and reads == 2:
                memory.pack(address + 4, "<i", 4)

        memory.on_read = mutate
        result = await self.observe(memory, manager)
        self.assertEqual("changed-duel-context", result["reason"])
        self.assertIsNone(result["phase"])

    async def test_changed_manager_discards_match(self):
        memory = DuelMemory()
        duel, _ = memory.duel(100, [10, 20])
        manager, _, _ = memory.manager([(100, duel)])

        def mutate(address, count, reads):
            if address == manager + 8 and reads == 2:
                memory.pack(address, "<Q", 0)

        memory.on_read = mutate
        result = await self.observe(memory, manager)
        self.assertEqual("changed-duel-context", result["reason"])

    async def test_participant_vector_and_duplicate_owners_rejected(self):
        for owners in ([10] * 2, list(range(1, 10))):
            memory = DuelMemory()
            duel, _ = memory.duel(100, owners)
            manager, _, _ = memory.manager([(100, duel)])
            result = await self.observe(memory, manager)
            self.assertIn(result["reason"], ("duplicate-owner", "participant-vector"))

    async def test_wrong_manager_type_does_not_read_map(self):
        memory = DuelMemory()
        manager = memory.typed("Duel")
        result = await self.observe(memory, manager)
        self.assertEqual("type-mismatch", result["reason"])
        self.assertNotIn((manager + 8, 8), memory.calls)

    async def test_budgets_bound_map_and_read_work(self):
        memory = DuelMemory()
        first, _ = memory.duel(100, [10, 20])
        second, _ = memory.duel(200, [30, 40])
        manager, _, _ = memory.manager([(100, first), (200, second)])
        for constant, budget, reason in (
            ("MAX_DUELS", 1, "map-cycle-or-budget"),
            ("MAX_BYTES", 80, "read-budget"),
            ("MAX_READS", 4, "read-budget"),
        ):
            with self.subTest(constant=constant):
                with patch.object(reader, constant, budget):
                    result = await self.observe(memory, manager)
                self.assertEqual(reason, result["reason"])
                self.assertIsNone(result["phase"])

    async def test_failures_are_unknown_and_exception_text_is_redacted(self):
        async def fail(address, count):
            raise RuntimeError("0xDEADBEEF private value")

        result = await reader.read_duel_context(
            fail, 0x100000, ["10"], layout_id=reader.LAYOUT_ID
        )
        self.assertEqual("read-failed", result["reason"])
        self.assertEqual("unknown", result["status"])
        self.assertNotIn("DEADBEEF", json.dumps(result))
        self.assertIsNone(result["phase"])

        async def cancel(address, count):
            raise asyncio.CancelledError()

        with self.assertRaises(asyncio.CancelledError):
            await reader.read_duel_context(
                cancel, 0x100000, ["10"], layout_id=reader.LAYOUT_ID
            )

    async def test_invalid_owner_bindings_are_rejected_without_reads(self):
        memory = DuelMemory()
        for owners in (
            [],
            ["0"],
            ["01"],
            ["1", "1"],
            [1],
            ["-1"],
            ["18446744073709551616"],
            ["１"],
        ):
            with self.subTest(owners=owners):
                with self.assertRaises(ValueError):
                    await reader.read_duel_context(
                        memory.read, 0x100000, owners, layout_id=reader.LAYOUT_ID
                    )
        with self.assertRaises(ValueError):
            await reader.read_duel_context(
                memory.read, 0, ["1"], layout_id=reader.LAYOUT_ID
            )
        with self.assertRaises(ValueError):
            await reader.read_duel_context(
                memory.read, 0x100000, ["1"], layout_id="archived-wizwalker"
            )
        self.assertFalse(memory.calls)


if __name__ == "__main__":
    unittest.main()
