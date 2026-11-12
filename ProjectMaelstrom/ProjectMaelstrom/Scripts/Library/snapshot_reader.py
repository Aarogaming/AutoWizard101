import json
import os
from dataclasses import dataclass
from datetime import datetime, timezone
import math
from typing import Optional


@dataclass
class MetricPair:
    current: Optional[int]
    max: Optional[int]
    confidence: float


@dataclass
class MetricSingle:
    value: Optional[int]
    confidence: float


@dataclass
class Snapshot:
    health: Optional[MetricPair]
    mana: Optional[MetricPair]
    gold: Optional[MetricSingle]
    warnings: list
    raw: dict
    energy: Optional[MetricPair] = None
    potions: Optional[MetricSingle] = None
    captured_utc: Optional[datetime] = None
    window_present: bool = False
    has_focus: bool = False
    resolution: Optional[str] = None


def load_snapshot(path: str = None, max_age_seconds: Optional[float] = None) -> Snapshot:
    """Read the shared snapshot; optionally reject missing, future or stale timestamps.

    Explicit path takes precedence over MAELSTROM_SNAPSHOT_PATH, then the library cache.
    """
    snapshot_path = path or os.environ.get("MAELSTROM_SNAPSHOT_PATH") or os.path.join(
        os.path.dirname(__file__), "..", ".cache", "snapshot.json"
    )
    with open(snapshot_path, "r", encoding="utf-8-sig") as f:
        data = json.load(f)

    captured = None
    if data.get("CapturedUtc"):
        captured = datetime.fromisoformat(data["CapturedUtc"].replace("Z", "+00:00"))
        if captured.tzinfo is None:
            raise ValueError("CapturedUtc must include a timezone")
    if max_age_seconds is not None:
        if not math.isfinite(max_age_seconds) or max_age_seconds < 0:
            raise ValueError("max_age_seconds must be finite and nonnegative")
        if captured is None:
            raise ValueError("Snapshot timestamp missing")
        age = (datetime.now(timezone.utc) - captured).total_seconds()
        if age < -5 or age > max_age_seconds:
            raise ValueError("Snapshot timestamp is future-dated or stale")

    def to_pair(key):
        item = data.get(key) or {}
        return MetricPair(
            current=item.get("Current"),
            max=item.get("Max"),
            confidence=item.get("Confidence", 0.0),
        ) if item else None

    def to_single(key):
        item = data.get(key) or {}
        return MetricSingle(
            value=item.get("Value"),
            confidence=item.get("Confidence", 0.0),
        ) if item else None

    return Snapshot(
        health=to_pair("Health"),
        mana=to_pair("Mana"),
        gold=to_single("Gold"),
        warnings=data.get("Warnings") or [],
        raw=data,
        energy=to_pair("Energy"),
        potions=to_single("Potions"),
        captured_utc=captured,
        window_present=data.get("WindowPresent") is True,
        has_focus=data.get("HasFocus") is True,
        resolution=data.get("Resolution"),
    )


if __name__ == "__main__":
    snap = load_snapshot()
    print("Health:", snap.health)
    print("Mana:", snap.mana)
    print("Gold:", snap.gold)
    print("Warnings:", snap.warnings)
