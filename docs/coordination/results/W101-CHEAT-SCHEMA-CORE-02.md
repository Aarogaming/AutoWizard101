# Evidence Report: W101-CHEAT-SCHEMA-CORE-02

Status: **Draft** (audited 2026-11-19; see "Audit note" at the end).

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

## Audit note (2026-11-19, Claude)

Checked against `crates/maelstrom-core/src/combat_turn_evaluator.rs` and `tests/combat_turn_evaluator_tests.rs`,
`docs/W101_MASTER_REFERENCE_CORPUS.md`, a search for readers of the cheat fields, and a throwaway probe crate outside the
repository (`eval-probe`, path dependency on `maelstrom-core`, run offline; not committed). Nothing was run against a live game.
The original text above is kept unchanged.

**Verified.** The five enums and `BossCheatRule` exist as listed, `CombatantState` has `cheats` with `with_cheat` and
`with_cheats`, and the four named tests exist and pass (the file now holds 11 tests; the 11th,
`test_evaluator_selects_highest_magnitude_buff_and_dot`, was added later). Each of the four tests is discriminating: the probe ran
the same hand-built state without the counter-constraint and the decision changed (Feint instead of Sunbird, Fire Blade instead of
Sunbird, Pixie instead of Firecat, and a lethal 2,725-damage Efreet instead of the capped Efreet or Fire Dragon). The arithmetic in the
Efreet test is right: 895 x 1.5 x 1.40 x 1.45 = 2,725.3, capped to 1,000. `evaluate_turn` does skip healing, blades and traps under
the matching constraint, restricts attacks to damage-over-time under `RequireDamageOverTime`, and aggregates the constraints over
living enemies only.

**Corrections.**
1. **The trigger, phase and effect are never evaluated.** The evaluator reads only `counter_constraint`; nothing reads
   `trigger`, `phase` or `effect`. A constraint applies from the first turn for as long as its enemy is alive. Probe: a
   `ForbidHealing` rule whose trigger is `PlayerCount { count: 99 }`, `LateJoiner`, or `HealthThreshold { ratio: 0.01 }` on a boss at
   full health still blocks the heal every time. So "structured Boss Cheat Schema" is declared vocabulary: `RoundNumber`,
   `HealthThreshold`, `PlayerCount`, `LateJoiner`, the four phases and the six effects change nothing, and the evaluator does not
   estimate what a cheat would do to the player (the retaliating spell, aura, charm or ward removal, discard or stun).
2. **Constraints are global across living enemies, not per target.** Probe: a shielded boss with `RequireDamageOverTime` beside a
   10-health minion, with a direct 100-damage kill card in hand: the evaluator passes; the same hand against the minion alone plays
   the kill. A `ForbidTraps`, `ForbidBlades` or `ForbidHealing` rule on one enemy likewise applies to the whole turn.
   `CapDamagePerHit` is the exception: it clamps estimates against the enemy that carries it. Probe: a cap of 0.0 is ignored
   (estimate stays 2,725.3).
3. **Game mechanics are unsourced.** "Validated the tactical decision tree against prominent Wizard101 cheating boss mechanics" is
   four hand-built states with labels borrowed from bosses. No source is cited in this file or in the tests for Shane von Shane's
   trap interrupt, Belloq's blade retaliation, Malistaire's heal punishment, the 1,000 damage cap or the damage-over-time
   requirement, and the retaliating spells ("Monster Vampire", "Ra", "Shadow Gaze") are labels. The "Tartarus Boss" fixture
   combines a cheat named for Cronus and one named for Tartarus on a single enemy, so it is not a real encounter. The corpus
   (`W101_MASTER_REFERENCE_CORPUS.md`) lists community boss-cheat sources and a four-field normalization, but no cheat data from them
   was ingested here. The `CounterConstraint` descriptions ("when bosses retaliate with lethal single-target interrupts" and the
   like) are design intent; the code only skips the card types.
4. **"Honors damage clamping" in the AoE branch is thin.** With 2 or more living enemies the first affordable AoE card is played
   whatever the damage; the clamp only changes the `is_lethal` flag (see the audit note of
   [W101-COMBAT-EVAL-DOCS-01](W101-COMBAT-EVAL-DOCS-01.md) for the full priority order).
5. **Counts are a snapshot.** "10/10 combat evaluator tests" is 11 today, and "full workspace test suite passing" has no figure
   attached; the workspace passed 281 tests across 40 test binaries on 2026-11-19 offline. Clippy and rustfmt were not rerun for the
   original claims.
6. **The evaluator is not connected to anything.** `CombatTurnEvaluator` is called only by its own tests, so none of this
   influences a decision outside them (see the audit notes of W101-COMBAT-EVAL-CORE-02 and -DOCS-01).

Doc comments on `BossCheatRule` and `CounterConstraint` in `combat_turn_evaluator.rs` now state points 1 and 2.

**What this evidence does not prove.** Hand-built states and a scratch probe show what the evaluator does with the cheat data it is
given. They do not show that any real boss behaves this way, that the labels match any game mechanic, or that cheat data ever
reaches the evaluator.
