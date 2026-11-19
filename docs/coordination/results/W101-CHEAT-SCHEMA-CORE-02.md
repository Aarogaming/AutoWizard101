# Evidence Report: W101-CHEAT-SCHEMA-CORE-02

## 1. Summary
Implemented the structured Boss Cheat Schema and tactical counter-constraint arbitration inside `crates/maelstrom-core/src/combat_turn_evaluator.rs`, and validated the tactical decision tree against prominent Wizard101 cheating boss mechanics in `crates/maelstrom-core/tests/combat_turn_evaluator_tests.rs`.

## 2. Implemented Components & Schemas

### Structured Boss Cheat Schema
- **`CheatTriggerCondition`**:
  - `RoundNumber { modulo: u32, offset: u32 }`
  - `SpellCastType(SpellCardType)`
  - `HealthThreshold { ratio: f64 }`
  - `PlayerCount { count: usize }`
  - `LateJoiner`
- **`CheatInterruptPhase`**:
  - `StartOfRound`
  - `ImmediateReaction`
  - `EndOfRound`
  - `PassiveAura`
- **`CheatEffect`**:
  - `CastSpell { spell_name: String, target_pedestal: usize }`
  - `ApplyAura { aura_name: String }`
  - `RemoveCharms`
  - `RemoveWards`
  - `ForceDiscardHand`
  - `InterruptStun`
- **`CounterConstraint`**:
  - `ForbidTraps`: Suppresses trap/feint placement when bosses retaliate with lethal single-target interrupts.
  - `ForbidBlades`: Suppresses blade stacking when bosses react with severe party punishment or blade theft.
  - `RequireDamageOverTime`: Prioritizes damage-over-time spells to bypass damage immunity or shield-clearing mechanics.
  - `CapDamagePerHit(f64)`: Clamps single-hit damage estimations to prevent false-lethal calculations on damage-capped encounters.
  - `ForbidHealing`: Suppresses emergency heals when healing triggers lethal retaliation.
- **`BossCheatRule`**:
  - Encapsulates `name`, `trigger`, `phase`, `effect`, and optional `counter_constraint`.

### Dynamic Combatant Integration
- Added `pub cheats: Vec<BossCheatRule>` to `CombatantState`.
- Implemented fluent builder methods `.with_cheat(cheat)` and `.with_cheats(cheats)`.

### Evaluator Enforcement (`evaluate_turn` & `estimate_damage`)
- **`estimate_damage`**: Clamps raw single-hit damage calculations to active `CapDamagePerHit(cap)` constraints, preventing false lethal evaluations against capped bosses (e.g. Tartarus/Cronus).
- **`evaluate_turn`**: Aggregates all living enemy counter-constraints into active tactical rules:
  1. *Critical Health Healing*: Blocked if `ForbidHealing` is active.
  2. *DoT Priority*: If `RequireDamageOverTime` is active, evaluates and prioritizes affordable `DamageOverTime` spells ahead of standard attacks.
  3. *Multi-Enemy AoE & Lethal*: Honors damage clamping and restricts attacks to DoT when required.
  4. *Buff Stacking*: Blocked if `ForbidBlades` is active.
  5. *Trap Stacking*: Blocked if `ForbidTraps` is active.
  6. *Standard Fallback*: Respects DoT requirements and selects highest viable candidate.

## 3. Verification & Test Evidence
- **Tests**:
  - `test_boss_cheat_forbid_traps_constraint`: Shane von Shane vampire trap interrupt suppresses Feint and executes fallback attack.
  - `test_boss_cheat_forbid_blades_constraint`: Belloq blade retaliation suppresses Fire Blade and executes direct attack.
  - `test_boss_cheat_damage_cap_and_dot_priority`: Clamps 2,725 raw damage Efreet to 1,000 via `CapDamagePerHit(1000.0)` and prioritizes Fire Dragon DoT under `RequireDamageOverTime`.
  - `test_boss_cheat_forbid_healing_constraint`: Malistaire retribution suppresses Pixie at 20% health and attacks.
  - Total 10/10 combat evaluator tests passing (`cargo test -p maelstrom-core --test combat_turn_evaluator_tests`).
  - Full workspace test suite passing (`cargo test --workspace`).
- **Linter & Code Standards**:
  - `cargo clippy -p maelstrom-core --all-targets -- -D warnings`: 0 warnings.
  - `rustfmt --edition 2021`: formatted and clean.
  - `python tools/check_project_board.py`: 30 workstreams valid.
  - `python tools/check_text_encoding.py`: 0 violations.
