"""Bounded combat fields from explicitly supplied, already-rooted UI controls.

This module has no process access, hooks, input, discovery, or live layout
qualification. The owner supplies an async read_bytes(address, count), vetted
control addresses, privacy/ancestry filtering, target lifetime and a hard worker
deadline. Every result remains source-layout-only until independently qualified.
No pointer is included in the returned JSON-compatible observation.
"""

from __future__ import annotations

import asyncio
from dataclasses import dataclass
import struct
import time

VERSION = "w101-combat-fields-v1"
MAX_BYTES = 65536
MAX_READS = 768
MAX_CARDS = 16
MAX_PARTICIPANTS = 8
MAX_EFFECTS = 8
MAX_STRING = 128
MAX_SECONDS = 2.0


@dataclass(frozen=True)
class ControlBinding:
    """Internal-only root traversal binding. Never serialize this object."""

    index: int
    address: int
    kind: str  # card or participant; provided by the owning tree's allowlist


@dataclass(frozen=True)
class _Layout:
    spell_pointer: int
    grayed: int
    checked: int | None
    participant_pointer: int
    spell_id: int
    treasure: int
    template_type: int
    health: int
    pip_pointer: bool


# Definitions are deliberately separate. Never probe several offsets until one
# looks plausible. An image pin for the root hook does not qualify these fields.
_LAYOUTS = {
    "archived-wizwalker": _Layout(952, 1024, None, 1656, 272, 265, 312, 236, False),
    "deimos-development-df2283f": _Layout(
        1104, 1208, 1028, 1888, 204, 197, 352, 244, True
    ),
}


class _Rejected(Exception):
    """Fixed reason code only; external exception text may contain addresses."""


def _address(value, alignment=1):
    if (
        type(value) is not int
        or not 65536 <= value < 0x7FFFFFFFFFFF
        or value % alignment
    ):
        raise _Rejected("invalid-pointer")
    return value


def _flag(value):
    if value not in (0, 1):
        raise _Rejected("invalid-boolean")
    return bool(value)


