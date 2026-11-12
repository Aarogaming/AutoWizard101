"""Unit tests for repaired Deimos math and deck encoding components."""

import ast
import base64
from collections import namedtuple
from pathlib import Path
from typing import Optional
import unittest
from unittest import mock
import zlib

ROOT = Path(__file__).resolve().parents[2]
DEIMOS_SRC = (
    ROOT / "ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/Deimos-Wizard101/src"
)

XYZ = namedtuple("XYZ", "x y z")


def load_teleport_math_definitions(src_path: Path):
    tree = ast.parse(src_path.read_bytes(), filename=src_path.name)
    target_names = {
        "are_xyzs_within_threshold",
        "calc_Distance",
        "calc_squareDistance",
    }
    nodes = [
        n
        for n in tree.body
        if isinstance(n, ast.FunctionDef) and n.name in target_names
    ]
    namespace = {"XYZ": XYZ, "pow": pow}
    exec(
        compile(ast.Module(body=nodes, type_ignores=[]), src_path.name, "exec"),
        namespace,
    )
    return namespace


def load_deck_encoder(src_path: Path):
    tree = ast.parse(src_path.read_bytes(), filename=src_path.name)
    nodes = [
        n
        for n in tree.body
        if isinstance(n, ast.ClassDef) and n.name == "DeckEncoderDecoder"
    ]
    namespace = {
        "base64": base64,
        "zlib": zlib,
        "Optional": Optional,
    }
    exec(
        compile(ast.Module(body=nodes, type_ignores=[]), src_path.name, "exec"),
        namespace,
    )
    return namespace["DeckEncoderDecoder"]


class DeimosTeleportMathTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.funcs = load_teleport_math_definitions(DEIMOS_SRC / "teleport_math.py")

    def are_within(self, xyz_1, xyz_2, threshold=200):
        return self.funcs["are_xyzs_within_threshold"](xyz_1, xyz_2, threshold)

    def test_opposite_coordinates_not_treated_as_nearby(self):
        # Opposite coordinates along X, Y, or Z should NOT be treated as nearby
        self.assertFalse(self.are_within(XYZ(-100, 0, 0), XYZ(100, 0, 0), 10))
        self.assertFalse(self.are_within(XYZ(0, -100, 0), XYZ(0, 100, 0), 10))
        self.assertFalse(self.are_within(XYZ(0, 0, -100), XYZ(0, 0, 100), 10))

    def test_nearby_coordinates_within_threshold(self):
        # Coordinates within the threshold on all axes return True
        self.assertTrue(self.are_within(XYZ(100, 50, 20), XYZ(102, 48, 22), 5))
        self.assertTrue(self.are_within(XYZ(-100, -50, -20), XYZ(-98, -52, -19), 5))

    def test_single_axis_exceeding_threshold_fails(self):
        # If any single axis exceeds threshold, should return False
        self.assertFalse(self.are_within(XYZ(100, 50, 20), XYZ(110, 50, 20), 5))
        self.assertFalse(self.are_within(XYZ(100, 50, 20), XYZ(100, 60, 20), 5))
        self.assertFalse(self.are_within(XYZ(100, 50, 20), XYZ(100, 50, 30), 5))

    def test_exact_threshold_boundary(self):
        # Exact delta equal to threshold is not strictly less than threshold
        self.assertFalse(self.are_within(XYZ(0, 0, 0), XYZ(10, 0, 0), 10))


class DeimosDeckEncoderTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.DeckEncoder = load_deck_encoder(DEIMOS_SRC / "deck_encoder.py")

    def test_roundtrip_empty_deck(self):
        deck = {"normal": {}, "tc": {}, "item": {}}
        encoder = self.DeckEncoder(deck=deck)
        token = encoder.encode()
        self.assertTrue(isinstance(token, str) and len(token) > 0)
        decoded = self.DeckEncoder(token=token).decode()
        self.assertEqual(decoded, deck)

    def test_roundtrip_populated_deck(self):
        deck = {
            "normal": {"Fire Cat": 3, "Heckhound": 1},
            "tc": {"Tower Shield": 4},
            "item": {"Wand Hit": 5},
        }
        encoder = self.DeckEncoder(deck=deck)
        token = encoder.encode()
        decoded = self.DeckEncoder(token=token).decode()
        self.assertEqual(decoded, deck)

    def test_malformed_token_insufficient_sections_raises_value_error(self):
        # b"N" decompresses to a single section instead of 3
        token = base64.b64encode(zlib.compress(b"N")).decode()
        with self.assertRaises(ValueError):
            self.DeckEncoder(token=token).decode()

    def test_malformed_token_corrupted_payload_raises_value_error(self):
        with self.assertRaises(ValueError):
            self.DeckEncoder(token="not-valid-base64!@#$").decode()

    def test_missing_token_and_deck_raises_value_error(self):
        with self.assertRaises(ValueError):
            self.DeckEncoder().decode()
        with self.assertRaises(ValueError):
            self.DeckEncoder().encode()


if __name__ == "__main__":
    unittest.main()
