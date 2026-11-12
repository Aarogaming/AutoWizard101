"""Unit tests for the deep integration of supplied projects into Maelstrom.

Covers:
1. Deimos UI Paths -> tools/w101_ui_catalog.py
2. Bazaar TC Farming -> tools/bazaar_tc_sniper.py
3. Gardening Bot -> tools/gardening_pipeline.py
4. WizWiki Central -> tools/boss_wiki_resolver.py
"""

from pathlib import Path
import unittest

from tools.bazaar_tc_sniper import (
    BUY_BUTTON_COORD,
    BUY_CONFIRM_OK_COORD,
    TC_TEMPLATES,
    BazaarSniperState,
)
from tools.boss_wiki_resolver import BossWikiResolver
from tools.gardening_pipeline import (
    COUCH_POTATOES_PROFILE,
    EVIL_MAGMA_PEAS_PROFILE,
    GardeningRoutineBuilder,
)
from tools.w101_ui_catalog import CATALOG, get_ui_path


class TestSuppliedProjectsIntegration(unittest.TestCase):
    # -------------------------------------------------------------------------
    # 1. UI Window Catalog (Deimos integration)
    # -------------------------------------------------------------------------
    def test_ui_catalog_keys_and_paths(self):
        self.assertIn("dialog.advance", CATALOG)
        self.assertIn("potion.buy", CATALOG)
        self.assertIn("spiral_door.teleport", CATALOG)
        self.assertIn("pet.dance_sigil_play", CATALOG)

        adv_path = get_ui_path("dialog.advance")
        self.assertEqual(adv_path, ["WorldView", "wndDialogMain", "btnRight"])

        potion_path = get_ui_path("potion.fill_all")
        self.assertEqual(potion_path, ["WorldView", "main", "fillallpotions"])

    # -------------------------------------------------------------------------
    # 2. Bazaar TC & Empower Sniping (Nico BazaarTCBot integration)
    # -------------------------------------------------------------------------
    def test_bazaar_tc_templates_and_empower_flow(self):
        self.assertGreaterEqual(len(TC_TEMPLATES), 25)
        self.assertIn("Empower", TC_TEMPLATES)
        self.assertIn("Deer Knight", TC_TEMPLATES)
        self.assertIn("Loremaster", TC_TEMPLATES)

        sniper = BazaarSniperState(
            target_tcs=["Empower", "Deer Knight"], max_gold=50000
        )

        # Empower price drop active -> emits purchase click sequence
        clicks = sniper.plan_empower_cycle(lowest_price_active=True)
        self.assertEqual(len(clicks), 2)
        self.assertEqual(clicks[0]["action"], "CLICK")
        self.assertEqual(clicks[0]["x"], BUY_BUTTON_COORD[0])
        self.assertEqual(clicks[1]["x"], BUY_CONFIRM_OK_COORD[0])

        # Record purchase updates gold balance
        sniper.record_purchase("Empower", price=3200, qty=10)
        self.assertEqual(sniper.current_gold, 18000)
        self.assertEqual(len(sniper.purchases), 1)

    # -------------------------------------------------------------------------
    # 3. Gardening Pipeline (gardening-bot-main Couch Potatoes integration)
    # -------------------------------------------------------------------------
    def test_gardening_likes_multipliers_and_routine(self):
        # Couch Potatoes likes: Tropical Garden Gnome (0.25), Pixie (0.20), Red Barn Farm (0.15)
        # Total multiplier: 1.0 + 0.25 + 0.20 + 0.15 = 1.60
        likes = ["tropical_garden_gnome", "pixie", "red_barn_farm"]
        durations = COUCH_POTATOES_PROFILE.calculate_phase_durations(likes)

        self.assertEqual(durations["speed_multiplier"], 1.60)
        self.assertLess(
            durations["total_to_elder_hours"], 258.0
        )  # Unboosted is 13+29+216=258

        # Compile gardening care cycle to MacroGraph
        graph = GardeningRoutineBuilder.build_care_cycle(
            garden_name="Couch Potatoes Red Barn",
            spells=["Downpour", "Flute Ensemble", "Gusty Winds"],
        )
        self.assertEqual(graph["schema"], "maelstrom.macro-graph.v1")
        self.assertEqual(graph["node_count"], 5)  # open + 3 spells + close

    # -------------------------------------------------------------------------
    # 4. Boss Wiki & Cheats (WizWikiAPI-main 1,851 creatures integration)
    # -------------------------------------------------------------------------
    def test_boss_wiki_resolver(self):
        resolver = BossWikiResolver()
        self.assertGreater(resolver.count, 1800)

        # Halfang Bristlecrown
        halfang = resolver.resolve("Halfang Bristlecrown")
        self.assertIsNotNone(halfang)
        self.assertEqual(halfang.max_hp, 1540)
        self.assertEqual(halfang.school, "Storm")
        self.assertTrue(halfang.is_boss)

        # Belloq (with Ra cheat)
        belloq = resolver.resolve("Belloq")
        self.assertIsNotNone(belloq)
        self.assertTrue(belloq.has_cheats)
        self.assertIn("Ra", belloq.cheats[0].trigger_text)


if __name__ == "__main__":
    unittest.main()
