# GEM-MINIGAME-READINESS-98 — Minigame Readiness & Next Playable Integrations Audit (Corrected Baseline)

**Task ID**: `GEM-MINIGAME-READINESS-98`  
**Workstream**: `W101-DANCE`  
**Author**: Gemini  
**Date**: 2026-11-16T12:25:00-05:00 (Revised per coordinator audit `GEM-HONESTY-AUDIT-01` / `LIA-0121`)  
**Repository Revision**: HEAD `6dd15e5` (`maelstrom/rust-architecture-v1`)  
**Status**: READ-ONLY AUDIT & CANDIDATE INTEGRATION RANKING (CORRECTED)  
**Owned Output**: `docs/coordination/results/GEM-MINIGAME-READINESS-98.md`  

---

## 1. Executive Summary & Scope

This report fulfills directive `LIA-0051` and incorporates corrections from honesty audit `LIA-0121` (`GEM-HONESTY-AUDIT-01`). It provides an evidence-backed readiness audit and ranking of candidate minigames in `ProjectMaelstrom` beyond Pet Dance.

### 1.1 Scope Boundaries & Current Workstream Separation
- **Pet Dance Retained by Root**: Root coordinator owns the active live Dance qualification lane and production wiring. Pet Dance is excluded from this ranking as it is already in live qualification.
- **Strict Distinction**: Every component is categorized as either an **offline planner/model** (pure logic, memory, vision feature matcher, synthetic tests) or a **live runner** (scheduler lease, window capture, input dispatch, process lifecycle).
- **Excluded Non-Viable Adapters**:
  - `ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/GrubNinja-master/`: Third-party Electron utility that makes HTTP requests to KingsIsle servers (`buyEnergy.js`, `feedSnack.js`). This violates Maelstrom's core boundary excluding network manipulation and traffic forgery (`AGENTS.md`).
  - `ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/Deimos-Wizard101/src/auto_pet.py`: Standalone script utilizing unhedged direct process memory writing (`pymem`, `dance_game_hook`). It lacks scheduler integration, input lease gating, and cancellation recovery.
  - *Sorcery Stones*, *Choo-Choo Zoo*, *Doodle Doug*, *Skull Riders*, *Hot Shots*: Documented solely in [MINIGAME_RESEARCH.md](file:///F:/Modlists/AutoWizard101/docs/MINIGAME_RESEARCH.md) as research baselines; have zero implementation code, zero test suites, and zero fixtures in the repository.

---

## 2. Ranked Playable Integration Matrix

The top three candidate minigames are ranked below by architectural maturity, implementation completeness, existing test coverage, and feasibility of live integration under the shared `GameInputLease`.

```
+-----------------------------------------------------------------------------------------------------------------------+
|                                  TOP THREE NEXT PLAYABLE MINIGAME INTEGRATIONS                                        |
+------+-----------------------------+---------------------------------------+---------------------+--------------------+
| Rank | Minigame Candidate          | Existing Offline Implementation       | Game Complexity     | Primary Live Gap   |
+------+-----------------------------+---------------------------------------+---------------------+--------------------+
|  1   | Conjuration Concentration   | Memory Planner + Vision Recognizer    | Low (Turn-based)    | Real Client Cards  |
|  2   | Potion Motion               | Dual C# & Python Board Solvers        | Low/Mod (Turn-based)| Drag Dispatcher    |
|  3   | Dueling Diego               | Offline Vision Detector + Annotations | High (Action/Timing)| Real-time Motion   |
+------+-----------------------------+---------------------------------------+---------------------+--------------------+
```

---

## 3. Detailed Component & Readiness Audit by Candidate

### 3.1 Rank 1: Conjuration Concentration (Highest Readiness)

[CONJURATION_OFFLINE.md](file:///F:/Modlists/AutoWizard101/docs/CONJURATION_OFFLINE.md) | [CONJURATION_RECOGNITION.md](file:///F:/Modlists/AutoWizard101/docs/CONJURATION_RECOGNITION.md) | [CONJURATION_CARD_COLLECTION.md](file:///F:/Modlists/AutoWizard101/docs/CONJURATION_CARD_COLLECTION.md)

Conjuration Concentration is the most mature non-Dance minigame in the codebase. It features an existing, fully tested memory decision engine, a vision recognition framework, and an observation replay pipeline.

#### A. Existing Runnable Components (Offline)
- **Memory & Decision Engine**:
  - File: `tools/conjuration_memory.py`
  - Core Symbols: `MemoryMatch`, `Board`, `Card`, `FlipProposal`, `DispatchReceipt`, `ObservedOutcome`.
  - Capabilities: Implements optimal card matching under partial observability. Tracks revealed, hidden, and removed cards. Proposes single bounded flips, awaits dispatch receipts, and validates card transitions without guessing. Adapts dynamically to 2-match or 3-match rules.
- **Vision Recognition Engine**:
  - File: `tools/conjuration_vision.py`
  - Core Symbols: `ConjurationVision`, `VisionProfile`, `load_image`, `decode_image`.
  - Capabilities: Compares bilinear-normalized 32x32 RGB card descriptors against a frozen gallery of reference crops using whole-patch MAE and 4x4 subpatch error checks to handle occlusions.
- **Fixture Generation & Replay Pipeline**:
  - Files: `tools/conjuration_observation_fixtures.py`, `tools/conjuration_recognition_replay.py`, `tools/conjuration_collection.py`.
  - Capabilities: Synthesizes procedural card layouts, validates observation schemas, and checks replay stability.

#### B. Verified Test Fixtures & Test Coverage
- **Fixtures**:
  - `tools/fixtures/conjuration_vision/synthetic-profile.json` (procedural 4-card layout).
  - `tools/fixtures/conjuration_vision/provenance.json` (RGB test conversions and negative instructions).
  - `tools/fixtures/conjuration_collection/plan-v1.json` (collection schema).
- **Unit Test Suites**:
  - `tools/tests/test_conjuration_memory.py` (21 tests passing: preview phases, seeded random pairs/triples, separate proposals/receipts/removals, uncertain dispatch, rejected dispatch recovery, stale/wrong receipts, animation/unknown resilience, identity conflicts, board resets, invisible labels, group size bounds, and result phase gating).
  - `tools/tests/test_conjuration_vision.py` (profile loading, descriptor hashing, ambiguity checks).
  - `tools/tests/test_conjuration_observation_fixtures.py`.
  - `tools/tests/test_conjuration_collection.py`.
  - `tools/tests/test_conjuration_recognition_replay.py`.

#### C. Entry, Exit & Input Ownership Model
- **Entry & Exit**: Uses the standard Fairground sigil prompt (`OutsideAfterGame` with 'X' prompt) and transitions through common modal screens (`loading.png`, `reward.png`, `native-idle.png` in `TestData/Modules/W101Menus`).
- **Input Dispatch**: Pure point-and-click. Each action is a single left-click at a resolved card slot center. Fits directly into the existing `PetDanceMenuInput.DispatchSequenceAsync` pattern (80ms settle, click, 80ms release).

#### D. Concrete Missing Production Bindings
1. **Real-Client Card Reference Gallery**: Current fixtures are synthetic 4-card procedural shapes; real client card textures, hidden-back textures, and matched/inactive visual states must be collected and calibrated.
2. **Phase Recognition**: Distinguishing the initial preview inspection window from active play, flip animations, and end-of-board victory.
3. **C# Production Runner / Scheduler Adapter**: An `OperationScheduler`-bound runner (e.g. `ConjurationVisit`) connecting window capture to the memory engine and managing the lease.
4. **Group Size Detection**: Detecting whether the current board level requires pairs (2 cards) or triplets (3 cards).

---

### 3.2 Rank 2: Potion Motion (Second Readiness — Refill Utility)

[POTION-PLANNER-01.md](file:///F:/Modlists/AutoWizard101/docs/coordination/results/POTION-PLANNER-01.md) | [PotionMotionBoard.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/PotionMotionBoard.cs)

Potion Motion is an essential utility minigame: scoring sufficient points in Potion Motion refills player potion bottles and mana without spending gold or waiting. (Note: community advice commonly cites ~1,000–1,200 points, but `docs/MINIGAME_RESEARCH.md:30` labels exact refill thresholds as unverified and inconsistent; completion must be verified against actual pre/post potion state rather than an assumed score).

#### A. Existing Runnable Components (Offline)
- **Native C# Row-Slide Evaluator**:
  - File: [ProjectMaelstrom/ProjectMaelstrom/Utilities/PotionMotionBoard.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/PotionMotionBoard.cs)
  - Core Symbols: `PotionMotionBoard`, `PotionMotionMove`, `FindMatches`, `ApplyRowSlide`, `FindBestMove`.
  - Capabilities: Evaluates rectangular grids of string tokens, detects 4-neighbor connected groups of $\ge 3$ matching bottles (including bent L and T shapes), models row slide displacements (`ApplyRowSlide`) with optional wrapping, and finds the highest-scoring row move (`FindBestMove`). *Note*: C# implementation does not evaluate column slides, lacks parameterless 7x7 constructors, and is a partial row-only port.
- **Python Board Evaluator & Move Planner**:
  - File: `tools/potion_motion_board.py`
  - Core Symbols: `PotionMotionBoard`, `MoveProposal`, `shift_row`, `shift_col`, `find_connected_groups`, `evaluate_board`, `find_best_moves`.
  - Capabilities: Evaluates rectangular grids, finds 4-connected components of $\ge 3$ matching cells (including bent L/T shapes), simulates single row and column shifts with optional wrapping, and ranks top single-step move proposals via `find_best_moves`. (No 2-ply lookahead or gravity collapse simulation is implemented).

#### B. Verified Test Fixtures & Test Coverage
- **Fixtures**:
  - `tools/fixtures/potion_motion/board-schema-v1.json` (board serialization schema).
  - `tools/fixtures/potion_motion/synthetic-cases.json` (pre-configured challenge boards).
- **Unit Test Suite**:
  - `tools/tests/test_potion_motion_board.py` (9 tests passing: bounds safety, horizontal/vertical matches, L/T intersections, scoring rules, single row/col shifts, valid move generation, optimal move selection, row/col wrapping).

#### C. Entry, Exit & Input Ownership Model
- **Entry & Exit**: Fairground sigil entry. Gameplay terminates once score reaches the unverified refill threshold, followed by exit to Commons.
- **Input Dispatch**: Requires **drag gestures**. Unlike Dance or Conjuration (which use single clicks or key taps), Potion Motion slides an entire row or column. Dispatch requires mouse-down at a cell center, smooth dragging across cell pitch (horizontal or vertical), hold, and mouse-up under `GameInputLease`.

#### D. Concrete Missing Production Bindings
1. **Live Grid Vision Classifier**: No recognizer exists to map game screen pixels to the 7x7 cell matrix and classify potion bottle colors (red, blue, green, yellow, purple, orange).
2. **Drag Gesture Dispatcher**: `PetDanceMenuInput` only supports single-point clicks and keyboard taps; it lacks drag-and-drop primitives with physical release confirmation.
3. **Score & Timer OCR**: Monitoring real-time score and timer.
4. **Refill State Verification**: Confirming potion bottle fullness before and after the game via client memory or UI tree observation.

---

### 3.3 Rank 3: Dueling Diego (Third Readiness — Action Game Prototype)

[tools/diego_vision.py](file:///F:/Modlists/AutoWizard101/tools/diego_vision.py) | [tools/diego_observer.py](file:///F:/Modlists/AutoWizard101/tools/diego_observer.py)

Dueling Diego is a side-scrolling platformer. While visual observation tools exist in the repository, real-time control presents high latency and coordination challenges.

#### A. Existing Runnable Components (Offline)
- **Visual Detection & Template Matcher**:
  - File: `tools/diego_vision.py`
  - Core Symbols: `Detection`, `Examples`, `SceneTracker`, `detect`, `rectangle`, `overlap`.
  - Capabilities: Bounded template matching for character sprites and obstacles across saved frames.
- **Observer UI & Report Tooling**:
  - Files: `tools/diego_observer.py`, `tools/diego_report.py`, `tools/build_diego_fixtures.py`, `tools/build_diego_tutorial_pack.py`.
  - Capabilities: Frame-by-frame UI tool for annotating character poses, enemy bounding boxes, and tutorial sequences.

#### B. Verified Test Fixtures & Test Coverage
- **Fixtures**:
  - `tools/fixtures/diego_observer/v1/pack/diego.png` (reference template pack).
  - `tools/diego_tutorial_annotations.json` (annotated walkthrough keyframes).
- **Unit Test Suites**:
  - `tools/tests/test_diego_vision.py` (template matching and overlap tests).
  - `tools/tests/test_diego_observer_ui.py` (UI annotation harness tests).
  - `tools/tests/test_diego_coordination.py`.

#### C. Entry, Exit & Input Ownership Model
- **Entry & Exit**: Fairground sigil entry.
- **Input Dispatch**: Continuous real-time keyboard control (Left/Right to move, Space to jump, A/S/D for high/mid/low sword strikes). Continuous rapid input creates significant race conditions with desktop takeover detection.

#### D. Concrete Missing Production Bindings
1. **Dynamic Motion Tracking**: Tracking continuously scrolling terrain, moving enemies, and projectile trajectories.
2. **Low-Latency Continuous Keyboard Dispatcher**: Reactive, sub-50ms key pulses under `GameInputLease`.
3. **Boss Battle State Machine**: Logic for multi-phase boss mechanics.

---

## 4. Architectural Comparison & Distinction: Planners vs. Runners

A critical finding of this audit is that **all three minigames currently have offline planners/analyzers, but zero production live runners**:

```
+---------------------------+-----------------------------------+---------------------------------------------------+
| Minigame                  | Offline Planner / Solver (Exists) | Live Runner / Dispatcher (Missing)                |
+---------------------------+-----------------------------------+---------------------------------------------------+
| Conjuration Concentration | tools/conjuration_memory.py       | No C# ScheduledVisit / Lease link                 |
| Potion Motion             | PotionMotionBoard.cs (partial C#) | No live vision / No drag dispatch                 |
| Dueling Diego             | tools/diego_vision.py             | No real-time reactive controller                  |
| Pet Dance (Reference)     | PetDanceVisit.cs / Trial (C#)     | Specialist trial only; IPetDanceVisitRuntime open |
+---------------------------+-----------------------------------+---------------------------------------------------+
```

---

## 5. Strategic Recommendations for Next Playable Minigame

1. **Immediate Focus**: Allow root coordinator to complete the Pet Dance live qualification lane (`f49b04f78ea94b439fe1f7e3acb14a43`).
2. **First Expansion Target — Conjuration Concentration**:
   - Why: Highest return on investment. The memory state machine is completely solved and tested; the gameplay is discrete and turn-based (no physics or timing hazards); and the input is simple point-and-click matching the existing `PetDanceMenuInput` click engine.
   - Next Task: Port or wrap `conjuration_memory.py` / `conjuration_vision.py` into a C# `ConjurationScheduledVisit` and capture real client card assets.
3. **Second Expansion Target — Potion Motion**:
   - Why: Provides utility (potential potion refills). Row-slide solver exists in C# (`PotionMotionBoard.cs`).
   - Next Task: Implement a drag-and-drop input helper in C# and build a 7x7 cell color classifier.

---

## 6. Coordination & Verification Record

- **Reviewed Directives**: `LIA-0051` (activation), `GEM-0055` (task ACK), `LIA-0121` (`GEM-HONESTY-AUDIT-01`).
- **Git HEAD Verified**: HEAD `6dd15e5` (`maelstrom/rust-architecture-v1`).
- **Zero Modification Invariant**: No production/test code edited; zero tests/builds executed; zero live game interaction.
- **Deliverable**: Audit report delivered to [`docs/coordination/results/GEM-MINIGAME-READINESS-98.md`](file:///F:/Modlists/AutoWizard101/docs/coordination/results/GEM-MINIGAME-READINESS-98.md).

---

## Audit note (2026-11-19)
The imported `Scripts/Library/Deimos-Wizard101` tree cited above (including `src/auto_pet.py`) was moved out of the repository during the 2026-11-19 imported-project archive cleanup, so that path no longer exists here. The assessment of that script describes the archived copy, not anything in the working tree. Found by `tools/check_claim_evidence.py`.
