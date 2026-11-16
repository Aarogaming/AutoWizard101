# GEM-CONJ-REPLAY-GAPS-102: Conjuration Perception, Memory Solver & Replay Gaps (Revised)

**Task ID**: `GEM-CONJ-REPLAY-GAPS-102`  
**Claim Owner**: `gemini` (Active in `docs/coordination/claims.json`)  
**Status**: Read-only static review and architectural gap audit; offline analysis only.  
**Deliverable Path**: `docs/coordination/results/GEM-CONJ-REPLAY-GAPS-102.md`  
**Revision Context**: Revised per `LIA-0056` to distinguish missing Conjuration bindings from existing shared infrastructure (`PetDanceMenuInput.MapPoint`, `GameInputLease`, `OperationScheduler`), eliminate unevidenced `IScheduledVisit` references, qualify historical minigame rules as unverified research claims, and replace synthetic unit test code with the actual checked-in fixture invocation and returned trace.

---

## 1. Executive Summary & Boundaries

Under directives `LIA-0055` and `LIA-0056`, this report traces the existing offline Conjuration Concentration components—spanning visual perception (`tools/conjuration_vision.py`), memory decision solving (`tools/conjuration_memory.py`), and replay harness infrastructure (`tools/conjuration_observation_fixtures.py`, `tools/conjuration_recognition_replay.py`).

### Strict Boundary Declarations
- **Zero Production Edits**: No modifications made to production C# or Python files.
- **Zero Live Interactions**: No game client connection, process hooks, memory writes, or input dispatches. Root retains all live navigation and controller authority.
- **Zero Test/Build Runs**: No execution of `dotnet test`, `cargo test`, or python test suites; all analyses are static and use offline checked-in data.
- **Distinction of Status**: Existing positive Conjuration tests are **synthetic offline fixtures**. Real client Conjuration gameplay remains completely unintegrated and unqualified.

---

## 2. End-to-End Pipeline Trace

The offline Conjuration pipeline operates across three strictly separated tiers:

```
[ Saved Image / Frame ]
        │
        ▼
┌────────────────────────────────────────┐
│  Tier 1: Perception                   │
│  (tools/conjuration_vision.py)         │
│  - Normalizes 32x32 RGB descriptors    │
│  - Classifies vs. frozen reference set │
│  - Enforces temporal stability streak  │
└──────────────────┬─────────────────────┘
                   │ Emits Board(cards, phase, revision)
                   ▼
┌────────────────────────────────────────┐
│  Tier 2: Memory Decision Solver       │
│  (tools/conjuration_memory.py)         │
│  - Learns revealed cards into memory   │
│  - Pairs known matching cards          │
│  - Enforces action withholding rules   │
└──────────────────┬─────────────────────┘
                   │ Emits FlipProposal(slot, x, y)
                   ▼
┌────────────────────────────────────────┐
│  Tier 3: Replay & Evidence Adapters   │
│  (tools/conjuration_observation_       │
│   fixtures.py & recognition_replay.py) │
│  - Replays ordered event streams       │
│  - Injects simulated DispatchReceipts  │
│  - Verifies outcome assertions         │
└────────────────────────────────────────┘
```

### 2.1 Tier 1: Perception (`tools/conjuration_vision.py`)
- **Primary Contract**: `ConjurationVision(profile, gallery).observe(image, board_id, revision, time_ms, phase) -> VisionObservation`.
- **Image Preprocessing**: Extracts slot regions based on normalized `SlotProfile.roi` relative to `VisionProfile.board_roi`. Downsamples each crop via bilinear interpolation to `32 × 32` RGB float32 descriptors normalized to `[0.0, 1.0]`.
- **Classification (`_classify`)**:
  - Compares descriptor against `ReferenceProfile` templates.
  - Computes global mean absolute error (similarity $= 1 - \text{MAE}$) and localized $4 \times 4$ patch maximum error (`patch`).
  - Requires:
    - $\text{similarity} \ge \text{minimum\_similarity}$ (default $0.96$).
    - $\text{margin} = \text{score}_1 - \text{score}_2 \ge \text{minimum\_margin}$ (default $0.03$).
    - $\text{patch} \le \text{maximum\_patch\_error}$ (default $0.14$).
- **Temporal Stability Window**:
  - Requires matching the same candidate label across $\ge \text{stable\_frames}$ (default $2$) spanning $\ge \text{stable\_ms}$ (default $50.0\text{ ms}$).
  - Gaps $> \text{maximum\_gap\_ms}$ (default $250.0\text{ ms}$) or descriptor drift $> 0.02$ reset stability.
  - Until stability is reached, card state is forced to `"unknown"` with `identity = None` and diagnostic reason `"awaiting-stable-pixels"`.
