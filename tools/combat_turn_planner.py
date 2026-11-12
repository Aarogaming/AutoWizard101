"""Wizard101 Combat Turn Decision Planner.

Evaluates combat state (player resources, hand, enemy vulnerabilities and threat)
to propose deterministic turn actions for a deliberately limited rules model:
1. Emergency healing when player health is critically low.
2. Lethal elimination (single-target or AoE) taking advantage of damage math,
   boosts, resists, and pierce.
3. Buff setup (blades / traps) when non-lethal to build damage multiplier.
4. Pip conservation / passing when saving for high-rank spells.
"""

from __future__ import annotations

from dataclasses import dataclass, field, fields
from copy import deepcopy
import json
import math
from pathlib import Path
import sys
from typing import Any, Dict, List, Optional


@dataclass
class SpellCard:
    name: str
    school: str
    spell_type: str  # "Attack", "Blade", "Trap", "Shield", "Heal", "Utility"
    pip_cost: int
    base_damage: float = 0.0
    flat_enchant: float = 0.0
    heal_amount: float = 0.0
    buff_percent: float = 0.0
    shield_percent: float = 0.0
    accuracy: float = 0.85
    is_aoe: bool = False

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> SpellCard:
        result = _construct(cls, data, "card")
        _validate_card(result)
        return deepcopy(result)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "name": self.name,
            "school": self.school,
            "spell_type": self.spell_type,
            "pip_cost": self.pip_cost,
            "base_damage": self.base_damage,
            "flat_enchant": self.flat_enchant,
            "heal_amount": self.heal_amount,
            "buff_percent": self.buff_percent,
            "shield_percent": self.shield_percent,
            "accuracy": self.accuracy,
            "is_aoe": self.is_aoe,
        }


@dataclass
class Combatant:
    """Synthetic/effective state; effect arrays must already be in application order."""

    id: str
    name: str
    current_hp: int
    max_hp: int
    pips: int = 0
    power_pips: int = 0
    school: str = "Balance"
    gear_damage_boost: float = 0.0
    pierce: float = 0.0
    active_blades: List[float] = field(default_factory=list)
    active_traps: List[float] = field(default_factory=list)
    active_shields: List[float] = field(default_factory=list)
    resist: float = 0.0
    boost: float = 0.0
    is_boss: bool = False
    is_player: bool = False

    @property
    def is_alive(self) -> bool:
        return self.current_hp > 0

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> Combatant:
        result = _construct(cls, data, "combatant")
        _validate_combatant(result)
        return deepcopy(result)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "id": self.id,
            "name": self.name,
            "current_hp": self.current_hp,
            "max_hp": self.max_hp,
            "pips": self.pips,
            "power_pips": self.power_pips,
            "school": self.school,
            "gear_damage_boost": self.gear_damage_boost,
            "pierce": self.pierce,
            "active_blades": list(self.active_blades),
            "active_traps": list(self.active_traps),
            "active_shields": list(self.active_shields),
            "resist": self.resist,
            "boost": self.boost,
            "is_boss": self.is_boss,
            "is_player": self.is_player,
        }


_SCHOOLS = frozenset({"fire", "ice", "storm", "myth", "life", "death", "balance"})
_SPELL_TYPES = frozenset({"Attack", "Blade", "Trap", "Shield", "Heal", "Utility"})
_EFFECT_FIELDS = {
    "Attack": frozenset({"base_damage", "flat_enchant"}),
    "Heal": frozenset({"heal_amount"}),
    "Blade": frozenset({"buff_percent"}),
    "Trap": frozenset({"buff_percent"}),
    "Shield": frozenset({"shield_percent"}),
    "Utility": frozenset(),
}


def _text(value: Any, name: str) -> None:
    if not isinstance(value, str) or not value.strip() or len(value) > 256:
        raise ValueError(f"{name} must be nonempty text of at most 256 characters.")


def _number(value: Any, name: str, minimum: float, maximum: float) -> None:
    if (
        type(value) not in (int, float)
        or not minimum <= value <= maximum
        or not math.isfinite(value)
    ):
        raise ValueError(f"{name} must be a finite number in [{minimum}, {maximum}].")


def _integer(value: Any, name: str, minimum: int, maximum: int) -> None:
    if type(value) is not int or not minimum <= value <= maximum:
        raise ValueError(f"{name} must be an integer in [{minimum}, {maximum}].")


