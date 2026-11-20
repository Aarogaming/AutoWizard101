"""Bounded source-layout duel correlation; no hook, scan, input or process API.

The caller supplies a separately image-validated ClientDuelManager address and
fresh owner IDs from its root-bound combatant controls. An empty map or read error
is unknown, never a win. All emitted fields remain unqualified source candidates.
"""

from __future__ import annotations

import struct

from w101_combat_reader import _Reader, _Rejected, _address, _flag

LAYOUT_ID = "deimos-development-df2283f"
MAX_DUELS = 16
MAX_PARTICIPANTS = 8
MAX_BYTES = 16384
MAX_READS = 256
PHASES = {
    0: "starting",
    1: "pre-planning",
    2: "planning",
    3: "pre-execution",
    4: "execution",
    5: "resolution",
    6: "victory",
    7: "ended",
}


class _DuelReader(_Reader):
    def __init__(self, read):
        super().__init__(read, None)
        self.stable_reads = []

    async def get(self, address, count):
        if self.bytes + count > MAX_BYTES or self.calls >= MAX_READS:
            raise _Rejected("read-budget")
        return await super().get(address, count)

    async def stable(self, address, count):
        data = await self.get(address, count)
        self.stable_reads.append((address, data))
        return data

    async def verify(self):
        for address, original in self.stable_reads:
            if original != await self.get(address, len(original)):
                raise _Rejected("changed-duel-context")

    async def duels(self, manager):
        await self.typed(manager, "ClientDuelManager")
        root = _address(struct.unpack("<Q", await self.stable(manager + 8, 8))[0], 8)
        header = await self.stable(root, 32)
        if not _flag(header[25]):
            raise _Rejected("map-sentinel")
        first = _address(struct.unpack_from("<Q", header, 8)[0], 8)
        pending = [] if first == root else [first]
        seen, keys, duel_addresses, result = set(), set(), set(), []
        while pending:
            node = pending.pop()
            if node == root:
                continue
            if node in seen or len(seen) >= MAX_DUELS:
                raise _Rejected("map-cycle-or-budget")
            seen.add(node)
            data = await self.stable(node, 48)
            if _flag(data[25]):
                # The reviewed std::map has one sentinel, not arbitrary leaves.
                raise _Rejected("map-sentinel")
            left, right = (
                struct.unpack_from("<Q", data)[0],
                struct.unpack_from("<Q", data, 16)[0],
            )
            key, duel = struct.unpack_from("<QQ", data, 32)
            duel = _address(duel, 8)
            if not key or key in keys or duel in duel_addresses:
                raise _Rejected("duplicate-duel")
            keys.add(key)
            duel_addresses.add(duel)
            result.append((key, duel))
            for child in (left, right):
                if child:
                    pending.append(_address(child, 8))
        return result

    async def duel(self, key, address):
        await self.typed(address, "Duel")
        identity = await self.stable(address + 72, 24)
        duel_id, start, end = struct.unpack("<QQQ", identity)
        if not duel_id or duel_id != key:
            raise _Rejected("duel-id-mismatch")
        if start == end == 0:
            vector = b""
        else:
            _address(start, 8)
            _address(end, 8)
            size = end - start
            if size < 0 or size % 16 or size > MAX_PARTICIPANTS * 16:
                raise _Rejected("participant-vector")
            vector = await self.stable(start, size) if size else b""
        owners, seen = set(), set()
        for offset in range(0, len(vector), 16):
            participant = _address(struct.unpack_from("<Q", vector, offset)[0], 8)
            if participant in seen:
                raise _Rejected("duplicate-participant")
            seen.add(participant)
            await self.typed(participant, "CombatParticipant")
            owner = struct.unpack("<Q", await self.stable(participant + 112, 8))[0]
            if not owner or owner in owners:
                raise _Rejected("duplicate-owner")
            owners.add(owner)
        turn = await self.stable(address + 192, 8)
        round_number, phase = struct.unpack("<ii", turn)
        if not 0 <= round_number <= 10000 or phase not in PHASES:
            raise _Rejected("duel-phase-or-round")
        return {
            "duelId": str(duel_id),
            "round": round_number,
            "phaseCode": phase,
            "phase": PHASES[phase],
            "participantOwnerIds": [str(owner) for owner in sorted(owners)],
        }, owners


async def read_duel_context(
    read_bytes, manager_address, observed_owner_ids, *, layout_id
):
    """Correlate one stable duel with all supplied freshly observed owner IDs.

    ``observed_owner_ids`` must be 1..8 unique canonical decimal uint64 strings
    from the current root-bound participant observation. The caller validates
    target/image, MOV signature and module ranges before supplying the manager
    address, and rechecks loading, target/lifetime and freshness afterward.
    ``status == 'matched'`` proves only source-layout consistency; it does not
    qualify input or establish whose victory was reported by a duel phase.
    """
    if layout_id != LAYOUT_ID:
        raise ValueError("Unsupported duel source layout")
    try:
        _address(manager_address, 8)
    except _Rejected:
        raise ValueError("Invalid duel manager binding") from None
    if (
        type(observed_owner_ids) not in (tuple, list)
        or not 1 <= len(observed_owner_ids) <= MAX_PARTICIPANTS
    ):
        raise ValueError("Invalid observed owner IDs")
    owners = set()
    for owner in observed_owner_ids:
        if (
            type(owner) is not str
            or not owner.isascii()
            or not owner.isdecimal()
            or not 1 <= len(owner) <= 20
            or owner.startswith("0")
            or not 0 < int(owner) <= 0xFFFFFFFFFFFFFFFF
            or int(owner) in owners
        ):
            raise ValueError("Invalid observed owner IDs")
        owners.add(int(owner))
    reader = _DuelReader(read_bytes)
    result = {
        "schemaVersion": 1,
        "layoutId": layout_id,
        "qualification": "source-layout-only",
        "metadataQualified": False,
        "status": "unknown",
        "reason": None,
        "duelId": None,
        "phase": None,
        "phaseCode": None,
        "round": None,
        "participantOwnerIds": [],
        "outcome": None,
        "winnerTeamId": None,
    }
    try:
        matches = []
        for key, address in await reader.duels(manager_address):
            candidate, candidate_owners = await reader.duel(key, address)
            if owners.issubset(candidate_owners):
                matches.append(candidate)
        await reader.verify()
        if len(matches) == 1:
            result.update(matches[0])
            result["status"] = "matched"
        else:
            result["reason"] = "no-matching-duel" if not matches else "ambiguous-duel"
    except _Rejected as error:
        result["reason"] = str(error)
    result["bytesRead"] = reader.bytes
    result["readCalls"] = reader.calls
    return result