- **Crucial Boundary**: `phase` is **never inferred or detected** by the vision system. The observation metadata explicitly records `"phase_detection_implemented": false` and marks the phase as `"caller-supplied-not-detected"`. Non-stable phases (`"animation"`, `"result"`, `"unknown"`) immediately withhold card recognition.

### 2.2 Tier 2: Decision Solver (`tools/conjuration_memory.py`)
- **Primary Contract**: `MemoryMatch(board_id, group_size)` maintaining discrete board state.
- **Observation Absorption (`observe(board)`)**:
  - Validates strictly increasing revisions (`board.revision > self._last_revision`) and constant card coordinates.
  - When `phase in ("preview", "active")`: registers visible `face_up` cards into internal dictionary `_memory[slot] = card.identity`.
  - When in-flight selection reaches `group_size`: monitors subsequent observation to verify group outcome (`match_observed` if all turn `removed`; `mismatch_observed` if all turn `hidden`).
- **Action Generation (`propose() -> FlipProposal | None`)**:
  - **Withholding Invariants** (returns `None`):
    - Solver is blocked (`_blocked is not None`, e.g. conflicting identities or excessive visible faces).
    - Board phase is not `"active"` (e.g. `"preview"`, `"animation"`, `"result"`, `"unknown"` withhold all actions).
    - An action is currently pending dispatch confirmation (`_pending_slot is not None`).
    - Board is cleared (`board-cleared-observed`).
  - **Selection Invariants** (when proposal admitted):
    - *In-progress selection*: If $1 \le \text{visible} < \text{group\_size}$, checks if any matching card with the current visible identity exists in `_memory` at a `hidden` slot. If found, proposes it (`reason = "complete-revealed-selection"` or `"match-known-pair"`). If unknown, explores an unseen slot (`reason = "learn-unseen-card"`).
    - *New selection*: If $\text{visible} == 0$, checks if any complete matching group is known in `_memory`. If so, initiates that group (`reason = "select-known-group"`). Otherwise explores an unseen slot (`reason = "learn-unseen-card"`).
- **Dispatch Tracking (`record_dispatch(receipt)`)**:
  - Accepts `DispatchReceipt(proposal_id, status)`.
  - If `status == "dispatched"`: latches `_pending_slot = proposal.slot`.
  - If `status == "rejected"`: unlatches `_pending_slot` allowing reconsideration.
  - If `status == "unknown"`: latches uncertain state; subsequent observation must verify card state.

### 2.3 Tier 3: Replay Adapters (`tools/conjuration_observation_fixtures.py` & `tools/conjuration_recognition_replay.py`)
- **Label-Level Replay (`replay_fixture` / `load_and_replay`)**:
  - Consumes declarative JSON events (`"observation"`, `"action_receipt"`, `"reset"`).
  - Feeds observations to `MemoryMatch.observe()`, evaluates `MemoryMatch.propose()`, and verifies optional `"expected"` assertions (`proposal_slot`, `outcome`).
  - Injects simulated dispatch receipts without generating desktop events.
- **Image-Level Rehearsal (`replay_images`)**:
  - Parses an image episode manifest containing frozen reference templates and sequential frame PNGs.
  - Replays frames through `ConjurationVision.observe()`, converts `VisionObservation.board` into standard observation events, and delegates to `replay_fixture()`.

---

## 3. Existing Replay Fixture Invocation & Actual API Result

The repository already provides an executable offline perception-to-proposal replay contract via `tools/conjuration_observation_fixtures.py` consuming checked-in fixture `tools/fixtures/conjuration_observations/synthetic-pairs.json`.

### 3.1 Executable Invocation Contract
```python
from pathlib import Path
from conjuration_observation_fixtures import load_and_replay

# Execute existing offline replay on checked-in synthetic fixture
fixture_path = (
    Path("tools") / "fixtures" / "conjuration_observations" / "synthetic-pairs.json"
)
report = load_and_replay(fixture_path)
```

CLI Equivalent:
```powershell
python tools/conjuration_observation_fixtures.py tools/fixtures/conjuration_observations/synthetic-pairs.json report.json
```

### 3.2 Actual API Return Structure & Key Verification Fields
The returned dictionary adheres to the schema-v1 replay specification:

```python
{
    "schema_version": 1,
    "fixture_id": "synthetic-pairs",
    "evidence_mode": "synthetic-label-replay",
    "desktop_dispatch_count": 0,
    "live_qualified": False,
    "proposal_count": 6,
    "supplied_receipt_count": 6,
    "matched_groups_observed": 2,
    "last_board_cleared": True,
    "game_win_verified": False,
    "final_reason": "board-cleared-observed",
    "sha256_fixture": "...",  # 64-char hexadecimal digest
    "images": {},
    "trace": [...],  # 18 ordered event evaluation records
}
```

### 3.3 Trace Transition: Action Proposal, Dispatch Latching, and Withholding
The trace demonstrates the exact cycle of proposal emission, dispatch receipt recording, and strict proposal withholding:

#### Sequence 1 (Event 1): Active Frame 1 Emits Proposal
- Input: Monotonic `time_ms: 100`, `revision: 1`, `phase: "active"`, cards `[0, 1, 2, 3]` all `hidden`.
- Decision: Action admitted; explores slot `"0"`.
```python
{
    "sequence": 1,
    "time_ms": 100,
    "board_id": "synthetic-pairs-board",
    "reason": "proposal-awaiting-dispatch",
    "proposal": {
        "proposal_id": "synthetic-pairs-board:1",
        "board_id": "synthetic-pairs-board",
        "observation_revision": 1,
        "slot": "0",
        "x": 30,
        "y": 30,
        "reason": "learn-unseen-card",
    },
    "outcome": None,
    "source": {"annotation_origin": "synthetic", "image": None, "sha256": None},
}
```

#### Sequence 2 (Event 2): Simulated Dispatch Receipt Latches Pending Flip
- Input: `time_ms: 120`, `proposal_id: "synthetic-pairs-board:1"`, `status: "dispatched"`.
- Decision: Solver latches `_pending_slot = "0"`.
```python
{
    "sequence": 2,
    "time_ms": 120,
    "receipt": {
        "kind": "action_receipt",
        "time_ms": 120,
        "proposal_id": "synthetic-pairs-board:1",
        "status": "dispatched",
    },
    "reason": "dispatch-dispatched",
}
```

#### Sequence 3 (Event 3): Animation Phase Strictly Withholds Proposals
- Input: `time_ms: 220`, `revision: 2`, `phase: "animation"`, slot `"0"` is `face_up` with identity `"A"`.
- Decision: Proposals withheld (`proposal: None`); solver holds `_pending_slot` waiting for stable board.
```python
{
    "sequence": 3,
    "time_ms": 220,
    "board_id": "synthetic-pairs-board",
    "reason": "waiting-for-dispatched-flip",
    "proposal": None,
    "outcome": None,
    "source": {"annotation_origin": "synthetic", "image": None, "sha256": None},
}
```

---

## 4. Three Critical Missing Seams

Tracing perception through solver and replay reveals **three distinct architectural seams** separating existing offline components from production game readiness. Crucially, shared scheduling and input ownership already exist in Maelstrom; the gaps are Conjuration-specific bindings:

### Seam 1: Dynamic Phase Detection & Game Lifecycle Recognition
- **Current State**:
  - `ConjurationVision.observe()` requires `phase` to be passed as an explicit caller-provided string.
  - Emitted observations tag phase as `"caller-supplied-not-detected"`.
  - Replay manifests explicitly supply synthetic `phase` transitions (`"preview"`, `"active"`, `"animation"`, `"result"`).
- **The Gap**:
  - In the live client, phase transitions occur continuously without caller prompts.
  - The recognizer contains **zero** logic to detect:
    1. Initial countdown / preview banner ("Look at the cards!").
    2. Start of active play timer.
    3. In-flight flip animation vs. settled card state.
    4. Board transition / round victory / game-over score screen.
  - *Consequence*: Without a real phase observer, feeding raw game captures to `ConjurationVision` forces the caller to guess the phase, directly risking clicks during flip animations or after timer expiration.

### Seam 2: Real Client Card Textures, Grid Geometry & Group-Size Calibration
- **Current State**:
  - Tested exclusively against procedural synthetic profiles (`tools/fixtures/conjuration_vision/synthetic-profile.json`) using 4 geometrical shapes (circle, square, diamond, triangle) at normalized coordinates.
  - The only real W101 Conjuration assets in-tree (`tools/fixtures/conjuration_vision/provenance.json`) are 17 identical instructions frames. These contain a decorative card strip with partial alpha transparency (`7` to `255`), rejected as unplayable by strict loaders.
- **The Gap & Unverified Historical Claims**:
  - Historical minigame research notes (`docs/MINIGAME_RESEARCH.md`) suggest multi-tier boards (e.g. 4x2, 4x4, 5x4 grids) and multi-card matching requirements (triples / quads at higher tiers).
  - **These claims remain unverified research hypotheses** until confirmed by live client observation.
  - Current-client card art (Firecat, Bloodbat, Imp, Ghoul, Frost Beetle), school icons, card border contrast, and active board dimensions have **never been captured, calibrated, or qualified** in Maelstrom. Group size cannot be hardcoded in production; slot ROIs cannot be static percentages; reference templates must be harvested and qualified from real client captures.

