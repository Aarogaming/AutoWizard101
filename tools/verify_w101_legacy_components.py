"""Bounded offline probes of reviewed legacy components; never launch a bot.

Runs only selected definitions with injected image/input/clock doubles, or the
reviewed, standard-library-only Deimos compiler. This is not a generic sandbox.
Failures are evidence about upstream behavior, not silently accepted regressions.
"""

from __future__ import annotations

import argparse
import ast
import base64
from collections import namedtuple
from contextlib import redirect_stdout
from dataclasses import dataclass
from enum import Enum
import hashlib
import importlib
import importlib.util
import io
import json
import math
from mmap import ACCESS_READ, mmap
from pathlib import Path
import struct
import sys
import tempfile
import time
from types import SimpleNamespace
from typing import List, Optional, Tuple, Union
import zlib

ROOT = Path(__file__).resolve().parents[1]
PINS = ROOT / "research/w101-legacy-reviewed-files.json"


def checked_source(root: Path, relative: str) -> Path:
    pins = json.loads(PINS.read_text(encoding="utf-8"))
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError("Source outside the reviewed collection")
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if pins.get(relative) != digest:
        raise ValueError("Source is not the reviewed revision")
    return path


def definitions(root, relative, names, bindings):
    path = checked_source(root, relative)
    tree = ast.parse(path.read_bytes(), filename=relative)
    nodes = [
        n
        for n in tree.body
        if isinstance(n, (ast.ClassDef, ast.FunctionDef, ast.AsyncFunctionDef))
        and n.name in names
    ]
    if {n.name for n in nodes} != set(names):
        raise ValueError("Missing reviewed definition")
    namespace = {"__name__": "legacy_probe", **bindings}
    exec(
        compile(
            ast.Module(body=nodes, type_ignores=[]), relative, "exec", dont_inherit=True
        ),
        namespace,
    )
    return namespace


class Clock:
    def __init__(self, fail=False):
        self.now = 0.0
        self.fail = fail

    def time(self):
        return self.now

    def sleep(self, seconds):
        if self.fail:
            raise InterruptedError("Controlled interruption")
        self.now += seconds
        if self.now > 40:
            raise TimeoutError("Virtual time budget")


