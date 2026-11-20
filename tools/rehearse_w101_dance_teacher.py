"""Rehearse pinned legacy DanceBot decisions on synthetic images, with no desktop I/O.

Uses the existing isolated dance environment. Does not import the bot's entry
point or its desktop modules. Reviewed functions run with image fixtures, a
virtual clock and a key-proposal recorder. This is compatibility testing, not a
security sandbox, live gameplay, or an accuracy claim on current W101 frames.
"""

from __future__ import annotations

import argparse
import ast
from enum import Enum
from functools import lru_cache
import hashlib
import json
from pathlib import Path
import random
import time
from types import SimpleNamespace
from typing import List

from game_packs import validate_pack

PINS = {
    "dance_game.py": "249e70e7492e7e52dc47d6e8054216f4c1cc851738eea924a41bffd96bb04487",
    "image.py": "d33b40baccb4ca1b17973eb8da34fae3f5d40e7a454ffd3f7d29ec3c450b8a97",
}
Arrow = Enum("Arrow", {"Up": 0, "Down": 1, "Left": 2, "Right": 3})


@lru_cache(maxsize=1)
def imaging():
    # Keep cataloguing, cue-gate tests and ordinary pack tools stdlib-only.
    # The actual rehearsal explicitly uses the existing isolated dance env.
    import cv2
    import numpy as np
    from PIL import Image

    return cv2, np, Image


class CueGate:
    """Accept one cue per presentation; require two neutral samples to rearm."""

    def __init__(self):
        self.armed = True
        self.neutral = 0

    def accept(self, candidates: list[str]) -> bool:
        if not candidates:
            self.neutral += 1
            if self.neutral >= 2:
                self.armed = True
            return False
        self.neutral = 0
        if len(candidates) != 1 or not self.armed:
            return False
        self.armed = False
        return True


def definitions(source: Path, filename: str, names: set[str], namespace: dict):
    raw = (source / filename).read_bytes()
    if hashlib.sha256(raw).hexdigest() != PINS[filename]:
        raise ValueError("Legacy source changed; review before using: " + filename)
    tree = ast.parse(raw, filename=filename)
    functions = [
        node
        for node in tree.body
        if isinstance(node, ast.FunctionDef) and node.name in names
    ]
    if {node.name for node in functions} != names:
        raise ValueError("Reviewed functions are missing")
    # Hash pinning and an explicit function list prevent accidental startup of
    # the upstream application's screen, focus, key-listener or feeding loops.
    exec(
        compile(ast.Module(body=functions, type_ignores=[]), filename, "exec"),
        namespace,
    )


def locate(template, frame, confidence=0.95):
    cv2, np, _ = imaging()
    small = cv2.cvtColor(np.array(template.convert("RGB")), cv2.COLOR_RGB2GRAY)
    large = cv2.cvtColor(np.array(frame.convert("RGB")), cv2.COLOR_RGB2GRAY)
    if small.shape[0] > large.shape[0] or small.shape[1] > large.shape[1]:
        return None
    scores = cv2.matchTemplate(large, small, cv2.TM_CCOEFF_NORMED)
    _, maximum, _, position = cv2.minMaxLoc(scores)
    return (
        (*position, small.shape[1], small.shape[0]) if maximum >= confidence else None
    )


