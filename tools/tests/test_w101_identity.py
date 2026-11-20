import asyncio
import hashlib
import json
from pathlib import Path
import struct
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import w101_identity as identity

BASE = 0x140000000
CLIENT = 0x100000
SENTINEL = 0x200000
BUCKETS = 0x300000
NODE = 0x400000
OWNER = 0x500000
CONTROL = 0x600000
STATS = 0x700000
KEY = 100
CHARACTER = 0x123456789ABCDEF0


class Memory:
    def __init__(self, count=1):
        self.data = {}
        self.calls = {}
        self.changes = {}
        for rva, raw in identity.SIGNATURES:
            self.put(BASE + rva, raw)
        self.qword(BASE + identity.CLIENT_RVA, CLIENT)
        self.qword(CLIENT + identity.KEY_OFFSET, KEY)
        self.qword(BASE + identity.SENTINEL_RVA, SENTINEL)
        self.qword(BASE + identity.BUCKETS_RVA, BUCKETS)
        self.qword(BASE + identity.MASK_RVA, 0)
        self.put(BUCKETS, struct.pack("<QQ", NODE, NODE + (count - 1) * 0x100))
        for index in range(count):
            address = NODE + index * 0x100
            next_node = SENTINEL if index == count - 1 else address + 0x100
            key = KEY if index == count - 1 else index + 1
            self.put(
                address, struct.pack("<5Q", next_node, SENTINEL, key, OWNER, CONTROL)
            )
        self.put(CONTROL + 8, struct.pack("<i", 1))
        self.qword(OWNER + identity.CHARACTER_ID_OFFSET, CHARACTER)
        self.qword(OWNER + identity.STATS_OFFSET, STATS)

    def put(self, address, raw):
        self.data.update({address + index: value for index, value in enumerate(raw)})

    def qword(self, address, value):
        self.put(address, struct.pack("<Q", value))

    async def read(self, address, count):
        self.calls[address] = self.calls.get(address, 0) + 1
        mutation = self.changes.get((address, self.calls[address]))
        if mutation:
            self.put(address, mutation)
        return bytes(self.data[address + index] for index in range(count))


