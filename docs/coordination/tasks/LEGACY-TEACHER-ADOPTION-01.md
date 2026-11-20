# LEGACY-TEACHER-ADOPTION-01 — Adoption Brief: Transforming Legacy Bots into Bounded Teachers (REPORT-ONLY)

Status: **PROPOSED / ADOPTION BRIEF — static architectural brief. NO CODE EDITS, NO TEST RUNS, NO LIVE QUALIFICATION.**  
Date: 2026-11-16  
Auditor / Author: `gemini`  
Workstream: `CORE-DELIVERY`  
Directive: `LIA-0127`  
Active Claim: `LEGACY-TEACHER-BLUEPRINT-CORRECTION-01`  
Workspace: `F:\Modlists\AutoWizard101`  
Branch: `maelstrom/rust-architecture-v1`  
Committed Tree HEAD: `67d41da`  

---

## Executive Summary

This brief provides a claim-ready engineering specification for adopting fragile legacy automation bots as reusable, offline "Teachers" within the Maelstrom runtime. It corrects seven audited defects identified in the earlier conversational blueprint, firmly establishes the owner's central "wrap-first, extract-model-second" design directive, anchors all abstractions in real committed contracts, and maps an actionable adoption plan for the first target teacher: the **Combat Turn Planning Teacher**.

---

## 1. The Owner's Central Design Directive (First Principle)

> **"Wrapping an existing working function is better than having no function. Once the wrapper works under Maelstrom's owner, reverse-engineer a working model FROM the working wrapper. Do NOT build a from-scratch replacement merely 'inspired by' the legacy bot."**

Legacy bots (`Deimos`, `Automatus`, `DanceBot`, `wizAPI`, `Wizard101-Farming`) contain fragile integration loops, but they also represent years of painstaking, reverse-engineered domain understanding (card coordinate heuristics, spell interaction rules, game phase transitions, and visual templates).

To avoid repeating failure modes, every legacy adoption task must adhere to a strict two-phase sequence:

```
PHASE 1: THE WRAP-FIRST SEAM                 PHASE 2: THE MODEL-EXTRACTION STEP
┌─────────────────────────────────┐          ┌───────────────────────────────────┐
│ Wrap the existing working       │          │ Reverse-engineer clean, pure math │
│ legacy function AS-IS behind    │  ─────►  │ or state logic FROM the working   │
│ Maelstrom's proposal contract   │          │ wrapper into native C# / Rust     │
│ (Even if Python / OpenCV script)│          │ (Locked by deterministic TSVs)    │
└─────────────────────────────────┘          └───────────────────────────────────┘
```

1. **Step A (The Wrap-First Seam)**:
   - Isolate the legacy bot's core decision routine into a bounded Python or C# wrapper.
   - Strip out global input side effects (`pyautogui`, Win32 `SendInput`, `mouse_event`).
   - Wire the wrapper to consume an observation and emit candidate proposals without touching the game or desktop.
   - Prove the wrapper runs offline against saved recordings or synthetic fixtures.
2. **Step B (The Model-Extraction Step)**:
   - Once the wrapper is proven functional and generating verifiable proposals under Maelstrom's owner, reverse-engineer the underlying decision model (e.g. lethal calculations, damage multipliers, geometric projections) directly *from the working wrapper*.
   - Re-implement that pure logic as an immutable, native C# or Rust component.
   - Lock behavioral parity using deterministic fixture rows before replacing the legacy script.

Building from-scratch components that are merely "inspired by" legacy tools is explicitly prohibited because it discards working edge-case handling and introduces uncalibrated assumptions.

---

## 2. Audit Balance: Corrections vs Preserved Truths

In accordance with directive `LIA-0127`, this brief resolves the seven defects identified in root's adversarial audit while preserving verified foundational findings:

