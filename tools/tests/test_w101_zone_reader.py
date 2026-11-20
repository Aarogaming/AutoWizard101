"""Synthetic archived-layout zone decoding; never opens a game process."""

import asyncio
from pathlib import Path
import struct
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from w101_zone_reader import LAYOUT_ID, read_zone


class ZoneReaderTests(unittest.IsolatedAsyncioTestCase):
    def setUp(self):
        self.name = b"WizardCity/WC_Hub"
        self.counts = {}
        self.mutate = None

    async def read(self, address, count):
        self.counts[address] = self.counts.get(address, 0) + 1
        header = bytearray(24)
        if len(self.name) >= 16:
            struct.pack_into("<Q", header, 0, 0x40000)
        else:
            header[: len(self.name)] = self.name
        struct.pack_into("<Q", header, 16, len(self.name))
        if address == 0x10000:
            raw = struct.pack("<Q", 0x50000)
        elif address == 0x10000 + 304:
            raw = struct.pack("<Q", 0x20000)
        elif address == 0x20000 + 88:
            raw = bytes(header[:count])
        elif address == 0x40000:
            raw = self.name
        else:
            raise AssertionError("Unexpected synthetic address")
        return self.mutate(address, count, raw) if self.mutate else raw

    async def run_zone(self, **kwargs):
        return await read_zone(
            self.read, 0x10000, layout_id=kwargs.get("layout_id", LAYOUT_ID)
        )

    async def test_heap_path_candidate_and_bounded_counters(self):
        result = await self.run_zone()
        self.assertEqual("WizardCity/WC_Hub", result["zoneName"])
        self.assertEqual("candidate", result["status"])
        self.assertFalse(result["liveQualified"])
        self.assertFalse(result["movementAuthorized"])
        self.assertEqual(8, result["readCalls"])
        self.assertLessEqual(result["readBytes"], 336)
        self.assertNotIn("address", str(result).lower())

    async def test_inline_path(self):
        self.name = b"World/Zone"
        self.assertEqual("World/Zone", (await self.run_zone())["zoneName"])

    async def test_wrong_layout_does_not_read(self):
        result = await self.run_zone(layout_id="guess")
        self.assertEqual("unreviewed-layout", result["reason"])
        self.assertEqual(0, result["readCalls"])

    async def test_length_rejected_before_string_read(self):
        self.name = b"A/" + b"B" * 127
        result = await self.run_zone()
        self.assertEqual("invalid-zone-length", result["reason"])
        self.assertNotIn(0x40000, self.counts)

    async def test_path_allowlist_rejects_private_or_traversal_like_text(self):
        for name in (
            b"hello world",
            b"../zone",
            b"A//B",
            b"A/B\0",
            b"A/\xff",
            b"C:/Game",
        ):
            self.name = name
            result = await self.run_zone()
            self.assertEqual("unknown", result["status"])
            self.assertIsNone(result["zoneName"])

    async def test_fences_detect_changes(self):
        for address, reason in (
            (0x40000, "zone-string-changed"),
            (0x20000 + 88, "zone-header-changed"),
            (0x10000 + 304, "zone-binding-changed"),
            (0x10000, "client-object-changed"),
        ):
            self.counts = {}
            self.mutate = lambda a, c, raw: (
                bytes([raw[0] ^ 1]) + raw[1:]
                if a == address and self.counts[a] > 1
                else raw
            )
            result = await self.run_zone()
            self.assertEqual(reason, result["reason"])
            self.assertIsNone(result["zoneName"])

    async def test_invalid_zone_pointer(self):
        self.mutate = lambda a, c, raw: bytes(8) if a == 0x10000 + 304 else raw
        self.assertEqual("invalid-pointer", (await self.run_zone())["reason"])

    async def test_transport_failure_is_sanitized(self):
        async def broken(address, count):
            raise OSError("private address secret")

        result = await read_zone(broken, 0x10000, layout_id=LAYOUT_ID)
        self.assertEqual("unknown", result["status"])
        self.assertNotIn("private", str(result))

    async def test_cancellation_propagates(self):
        async def cancelled(address, count):
            raise asyncio.CancelledError()

        with self.assertRaises(asyncio.CancelledError):
            await read_zone(cancelled, 0x10000, layout_id=LAYOUT_ID)


if __name__ == "__main__":
    unittest.main()