def _school(value: Any, name: str) -> None:
    _text(value, name)
    if value.casefold() not in _SCHOOLS:
        raise ValueError(f"{name} is unsupported by this planner.")


def _construct(cls, data: Dict[str, Any], name: str):
    if not isinstance(data, dict):
        raise ValueError(f"{name} must be an object.")
    if any(not isinstance(key, str) for key in data):
        raise ValueError(f"{name} fields must have text names.")
    unknown = set(data) - {f.name for f in fields(cls)}
    if unknown:
        raise ValueError(f"Unsupported {name} fields: {', '.join(sorted(unknown))}.")
    try:
        return cls(**data)
    except TypeError as exc:
        raise ValueError(f"Missing required {name} fields.") from exc


def _validate_combatant(value: Combatant) -> None:
    if not isinstance(value, Combatant):
        raise ValueError("Expected a Combatant.")
    _text(value.id, "combatant.id")
    _text(value.name, "combatant.name")
    _school(value.school, "combatant.school")
    _integer(value.max_hp, "max_hp", 1, 100_000_000)
    _integer(value.current_hp, "current_hp", 0, value.max_hp)
    # Broad synthetic bounds; this is not validation of a live seven-slot pip layout.
    _integer(value.pips, "pips", 0, 14)
    _integer(value.power_pips, "power_pips", 0, 7)
    for name in ("is_boss", "is_player"):
        if type(getattr(value, name)) is not bool:
            raise ValueError(f"{name} must be a boolean.")
    for name in ("pierce", "resist", "boost"):
        _number(getattr(value, name), name, 0, 1000)
    _number(value.gear_damage_boost, "gear_damage_boost", -100, 1000)
    for name in ("active_blades", "active_traps", "active_shields"):
        effects = getattr(value, name)
        if not isinstance(effects, list) or len(effects) > 64:
            raise ValueError(f"{name} must be a list of at most 64 effects.")
        for effect in effects:
            _number(
                effect,
                name,
                0 if name == "active_shields" else -100,
                100 if name == "active_shields" else 1000,
            )


def _validate_card(card: SpellCard) -> None:
    if not isinstance(card, SpellCard):
        raise ValueError("Expected a SpellCard.")
    _text(card.name, "card.name")
    _school(card.school, "card.school")
    _text(card.spell_type, "spell_type")
    if card.spell_type not in _SPELL_TYPES:
        raise ValueError("Unsupported spell_type.")
    _integer(card.pip_cost, "pip_cost", 0, 14)
    _number(card.accuracy, "accuracy", 0, 1)
    if type(card.is_aoe) is not bool:
        raise ValueError("is_aoe must be a boolean.")
    if card.is_aoe and card.spell_type != "Attack":
        raise ValueError("Only enemy-targeted attack AoE is supported.")
    for name in ("base_damage", "flat_enchant", "heal_amount"):
        _number(getattr(card, name), name, 0, 100_000_000)
    _number(card.buff_percent, "buff_percent", 0, 1000)
    _number(card.shield_percent, "shield_percent", 0, 100)
    for name in (
        "base_damage",
        "flat_enchant",
        "heal_amount",
        "buff_percent",
        "shield_percent",
    ):
        if name not in _EFFECT_FIELDS[card.spell_type] and getattr(card, name) != 0:
            raise ValueError(f"Unsupported mixed {card.spell_type} effects: {name}.")


def calculate_effective_pips(player: Combatant, card: SpellCard) -> int:
    """Base-school power pips only; mastery and school/shadow pips are unsupported."""
    _validate_combatant(player)
    _validate_card(card)
    is_school_match = card.school.casefold() == player.school.casefold()
    if is_school_match:
        return player.pips + (player.power_pips * 2)
    return player.pips + player.power_pips


def can_afford_spell(player: Combatant, card: SpellCard) -> bool:
    """Returns True if player has enough pips to cast the card."""
    return calculate_effective_pips(player, card) >= card.pip_cost


