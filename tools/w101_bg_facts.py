"""Background W101 facts worker — OFFLINE CONTRACT ONLY (not live-qualified).

This module defines the bounded request/response schema and pure fact mapping for a
future resident reader. It does NOT attach to the client, read memory, capture,
send input, or promote any fact to Observed. A real reader path will be added only
after a separately authorized live pin-qualification pass (see
docs/coordination/tasks/W101-BG-WORKER-DESIGN-01.md).

Known fact IDs mirror the registered C# catalog subset; every published value here
is CANDIDATE until a reviewed producer is qualified natively.
"""

from __future__ import annotations

import json
import sys
from typing import Any

SCHEMA_VERSION = 1
MAX_FACTS = 64
MIN_INTERVAL_MS = 1
MAX_INTERVAL_MS = 5000
MAX_CYCLES = 7200

# Candidate-only IDs. A producer may never promote these to Observed from Python.
ALLOWED_FACT_IDS = frozenset(
    {
        "health",
        "mana",
        "energy",
        "gold.current",
        "wizard.level",
        "zone.candidate",
        "activity.diego.visible",
    }
)
REQUIRED_TARGET_KEYS = ("handle", "pid", "started_utc_ticks", "image_sha256")


def validate_request(request: dict[str, Any]) -> tuple[bool, str]:
    """Validate a bounded request. Foreground/focus is deliberately NOT checked."""
    if type(request) is not dict:
        return False, "invalid-request"
    if request.get("schemaVersion") != SCHEMA_VERSION:
        return False, "unsupported-schema"
    target = request.get("target")
    if type(target) is not dict or any(k not in target for k in REQUIRED_TARGET_KEYS):
        return False, "exact-target-required"
    if any(
        type(target[k]) is not int or target[k] <= 0 for k in REQUIRED_TARGET_KEYS[:3]
    ):
        return False, "invalid-target-identity"
    image = target.get("image_sha256")
    if (
        type(image) is not str
        or len(image) != 64
        or any(ch not in "0123456789abcdefABCDEF" for ch in image)
    ):
        return False, "invalid-image-pin"
    interval = request.get("intervalMs", 500)
    cycles = request.get("maximumCycles", 1)
    if type(interval) is not int or not MIN_INTERVAL_MS <= interval <= MAX_INTERVAL_MS:
        return False, "invalid-interval"
    if type(cycles) is not int or not 1 <= cycles <= MAX_CYCLES:
        return False, "invalid-cycle-budget"
    return True, "admitted"


def map_observations(
    observations: list[dict[str, Any]], observed_utc_ticks: int, layout_id: str
) -> list[dict[str, Any]]:
    """Map raw observations to candidate fact records with explicit provenance.

    Unknown IDs and non-finite/negative timestamps are rejected. This function never
    reads memory and never assigns qualification higher than 'candidate'.
    """
    if type(observations) is not list or len(observations) > MAX_FACTS:
        return []
    if type(observed_utc_ticks) is not int or observed_utc_ticks <= 0:
        return []
    if type(layout_id) is not str or not layout_id:
        return []
    facts: list[dict[str, Any]] = []
    for item in observations:
        if type(item) is not dict:
            continue
        fact_id = item.get("id")
        value = item.get("value")
        if fact_id not in ALLOWED_FACT_IDS or type(value) is not str or not value:
            continue
        facts.append(
            {
                "id": fact_id,
                "value": value,
                "producer": item.get("producer", "w101.background-unqualified"),
                "observedUtcTicks": observed_utc_ticks,
                "layoutId": layout_id,
                "qualification": "candidate",
                "note": "candidate-only; not live-qualified",
            }
        )
    return facts[:MAX_FACTS]


def self_test() -> dict[str, Any]:
    """Deterministic schema probe. No client contact, no facts, no input."""
    return {
        "schemaVersion": SCHEMA_VERSION,
        "status": "self-test",
        "code": "offline-contract-only",
        "facts": [],
        "readerQualified": False,
        "note": "Schema self-test only. No live reads; pin promotion requires an authorized qualification pass.",
    }


def run(request: dict[str, Any]) -> dict[str, Any]:
    """Inert run path until a reviewed reader is installed by qualification."""
    ok, reason = validate_request(request)
    if not ok:
        return {
            "schemaVersion": SCHEMA_VERSION,
            "status": "rejected",
            "code": reason,
            "facts": [],
        }
    return {
        "schemaVersion": SCHEMA_VERSION,
        "status": "observed",
        "code": "reader-not-qualified",
        "facts": [],
        "readerQualified": False,
        "note": "Target admitted but no reviewed memory reader is installed; no facts published.",
    }


def main() -> int:
    raw = sys.stdin.read(65536)
    try:
        request = json.loads(raw)
    except json.JSONDecodeError:
        print(
            json.dumps(
                {
                    "schemaVersion": SCHEMA_VERSION,
                    "status": "rejected",
                    "code": "invalid-json",
                }
            )
        )
        return 2
    result = self_test() if request == {"selfTest": True} else run(request)
    print(json.dumps(result, allow_nan=False))
    return 0 if result.get("status") in ("self-test", "observed") else 2


if __name__ == "__main__":
    raise SystemExit(main())
