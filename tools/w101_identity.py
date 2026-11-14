"""Bounded read-only identity lookup for the reviewed d410 W101 executable.

The caller pins IMAGE_SHA256 and target lifetime, supplies an async read(address,
size), and owns the surrounding deadline. This helper opens no process, installs
no hook, and performs no writes/refcount changes. Static instruction guards and
SizeOfImage are checked before following the current-client registry lookup.

At most 16 registry nodes, 64 reads, 2048 requested bytes and one second per call.
All traversed nodes, root bindings, lookup key, selected owner and ID are read
again; changes become unknown. This rejects observed races but does not lock or
pin game objects and cannot exclude ABA changes. The caller should compare this
identity before/after its stats reads too. No raw pointers/IDs enter the result.
CharacterKey is SHA256(domain + little-endian eight-byte character ID), stable
across scans/restarts; profileId is its first 32 hex characters. It is an opaque
identifier, not a secret or encryption, and grants no action authority.
"""

import asyncio
import hashlib
import struct
import time

IMAGE_SHA256 = "d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a"
IMAGE_SIZE = 0x3B69000
SOURCE = "client-object-registry-v1"
MAX_NODES = 16
MAX_READ_CALLS = 64
MAX_READ_BYTES = 2048
MAX_SECONDS = 1.0
DOMAIN = b"maelstrom.w101.character-id.v1\0"
CLIENT_RVA = 0x3762040
KEY_OFFSET = 0x213A8
SENTINEL_RVA = 0x37EBA38
BUCKETS_RVA = 0x37EBA48
MASK_RVA = 0x37EBA60
CHARACTER_ID_OFFSET = 0x1C0
STATS_OFFSET = 0x230
SIGNATURES = (
    (0x16A1BF6, bytes.fromhex("48 8b 15 43 04 0c 02")),
    (0x16A1C05, bytes.fromhex("48 8b 92 a8 13 02 00")),
    (0x2078B00, bytes.fromhex("48 8b 81 30 02 00 00 c3")),
    (0x16C0597, bytes.fromhex("4c 8d 86 c0 01 00 00")),
    (0x16C373C, bytes.fromhex("48 23 15 1d 83 12 02")),
    (0x16C3749, bytes.fromhex("4c 8b 05 f8 82 12 02")),
    (0x16C3760, bytes.fromhex("48 8b 15 d1 82 12 02")),
)


class _Rejected(Exception):
    pass


def _pointer(value):
    if type(value) is not int or not 65536 <= value < 0x7FFFFFFFFFFF or value % 8:
        raise _Rejected("invalid-pointer")
    return value


def _qword(raw):
    return struct.unpack("<Q", raw)[0]


class _Reader:
    def __init__(self, read):
        self.read = read
        self.calls = self.bytes = 0
        self.deadline = time.monotonic() + MAX_SECONDS

    async def get(self, address, count):
        if (
            type(address) is not int
            or not 65536 <= address < 0x7FFFFFFFFFFF
            or address + count >= 0x7FFFFFFFFFFF
        ):
            raise _Rejected("invalid-read-range")
        if self.calls >= MAX_READ_CALLS or self.bytes + count > MAX_READ_BYTES:
            raise _Rejected("read-budget")
        remaining = self.deadline - time.monotonic()
        if remaining <= 0:
            raise _Rejected("read-timeout")
        self.calls += 1
        self.bytes += count
        try:
            raw = await asyncio.wait_for(
                self.read(address, count), min(0.25, remaining)
            )
        except asyncio.TimeoutError:
            raise _Rejected("read-timeout") from None
        except Exception:
            raise _Rejected("read-failed") from None
        if not isinstance(raw, bytes) or len(raw) != count:
            raise _Rejected("incomplete-read")
        return raw