### Seam 3: Conjuration-Specific Runtime Binding to Shared Infrastructure
- **Existing Shared Infrastructure (Already Present)**:
  - Coordinate mapping: `PetDanceMenuInput.MapPoint` already exists in `ProjectMaelstrom` to map parent/client rectangles to desktop screen coordinates.
  - Scheduler & Input: `OperationScheduler`, `OperationContext`, and `GameInputLease` already exist to manage exclusive input ownership, cancel tokens, desktop focus monitoring, and physical takeover aborts.
- **The Gap (Missing Conjuration Binding)**:
  - There is **no Conjuration-specific runtime binding** in C#:
    1. No `IConjurationVisitRuntime` or `ConjurationScheduledVisit` class implementing an operational visit machine (analogous to `IPetDanceVisitRuntime` and `PetDanceScheduledVisit`).
    2. No binding that takes `FlipProposal(slot, x, y)` from Python/memory logic, resolves client coordinates via `MapPoint`, and dispatches a single guarded click via `GameInputLease.DispatchClickAsync`.
    3. No post-flip observation delay or focus verification loop tied to Conjuration.
  - *Consequence*: Shared infrastructure exists, but Maelstrom lacks the specialized adapter class to bind Conjuration proposals to that infrastructure.

---

## 5. Synthetic Recognition vs. Real Gameplay Demarcation

| Dimension | Existing Synthetic Replay (`tools/conjuration_*`) | Production / Live Gameplay Requirements |
| :--- | :--- | :--- |
| **Visual Input** | Flat RGB synthetic shapes, perfect contrast, zero noise | 3D client rendering, texture compression, alpha blending, UI scaling |
| **Grid Geometry** | Fixed 4-slot procedural layout | Variable grids calibrated per live client resolution |
| **Phase Flow** | Explicitly declared per event in JSON | Continuous client state; requires visual banner/timer detection |
| **Timing & Clocks** | Discrete artificial milliseconds (`time_ms: 100, 120, 220`) | Live wall-clock budgets, frame rate variance, animation latencies |
| **Card Matching** | Hardcoded group size (2 or 3) | Live-verified group size per game level (historical rules unverified) |
| **Action Execution** | Simulated `DispatchReceipt` injected into solver | Guarded single-click dispatches via `GameInputLease` + `MapPoint` |
| **Safety Invariants** | Offline assertion checks only | Window focus check, physical key takeover abort, verified native Idle |
| **Game Outcome** | `board_cleared = true` (offline boolean) | Real score accumulation, gold/snack rewards, energy consumption |

---

## 6. Prioritized Offline Verification Matrix

Before any production C# adapter or live client interaction is attempted, the following offline test cases should be verified:

| Priority | Test Name / ID | Target Component | Verifies |
| :---: | :--- | :--- | :--- |
| **P1** | `test_phase_transition_withholding` | `conjuration_memory.py` | Confirms proposals are withheld when transitioning through `"animation"`, `"unknown"`, and `"result"`, and re-admitted only on stable `"active"`. |
| **P2** | `test_unstable_vision_emits_unknown` | `conjuration_vision.py` | Confirms transient card state changes during flips emit `"unknown"` until the stability streak threshold ($\ge 50\text{ ms}$) is met. |
| **P3** | `test_variable_grid_size_adaptation` | `conjuration_vision.py` | Validates profile loader and classifier on multiple grid geometries without aspect distortion. |
| **P4** | `test_dispatch_timeout_recovery` | `conjuration_memory.py` | Simulates an unacknowledged or dropped dispatch receipt; verifies solver latches recovery rather than emitting overlapping clicks. |
| **P5** | `test_board_reset_on_new_level` | `conjuration_observation_fixtures.py` | Exercises multi-board sequences where clearing Board A triggers a reset event with fresh `board_id` and new group size. |

---

## 7. Handback & Next Steps

This revised review provides the exact structural mapping and gap inventory between existing Conjuration perception/solver components and a full replay/execution runtime.

- **Completed**: Revised report delivered to claimed path `docs/coordination/results/GEM-CONJ-REPLAY-GAPS-102.md`.
- **Next Step**: Append `GEM-0062` (`Kind: READY_FOR_REVIEW`) to `docs/coordination/gemini/FROM_GEMINI.md` and stand down awaiting coordinator review.
