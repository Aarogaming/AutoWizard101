"""Tests for Vision-to-Planner Combat Adapter."""

from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))

from combat_turn_planner import Combatant
from vision_combat_adapter import VisionCombatAdapter, VisualCardMatch


class TestVisionCombatAdapter(unittest.TestCase):

    def setUp(self):
        self.adapter = VisionCombatAdapter()

    def test_default_catalog_resolution(self):
        meteor = self.adapter.resolve_card_metadata("meteor")
        self.assertEqual(meteor.name, "Meteor Strike")
        self.assertEqual(meteor.school, "Fire")
        self.assertEqual(meteor.pip_cost, 4)
        self.assertTrue(meteor.is_aoe)

        pixie = self.adapter.resolve_card_metadata("pixie")
        self.assertEqual(pixie.spell_type, "Heal")
        self.assertEqual(pixie.heal_amount, 420.0)

        # Fallback for unknown card
        unknown = self.adapter.resolve_card_metadata("unknown_test_spell")
        self.assertEqual(unknown.name, "Unknown_Test_Spell")
        self.assertEqual(unknown.spell_type, "Attack")

    def test_template_directory_discovery(self):
        template_dirs = self.adapter.get_template_dirs("1024x768")
        self.assertIn("meteor", template_dirs)
        pngs = template_dirs["meteor"]
        self.assertGreaterEqual(len(pngs), 2)
        filenames = [p.name for p in pngs]
        self.assertIn("m1.png", filenames)
        self.assertIn("m2.png", filenames)

    def test_parse_hand_from_detections_sorts_by_x(self):
        raw_detections = [
            {"card_id": "meteor", "confidence": 0.95, "bbox": [250, 550, 60, 90]},
            {"card_id": "fire_cat", "confidence": 0.88, "bbox": [100, 550, 60, 90]},
            {"card_id": "fireblade", "confidence": 0.92, "bbox": [175, 550, 60, 90]},
        ]
        matches = self.adapter.parse_hand_from_detections(raw_detections)
        self.assertEqual(len(matches), 3)
        # Should be sorted left-to-right: fire_cat (100) -> fireblade (175) -> meteor (250)
        self.assertEqual(matches[0].card_id, "fire_cat")
        self.assertEqual(matches[1].card_id, "fireblade")
        self.assertEqual(matches[2].card_id, "meteor")

    def test_evaluate_turn_from_visuals_lethal_aoe(self):
        raw_detections = [
            {"card_id": "fire_cat", "confidence": 0.9, "bbox": [100, 550, 60, 90]},
            {"card_id": "meteor", "confidence": 0.95, "bbox": [180, 550, 60, 90]},
        ]
        matches = self.adapter.parse_hand_from_detections(raw_detections)

        player = Combatant(
            id="p1",
            name="Aaron Pyromancer",
            current_hp=1000,
            max_hp=1000,
            pips=0,
            power_pips=2,  # 4 effective pips
            school="Fire",
        )
        enemies = [
            Combatant(
                id="e1", name="Gobbler A", current_hp=200, max_hp=200, school="Ice"
            ),
            Combatant(
                id="e2", name="Gobbler B", current_hp=250, max_hp=250, school="Ice"
            ),
        ]

        proposal = self.adapter.evaluate_turn_from_visuals(matches, player, enemies)
        self.assertEqual(proposal.action, "CAST_SPELL")
        self.assertEqual(proposal.card_name, "Meteor Strike")
        self.assertEqual(proposal.rule_triggered, "LETHAL_ATTACK")
        self.assertTrue(proposal.is_lethal)
        # Verify visual bounding box is annotated in proposal rationale
        self.assertIn("Visual Target Box: (180, 550, 60, 90)", proposal.rationale)

    def test_build_turn_planner_payload(self):
        raw_detections = [
            {"card_id": "fireblade", "confidence": 0.9, "bbox": [150, 550, 60, 90]}
        ]
        matches = self.adapter.parse_hand_from_detections(raw_detections)
        player = Combatant(
            id="p1", name="Aaron", current_hp=800, max_hp=1000, pips=1, school="Fire"
        )
        enemies = [Combatant(id="e1", name="Enemy", current_hp=500, max_hp=500)]

        payload = self.adapter.build_turn_planner_payload(matches, player, enemies)
        self.assertIn("player", payload)
        self.assertIn("enemies", payload)
        self.assertIn("hand", payload)
        self.assertIn("visual_hand_metadata", payload)
        self.assertEqual(payload["hand"][0]["name"], "Fireblade")


if __name__ == "__main__":
    unittest.main()
