"""Bounded visual/input bridge for Maelstrom's private WSL Dodge test display.

This runs only inside the test namespace; it never reads the Windows desktop.
"""

import json
import os
from pathlib import Path
import struct
import subprocess
import sys
import time

KEYS = {
    "LEFT": ("Left",),
    "RIGHT": ("Right",),
    "UP": ("Up",),
    "DOWN": ("Down",),
    "A": ("a",),
    "D": ("d",),
    "W": ("w",),
    "S": ("s",),
    "UP_LEFT": ("Up", "Left"),
    "UP_RIGHT": ("Up", "Right"),
    "DOWN_LEFT": ("Down", "Left"),
    "DOWN_RIGHT": ("Down", "Right"),
}


def replace_with_retry(source, destination):
    """Atomically publish a frame or score despite short Windows sharing locks."""
    for attempt in range(25):
        try:
            os.replace(source, destination)
            return
        except PermissionError:
            if attempt == 24:
                raise
            time.sleep(0.01)


def frames_for_simulated_ms(duration_ms, speed):
    if speed not in (0.25, 0.5, 1.0) or not 0 < duration_ms <= 2000:
        raise ValueError("Invalid training duration or speed")
    return max(1, min(240, round(duration_ms * 30 / (1000 * speed))))


def xwd_to_bmp(data):
    """Convert a 480x720 little-endian Xvfb framebuffer into a top-down BMP."""
    if len(data) < 100 or len(data) > 2_000_000:
        raise ValueError("Unexpected framebuffer size")
    h = struct.unpack(">25I", data[:100])
    if (h[1], h[2], h[3], h[4], h[5], h[7], h[11], h[12], h[14], h[15], h[16]) != (
        7,
        2,
        24,
        480,
        720,
        0,
        32,
        1920,
        0xFF0000,
        0x00FF00,
        0x0000FF,
    ):
        raise ValueError("Unsupported Xvfb framebuffer format")
    start = h[0] + h[19] * 12
    size = 480 * 720 * 4
    pixels = data[start : start + size]
    if len(pixels) != size:
        raise ValueError("Truncated framebuffer")
    return (
        b"BM"
        + struct.pack("<IHHI", 54 + size, 0, 0, 54)
        + struct.pack("<IiiHHIIiiII", 40, 480, -720, 1, 32, 0, size, 2835, 2835, 0, 0)
        + pixels
    )