class IdentityTests(unittest.IsolatedAsyncioTestCase):
    async def result(self, memory, **options):
        return await identity.read_identity(
            memory.read,
            BASE,
            options.get("image_size", identity.IMAGE_SIZE),
            options.get("expected_stats", STATS),
            expected_owner=options.get("expected_owner"),
        )

    async def test_pose_parent_must_equal_registry_owner(self):
        good = await self.result(Memory(), expected_owner=OWNER)
        self.assertEqual("observed", good["status"])
        self.assertTrue(good["ownerMatched"])
        wrong = await self.result(Memory(), expected_owner=OWNER + 8)
        self.assertEqual("unknown", wrong["status"])
        self.assertEqual("local-owner-mismatch", wrong["reason"])
        self.assertIsNone(wrong["characterKey"])

    async def test_stable_owned_character_is_hashed_without_raw_identity(self):
        memory = Memory()
        result = await self.result(memory)
        token = hashlib.sha256(
            identity.DOMAIN + struct.pack("<Q", CHARACTER)
        ).hexdigest()
        self.assertEqual("observed", result["status"])
        self.assertTrue(result["statsMatched"])
        self.assertEqual(token, result["characterKey"])
        self.assertEqual(token[:32], result["profileId"])
        self.assertEqual(identity.SOURCE, result["source"])
        self.assertEqual(result, await self.result(Memory()))
        self.assertNotIn(str(CHARACTER), json.dumps(result))
        self.assertNotIn(str(OWNER), json.dumps(result))
        self.assertLessEqual(result["readCalls"], identity.MAX_READ_CALLS)
        self.assertLessEqual(result["readBytes"], identity.MAX_READ_BYTES)

    async def test_maximum_chain_is_bounded_and_valid(self):
        result = await self.result(Memory(16))
        self.assertEqual("observed", result["status"])
        self.assertEqual(16, result["nodesVisited"])
        self.assertLessEqual(result["readCalls"], 64)
        self.assertLessEqual(result["readBytes"], 2048)

    async def test_bucket_uses_high_low_key_xor_and_character_id_is_separate(self):
        memory = Memory()
        key = 0x0000000100000002
        memory.qword(CLIENT + identity.KEY_OFFSET, key)
        memory.qword(BASE + identity.MASK_RVA, 7)
        memory.put(BUCKETS + 3 * 16, struct.pack("<QQ", NODE, NODE))
        memory.qword(NODE + 16, key)
        result = await self.result(memory)
        self.assertEqual("observed", result["status"])
        self.assertEqual(
            hashlib.sha256(identity.DOMAIN + struct.pack("<Q", CHARACTER)).hexdigest(),
            result["characterKey"],
        )

    async def test_changed_bucket_end_is_rejected(self):
        memory = Memory()
        memory.changes[NODE, 4] = struct.pack("<Q", SENTINEL + 8)
        self.assertEqual("bucket-changed", (await self.result(memory))["reason"])

    async def test_wrong_expected_stats_and_zero_id_rejected(self):
        result = await self.result(Memory(), expected_stats=STATS + 8)
        self.assertEqual("stats-owner-mismatch", result["reason"])
        self.assertFalse(result["statsMatched"])
        memory = Memory()
        memory.qword(OWNER + identity.CHARACTER_ID_OFFSET, 0)
        self.assertEqual(
            "character-id-unavailable", (await self.result(memory))["reason"]
        )

    async def test_changed_roots_key_node_owner_id_and_stats_rejected(self):
        cases = (
            (BASE + identity.CLIENT_RVA, 2, CLIENT + 8, "local-client-changed"),
            (CLIENT + identity.KEY_OFFSET, 2, KEY + 1, "local-client-changed"),
            (BASE + identity.SENTINEL_RVA, 2, SENTINEL + 8, "registry-root-changed"),
            (BASE + identity.BUCKETS_RVA, 2, BUCKETS + 8, "registry-root-changed"),
            (BASE + identity.MASK_RVA, 2, 1, "registry-root-changed"),
            (OWNER + identity.CHARACTER_ID_OFFSET, 2, CHARACTER + 1, "owner-changed"),
            (OWNER + identity.STATS_OFFSET, 2, STATS + 8, "owner-changed"),
            # Single node is also bucket.last, so its first read obtains next only.
            (NODE, 3, SENTINEL + 8, "registry-node-changed"),
        )
        for address, call, value, expected in cases:
            with self.subTest(address=address):
                memory = Memory()
                memory.changes[address, call] = struct.pack("<Q", value)
                result = await self.result(memory)
                self.assertEqual(expected, result["reason"])
                self.assertIsNone(result["characterKey"])

    async def test_changed_node_key_and_owner_pointer_rejected(self):
        for offset, value in ((16, KEY + 1), (24, OWNER + 8), (32, CONTROL + 8)):
            memory = Memory()
            raw = bytearray(bytes(memory.data[NODE + i] for i in range(40)))
            struct.pack_into("<Q", raw, offset, value)
            memory.changes[NODE, 3] = bytes(raw)
            result = await self.result(memory)
            self.assertEqual("registry-node-changed", result["reason"])

    async def test_control_must_remain_live(self):
        for occurrence in (1, 2):
            memory = Memory()
            memory.changes[CONTROL + 8, occurrence] = struct.pack("<i", 0)
            self.assertEqual("owner-expired", (await self.result(memory))["reason"])

    async def test_cycle_and_over_budget_chain_rejected(self):
        memory = Memory(2)
        memory.qword(NODE, NODE)
        self.assertEqual("registry-cycle", (await self.result(memory))["reason"])
        result = await self.result(Memory(17))
        self.assertEqual("node-budget", result["reason"])
        self.assertEqual(16, result["nodesVisited"])

    async def test_invalid_pointer_mask_build_and_signature_rejected(self):
        memory = Memory()
        memory.qword(BASE + identity.CLIENT_RVA, 0)
        self.assertEqual("invalid-pointer", (await self.result(memory))["reason"])
        memory = Memory()
        memory.qword(BASE + identity.MASK_RVA, 2)
        self.assertEqual("invalid-bucket-mask", (await self.result(memory))["reason"])
        self.assertEqual(
            "unreviewed-image", (await self.result(Memory(), image_size=1))["reason"]
        )
        memory = Memory()
        memory.put(BASE + identity.SIGNATURES[0][0], b"\0" * 7)
        self.assertEqual("instruction-mismatch", (await self.result(memory))["reason"])

    async def test_empty_sorted_miss_and_zero_local_key_rejected(self):
        memory = Memory()
        memory.put(BUCKETS, struct.pack("<QQ", SENTINEL, SENTINEL))
        self.assertEqual(
            "local-object-not-found", (await self.result(memory))["reason"]
        )
        memory = Memory()
        memory.qword(NODE + 16, KEY + 1)
        self.assertEqual(
            "local-object-not-found", (await self.result(memory))["reason"]
        )
        memory = Memory()
        memory.qword(CLIENT + identity.KEY_OFFSET, 0)
        self.assertEqual("local-key-unavailable", (await self.result(memory))["reason"])

    async def test_bucket_changes_are_rejected(self):
        memory = Memory()
        memory.changes[BUCKETS, 2] = struct.pack("<QQ", NODE + 8, NODE)
        self.assertEqual("bucket-changed", (await self.result(memory))["reason"])

    async def test_failed_short_reads_sanitized_and_cancellation_propagates(self):
        async def failure(address, count):
            raise RuntimeError("private address/ID detail")

        result = await identity.read_identity(failure, BASE, identity.IMAGE_SIZE, STATS)
        self.assertEqual("read-failed", result["reason"])
        self.assertNotIn("private", json.dumps(result))

        async def short(address, count):
            return b""

        self.assertEqual(
            "incomplete-read",
            (await identity.read_identity(short, BASE, identity.IMAGE_SIZE, STATS))[
                "reason"
            ],
        )

        async def cancelled(address, count):
            raise asyncio.CancelledError()

        with self.assertRaises(asyncio.CancelledError):
            await identity.read_identity(cancelled, BASE, identity.IMAGE_SIZE, STATS)


if __name__ == "__main__":
    unittest.main()