def evaluate_spell_damage(
    attacker: Combatant,
    card: SpellCard,
    target: Combatant,
) -> int:
    """Estimate one flat hit using the supplied effect application order.

    Shields consume pierce sequentially, so their ordering can change the result.
    This is neither a universal equivalent of other calculators nor a qualified
    statement of current-client ordering. The observation adapter owns that fact.
    """
    _validate_combatant(attacker)
    _validate_combatant(target)
    _validate_card(card)
    if card.spell_type != "Attack" or (card.base_damage + card.flat_enchant) <= 0:
        return 0

    damage = card.base_damage + card.flat_enchant

    # Pierce vs Shields first, then Resist
    remaining_pierce = attacker.pierce

    # Apply shields with pierce reduction
    for shield in target.active_shields:
        if shield <= 0:
            continue
        effective_shield = shield
        if remaining_pierce > 0:
            if remaining_pierce >= effective_shield:
                remaining_pierce -= effective_shield
                continue
            else:
                effective_shield -= remaining_pierce
                remaining_pierce = 0.0
        damage *= 1.0 - effective_shield / 100.0

    # Apply resist with remaining pierce
    if target.resist > 0:
        net_resist = max(0.0, target.resist - remaining_pierce)
        if net_resist > 0:
            damage *= max(0.0, 1.0 - net_resist / 100.0)

    # Apply attacker blades
    for blade in attacker.active_blades:
        damage *= 1.0 + blade / 100.0

    # Apply target traps
    for trap in target.active_traps:
        damage *= 1.0 + trap / 100.0

    # Apply attacker gear damage percentage and target natural boost
    stat_multiplier = 1.0
    if attacker.gear_damage_boost != 0:
        stat_multiplier *= 1.0 + attacker.gear_damage_boost / 100.0
    if target.boost != 0:
        stat_multiplier *= 1.0 + target.boost / 100.0

    damage *= stat_multiplier

    return max(0, int(round(damage)))


@dataclass
class TurnProposal:
    action: str  # "CAST_SPELL" or "PASS"
    card_index: Optional[int] = None
    card_name: Optional[str] = None
    target_index: Optional[int] = None
    target_name: Optional[str] = None
    expected_damage: int = 0
    expected_heal: int = 0
    is_lethal: bool = False
    rule_triggered: str = "PASS_DEFAULT"
    rationale: str = ""
    alternate_proposals: List[Dict[str, Any]] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "action": self.action,
            "card_index": self.card_index,
            "card_name": self.card_name,
            "target_index": self.target_index,
            "target_name": self.target_name,
            "expected_damage": self.expected_damage,
            "expected_heal": self.expected_heal,
            "is_lethal": self.is_lethal,
            "rule_triggered": self.rule_triggered,
            "rationale": self.rationale,
            "alternate_proposals": deepcopy(self.alternate_proposals),
        }


