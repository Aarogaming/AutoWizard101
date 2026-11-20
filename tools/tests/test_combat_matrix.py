"""Unit tests for the Precomputed Combat Matrix."""

import json
from pathlib import Path
import unittest

from tools.precompute_combat_matrix import CombatMatrixLookup, precompute_matrix

ROOT = Path(__file__).resolve().parents[2]


class TestCombatMatrix(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fixture_path = ROOT / "tools/fixtures/combat/combat_lookup_matrix.json"
        cls.lookup_client = CombatMatrixLookup.load(cls.fixture_path)

    def test_fixture_structure(self):
        self.assertTrue(self.fixture_path.exists())
        data = json.loads(self.fixture_path.read_text(encoding="utf-8"))
        self.assertEqual(data["schema"], "maelstrom.combat-matrix.v1")
        self.assertGreater(data["total_entries"], 200)
        self.assertIn("matrix", data)

    def test_emergency_heal_lookup(self):
        # hp_ratio = 0.25 (critical health), pips = 4
        res = self.lookup_client.lookup(
            pips=4, hp_ratio=0.25, enemy_hp=500, gear_boost=30.0
        )
        self.assertIsNotNone(res)
        self.assertEqual(res["action"], "CAST_SPELL")
        self.assertEqual(res["card_name"], "Pixie")
        self.assertEqual(res["rule_triggered"], "EMERGENCY_HEAL")

    def test_lethal_strike_lookup(self):
        # hp_ratio = 1.0 (healthy), enemy_hp = 200, pips = 4, gear_boost = 100.0
        res = self.lookup_client.lookup(
            pips=4, hp_ratio=1.0, enemy_hp=200, gear_boost=100.0
        )
        self.assertIsNotNone(res)
        self.assertEqual(res["action"], "CAST_SPELL")
        self.assertEqual(res["rule_triggered"], "LETHAL_ATTACK")

    def test_buff_setup_vs_high_hp(self):
        # hp_ratio = 1.0, enemy_hp = 3000, pips = 2, blades = 0
        res = self.lookup_client.lookup(
            pips=2, hp_ratio=1.0, enemy_hp=3000, gear_boost=0.0, blades=0
        )
        self.assertIsNotNone(res)
        self.assertEqual(res["action"], "CAST_SPELL")
        self.assertEqual(res["rule_triggered"], "BUFF_SETUP_BLADE")
        self.assertEqual(res["card_name"], "Fire Blade")


if __name__ == "__main__":
    unittest.main()
