"""Bounded offline-testable ActorBody decoder; no discovery, hooks or inputs.

The owner must supply a separately validated local-player address and lifetime.
Archived layout compatibility is not current-client qualification. Coordinates
are candidates, not navigation authority or evidence that a road is clear.
"""

import math
import struct

from w101_combat_reader import _Reader, _Rejected, _address

LAYOUT_ID = "archived-wizwalker-actor-body"


async def read_dimensions(read_bytes, body_address):
    """Archived height132/scale112 candidates; not collision dimensions.

    Four bounded reads (64 bytes), with parent equality and repeated values.
    Caller separately owns target/build, actor marker and registry verification.
    """
    reader = _Reader(read_bytes, None)
    result = dict(status="unknown", collisionQualified=False)
    try:
        body = _address(body_address, 8)
        parent = await reader.get(body + 72, 8)
        _address(struct.unpack("<Q", parent)[0], 8)
        raw = await reader.get(body + 112, 24)
        scale, height = (
            struct.unpack_from("<f", raw)[0],
            struct.unpack_from("<f", raw, 20)[0],
        )
        if (
            not math.isfinite(scale)
            or not math.isfinite(height)
            or not 0 < scale <= 100
            or not 0 <= height <= 100000
        ):
            raise _Rejected("invalid-body-dimensions")
        if raw != await reader.get(body + 112, 24) or parent != await reader.get(
            body + 72, 8
        ):
            raise _Rejected("body-dimensions-changed")
        result.update(status="candidate", heightField=height, scaleField=scale)
    except _Rejected as error:
        result["reason"] = str(error)
    result.update(readCalls=reader.calls, readBytes=reader.bytes)
    return result


def player_hook(base):
    """Capture candidate ActorBody while preserving RCX and original flags.

    Replays the exact five displaced bytes; does not change game coordinates.
    The owner verifies entry/scratch/image before installation and after removal.
    """

    class ReviewedPlayerHook(base):
        async def bytecode_generator(self, packed_exports):
            return (
                bytes.fromhex(
                    "9c 51 8b 88 74 04 00 00 83 f9 08 0f 85 0a 00 00 00 48 a3"
                )
                + packed_exports[0][1]
                + bytes.fromhex("59 9d f2 0f 10 40 58")
            )

    return ReviewedPlayerHook


async def read_pose(read_bytes, body_address, *, layout_id):
    """Read at most 72 bytes in six calls; reject torn identity/pose snapshots.

    The inherited read transport supplies per-call timeouts and sanitized errors.
    Cancellation propagates. No address or external exception text is returned.
    A moving actor may yield Unknown; callers may reobserve under their budget.
    """
    reader = _Reader(read_bytes, None)
    result = {
        "layoutId": layout_id,
        "status": "unknown",
        "liveQualified": False,
        "movementAuthorized": False,
        "position": None,
        "yawRadians": None,
    }
    try:
        if layout_id != LAYOUT_ID:
            raise _Rejected("unreviewed-layout")
        body = _address(body_address, 8)
        parent = await reader.get(body + 72, 8)
        _address(struct.unpack("<Q", parent)[0], 8)
        marker = await reader.get(body + 0x474, 4)
        if struct.unpack("<I", marker)[0] != 8:
            raise _Rejected("actor-marker-mismatch")
        pose = await reader.get(body + 88, 24)
        x, y, z, pitch, roll, yaw = struct.unpack("<6f", pose)
        if not all(math.isfinite(v) for v in (x, y, z, pitch, roll, yaw)):
            raise _Rejected("nonfinite-pose")
        if max(abs(x), abs(y), abs(z)) > 10_000_000 or abs(yaw) > 2 * math.pi + 0.01:
            raise _Rejected("implausible-pose")
        if parent != await reader.get(body + 72, 8):
            raise _Rejected("actor-identity-changed")
        if marker != await reader.get(body + 0x474, 4):
            raise _Rejected("actor-identity-changed")
        if pose != await reader.get(body + 88, 24):
            raise _Rejected("pose-changed-during-read")
        result.update(
            status="candidate", position={"x": x, "y": y, "z": z}, yawRadians=yaw
        )
    except _Rejected as error:
        result["reason"] = str(error)
    result.update(readCalls=reader.calls, readBytes=reader.bytes)
    return result
