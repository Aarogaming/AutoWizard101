"""Precomputed Combat Decision & Damage Matrix for Wizard101.

Precomputes optimal, deterministic card choices across standard combat state spaces:
- Effective Pips (1 to 14)
- Player Health Ratio (<0.35 emergency heal, >=0.35 normal)
- Target HP brackets (100 to 5,000 HP)
- Player Damage Boost (0% to 150%)
- Blade counts (0, 1, 2)

Enables O(1) instantaneous turn planning without runtime search or external dependencies.
"""

from __future__ import annotations

from dataclasses import asdict, dataclass
import json
from pathlib import Path
import sys
from typing import Any, Dict, List, Optional, Tuple

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from combat_turn_planner import (
    Combatant,
    CombatTurnPlanner,
    SpellCard,
    TurnProposal,
)

# Standard Spell Archetype Pool (Fire & Universal baseline)
STANDARD_SPELL_POOL: List[SpellCard] = [
    SpellCard("Fire Blade", "Fire", "Blade", pip_cost=0, buff_percent=35.0),
    SpellCard("Fire Trap", "Fire", "Trap", pip_cost=0, buff_percent=25.0),
    SpellCard("Pixie", "Life", "Heal", pip_cost=2, heal_amount=400.0),
    SpellCard("Fire Cat", "Fire", "Attack", pip_cost=1, base_damage=105.0),
    SpellCard("Sunbird", "Fire", "Attack", pip_cost=3, base_damage=310.0),
    SpellCard(
        "Meteor Strike", "Fire", "Attack", pip_cost=4, base_damage=325.0, is_aoe=True
    ),
    SpellCard("Hephaestus", "Fire", "Attack", pip_cost=5, base_damage=450.0),
    SpellCard(
        "Fire Dragon", "Fire", "Attack", pip_cost=7, base_damage=750.0, is_aoe=True
    ),
]


def precompute_matrix(
    pip_range: List[int] = [1, 2, 3, 4, 5, 7, 10],
    hp_ratios: List[float] = [0.25, 0.50, 0.80, 1.0],
    enemy_hp_tiers: List[int] = [200, 450, 800, 1500, 3000],
    gear_boosts: List[float] = [0.0, 30.0, 60.0, 100.0],
    blade_counts: List[int] = [0, 1],
) -> Dict[str, Any]:
    """Generates the full precomputed combat matrix."""
    matrix: Dict[str, Any] = {}
    total_entries = 0

    for pips in pip_range:
        for hp_ratio in hp_ratios:
            for e_hp in enemy_hp_tiers:
                for boost in gear_boosts:
                    for blades in blade_counts:
                        key = f"pips_{pips}|hp_{hp_ratio:.2f}|ehp_{e_hp}|boost_{int(boost)}|blades_{blades}"

                        player = Combatant(
                            id="p1",
                            name="Wizard",
                            current_hp=int(2000 * hp_ratio),
                            max_hp=2000,
                            pips=pips,
                            power_pips=0,
                            school="Fire",
                            gear_damage_boost=boost,
                            active_blades=[35.0] * blades,
                        )

                        enemy = Combatant(
                            id="e1",
                            name="Enemy Mob",
                            current_hp=e_hp,
                            max_hp=e_hp,
                            school="Ice",
                        )

                        planner = CombatTurnPlanner(emergency_heal_threshold=0.35)
                        decision = planner.plan_turn(
                            player=player,
                            enemies=[enemy],
                            hand=STANDARD_SPELL_POOL,
                        )

                        matrix[key] = {
                            "action": decision.action,
                            "card_name": decision.card_name,
                            "target_name": decision.target_name,
                            "estimated_damage": decision.expected_damage,
                            "expected_heal": decision.expected_heal,
                            "rule_triggered": decision.rule_triggered,
                            "reason": decision.rationale,
                        }
                        total_entries += 1

    return {
        "schema": "maelstrom.combat-matrix.v1",
        "total_entries": total_entries,
        "parameters": {
            "pips": pip_range,
            "hp_ratios": hp_ratios,
            "enemy_hp_tiers": enemy_hp_tiers,
            "gear_boosts": gear_boosts,
            "blade_counts": blade_counts,
        },
        "matrix": matrix,
    }


class CombatMatrixLookup:
    """Fast in-memory lookup client for precomputed combat decisions."""

    def __init__(self, matrix_data: Dict[str, Any]) -> None:
        self.matrix = matrix_data.get("matrix", {})
        self.params = matrix_data.get("parameters", {})

    @classmethod
    def load(cls, path: Path) -> CombatMatrixLookup:
        data = json.loads(path.read_text(encoding="utf-8"))
        return cls(data)

    def lookup(
        self,
        pips: int,
        hp_ratio: float,
        enemy_hp: int,
        gear_boost: float,
        blades: int = 0,
    ) -> Optional[Dict[str, Any]]:
        """Finds closest matching precomputed combat decision in O(1)."""

        # Snap to nearest discrete parameters
        def nearest(val: float, choices: List[Any]) -> Any:
            return min(choices, key=lambda x: abs(x - val))

        pips_c = nearest(pips, self.params.get("pips", [1, 2, 3, 4, 5, 7, 10]))
        hp_c = nearest(hp_ratio, self.params.get("hp_ratios", [0.25, 0.50, 0.80, 1.0]))
        ehp_c = nearest(
            enemy_hp, self.params.get("enemy_hp_tiers", [200, 450, 800, 1500, 3000])
        )
        boost_c = nearest(
            gear_boost, self.params.get("gear_boosts", [0.0, 30.0, 60.0, 100.0])
        )
        blades_c = min(1, max(0, blades))

        key = f"pips_{pips_c}|hp_{hp_c:.2f}|ehp_{ehp_c}|boost_{int(boost_c)}|blades_{blades_c}"
        return self.matrix.get(key)


def export_combat_matrix(dest_path: Path) -> Path:
    dest_path.parent.mkdir(parents=True, exist_ok=True)
    matrix = precompute_matrix()
    dest_path.write_text(json.dumps(matrix, indent=2), encoding="utf-8")
    return dest_path


if __name__ == "__main__":
    out = ROOT / "tools/fixtures/combat/combat_lookup_matrix.json"
    export_combat_matrix(out)
    print(f"Exported precomputed combat matrix to {out}")