def environment(source: Path, resolution: str):
    _, _, Image = imaging()
    actions = []
    state = {"time_ms": 0.0, "frame": None}

    def sleep(seconds):
        state["time_ms"] += seconds * 1000

    def open_asset(relative):
        path = (source / relative).resolve()
        if not path.is_relative_to((source / "assets").resolve()):
            raise ValueError("Only local arrow assets are readable")
        return Image.open(path)

    namespace = {
        "Arrow": Arrow,
        "List": List,
        "Image": SimpleNamespace(open=open_asset, Image=Image.Image),
        "arrow_icons": [],
        "arrow_subicons": [],
        "locate": locate,
        "time": SimpleNamespace(sleep=sleep),
        "logging": SimpleNamespace(debug=lambda *args, **kwargs: None),
        "Globals": SimpleNamespace(game_finished=False),
        "pyautogui": SimpleNamespace(
            press=lambda key: actions.append(
                {
                    "key": key.upper(),
                    "virtual_time_ms": state["time_ms"],
                    "kind": "teacher_proposal",
                    "dispatched": False,
                }
            )
        ),
        "get_screenshot": lambda: state["frame"],
        "moves": [],
        "turn": 0,
        "ARROW_TO_KEY": {arrow: arrow.name.lower() for arrow in Arrow},
    }
    definitions(
        source,
        "image.py",
        {
            "load_textures",
            "generate_icon_subregions",
            "generate_subicons",
            "remove_duplicate_subicons",
        },
        namespace,
    )
    definitions(source, "dance_game.py", {"update_search", "input_moves"}, namespace)
    namespace["load_textures"](resolution)
    namespace["generate_subicons"]()
    namespace["remove_duplicate_subicons"]()
    return namespace, state, actions