class CombatTurnPlanner:
    """Plans combat turn decisions deterministically."""

    def __init__(self, emergency_heal_threshold: float = 0.35):
        _number(emergency_heal_threshold, "emergency_heal_threshold", 0, 1)
        self.emergency_heal_threshold = emergency_heal_threshold

    def plan_turn(
        self,
        player: Combatant,
        enemies: List[Combatant],
        hand: List[SpellCard],
    ) -> TurnProposal:
        """Selects the best action for the current combat turn."""
        _validate_combatant(player)
        if not player.is_alive:
            raise ValueError("A defeated player cannot select a combat action.")
        if not isinstance(enemies, list) or len(enemies) > 8:
            raise ValueError("enemies must be a list of at most 8 combatants.")
        if not isinstance(hand, list) or len(hand) > 64:
            raise ValueError("hand must be a list of at most 64 cards.")
        identities = {player.id}
        for enemy in enemies:
            _validate_combatant(enemy)
            if enemy.id in identities:
                raise ValueError("Combatant identities must be unique.")
            identities.add(enemy.id)
        for card in hand:
            _validate_card(card)

        def playable(card: SpellCard) -> bool:
            return card.accuracy > 0 and can_afford_spell(player, card)

        living_enemies = [e for e in enemies if e.is_alive]
        if not living_enemies:
            return TurnProposal(
                action="PASS",
                rule_triggered="NO_LIVING_ENEMIES",
                rationale="No living enemies in supplied data; this does not establish battle victory.",
            )

        # -------------------------------------------------------------
        # Rule 1: Emergency Heal
        # -------------------------------------------------------------
        hp_ratio = player.current_hp / max(1, player.max_hp)
        if hp_ratio <= self.emergency_heal_threshold:
            heal_cards = [
                (idx, card)
                for idx, card in enumerate(hand)
                if card.spell_type == "Heal" and card.heal_amount > 0 and playable(card)
            ]
            if heal_cards:
                # Pick the heal that gives the most HP
                best_heal_idx, best_heal_card = max(
                    heal_cards, key=lambda pair: pair[1].heal_amount
                )
                return TurnProposal(
                    action="CAST_SPELL",
                    card_index=best_heal_idx,
                    card_name=best_heal_card.name,
                    target_index=None,
                    target_name=player.name,
                    expected_heal=min(
                        player.max_hp - player.current_hp,
                        int(best_heal_card.heal_amount),
                    ),
                    rule_triggered="EMERGENCY_HEAL",
                    rationale=f"Player HP critical ({player.current_hp}/{player.max_hp}, {hp_ratio:.1%}). Casting {best_heal_card.name} to recover.",
                )

        # -------------------------------------------------------------
        # Rule 2: Lethal Attacks (AoE and Single-Target)
        # -------------------------------------------------------------
        attack_options = []
        for c_idx, card in enumerate(hand):
            if card.spell_type != "Attack" or not playable(card):
                continue

            if card.is_aoe:
                kills = 0
                total_dmg = 0
                damage_by_target = []
                for e_idx, enemy in enumerate(enemies):
                    if not enemy.is_alive:
                        continue
                    dmg = evaluate_spell_damage(player, card, enemy)
                    total_dmg += dmg
                    if dmg >= enemy.current_hp:
                        kills += 1
                    damage_by_target.append((e_idx, enemy, dmg))

                if total_dmg <= 0:
                    continue
                attack_options.append(
                    {
                        "card_index": c_idx,
                        "card": card,
                        "target_index": None,
                        "target": None,
                        "is_aoe": True,
                        "kills": kills,
                        "total_damage": total_dmg,
                        "is_lethal": kills > 0,
                        "pip_cost": card.pip_cost,
                        "damage_by_target": damage_by_target,
                    }
                )
            else:
                for e_idx, enemy in enumerate(enemies):
                    if not enemy.is_alive:
                        continue
                    dmg = evaluate_spell_damage(player, card, enemy)
                    if dmg <= 0:
                        continue
                    is_lethal = dmg >= enemy.current_hp
                    attack_options.append(
                        {
                            "card_index": c_idx,
                            "card": card,
                            "target_index": e_idx,
                            "target": enemy,
                            "is_aoe": False,
                            "kills": 1 if is_lethal else 0,
                            "total_damage": dmg,
                            "is_lethal": is_lethal,
                            "pip_cost": card.pip_cost,
                        }
                    )

        # Check for lethal wipe or high-value lethal attack
        lethal_options = [opt for opt in attack_options if opt["is_lethal"]]
        if lethal_options:
            # Sort lethal options:
            # 1. Most kills (e.g. AoE wiping all or multiple enemies)
            # 2. Target priority: Boss target first
            # 3. Pip efficiency (lower pip cost wins for same kill count)
            # 4. Highest total damage
            def lethal_sort_key(opt: Dict[str, Any]):
                is_boss_kill = (
                    not opt["is_aoe"]
                    and opt["target"] is not None
                    and opt["target"].is_boss
                )
                return (
                    opt["kills"],
                    1 if is_boss_kill else 0,
                    -opt["pip_cost"],
                    opt["total_damage"],
                )

            best_lethal = max(lethal_options, key=lethal_sort_key)
            target_name = (
                "All Enemies" if best_lethal["is_aoe"] else best_lethal["target"].name
            )
            return TurnProposal(
                action="CAST_SPELL",
                card_index=best_lethal["card_index"],
                card_name=best_lethal["card"].name,
                target_index=best_lethal["target_index"],
                target_name=target_name,
                expected_damage=best_lethal["total_damage"],
                is_lethal=True,
                rule_triggered="LETHAL_ATTACK",
                rationale=(
                    f"Conditional lethal estimate: if {best_lethal['card'].name} lands, "
                    f"{best_lethal['kills']} enemy(ies) may fall from {best_lethal['total_damage']} estimated damage."
                ),
            )

        # -------------------------------------------------------------
        # Rule 3: Buff Setup (Blades and Traps)
        # -------------------------------------------------------------
        # Check affordable blade cards
        blade_cards = [
            (idx, card)
            for idx, card in enumerate(hand)
            if card.spell_type == "Blade"
            and playable(card)
            and card.buff_percent > 0
            and card.buff_percent not in player.active_blades
            and card.school.casefold() == player.school.casefold()
        ]
        if blade_cards:
            best_blade_idx, best_blade_card = max(
                blade_cards, key=lambda pair: pair[1].buff_percent
            )
            return TurnProposal(
                action="CAST_SPELL",
                card_index=best_blade_idx,
                card_name=best_blade_card.name,
                target_index=None,
                target_name=player.name,
                rule_triggered="BUFF_SETUP_BLADE",
                rationale=f"Setting up damage boost: casting {best_blade_card.name} (+{best_blade_card.buff_percent}%).",
            )

        # Check affordable trap cards
        trap_cards = [
            (idx, card)
            for idx, card in enumerate(hand)
            if card.spell_type == "Trap"
            and playable(card)
            and card.buff_percent > 0
            and card.school.casefold() == player.school.casefold()
        ]
        if trap_cards:
            # Target boss or highest current HP enemy
            candidates = [
                (c_idx, card, e_idx, enemy)
                for c_idx, card in trap_cards
                for e_idx, enemy in enumerate(enemies)
                if enemy.is_alive and card.buff_percent not in enemy.active_traps
            ]
            if candidates:
                best_trap_idx, best_trap_card, target_idx, target_enemy = max(
                    candidates,
                    key=lambda choice: (
                        choice[3].is_boss,
                        choice[3].current_hp,
                        choice[1].buff_percent,
                    ),
                )
                return TurnProposal(
                    action="CAST_SPELL",
                    card_index=best_trap_idx,
                    card_name=best_trap_card.name,
                    target_index=target_idx,
                    target_name=target_enemy.name,
                    rule_triggered="BUFF_SETUP_TRAP",
                    rationale=f"Setting up damage boost on {target_enemy.name}: casting {best_trap_card.name} (+{best_trap_card.buff_percent}%).",
                )

        # -------------------------------------------------------------
        # Rule 4: Shield / Defensive Play
        # -------------------------------------------------------------
        shield_cards = [
            (idx, card)
            for idx, card in enumerate(hand)
            if card.spell_type == "Shield"
            and playable(card)
            and card.shield_percent > 0
            and card.shield_percent not in player.active_shields
        ]
        if shield_cards and hp_ratio <= 0.60:
            best_shield_idx, best_shield_card = max(
                shield_cards, key=lambda pair: pair[1].shield_percent
            )
            return TurnProposal(
                action="CAST_SPELL",
                card_index=best_shield_idx,
                card_name=best_shield_card.name,
                target_index=None,
                target_name=player.name,
                rule_triggered="DEFENSIVE_SHIELD",
                rationale=f"Defensive shielding: casting {best_shield_card.name} (-{best_shield_card.shield_percent}%).",
            )

        # -------------------------------------------------------------
        # Rule 5: Non-lethal attack if high pips or heavy attack available
        # -------------------------------------------------------------
        # A bounded heuristic, not a pip-cap or best-play guarantee.
        effective_pips = player.pips + player.power_pips * 2
        if effective_pips >= 5 and attack_options:
            best_chunk_attack = max(attack_options, key=lambda opt: opt["total_damage"])
            target_name = (
                "All Enemies"
                if best_chunk_attack["is_aoe"]
                else best_chunk_attack["target"].name
            )
            return TurnProposal(
                action="CAST_SPELL",
                card_index=best_chunk_attack["card_index"],
                card_name=best_chunk_attack["card"].name,
                target_index=best_chunk_attack["target_index"],
                target_name=target_name,
                expected_damage=best_chunk_attack["total_damage"],
                rule_triggered="HEAVY_ATTACK_CHUNK",
                rationale=f"High pip threshold reached ({effective_pips} pips). Casting {best_chunk_attack['card'].name} for {best_chunk_attack['total_damage']} damage.",
            )

        # -------------------------------------------------------------
        # Rule 6: Pass / Conserve Pips
        # -------------------------------------------------------------
        return TurnProposal(
            action="PASS",
            rule_triggered="CONSERVE_PIPS",
            rationale=f"Conserving pips ({effective_pips} available) for a decisive attack next round.",
        )


