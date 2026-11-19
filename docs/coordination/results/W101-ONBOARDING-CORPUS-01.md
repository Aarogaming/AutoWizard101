# Evidence Report: W101-ONBOARDING-CORPUS-01

## 1. Summary
Ingested and formalized the comprehensive Wizard101 Onboarding Context Corpus into documentation (`docs/W101_ONBOARDING_CONTEXT_CORPUS.md`, `docs/INDEX.md`) and codified deterministic operational types, zero-waste exit evaluators, puzzle policies, and the exact damage pipeline formula in Rust (`crates/maelstrom-core/src/minigame_domain.rs`, `crates/maelstrom-core/tests/minigame_domain_tests.rs`).

## 2. Ingested Specifications & Implementations

### Documentation (`docs/W101_ONBOARDING_CONTEXT_CORPUS.md`)
1. **Pet Pavilion Mini-Games**:
   - Monotonic energy cost scaling: 2 Energy (Baby) $\rightarrow$ 14 Energy (Ultra).
   - Hard cap of 4 Pet XP (8 during Double Pet XP events); zero-waste exit invariant upon hitting threshold.
   - Exact input mechanics, scoring targets, optimal agent strategies, and automation tiers across Dance Game (Tier 1), Gobbler Drop (Tier 2), Maze Game (Tier 2), Cannon Game (Tier 3), Grumpy Gobblers (Tier 4), and Way of the Ninja Pig (Excluded).
   - Post-game snack feeding rules converting snack attributes into Strength, Intellect, Agility, Will, Power, and bonus XP.
2. **Fairegrounds & World Mini-Games**:
   - Fairegrounds games (Potion Motion, Doodle Dug, Skull Riders, etc.) refilling blue mana wisps and potion bottles on exit.
   - Silver Chest puzzle rules: Shock-A-Lock (tile rotation conduit) and Catch-A-Key / Lockpick (timing oscillator).
   - Policy: Hard maximum of 2 retries per chest, followed by coordinates logged to a 300s TTL cooldown table.
   - Cantrip chests: Solo (1 wizard) vs Three-Lock (3 wizards casting simultaneously within 5s window).
3. **Combat Onboarding Spec**:
   - Turn initiative (First vs Second) and Solo Street scaling (`Players + 1` rule from Colossus Boulevard onward).
   - Exact multiplicative damage calculation equation with order-dependent pipeline operations.
   - LIFO stack resolution and duplicate spell source stacking requirements.
   - Weakness/shield clearing with 0-pip off-school wand hits.
   - Target selection & threat arbitration (Slot 1 default, damage vs healing threat weighting).
   - Mob-clearing heuristic (1–2 blades into school AoE) and combat flee recovery invariants (mana drops to 0, teleports to Commons).
4. **Zero-Mouse Navigation Shortcuts**:
   - Comprehensive keyboard mapping (`W/A/S/D`, `Q/E`, `X`, `Spacebar`, `Ctrl+T`, `Ctrl+Shift+Q`, `N`, `Home`, `End`, `Page Down/Page Up`, `Ctrl+O`, `Ctrl+K`, `Ctrl+M`).
5. **Curated Onboarding Tutorials & Reference Links**:
   - Official KI onboarding sequence, Central Pet Pavilion mechanics, Final Bastion combat/deck templates, and silver chest guides.

### Rust Codification (`crates/maelstrom-core/src/minigame_domain.rs`)
- `PetAgeBracket`: `energy_cost()` (2..14), `max_base_xp(is_double_xp)`.
- `PetPavilionMiniGame`: `priority_tier()`, `is_recommended_for_automation()`, `input_modality()`.
- Zero-Waste Evaluators:
  - `evaluate_dance_game_exit`: 3 rounds for `PromenadeWinOnly`; 5 rounds for `MaxPetXp`.
  - `evaluate_maze_game_exit`: Stops and avoids +10s clocks; exits immediately at 70 snacks.
  - `evaluate_gobbler_drop_exit`: Exits immediately upon reaching target score, skipping clocks.
- `SilverChestPolicy`: Tracks attempts, caps retries at 2, enforces cooldown requirement.
- `CantripChestType`: Validates solo vs 3-lock required player counts.
- `DamagePipelineInputs` & `calculate_pipeline_damage`: Evaluates exact mathematical formula:
  $$\text{Damage} = \left\lfloor \left(\text{Base} + \text{Enchant}\right) \times \left(1.0 + \text{Gear\%}\right) + \text{FlatDmg} \right\rfloor \times \left(1.0 + \text{Aura}\right) \times \prod \left(1.0 + \text{Charm}\right) \times \left(1.0 + \text{Global}\right) \times \prod \left(1.0 + \text{Ward}\right) \times \text{CritMult} \times \left(1.0 - \text{Resist} + \text{Pierce}\right)$$

## 3. Verification & Test Evidence
- **Integration Tests (`crates/maelstrom-core/tests/minigame_domain_tests.rs`)**:
  - `test_pet_age_energy_scaling_and_max_xp`: Passed.
  - `test_pet_pavilion_mini_game_tiers`: Passed.
  - `test_dance_game_zero_waste_exit`: Passed.
  - `test_maze_game_zero_waste_exit`: Passed.
  - `test_gobbler_drop_zero_waste_exit`: Passed.
  - `test_silver_chest_retry_policy_and_cooldown`: Passed.
  - `test_cantrip_chest_solo_vs_multi_player_check`: Passed.
  - `test_exact_damage_calculation_pipeline`: Passed (tested multi-buff Efreet calculation: 27,906 damage).
- **Workspace Test Suite**: 100% tests passing across all crates (`cargo test --workspace`).
- **Code Standards**:
  - `cargo clippy -p maelstrom-core --all-targets -- -D warnings`: 0 warnings.
  - `rustfmt --edition 2021`: Clean.
  - `python tools/check_project_board.py`: 30 workstreams valid.
  - `python tools/check_text_encoding.py`: 0 violations.