class _Reader:
    def __init__(self, read, layout):
        self.read = read
        self.layout = layout
        self.bytes = 0
        self.calls = 0
        self.deadline = time.monotonic() + MAX_SECONDS

    async def get(self, address, count):
        _address(address)
        if (
            type(count) is not int
            or not 0 < count <= 2048
            or address + count > 0x7FFFFFFFFFFF
            or self.bytes + count > MAX_BYTES
            or self.calls >= MAX_READS
        ):
            raise _Rejected("read-budget")
        remaining = self.deadline - time.monotonic()
        if remaining <= 0:
            raise _Rejected("read-timeout")
        self.bytes += count
        self.calls += 1
        try:
            data = await asyncio.wait_for(
                self.read(address, count), min(0.25, remaining)
            )
        except asyncio.TimeoutError:
            raise _Rejected("read-timeout") from None
        except Exception:
            raise _Rejected("read-failed") from None
        if not isinstance(data, (bytes, bytearray)) or len(data) != count:
            raise _Rejected("short-read")
        return bytes(data)

    async def ptr(self, address):
        return _address(struct.unpack("<Q", await self.get(address, 8))[0], 8)

    async def typed(self, address, expected):
        """Validate the documented PropertyClass type-name stub, not RTTI guesses."""
        vtable = await self.ptr(address)
        function = _address(struct.unpack("<Q", await self.get(vtable, 8))[0])
        prefix = await self.get(function, 5)
        if prefix[0] == 0xE9:
            function = _address(function + 5 + struct.unpack("<i", prefix[1:])[0])
        lea = await self.get(function + 63, 7)
        if lea[:3] != b"\x48\x8d\x0d":
            raise _Rejected("type-instruction")
        name_address = _address(function + 70 + struct.unpack("<i", lea[3:])[0])
        name = await self.get(name_address, 60)
        if b"\0" not in name or name.split(b"\0", 1)[0] != expected.encode("ascii"):
            raise _Rejected("type-mismatch")

    async def string(self, address):
        header = await self.get(address, 24)
        length = struct.unpack_from("<Q", header, 16)[0]
        if not 0 < length <= MAX_STRING:
            raise _Rejected("string-length")
        raw = (
            header[:length]
            if length < 16
            else await self.get(_address(struct.unpack_from("<Q", header)[0]), length)
        )
        if header != await self.get(address, 24):
            raise _Rejected("changed-string")
        if length >= 16 and raw != await self.get(
            _address(struct.unpack_from("<Q", header)[0]), length
        ):
            raise _Rejected("changed-string")
        try:
            text = raw.decode("utf-8")
        except UnicodeDecodeError:
            raise _Rejected("string-encoding") from None
        if not text.isprintable():
            raise _Rejected("string-characters")
        return text

    async def effects(self, address):
        header = await self.get(address, 16)
        start, end = struct.unpack("<QQ", header)
        if start == end == 0:
            pointers = b""
        else:
            _address(start, 8)
            _address(end, 8)
            size = end - start
            if size < 0 or size % 16 or size > MAX_EFFECTS * 16:
                raise _Rejected("effect-vector")
            pointers = await self.get(start, size) if size else b""
        result, seen = [], set()
        for offset in range(0, len(pointers), 16):
            effect = _address(struct.unpack_from("<Q", pointers, offset)[0], 8)
            if effect in seen:
                raise _Rejected("duplicate-effect")
            seen.add(effect)
            # Variants can contain conditionals/random/nested effects. Reject
            # rather than flattening one into a known base damage effect.
            await self.typed(effect, "SpellEffect")
            data = await self.get(effect + 72, 88)
            effect_type, parameter, disposition, school = struct.unpack_from(
                "<iiiI", data
            )
            target, rounds, per_round = struct.unpack_from("<iii", data, 68)
            if not 0 <= effect_type <= 1024 or not 0 <= target <= 17:
                raise _Rejected("effect-enum")
            if not -10000000 <= parameter <= 10000000 or not 0 <= rounds <= 10000:
                raise _Rejected("effect-range")
            cloaked = _flag(data[85])
            result.append(
                {
                    "effectType": effect_type,
                    "parameter": parameter,
                    "disposition": disposition,
                    "schoolId": school,
                    "target": target,
                    "rounds": rounds,
                    "parameterPerRound": per_round,
                    "cloaked": cloaked,
                }
            )
            if data != await self.get(effect + 72, 88):
                raise _Rejected("changed-effect")
        if header != await self.get(address, 16):
            raise _Rejected("changed-effect-vector")
        if pointers and pointers != await self.get(start, len(pointers)):
            raise _Rejected("changed-effect-vector")
        return result

    async def control_flags(self, address):
        flags = struct.unpack("<I", await self.get(address + 156, 4))[0]
        if not flags & 1:
            raise _Rejected("hidden-control")
        return flags

    async def card(self, binding):
        control = binding.address
        await self.typed(control, "SpellCheckBox")
        flags = await self.control_flags(control)
        spell = await self.ptr(control + self.layout.spell_pointer)
        await self.typed(spell, "GraphicalSpell")
        template = await self.ptr(spell + 120)
        await self.typed(template, "SpellTemplate")
        identity = await self.get(spell + 128, 12)
        template_id, accuracy, school = (
            struct.unpack_from("<I", identity)[0],
            identity[4],
            struct.unpack_from("<I", identity, 8)[0],
        )
        spell_id = await self.get(spell + self.layout.spell_id, 4)
        if not template_id or accuracy > 100:
            raise _Rejected("spell-range")
        enchantment = struct.unpack("<I", await self.get(spell + 80, 4))[0]
        # Current dynamic effects, not template estimates or catalog averages.
        effects = await self.effects(spell + 88)
        name = await self.string(template + 96)
        spell_type = await self.string(template + self.layout.template_type)
        grayed = _flag((await self.get(control + self.layout.grayed, 1))[0])
        checked = (
            _flag((await self.get(control + self.layout.checked, 1))[0])
            if self.layout.checked is not None
            else None
        )
        treasure = _flag((await self.get(spell + self.layout.treasure, 1))[0])
        if (
            spell != await self.ptr(control + self.layout.spell_pointer)
            or template != await self.ptr(spell + 120)
            or identity != await self.get(spell + 128, 12)
            or spell_id != await self.get(spell + self.layout.spell_id, 4)
            or enchantment != struct.unpack("<I", await self.get(spell + 80, 4))[0]
            or treasure != _flag((await self.get(spell + self.layout.treasure, 1))[0])
            or flags != await self.control_flags(control)
            or grayed != _flag((await self.get(control + self.layout.grayed, 1))[0])
            or (
                self.layout.checked is not None
                and checked
                != _flag((await self.get(control + self.layout.checked, 1))[0])
            )
        ):
            raise _Rejected("changed-card")
        return {
            "controlIndex": binding.index,
            "templateId": template_id,
            "spellId": struct.unpack("<I", spell_id)[0],
            "templateName": name,
            "spellType": spell_type,
            "schoolId": school,
            "accuracy": accuracy,
            "enchantmentId": enchantment,
            "treasure": treasure,
            "grayedCandidate": grayed,
            "castableCandidate": not grayed and not flags & 0x80000000,
            "checkedCandidate": checked,
            "selected": None,
            "effects": effects,
        }

    async def participant(self, binding):
        control = binding.address
        await self.typed(control, "CombatantDataControl")
        flags = await self.control_flags(control)
        participant = await self.ptr(control + self.layout.participant_pointer)
        await self.typed(participant, "CombatParticipant")
        data = await self.get(participant + 112, self.layout.health - 112 + 9)
        owner_id, template_id = struct.unpack_from("<QQ", data)
        is_player = _flag(data[16])
        team_id, school = struct.unpack_from("<iI", data, 32)
        hp_offset = self.layout.health - 112
        health, max_health = struct.unpack_from("<ii", data, hp_offset)
        hidden_health = _flag(data[hp_offset + 8])
        if (
            not owner_id
            or not 0 <= team_id <= 1024
            or not 0 <= health <= max_health <= 10000000
            or max_health == 0
        ):
            raise _Rejected("participant-range")
        if self.layout.pip_pointer:
            pip_pointer = _address(struct.unpack_from("<Q", data, 40)[0], 8)
            await self.typed(pip_pointer, "PipCount")
            pip_bytes = await self.get(pip_pointer + 80, 10)
            if sum(pip_bytes[:9]) > 7 or pip_bytes[9] > 2:
                raise _Rejected("pip-range")
            if pip_bytes != await self.get(pip_pointer + 80, 10):
                raise _Rejected("changed-pips")
            pips = dict(
                zip(
                    (
                        "generic",
                        "power",
                        "balance",
                        "death",
                        "fire",
                        "ice",
                        "life",
                        "myth",
                        "storm",
                        "shadow",
                    ),
                    pip_bytes,
                )
            )
        else:
            ordinary, power, shadow = data[40:43]
            if ordinary + power > 7 or shadow > 2:
                raise _Rejected("pip-range")
            pips = {"generic": ordinary, "power": power, "shadow": shadow}
        if (
            participant != await self.ptr(control + self.layout.participant_pointer)
            or data != await self.get(participant + 112, len(data))
            or flags != await self.control_flags(control)
        ):
            raise _Rejected("changed-participant")
        return {
            "controlIndex": binding.index,
            "ownerId": str(owner_id),
            "templateId": str(template_id),
            "teamId": team_id,
            "isPlayer": is_player,
            "schoolId": school,
            "health": None if hidden_health else health,
            "maxHealth": None if hidden_health else max_health,
            "healthHidden": hidden_health,
            "pips": pips,
            "schoolPipsKnown": self.layout.pip_pointer,
            "relation": None,
        }


