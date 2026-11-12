"""Tests for repaired legacy components: wizAPI, wizwad, and gardening-bot actions."""

import ast
import io
from mmap import ACCESS_READ, mmap
from pathlib import Path
import struct
import tempfile
from typing import List, Optional, Union
import unittest
from unittest.mock import MagicMock
import zlib

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS_LIB = ROOT / "ProjectMaelstrom/ProjectMaelstrom/Scripts/Library"


class WizApiRepairTests(unittest.TestCase):
    def test_nonsquare_match_center(self):
        # Test that match_image computes center with width and height in correct order
        path = SCRIPTS_LIB / "wizAPI/wizAPI-master/wizAPI.py"
        tree = ast.parse(path.read_bytes(), filename=path.name)
        nodes = [
            n for n in tree.body if isinstance(n, ast.ClassDef) and n.name == "wizAPI"
        ]

        class FakeCV2:
            TM_SQDIFF_NORMED = 1

            def __init__(self, rect_w, rect_h, match_x, match_y):
                self.rect_w = rect_w
                self.rect_h = rect_h
                self.match_x = match_x
                self.match_y = match_y

            def imread(self, img_path):
                # Return dummy object with shape (height, width, channels)
                if "small" in img_path:
                    # shape is (height, width, 3)
                    mock_arr = MagicMock()
                    mock_arr.shape = (self.rect_h, self.rect_w, 3)
                    return mock_arr
                else:
                    mock_arr = MagicMock()
                    mock_arr.shape = (200, 200, 3)
                    return mock_arr

            def matchTemplate(self, small, large, method):
                return MagicMock()

            def minMaxLoc(self, result):
                # (minVal, maxVal, minLoc, maxLoc)
                return (0.01, 1.0, (self.match_x, self.match_y), (0, 0))

        fake_cv2 = FakeCV2(rect_w=60, rect_h=30, match_x=10, match_y=20)
        namespace = {"cv2": fake_cv2}
        exec(
            compile(ast.Module(body=nodes, type_ignores=[]), path.name, "exec"),
            namespace,
        )
        api = namespace["wizAPI"]()
        center = api.match_image("large.png", "small.png", threshold=0.1)

        # Expected center: x + w/2 = 10 + 30 = 40.0, y + h/2 = 20 + 15 = 35.0
        self.assertEqual(center, (40.0, 35.0))


class WizWadRepairTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        path = SCRIPTS_LIB / "wizwad-main/wizwad-main/wizwad/wad.py"
        tree = ast.parse(path.read_bytes(), filename=path.name)
        target_classes = {"WadFileInfo", "Wad"}
        nodes = [
            n
            for n in tree.body
            if isinstance(n, ast.ClassDef) and n.name in target_classes
        ]
        from dataclasses import dataclass

        namespace = {
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
        }
        exec(
            compile(ast.Module(body=nodes, type_ignores=[]), path.name, "exec"),
            namespace,
        )
        cls.Wad = namespace["Wad"]

    def test_valid_kiwad_signature_accepted(self):
        with tempfile.NamedTemporaryFile(suffix=".wad", delete=False) as f:
            # KIWAD header: 5 bytes magic, 4 bytes version (1), 4 bytes file count (0)
            f.write(b"KIWAD" + struct.pack("<ll", 1, 0))
            f.flush()
            temp_path = Path(f.name)

        try:
            with self.Wad(temp_path) as wad:
                self.assertEqual(wad.name_list(), [])
        finally:
            temp_path.unlink(missing_ok=True)

    def test_invalid_signature_rejected_with_value_error(self):
        with tempfile.NamedTemporaryFile(suffix=".wad", delete=False) as f:
            f.write(b"WRONG" + struct.pack("<ll", 1, 0))
            f.flush()
            temp_path = Path(f.name)

        try:
            with self.assertRaises(ValueError):
                with self.Wad(temp_path):
                    pass
        finally:
            temp_path.unlink(missing_ok=True)

    def test_truncated_file_rejected_with_value_error(self):
        with tempfile.NamedTemporaryFile(suffix=".wad", delete=False) as f:
            f.write(b"KIW")
            f.flush()
            temp_path = Path(f.name)

        try:
            with self.assertRaises(ValueError):
                with self.Wad(temp_path):
                    pass
        finally:
            temp_path.unlink(missing_ok=True)


class GardeningActionsRepairTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        path = SCRIPTS_LIB / "gardening-bot-main/gardening-bot-main/utils/actions.py"
        tree = ast.parse(path.read_bytes(), filename=path.name)
        nodes = [
            n for n in tree.body if isinstance(n, ast.ClassDef) and n.name == "Actions"
        ]
        cls.actions_class_nodes = nodes
        cls.path_name = path.name

    def _make_actions(self, interrupt=False):
        class FakeInputs:
            def __init__(self):
                self.held = set()
                self.events = []

            def keyDown(self, key):
                self.held.add(key)
                self.events.append(("down", key))

            def keyUp(self, key):
                self.held.discard(key)
                self.events.append(("up", key))

        class FakeTime:
            def sleep(self, seconds):
                if interrupt:
                    raise InterruptedError("Interrupted action")

        inputs = FakeInputs()
        fake_time = FakeTime()
        namespace = {"pyautogui": inputs, "time": fake_time}
        exec(
            compile(
                ast.Module(body=self.actions_class_nodes, type_ignores=[]),
                self.path_name,
                "exec",
            ),
            namespace,
        )
        actions = namespace["Actions"]()
        return actions, inputs

    def test_normal_movement_order(self):
        actions, inputs = self._make_actions(interrupt=False)
        actions.perform_movements(
            [{"key": "a", "duration": 0.1}, {"key": "w", "duration": 0.2}]
        )
        self.assertEqual(
            inputs.events,
            [("down", "a"), ("up", "a"), ("down", "w"), ("up", "w")],
        )
        self.assertEqual(len(inputs.held), 0)

    def test_interrupted_action_clears_held_keys(self):
        actions, inputs = self._make_actions(interrupt=True)
        try:
            actions.perform_movements([{"key": "a", "duration": 0.1}])
        except InterruptedError:
            pass

        # Key 'a' must NOT remain in inputs.held despite the interruption
        self.assertEqual(inputs.held, set())
        self.assertEqual(inputs.events, [("down", "a"), ("up", "a")])


if __name__ == "__main__":
    unittest.main()
