# W101-BUILD-PROFILE-01 — Research: Requirements for a Reviewed Combat Field Profile for Client Image d410a246... (REPORT-ONLY)

Status: **RESEARCH ONLY — static architectural and offset research. NO SOURCE EDITS, NO BUILDS, NO TEST RUNS, NO LIVE SESSIONS, NO PROMOTION TO QUALIFIED.**  
Date: 2026-11-17  
Auditor / Author: `gemini`  
Workstream: `W101-COMBAT`  
Directive: `LIA-0131`  
Active Claim: `W101-BUILD-PROFILE-01`  
Workspace: `F:\Modlists\AutoWizard101`  
Branch: `maelstrom/rust-architecture-v1`  
Committed Tree HEAD: `827030d`  

---

## Executive Summary

This research report investigates why Wizard101 client image `d410a246...` produced visible combat UI nodes but **0 combat field candidates** (`CardCandidates == 0`, `ParticipantCandidates == 0`, `Ready: false`) across two independent boss battles in Khrysalis, responding directly to directive `LIA-0131`.

### Key Root-Cause Finding
The absence of combat field telemetry is caused by a **dual architectural gating gap** in the observation pipeline:
1. **Tree-Binding Gate**: In [`tools/w101_ui_tree.py:462`](file:///f:/Modlists/AutoWizard101/tools/w101_ui_tree.py#L462), `self.current_layout` is strictly defined as `profile == "r13-current"`. Client image `d410a246...` maps to `r15-archived` ([`tools/w101_ui_tree.py:109–110`](file:///f:/Modlists/AutoWizard101/tools/w101_ui_tree.py#L109)), setting `self.current_layout = False`. Consequently, lines 1235 and 1395 bypass combat control binding and field extraction entirely.
2. **Missing Layout Specification**: In [`tools/w101_combat_reader.py:51–56`](file:///f:/Modlists/AutoWizard101/tools/w101_combat_reader.py#L51), the `_LAYOUTS` catalog contains only two legacy configurations (`"archived-wizwalker"` and `"deimos-development-df2283f"`). There is **no profile defined for `d410a246...`**, and line 1397 of `w101_ui_tree.py` hardcodes the layout ID to `deimos-development-df2283f`.

This report specifies the exact technical requirements to construct a reviewed combat field profile for `d410a246...`: the required binary pin, layout identifier, the 9 field offsets conforming to `w101_combat_reader._Layout`, the C++ `PropertyClass` virtual type stubs, the diagnostic tooling pipeline (`wiztype`), and a 4-stage qualification gate pipeline.

> [!IMPORTANT]
> **THIS CLIENT BUILD IS NOT QUALIFIED FOR COMBAT READING.**  
> This document defines requirements only. No source code has been altered, no memory offsets have been guessed, and no new fact IDs are marked available.

---

## 1. Primary Evidence & Root Cause Analysis

### 1.1 Reproducible Ground-Truth Observations
Two separate owner-authorized boss battle observations in Khrysalis recorded full combat planning phases, both exhibiting the exact same failure mode:

| Observation Run | Zone Candidate | Receipt SHA-256 | Distinct Nodes | Visible Combat Nodes | Combat Field Candidates | Primary Evidence Report |
|---|---|---|---|---|---|---|
| **Pass 1** | `Khrysalis/Interiors/KR_Z10_I05_DryGulchCave` | `a26b8b8ecc314bac8503ba981bb15df8` | 136 | `CombatantControl`, `Health`, `Pips`, `ShadowPips` (18/25), `Hand`, `Card1`..`Card7`, `SpellSelection`, `Target` (9/25) | 0 cards, 0 participants (`Ready: false`, `Missing: ["fresh-combat-observation"]`) | [`docs/coordination/results/W101-COMBAT-UI-NAMING-01.md:8–21`](file:///f:/Modlists/AutoWizard101/docs/coordination/results/W101-COMBAT-UI-NAMING-01.md#L8) |
| **Pass 2** | `Khrysalis/Interiors/KR_Z10_I05_AlluviumCave` | `1d26e3a4819e4cbf97750e3cad7435b5` | 139 | `CombatantControl`, `Health`, `Pips`, `ShadowPips` (21/25), `Hand`, `Card1`..`Card7` (6/25) | 0 cards, 0 participants (`Ready: false`, `Missing: ["fresh-combat-observation"]`) | [`docs/coordination/results/W101-COMBAT-UI-NAMING-01.md:38–47`](file:///f:/Modlists/AutoWizard101/docs/coordination/results/W101-COMBAT-UI-NAMING-01.md#L38) |

### 1.2 The Code Execution Trace
Tracing the execution in [`tools/w101_ui_tree.py`](file:///f:/Modlists/AutoWizard101/tools/w101_ui_tree.py) reveals why 0 candidates were returned despite visible UI nodes:

```
[Target Client Image: d410a246...]
         │
         ▼
tools/w101_ui_tree.py:109-110: profile_for(image)
         │
         ├─► profile = "r15-archived"
         ├─► entry = bytes.fromhex("49 8b 8f d8 00 00 00")
         └─► scratch_size = 3900
         │
         ▼
tools/w101_ui_tree.py:462-463: Layout Flags Initialized
         │
         ├─► self.current_layout = (profile == "r13-current")  ==> FALSE
         └─► self.archived_profile = (profile == "r15-archived") ==> TRUE
         │
         ▼
tools/w101_ui_tree.py:1235: Control Binding Evaluation
         │
         └─► if current_layout and combat_reader is not None:
                 [SKIPPED because current_layout is FALSE]
                 ==> combat_bindings list remains EMPTY (len == 0)
         │
         ▼
tools/w101_ui_tree.py:1395: Combat Field Extraction
         │
         └─► if include_combat_context and current_layout and combat_reader is not None:
                 [SKIPPED because current_layout is FALSE]
                 ==> fields = None
         │
         ▼
tools/w101_ui_tree.py:1424: Returned Payload
         │
         └─► return { ..., "combatFieldCandidates": None }
```

Even if `current_layout` were forced to `True`, line 1397 explicitly executes:
```python
fields = await combat_reader.read_combat_fields(
    bounded, combat_bindings, layout_id="deimos-development-df2283f"
)
```
Because `deimos-development-df2283f` was calibrated for a different build (`544ab726...`), attempting to read memory at those offsets would fail closed via `_Rejected` exceptions (invalid pointer alignment, vtable type mismatch, or read budget exhaustion).

---

## 2. Technical Profile Specification for Build `d410a246...`

To enable reviewed combat field extraction on this build, a dedicated profile must be formally qualified and registered.

### 2.1 Image Pin Identity
- **Binary Image Name**: `WizardGraphicalClient.exe`
- **Image SHA-256 Digest**: `d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a`
- **UI Tree Profile ID**: `r15-archived` ([`tools/w101_ui_tree.py:110`](file:///f:/Modlists/AutoWizard101/tools/w101_ui_tree.py#L110))
- **Verified Entry Bytecode**: `49 8b 8f d8 00 00 00` (`mov rcx, [r15 + 0xD8]`)
- **Autobot Scratch Size**: `3900` bytes
- **Hook Structure**: Standard `RootWindowHook` without the dynamic scratch pattern override required by `r13-current`.

### 2.2 Proposed Combat Layout Identifier
- **Layout ID String**: `"archived-r15-d410a246"` (or `"r15-archived"`) in `tools/w101_combat_reader._LAYOUTS`.
- **Decoupling Rule**: `tools/w101_ui_tree.py` must decouple layout identification from the boolean `current_layout`. Profile selection should pass the specific resolved layout ID matching `target["imageSha256"]`.

---

## 3. Field Definitions Matching `w101_combat_reader._Layout`

In [`tools/w101_combat_reader.py:37–47`](file:///f:/Modlists/AutoWizard101/tools/w101_combat_reader.py#L37), every layout profile is defined by an immutable dataclass `_Layout`. The table below outlines the 9 fields, comparing existing historical layouts against the requirements for `d410a246...`:

| Field Name | Type | Target Class & Offset Semantics | Value in `archived-wizwalker` | Value in `deimos-development-df2283f` | Requirement for `d410a246...` Profile |
|---|---|---|---|---|---|
| `spell_pointer` | `int` | Offset from `SpellCheckBox` base to `GraphicalSpell*` pointer. | `952` (`0x3B8`) | `1104` (`0x450`) | **Must be dumped** via `SpellCheckBox` type inspection. |
| `grayed` | `int` | Offset from `SpellCheckBox` base to 1-byte boolean (card uncastable / grayed out). | `1024` (`0x400`) | `1208` (`0x4B8`) | **Must be dumped**; determines `grayedCandidate`. |
| `checked` | `int \| None` | Offset from `SpellCheckBox` base to 1-byte boolean (checkbox checked / card selected). | `None` | `1028` (`0x404`) | **Must be dumped** or set to `None` if unverified. |
| `participant_pointer` | `int` | Offset from `CombatantDataControl` base to `CombatParticipant*` pointer. | `1656` (`0x678`) | `1888` (`0x760`) | **Must be dumped** via `CombatantDataControl` type inspection. |
| `spell_id` | `int` | Offset from `GraphicalSpell` base to 4-byte uint32 `spell_id`. | `272` (`0x110`) | `204` (`0xCC`) | **Must be dumped** via `GraphicalSpell` type inspection. |
| `treasure` | `int` | Offset from `GraphicalSpell` base to 1-byte boolean (`is_treasure_card`). | `265` (`0x109`) | `197` (`0xC5`) | **Must be dumped**; distinguishes TC from regular deck spells. |
| `template_type` | `int` | Offset from `SpellTemplate` base to `std::string` header for `spell_type`. | `312` (`0x138`) | `352` (`0x160`) | **Must be dumped**; decodes string like `"Attack"`, `"Enchantment"`. |
| `health` | `int` | Offset from `CombatParticipant` base to `<ii` (`current_health`, `max_health`). | `236` (`0xEC`) | `244` (`0xF4`) | **Must be dumped**; followed at `+8` by 1-byte `hidden_health`. |
| `pip_pointer` | `bool` | True if `participant + 152` is a `PipCount*` pointer; False if inline 3-byte layout. | `False` | `True` | **Must be verified** via type stub on pointer at `+152`. |

---

## 4. C++ Sub-Object Type Stubs & Layout Invariants

Beyond the 9 scalar offsets in `_Layout`, `w101_combat_reader.py` validates internal memory structures using strict structural invariants:

### 4.1 `PropertyClass` Type-Name Verification Stub
The reader does not guess RTTI or rely on unstable heuristic offsets. At [`tools/w101_combat_reader.py:117–130`](file:///f:/Modlists/AutoWizard101/tools/w101_combat_reader.py#L117), `_Reader.typed(address, expected)` verifies the documented KingsIsle `PropertyClass` virtual method:
1. `vtable = ptr(address)` $\rightarrow$ reads object vtable pointer.
2. `function = ptr(vtable + 8)` $\rightarrow$ reads slot 1 of vtable.
3. If first byte is `0xE9` (jmp), resolves relative 32-bit jump target.
4. Checks that bytes at `function + 63` are `\x48\x8d\x0d` (`lea rcx, [rip + disp32]`).
5. Evaluates `name_address = function + 70 + disp32` and asserts that the null-terminated ASCII string matches the expected class name exactly.

**Required Class Names that MUST exist in Build `d410a246...`**:
- `SpellCheckBox`: Root UI control for hand cards.
- `GraphicalSpell`: Dynamic spell instance bound to the card control.
- `SpellTemplate`: Static template containing spell metadata and localized name.
- `SpellEffect`: Polymorphic effect nodes within `GraphicalSpell.effects`.
- `CombatantDataControl`: Root UI control for combat participants.
- `CombatParticipant`: Dynamic combatant entity (player or mob).
- `PipCount`: Separate pip container (if `pip_pointer == True`).

### 4.2 Invariant Secondary Offsets in Sub-Objects
If build `d410a246...` shifted struct members, the reader's hardcoded secondary offsets must also be verified:
- **UI Control Flags**: `control + 156` (uint32 bit 0 indicates visible; bit 31 indicates disabled).
- **`GraphicalSpell` Invariants**:
  - `spell + 80`: uint32 `enchantment_id`.
  - `spell + 88`: `std::vector<SpellEffect*>` header (`<QQ` start, end pointers; max 8 effects).
  - `spell + 120`: pointer to `SpellTemplate`.
  - `spell + 128`: identity header (`<IB3xI`: `template_id`, `accuracy`, `school_id`).
- **`SpellTemplate` Invariants**:
  - `template + 96`: `std::string` header for spell name.
- **`CombatParticipant` Invariants**:
  - `participant + 112`: `<QQ` (`owner_id`, `template_id`), byte at `+16` (`is_player`).
  - `participant + 144`: `<iI` (`team_id`, `school_id`).
  - `participant + 152`: Pip data block.
- **`SpellEffect` Invariants**:
  - `effect + 72`: 88-byte payload (`<iiiI` `effect_type`, `parameter`, `disposition`, `school_id`; `+68` `<iii` `target`, `rounds`, `per_round`; `+85` byte `cloaked`).

---

## 5. Type and Offset Dump Prerequisites

No offsets may be guessed by trial and error. The required offsets must be extracted from primary artifacts using documented tools:

### 5.1 Tooling: `wiztype` Diagnostic Tool
- **Status in Tree**: `wiztype 0.1.13` is cataloged and installed in a dedicated Python environment (`.local/upstream/wiztype`), integrated via [`tools/wizard101_hub.py:68–74`](file:///f:/Modlists/AutoWizard101/tools/wizard101_hub.py#L68).
- **Policy Classification**: Classified under `ProcessMemoryObservation` in [`ProjectMaelstrom/ProjectMaelstrom/Utilities/W101InteractionPolicy.cs:10`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/W101InteractionPolicy.cs#L10) and [`ProjectMaelstrom/ProjectMaelstrom/Utilities/ToolIntegrationService.cs:40`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/ToolIntegrationService.cs#L40).
- **Prerequisite for Execution**: Requires an active, foregrounded `WizardGraphicalClient.exe` instance with binary SHA-256 `d410a246...` and explicit user authorization (`ALLOW_LIVE_AUTOMATION = true`).
- **Output Artifact**: A versioned JSON type dump:
  ```powershell
  python tools/wizard101_hub.py launch wiztype --version 2 --output .local/artifacts/client-types-d410a246.json
  ```
- **Offline Comparison**: [`tools/wizard101_hub.py:585`](file:///f:/Modlists/AutoWizard101/tools/wizard101_hub.py#L585) provides `compare-types` to automatically diff property offsets between `client-types-d410a246.json` and historical Deimos/wizwalker baselines.

### 5.2 Pinned Memory Reader Diagnostic Alternative
If full type reflection is unavailable, an owner-supervised read-only diagnostic script can inspect the verified C++ class instances directly:
1. Locate live `SpellCheckBox` and `CombatantDataControl` memory addresses from the saved UI tree.
2. Read vtable slot 1 to verify `PropertyClass` ASCII names.
3. Perform memory scans within the object bounds (`0x000` to `0x800`) to pinpoint pointers resolving to `GraphicalSpell`, `SpellTemplate`, and `CombatParticipant`.

---

## 6. Four-Stage Qualification Gate Pipeline

A candidate combat field profile cannot be committed to production without passing four sequential verification gates:

```
┌─────────────────────────────────┐
│ GATE 1: STATIC DUMP AUDIT       │  Type dump confirms class names & offsets
└────────────────┬────────────────┘
                 │
                 ▼
┌─────────────────────────────────┐
│ GATE 2: SYNTHETIC FIXTURE TESTS │  test_w101_combat_reader.py passes 100%
└────────────────┬────────────────┘
                 │
                 ▼
┌─────────────────────────────────┐
│ GATE 3: RECORDED TREE REPLAY    │  25 saved trees in receipts a26b... & 1d26...
│                                 │  yield CardCandidates > 0 & Participants > 0
└────────────────┬────────────────┘
                 │
                 ▼
┌─────────────────────────────────┐
│ GATE 4: FACT INTEGRATION        │  W101-BACKGROUND-API-01: candidate facts
│                                 │  remain ProducerAvailable = false until live
└─────────────────────────────────┘
```

1. **Gate 1: Static Type Dump Consistency**:
   - Machine-verifiable JSON type dump proving offsets for `SpellCheckBox`, `CombatantDataControl`, `GraphicalSpell`, `SpellTemplate`, `CombatParticipant`, and `PipCount`.
2. **Gate 2: Synthetic Fixture Unit Tests**:
   - Layout added to `_LAYOUTS` in [`tools/w101_combat_reader.py`](file:///f:/Modlists/AutoWizard101/tools/w101_combat_reader.py).
   - Fixture test in [`tools/tests/test_w101_combat_reader.py`](file:///f:/Modlists/AutoWizard101/tools/tests/test_w101_combat_reader.py) verifying card decoding, effect parsing, participant health, and pip extraction without `_Rejected` exceptions.
3. **Gate 3: Offline Recorded Tree Replay**:
   - Replay against the 25 recorded trees in receipt `a26b8b8ecc314bac8503ba981bb15df8` (Dry Gulch Cave) and 25 trees in receipt `1d26e3a4819e4cbf97750e3cad7435b5` (Alluvium Cave).
   - Must decode at least 7 hand cards during visible planning phases (`SchoolPipPanel` visible) and at least 2 combat participants (player + boss) with valid non-zero max health.
4. **Gate 4: Read-Only Background Facts Gate**:
   - Integration into [`docs/coordination/tasks/W101-BACKGROUND-API-01.md`](file:///f:/Modlists/AutoWizard101/docs/coordination/tasks/W101-BACKGROUND-API-01.md).
   - Candidate combat facts default to `ProducerAvailable = false`. No fact is promoted to `qualified` without an owner-authorized live gameplay run.

---

## 7. Balance Section: Verified Facts vs Proposals

To ensure strict compliance with `LIA-0122` and `LIA-0131`, this table distinguishes verified primary source facts from proposed architectural design:

| Feature / Artifact / Observation | Status in Committed Tree (HEAD `827030d`) | Status in this Research Brief |
|---|---|---|
| **Client Image SHA-256 Pin** | ✅ **VERIFIED**: `d410a246...` is committed as `ARCHIVED_LAYOUT_IMAGE_SHA256` in [`tools/w101_ui_tree.py:101–102`](file:///f:/Modlists/AutoWizard101/tools/w101_ui_tree.py#L101). | Retained as the exact immutable binary target pin. |
| **Profile Mapping to `r15-archived`** | ✅ **VERIFIED**: `profile_for("d410a246...")` returns `"r15-archived"` ([`tools/w101_ui_tree.py:110`](file:///f:/Modlists/AutoWizard101/tools/w101_ui_tree.py#L110)). | Retained as the active UI tree profile. |
| **`current_layout == False` Gating** | ✅ **VERIFIED**: Line 462 sets `current_layout = False`, causing lines 1235 and 1395 to skip combat bindings and field reads. | Documented as the primary root-cause gating gap. |
| **0 Candidates in Boss Battles** | ✅ **VERIFIED**: Passes `a26b8b8...` and `1d26e3a...` produced 136/139 node names but 0 field candidates ([`W101-COMBAT-UI-NAMING-01.md`](file:///f:/Modlists/AutoWizard101/docs/coordination/results/W101-COMBAT-UI-NAMING-01.md)). | Cited as primary experimental evidence. |
| **`_LAYOUTS` in `w101_combat_reader.py`** | ✅ **VERIFIED**: Contains exactly two layouts: `archived-wizwalker` and `deimos-development-df2283f`. | Proves that build `d410a246...` lacks a layout profile. |
| **Specific Offsets for `d410a246...`** | ❌ **UNVERIFIED / ABSENT**: No committed type dump or verified offset table exists for `d410a246...`. | Defined as required research; offsets are NOT guessed. |
| **Layout ID `"archived-r15-d410a246"`** | ⚠️ **PROPOSED ONLY**: Does not exist in `tools/w101_combat_reader.py`. | Proposed as the formal profile identifier. |
| **`wiztype` Execution on `d410a246...`** | ❌ **UNATTEMPTED**: `wiztype` CLI help verified, but live client dump was never executed. | Mapped as the required Stage 1 diagnostic prerequisite. |
| **Combat Fact Availability** | ⚠️ **PROPOSED ONLY**: Combat field facts remain `ProducerAvailable = false`. | Preserved fail-closed; no fact marked available. |

---

## 8. Certification of Operating Boundaries

This report was produced under strict report-only operating boundaries:
- **0** source code files modified.
- **0** test runs or builds executed.
- **0** live game, process memory, desktop, or background sessions initiated.
- **0** modifications to `docs/coordination/claims.json` or project board state.
- All numbers, hashes, receipts, node names, and citations were extracted directly from committed primary source artifacts at HEAD (`827030d`).
