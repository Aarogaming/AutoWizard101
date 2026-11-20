"""Versioned, game-neutral offline session contract and replay reader.

Replay is deliberately observational: it validates and yields recorded events but
has no desktop capture, input, subprocess, or game-control implementation.
"""

from __future__ import annotations

from dataclasses import dataclass
import json
import math
import hashlib
import io
from pathlib import Path
from typing import Any, Iterator
from game_data import decode, read_bytes

SCHEMA_VERSION = 1
MAX_LINE_BYTES = 1_048_576


class SessionError(ValueError):
    """A session is malformed or uses an unsupported contract."""


@dataclass(frozen=True)
class SessionEvent:
    event_id: str
    kind: str
    sequence: int
    time_ms: float
    payload: dict[str, Any]


def _object(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise SessionError(f"{label} must be an object")
    return value


def read_session_snapshot(path: str | Path):
    """Read a session manifest and return a lazy, ordered JSONL event stream."""
    session_path = Path(path)
    try:
        manifest_raw = read_bytes(session_path)
        manifest = decode(manifest_raw)
    except (OSError, UnicodeError, ValueError) as exc:
        raise SessionError(f"Cannot read session manifest: {exc}") from exc
    manifest = _object(manifest, "manifest")
    if (
        type(manifest.get("schema_version")) is not int
        or manifest["schema_version"] != SCHEMA_VERSION
    ):
        raise SessionError("Unsupported session schema_version")
    if (
        not isinstance(manifest.get("session_id"), str)
        or not manifest["session_id"].strip()
    ):
        raise SessionError("manifest.session_id is required")
    if (
        not isinstance(manifest.get("clock_origin"), str)
        or not manifest["clock_origin"].strip()
    ):
        raise SessionError("manifest.clock_origin is required")
    filename = manifest.get("events_file")
    if (
        not isinstance(filename, str)
        or not filename
        or filename in {".", ".."}
        or any(c in filename for c in "/\\:")
        or Path(filename).name != filename
    ):
        raise SessionError("manifest.events_file must be a local filename")
    events_path = session_path.parent / filename
    if not events_path.resolve().is_relative_to(session_path.parent.resolve()):
        raise SessionError("Events file escapes session directory")
    try:
        events_raw = read_bytes(events_path, 16 * 1_048_576)
    except (OSError, ValueError) as exc:
        raise SessionError(str(exc)) from exc

    def events() -> Iterator[SessionEvent]:
        last_sequence = -1
        last_time = 0
        seen = set()
        try:
            with io.BytesIO(events_raw) as stream:
                for line_number, raw in enumerate(stream, 1):
                    if len(raw) > MAX_LINE_BYTES:
                        raise SessionError(
                            f"Event line {line_number} exceeds size limit"
                        )
                    if not raw.strip():
                        continue
                    try:
                        entry = _object(decode(raw), f"event line {line_number}")
                    except (UnicodeError, ValueError) as exc:
                        raise SessionError(
                            f"Malformed event line {line_number}: {exc}"
                        ) from exc
                    event_id, kind = entry.get("event_id"), entry.get("kind")
                    sequence, time_ms = entry.get("sequence"), entry.get("time_ms")
                    payload = entry.get("payload", {})
                    if not isinstance(event_id, str) or not event_id.strip():
                        raise SessionError(
                            f"Event line {line_number}: event_id required"
                        )
                    if event_id in seen or len(seen) >= 20000:
                        raise SessionError(
                            "Duplicate event ID or session exceeds 20000 events"
                        )
                    seen.add(event_id)
                    if not isinstance(kind, str) or kind not in {
                        "observation",
                        "action_proposal",
                        "action_receipt",
                        "outcome",
                        "marker",
                    }:
                        raise SessionError(f"Event line {line_number}: invalid kind")
                    if type(sequence) is not int or sequence <= last_sequence:
                        raise SessionError(
                            f"Event line {line_number}: sequence must increase"
                        )
                    if (
                        isinstance(time_ms, bool)
                        or not isinstance(time_ms, (int, float))
                        or not math.isfinite(time_ms)
                        or time_ms < last_time
                    ):
                        raise SessionError(
                            f"Event line {line_number}: time_ms must be finite and nondecreasing"
                        )
                    payload = _object(payload, f"event line {line_number} payload")
                    last_sequence, last_time = sequence, time_ms
                    yield SessionEvent(
                        event_id, kind, sequence, float(time_ms), payload
                    )
        except OSError as exc:
            raise SessionError(f"Cannot read events file: {exc}") from exc

    return (
        manifest,
        events(),
        {
            "sha256_manifest": hashlib.sha256(manifest_raw).hexdigest(),
            "sha256_events": hashlib.sha256(events_raw).hexdigest(),
        },
    )


def read_session(path: str | Path) -> tuple[dict[str, Any], Iterator[SessionEvent]]:
    manifest, events, _ = read_session_snapshot(path)
    return manifest, events


def replay(path: str | Path) -> Iterator[SessionEvent]:
    """Yield validated recorded events. This function cannot dispatch actions."""
    _, stream = read_session(path)
    yield from stream
