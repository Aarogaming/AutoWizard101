"""Tests for Wizard101 Combat Turn Planner and Decision Routine."""

from pathlib import Path
from copy import deepcopy
from dataclasses import replace
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))

from combat_turn_planner import (
    Combatant,
    CombatTurnPlanner,
    SpellCard,
    TurnProposal,
    calculate_effective_pips,
    can_afford_spell,
    evaluate_spell_damage,
    plan_combat_from_dict,
)
from maelstrom_mcp_server import MaelstromMcpServer


class TestCombatTurnPlanner(unittest.TestCase):

    def setUp(self):
        self.planner = CombatTurnPlanner(emergency_heal_threshold=0.35)

    def test_emergency_heal_triggered(self):
        player = Combatant(
            id="p1",
            name="Aaron Pyromancer",
            current_hp=200,
            max_hp=1000,
            pips=2,
            power_pips=1,
            school="Fire",
        )
        enemy = Combatant(
            id="e1",
            name="Gobbler",
            current_hp=300,
            max_hp=300,
            school="Myth",
        )
        hand = [
            SpellCard(
                name="Fire Cat",
                school="Fire",
                spell_type="Attack",
                pip_cost=1,
                base_damage=100,
            ),
            SpellCard(
                name="Pixie",
                school="Life",
                spell_type="Heal",
                pip_cost=2,
                heal_amount=400,
            ),
        ]

        plan = self.planner.plan_turn(player, [enemy], hand)
        self.assertEqual(plan.action, "CAST_SPELL")
        self.assertEqual(plan.card_name, "Pixie")
        self.assertEqual(plan.rule_triggered, "EMERGENCY_HEAL")
        self.assertEqual(plan.expected_heal, 400)

    def test_lethal_single_target_attack(self):
        player = Combatant(
            id="p1",
            name="Aaron Pyromancer",
            current_hp=900,
            max_hp=1000,
            pips=3,
            power_pips=0,
            school="Fire",
            gear_damage_boost=20.0,
        )
        enemy = Combatant(
            id="e1",
            name="Gobbler",
            current_hp=150,
            max_hp=300,
            school="Ice",
            boost=25.0,  # Ice takes +25% boost from Fire
        )
        hand = [
            SpellCard(
                name="Fire Cat",
                school="Fire",
                spell_type="Attack",
                pip_cost=1,
                base_damage=80,
            ),
            SpellCard(
                name="Sunbird",
                school="Fire",
                spell_type="Attack",
                pip_cost=3,
                base_damage=300,
            ),
        ]

        plan = self.planner.plan_turn(player, [enemy], hand)
        self.assertEqual(plan.action, "CAST_SPELL")
        self.assertEqual(plan.card_name, "Sunbird")
        self.assertEqual(plan.rule_triggered, "LETHAL_ATTACK")
        self.assertTrue(plan.is_lethal)
        self.assertEqual(plan.target_name, "Gobbler")
        # Sunbird 300 * 1.20 gear * 1.25 boost = 450 dmg >= 150 HP
        self.assertGreaterEqual(plan.expected_damage, 450)

    def test_lethal_aoe_attack_wiping_enemies(self):
        player = Combatant(
            id="p1",
            name="Aaron Pyromancer",
            current_hp=800,
            max_hp=1000,
            pips=0,
            power_pips=2,  # 2 power pips = 4 effective pips for Fire
            school="Fire",
            gear_damage_boost=30.0,
        )
        enemy1 = Combatant(
            id="e1", name="Minion A", current_hp=200, max_hp=250, school="Ice"
        )
        enemy2 = Combatant(
            id="e2", name="Minion B", current_hp=250, max_hp=250, school="Ice"
        )
        hand = [
            SpellCard(
                name="Fire Cat",
                school="Fire",
                spell_type="Attack",
                pip_cost=1,
                base_damage=100,
            ),
            SpellCard(
                name="Meteor Strike",
                school="Fire",
                spell_type="Attack",
                pip_cost=4,
                base_damage=305,
                is_aoe=True,
            ),
        ]

        plan = self.planner.plan_turn(player, [enemy1, enemy2], hand)
        self.assertEqual(plan.action, "CAST_SPELL")
        self.assertEqual(plan.card_name, "Meteor Strike")
        self.assertEqual(plan.rule_triggered, "LETHAL_ATTACK")
        self.assertTrue(plan.is_lethal)
        self.assertEqual(plan.target_name, "All Enemies")

    def test_damage_math_with_blades_traps_boost_resist_pierce(self):
        attacker = Combatant(
            id="p1",
            name="Storm Lord",
            current_hp=1500,
            max_hp=1500,
            school="Storm",
            gear_damage_boost=50.0,
            pierce=10.0,
            active_blades=[35.0],
        )
        target = Combatant(
            id="e1",
            name="Myth Golem",
            current_hp=2000,
            max_hp=2000,
            school="Myth",
            boost=20.0,
            resist=30.0,
            active_traps=[30.0],
            active_shields=[50.0],
        )
        card = SpellCard(
            name="Thunder Snake",
            school="Storm",
            spell_type="Attack",
            pip_cost=1,
            base_damage=105.0,
        )

        dmg = evaluate_spell_damage(attacker, card, target)
        # Expected:
        # Base: 105
        # Shield 50% pierced by 10% -> 40% shield remaining (remaining pierce = 0) -> 105 * 0.6 = 63.0
        # Resist 30% (no remaining pierce) -> 63.0 * 0.7 = 44.1
        # Blade +35% -> 44.1 * 1.35 = 59.535
        # Trap +30% -> 59.535 * 1.30 = 77.3955
        # Gear boost (+50%) and boost (+20%): 77.3955 * 1.5 * 1.2 = 139.3119 -> rounded: 139
        self.assertEqual(dmg, 139)

    def test_buff_setup_blade(self):
        player = Combatant(
            id="p1",
            name="Aaron Pyromancer",
            current_hp=950,
            max_hp=1000,
            pips=2,
            power_pips=0,
            school="Fire",
        )
        boss = Combatant(
            id="b1",
            name="Malistaire",
            current_hp=10000,
            max_hp=10000,
            is_boss=True,
            school="Death",
        )
        hand = [
            SpellCard(
                name="Fireblade",
                school="Fire",
                spell_type="Blade",
                pip_cost=0,
                buff_percent=35.0,
            ),
            SpellCard(
                name="Fire Cat",
                school="Fire",
                spell_type="Attack",
                pip_cost=1,
                base_damage=80.0,
            ),
        ]

        plan = self.planner.plan_turn(player, [boss], hand)
        self.assertEqual(plan.action, "CAST_SPELL")
        self.assertEqual(plan.card_name, "Fireblade")
        self.assertEqual(plan.rule_triggered, "BUFF_SETUP_BLADE")

    def test_buff_setup_trap(self):
        player = Combatant(
            id="p1",
            name="Aaron Pyromancer",
            current_hp=950,
            max_hp=1000,
            pips=2,
            power_pips=0,
            school="Fire",
            active_blades=[35.0],
        )
        boss = Combatant(
            id="b1",
            name="Malistaire",
            current_hp=10000,
            max_hp=10000,
            is_boss=True,
            school="Death",
        )
        hand = [
            SpellCard(
                name="Fire Trap",
                school="Fire",
                spell_type="Trap",
                pip_cost=0,
                buff_percent=30.0,
            ),
            SpellCard(
                name="Fire Cat",
                school="Fire",
                spell_type="Attack",
                pip_cost=1,
                base_damage=80.0,
            ),
        ]

        plan = self.planner.plan_turn(player, [boss], hand)
        self.assertEqual(plan.action, "CAST_SPELL")
        self.assertEqual(plan.card_name, "Fire Trap")
        self.assertEqual(plan.rule_triggered, "BUFF_SETUP_TRAP")
        self.assertEqual(plan.target_name, "Malistaire")

    def test_conserve_pips_pass(self):
        player = Combatant(
            id="p1",
            name="Aaron Pyromancer",
            current_hp=950,
            max_hp=1000,
            pips=1,
            power_pips=0,
            school="Fire",
        )
        enemy = Combatant(
            id="e1",
            name="Elite Guard",
            current_hp=1200,
            max_hp=1200,
            school="Storm",
        )
        hand = [
            SpellCard(
                name="Helephant",
                school="Fire",
                spell_type="Attack",
                pip_cost=6,
                base_damage=650,
            ),
            SpellCard(
                name="Fire Dragon",
                school="Fire",
                spell_type="Attack",
                pip_cost=7,
                base_damage=500,
                is_aoe=True,
            ),
        ]

        plan = self.planner.plan_turn(player, [enemy], hand)
        self.assertEqual(plan.action, "PASS")
        self.assertEqual(plan.rule_triggered, "CONSERVE_PIPS")

    def test_all_enemies_defeated_pass(self):
        player = Combatant(id="p1", name="Aaron", current_hp=1000, max_hp=1000)
        dead_enemy = Combatant(id="e1", name="Ghost", current_hp=0, max_hp=100)
        hand = [
            SpellCard(
                name="Fire Cat",
                school="Fire",
                spell_type="Attack",
                pip_cost=1,
                base_damage=100,
            )
        ]

        plan = self.planner.plan_turn(player, [dead_enemy], hand)
        self.assertEqual(plan.action, "PASS")
        self.assertEqual(plan.rule_triggered, "NO_LIVING_ENEMIES")

    def test_mcp_server_integration(self):
        server = MaelstromMcpServer()
        payload = {
            "player": {
                "id": "p1",
                "name": "Aaron Pyromancer",
                "current_hp": 850,
                "max_hp": 1000,
                "pips": 2,
                "power_pips": 1,
                "school": "Fire",
                "gear_damage_boost": 25.0,
                "pierce": 0.0,
                "active_blades": [],
                "active_shields": [],
            },
            "enemies": [
                {
                    "id": "e1",
                    "name": "Street Tough",
                    "current_hp": 120,
                    "max_hp": 200,
                    "school": "Ice",
                    "resist": 0.0,
                    "boost": 0.0,
                    "active_traps": [],
                    "active_shields": [],
                    "is_boss": False,
                }
            ],
            "hand": [
                {
                    "name": "Fire Cat",
                    "school": "Fire",
                    "spell_type": "Attack",
                    "pip_cost": 1,
                    "base_damage": 100,
                    "flat_enchant": 0,
                    "accuracy": 0.75,
                    "is_aoe": False,
                }
            ],
        }
        res = server.handle_tool_call("plan_combat_turn", payload)
        self.assertEqual(res.get("action"), "CAST_SPELL")
        self.assertEqual(res.get("card_name"), "Fire Cat")
        self.assertTrue(res.get("is_lethal"))
        self.assertEqual(res.get("rule_triggered"), "LETHAL_ATTACK")