| # | Defect in Conversational Blueprint | Correction Applied in this Brief | Primary Evidence / Citation |
|---|---|---|---|
| 1 | **Phantom Interfaces**: Cited `ITeacher`, `IOperationModule`, `ObservationContext`, `ActionProposal`, `ExpectedOutcome`, `CombatStrategyTeacher`, etc. | All phantom interfaces are explicitly labeled `[PROPOSED]` or replaced with real committed contracts (`RoutineGraph`, `RoutineReplay`, `W101UiTreeResult`, `GameInputLease`). | Tracked tree inspection: 0 occurrences of phantom symbols in C#/Python. |
| 2 | **Broken Path**: Cited `TestData/RustParity/dance-observations-v1.tsv`. | Corrected path to `ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/RustParity/dance-observations-v1.tsv`. Every path audited against filesystem. | Filesystem verification: file exists only under `ProjectMaelstrom.Tests/`. |
| 3 | **Misleading "Zero Live Crashes" Claim**: Claimed Dance had zero live crashes. | Corrected: No live Dance visit has ever been attempted (`DANCE-PRODUCTION-RUNTIME-01.md` title is explicitly `OFFLINE IMPLEMENTATION - NO LIVE QUALIFICATION`). Exactly one live UI observation occurred (2026-11-16), producing `CleanupFailed` (receipt `3657311ef2fc4551ad2c5ec6d0e0c682`), repaired offline in commit `7058fb2`. | `docs/coordination/results/W101-UI-OBSERVER-SHUTDOWN-01.md:8–13`. |
| 4 | **Frame Hashing Conflated with Outcome**: Implied SHA-256 frame hashing proved victory. | Corrected: SHA-256 frame hashing proves **evidence identity** (matching reference frames), not gameplay outcome. `GameVictoryVerified` is hardcoded `false` in `PetDanceLiveTrial.cs:105`. | `ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceLiveTrial.cs:105`. |
| 5 | **Pip Veto Claimed as Existing Feature**: Claimed scheduler vetoes spells when pips are low. | Corrected: No pip fact exists in `JobFactCatalog.cs` or `JobFacts`, and `CombatGeometry.cs` coordinates are uncalibrated. The pip veto is a **future planned gate**, not current code. | `ProjectMaelstrom/ProjectMaelstrom/Utilities/JobFactCatalog.cs:11–40`. |
| 6 | **Wrong Navigation Stall Timing**: Stated "halt if position doesn't change after 500ms". | Corrected with real code: `WaypointFeedback.cs:32–33` stops if `_stalls >= 2` with `moved < 2`; `PathFollower.cs:20, 184` uses `StuckMs = 1500` with `_stalledMs >= _options.StuckMs`. | `ProjectMaelstrom/ProjectMaelstrom/Utilities/WaypointFeedback.cs:32–33`, `PathFollower.cs:184`. |
| 7 | **Overstated "Bulletproof Host"**: Used hyperbolic language. | Corrected to: "bounded, single-owner, fail-closed host". | `AGENTS.md` core principles. |