def plan_combat_from_dict(data: Dict[str, Any]) -> Dict[str, Any]:
    """Validate explicit observations/metadata and return a proposal, never a receipt.

    Missing live-style facts are errors, not zero resources. Dataclass construction
    remains available for explicitly synthetic arithmetic fixtures. Scalar modifiers
    must already apply to the one attack school supplied; school-indexed effects,
    DOT, mastery, archmastery, shadow pips and complex target modes are unsupported.
    Nonempty effect arrays require effect_order="application"; adapters must
    establish that ordering rather than forwarding arbitrary source enumeration.
    Freshness, current controls and turn identity belong to the observation bridge.
    """
    if not isinstance(data, dict):
        raise ValueError("Combat input must be an object.")
    if any(not isinstance(key, str) for key in data):
        raise ValueError("Combat fields must have text names.")
    unknown = set(data) - {
        "player",
        "enemies",
        "hand",
        "emergency_heal_threshold",
        "effect_order",
    }
    if unknown:
        raise ValueError(f"Unsupported combat fields: {', '.join(sorted(unknown))}.")
    if "effect_order" in data and data["effect_order"] != "application":
        raise ValueError("effect_order must be 'application' when supplied.")

    def require(value, names, label):
        if not isinstance(value, dict):
            raise ValueError(f"{label} must be an object.")
        missing = sorted(
            name for name in names if name not in value or value[name] is None
        )
        if missing:
            raise ValueError(f"{label} missing known fields: {', '.join(missing)}.")

    player_data = data.get("player")
    require(
        player_data,
        {
            "id",
            "name",
            "current_hp",
            "max_hp",
            "pips",
            "power_pips",
            "school",
            "gear_damage_boost",
            "pierce",
            "active_blades",
            "active_shields",
        },
        "player",
    )
    player = Combatant.from_dict(player_data)
    if player.pips + player.power_pips > 7:
        raise ValueError("Observed ordinary and power pips exceed seven slots.")

    enemies_data = data.get("enemies")
    if not isinstance(enemies_data, list) or not 1 <= len(enemies_data) <= 8:
        raise ValueError("enemies must contain 1 to 8 observed combatants.")
    for enemy in enemies_data:
        require(
            enemy,
            {
                "id",
                "name",
                "current_hp",
                "max_hp",
                "school",
                "resist",
                "boost",
                "active_traps",
                "active_shields",
                "is_boss",
            },
            "enemy",
        )
    enemies = [Combatant.from_dict(e) for e in enemies_data]
    has_effects = any(
        getattr(combatant, name)
        for combatant in [player, *enemies]
        for name in ("active_blades", "active_traps", "active_shields")
    )
    if has_effects and data.get("effect_order") != "application":
        raise ValueError(
            "Nonempty effect arrays require verified effect_order='application'."
        )

    hand_data = data.get("hand")
    if not isinstance(hand_data, list) or len(hand_data) > 64:
        raise ValueError("hand must be an explicit list of at most 64 observed cards.")
    for card in hand_data:
        require(
            card,
            {"name", "school", "spell_type", "pip_cost", "accuracy", "is_aoe"},
            "card",
        )
        if (
            not isinstance(card["spell_type"], str)
            or card["spell_type"] not in _EFFECT_FIELDS
        ):
            raise ValueError("Unsupported spell_type.")
        require(card, _EFFECT_FIELDS[card["spell_type"]], "card")
    hand = [SpellCard.from_dict(c) for c in hand_data]
    if len({c.school.casefold() for c in hand if c.spell_type == "Attack"}) > 1:
        raise ValueError(
            "Mixed attack schools require school-indexed modifiers, which are unsupported."
        )

    planner = CombatTurnPlanner(
        emergency_heal_threshold=data.get("emergency_heal_threshold", 0.35)
    )
    decision = planner.plan_turn(player, enemies, hand)
    return decision.to_dict()


def main():
    if len(sys.argv) < 2:
        print("Usage: python combat_turn_planner.py <input.json> [output.json]")
        sys.exit(1)

    input_path = Path(sys.argv[1])
    if not input_path.exists():
        print(f"Error: input file {input_path} not found.")
        sys.exit(1)

    raw_data = json.loads(input_path.read_text(encoding="utf-8"))
    result = plan_combat_from_dict(raw_data)

    if len(sys.argv) >= 3:
        output_path = Path(sys.argv[2])
        output_path.write_text(json.dumps(result, indent=2), encoding="utf-8")
        print(f"Decision written to {output_path}")
    else:
        print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
