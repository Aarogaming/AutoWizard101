"""Synthetic rooted-object layouts only; no game/process access."""

import asyncio
import json
from pathlib import Path
import struct
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import w101_combat_reader as reader

DEV = "deimos-development-df2283f"
OLD = "archived-wizwalker"


class Memory:
    def __init__(self):
        self.next = 0x100000
        self.memory = {}
        self.calls = []
        self.counts = {}
        self.on_read = None

    def alloc(self, size=4096):
        address = self.next
        self.next += 8192
        self.put(address, bytes(size))
        return address

    def put(self, address, data):
        self.memory.update({address + i: value for i, value in enumerate(data)})

    def pack(self, address, form, *values):
        self.put(address, struct.pack(form, *values))

    def typed(self, name, *, jump=False):
        address, vtable, function, typename = [self.alloc() for _ in range(4)]
        self.pack(address, "<Q", vtable)
        self.pack(vtable, "<Q", function)
        if jump:
            target = self.alloc()
            self.pack(function, "<Bi", 0xE9, target - function - 5)
            function = target
        self.put(function + 63, b"\x48\x8d\x0d")
        self.pack(function + 66, "<i", typename - function - 70)
        self.put(typename, name.encode("ascii") + b"\0")
        return address

    def string(self, address, text):
        raw = text.encode("utf-8")
        if len(raw) < 16:
            self.put(address, raw + bytes(16 - len(raw)))
        else:
            pointer = self.alloc()
            self.pack(address, "<Q", pointer)
            self.put(pointer, raw)
        self.pack(address + 16, "<Q", len(raw))

    def effect(self, *, typename="SpellEffect"):
        effect = self.typed(typename)
        self.pack(effect + 72, "<iiiI", 1, 100, 1, 123)
        self.pack(effect + 140, "<iii", 8, 0, 0)
        return effect

    def card(self, layout_id=DEV, *, index=1, jump=False):
        layout = reader._LAYOUTS[layout_id]
        control = self.typed("SpellCheckBox", jump=jump)
        spell = self.typed("GraphicalSpell")
        template = self.typed("SpellTemplate")
        effect = self.effect()
        vector = self.alloc()
        self.pack(control + 156, "<I", 1)
        self.pack(control + layout.spell_pointer, "<Q", spell)
        self.pack(control + layout.grayed, "<B", 0)
        if layout.checked:
            self.pack(control + layout.checked, "<B", 1)
        self.pack(spell + 120, "<Q", template)
        self.pack(spell + 128, "<IB3xI", 4321, 85, 123)
        self.pack(spell + layout.spell_id, "<I", 5678)
        self.pack(spell + 80, "<I", 0)
        self.pack(spell + 88, "<QQ", vector, vector + 16)
        self.pack(vector, "<QQ", effect, 0)
        self.string(template + 96, "Fire_Cat_Template")
        self.string(template + layout.template_type, "Attack")
        return reader.ControlBinding(index, control, "card"), {
            "control": control,
            "spell": spell,
            "template": template,
            "effect": effect,
            "vector": vector,
        }

    def participant(self, layout_id=DEV, *, index=2):
        layout = reader._LAYOUTS[layout_id]
        control = self.typed("CombatantDataControl")
        participant = self.typed("CombatParticipant")
        self.pack(control + 156, "<I", 1)
        self.pack(control + layout.participant_pointer, "<Q", participant)
        self.pack(participant + 112, "<QQB", 18446744073709551615, 42, 0)
        self.pack(participant + 144, "<iI", 1, 123)
        self.pack(participant + layout.health, "<iiB", 200, 300, 0)
        pip_count = None
        if layout.pip_pointer:
            pip_count = self.typed("PipCount")
            self.pack(participant + 152, "<Q", pip_count)
            self.put(pip_count + 80, bytes((1, 2, 0, 0, 1, 0, 0, 0, 0, 1)))
        else:
            self.put(participant + 152, bytes((1, 2, 1)))
        return reader.ControlBinding(index, control, "participant"), {
            "control": control,
            "participant": participant,
            "pips": pip_count,
        }

    async def read(self, address, count):
        self.calls.append((address, count))
        self.counts[address] = self.counts.get(address, 0) + 1
        if self.on_read:
            self.on_read(address, count, self.counts[address])
        return bytes(self.memory[address + i] for i in range(count))


