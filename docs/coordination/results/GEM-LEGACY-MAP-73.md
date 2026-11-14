# Legacy Dance and Navigation Components Map (GEM-LEGACY-MAP-73)

## 1. Overview & Disclaimer
- **Evaluated Commit**: `595ef2c7b8423995de21b055e3712eef195507c8` (HEAD of `maelstrom/rust-architecture-v1`).
- **Audit Scope**: Read-only static cataloging of extracted legacy materials located in `ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/`.
- **Safety Disclaimer**: Imported legacy scripts and archives are **historical references**, not operational integrations. No legacy programs were installed or executed. Network manipulation, packet crafting, blind input injection, and idle evasion remain strictly excluded per `AGENTS.md` and `docs/W101_CLIENT_SIDE_EXCEPTION_20261110.md`.

---

## 2. Component Analysis & Classification

### 1. Deimos-Wizard101 (`auto_pet.py` & `dance_game_hook.py`)
- **Provenance**: `Scripts/Library/Deimos-Wizard101/src/` (Community open-source, MIT license).
- **Exact Upstream Symbols**:
  - `DanceGameMovesHook` / `activate_dance_game_moves_hook`: Bytecode pattern `\x48\x8B\xF8\x48\x39\x70\x10`, instruction length 7, reads 8 bytes from `_base_addrs["dance_game_moves"]` translated via `maketrans("abcd", "WDSA")`.
  - `navigate_to_dance_game`: Waypoints `goto(x=-1738.78, y=-387.35)` (Milo Barker), `goto(x=-4090.11, y=-1186.37)` (front of game), `goto(x=-4449.37, y=-992.00)` (Dance sigil).
  - `navigate_to_pavilion_from_commons`: Teleports to `XYZ(8426.38, -2165.70, -27.91)`, waits for zone change `WizardCity/WC_Hub`.
  - `nomnom`: Reads UI textbox strings `Energy: XX/YY`, evaluates `total_energy >= energy_cost`.
- **Resolution & Scale Assumptions**:
  - Memory hook is resolution-independent.
  - UI path queries (`pet_feed_window_visible_path`) depend on client UI widget tree.
- **Input Ownership & Client Dependencies**:
  - Direct `pymem` / `wizwalker` injection without shared input lease, cancellation, or user takeover detection.
- **Classification**:
  - **Teacher / Reference**: Exact Pet Pavilion Dance sigil coordinates (`XYZ(-4449.37, -992.00)`) and energy cost check logic.
  - **Missing Adapter (Optional Secondary Reader)**: The memory hook pattern (`\x48\x8B\xF8\x48\x39\x70\x10`) is authorized under the 2026-11-10 client-side exception as an optional read-only helper, provided primary visual tracking remains independently functional.
  - **Unsuitable (in raw form)**: Direct uncoordinated keyboard dispatch violates Maelstrom's `GameInputLease` architecture.
- **Maelstrom Equivalent**:
  - `ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceVisit.cs`
  - `ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceSequence.cs`
  - `crates/maelstrom-fallback/src/pet_dance.rs`
- **Smallest Next Step & Test Idea**:
  - Write an isolated memory reader fixture testing pattern scan against a synthetic memory buffer, verifying it safely handles missing/corrupted buffers with graceful fallback to visual tracking.

---

### 2. Legacy `petdance.zip` Template Archive (`PetDanceTemplates.cs`)
- **Provenance**: Extracted legacy Pet Dance template pack (historical community archive).
- **Exact Upstream Symbols**:
  - 8 PNG assets across `800x600` and `1280x800`: `Down.png`, `Left.png`, `Right.png`, `Up.png`.
- **Behavior**:
  - Direct pixel template matching of arrow cues on the Dance board.
- **Resolution & Scale Assumptions**:
  - Hardcoded to exact 800x600 and 1280x800 rendering; breaks under dynamic UI scaling or custom aspect ratios without interpolation.
- **Input Ownership & Client Dependencies**:
  - None (pure image assets).
- **Classification**:
  - **Direct Reuse (Already Integrated)**: Verified and hashed in `PetDanceAssets.Reviewed` inside `ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceTemplates.cs`.
- **Maelstrom Equivalent**:
  - `PetDanceAssets.Import(...)` in `ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceTemplates.cs`.
  - `ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceArrowShapeMatcher.cs`.
- **Smallest Next Step & Test Idea**:
  - Add multi-scale template matching unit tests asserting tolerance across non-standard DPI scales (e.g. 1920x1080 scaled down).

---

### 3. Automatus-v2 (`locomotion.py` & `mapping.py`)
- **Provenance**: `Scripts/Library/Automatus-v2-master/Automatus-v2-master/func/` (GPL-3.0).
- **Exact Upstream Symbols**:
  - `follow_map`: Iterates `point = map.next()`, maps `point.z == 0` to `S`, `point.z == 1` to `W`, `point.z == -1` to `X`, calls `bot.client.goto(point.x, point.y)`.
- **Resolution & Scale Assumptions**:
  - World coordinate navigation; resolution-independent.
