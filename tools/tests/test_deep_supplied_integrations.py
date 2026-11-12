"""
Unit tests for the four modernized supplied project components:
1. tools/automatus_topology.py (Automatus-v2 55-zone hierarchy & route topology)
2. tools/pet_talent_calculator.py (Wizard101-Utilities pet stat math)
3. tools/deck_encoder.py (Deimos zlib + base64 deck serialization token codec)
4. tools/wizsdk_combat_geometry.py (wizSDK enchant math & targeting geometry)
"""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "tools"))

from tools.automatus_topology import AutomatusTopologyResolver
from tools.deck_encoder import DeckEncoderDecoder
from tools.pet_talent_calculator import (
    PetAttributes,
    PetTalentCalculator,
    PetTalentDefinition,
)
from tools.wizsdk_combat_geometry import CombatCardGeometry, WizSDKCombatGeometry


class TestSuppliedProjectsDeepIntegrations(unittest.TestCase):
    def test_automatus_topology(self):
        resolver = AutomatusTopologyResolver()

        # Zone name resolution
        self.assertEqual(resolver.get_zone_path("COMMONS"), "WizardCity/WC_Hub")
        self.assertEqual(
            resolver.get_zone_path("BAZAAR"),
            "WizardCity/WC_Streets/Interiors/WC_OldeTown_AuctionHouse",
        )
        self.assertEqual(resolver.get_codename("WizardCity/WC_Ravenwood"), "RAVENWOOD")

        # BFS shortest path navigation
        path = resolver.find_shortest_zone_path("COMMONS", "BAZAAR")
        self.assertEqual(path, ["COMMONS", "SHOPPING_DISTRICT", "OLDE_TOWN", "BAZAAR"])

        path_ravenwood = resolver.find_shortest_zone_path("COMMONS", "SCHOOL_OF_FIRE")
        self.assertEqual(path_ravenwood, ["COMMONS", "RAVENWOOD", "SCHOOL_OF_FIRE"])

        # Verified Unicorn Way coordinates preserved
        self.assertEqual(len(resolver.UNICORN_WAY_COORDINATES), 6)
        self.assertEqual(resolver.UNICORN_WAY_COORDINATES[0], (27, 27, -27))

    def test_pet_talent_calculator(self):
        # Max stats pet
        max_pet = PetAttributes(
            strength=255, intellect=250, agility=260, will=260, power=250
        )

        # Spell-Proof (Universal Resist): (2 * (Str + Agi) + Pow) * 2 / 250
        proof_def = PetTalentCalculator.STANDARD_TALENTS["Spell-Proof"]
        proof_val = PetTalentCalculator.calculate_talent_value(proof_def, max_pet)
        # (2 * (255 + 260) + 250) * 0.008 = (1030 + 250) * 0.008 = 1280 * 0.008 = 10.24%
        self.assertAlmostEqual(proof_val, 10.24, places=2)

        # Fire-Dealer (Fire Damage): (2 * (Str + Will) + Pow) * 3 / 400
        fire_dealer_def = PetTalentCalculator.STANDARD_TALENTS["Fire-Dealer"]
        dealer_val = PetTalentCalculator.calculate_talent_value(
            fire_dealer_def, max_pet
        )
        # (2 * (255 + 260) + 250) * 0.0075 = 1280 * 0.0075 = 9.6%
        self.assertAlmostEqual(dealer_val, 9.60, places=2)

        # Summary pool aggregation
        pool = ["Spell-Proof", "Spell-Defying", "Fire-Dealer"]
        summary = PetTalentCalculator.calculate_pool_summary(pool, max_pet)
        self.assertIn("Resist", summary)
        self.assertIn("Damage", summary)
        self.assertAlmostEqual(summary["Damage"]["Fire"], 9.60, places=2)

    def test_deck_encoder_decoder(self):
        deck = {
            "normal": {"Epic": 3, "Tempest": 4, "Storm Blade": 2},
            "tc": {"Empower TC": 5, "Feint TC": 2},
            "item": {"Wand Hit": 1},
        }

        encoder = DeckEncoderDecoder(deck=deck)
        token = encoder.encode()
        self.assertTrue(isinstance(token, str) and len(token) > 0)

        decoder = DeckEncoderDecoder(token=token)
        decoded_deck = decoder.decode()
        self.assertEqual(decoded_deck, deck)

    def test_wizsdk_combat_geometry(self):
        # Enchant calculation
        epic = CombatCardGeometry(name="Epic", spell_x=260, card_width=52)
        tempest = CombatCardGeometry(name="Tempest", spell_x=320, card_width=52)

        c1, c2, enchanted = WizSDKCombatGeometry.calculate_enchant_geometry(
            epic, tempest
        )
        self.assertEqual(c1, (260, 325))
        self.assertEqual(c2, (320, 325))
        self.assertEqual(enchanted.name, "Epic-Tempest")
        # Since enchant is before (260 < 320): new_pos = 320 + 26 - 52 = 294
        self.assertEqual(enchanted.spell_x, 294)

        # Targeting coordinates
        enemy_0 = WizSDKCombatGeometry.calculate_target_coordinates(0)
        self.assertEqual(enemy_0, (130, 50))
        enemy_1 = WizSDKCombatGeometry.calculate_target_coordinates(1)
        self.assertEqual(enemy_1, (304, 50))

        ally_4 = WizSDKCombatGeometry.calculate_target_coordinates(4)
        self.assertEqual(ally_4, (682, 590))

        # Pedestal positions
        ped_0 = WizSDKCombatGeometry.get_enemy_pedestal_coordinates(0)
        self.assertEqual(ped_0, (203, 75))
        ped_1 = WizSDKCombatGeometry.get_enemy_pedestal_coordinates(1)
        self.assertEqual(ped_1, (377, 75))


if __name__ == "__main__":
    unittest.main()
