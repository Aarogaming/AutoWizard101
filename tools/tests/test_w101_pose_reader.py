import asyncio
import struct
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from w101_pose_reader import LAYOUT_ID, read_pose, player_hook, read_dimensions


class PoseReaderTests(unittest.IsolatedAsyncioTestCase):
    async def test_dimensions_remain_unqualified_and_reject_mutation(self):
        for mutate in (False, True):
            calls = 0

            async def read(address, count):
                nonlocal calls
                if count == 8:
                    return struct.pack("<Q", 0x20000)
                calls += 1
                return struct.pack(
                    "<6f", 1, 0, 0, 0, 0, 101 if mutate and calls == 2 else 100
                )

            value = await read_dimensions(read, 0x10000)
            self.assertFalse(value["collisionQualified"])
            self.assertEqual("unknown" if mutate else "candidate", value["status"])
            if not mutate:
                self.assertEqual(100, value["heightField"])
                self.assertEqual(64, value["readBytes"])

    async def test_hook_preserves_flags_and_register_on_both_paths(self):
        hook = player_hook(object)()
        code = await hook.bytecode_generator(
            [("player_struct", struct.pack("<Q", 0x20000))]
        )
        self.assertEqual(bytes.fromhex("9c 51"), code[:2])
        # Conditional branch skips only the ten-byte export store, so both paths
        # restore RCX and flags before replaying the displaced original instruction.
        branch_end = 17
        displacement = struct.unpack("<i", code[13:17])[0]
        self.assertEqual(
            bytes.fromhex("59 9d f2 0f 10 40 58"), code[branch_end + displacement :]
        )
        self.assertEqual(34, len(code))

    async def run_reader(self, *, change=None, marker=8, x=10.0, layout=LAYOUT_ID):
        body = 0x10000
        values = {
            body + 72: struct.pack("<Q", 0x20000),
            body + 0x474: struct.pack("<I", marker),
            body + 88: struct.pack("<6f", x, 20, 30, 0, 0, 1.5),
        }
        calls = {}

        async def read(address, count):
            calls[address] = calls.get(address, 0) + 1
            if change == "cancel":
                raise asyncio.CancelledError()
            if change == "error":
                raise OSError("private-address-secret")
            value = values[address]
            if change == address - body and calls[address] > 1:
                value = bytes([value[0] ^ 1]) + value[1:]
            return value

        return await read_pose(read, body, layout_id=layout)

    async def test_stable_pose_is_candidate_only(self):
        result = await self.run_reader()
        self.assertEqual("candidate", result["status"])
        self.assertEqual({"x": 10, "y": 20, "z": 30}, result["position"])
        self.assertEqual(6, result["readCalls"])
        self.assertEqual(72, result["readBytes"])
        self.assertFalse(result["liveQualified"])
        self.assertFalse(result["movementAuthorized"])

    async def test_torn_reads_are_unknown(self):
        for offset in (72, 0x474, 88):
            with self.subTest(offset=offset):
                result = await self.run_reader(change=offset)
                self.assertEqual("unknown", result["status"])
                self.assertIsNone(result["position"])

    async def test_nonfinite_or_extreme_pose_is_unknown(self):
        for x in (float("nan"), float("inf"), 1e20):
            self.assertEqual("unknown", (await self.run_reader(x=x))["status"])

    async def test_wrong_marker_and_layout_rejected(self):
        self.assertEqual("unknown", (await self.run_reader(marker=0))["status"])
        result = await self.run_reader(layout="guess")
        self.assertEqual(0, result["readCalls"])
        self.assertEqual("unknown", result["status"])

    async def test_transport_errors_do_not_expose_details(self):
        result = await self.run_reader(change="error")
        self.assertEqual("unknown", result["status"])
        self.assertNotIn("private-address-secret", str(result))

    async def test_cancellation_propagates(self):
        with self.assertRaises(asyncio.CancelledError):
            await self.run_reader(change="cancel")


if __name__ == "__main__":
    unittest.main()
