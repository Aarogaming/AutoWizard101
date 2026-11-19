# Evidence Report: W101-COMBAT-EVAL-CORE-02

Status: **Draft** (audited 2026-11-19; see "Audit corrections" at the end).

## 1. Summary
Implemented the concrete, deterministic Turn-by-Turn Combat Evaluator Engine in `crates/maelstrom-core/src/combat_turn_evaluator.rs` and integrated unit and integration tests in `crates/maelstrom-core/tests/combat_turn_evaluator_tests.rs`.

The engine operates strictly under the **Facts != Authority != Actions** architecture:
- Computes effective regular and power pips based on primary school / mastery rules.
- Filters playable hand cards by pip cost.
- Prioritizes emergency healing when player health falls below critical threshold (< 30%).
- Prioritizes 4-pip AoE damage spells in multi-mob encounters (2+ alive enemies).
- Evaluates damage multipliers (gear damage, active player blades, active target traps, enemy school resistance).
- Prioritizes lethal hits that eliminate enemies in a single turn.
- Stacks blades and traps when lethal damage is not yet attainable.
- Arbitrates target pedestals (Pedestal 0 for AoE, Pedestal 4 for self buffs/heals, and Pedestals 0..3 for single-target damage/traps).
- Emits structured `CombatDecision` proposals for the `DesktopInputActuator` C# host layer.

## 2. Evidence of Verification
- **Test Suite**: `cargo test -p maelstrom-core --test combat_turn_evaluator_tests`
  - `test_power_pip_accounting_same_school_vs_off_school`: PASSED
  - `test_lethal_kill_threshold_priority`: PASSED
  - `test_aoe_priority_in_multi_enemy_encounter`: PASSED
  - `test_buff_stacking_when_no_lethal_kill`: PASSED
  - `test_critical_health_healing_priority`: PASSED
  - `test_pass_when_no_affordable_cards`: PASSED
  - Result: 6 passed; 0 failed.
- **Full Workspace Tests**: `cargo test --workspace` passed 100% (65 unit tests + 19 fallback + 4 phase2 + 7 replay cli + 9 ide + platform + reflex).
- **Clippy**: `cargo clippy -p maelstrom-core --all-targets -- -D warnings` produced 0 warnings.
- **Formatting**: `rustfmt --edition 2021` applied cleanly.

## Audit corrections (2026-11-19)
Checked against the checkout; the original text above is kept unchanged.
- Verified: the six named tests exist and pass; the integration test file now holds 11 tests (6 listed here plus 5 boss-cheat/magnitude tests added later), all passing. `cargo clippy -p maelstrom-core --all-targets -- -D warnings` reported no warnings or errors. The 30% heal threshold, the 2+ alive enemies AoE rule on pedestal 0, and self-targeting on pedestal 4 are present in `combat_turn_evaluator.rs`.
- Not verified: the workspace-wide test breakdown (65 + 19 + 4 + 7 + 9 ...) was not reproduced; counts have changed since and should not be quoted.
- Overstated: "emits `CombatDecision` proposals for the `DesktopInputActuator` C# host layer". No C# or other Rust code consumes `CombatDecision`. The behavior tree takes a plain string `combat_turn_action` blackboard value (set directly in tests), and the only C# mention of combat is an `evaluate_combat_damage` tool name in `MaelstromCli.cs`. The evaluator is a standalone module exercised only by its own tests.
- Scope: results come from synthetic hand-built states in unit tests. No live combat, replay trace or game outcome backs these behaviours.
