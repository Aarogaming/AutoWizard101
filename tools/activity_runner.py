"""Validate or run a guided activity in its isolated backend environment."""

import argparse
import asyncio
import json
import os
from pathlib import Path
import runpy
import sys

from activity_runtime import (
    ACTIVITIES,
    ActivityRun,
    ActivityStopped,
    combat_config,
    load_profile,
    prepare_deimos_config,
    run_dance,
)
from w101_interaction_policy import require_tool_execution


def dance_window(backend, resolution):
    windows = [
        window
        for window in backend.pgw.getWindowsWithTitle("Wizard101")
        if window.title == "Wizard101"
    ]
    if len(windows) != 1:
        raise ValueError(
            "Open exactly one Wizard101 game window before checking pet dance."
        )
    width, height = windows[0].size
    expected_width, expected_height = map(int, resolution.split("x"))
    if (
        abs(width - 10 - expected_width) >= 15
        or abs(height - 30 - expected_height) >= 15
    ):
        raise ValueError(f"Set the game to windowed {resolution}, then check again.")
    return windows[0]


async def probe_deimos(profile):
    # Also guard direct calls: a connection check must never activate client hooks.
    require_tool_execution("deimos")


def validate_backend(profile, source):
    if profile["activity"] == "pet_dance":
        for arrow in ("Up", "Down", "Left", "Right"):
            path = source / "assets" / profile["resolution"] / (arrow + ".png")
            if not path.is_file():
                raise ValueError(
                    "The selected resolution's arrow templates are missing."
                )
        return None
    if "maelstrom_run" not in (source / "Deimos.py").read_text(encoding="utf-8"):
        raise ValueError(
            "This Deimos source has no guided activity adapter. Use the bundled version."
        )
    # Offline setup checks inspect source/configuration only; do not import the backend.
    combat_config(profile)
    return None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for argument in ("profile", "source"):
        parser.add_argument("--" + argument, required=True)
    for argument in ("events", "control", "policy"):
        parser.add_argument("--" + argument)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--probe", action="store_true")
    args = parser.parse_args()
    profile = load_profile(args.profile)
    if args.probe or not args.check:
        require_tool_execution(ACTIVITIES[profile["activity"]])
    source = Path(args.source).resolve(strict=True)
    os.chdir(source)
    sys.path.insert(0, str(source))
    sys.argv = [
        str(source / ("main.py" if profile["activity"] == "pet_dance" else "Deimos.py"))
    ]
    backend = validate_backend(profile, source)
    if args.probe:
        if profile["activity"] == "pet_dance":
            dance_window(backend, profile["resolution"])
            report = {"connected": True, "resolution": profile["resolution"]}
        else:
            report = asyncio.run(probe_deimos(profile))
        print(json.dumps(report), flush=True)
        return 0
    if args.check:
        print(
            json.dumps(
                {
                    "valid": True,
                    "tool": ACTIVITIES[profile["activity"]],
                    "activity": profile["activity"],
                }
            )
        )
        return 0
    if not all((args.events, args.control, args.policy)):
        raise ValueError(
            "The run is missing its progress, stop control, or policy path."
        )
    from wizard101_hub import validate_policy

    validate_policy(args.policy)
    run = ActivityRun(profile, args.events, args.control, policy=args.policy)
    try:
        if profile["activity"] == "pet_dance":
            dance_window(backend, profile["resolution"])
            # Template loading happens again only in a fresh import state below.
            backend.DG.arrow_icons.clear()
            backend.DG.arrow_subicons.clear()

            def focused():
                window = backend.pgw.getActiveWindow()
                return window is not None and window.title == "Wizard101"

            from pynput import keyboard

            def on_key(key):
                if getattr(key, "char", None) == "q":
                    run.control.touch()

            with keyboard.Listener(on_press=on_key):
                run_dance(run, backend, focused)
        else:
            from wizwalker.utils import get_all_wizard_handles

            if len(get_all_wizard_handles()) != 1:
                raise ValueError(
                    "Open exactly one Wizard101 client for a guided activity."
                )
            config = Path(args.events).with_name("deimos-run.ini")
            prepare_deimos_config(profile, source, config)
            os.environ["MAELSTROM_DEIMOS_CONFIG"] = str(config)
            runpy.run_path(
                str(source / "Deimos.py"),
                run_name="__main__",
                init_globals={"maelstrom_run": run},
            )
        return 0
    except ActivityStopped as exc:
        run.emit("stopped", str(exc))
        return 0
    except Exception as exc:
        run.emit("failed", str(exc))
        raise


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:
        print(f"Activity could not start: {error}", file=sys.stderr, flush=True)
        sys.exit(1)