def rehearse(
    source: Path, rounds: int = 20, seed: int = 101, deduplicate: bool = False
):
    if not 1 <= rounds <= 100:
        raise ValueError("Use 1–100 synthetic rounds per variant")
    cv2, _, Image = imaging()
    cv2.setNumThreads(1)
    rng = random.Random(seed)
    started = time.perf_counter()
    results = []
    asset_hashes = {}
    for resolution in ("800x600", "1280x800"):
        for path in sorted((source / "assets" / resolution).glob("*.png")):
            asset_hashes[path.relative_to(source).as_posix()] = hashlib.sha256(
                path.read_bytes()
            ).hexdigest()
        for held_frames in (1, 2):
            for trial in range(rounds):
                namespace, state, actions = environment(source, resolution)
                cue_gate = CueGate()

                def tick():
                    if deduplicate:
                        candidates = [
                            arrow.name
                            for arrow in Arrow
                            if any(
                                locate(icon, state["frame"], 0.9) is not None
                                for icon in namespace["arrow_subicons"][arrow.value]
                            )
                        ]
                        if not cue_gate.accept(candidates):
                            return
                    namespace["update_search"]()

                length = 3 + trial % 5
                sequence = [rng.choice(list(Arrow)) for _ in range(length)]
                namespace["turn"] = length - 3
                for arrow in sequence:
                    icon = namespace["arrow_icons"][arrow.value].convert("RGBA")
                    frame = Image.new("RGB", (95, 95), (22, 28, 40))
                    frame.paste(
                        icon, ((95 - icon.width) // 2, (95 - icon.height) // 2), icon
                    )
                    state["frame"] = frame
                    for _ in range(held_frames):
                        tick()
                    # A neutral gap is explicit; repeat arrows are distinct events.
                    state["frame"] = Image.new("RGB", (95, 95), (22, 28, 40))
                    tick()
                    tick()
                expected = [arrow.name.upper() for arrow in sequence]
                actual = [action["key"] for action in actions]
                results.append(
                    {
                        "resolution": resolution,
                        "held_frames": held_frames,
                        "trial": trial,
                        "expected": expected,
                        "proposals": actions,
                        "exact_sequence": actual == expected,
                        "unconsumed_moves": [a.name for a in namespace["moves"]],
                    }
                )
                for icon in namespace["arrow_icons"]:
                    icon.close()
                for icons in namespace["arrow_subicons"]:
                    for icon in icons:
                        icon.close()
    return {
        "schema_version": 1,
        "mode": "offline_teacher_rehearsal",
        "source_sha256": PINS,
        "asset_sha256": asset_hashes,
        "seed": seed,
        "trials": results,
        "exact_sequences": sum(row["exact_sequence"] for row in results),
        "total_sequences": len(results),
        "elapsed_seconds": time.perf_counter() - started,
        "desktop_inputs": 0,
        "live_frames": 0,
        "model_training": False,
        "cue_adapter": "two-neutral-sample-rearm" if deduplicate else "none",
        "matching_adapter": "OpenCV grayscale TM_CCOEFF_NORMED, missing matches return None",
        "qualification": "Synthetic arrow fixtures only. Repeated-frame robustness is tested; no current-game success or privileged labels are assumed.",
    }


def export_bootstrap(source: Path, destination: Path, report: dict):
    """Export local reference assets and action bindings, without claiming live skills."""
    if destination.exists():
        raise FileExistsError(
            "Choose a new bootstrap directory; existing packs are preserved"
        )
    _, _, Image = imaging()
    assets = []
    prepared = []
    for relative, expected in report["asset_sha256"].items():
        path = (source / relative).resolve()
        if (
            not path.is_relative_to((source / "assets").resolve())
            or path.stat().st_size > 1_048_576
        ):
            raise ValueError("Invalid or oversized reference asset")
        raw = path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != expected:
            raise ValueError("Reference asset changed after rehearsal")
        with Image.open(path) as image:
            if image.width > 256 or image.height > 256:
                raise ValueError("Reference arrow is larger than expected")
        prepared.append((relative, raw))
        assets.append(
            {
                "file": relative,
                "label": path.stem.lower(),
                "sha256": expected,
                "source_resolution": path.parent.name,
                "label_source": "legacy_asset_filename",
            }
        )
    pack = validate_pack(
        {
            "schema_version": 2,
            "pack_id": "wizard101-pet-dance-teacher",
            "game_id": "wizard101",
            "title": "Pet Dance · legacy teacher bootstrap",
            "version": "0.1.0",
            "status": "draft",
            "actions": [
                {
                    "id": "dance-" + a.name.lower(),
                    "title": "Dance " + a.name.lower(),
                    "key": a.name.upper(),
                    "duration_ms": 100,
                }
                for a in Arrow
            ],
            "capabilities": [],
            "skills": [],
            "fixtures": [],
            "dependencies": [],
            "teacher_bootstrap": {
                "teacher": "Wizard101_DanceBot",
                "category": "Pets",
                "source_sha256": PINS,
                "candidate_assets": assets,
                "report": "teacher-rehearsal.json",
                "live_qualified": False,
                "phase_gate_required": "Watch sequence, then verify Go before replaying keys",
                "license_status": "No license file found in the supplied DanceBot source; local research copy, not bundled for redistribution",
                "note": "Asset labels are not human-reviewed current-game labels. Existing Observe requires reviewed teaching examples; these assets alone do not provide them.",
            },
        }
    )
    destination.mkdir(parents=True)
    for relative, raw in prepared:
        path = destination / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
    (destination / "pack.json").write_text(
        json.dumps(pack, indent=2) + "\n", encoding="utf-8"
    )
    (destination / "teacher-rehearsal.json").write_text(
        json.dumps(report, indent=2) + "\n", encoding="utf-8"
    )
    return pack


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--rounds", type=int, default=20)
    parser.add_argument(
        "--deduplicate",
        action="store_true",
        help="Gate duplicate visual cues before the legacy decision function",
    )
    parser.add_argument(
        "--bootstrap-pack",
        type=Path,
        help="New local-only draft pack folder for the reviewed source assets and bindings",
    )
    args = parser.parse_args()
    report = rehearse(args.source.resolve(), args.rounds, deduplicate=args.deduplicate)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(report, stream, indent=2)
        stream.write("\n")
    if args.bootstrap_pack:
        export_bootstrap(args.source.resolve(), args.bootstrap_pack, report)
    print(
        json.dumps(
            {
                key: report[key]
                for key in (
                    "exact_sequences",
                    "total_sequences",
                    "elapsed_seconds",
                    "desktop_inputs",
                )
            }
        )
    )


if __name__ == "__main__":
    main()
