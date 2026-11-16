"""Injected candidate decoder for archived client-object/zone offsets.

No discovery, process access, hooks, writes, or current-client qualification.
The owner must independently bind the client object to the target lifetime and
invalidate it on loading. Equality fences reject observed races, not ABA reuse.
"""

import asyncio
import re
import struct

from w101_combat_reader import _Reader, _Rejected, _address

LAYOUT_ID = "archived-wizwalker-client-zone"
MAX_STRING_BYTES = 128
MAX_SECONDS = 1.0
_ZONE = re.compile(r"[A-Za-z0-9_\-]+(?:/[A-Za-z0-9_\-]+){1,15}\Z")


async def read_zone(read_bytes, client_object_address, *, layout_id):
    """Read a candidate path with at most eight reads/336 bytes in one second.

    ClientObject +304 -> ClientZone; zone +88 is an archived MSVC string.
    Length is read as an unsigned 64-bit size_t, capped before dereferencing.
    Strings under16 bytes are inline; longer strings use the first pointer.
    These offsets are source-derived hypotheses, not current-client facts.
    Cancellation propagates. Failures discard names and sanitize exceptions.
    """
    reader = _Reader(read_bytes, None)
    result = dict(
        status="unknown",
        layoutId=LAYOUT_ID if layout_id == LAYOUT_ID else None,
        liveQualified=False,
        movementAuthorized=False,
        zoneName=None,
    )

    async def collect():
        if layout_id != LAYOUT_ID:
            raise _Rejected("unreviewed-layout")
        owner = _address(client_object_address, 8)
        owner_type = await reader.get(owner, 8)
        _address(struct.unpack("<Q", owner_type)[0], 8)
        zone_raw = await reader.get(owner + 304, 8)
        zone = _address(struct.unpack("<Q", zone_raw)[0], 8)
        header = await reader.get(zone + 88, 24)
        length = struct.unpack_from("<Q", header, 16)[0]
        if not 1 <= length <= MAX_STRING_BYTES:
            raise _Rejected("invalid-zone-length")
        string_address = (
            _address(struct.unpack_from("<Q", header)[0]) if length >= 16 else zone + 88
        )
        raw = await reader.get(string_address, length)
        try:
            name = raw.decode("ascii")
        except UnicodeDecodeError:
            raise _Rejected("invalid-zone-path") from None
        if not _ZONE.fullmatch(name):
            raise _Rejected("invalid-zone-path")
        if raw != await reader.get(string_address, length):
            raise _Rejected("zone-string-changed")
        if header != await reader.get(zone + 88, 24):
            raise _Rejected("zone-header-changed")
        if zone_raw != await reader.get(owner + 304, 8):
            raise _Rejected("zone-binding-changed")
        if owner_type != await reader.get(owner, 8):
            raise _Rejected("client-object-changed")
        return name

    try:
        result["zoneName"] = await asyncio.wait_for(collect(), MAX_SECONDS)
        result["status"] = "candidate"
    except _Rejected as error:
        result["reason"] = str(error)
    except asyncio.TimeoutError:
        result["reason"] = "zone-window-timeout"
    except Exception:
        result["reason"] = "zone-observation-failed"
    result.update(readCalls=reader.calls, readBytes=reader.bytes)
    return result
