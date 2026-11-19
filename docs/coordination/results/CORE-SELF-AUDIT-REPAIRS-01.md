# Evidence Report: CORE-SELF-AUDIT-REPAIRS-01

## 1. Summary
Conducted a comprehensive, critical self-audit of all implementations, domain modules, and tests produced during this session. Identified, diagnosed, and repaired three concrete domain and spatial failure modes, followed by adding targeted regression tests and validating the complete workspace.

---

## 2. Identified Deficiencies & Applied Corrections

### A. Elemental Boost Preservation in Damage Pipeline (`crates/maelstrom-core/src/minigame_domain.rs`)
- **Deficiency**: In `calculate_pipeline_damage`, the effective resistance calculation used:
  ```rust
  let effective_resist = (inputs.enemy_resist - inputs.player_pierce).max(0.0);
  ```
  This unconditionally clamped `effective_resist` to $\ge 0.0$. In Wizard101, when an enemy has a natural school weakness/boost (e.g. Ice mob having $-30\%$ Fire resistance, or `enemy_resist = -0.30`), the incoming spell damage should amplify by $+30\%$ ($1.0 - (-0.30) = 1.30$). The `.max(0.0)` clamp completely erased all natural mob boosts to $0\%$.
- **Correction**: Replaced with conditional pierce application that preserves negative resistance (boosts) while preventing pierce from amplifying resistance below zero:
  ```rust
  let effective_resist = if inputs.enemy_resist > 0.0 {
      (inputs.enemy_resist - inputs.player_pierce).max(0.0)
  } else {
      inputs.enemy_resist
  };
  let resist_factor = (1.0 - effective_resist).max(0.0);
  ```
- **Test Evidence**: Added `test_damage_pipeline_enemy_boost_amplification` in `crates/maelstrom-core/tests/minigame_domain_tests.rs`, validating that a 500 base damage spell against a mob with $-30\%$ resist outputs exactly 650 damage, and confirming player pierce does not artificially inflate negative resist.

---

### B. Vertical Clearance on Bridges & Overpasses (`crates/maelstrom-core/src/predictive_rollout.rs`)
- **Deficiency**: In `simulate_action`, physical hazard clearance was computed purely on the $(X, Y)$ ground plane:
  ```rust
  let dx = cur_pose.x - pred_mob_x;
  let dy = cur_pose.y - pred_mob_y;
  let dist = (dx * dx + dy * dy).sqrt();
  let clearance = dist - effective_aggro;
  ```
  While `Hazard::evaluate_threat_cost` correctly checks vertical separation against `vertical_tolerance`, `simulate_action` neglected vertical checking. Consequently, when crossing high bridges or elevated terrain (e.g. $Z = 45$ over a street mob at $Z = 0$ with `vertical_tolerance = 30`), the 2D footprint overlap caused `is_lethal = true` and `score = -1,000,000`, falsely pruning completely safe elevated bridges.
- **Correction**: Added vertical separation gating prior to evaluating 2D cylinder aggro:
  ```rust
  let vert_diff = (cur_pose.z - hazard.position.2).abs();
  if vert_diff <= hazard.vertical_tolerance {
      let pred_mob_x = hazard.position.0 + hazard.velocity.0 * elapsed_t.clamp(0.0, 3.0);
      let pred_mob_y = hazard.position.1 + hazard.velocity.1 * elapsed_t.clamp(0.0, 3.0);
      let dx = cur_pose.x - pred_mob_x;
      let dy = cur_pose.y - pred_mob_y;
      let dist = (dx * dx + dy * dy).sqrt();
      let effective_aggro = hazard.aggro_radius * hazard_margin_factor;
      let clearance = dist - effective_aggro;
      if clearance < min_clearance { min_clearance = clearance; }
      if clearance <= 0.0 { is_lethal = true; }
  }
  ```
- **Test Evidence**: Added `test_bridge_vertical_separation_not_pruned_as_lethal` in `crates/maelstrom-core/tests/predictive_rollout_tests.rs`, verifying forward progress across a bridge at $Z = 45$ over a street mob at $Z = 0$ achieves a positive score and `is_safe = true`.

---

### C. Suboptimal Card Selection for DoTs, Blades & Traps (`crates/maelstrom-core/src/combat_turn_evaluator.rs`)
- **Deficiency**: `evaluate_turn` used `.find(...)` to pick candidate cards, taking whichever card appeared first in hand order. If the player held a minor DoT (Fire Elf) in slot 0 and a primary DoT (Fire Dragon) in slot 1 under a `RequireDamageOverTime` cheat, it selected Fire Elf. Similarly, blade stacking picked whatever blade was drawn first rather than prioritizing higher buff percentages (e.g. $+45\%$ Sharpened Blade over standard $+35\%$ Blade).
- **Correction**:
  - `require_dot`: Collects all affordable DoT spells and sorts by `base_damage` descending.
  - `BladeSelf`: Collects all affordable blades and sorts by `buff_percentage` descending.
  - `TrapEnemy`: Collects all affordable traps and sorts by `buff_percentage` descending.
- **Test Evidence**: Added `test_evaluator_selects_highest_magnitude_buff_and_dot` in `crates/maelstrom-core/tests/combat_turn_evaluator_tests.rs`, validating that the evaluator prioritizes $+45\%$ Sharpened Blade over $+35\%$ Blade, and prioritizes 440 damage Fire Dragon over 150 damage Fire Elf.

---

## 3. Verification Suite
- `cargo test -p maelstrom-core --test minigame_domain_tests`: 9/9 passed.
- `cargo test -p maelstrom-core --test predictive_rollout_tests`: 7/7 passed.
- `cargo test -p maelstrom-core --test combat_turn_evaluator_tests`: 11/11 passed.
- `cargo test --workspace`: 100% tests passing across all crates.
- `cargo clippy -p maelstrom-core --all-targets -- -D warnings`: 0 warnings.
- `python tools/check_project_board.py`: 30/30 workstreams valid.
- `python tools/check_text_encoding.py`: 0 violations.
