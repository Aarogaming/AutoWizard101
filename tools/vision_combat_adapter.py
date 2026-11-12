"""Vision-to-Planner Combat Adapter for Wizard101.

Connects visual template recognition of cards in hand to the deterministic
combat turn planner (tools/combat_turn_planner.py).

Provides:
- Card catalog mapping template names to SpellCard definitions.
- Hand detection from screenshot frames using template matching.
- Translation of visual detections into structured combat planner input.
"""

from __future__ import annotations

from dataclasses import dataclass
import json
from pathlib import Path
import sys
from typing import Any, Dict, List, Optional, Tuple

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from combat_turn_planner import Combatant, CombatTurnPlanner, SpellCard, TurnProposal

# Known Wizard101 spell cards catalog for visual mapping
DEFAULT_SPELL_CATALOG: Dict[str, Dict[str, Any]] = {
    "meteor": {
        "name": "Meteor Strike",
        "school": "Fire",
        "spell_type": "Attack",
        "pip_cost": 4,
        "base_damage": 305.0,
        "is_aoe": True,
        "accuracy": 0.75,
    },
    "fire_cat": {
        "name": "Fire Cat",
        "school": "Fire",
        "spell_type": "Attack",
        "pip_cost": 1,
        "base_damage": 100.0,
        "is_aoe": False,
        "accuracy": 0.75,
    },
    "sunbird": {
        "name": "Sunbird",
        "school": "Fire",
        "spell_type": "Attack",
        "pip_cost": 3,
        "base_damage": 315.0,
        "is_aoe": False,
        "accuracy": 0.75,
    },
    "fireblade": {
        "name": "Fireblade",
        "school": "Fire",
        "spell_type": "Blade",
        "pip_cost": 0,
        "buff_percent": 35.0,
        "is_aoe": False,
        "accuracy": 1.0,
    },
    "fire_trap": {
        "name": "Fire Trap",
        "school": "Fire",
        "spell_type": "Trap",
        "pip_cost": 0,
        "buff_percent": 30.0,
        "is_aoe": False,
        "accuracy": 1.0,
    },
    "pixie": {
        "name": "Pixie",
        "school": "Life",
        "spell_type": "Heal",
        "pip_cost": 2,
        "heal_amount": 420.0,
        "is_aoe": False,
        "accuracy": 1.0,
    },
    "tower_shield": {
        "name": "Tower Shield",
        "school": "Ice",
        "spell_type": "Shield",
        "pip_cost": 0,
        "shield_percent": 50.0,
        "is_aoe": False,
        "accuracy": 1.0,
    },
}


@dataclass
class VisualCardMatch:
    card_id: str
    spell_card: SpellCard
    confidence: float
    bbox: Tuple[int, int, int, int]  # (x, y, w, h)
    template_path: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        return {
            "card_id": self.card_id,
            "spell_name": self.spell_card.name,
            "confidence": round(self.confidence, 4),
            "bbox": list(self.bbox),
            "card_data": self.spell_card.to_dict(),
            "template_path": self.template_path,
        }


class VisionCombatAdapter:
    """Discovers templates, parses game scenes, and bridges visual cards to turn decisions."""

    def __init__(
        self,
        resources_root: Optional[Path] = None,
        custom_catalog: Optional[Dict[str, Dict[str, Any]]] = None,
    ):
        self.resources_root = resources_root or (
            ROOT / "ProjectMaelstrom/ProjectMaelstrom/Resources"
        )
        self.catalog = dict(custom_catalog or DEFAULT_SPELL_CATALOG)

    def get_template_dirs(self, resolution: str = "1024x768") -> Dict[str, List[Path]]:
        """Finds all card template images available for the specified resolution."""
        cards_dir = self.resources_root / resolution / "Combat/Cards"
        results: Dict[str, List[Path]] = {}
        if not cards_dir.exists():
            return results

        for card_folder in cards_dir.iterdir():
            if card_folder.is_dir():
                card_name = card_folder.name.lower()
                pngs = sorted(card_folder.glob("*.png"))
                if pngs:
                    results[card_name] = pngs
        return results

    def resolve_card_metadata(self, card_name: str) -> SpellCard:
        """Resolves template directory name to a full SpellCard object."""
        normalized = card_name.lower().replace("-", "_").replace(" ", "_")
        if normalized in self.catalog:
            return SpellCard.from_dict(self.catalog[normalized])

        # Fallback default attack card if unknown
        return SpellCard(
            name=card_name.title(),
            school="Balance",
            spell_type="Attack",
            pip_cost=1,
            base_damage=100.0,
        )

    def parse_hand_from_detections(
        self, detections: List[Dict[str, Any]]
    ) -> List[VisualCardMatch]:
        """Converts raw detector outputs into typed VisualCardMatch instances."""
        matches = []
        for det in detections:
            card_id = str(det.get("card_id", "unknown")).lower()
            confidence = float(det.get("confidence", 1.0))
            bbox = tuple(det.get("bbox", (0, 0, 50, 80)))
            spell_card = self.resolve_card_metadata(card_id)
            matches.append(
                VisualCardMatch(
                    card_id=card_id,
                    spell_card=spell_card,
                    confidence=confidence,
                    bbox=bbox,  # type: ignore
                    template_path=det.get("template_path"),
                )
            )
        # Sort cards left to right by X coordinate
        matches.sort(key=lambda m: m.bbox[0])
        return matches

    def build_turn_planner_payload(
        self,
        hand_matches: List[VisualCardMatch],
        player: Combatant,
        enemies: List[Combatant],
        emergency_heal_threshold: float = 0.35,
    ) -> Dict[str, Any]:
        """Constructs the full JSON payload required by tools/combat_turn_planner.py."""
        return {
            "player": player.to_dict(),
            "enemies": [e.to_dict() for e in enemies],
            "hand": [m.spell_card.to_dict() for m in hand_matches],
            "visual_hand_metadata": [m.to_dict() for m in hand_matches],
            "emergency_heal_threshold": emergency_heal_threshold,
        }

    def evaluate_turn_from_visuals(
        self,
        hand_matches: List[VisualCardMatch],
        player: Combatant,
        enemies: List[Combatant],
        emergency_heal_threshold: float = 0.35,
    ) -> TurnProposal:
        """Directly plans the best action from visually detected hand cards."""
        planner = CombatTurnPlanner(emergency_heal_threshold=emergency_heal_threshold)
        hand_cards = [m.spell_card for m in hand_matches]
        proposal = planner.plan_turn(player, enemies, hand_cards)

        # Attach visual screen bounding box if a card action was chosen
        if proposal.action == "CAST_SPELL" and proposal.card_index is not None:
            if 0 <= proposal.card_index < len(hand_matches):
                chosen_match = hand_matches[proposal.card_index]
                proposal.rationale += f" [Visual Target Box: {chosen_match.bbox}]"
        return proposal
