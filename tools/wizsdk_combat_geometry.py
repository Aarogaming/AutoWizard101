"""
WizSDK Spell Enchantment Geometry and Card Management Engine.
Ingests and modernizes the exact combat card coordinates and enchant geometry
from wizSDK-master (wizsdk/card.py & wizsdk/battle.py).
Provides non-invasive, window-relative coordinate calculation for card inspection,
enchantment clicks, and team/enemy targeting without stealing the desktop mouse.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import List, Optional, Tuple


@dataclass
class CombatCardGeometry:
    name: str
    spell_x: int
    spell_y: int = 325
    card_width: int = 52


class WizSDKCombatGeometry:
    """
    Computes exact relative mouse click coordinates for combat card handling,
    spell enchantments, and enemy/ally targeting according to the wizSDK specification.
    """

    # Hand Spell Area: (x: 245, y: 290, width: 370, height: 70)
    SPELL_AREA = (245, 290, 370, 70)

    # Pass button coordinates
    PASS_BUTTON = (254, 398)

    # Flee button verification pixels
    FLEE_YELLOW = ((546, 398), (255, 255, 0))
    FLEE_BROWN = ((578, 394), (124, 68, 0))

    # Health & Mana globe monitor pixels (from wizAPI / wizSDK)
    HEALTH_GLOBE = (23, 563)
    MANA_GLOBE = (79, 591)

    @classmethod
    def calculate_enchant_geometry(
        cls, enchant_card: CombatCardGeometry, spell_card: CombatCardGeometry
    ) -> Tuple[Tuple[int, int], Tuple[int, int], CombatCardGeometry]:
        """
        Calculates the two click coordinates required to enchant a spell,
        and computes the resulting combined card's new position.

        Exact rule from wizSDK card.py:
        enchant_is_before = enchant.spell_x < spell.spell_x
        new_pos = spell.spell_x + (card_width / 2)
        if enchant_is_before:
            new_pos -= card_width
        """
        click_1 = (enchant_card.spell_x, enchant_card.spell_y)
        click_2 = (spell_card.spell_x, spell_card.spell_y)

        enchant_is_before = enchant_card.spell_x < spell_card.spell_x
        new_x = spell_card.spell_x + (spell_card.card_width // 2)
        if enchant_is_before:
            new_x -= spell_card.card_width

        enchanted_card = CombatCardGeometry(
            name=f"{enchant_card.name}-{spell_card.name}",
            spell_x=new_x,
            spell_y=spell_card.spell_y,
            card_width=spell_card.card_width,
        )
        return click_1, click_2, enchanted_card

    @classmethod
    def calculate_target_coordinates(cls, target_index: int) -> Tuple[int, int]:
        """
        Calculates window-relative click coordinates for target selection.
        Targets 0-3 are enemies (top row).
        Targets 4-7 are allies (bottom row).

        Exact formula from wizSDK card.py:
        if target < 4:
            x = (174 * target) + 130
            y = 50
        elif target < 8:
            x = (174 * (7 - target)) + 160
            y = 590
        """
        if not (0 <= target_index <= 7):
            raise ValueError(
                f"Target index must be between 0 and 7, got {target_index}"
            )

        if target_index < 4:
            x = (174 * target_index) + 130
            y = 50
        else:
            x = (174 * (7 - target_index)) + 160
            y = 590
        return (x, y)

    @classmethod
    def get_enemy_pedestal_coordinates(cls, enemy_index: int) -> Tuple[int, int]:
        """
        Returns pedestal circle check coordinates for active enemies (0 to 3).
        Formula from wizSDK battle.py:
        X = (174 * enemy_index) + 203, Y = 75
        """
        if not (0 <= enemy_index < 4):
            raise ValueError(f"Enemy index must be between 0 and 3, got {enemy_index}")
        return ((174 * enemy_index) + 203, 75)