- **Input Ownership & Client Dependencies**:
  - Direct async keypress sending (`bot.client.send_key`) without focus check or takeover detection.
- **Classification**:
  - **Teacher / Reference**: Sequential waypoint following and action-key triggers (`X` on reaching sigil) inform Maelstrom travel routines.
  - **Unsuitable (as runtime code)**: Lacks input lease ownership, recovery on stuck, or cancellation cleanup.
- **Maelstrom Equivalent**:
  - `ProjectMaelstrom/ProjectMaelstrom/Resources/Macros/commons-to-pet-pavilion.macro.json`
  - `ProjectMaelstrom/ProjectMaelstrom/Utilities/HomeMovementOperation.cs`
- **Smallest Next Step & Test Idea**:
  - Convert Automatus Pet Pavilion waypoint lists into deterministic JSON macro steps validated by `HomeMovementGateTests.cs`.

---

### 4. GrubNinja (`endpoints.js`, `feedSnack.js`, `metrics.js`)
- **Provenance**: `Scripts/Library/GrubNinja-master/` (Community Electron app).
- **Exact Upstream Symbols**:
  - `buyEnergy.js`, `feedSnack.js`, `fetchRewards.js`, `generateGameId.js`, `petSelectors.js`.
- **Behavior**:
  - Directly calls KingsIsle Grub Guardian web REST APIs to trigger minigame rewards and feed snacks out-of-game.
- **Resolution & Scale Assumptions**:
  - N/A (headless HTTP requests).
- **Input Ownership & Client Dependencies**:
  - Direct HTTP networking, bypassing the desktop game client completely.
- **Classification**:
  - **UNSUITABLE (Policy Violation)**: Web API replay and packet crafting violate `AGENTS.md` ("Network manipulation remains excluded").
  - **Teacher / Reference (Data Only)**: Pet stat formulas, talent requirements, and snack XP values (`petSelectors.js`, `metricSelectors.js`) can serve as reference data for offline calculations.
- **Maelstrom Equivalent**:
  - `ProjectMaelstrom/ProjectMaelstrom/Utilities/PetTrainingPreferences.cs`
  - `tools/pet_talent_calculator.py`
- **Smallest Next Step & Test Idea**:
  - Verify that snack database values in `pet_talent_calculator.py` match known canonical pet experience tables.

---

### 5. afk-wizard (`main.py`)
- **Provenance**: `Scripts/Library/afk-wizard-main/afk-wizard-main/` (Community anti-idle bot).
- **Exact Upstream Symbols**:
  - `wander_commands`: Alternates WASD pulses at random intervals to prevent idle logout.
  - `battle_cycle`: Hardcoded screen clicks at `(880, 610)` for pass and `(980, 610)` for spell.
- **Resolution & Scale Assumptions**:
  - Hardcoded to 1280x720 window.
- **Input Ownership & Client Dependencies**:
  - File-based input bridge (`commands.json`).
- **Classification**:
  - **UNSUITABLE / OBSOLETE**: Wandering or blind clicking to evade idle is contrary to Maelstrom core principles. Maelstrom explicitly requires return to **verified native Idle** (`"YOU ARE IDLE"` banner) through standard game menus, not spinning or anti-idle macros.
- **Maelstrom Equivalent**:
  - `ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceNativeIdle.cs`
  - `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101IdleCycleOperation.cs`
- **Smallest Next Step & Test Idea**:
  - No integration step needed; retain purely as negative anti-pattern reference.

---

## 3. Summary Map

| Legacy Component | Source Location | Provenance / License | Primary Behavior | Maelstrom Classification | Maelstrom Target Function |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Deimos Memory Hook** | `Deimos-Wizard101/src/dance_game_hook.py` | Open source (MIT) | Process memory read for dance moves (`\x48\x8B\xF8...`) | **Missing Adapter / Secondary Validator** | Optional helper for `PetDanceSequence.cs` / Rust bridge |
| **Deimos Sigil Waypoints** | `Deimos-Wizard101/src/auto_pet.py` | Open source (MIT) | Coordinates for Milo Barker & Dance sigil | **Teacher / Reference** | `commons-to-pet-pavilion.macro.json` |
| **petdance.zip Templates** | `PetDanceTemplates.cs` | Extracted community archive | 800x600 & 1280x800 arrow template bitmaps | **Direct Reuse (Integrated)** | `PetDanceAssets.Reviewed` in `PetDanceTemplates.cs` |
| **Automatus Waypoints** | `Automatus-v2-master/.../locomotion.py` | Open source (GPL-3.0) | Sequential `goto()` waypoint navigation | **Teacher / Reference** | `HomeMovementOperation.cs` |
| **GrubNinja Web Client** | `GrubNinja-master/.../endpoints.js` | Community app | Web API network simulation for minigames | **UNSUITABLE (Policy)** | None (Data reference for `pet_talent_calculator.py`) |
| **afk-wizard Anti-Idle** | `afk-wizard-main/.../main.py` | Community macro | WASD wander to evade idle logout | **UNSUITABLE (Obsolete)** | Superseded by verified `PetDanceNativeIdle.cs` |
