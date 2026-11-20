# W101-COMBAT-01 — Offline Combat Turn Decision Routine and Planner

Status: implemented and tested offline; live combat dispatch is not qualified.

The Combat Turn Planner and Decision Routine provide a deterministic, rule-based turn evaluator for Wizard101 combat encounters within Maelstrom. The implementation pairs a declarative routine transition graph with an offline decision engine powered by the repaired KursovaLibrary damage arithmetic.

## Architecture and Contracts

1. **Combat Decision Planner (`tools/combat_turn_planner.py`):**
   - Inputs:
     - Player State: `current_hp`, `max_hp`, `pips`, `power_pips`, `school`, `gear_damage_boost`, `pierce`, `active_blades`, `active_shields`.
     - Enemy States: list of enemies with `current_hp`, `max_hp`, `school`, `resist`, `boost`, `active_traps`, `active_shields`, `is_boss`.
     - Hand Cards: list of cards in hand with `name`, `school`, `spell_type`, `pip_cost`, `base_damage`, `flat_enchant`, `heal_amount`, `buff_percent`, `shield_percent`, `is_aoe`.
   - Priority Hierarchy:
     - **Rule 1 (Emergency Heal):** Prioritizes healing when player HP <= 35% of max HP, selecting the highest-yield affordable heal.
     - **Rule 2 (Lethal Elimination):** Evaluates all affordable attacks against all living targets. Calculates exact damage considering pierce vs shields, net resist, active blades, traps, gear boost, and natural school vulnerabilities. Prioritizes AoE multi-kill wipes, then single-target lethal hits on bosses or priority minions.
     - **Rule 3 (Buff Setup):** If no attack is lethal, selects matching school blades (targeting player) or potent traps (targeting boss/highest HP enemy).
     - **Rule 4 (Defensive Shielding):** Plays shields when player health is moderate-to-low and incoming damage is anticipated.
     - **Rule 5 (Heavy Chunking):** Casts high-damage attack when pips are near capacity (>= 5 pips) even if non-lethal.
     - **Rule 6 (Pip Conservation / Pass):** Passes to accumulate pips for high-rank spells when no productive card can be cast.

2. **Declarative Routine Graph (`ProjectMaelstrom/ProjectMaelstrom/Resources/Routines/combat-turn-decision.json`):**
   - Conforms strictly to `RoutineGraph` schema v1:
     - `start_node`: `combat_start`
     - Edges:
       - `CombatEntered` -> action `combat.detect-hand` -> expected `HandObserved` -> node `eval_turn`
       - `HandObserved` -> action `combat.evaluate-turn` -> expected `TurnPlanReady` -> node `execute_decision`
       - `TurnPlanReady` -> action `combat.dispatch-card` -> expected `ActionDispatched` -> node `round_resolving`
       - `ActionDispatched` -> action `combat.observe-round` -> expected `RoundComplete` / `CombatVictory` -> node `combat_loop`
       - `RoundComplete` -> action `combat.prepare-round` -> expected `CombatEntered` -> node `combat_start`
       - `CombatVictory` -> action `combat.collect-rewards` -> expected `CombatFinished` -> node `done`
     - `complete_scene`: `CombatFinished` at terminal node `done`.
   - Embedded into assembly as `Maelstrom.Routines.combat-turn-decision.json` and validated by `RoutineGraphs.BuiltIn("combat-turn-decision")`.

3. **Maelstrom MCP Server Integration (`tools/maelstrom_mcp_server.py`):**
   - Exposes `plan_combat_turn` tool over JSON-RPC 2.0 stdio, returning structured proposals with audit rationales and predicted damage/heal quantities.
   - Exposes `evaluate_combat_damage` tool for raw single-spell damage and critical projections.

## Evidence

- **Decision Engine Tests (`tools/tests/test_combat_turn_planner.py`):** 9 tests passed:
  - `test_emergency_heal_triggered`: Validates heal selection under critical HP.
  - `test_lethal_single_target_attack`: Validates single-target lethal hit with gear damage and school boost.
  - `test_lethal_aoe_attack_wiping_enemies`: Validates multi-enemy lethal wipe via AoE spell.
  - `test_damage_math_with_blades_traps_boost_resist_pierce`: Validates exact arithmetic matching repaired KursovaLibrary formula (139 predicted damage).
  - `test_buff_setup_blade`: Validates blade buff selection when non-lethal.
  - `test_buff_setup_trap`: Validates trap setup on boss enemy.
  - `test_conserve_pips_pass`: Validates pass when pips are insufficient for high-rank spells.
  - `test_all_enemies_defeated_pass`: Validates clean pass when no enemies remain alive.
  - `test_mcp_server_integration`: Validates MCP tool call execution and response formatting.

- **Native Routine Runner Tests (`ProjectMaelstrom.Tests/RoutineGraphTests.cs`):**
  - `CombatTurnDecisionRoutineLoadsAndAdvancesCorrectly`: Validates end-to-end receipt acknowledgement and scene transitions from `CombatEntered` through `CombatVictory` to `CombatFinished`.
  - Format, whitespace, and analyzers verified clean with zero warnings/errors.

- **Offline Boundary:**
  - `live_qualified: false`.
  - Zero desktop dispatch or live memory access during planning.