def main():
    channel, framebuffer, window, xdotool = map(str, sys.argv[1:5])
    mode = sys.argv[5] if len(sys.argv) > 5 else "realtime"
    speed = float(sys.argv[6]) if len(sys.argv) > 6 else 1.0
    if (
        mode not in ("realtime", "stepped")
        or speed not in (0.25, 0.5, 1.0)
        or (mode == "realtime" and speed != 1)
    ):
        raise ValueError("Invalid private-display training mode")
    channel_path = Path(channel)
    step_path = Path(sys.argv[7]) if len(sys.argv) > 7 else channel_path
    expected = int(window)
    step_id = 0
    ticks = 0
    first_observation = True

    def advance(frames):
        nonlocal step_id, ticks
        request = step_path / f"step-request-{step_id:04d}.json"
        temporary = step_path / f"step-request-{step_id:04d}.tmp"
        ack = step_path / f"step-ack-{step_id:04d}.json"
        temporary.write_text(
            json.dumps({"id": step_id, "frames": frames}), encoding="utf-8"
        )
        os.replace(temporary, request)
        # The private software renderer may yield heavily to other work. Wait
        # for this exact acknowledgement; never resend a possibly applied step.
        deadline = time.monotonic() + 60
        while not ack.exists():
            if time.monotonic() >= deadline:
                raise TimeoutError("Controlled game step timed out")
            time.sleep(0.02)
        result = json.loads(ack.read_text(encoding="utf-8"))
        if (
            result.get("id") != step_id
            or type(result.get("ticks")) is not int
            or result["ticks"] != ticks + frames
        ):
            raise ValueError("Invalid controlled-step acknowledgement")
        if os.environ.get("MAELSTROM_DODGE_EVAL") == "1":
            evaluation = (step_path / "evaluation.json").read_bytes()
            if len(evaluation) > 1024:
                raise ValueError("Invalid private evaluation size")
            evaluation_tmp = channel_path / "evaluation.tmp"
            evaluation_tmp.write_bytes(evaluation)
            replace_with_retry(evaluation_tmp, channel_path / "evaluation.json")
        ticks = result["ticks"]
        step_id += 1
        request.unlink()
        ack.unlink()
        time.sleep(
            0.05
        )  # Wait for the private framebuffer to receive the rendered step.

    def focused():
        result = subprocess.run(
            [xdotool, "getwindowfocus"],
            capture_output=True,
            text=True,
            timeout=1,
            check=True,
        )
        return int(result.stdout.strip()) == expected

    def snapshot():
        raw = Path(framebuffer).read_bytes()
        captured_ms = (
            ticks * 1000 / 30 * speed
            if mode == "stepped"
            else time.monotonic_ns() / 1_000_000
        )
        data = xwd_to_bmp(raw)
        target = channel_path / "frame.bmp"
        temporary = channel_path / "frame.tmp"
        temporary.write_bytes(data)
        replace_with_retry(temporary, target)
        return target.name, captured_ms

    print(
        json.dumps(
            {"kind": "ready", "display": "private-x11", "mode": mode, "speed": speed}
        ),
        flush=True,
    )
    for line in sys.stdin:
        try:
            if len(line) > 2048:
                raise ValueError("Request too large")
            request = json.loads(line)
            seq = request["id"]
            if type(seq) is not int or not 0 <= seq <= 10000:
                raise ValueError("Invalid sequence")
            kind = request["kind"]
            if kind == "stop":
                break
            if not focused():
                raise RuntimeError("Private game window lost focus")
            if kind == "action":
                key, duration = request["key"], request["duration_ms"]
                if (
                    key not in KEYS
                    or type(duration) is not int
                    or not 1 <= duration <= 200
                ):
                    raise ValueError("Invalid bounded action")
                held = []
                try:
                    for individual_key in KEYS[key]:
                        held.append(individual_key)
                        subprocess.run(
                            [xdotool, "keydown", individual_key], check=True, timeout=1
                        )
                    if mode == "stepped":
                        time.sleep(
                            0.05
                        )  # Let the private window receive keydown while paused.
                        advance(frames_for_simulated_ms(duration, speed))
                    else:
                        time.sleep(duration / 1000)
                finally:
                    release_failure = None
                    for individual_key in reversed(held):
                        try:
                            subprocess.run(
                                [xdotool, "keyup", individual_key],
                                check=True,
                                timeout=1,
                            )
                        except subprocess.SubprocessError as exc:
                            release_failure = exc
                    if release_failure is not None:
                        raise RuntimeError(
                            "Could not release every private key"
                        ) from release_failure
                if not focused():
                    raise RuntimeError("Private game window lost focus during input")
                if mode == "realtime":
                    time.sleep(0.08)
            elif kind != "observe":
                raise ValueError("Unsupported request")
            elif mode == "stepped":
                duration = 1200 if first_observation else 1000 / 15
                advance(frames_for_simulated_ms(duration, speed))
                first_observation = False
            name, captured_ms = snapshot()
            print(
                json.dumps(
                    {
                        "id": seq,
                        "kind": "frame",
                        "frame": name,
                        "capture_ms": captured_ms,
                    }
                ),
                flush=True,
            )
        except (
            ValueError,
            KeyError,
            TypeError,
            OSError,
            RuntimeError,
            subprocess.SubprocessError,
        ) as exc:
            print(json.dumps({"kind": "error", "message": str(exc)}), flush=True)
            return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
