"""Bounded qualification PLAN for the background facts worker (offline only).

This tool does NOT attach to Wizard101, launch a client, dispatch input or promote a
pin. It produces the deterministic sequence that will be executed only in a future,
explicitly authorized live window, and refuses live mode until a reviewed pin and
owner token exist. See docs/coordination/tasks/W101-BG-WORKER-DESIGN-01.md.
"""

from __future__ import annotations

import json
import sys
from typing import Any

SCHEMA_VERSION = 1
MIN_CYCLES = 3
MAX_CYCLES = 5
LIVE_TOKEN = "OWNER-LIVE-BG-WORKER-QUALIFICATION"


def plan(
    cycles: int = 4,
    image_pin: str = "d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a",
) -> dict[str, Any]:
    """Deterministic, bounded qualification sequence with explicit gates."""
    if not MIN_CYCLES <= cycles <= MAX_CYCLES:
        raise ValueError(f"cycles must be {MIN_CYCLES}..{MAX_CYCLES}")
    if not (type(image_pin) is str and len(image_pin) == 64):
        raise ValueError("image pin must be a 64-character SHA-256")
    return {
        "schemaVersion": SCHEMA_VERSION,
        "kind": "maelstrom-bg-worker-qualification-plan-v1",
        "imagePin": image_pin,
        "maxCycles": cycles,
        "steps": [
            {
                "step": 1,
                "name": "worker-self-test",
                "mode": "offline",
                "gate": "self-test status + schema",
            },
            {
                "step": 2,
                "name": "pin-worker-sha256",
                "mode": "offline",
                "gate": "hash embedded in C# const",
            },
            {
                "step": 3,
                "name": "exact-target-attach",
                "mode": "live",
                "gate": "HWND/PID/start-ticks/image hash match",
            },
            {
                "step": 4,
                "name": "bounded-cycles",
                "mode": "live",
                "gate": "3-5 cycles, no input, per-read timeout",
            },
            {
                "step": 5,
                "name": "unfocused-cycle",
                "mode": "live",
                "gate": "one cycle with W101 not foreground; still publishes candidate facts",
            },
            {
                "step": 6,
                "name": "cleanup-verification",
                "mode": "live",
                "gate": "hook restored, disconnect confirmed, no recovery hold",
            },
            {
                "step": 7,
                "name": "receipt-record",
                "mode": "offline",
                "gate": "report + hashes + evidence stored",
            },
        ],
        "note": "PLAN ONLY. Live steps require separate owner authorization and a reviewed pin; no pin is promoted by this tool.",
    }


def refuse_live(token: str | None, reviewed_pin: bool) -> str:
    """Return the reason live execution is refused, or '' if all gates exist."""
    if token != LIVE_TOKEN:
        return "missing-owner-token"
    if not reviewed_pin:
        return "no-reviewed-pin"
    return ""


def main() -> int:
    args = sys.argv[1:]
    live = "--live" in args
    args = [a for a in args if a != "--live"]
    cycles = 4
    if "--cycles" in args:
        idx = args.index("--cycles")
        if idx + 1 >= len(args):
            print(
                "usage: qualify_bg_worker.py [--cycles 3..5] [--live --token <token> --pin-reviewed]",
                file=sys.stderr,
            )
            return 2
        try:
            cycles = int(args[idx + 1])
        except ValueError:
            return 2
    token = (
        args[args.index("--token") + 1]
        if "--token" in args and args.index("--token") + 1 < len(args)
        else None
    )
    reviewed = "--pin-reviewed" in args
    if live:
        reason = refuse_live(token, reviewed)
        print(
            json.dumps(
                {
                    "status": "refused",
                    "reason": reason or "live-execution-not-implemented",
                }
            )
        )
        return 2
    print(json.dumps(plan(cycles), indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
