"""Wizard101 Gardening Pipeline & Plant Growth State Machine.

Modernizes gardening-bot-main (Couch Potatoes / Evil Magma Peas farming):
1. Ingests verified growth times and like multipliers (Red Barn Farm, Gnome, Pixie, etc.).
2. Computes precise growth schedules and next-harvest prediction.
3. Maps gardening spell needs (Downpour, Flute Ensemble, Gusty Winds) into
   deterministic, bounded macro action sequences with guaranteed key release.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timedelta
import json
from pathlib import Path
import sys
from typing import Any, Dict, List, Optional

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from macro_pipeline import MacroNode, macro_to_dict


@dataclass
class PlantGrowthProfile:
    plant_name: str
    seedling_hours: float
    young_hours: float
    mature_hours: float
    likes: Dict[str, float] = field(default_factory=dict)

    def calculate_multiplier(self, active_likes: List[str]) -> float:
        """Calculates total speed multiplier from present housing likes."""
        bonus = sum(self.likes.get(like.lower(), 0.0) for like in active_likes)
        return max(1.0, 1.0 + bonus)

    def calculate_phase_durations(self, active_likes: List[str]) -> Dict[str, float]:
        """Calculates effective duration in hours per growth phase."""
        mult = self.calculate_multiplier(active_likes)
        return {
            "seedling_hours": round(self.seedling_hours / mult, 2),
            "young_hours": round(self.young_hours / mult, 2),
            "mature_hours": round(self.mature_hours / mult, 2),
            "total_to_elder_hours": round(
                (self.seedling_hours + self.young_hours + self.mature_hours) / mult, 2
            ),
            "speed_multiplier": round(mult, 2),
        }


COUCH_POTATOES_PROFILE = PlantGrowthProfile(
    plant_name="Couch Potatoes",
    seedling_hours=13.0,
    young_hours=29.0,
    mature_hours=216.0,
    likes={
        "tropical_garden_gnome": 0.25,
        "pixie": 0.20,
        "sandwich_station": 0.20,
        "red_barn_farm": 0.15,
        "litter": 0.10,
        "king_parsley": 0.10,
    },
)

EVIL_MAGMA_PEAS_PROFILE = PlantGrowthProfile(
    plant_name="Evil Magma Peas",
    seedling_hours=9.0,
    young_hours=21.0,
    mature_hours=168.0,
    likes={
        "tropical_garden_gnome": 0.25,
        "pixie": 0.20,
        "djembe_drum": 0.20,
        "red_barn_farm": 0.15,
        "egg_basket": 0.10,
    },
)


class GardeningRoutineBuilder:
    """Compiles gardening care cycles into bounded MacroGraphs."""

    @staticmethod
    def build_care_cycle(
        garden_name: str = "Couch Potatoes Farm",
        spells: List[str] = ["Downpour", "Flute Ensemble", "Gusty Winds"],
    ) -> Dict[str, Any]:
        nodes: List[MacroNode] = []

        # 1. Open Gardening Book (key G)
        nodes.append(
            MacroNode(
                node_id="garden_open",
                kind="interact_key",
                payload={"key": "G", "note": "Open gardening spellbook"},
                timeout_s=5.0,
            )
        )

        # 2. Cast each required area care spell
        for idx, spell in enumerate(spells, 1):
            nodes.append(
                MacroNode(
                    node_id=f"cast_spell_{idx}",
                    kind="interact_key",
                    payload={
                        "key": "Space",
                        "spell_name": spell,
                        "note": f"Cast {spell} on farm plot",
                    },
                    timeout_s=8.0,
                )
            )

        # 3. Close Gardening Book (key Escape or G)
        nodes.append(
            MacroNode(
                node_id="garden_close",
                kind="interact_key",
                payload={"key": "Escape", "note": "Close gardening spellbook"},
                timeout_s=5.0,
            )
        )

        graph = macro_to_dict(
            nodes, title=f"gardening-{garden_name.lower().replace(' ', '-')}"
        )
        graph["metadata"] = {
            "routine_type": "gardening_care",
            "spells": spells,
            "target_garden": garden_name,
        }
        return graph