### Preserved Foundational Facts
- Legacy-bot-as-teacher is the controlling architectural thesis ([`docs/W101_LEGACY_TEACHERS.md`](file:///f:/Modlists/AutoWizard101/docs/W101_LEGACY_TEACHERS.md)).
- The no-input proposal principle is implemented in [`RoutineGraph.cs`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/RoutineGraph.cs) and tested in [`RoutineReplay.cs`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/RoutineReplay.cs) (`desktop_dispatch_count = 0`).
- The 8 arrow templates exist in [`ProjectMaelstrom/ProjectMaelstrom/Resources/PetDance/default-v1/`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Resources/PetDance/default-v1/).
- Dance TSV fixture contains **405 data rows** across **15 cases** ([`ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/RustParity/dance-observations-v1.tsv`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/RustParity/dance-observations-v1.tsv)).
- [`PetDanceProductionRuntime.cs`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceProductionRuntime.cs) enforces hardware scancode `0x2D` and `WizardCity` / `1282x760` arena bounds, passing 56/56 focused tests.
- Legacy bot source trees are preserved in [`ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/).

---

## 3. Real Contracts vs Proposed Abstractions

To ensure architectural precision, this brief maps all integration concepts directly to existing, tracked contracts:

```
LEGACY TEACHER INPUT / OUTPUT BOUNDARY
┌────────────────────────────────────────────────────────────────────────┐
│ REAL INPUT: W101UiTreeResult (CombatContext, Loading, States)           │
│             from ProjectMaelstrom/Utilities/W101UiTreeOperation.cs      │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ TEACHER SEAM: [PROPOSED: ICombatTurnTeacher]                           │
│               Wraps legacy tools/combat_turn_planner.py                │
│               Input:  Observed Hand Cards + Combatant State            │
│               Output: Ranked Card Proposals (Target, Index, Intent)    │
│               NO DESKTOP INPUT / NO WIN32 DISPATCH                     │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ REAL OUTPUT: RoutineStep / RoutineAction (InputReleased = true)        │
│              from ProjectMaelstrom/Utilities/RoutineGraph.cs           │
│ VERIFIED BY: RoutineReplay (desktop_dispatch_count == 0)               │
│              from ProjectMaelstrom/Utilities/RoutineReplay.cs          │
│ DISPATCHED:  Only via GameInputLease (Single Owner)                    │
│              from ProjectMaelstrom/Utilities/GameInputLease.cs         │
└────────────────────────────────────────────────────────────────────────┘
```

1. **The Real Input Contract**:
   - `W101UiTreeResult` ([`ProjectMaelstrom/ProjectMaelstrom/Utilities/W101UiTreeOperation.cs:33`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/W101UiTreeOperation.cs)): Provides `CombatContext` (`W101CombatUiContext`), `Loading`, `States`, and timestamps.
   - `JobFacts` ([`ProjectMaelstrom/ProjectMaelstrom/Utilities/JobDeck.cs:13`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/JobDeck.cs)): Provides verified state facts (`health.percent`, `mana.percent`, `nativeIdle`).
2. **The Proposal Contract**:
   - In Maelstrom, routines are modeled as pure state transitions. A teacher outputs a `RoutineAction` ([`ProjectMaelstrom/ProjectMaelstrom/Utilities/RoutineGraph.cs:17`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/RoutineGraph.cs)) with `InputReleased = true`.
   - Offline verification uses `RoutineReplay.Replay(...)` ([`ProjectMaelstrom/ProjectMaelstrom/Utilities/RoutineReplay.cs:33`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/RoutineReplay.cs)), asserting that `desktop_dispatch_count == 0` (zero real hardware input issued).
3. **The Proposed Interface Labels**:
   - Any interface not yet in the C# codebase is explicitly designated with `[PROPOSED]`:
     - `[PROPOSED: ICombatTurnTeacher]`
     - `[PROPOSED: CombatTurnProposal]`
     - `[PROPOSED: IBazaarSearchTeacher]`

---

## 4. Target Adoption Plan: The Combat Turn Planning Teacher

To make this blueprint concrete and claim-ready, we select **Combat Turn Planning** as the first legacy teacher to adopt.

### Why Combat Turn Planning?
- **Existing In-Tree Working Code**: [`tools/combat_turn_planner.py`](file:///f:/Modlists/AutoWizard101/tools/combat_turn_planner.py) (757 lines, **36 test methods defined** in [`tools/tests/test_combat_turn_planner.py`](file:///f:/Modlists/AutoWizard101/tools/tests/test_combat_turn_planner.py), not executed under report-only scope), implementing lethal targeting, emergency healing, buff setup, and pass heuristics.
- **Existing Legacy Sources**: [`ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/Deimos-Wizard101`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/Deimos-Wizard101) (`combat_math.py`, `combat_utils.py`) and [`ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/wizSDK`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/wizSDK) (`battle.py`, `card.py`).
- **Existing In-Tree Fixtures**: [`tools/fixtures/combat/combat_lookup_matrix.json`](file:///f:/Modlists/AutoWizard101/tools/fixtures/combat/combat_lookup_matrix.json) (384,108 bytes) and 56 parity rows in [`ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/RustParity/component-parity-v1.tsv`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/RustParity/component-parity-v1.tsv).

### Two-Step Adoption Execution

```
STEP A: WRAP-FIRST SEAM
tools/w101_combat_snapshot.py ── wraps ──> tools/combat_turn_planner.py
- Takes W101CombatUiContext
- Produces deterministic proposal (Card name + Target pedestal)
- Desktop dispatch: ZERO

STEP B: MODEL-EXTRACTION STEP
crates/maelstrom-core/src/combat_planner.rs  (or C# CombatTurnEvaluator.cs)
- Reverse-engineered FROM tools/combat_turn_planner.py
- 100% pure math: (Damage, Blades, Traps, Resist) -> Lethal?
- Locked with deterministic parity TSVs
```

#### Step A: The Wrap-First Seam (Python Adapter)
1. **Seam Location**: Create `tools/w101_combat_snapshot.py` to wrap the existing working logic in `tools/combat_turn_planner.py`.
2. **Interface Contract**:
   - Input: Serialized `W101CombatUiContext` (hand controls, combatant containers) + verified resources (`health.percent`, `mana.percent`).
   - Output: `[PROPOSED: CombatTurnProposal]` JSON record:
     ```json
     {
       "schema_version": 1,
       "status": "proposed",
       "action_kind": "cast_spell",
       "card_name": "Fire Dragon",
       "target_index": 1,
       "target_kind": "enemy_pedestal",
       "expected_outcome": "card_cast_dispatched"
     }
     ```
3. **Zero Side Effects**: The wrapper contains zero calls to `pyautogui`, `PostMessage`, or Win32 APIs. It executes purely as a proposal generator.

#### Step B: The Model-Extraction Step (Native Component)
1. Once Step A is verified against test fixtures, reverse-engineer the pure mathematical rules from `tools/combat_turn_planner.py` (lethal threshold calculation, resist/boost modifiers, blade stacking) into a native component:
   - Target: `crates/maelstrom-core/src/combat_planner.rs` or `ProjectMaelstrom/ProjectMaelstrom/Utilities/CombatTurnEvaluator.cs`.
2. Author a new TSV fixture: `ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/RustParity/combat-decisions-v1.tsv` asserting identical decisions between the Python wrapper and native engine across 50+ combat hand/pip scenarios.

---

## 5. Negative Bounds and Future Planned Gates

To preserve system safety, the adopted teacher operates under explicit, fail-closed constraints:

### Hard Negative Bounds (Enforced Immediately)
1. **No Real Money / Crowns**: The teacher has zero interface paths to purchase crowns, buy booster packs, or access cash-shop menus.
2. **No Snack Feeding**: Pet snack feeding remains forbidden by policy; feeding routines cannot be invoked.
3. **No Uncalibrated Desktop Clicks**: The teacher never emits screen coordinates. It emits logical targets (`card_index: 2`, `pedestal: 1`). The native host maps these through [`CombatGeometry.cs`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CombatGeometry.cs).
4. **No Action During Loading / Stale State**: Any proposal is immediately discarded if `W101UiTreeResult.Loading == true` or observation age exceeds `2000 ms`.
5. **Human Takeover Priority**: The native [`GameInputLease`](file:///f:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/GameInputLease.cs) revokes control instantly upon detecting mouse or keyboard input from the user.

### Future Planned Gates (Not Yet Implemented)
1. **Pip Fact Verification**: As established in the audit, pips do not currently exist in `JobFactCatalog.cs`. A future claim (`W101-RESOURCE-PIPS-01`) must register and qualify pip observation facts before a strict pip-cost veto can be enforced.
2. **Combat Geometry Calibration**: Current coordinates in `CombatGeometry.cs` are fixed 1280×720 layouts. A future calibration claim must qualify window-relative coordinate transforms across variable UI scaling.

---

## 6. Adoption Task Scope & Acceptance Gates

### Proposed Claim Definition
- **Task ID**: `W101-COMBAT-TEACHER-01`
- **Workstream**: `W101-COMBAT`
- **Owner**: `gemini`
- **Owned Paths**:
  - `tools/w101_combat_snapshot.py`
  - `tools/tests/test_w101_combat_snapshot.py`
  - `tools/fixtures/combat/turn-proposals-v1.json`
  - `docs/coordination/tasks/W101-COMBAT-TEACHER-01.md`
  - `docs/coordination/results/W101-COMBAT-TEACHER-01.md`

### Required Authority
- **Authority Granted**: Source edits on the 5 owned paths; Python unit test execution (`python -m unittest discover -s tools/tests -p "test_w101_combat_snapshot.py"`).
- **Authority Denied**: Zero C# production edits, zero builds, zero desktop/live game interaction, zero installation, and no edits to `claims.json`.

### Acceptance Gates
1. **Wrap-First Operational**: `tools/w101_combat_snapshot.py` successfully wraps `tools/combat_turn_planner.py`, mapping `W101CombatUiContext` fixture states to structured turn proposals.
2. **Zero Desktop Input**: Asserted via unit tests that no OS input libraries are imported or called during proposal generation.
3. **Deterministic Fixture Coverage**: 100% pass rate across all scenario cases in `tools/fixtures/combat/turn-proposals-v1.json`.
4. **Code Quality**: `python -m black --check` clean, `python -m compileall` exit 0, text encoding 0 violations, and project board 30 workstreams valid.

---

## Certification of Operating Boundaries

This brief was authored under strict report-only operating boundaries:
- **0** source code files modified.
- **0** test runs or builds executed.
- **0** desktop or live game actions attempted.
- **0** modifications to `claims.json` or project board state.
- All metrics, paths, and identifiers quoted verbatim from committed primary source artifacts at HEAD (`67d41da`).
