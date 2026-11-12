"""Thin local bridge to the native deck owner. Never simulates game outcomes."""

from __future__ import annotations

import json
import os
from pathlib import Path
import subprocess
import tempfile
from typing import Any


def call_deck(
    command: str, arguments: dict[str, Any] | None = None, request_id: str | None = None
) -> dict[str, Any]:
    """Use an explicitly configured executable; do not auto-start a controller.

    MAELSTROM_EXE selects the installed/current native executable.
    MAELSTROM_DATA_ROOT must match the running owner's data directory.
    Requests time out and are never retried automatically after ambiguous outcomes.
    """
    exe = os.environ.get("MAELSTROM_EXE")
    root = os.environ.get("MAELSTROM_DATA_ROOT")
    if not exe or not root:
        return {
            "ok": False,
            "error": "Configure MAELSTROM_EXE and MAELSTROM_DATA_ROOT, then open Maelstrom's Card Deck or start --deck-host. No game state is available.",
        }
    executable = Path(exe)
    if (
        not executable.is_absolute()
        or not executable.is_file()
        or not Path(root).is_absolute()
    ):
        return {
            "ok": False,
            "error": "Use existing absolute Maelstrom executable and data-root paths.",
        }
    try:
        if arguments is not None and not isinstance(arguments, dict):
            raise ValueError("Arguments must be an object")
        request = json.dumps(
            {"command": command, "arguments": arguments or {}, "requestId": request_id},
            allow_nan=False,
        )
    except (ValueError, TypeError):
        return {"ok": False, "error": "Deck arguments must contain finite JSON values."}
    if len(request.encode("utf-8")) > 262144:
        return {"ok": False, "error": "Deck request exceeds its size budget."}
    try:
        with tempfile.TemporaryDirectory(prefix="maelstrom-deck-client-") as folder:
            incoming = Path(folder) / "request.json"
            outgoing = Path(folder) / "response.json"
            incoming.write_text(request, encoding="utf-8")
            result = subprocess.run(
                [str(executable), "--deck-api", root, str(incoming), str(outgoing)],
                shell=False,
                stdin=subprocess.DEVNULL,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                timeout=12,
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
            if outgoing.is_file() and 0 < outgoing.stat().st_size <= 262144:
                payload = json.loads(outgoing.read_text(encoding="utf-8-sig"))
                if isinstance(payload, dict) and isinstance(payload.get("ok"), bool):
                    return payload
            return {
                "ok": False,
                "error": f"Native deck bridge returned no valid result (exit {result.returncode}). No completion is inferred.",
            }
    except (OSError, subprocess.TimeoutExpired, ValueError) as error:
        return {
            "ok": False,
            "error": f"Deck owner unavailable or response unconfirmed: {type(error).__name__}. Inspect state before retrying with the same request ID.",
        }
