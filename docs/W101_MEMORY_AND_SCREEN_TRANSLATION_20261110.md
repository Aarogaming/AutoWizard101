# Wizard101 Memory and Screen Translation Architecture

Status: Architectural documentation and offline integration report. 2026-11-10.

Under the 2026-11-10 W101 client-side exception, Maelstrom can leverage read-only process memory observation to translate game state and rendered UI elements into structured, zero-latency inputs for automation.

---

## 1. What Can Be Extracted from Process Memory

Wizard101's client (`WizardGraphicalClient.exe`) maintains a rich, strongly-typed object graph in user-mode memory. Through reviewed memory structures (derived from `wizwalker` and `Deimos`), the following ground-truth data points can be extracted:

### A. Dynamic UI Hierarchy & Exact Screen Coordinates (`DynamicWindow`)
- **Root Window Tree:** Every rendered dialog, button, HUD element, and card slot descends from a `root_window` object in memory.
- **Pixel-Perfect Screen Rectangles:** Calling `scale_to_client()` on any `DynamicWindow` traverses parent transforms and the UI scale factor to compute the exact `(x, y, width, height)` client window rectangle where that element is currently rendered.
- **Element State:** Element visibility flags, clickable states, and text labels (e.g. "Pass", "Health", "Name") can be read directly, eliminating guesswork from visual occlusions or anti-aliasing variations.

### B. Real-Time Combat State (`CombatParticipant` & `Duel`)
- **Player & Enemy Resources:**
  - `current_hp` and `max_hp` for player, allies, and all enemies.
  - `num_pips`, `num_power_pips`, and `num_shadow_pips`.
  - `is_dead`, `stunned`, and duel subcircle slot (positions 1–4).
- **Hanging Effects (Blades, Traps, Shields, Weaknesses):**
  - Read via the participant's `hanging_effects` linked list (`DynamicSpellEffect`).
  - Provides exact damage multiplier percentages (e.g. +35% Fireblade, -50% Tower Shield), school types, and rounds remaining.
- **Cards in Hand:**
  - Drawn cards read via `Hand.spell_list` (`DynamicSpell`).
  - Supplies exact spell template ID, pip rank, applied enchantments (e.g. Monstrous, Colossal), and target disposition.

### C. Overworld Navigation & Entity Positioning (`ActorBody` & `QuestPosition`)
- **Player Kinematics:** Exact 3D coordinates $(X, Y, Z)$, pitch/yaw/roll orientation, movement speed, and collision bounds.
- **Quest Objectives:** Target destination $(X, Y, Z)$ of the active quest helper arrow, quest stage ID, and zone name.
- **World Context:** Current zone ID and world name via `client_zone`.
- **Surrounding Entities:** Coordinates and identities of nearby interactables (sigils, NPCs, reagents, wooden chests, roaming mob entities).

### D. Minigame State (Pet Dance, etc.)
- **Pet Dance Sequence Hook:** As demonstrated in `dance_game_hook.py`, the upcoming sequence of arrow keys (Up, Down, Left, Right) is populated in the minigame controller memory prior to animation playback.

---

## 2. Translating Memory and Screen Data into Useful Inputs

Maelstrom integrates memory data using a **Sensor Fusion** model:

```
[ W101 Process Memory ]       [ Game Window Screen Capture ]
          │                                 │
  (Read-Only Telemetry)            (Visual Templates & OCR)
          │                                 │
          └─────────────┬───────────────────┘
                        ▼
             [ Maelstrom Sensor Fusion ]
          (Ground-Truth State + Coordinates)
                        │
                        ▼
             [ Routine Decision Engine ]
        (Turn Planner / Pathing / Minigames)
                        │
                        ▼
             [ Shared Input Lease ]
           (Exclusive Action Ownership)
                        │
                        ▼
             [ Bounded Desktop Inputs ]
      (Human-like clicks, keystrokes, releases)
```

1. **Click Target Resolution:**
   Instead of searching for template bitmaps with sliding scale factors, the planner queries the target `DynamicWindow` (e.g. card in hand slot 2 or enemy 1 health banner), acquires its exact client screen rectangle, and dispatches a bounded mouse click via Maelstrom's `PlayerController`.

2. **Zero-Latency Turn Decisions:**
   In combat, memory telemetry instantly feeds the player's pip count, active blades, enemy shields, and hand cards to `CombatTurnPlanner` without waiting for OCR frames to settle.

3. **Fallback Invariant (UI-Only Baseline):**
   If a game patch updates memory offsets before they are remapped, Maelstrom gracefully falls back to template matching (e.g. `CombatUtils.cs` and `vision_combat_adapter.py`) without breaking routine execution.
