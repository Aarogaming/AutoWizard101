"""
Pet Stat and Talent Evaluation Engine.
Modernizes the exact formula and stat definitions from petStatCalc.js in Wizard101-Utilities-master.
Calculates talent stat values (Damage, Resist, Health, Critical) as a function of Strength, Intellect, Agility, Will, and Power.
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Dict, List, Optional, Tuple


@dataclass
class PetAttributes:
    strength: float = 255.0
    intellect: float = 250.0
    agility: float = 260.0
    will: float = 260.0
    power: float = 250.0

    def get_stat(self, name: str) -> float:
        mapping = {
            "Str": self.strength,
            "Strength": self.strength,
            "Int": self.intellect,
            "Intellect": self.intellect,
            "Agi": self.agility,
            "Agility": self.agility,
            "Will": self.will,
            "Pow": self.power,
            "Power": self.power,
        }
        return mapping.get(name, 0.0)


@dataclass
class PetTalentDefinition:
    name: str
    talent_type: (
        str  # e.g., "Damage", "Resist", "Critical", "Pierce", "Health", "Maycast"
    )
    talent_school: str  # e.g., "Universal", "Fire", "Storm", etc.
    stat_1: str  # e.g., "Str"
    stat_2: str  # e.g., "Will"
    modifier: float  # Multiplier factor, e.g. 3/400 for Dealer, 3/800 for Giver


class PetTalentCalculator:
    """
    Evaluates pet talent contribution according to Wizard101 formula:
    amount = (2 * (stat_1 + stat_2) + Power) * modifier
    """

    # Standard verified talents from Wizard101
    STANDARD_TALENTS: Dict[str, PetTalentDefinition] = {
        "Spell-Proof": PetTalentDefinition(
            name="Spell-Proof",
            talent_type="Resist",
            talent_school="Universal",
            stat_1="Str",
            stat_2="Agi",
            modifier=2.0 / 250.0,  # Max 10% resist
        ),
        "Spell-Defying": PetTalentDefinition(
            name="Spell-Defying",
            talent_type="Resist",
            talent_school="Universal",
            stat_1="Str",
            stat_2="Agi",
            modifier=1.0 / 250.0,  # Max 5% resist
        ),
        "Fire-Dealer": PetTalentDefinition(
            name="Fire-Dealer",
            talent_type="Damage",
            talent_school="Fire",
            stat_1="Str",
            stat_2="Will",
            modifier=3.0 / 400.0,  # Max 10% damage
        ),
        "Fire-Giver": PetTalentDefinition(
            name="Fire-Giver",
            talent_type="Damage",
            talent_school="Fire",
            stat_1="Str",
            stat_2="Will",
            modifier=3.0 / 666.6667,  # Max 6% damage
        ),
        "Pain-Giver": PetTalentDefinition(
            name="Pain-Giver",
            talent_type="Damage",
            talent_school="Universal",
            stat_1="Str",
            stat_2="Will",
            modifier=3.0 / 666.6667,  # Max 6% universal damage
        ),
        "Storm-Dealer": PetTalentDefinition(
            name="Storm-Dealer",
            talent_type="Damage",
            talent_school="Storm",
            stat_1="Str",
            stat_2="Will",
            modifier=3.0 / 400.0,  # Max 10% damage
        ),
        "Storm-Giver": PetTalentDefinition(
            name="Storm-Giver",
            talent_type="Damage",
            talent_school="Storm",
            stat_1="Str",
            stat_2="Will",
            modifier=3.0 / 666.6667,  # Max 6% damage
        ),
        "Mighty": PetTalentDefinition(
            name="Mighty",
            talent_type="Selfish",
            talent_school="Universal",
            stat_1="Str",
            stat_2="Str",
            modifier=0.0,  # +65 Max Strength
        ),
    }

    @classmethod
    def calculate_talent_value(
        cls, talent: PetTalentDefinition, stats: PetAttributes
    ) -> float:
        """
        Exact math from Wizard101-Utilities petStatCalc.js:
        (2 * (stat1 + stat2) + Power) * modifier
        """
        if talent.modifier == 0.0:
            return 0.0

        val1 = stats.get_stat(talent.stat_1)
        val2 = stats.get_stat(talent.stat_2)
        power = stats.get_stat("Power")

        raw = (2.0 * (val1 + val2) + power) * talent.modifier
        return round(raw, 2)

    @classmethod
    def calculate_pool_summary(
        cls, talent_names: List[str], stats: PetAttributes
    ) -> Dict[str, Dict[str, float]]:
        """
        Aggregates stats from a pet's manifest.
        Returns mapping of { talent_type: { school: total_amount } }
        """
        summary: Dict[str, Dict[str, float]] = {}

        for name in talent_names:
            talent = cls.STANDARD_TALENTS.get(name)
            if not talent or talent.modifier == 0.0:
                continue

            val = cls.calculate_talent_value(talent, stats)
            type_dict = summary.setdefault(talent.talent_type, {})
            type_dict[talent.talent_school] = (
                type_dict.get(talent.talent_school, 0.0) + val
            )

        return summary