class Inputs:
    def __init__(self):
        self.held = set()
        self.events = []

    def keyDown(self, key):
        self.held.add(key)
        self.events.append(("down", key))

    def keyUp(self, key):
        self.held.discard(key)
        self.events.append(("up", key))


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def run(source_root: Path, output: Path):
    import cv2
    import numpy as np
    from PIL import Image

    cv2.setNumThreads(1)
    audit = json.loads((source_root.parent / "audit.json").read_text(encoding="utf-8"))
    projects = {p["archive"]: p for p in audit["projects"]}

    def relative(archive, suffix):
        base = Path(projects[archive]["source_copy"])
        matches = [
            p for p in base.rglob(Path(suffix).name) if p.as_posix().endswith(suffix)
        ]
        if len(matches) != 1:
            raise ValueError(f"Expected exactly one source: {suffix}")
        return matches[0].relative_to(source_root).as_posix()

    results = []

    def probe(archive, name, function):
        start = time.perf_counter()
        row = {"archive": archive, "test": name, "status": "passed", "live": False}
        try:
            with redirect_stdout(io.StringIO()):
                detail = function()
            if detail is not None:
                row["detail"] = detail
        except ModuleNotFoundError as error:
            row.update(
                status="blocked",
                detail=f"Missing isolated test dependency: {error.name}",
            )
        except Exception as error:
            row.update(status="failed", detail=f"{type(error).__name__}: {error}"[:350])
        row["elapsed_ms"] = round((time.perf_counter() - start) * 1000, 2)
        results.append(row)
        output.write_text(
            json.dumps(
                {"schema_version": 1, "complete": False, "checks": results}, indent=2
            )
            + "\n",
            encoding="utf-8",
        )
        print(json.dumps(row), flush=True)

    for archive, project in projects.items():

        def assets(project=project):
            count = 0
            for path in Path(project["source_copy"]).rglob("*"):
                if path.suffix.lower() not in {".png", ".jpg", ".jpeg", ".bmp", ".ico"}:
                    continue
                with Image.open(path) as image:
                    require(
                        image.width * image.height <= 40_000_000,
                        "Image exceeds review budget",
                    )
                    image.verify()
                count += 1
            return {
                "decoded_images": count,
                "scope": "asset decoding only; no recognition accuracy claim",
            }

        probe(archive, "image_assets_decode", assets)

    main = "Deimos-Wizard101-main.zip"
    deck_file = relative(main, "src/deck_encoder.py")
    deck = definitions(
        source_root,
        deck_file,
        ["DeckEncoderDecoder"],
        {"base64": base64, "zlib": zlib, "Optional": Optional},
    )["DeckEncoderDecoder"]

    def deck_roundtrip():
        for value in (
            {"normal": {}, "tc": {}, "item": {}},
            {"normal": {"Fire Cat": 3}, "tc": {"Storm Shark": 1}, "item": {"Wand": 2}},
        ):
            require(
                deck(token=deck(deck=value).encode()).decode() == value,
                "Deck changed after roundtrip",
            )
        return "Two decks preserve names, sections and counts"

    probe(main, "deck_roundtrip", deck_roundtrip)

    def malformed_deck():
        token = base64.b64encode(zlib.compress(b"N")).decode()
        try:
            deck(token=token).decode()
        except ValueError:
            return "Malformed token rejected consistently"
        raise AssertionError("Malformed token accepted")

    probe(main, "deck_malformed_section_count", malformed_deck)

    def compiler():
        compiler_path = checked_source(
            source_root, relative(main, "src/deimoslang/ir.py")
        )
        for name in ("__init__", "types", "tokenizer", "parser", "sem"):
            checked_source(
                source_root,
                (compiler_path.parent / (name + ".py"))
                .relative_to(source_root)
                .as_posix(),
            )
        spec = importlib.util.spec_from_file_location(
            "_legacy_language",
            compiler_path.parent / "__init__.py",
            submodule_search_locations=[str(compiler_path.parent)],
        )
        module = importlib.util.module_from_spec(spec)
        sys.modules[spec.name] = module
        spec.loader.exec_module(module)
        compiler_class = importlib.import_module("_legacy_language.ir").Compiler
        programs = [
            "sleep 0.1\n",
            "times 3 {\nsleep 0.1\n}\n",
            "if p1 incombat {\nsleep 0.1\n} else {\nsleep 0.2\n}\n",
        ]
        lengths = [len(compiler_class.from_text(code).compile()) for code in programs]
        require(all(length > 0 for length in lengths), "Empty compiled program")
        return {"programs": 3, "instructions": lengths, "vm_executed": False}

    probe(main, "routine_language_compile", compiler)

    xyz = namedtuple("XYZ", "x y z")
    for archive in (main, "Deimos-Wizard101-master.zip"):
        nav = definitions(
            source_root,
            relative(archive, "src/teleport_math.py"),
            [
                "calc_Distance",
                "calc_squareDistance",
                "are_xyzs_within_threshold",
                "TypedBytes",
                "parse_nav_data",
            ],
            {
                "math": math,
                "XYZ": xyz,
                "BytesIO": io.BytesIO,
                "Tuple": Tuple,
                "Union": Union,
                "struct": struct,
                "type_format_dict": {"short": "<h", "int": "<i", "float": "<f"},
            },
        )

        def geometry(nav=nav):
            require(
                nav["calc_Distance"](xyz(0, 0, 0), xyz(3, 4, 0)) == 5, "Wrong distance"
            )
            raw = (
                struct.pack("<hhh", 2, 2, 0)
                + struct.pack("<fffh", 0, 0, 0, 0)
                + struct.pack("<fffh", 3, 4, 0, 1)
                + struct.pack("<ihh", 1, 0, 1)
            )
            vertices, edges = nav["parse_nav_data"](raw)
            require(
                vertices == [xyz(0, 0, 0), xyz(3, 4, 0)] and edges == [(0, 1)],
                "Wrong navigation graph",
            )
            return "Distance and a two-vertex graph fixture"

        probe(archive, "navigation_geometry", geometry)
        probe(
            archive,
            "navigation_opposite_coordinates",
            lambda nav=nav: require(
                not nav["are_xyzs_within_threshold"](
                    xyz(-100, 0, 0), xyz(100, 0, 0), 10
                ),
                "Opposite coordinates incorrectly treated as nearby",
            ),
        )

    garden = "gardening-bot-main.zip"
    action_source = relative(garden, "utils/actions.py")

    def garden_actions(interrupt=False):
        inputs, clock = Inputs(), Clock(fail=interrupt)
        actions = definitions(
            source_root,
            action_source,
            ["Actions"],
            {"pyautogui": inputs, "time": clock},
        )["Actions"]()
        try:
            actions.perform_movements(
                [{"key": "a", "duration": 0.1}, {"key": "w", "duration": 0.2}]
            )
        except InterruptedError:
            pass
        require(not inputs.held, "Interrupted legacy action left a key held")
        if not interrupt:
            require(
                inputs.events
                == [("down", "a"), ("up", "a"), ("down", "w"), ("up", "w")],
                "Wrong movement order",
            )

    probe(garden, "movement_order", garden_actions)
    probe(garden, "interrupted_key_release", lambda: garden_actions(True))

    def garden_waits():
        clock = Clock()
        actions = definitions(source_root, action_source, ["Actions"], {"time": clock})[
            "Actions"
        ]()
        actions.is_image_visible = lambda *args: False
        require(
            actions.wait_until_visible("fixture", timeout=1) is False,
            "Missing image did not time out",
        )
        require(clock.now <= 2, "Timeout exceeded bound")
        actions.is_image_visible = lambda *args: True
        require(
            actions.wait_until_visible("fixture", timeout=1) is True,
            "Visible state not recognized",
        )

    probe(garden, "visibility_and_timeout", garden_waits)

    afk = "afk-wizard-main.zip"

    def afk_state():
        state = [None]
        fake = SimpleNamespace(locateOnScreen=lambda *a, **k: state[0])
        detect = definitions(
            source_root, relative(afk, "main.py"), ["in_battle"], {"auto": fake}
        )["in_battle"]
        require(detect() is True, "Missing book should match legacy battle assumption")
        state[0] = (1, 2, 3, 4)
        require(
            detect() is False, "Visible book should indicate legacy non-battle state"
        )
        return "Logic only; an obscured/missing book still falsely implies battle"

    probe(afk, "battle_state_truth_table", afk_state)

    automatus = "Automatus-v2-master.zip"

    def statistics():
        ns = definitions(
            source_root,
            relative(automatus, "lib/statistic.py"),
            ["StatCategory", "Statistic"],
            {"Enum": Enum, "log": lambda *a: None},
        )
        state = ns["Statistic"](False)
        key = ns["StatCategory"].LOOP_COUNT
        state.update(key, 1)
        state.update(key, 2)
        require(state.data[key] == 3, "Counter lost updates")
        try:
            state.update("invalid", 1)
        except TypeError:
            return "Counter accumulates; invalid category rejected"
        raise AssertionError("Invalid statistic accepted")

    probe(automatus, "typed_statistics", statistics)

    with tempfile.TemporaryDirectory(
        prefix="visual-fixtures-", dir=output.parent
    ) as temp:
        temp = Path(temp)
        rng = np.random.default_rng(101)
        template = rng.integers(20, 235, (18, 32, 3), dtype=np.uint8)
        canvas = np.zeros((100, 150, 3), dtype=np.uint8)
        canvas[31:49, 47:79] = template
        small, large = temp / "template.png", temp / "scene.png"
        cv2.imwrite(str(small), template)
        cv2.imwrite(str(large), canvas)
        api_archive = "wizAPI-master.zip"
        api = definitions(
            source_root, relative(api_archive, "wizAPI.py"), ["wizAPI"], {"cv2": cv2}
        )["wizAPI"]()
        probe(
            api_archive,
            "nonsquare_match_center",
            lambda: require(
                api.match_image(str(large), str(small)) == (63.0, 40.0),
                "Template center swaps width and height",
            ),
        )
        blank = temp / "blank.png"
        cv2.imwrite(str(blank), np.zeros_like(canvas))
        probe(
            api_archive,
            "absent_template",
            lambda: require(
                api.match_image(str(blank), str(small)) is False,
                "Blank scene falsely matched",
            ),
        )
        sdk = "wizSDK-master.zip"
        sdk_match = definitions(
            source_root,
            relative(sdk, "wizsdk/pixels.py"),
            ["_to_cv2_img", "match_image"],
            {"cv2": cv2, "np": np},
        )["match_image"]
        probe(
            sdk,
            "nonsquare_match_and_absent",
            lambda: require(
                sdk_match(canvas, template) == (63, 40)
                and sdk_match(np.zeros_like(canvas), template) is False,
                "Incorrect SDK template match",
            ),
        )

        bazaar = "Wizard101-Bazaar-Tc-Farming-master.zip"
        match = definitions(
            source_root,
            relative(bazaar, "imagesearch.py"),
            ["imagesearch"],
            {"cv2": cv2, "np": np},
        )["imagesearch"]
        # Gray fixtures isolate the legacy match logic from its RGB/BGR mismatch.
        gray = cv2.cvtColor(template, cv2.COLOR_BGR2GRAY)
        mono = np.zeros((100, 150), dtype=np.uint8)
        mono[31:49, 47:79] = gray
        cv2.imwrite(str(small), gray)
        probe(
            bazaar,
            "image_match_and_absent",
            lambda: require(
                match(str(small), screenshot=Image.fromarray(mono).convert("RGB"))
                == (47, 31)
                and match(str(small), screenshot=Image.new("RGB", (150, 100)))
                == [-1, -1],
                "Incorrect bazaar image location",
            ),
        )

        farming = "Wizard101-Farming-main.zip"
        fake_cv = SimpleNamespace(
            matchTemplate=cv2.matchTemplate,
            cvtColor=cv2.cvtColor,
            rectangle=cv2.rectangle,
            imwrite=lambda *a: True,
            COLOR_RGB2BGR=cv2.COLOR_RGB2BGR,
            TM_CCOEFF_NORMED=cv2.TM_CCOEFF_NORMED,
        )

        def farming_match():
            image = Image.fromarray(cv2.cvtColor(canvas, cv2.COLOR_BGR2RGB))
            window = SimpleNamespace(
                activate=lambda: None, left=0, top=0, width=150, height=100
            )
            ns = definitions(
                source_root,
                relative(farming, "Play.py"),
                ["Match"],
                {
                    "cv2": fake_cv,
                    "np": np,
                    "w": window,
                    "pyautogui": SimpleNamespace(screenshot=lambda **k: image),
                    "card_width": 32,
                    "card_height": 18,
                    "threshold": 0.999,
                },
            )
            require(
                ns["Match"]([template, (255, 255, 0)]) == [63, 40],
                "Wrong farming card center",
            )
            ns["pyautogui"].screenshot = lambda **k: Image.new("RGB", (150, 100))
            require(
                ns["Match"]([template, (255, 255, 0)]) == "",
                "Blank frame falsely matched",
            )
            return "Fake activation/capture/output; no real window touched"

        probe(farming, "card_match_and_absent", farming_match)

        def wad_fixture(version, compressed, magic=b"KIWAD"):
            data = b"Maelstrom offline asset fixture: " * 8
            stored = zlib.compress(data) if compressed else data
            name = b"folder/test.txt\0"
            header_size = 13 + (version >= 2) + 21 + len(name)
            raw = (
                magic
                + struct.pack("<ii", version, 1)
                + (b"\1" if version >= 2 else b"")
            )
            raw += (
                struct.pack(
                    "<iii?Ii",
                    header_size,
                    len(data),
                    len(stored) if compressed else -1,
                    compressed,
                    zlib.crc32(stored),
                    len(name),
                )
                + name
                + stored
            )
            file = temp / f"fixture-{version}-{compressed}-{magic.decode()}.wad"
            file.write_bytes(raw)
            return file, data

        wad_archive = "wizwad-main.zip"
        wad_ns = definitions(
            source_root,
            relative(wad_archive, "wizwad/wad.py"),
            ["WadFileInfo", "Wad"],
            {
                "dataclass": dataclass,
                "Path": Path,
                "Union": Union,
                "List": List,
                "Optional": Optional,
                "BytesIO": io.BytesIO,
                "mmap": mmap,
                "ACCESS_READ": ACCESS_READ,
                "struct": struct,
                "zlib": zlib,
            },
        )

        def wad_reads():
            for version in (1, 2):
                for compressed in (False, True):
                    path, expected = wad_fixture(version, compressed)
                    with wad_ns["Wad"](path) as wad:
                        require(
                            wad.name_list() == ["folder/test.txt"], "Wrong asset name"
                        )
                        require(
                            wad.read("folder/test.txt") == expected,
                            "Wrong decompressed bytes",
                        )
            return "Four independent format fixtures, both versions and compression modes; writer not tested"

        probe(wad_archive, "read_v1_v2_plain_compressed", wad_reads)

        def wad_magic():
            path, _ = wad_fixture(2, False, b"WRONG")
            try:
                with wad_ns["Wad"](path):
                    pass
            except ValueError:
                return
            raise AssertionError("Non-KIWAD signature accepted")

        probe(wad_archive, "invalid_signature_rejected", wad_magic)

        walker = "wizwalker-master.zip"
        walker_ns = definitions(
            source_root,
            relative(walker, "file_readers/wad.py"),
            ["WadFileInfo", "Wad"],
            {"Path": Path, "Union": Union, "List": List, "struct": struct},
        )

        def walker_journal():
            for version in (1, 2):
                path, expected = wad_fixture(version, False)
                wad = walker_ns["Wad"](path)
                with path.open("rb") as stream:
                    wad._file_pointer = stream
                    wad._refresh_journal()
                require(
                    len(wad._file_list) == 1
                    and wad._file_list[0].name == "folder/test.txt"
                    and wad._file_list[0].size == len(expected),
                    "Wrong journal metadata",
                )
            return "Journal parsing only; client-memory hooks and async extraction not exercised"

        probe(walker, "wad_journal_v1_v2", walker_journal)

    report = {
        "schema_version": 1,
        "complete": True,
        "mode": "offline_component_probes",
        "input_sent": False,
        "models_loaded": False,
        "checks": results,
        "note": "Limited fixtures do not qualify whole applications or current W101 compatibility.",
    }
    output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--source-root", type=Path, default=ROOT / ".local/legacy-validation/sources"
    )
    parser.add_argument(
        "--output", type=Path, default=ROOT / ".local/legacy-validation/components.json"
    )
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    run(args.source_root.resolve(), args.output.resolve())


if __name__ == "__main__":
    main()