async def read_identity(read, base, image_size, expected_stats):
    """Return a stable opaque identity only when its owner matches expected_stats.

    Unknown results contain fixed reason codes and counters. Cancellation
    propagates. The supplied transport must have no side effects beyond reading.
    """
    reader = _Reader(read)
    result = dict(
        status="unknown",
        source=SOURCE,
        characterKey=None,
        profileId=None,
        statsMatched=False,
        nodesVisited=0,
    )
    try:
        _pointer(base)
        _pointer(expected_stats)
        if (
            type(image_size) is not int
            or image_size != IMAGE_SIZE
            or base + image_size >= 0x7FFFFFFFFFFF
        ):
            raise _Rejected("unreviewed-image")
        for rva, expected in SIGNATURES:
            if (
                rva + len(expected) > image_size
                or await reader.get(base + rva, len(expected)) != expected
            ):
                raise _Rejected("instruction-mismatch")
        client_raw = await reader.get(base + CLIENT_RVA, 8)
        client = _pointer(_qword(client_raw))
        key_raw = await reader.get(client + KEY_OFFSET, 8)
        key = _qword(key_raw)
        if key == 0:
            raise _Rejected("local-key-unavailable")
        roots = [
            (base + rva, await reader.get(base + rva, 8))
            for rva in (SENTINEL_RVA, BUCKETS_RVA, MASK_RVA)
        ]
        sentinel, buckets = (_pointer(_qword(raw)) for _, raw in roots[:2])
        mask = _qword(roots[2][1])
        if mask > 0xFFFFF or mask & (mask + 1):
            raise _Rejected("invalid-bucket-mask")
        index = ((key >> 32) ^ (key & 0xFFFFFFFF)) & mask
        pair_address = buckets + index * 16
        pair_raw = await reader.get(pair_address, 16)
        first, last = map(_pointer, struct.unpack("<QQ", pair_raw))
        if first == sentinel:
            raise _Rejected("local-object-not-found")
        end_raw = await reader.get(last, 8)
        end = _pointer(_qword(end_raw))
        visited = {}
        node = first
        while node != end:
            if node in visited:
                raise _Rejected("registry-cycle")
            if len(visited) >= MAX_NODES:
                raise _Rejected("node-budget")
            raw = await reader.get(_pointer(node), 40)
            visited[node] = raw
            result["nodesVisited"] = len(visited)
            next_node, _, node_key, owner, control = struct.unpack("<5Q", raw)
            if node_key == key:
                break
            if node_key > key:
                raise _Rejected("local-object-not-found")
            node = _pointer(next_node)
        else:
            raise _Rejected("local-object-not-found")
        owner, control = _pointer(owner), _pointer(control)
        if struct.unpack("<i", await reader.get(control + 8, 4))[0] <= 0:
            raise _Rejected("owner-expired")
        character_raw = await reader.get(owner + CHARACTER_ID_OFFSET, 8)
        if _qword(character_raw) == 0:
            raise _Rejected("character-id-unavailable")
        stats_raw = await reader.get(owner + STATS_OFFSET, 8)
        if _pointer(_qword(stats_raw)) != expected_stats:
            raise _Rejected("stats-owner-mismatch")
        # Reverse the dependent reads before revisiting the roots. No stale
        # node can be accepted merely because its numeric values look plausible.
        if (
            await reader.get(owner + STATS_OFFSET, 8) != stats_raw
            or await reader.get(owner + CHARACTER_ID_OFFSET, 8) != character_raw
        ):
            raise _Rejected("owner-changed")
        if struct.unpack("<i", await reader.get(control + 8, 4))[0] <= 0:
            raise _Rejected("owner-expired")
        for address, raw in reversed(list(visited.items())):
            if await reader.get(address, len(raw)) != raw:
                raise _Rejected("registry-node-changed")
        if (
            await reader.get(last, 8) != end_raw
            or await reader.get(pair_address, 16) != pair_raw
        ):
            raise _Rejected("bucket-changed")
        for address, raw in roots:
            if await reader.get(address, 8) != raw:
                raise _Rejected("registry-root-changed")
        if (
            await reader.get(client + KEY_OFFSET, 8) != key_raw
            or await reader.get(base + CLIENT_RVA, 8) != client_raw
        ):
            raise _Rejected("local-client-changed")
        token = hashlib.sha256(DOMAIN + character_raw).hexdigest()
        result.update(
            status="observed",
            characterKey=token,
            profileId=token[:32],
            statsMatched=True,
        )
    except _Rejected as error:
        result["reason"] = str(error)
    result.update(readCalls=reader.calls, readBytes=reader.bytes)
    return result
