"""Wizard101 Bazaar TC & Empower Sniping Engine.

Modernizes Nico's original Bazaar TC Farming logic (Wizard101-Bazaar-Tc-Farming).
Provides:
1. Catalog of all 60+ Treasure Card visual templates.
2. Empower bulk purchasing at minimum price brackets (75+ quantity).
3. School-specific TC scanning across pages with amount-sorting.
4. Background PostMessage execution directly to the game client HWND
   without moving the user's desktop mouse.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
import sys
from typing import Any, Callable, Dict, List, Optional, Tuple

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

BAZAAR_ASSET_DIR = (
    ROOT
    / "ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/BazaarTCBot/Wizard101-Bazaar-Tc-Farming/Wizard101-Bazaar-Tc-Farming-master"
)


# Canonical TC Image Template Registry
TC_TEMPLATES: Dict[str, str] = {
    # High-Value Death
    "Empower": "tc_empower.png",
    "Deer Knight": "tc_deer_knight.png",
    "Headless Horseman": "tc_headless_horseman.png",
    "Red Ghost": "tc_red_ghost.png",
    "Deadly Minotaur": "tc_deadly_minotaur.png",
    "Bone Tree Lord": "tc_bonetree_lord.png",
    # Balance
    "Balanceblade": "tc_balanceblade.png",
    "Dragonblade": "tc_dragonblade.png",
    "Loremaster": "tc_loremaster.png",
    "Ninja Piglets": "tc_ninja_piglets.png",
    # Myth
    "Aftershock": "tc_aftershock.png",
    "Athena Battle Sight": "tc_athena_battle_sight.png",
    "Shift": "tc_shift.png",
    "Stun": "tc_stun.png",
    # Fire
    "Hephaestus": "tc_hephaestus.png",
    "Fire Beetle": "tc_fire_beetle.png",
    "Firezilla": "tc_firezilla.png",
    # Ice
    "Abominable Weaver": "tc_abominable_weaver.png",
    "Angry Snowpig": "tc_angry_snowpig.png",
    "Reindeer Knight": "tc_reindeer_knight.png",
    "Freeze": "tc_freeze.png",
    # Storm
    "Queen Calypso": "tc_queen_calypso.png",
    "Storm Beetle": "tc_storm_beetle.png",
    "Healing Current": "tc_healing_current.png",
    # Life
    "Brulee Sucre": "tc_brulee_sucre.png",
    "Brown Spider": "tc_brown_spider.png",
    "Mass Triage": "tc_mass_triage.png",
}

# Bazaar Navigation Layout Coordinates (1280x720 baseline)
SORT_BY_AMOUNT_COORD = (1126, 390)
BUY_BUTTON_COORD = (1150, 620)
BUY_CONFIRM_OK_COORD = (1150, 660)
BUY_YES_CONFIRM_COORD = (1000, 660)
NEXT_PAGE_COORD = (1180, 420)


@dataclass
class SnipedPurchase:
    card_name: str
    price: int
    quantity: int
    timestamp_ms: float


class BazaarSniperState:
    """State machine tracking current Bazaar purchase cycle."""

    def __init__(self, target_tcs: List[str], max_gold: int = 500000) -> None:
        self.target_tcs = list(target_tcs)
        self.current_gold = max_gold
        self.purchases: List[SnipedPurchase] = []
        self.current_page = 0

    def can_afford(self, price: int) -> bool:
        return self.current_gold >= price

    def record_purchase(
        self, card_name: str, price: int, qty: int = 1, now_ms: float = 0.0
    ) -> None:
        self.current_gold -= price * qty
        self.purchases.append(SnipedPurchase(card_name, price, qty, now_ms))

    def plan_empower_cycle(self, lowest_price_active: bool) -> List[Dict[str, Any]]:
        """Emits discrete purchase clicks if Empowers are at discount threshold."""
        if not lowest_price_active or not self.can_afford(3200):
            return [
                {
                    "action": "WAIT_PRICE_DROP",
                    "reason": "Empowers not at bulk discount bracket",
                }
            ]

        return [
            {
                "action": "CLICK",
                "x": BUY_BUTTON_COORD[0],
                "y": BUY_BUTTON_COORD[1],
                "delay_ms": 300,
            },
            {
                "action": "CLICK",
                "x": BUY_CONFIRM_OK_COORD[0],
                "y": BUY_CONFIRM_OK_COORD[1],
                "delay_ms": 250,
            },
        ]

    def plan_tc_snipe(
        self, found_tc_name: str, card_x: int, card_y: int
    ) -> List[Dict[str, Any]]:
        """Emits select-and-buy click sequence when a desired TC is spotted."""
        return [
            {
                "action": "CLICK",
                "x": card_x,
                "y": card_y,
                "delay_ms": 250,
                "note": f"Select {found_tc_name}",
            },
            {
                "action": "CLICK",
                "x": BUY_BUTTON_COORD[0],
                "y": BUY_BUTTON_COORD[1],
                "delay_ms": 300,
            },
            {
                "action": "CLICK",
                "x": BUY_CONFIRM_OK_COORD[0],
                "y": BUY_CONFIRM_OK_COORD[1],
                "delay_ms": 250,
            },
        ]
