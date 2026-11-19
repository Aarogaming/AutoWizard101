# Pre-Battle Deck Profile Switching & Spellbook Verification

> **Status: design note, partly aspirational (audit 2026-11-19, Claude).** In code today: the `DeckProfileTag` enum, five GOAP
> action constructors (`EnsureDeckProfile`, `EnterSigil`, `PullMob`, `RetreatToSafeZone`, `DeckSwapShortcut`) and the
> coordinate constants of Section 3 (`crates/maelstrom-core/src/goap.rs`, `combat_geometry.rs`); nothing calls them and
> nothing was run against the game. **Not implemented:** the 40.0 and 25.0 distance checks (the code has only a boolean
> `safe_from_mobs`), the "profile != target" precondition and zero cost in Section 4 (preconditions are equality-only and
> the action costs 5.0), `HealthAbove(0.80)`, the Section 5 latch, `HardInterrupt` and `AvoidMobAggro`, and the Spellbook
> open, navigate, equip and close sequence. **Unsourced:** the game-mechanic statements and card names in Sections 1 and 2,
> and the bounding boxes in Section 3. Details:
> [W101-DECK-SWITCH-DOCS-01, Audit note](coordination/results/W101-DECK-SWITCH-DOCS-01.md).

## 1. Operational Rationale & Invariant Requirements

In Wizard101, deck composition is strictly **locked upon entering a combat sigil or duel circle**. A player cannot swap decks, add treasure cards, or alter card counts once the duel initiation fanfare sounds. 

Entering a street mob encounter with a single-target boss setup results in inefficient, high-turn combat, while entering a boss encounter with a generic AoE deck risks exhaustion against high-health bosses or immune same-school opponents.

Therefore, optimal combat execution requires **Pre-Battle Deck Profile Switching** governed by the following strict architectural invariants:

1. **Pre-Combat Safe Zone Verification**:
   Opening the Spellbook (`P`) pauses player movement and obscures the visual field. Initiating deck modification while within range of dynamic mob patrols creates an unacceptable risk of involuntary combat collision. Deck switching is permitted **only** when:
   $$\text{in\_combat} = \text{false} \quad \land \quad d_{\text{min}}(\mathbf{p}_{\text{player}}, \mathbf{M}) \ge \text{safe\_margin} \quad (\ge 40.0\text{ units})$$
2. **Deterministic Encounter Tag Matching**:
   The planner or quest interpreter resolves the upcoming encounter type from the active quest archetype or target entity metadata, selecting one of four canonical deck profiles:
   - `MobAoE`: Low-pip buff (Blade) + 4-pip AoE card (Meteor, Tempest, Blizzard, Sandstorm) for multi-mob street clearing.
   - `BossFeint`: Stacked buffs (Blade + Trap + Feint) + high-damage single-target finisher for boss sigils.
   - `SchoolPrism`: Mandatory conversion prism slotted when the target boss shares the player's primary school to bypass 80%+ natural resistance.
   - `HealSupport`: Sustain cards (Pixie, Fairy, Satyr) prioritized for solo dungeon instances.
3. **Spellbook (`P`) UI Navigation & Confirmation**:
   Actuation proceeds via discrete, verified state transitions under the `GameInputLease`:
   - Open Spellbook via `P` pulse.
   - Verify Spellbook UI frame presence.
   - Navigate to Deck Management tab (Tab 3).
   - Verify or equip target deck profile.
   - Close Spellbook via `P` or `Esc` pulse and verify return to fluid navigation state.

---

## 2. Canonical Deck Profile Taxonomy

| Profile Tag | Target Encounter Archetype | Card Distribution Archetype | Target Turn Horizon |
| :--- | :--- | :--- | :--- |
| `MobAoE` | Street mobs, 2-mob pulls | 2x School Blade, 2x 4-Pip AoE, 1x Reshuffle, 2x Wand Hits | 2–3 Turns |
| `BossFeint` | Tower bosses, sigil instances | 2x Blade, 2x Feint, 1x Trap, 2x Single-Target Hit, 1x Pixie | 4–6 Turns |
| `SchoolPrism` | Same-school bosses / elites | 2x Prism, 2x Blade, 1x Feint, 2x Single-Target Hit | 5–7 Turns |
| `HealSupport` | Gauntlet / multi-wave bosses | 3x Heal, 2x Shield/Tower, 2x Blade, 1x Finisher | 6–10 Turns |

---

## 3. Spellbook UI Geometry & Resolution Anchors

Spellbook coordinates scale predictably across Wizard101's standard 4:3 (800x600) and 16:9 (1280x720) viewport calibrations:

### Standard Viewport Layout (Normalized & 1280x720)

| UI Element | Normalized $(x, y)$ | 1280x720 $(x, y)$ | Bounding Box $(w, h)$ |
| :--- | :--- | :--- | :--- |
| **Spellbook HUD Icon** | $(0.950, 0.933)$ | $(1216, 672)$ | $(48, 48)$ |
| **Spellbook Tab 1 (Character)** | $(0.220, 0.120)$ | $(282, 86)$ | $(40, 36)$ |
| **Spellbook Tab 2 (Spells)** | $(0.260, 0.120)$ | $(333, 86)$ | $(40, 36)$ |
| **Spellbook Tab 3 (Decks)** | $(0.300, 0.120)$ | $(384, 86)$ | $(40, 36)$ |
| **Active Deck Selector Left** | $(0.350, 0.180)$ | $(448, 130)$ | $(30, 24)$ |
| **Active Deck Selector Right**| $(0.650, 0.180)$ | $(832, 130)$ | $(30, 24)$ |
| **Deck Equip / Unequip Button**| $(0.500, 0.850)$ | $(640, 612)$ | $(120, 32)$ |
| **Spellbook Close (X) Button** | $(0.780, 0.120)$ | $(998, 86)$ | $(32, 32)$ |

---

## 4. GOAP Integration: `EnsureDeckProfile` Action

Within the Goal-Oriented Action Planning (GOAP) state-space search (`crates/maelstrom-core/src/goap.rs`), `EnsureDeckProfile` acts as an essential precondition gate for combat actions:

```
Goal: DefeatBossSigil(BossGlowfang)
  ^
  |-- Action: EnterCombatSigil
  |     Preconditions: AtSigil(true), DeckProfile(BossFeint), HealthAbove(0.80)
  |     Effects: InCombat(true)
  |
  |-- Action: EnsureDeckProfile(BossFeint)
  |     Preconditions: InCombat(false), SafeFromMobs(true), DeckProfile != BossFeint
  |     Effects: DeckProfile(BossFeint)
  |
  |-- Action: NavigateToSigil
        Preconditions: InCombat(false)
        Effects: AtSigil(true)
```

If the agent is already in the `BossFeint` profile, `EnsureDeckProfile` costs 0 and evaluates as already satisfied, avoiding unnecessary UI interruptions.

---

## 5. Behavior Tree Interaction & Preemption Safeguards

1. **State Atomic Latch**:
   When `EnsureDeckProfile` executes, the Behavior Tree sets `deck_switching_in_progress = true`.
2. **Adversarial Interrupt Handling**:
   - If an unexpected dynamic mob patrol approaches within $d < 25.0$ while the book is open, an immediate `HardInterrupt` pulses `Esc` to close the book and yields control to the `AvoidMobAggro` steering leaf.
   - If combat fanfare audio is detected via WASAPI reflex, the book state is immediately cleared and the system transitions to `CombatDuelingLoop`.