class CombatReaderTests(unittest.IsolatedAsyncioTestCase):
    async def test_development_card_and_participant_fields_without_qualification(self):
        memory = Memory()
        card, card_addresses = memory.card()
        participant, participant_addresses = memory.participant()
        result = await reader.read_combat_fields(
            memory.read, [card, participant], layout_id=DEV
        )
        self.assertEqual([], result["rejected"])
        self.assertEqual("source-layout-only", result["qualification"])
        self.assertFalse(result["metadataQualified"])
        self.assertIsNone(result["phase"])
        self.assertIsNone(result["round"])
        self.assertIsNone(result["selectedCard"])
        observed = result["cards"][0]
        self.assertEqual(
            (4321, 5678, "Attack"),
            (observed["templateId"], observed["spellId"], observed["spellType"]),
        )
        self.assertTrue(observed["castableCandidate"])
        self.assertTrue(observed["checkedCandidate"])
        self.assertIsNone(observed["selected"])
        self.assertEqual(8, observed["effects"][0]["target"])
        self.assertEqual(100, observed["effects"][0]["parameter"])
        combatant = result["participants"][0]
        self.assertEqual("18446744073709551615", combatant["ownerId"])
        self.assertEqual(200, combatant["health"])
        self.assertEqual(1, combatant["pips"]["fire"])
        self.assertIsNone(combatant["relation"])
        serialized = json.dumps(result)
        for address in list(card_addresses.values()) + list(
            participant_addresses.values()
        ):
            self.assertNotIn(str(address), serialized)
        self.assertLess(result["bytesRead"], 4096)
        self.assertLess(result["readCalls"], 100)

    async def test_archived_offsets_are_explicit_and_school_pips_unknown(self):
        memory = Memory()
        card, _ = memory.card(OLD, jump=True)
        participant, _ = memory.participant(OLD)
        result = await reader.read_combat_fields(
            memory.read, [card, participant], layout_id=OLD
        )
        self.assertFalse(result["rejected"])
        self.assertIsNone(result["cards"][0]["checkedCandidate"])
        self.assertFalse(result["participants"][0]["schoolPipsKnown"])
        wrong = await reader.read_combat_fields(memory.read, [card], layout_id=DEV)
        self.assertFalse(wrong["cards"])
        self.assertEqual("invalid-pointer", wrong["rejected"][0]["reason"])

    async def test_hidden_health_is_unknown_and_no_hostility_inference(self):
        memory = Memory()
        binding, addresses = memory.participant()
        memory.put(addresses["participant"] + 252, b"\x01")
        result = await reader.read_combat_fields(memory.read, [binding], layout_id=DEV)
        combatant = result["participants"][0]
        self.assertTrue(combatant["healthHidden"])
        self.assertIsNone(combatant["health"])
        self.assertIsNone(combatant["maxHealth"])
        self.assertIsNone(combatant["relation"])

    async def test_control_type_is_required_before_any_spell_pointer(self):
        memory = Memory()
        address = memory.typed("CombatantControl")
        result = await reader.read_combat_fields(
            memory.read,
            [reader.ControlBinding(1, address, "participant")],
            layout_id=DEV,
        )
        self.assertEqual("type-mismatch", result["rejected"][0]["reason"])
        self.assertNotIn((address + 1888, 8), memory.calls)

    async def test_unqualified_type_instruction_does_not_follow_name_pointer(self):
        memory = Memory()
        binding, _ = memory.card()
        vtable = struct.unpack("<Q", await memory.read(binding.address, 8))[0]
        function = struct.unpack("<Q", await memory.read(vtable, 8))[0]
        memory.put(function + 63, b"\x90\x90\x90")
        result = await reader.read_combat_fields(memory.read, [binding], layout_id=DEV)
        self.assertEqual("type-instruction", result["rejected"][0]["reason"])

    async def test_nested_effect_type_is_not_flattened_into_simple_attack(self):
        memory = Memory()
        binding, addresses = memory.card()
        nested = memory.effect(typename="RandomSpellEffect")
        memory.pack(addresses["vector"], "<Q", nested)
        result = await reader.read_combat_fields(memory.read, [binding], layout_id=DEV)
        self.assertFalse(result["cards"])
        self.assertEqual("type-mismatch", result["rejected"][0]["reason"])

    async def test_changed_pointer_rejects_entire_card(self):
        memory = Memory()
        binding, addresses = memory.card()

        def mutate(address, count, reads):
            if address == binding.address + 1104 and reads == 2:
                memory.pack(address, "<Q", addresses["template"])

        memory.on_read = mutate
        result = await reader.read_combat_fields(memory.read, [binding], layout_id=DEV)
        self.assertFalse(result["cards"])
        self.assertEqual("changed-card", result["rejected"][0]["reason"])

    async def test_changed_vector_rejects_entire_card(self):
        memory = Memory()
        binding, addresses = memory.card()

        def mutate(address, count, reads):
            if address == addresses["spell"] + 88 and reads == 2:
                memory.pack(address + 8, "<Q", addresses["vector"] + 32)

        memory.on_read = mutate
        result = await reader.read_combat_fields(memory.read, [binding], layout_id=DEV)
        self.assertEqual("changed-effect-vector", result["rejected"][0]["reason"])

    async def test_bounded_vectors_strings_and_valid_boolean_values(self):
        for what in ("effect-vector", "string-length", "invalid-boolean", "pip-range"):
            with self.subTest(what=what):
                memory = Memory()
                if what == "pip-range":
                    binding, addresses = memory.participant()
                    memory.put(addresses["pips"] + 80, b"\x08")
                else:
                    binding, addresses = memory.card()
                    if what == "effect-vector":
                        memory.pack(addresses["spell"] + 104, "<Q", 0)
                        memory.pack(
                            addresses["spell"] + 96, "<Q", addresses["vector"] + 16 * 9
                        )
                    elif what == "string-length":
                        memory.pack(addresses["template"] + 112, "<Q", 129)
                    else:
                        memory.put(binding.address + 1208, b"\x02")
                result = await reader.read_combat_fields(
                    memory.read, [binding], layout_id=DEV
                )
                self.assertEqual(what, result["rejected"][0]["reason"])

    async def test_grayed_disabled_or_hidden_card_cannot_be_castable(self):
        for flags, grayed in ((1, 1), (0x80000001, 0), (0, 0)):
            with self.subTest(flags=flags, grayed=grayed):
                memory = Memory()
                binding, _ = memory.card()
                memory.pack(binding.address + 156, "<I", flags)
                memory.pack(binding.address + 1208, "<B", grayed)
                result = await reader.read_combat_fields(
                    memory.read, [binding], layout_id=DEV
                )
                if flags == 0:
                    self.assertEqual("hidden-control", result["rejected"][0]["reason"])
                else:
                    self.assertFalse(result["cards"][0]["castableCandidate"])

    async def test_read_failure_redacts_exception_addresses(self):
        async def failed(address, count):
            raise RuntimeError("process failure at 0xDEADBEEF, private-data")

        result = await reader.read_combat_fields(
            failed, [reader.ControlBinding(0, 0x100000, "card")], layout_id=DEV
        )
        self.assertEqual("read-failed", result["rejected"][0]["reason"])
        self.assertNotIn("private", json.dumps(result))
        self.assertNotIn("DEADBEEF", json.dumps(result))

    async def test_short_reads_rejected_and_cancellation_propagates(self):
        async def short(address, count):
            return b""

        binding = reader.ControlBinding(0, 0x100000, "card")
        result = await reader.read_combat_fields(short, [binding], layout_id=DEV)
        self.assertEqual("short-read", result["rejected"][0]["reason"])

        async def canceled(address, count):
            raise asyncio.CancelledError()

        with self.assertRaises(asyncio.CancelledError):
            await reader.read_combat_fields(canceled, [binding], layout_id=DEV)

    async def test_budget_and_deadline_stop_reads(self):
        for constant, maximum in (
            ("MAX_BYTES", 100),
            ("MAX_READS", 4),
            ("MAX_SECONDS", 0),
        ):
            with self.subTest(constant=constant):
                memory = Memory()
                binding, _ = memory.card()
                with patch.object(reader, constant, maximum):
                    result = await reader.read_combat_fields(
                        memory.read, [binding], layout_id=DEV
                    )
                self.assertFalse(result["cards"])
                self.assertIn(
                    result["rejected"][0]["reason"], ("read-budget", "read-timeout")
                )
        memory = Memory()
        binding, _ = memory.card()

        async def stalled(address, count):
            await asyncio.sleep(1)

        with patch.object(reader, "MAX_SECONDS", 0.01):
            result = await reader.read_combat_fields(stalled, [binding], layout_id=DEV)
        self.assertEqual("read-timeout", result["rejected"][0]["reason"])

    async def test_bad_bindings_do_not_read(self):
        memory = Memory()
        binding, _ = memory.card()
        cases = (
            [binding, binding],
            [reader.ControlBinding(True, binding.address, "card")],
            [reader.ControlBinding(384, binding.address, "card")],
            [reader.ControlBinding(0, 0, "card")],
            [reader.ControlBinding(0, binding.address, "other")],
        )
        for case in cases:
            with self.assertRaises(ValueError):
                await reader.read_combat_fields(memory.read, case, layout_id=DEV)
        with self.assertRaises(ValueError):
            await reader.read_combat_fields(memory.read, [], layout_id="auto-detect")
        self.assertFalse(memory.calls)

    async def test_empty_does_not_claim_victory(self):
        memory = Memory()
        result = await reader.read_combat_fields(memory.read, [], layout_id=DEV)
        self.assertIsNone(result["phase"])
        self.assertEqual(0, result["bytesRead"])
        self.assertFalse(memory.calls)


if __name__ == "__main__":
    unittest.main()