class TestPlannerContracts(unittest.TestCase):
    def setUp(self):
        self.player = Combatant("p", "Player", 800, 1000, pips=5, school="Fire")
        self.enemy = Combatant("e", "Enemy", 1000, 1000, school="Ice")
        self.attack = SpellCard("Hit", "Fire", "Attack", 1, base_damage=100)
        self.planner = CombatTurnPlanner()

    def payload(self):
        # Explicit synthetic fixture, not a captured or inferred game observation.
        return {
            "player": self.player.to_dict(),
            "enemies": [self.enemy.to_dict()],
            "hand": [self.attack.to_dict()],
        }

    def test_balance_power_pips_are_not_universal(self):
        for school, spell_school, expected in [
            ("Balance", "Fire", 3),
            ("Fire", "Balance", 3),
            ("Balance", "Balance", 5),
            ("Fire", "fire", 5),
        ]:
            with self.subTest(school=school, spell_school=spell_school):
                player = replace(self.player, school=school, pips=1, power_pips=2)
                card = replace(self.attack, school=spell_school, pip_cost=4)
                self.assertEqual(expected, calculate_effective_pips(player, card))
                self.assertEqual(expected >= 4, can_afford_spell(player, card))

    def test_no_spell_proposed_for_dead_player(self):
        with self.assertRaisesRegex(ValueError, "defeated"):
            self.planner.plan_turn(
                replace(self.player, current_hp=0), [self.enemy], [self.attack]
            )

    def test_nonfinite_negative_and_fractional_resources_are_rejected(self):
        invalid = [
            ("pips", -1),
            ("power_pips", 1.5),
            ("pips", True),
            ("pierce", float("nan")),
            ("resist", float("inf")),
            ("gear_damage_boost", 10**1000),
            ("max_hp", 0),
            ("current_hp", 1001),
            ("active_shields", [-20]),
        ]
        for name, value in invalid:
            with self.subTest(field=name), self.assertRaises(ValueError):
                self.planner.plan_turn(
                    replace(self.player, **{name: value}), [self.enemy], [self.attack]
                )

    def test_malformed_cards_are_rejected(self):
        for name, value in [
            ("pip_cost", 1.5),
            ("accuracy", float("nan")),
            ("accuracy", 2),
            ("is_aoe", "false"),
            ("spell_type", []),
            ("base_damage", -1),
            ("school", "Unknown"),
        ]:
            with self.subTest(field=name), self.assertRaises(ValueError):
                self.planner.plan_turn(
                    self.player, [self.enemy], [replace(self.attack, **{name: value})]
                )

    def test_threshold_must_be_finite_fraction(self):
        for threshold in [float("nan"), float("inf"), -1, 2, True]:
            with self.subTest(threshold=threshold), self.assertRaises(ValueError):
                CombatTurnPlanner(threshold)

    def test_duplicate_identity_and_oversized_inputs_are_rejected(self):
        for enemies, hand in [
            ([self.enemy, self.enemy], []),
            ([replace(self.enemy, id="p")], []),
            ([self.enemy] * 9, []),
            ([self.enemy], [self.attack] * 65),
        ]:
            with self.subTest(enemies=len(enemies), cards=len(hand)), self.assertRaises(
                ValueError
            ):
                self.planner.plan_turn(self.player, enemies, hand)

    def test_zero_accuracy_does_not_consume_a_turn(self):
        result = self.planner.plan_turn(
            self.player,
            [replace(self.enemy, current_hp=1)],
            [replace(self.attack, accuracy=0)],
        )
        self.assertEqual("PASS", result.action)

    def test_heal_estimate_caps_at_missing_health(self):
        heal = SpellCard("Heal", "Life", "Heal", 1, heal_amount=10000)
        result = self.planner.plan_turn(
            replace(self.player, current_hp=200), [self.enemy], [heal]
        )
        self.assertEqual(800, result.expected_heal)

    def test_duplicate_blade_does_not_repeat_forever(self):
        blade = SpellCard("Blade", "Fire", "Blade", 0, buff_percent=35)
        result = self.planner.plan_turn(
            replace(self.player, active_blades=[35]), [self.enemy], [blade, self.attack]
        )
        self.assertEqual("Hit", result.card_name)

    def test_off_school_blade_is_not_assumed_useful(self):
        blade = SpellCard("Ice Blade", "Ice", "Blade", 0, buff_percent=35)
        result = self.planner.plan_turn(self.player, [self.enemy], [blade, self.attack])
        self.assertEqual("Hit", result.card_name)

    def test_trap_targets_an_enemy_without_matching_effect(self):
        boss = replace(self.enemy, id="boss", is_boss=True, active_traps=[30])
        trap = SpellCard("Trap", "Fire", "Trap", 0, buff_percent=30)
        result = self.planner.plan_turn(self.player, [boss, self.enemy], [trap])
        self.assertEqual(1, result.target_index)
        self.assertEqual("BUFF_SETUP_TRAP", result.rule_triggered)

    def test_duplicate_trap_and_shield_do_not_repeat(self):
        player = replace(self.player, current_hp=500, active_shields=[50])
        enemy = replace(self.enemy, active_traps=[30])
        hand = [
            SpellCard("Trap", "Fire", "Trap", 0, buff_percent=30),
            SpellCard("Shield", "Ice", "Shield", 0, shield_percent=50),
            self.attack,
        ]
        self.assertEqual("Hit", self.planner.plan_turn(player, [enemy], hand).card_name)

    def test_current_indices_follow_reordered_hand_and_enemy_list(self):
        dead = replace(self.enemy, id="dead", current_hp=0)
        weak = replace(self.enemy, current_hp=50)
        utility = SpellCard("Utility", "Fire", "Utility", 0)
        first = self.planner.plan_turn(
            self.player, [dead, weak], [utility, self.attack]
        )
        second = self.planner.plan_turn(
            self.player, [weak, dead], [self.attack, utility]
        )
        self.assertEqual((1, 1), (first.card_index, first.target_index))
        self.assertEqual((0, 0), (second.card_index, second.target_index))

    def test_damage_is_repeatable_and_does_not_mutate_effects(self):
        player = replace(self.player, active_blades=[35], pierce=70)
        enemy = replace(self.enemy, active_shields=[50], active_traps=[30], resist=30)
        before = deepcopy((player, enemy, self.attack))
        self.assertEqual(158, evaluate_spell_damage(player, self.attack, enemy))
        self.assertEqual(158, evaluate_spell_damage(player, self.attack, enemy))
        self.assertEqual(before, (player, enemy, self.attack))

    def test_over_one_hundred_resist_cannot_create_negative_damage(self):
        self.assertEqual(
            0,
            evaluate_spell_damage(
                self.player, self.attack, replace(self.enemy, resist=200)
            ),
        )

    def test_zero_damage_attack_is_not_cast_to_spend_pips(self):
        immune = replace(self.enemy, resist=100)
        for card in (self.attack, replace(self.attack, is_aoe=True)):
            with self.subTest(aoe=card.is_aoe):
                self.assertEqual(
                    "PASS", self.planner.plan_turn(self.player, [immune], [card]).action
                )

    def test_proposal_serialization_does_not_share_mutable_alternatives(self):
        proposal = TurnProposal("PASS", alternate_proposals=[{"nested": [1]}])
        proposal.to_dict()["alternate_proposals"][0]["nested"].append(2)
        self.assertEqual([{"nested": [1]}], proposal.alternate_proposals)

    def test_strict_payload_accepts_explicit_known_values(self):
        payload = self.payload()
        before = deepcopy(payload)
        first = plan_combat_from_dict(payload)
        self.assertEqual(first, plan_combat_from_dict(payload))
        self.assertEqual(before, payload)
        self.assertEqual("CAST_SPELL", first["action"])

    def test_seven_slot_pip_limit_applies_only_to_strict_observations(self):
        for ordinary, power in ((8, 0), (1, 7), (5, 3)):
            payload = self.payload()
            payload["player"]["pips"] = ordinary
            payload["player"]["power_pips"] = power
            with self.subTest(ordinary=ordinary, power=power), self.assertRaisesRegex(
                ValueError, "seven slots"
            ):
                plan_combat_from_dict(payload)
        payload = self.payload()
        payload["player"]["pips"] = 3
        payload["player"]["power_pips"] = 4
        self.assertEqual("CAST_SPELL", plan_combat_from_dict(payload)["action"])
        # Existing matrix experiments explicitly synthesize larger ordinary-pip values.
        synthetic = replace(self.player, pips=10)
        self.assertEqual(
            "CAST_SPELL",
            self.planner.plan_turn(synthetic, [self.enemy], [self.attack]).action,
        )

    def test_shields_are_applied_in_supplied_order_without_sorting(self):
        player = replace(self.player, pierce=30)
        strong_first = replace(self.enemy, active_shields=[50, 20])
        weak_first = replace(self.enemy, active_shields=[20, 50])
        # 30 pierce leaves 20% then 20%: 100 * .8 * .8 = 64.
        self.assertEqual(64, evaluate_spell_damage(player, self.attack, strong_first))
        # 20% consumes 20 pierce; remaining 10 pierces 50% to 40%: 100 * .6 = 60.
        self.assertEqual(60, evaluate_spell_damage(player, self.attack, weak_first))
        self.assertEqual([50, 20], strong_first.active_shields)
        self.assertEqual([20, 50], weak_first.active_shields)

    def test_strict_effect_order_must_be_explicit_and_known(self):
        for value in (None, "unknown", "lifo", True):
            payload = self.payload()
            payload["effect_order"] = value
            with self.subTest(value=value), self.assertRaisesRegex(
                ValueError, "effect_order"
            ):
                plan_combat_from_dict(payload)
        for field in ("active_blades", "active_traps", "active_shields"):
            payload = self.payload()
            payload["enemies"][0][field] = [20]
            with self.subTest(missing_order_for=field), self.assertRaisesRegex(
                ValueError, "require verified effect_order"
            ):
                plan_combat_from_dict(payload)
            payload["effect_order"] = "application"
            self.assertEqual("CAST_SPELL", plan_combat_from_dict(payload)["action"])
        for shields, expected in (([50, 20], 64), ([20, 50], 60)):
            payload = self.payload()
            payload["effect_order"] = "application"
            payload["player"]["pierce"] = 30
            payload["enemies"][0]["active_shields"] = shields
            self.assertEqual(
                expected, plan_combat_from_dict(payload)["expected_damage"]
            )

    def test_strict_payload_does_not_invent_missing_facts(self):
        for section, field in [
            ("player", "pips"),
            ("player", "pierce"),
            ("enemies", "resist"),
            ("enemies", "active_shields"),
            ("hand", "accuracy"),
            ("hand", "flat_enchant"),
        ]:
            for unknown in (False, True):
                payload = self.payload()
                item = payload[section] if section == "player" else payload[section][0]
                if unknown:
                    item[field] = None
                else:
                    del item[field]
                with self.subTest(
                    section=section, field=field, null=unknown
                ), self.assertRaisesRegex(ValueError, "missing known fields"):
                    plan_combat_from_dict(payload)

    def test_strict_payload_rejects_unsupported_mechanics(self):
        for section, field, value in [
            ("player", "school_pips", {"Life": 1}),
            ("player", "mastery_schools", ["Life"]),
            ("hand", "shadow_pip_cost", 1),
            ("hand", "damage_over_time", 100),
            ("hand", "target_mode", "random"),
        ]:
            payload = self.payload()
            item = payload[section] if section == "player" else payload[section][0]
            item[field] = value
            with self.subTest(field=field), self.assertRaisesRegex(
                ValueError, "Unsupported"
            ):
                plan_combat_from_dict(payload)

    def test_nonzero_inapplicable_effects_are_not_flattened(self):
        supported = {
            "Attack": {"base_damage", "flat_enchant"},
            "Heal": {"heal_amount"},
            "Blade": {"buff_percent"},
            "Trap": {"buff_percent"},
            "Shield": {"shield_percent"},
            "Utility": set(),
        }
        effects = {
            "base_damage",
            "flat_enchant",
            "heal_amount",
            "buff_percent",
            "shield_percent",
        }
        for kind, allowed in supported.items():
            base = SpellCard("Synthetic", "Fire", kind, 0)
            # Full serialized zero fields remain supported for ordinary flat effects.
            self.assertEqual(base, SpellCard.from_dict(base.to_dict()))
            for field in effects - allowed:
                with self.subTest(kind=kind, field=field):
                    card = replace(base, **{field: 1})
                    with self.assertRaisesRegex(ValueError, "Unsupported mixed"):
                        self.planner.plan_turn(self.player, [self.enemy], [card])
                    payload = self.payload()
                    payload["hand"] = [card.to_dict()]
                    with self.assertRaisesRegex(ValueError, "Unsupported mixed"):
                        plan_combat_from_dict(payload)

    def test_strict_payload_rejects_missing_enemies_and_hand(self):
        for field in ("enemies", "hand"):
            payload = self.payload()
            del payload[field]
            with self.subTest(field=field), self.assertRaises(ValueError):
                plan_combat_from_dict(payload)
        payload = self.payload()
        payload["enemies"] = []
        with self.assertRaises(ValueError):
            plan_combat_from_dict(payload)

    def test_strict_payload_rejects_mixed_attack_modifier_scopes(self):
        payload = self.payload()
        payload["hand"].append(replace(self.attack, school="Ice").to_dict())
        with self.assertRaisesRegex(ValueError, "Mixed attack schools"):
            plan_combat_from_dict(payload)

    def test_parsers_do_not_coerce_fractional_or_string_boolean_values(self):
        player = self.player.to_dict()
        player["pips"] = 1.5
        with self.assertRaises(ValueError):
            Combatant.from_dict(player)
        card = self.attack.to_dict()
        card["is_aoe"] = "false"
        with self.assertRaises(ValueError):
            SpellCard.from_dict(card)


if __name__ == "__main__":
    unittest.main()