async def read_combat_fields(read_bytes, controls, *, layout_id):
    """Read source-layout candidates only; this is not an executable snapshot.

    ``controls`` is a list/tuple of ControlBinding values supplied exclusively by
    the owning root traversal. Indices must be unique, snapshot-local 0..383;
    pointers must not be persisted or reused in a future observation. Unsupported
    types/layouts and changing data fail closed. External exception text is never
    exposed. Cancellation propagates. The owner must discard results on loading,
    target changes, incomplete tree traversal, stale sampling or cleanup failure.
    """
    if type(layout_id) is not str or layout_id not in _LAYOUTS:
        raise ValueError("Unsupported combat source layout")
    if (
        type(controls) not in (list, tuple)
        or len(controls) > MAX_CARDS + MAX_PARTICIPANTS
    ):
        raise ValueError("Invalid combat control list")
    seen_indices, seen_addresses = set(), set()
    kinds = {"card": 0, "participant": 0}
    for binding in controls:
        if (
            type(binding) is not ControlBinding
            or type(binding.index) is not int
            or not 0 <= binding.index < 384
            or type(binding.address) is not int
            or type(binding.kind) is not str
            or binding.index in seen_indices
            or binding.address in seen_addresses
            or binding.kind not in kinds
        ):
            raise ValueError("Invalid combat control binding")
        try:
            _address(binding.address, 8)
        except _Rejected:
            raise ValueError("Invalid combat control binding") from None
        seen_indices.add(binding.index)
        seen_addresses.add(binding.address)
        kinds[binding.kind] += 1
    if kinds["card"] > MAX_CARDS or kinds["participant"] > MAX_PARTICIPANTS:
        raise ValueError("Combat control budget exceeded")
    reader = _Reader(read_bytes, _LAYOUTS[layout_id])
    result = {
        "schemaVersion": 1,
        "layoutId": layout_id,
        "qualification": "source-layout-only",
        "metadataQualified": False,
        "phase": None,
        "round": None,
        "selectedCard": None,
        "cards": [],
        "participants": [],
        "rejected": [],
    }
    for binding in controls:
        try:
            value = await (
                reader.card(binding)
                if binding.kind == "card"
                else reader.participant(binding)
            )
            result["cards" if binding.kind == "card" else "participants"].append(value)
        except _Rejected as error:
            result["rejected"].append(
                {
                    "controlIndex": binding.index,
                    "kind": binding.kind,
                    "reason": str(error),
                }
            )
            if str(error) in ("read-budget", "read-timeout"):
                # No partial-budget observation is suitable even for comparison.
                result["cards"].clear()
                result["participants"].clear()
                break
    result["bytesRead"] = reader.bytes
    result["readCalls"] = reader.calls
    return result
