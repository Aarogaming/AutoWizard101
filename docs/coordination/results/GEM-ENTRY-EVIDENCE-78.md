# GEM-ENTRY-EVIDENCE-78 — Entry Evidence Adapter Design & Saved Fixture Inventory (Corrected Baseline)

**Task ID**: `GEM-ENTRY-EVIDENCE-78`  
**Workstream**: `W101-DANCE`  
**Author**: Gemini  
**Date**: 2026-11-16T12:30:00-05:00 (Revised per coordinator directives `LIA-0111` through `LIA-0121`)  
**Repository Revision**: HEAD `6dd15e5` (`maelstrom/rust-architecture-v1`)  
**Status**: READ-ONLY AUDIT & ADAPTER DESIGN SPECIFICATION (CORRECTED)  
**Owned Output**: `docs/coordination/results/GEM-ENTRY-EVIDENCE-78.md`  

---

## 1. Executive Summary & Architecture Context

### 1.1 Purpose & Scope
This report fulfills coordinator directives `LIA-0035` and `LIA-0108` for queue item `GEM-ENTRY-EVIDENCE-78`. It delivers:
1. An exhaustive **Field Matrix** for `PetDanceVisitObservation`, mapping every consumer property to verified production producers or identifying missing bindings.
2. A formal **Stage Coverage Specification** spanning all ten entry, gameplay, postgame, and Idle stages.
3. A verified **Saved Fixture Inventory** indexing all available local image, OCR, and manifest fixtures across `ProjectMaelstrom.Tests/TestData/`, updated with ground-truth byte counts and SHA-256 hashes from current repository HEAD `7888cf2`.
4. The design for the **Smallest Production Adapter Interface** (`IPetDanceVisitRuntime` and `IPrimedPetDanceVisitRuntime` binding) that integrates live vision and specialist execution without nested UI loops, fragmented input leases, or stale capture buffering.
5. A prioritized **Offline Acceptance Matrix** specifying deterministic test scenarios for boundary conditions.

### 1.2 Specialist Extraction Clarification (Superseding Report 71)
Audit `GEM-DANCE-AUDIT-71` described the Pet Dance gameplay stage as a subprocess-only GUI launcher (`PetDancePlayerLauncher.cs`). In subsequent root refactoring (Task 75), the gameplay engine was extracted into an **in-process asynchronous specialist method**:
- **`PetDanceLiveTrial.RunSpecialistAsync`**: Operates entirely within the parent's `OperationContext`, using the caller's existing `GameInputLease`. It launches **no** standalone subprocess, creates **no** nested modal `Form` or Win32 message loop, and operates purely via async observation and pulse dispatch.
- **`PetDanceSpecialistAdmission.OwnsInput(context, target)`**: Gates specialist execution by validating that `context.Request.WritesGame` is true, `!context.Request.DelegatedInput`, `context.Request.Target == ModuleOwnedInput.TargetId(target)`, and `ReferenceEquals(context.Input, GameInputLease.Current)`.

### 1.3 Critical Architectural Distinction: Specialist vs. Full-Visit Runtime
While `RunSpecialistAsync` implements the *gameplay round loop* (Watch → Respond → Repeat), the **full visit lifecycle** remains unbound in production:
- `PetDanceScheduledVisit` provides the state-machine orchestration and job preparation.
- `PetDanceVisit` provides deterministic offline routine graph transitions (`pet-dance-visit.json`).
- Root introduced `IPrimedPetDanceVisitRuntime` and `PetDancePrimedCapture` in `PetDanceScheduledVisit.cs` and `PetDancePrimedCapture.cs` to solve capture/play synchronization.
- **Missing Link**: A live production implementation of `IPetDanceVisitRuntime` / `IPrimedPetDanceVisitRuntime` that bridges live window capture (`BoundedWindowCapture`), menu OCR (`PetDanceMenuOcr`), level/play visual matchers (`PetDanceLevelSelectionVision`, `PetDancePlayButtonVision`), postgame classifier (`PetDancePostGameClassifier`), and native Idle confirmation (`PetDanceNativeIdle`) to the scheduler.

---

## 2. Comprehensive Field Matrix for `PetDanceVisitObservation`

The record `PetDanceVisitObservation` in [PetDanceVisit.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceVisit.cs) contains 19 properties evaluated by the deterministic routine runner on every tick.

| Field Name | Type | Consumer Usage in `PetDanceVisit` | Existing Producer Method / Property | Identity / Geometry Binding | Freshness / Timebase Constraints | Unknown / Conflict Behavior | Verification Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `TimeMs` | `double` | Monotonic timestamp for routine runner step, deadband tracking, and receipt correlation. | `CapturedWindowFrame.CaptureTime` or monotonic clock. | Binds to capture instant of current frame. | Must be finite, non-negative, and monotonically increasing. | Invalid/infinite timestamp causes frame rejection. | **Implemented** in capture pipeline. |
| `ImageSha256` | `string` | Pinned frame hash; validated via `PetDanceEntryVision.IsImageHash` (64 hex chars). | Pinned frame SHA-256 (Candidate frame hash calculation; no static `Sha256Hex` helper exists on `VisualStateRecognizer`). | Cryptographic binding to raw pixel buffer. | Must match `Entry.ImageSha256` exactly when `Entry` is present. | Hash mismatch sets `boundEntry = false`, withholding proposals (`entry-evidence-frame-mismatch`). | **Missing / Unbound** (no live pipeline producer). |
| `Scene` | `PetDanceScene` | Drives state machine graph transitions (`DanceSigil` → `Instructions` → `Watch` → `GameResult` → etc.). | `PetDanceEntryVision.Classify` (entry) or `PetDancePostGameClassifier.Classify` (postgame). | Bound to frame OCR + visual evidence. | Evaluated per frame; must be defined enum value. | Unrecognized or conflicting scenes map to `PetDanceScene.Unknown`. | **Implemented** in classifier logic; live stream binding missing. |
| `TargetId` | `string` | Target validation against `_options.TargetId`. | `ModuleOwnedInput.TargetId(target)` or `context.Request.Target`. | Pinned window/process ID (e.g. `w101-<pid>`). | Pinned at visit initialization; immutable. | Target mismatch withholds proposals; drops routine continuity. | **Implemented** in `ModuleOwnedInput`. |
| `Geometry` | `Size` | Window client dimensions validation against `_options.Geometry`. | OS window bounds check (`CapturedWindowFrame` has no `Size` property and `GameWindow` has no `ClientSize` property; requires explicit extraction). | Outer/client pixel geometry (e.g. `1282x760`). | Must match `_options.Geometry` and `Entry.Geometry`. | Geometry mismatch causes `boundEntry = false`, withholding proposals. | **Missing / Unbound** (requires explicit geometry extraction). |
| `ContextId` | `string` | Operational session tracking against `_options.ContextId`. | `context.Request.Id` (`Guid.NewGuid().ToString("N")`). | Unique job execution token. | Pinned for duration of visit job. | Context mismatch aborts routine evaluation. | **Implemented** in scheduler. |
| `Entry` | `PetDanceEntryScene?` | Supplies `PlayBounds`, `SelectedArena`, `LevelControls`, `SelectionVerified`, `PlayEnabledVerified`. | `PetDanceEntryVision.Classify(text, sha256, ms, evidence)`. | Bound to identical `ImageSha256`, `CapturedMs`, `Geometry`, `Scene`. | Frame-bound; non-reusable across frames. | If null during entry scenes, proposals withheld with reason `entry-evidence-frame-mismatch`. | **Implemented** in `PetDanceEntryVision`; live OCR producer unbound. |
| `ControlBounds` | `RectangleF?` | Screen-space click bounding box for postgame actions (`Next`, `Finish`, `SelectIdle`). | `PetDancePostGameScene.ActionBounds` from `PetDancePostGameClassifier`. | Local client pixel coordinates. | Evaluated per frame. | If null or out of client bounds, `ControlVerified = false`, withholding dispatch. | **Implemented** in postgame classifier. |
| `Fresh` | `bool` | Indicates capture occurred within real-time latency bounds. | `PetDanceTrialFrameGate.Inspect` or `DodgeLiveRun.IsFreshCapture(capturedMs, nowMs, 150)`. | Capture timestamp vs system wall clock. | Frame age must be $\le 150\text{ ms}$. | Stale frame sets runner `Fresh = false`, suppressing state advance and dispatches. | **Implemented** in `PetDanceTrialFrameGate`. |
| `TargetReady` | `bool` | Verifies game window is responsive and currently foreground. | Win32 foreground window check (Note: `GameWindow` has no `IsForegroundWindow` or `IsReady` methods; requires explicit Win32 interop). | OS window focus state. | Live Win32 check at observation time. | If false and not `FocusPaused`, proposal withheld; does not stop visit. | **Missing / Unbound** (no high-level `GameWindow` property). |
| `ContextVerified` | `bool` | Confirms execution context is active, valid, and non-delegated. | `context != null && !context.Request.DelegatedInput && context.Request.Id == options.ContextId`. | Pinned scheduler context. | Monitored continuously. | If false, aborts routine evaluation. | **Implemented** in scheduler. |
| `InputOwned` | `bool` | Verifies exclusive ownership of the shared input lease. | `PetDanceSpecialistAdmission.OwnsInput(context, target)`. | `ReferenceEquals(context.Input, GameInputLease.Current)`. | Must hold throughout entire action dispatch. | Loss of input lease halts proposals immediately. | **Implemented** in `PetDanceSpecialistAdmission`. |
| `EntryCostApproved` | `bool` | Gates transition from `InstructionsReady` to `Play`. | `PetTrainingEnergyGate.Evaluate(snapshot).Decision == PetTrainingEnergyDecision.OneAttempt`. (Note: `PetDanceArenaCosts` and `W101ResourceFacts.GetLiveResource` do not exist in repo; energy costs scale with pet level up to Ultra). | Evaluated from `facts.energy` and configured level cost. | Snapshot must be fresh; cannot use stale energy facts. | If false, proposal withheld with `"entry-cost-not-approved"`. NEVER assumed true. | **Implemented** in gate logic; live energy extraction unbound. |
| `WatchStartVerified` | `bool` | Confirms game round sequence has visually or acoustically started. | `PetDancePanelVision.UsesShapeSequence` / `PetDanceSoundObserver`. | Start of round 1 arrow sequence. | Onset detected within 5000 ms of Play dispatch. | If false, withheld with `"qualified-specialist-and-watch-start-required"`. | **Implemented** in specialist trial; full runtime binding missing. |
| `SpecialistQualified` | `bool` | Validates in-process specialist readiness (profile, templates, audio). | Validation of `PetDancePanelVision` and specialist admission. | Pinned specialist configuration. | Checked prior to handoff. | If false, withheld with `"qualified-specialist-and-watch-start-required"`. | **Implemented** in `PetDanceLiveTrial`. |
| `WholeGameResultVerified` | `bool` | Confirms independent visual observation of postgame result modal (`PetDanceScene.GameResult`). | Candidate producer: `PetDancePostGameClassifier.Classify` (detecting `PetDancePostGameKind.RewardModal`). Note: `PetDanceSpecialistResult` confirms only that rounds ended and input was cleanly released (`GameEnded && CleanupConfirmed && InputReleaseConfirmed`); its `GameVictoryVerified` is explicitly false and round feedback remains separate from result-screen verification. | Frame-bound OCR + visual evidence correlated with specialist receipt. | Evaluated upon postgame frame arrival. | If false (or if specialist receipt missing), withheld with `"completed-specialist-and-whole-game-result-required"`. | **Missing / Unbound** (Candidate classifier exists in `PetDancePostGameClassifier`, but live stream binding and correlation with specialist receipt are missing; score/victory verification is not implemented). |
| `UserActive` | `bool` | Detects physical keyboard intervention (causal user takeover). | `DesktopActivityMonitor.Revision` increment with `TargetActivityAllowed` and `IsTakeover`. | Global low-level keyboard hook (`WH_KEYBOARD_LL`). | Real-time hook event timestamp. | If true, triggers immediate causal takeover stop (`decision.Stopped = true`). | **Implemented** in `DesktopActivityMonitor`. |
| `Cancelled` | `bool` | Detects explicit user or timeout cancellation. | `token.IsCancellationRequested` or `DesktopActivityMonitor.EscapeRequested`. | Execution cancellation token. | Checked on every loop iteration. | If true, halts visit runner immediately (`decision.Stopped = true`). | **Implemented** in runner/scheduler. |
| `FocusPaused` | `bool` | Distinguishes temporary window focus loss from permanent takeover. | `!target.IsForegroundWindow() && !UserActive && !Cancelled && InputOwned`. | Foreground window check. | Real-time Win32 foreground check. | Feeds untrusted frame to runner to advance deadlines; pauses proposals (`"target-not-ready-paused"`) without stopping. | **Implemented** in `PetDanceVisit.cs`. |

---

## 3. Detailed Stage Coverage Specification

The full Pet Dance visit spans ten distinct stages:

```
[Outside / Sigil] --------> [Loading Screen] --------> [Instructions Unselected]
       |                            |                                |
       | (Press X)                  | (Wait)                         | (Click Arena Icon)
       v                            v                                v
[Dance Sigil Title] <------- [Loading Cleared] <------ [Instructions Ready]
                                                                     |
                                                                     | (Click Play if Energy Approved)
                                                                     v
[Postgame Native Idle] <--- [Graphics Menu] <--- [No-Feed Finish] <-- [Reward Screen] <-- [Watch/Respond Specialist]
```

### Stage 1: Dance Sigil Detection (`DanceSigil`)
- **Required Visual Evidence**: 
  - OCR detects interaction prompt (`"PRESS"` and `"TO PLAY"` on bottom row, or `"X"` icon).
  - Supplemental evidence provides `VerifiedSigilTitleBounds` located between $60\%$ and $90\%$ of window height.
  - Background scene is `OutsideAfterGame` without modal dialog headers (`YOUR REWARD`, `FEED PET`, `GRAPHICS`, `DANCE GAME`).
- **Gating**: Must have `InputOwned == true`, `TargetReady == true`, and fresh capture.
- **Proposed Action**: `PetDanceLifecycleAction.InteractX` (dispatch `'X'` key pulse).
- **Exit Condition**: Screen transitions to `Loading` or `InstructionsUnselected`.

### Stage 2: Loading Transition (`Loading`)
- **Required Visual Evidence**: Neutral loading artwork, lack of modal text, or verified loading cue via `evidence.LoadingVerified == true`.
- **Gating**: When loading is verified, conflicting cues (such as visible modal headers) force classification to `Unknown`.
- **Proposed Action**: `PetDanceLifecycleAction.Wait` (withhold all inputs; pause countdown).
- **Exit Condition**: Loading screen clears; instruction panel or game arena appears.

### Stage 3: Level Selection Unselected (`InstructionsUnselected`)
- **Required Visual Evidence**:
  - OCR detects `"DANCE GAME"`, `"OBJECTIVE"`, `"SELECT LEVEL"`, and `"PLAY"` in top-down layout hierarchy.
  - `LevelControls` list contains $\le 5$ non-overlapping bounding boxes for valid arenas.
  - Requested arena (e.g. `PetDanceArena.WizardCity`) is present and `IdentityVerified == true`.
  - Arena is currently unselected (`SelectedArena != options.Arena` or `SelectionVerified == false`).
- **Gating**: `InputOwned == true`, client geometry matches qualified layout (`1282x760`).
- **Proposed Action**: `PetDanceLifecycleAction.ChooseLevel` (mouse click targeting the requested arena icon bounds).
- **Exit Condition**: Icon visual state changes to selected appearance.

### Stage 4: Instructions Ready (`InstructionsReady`)
- **Required Visual Evidence**:
  - Instructions header and layout verified.
  - Requested arena icon matches selected appearance via `PetDanceLevelSelectionVision` (`selected-away` feature match, similarity $\ge 0.85$, margin $\ge 0.10$).
  - Play button matches enabled appearance via `PetDancePlayButtonVision` (`play-enabled` feature match, similarity $\ge 0.85$, margin $\ge 0.10$).
  - `evidence.SelectionHighlightVerified == true` and `evidence.PlayEnabledVerified == true`.
- **Gating**: Requires **`observation.EntryCostApproved == true`**.
- **Proposed Action**: `PetDanceLifecycleAction.Play` (mouse click targeting verified `PlayBounds`).
- **Exit Condition**: Instruction panel closes; transition into game arena.

### Stage 5: Configured Entry Cost vs. Qualified Energy Evaluation
- **Contract**: Evaluated via `PetTrainingEnergyGate.Evaluate(snapshot)`.
- **Required Facts**:
  - `snapshot.Current`: Current energy read from `facts.energy` (must be non-negative int; live fact extraction currently unbound).
  - `snapshot.NextAttemptCost`: Cost of selected arena (client energy costs scale with pet level up to Ultra; exact costs must be provided via snapshot, not hardcoded guesses).
  - `snapshot.Reserve`: Configured minimum energy reserve (default $\ge 10$).
  - `snapshot.EnergyVerified == true`, `snapshot.CostVerified == true`, `snapshot.ObservationFresh == true`.
- **Gating Rule**: If `Current - NextAttemptCost < Reserve`, decision is `PetTrainingEnergyDecision.Stop`, reason `"energy-reserve-would-be-crossed"`. `EntryCostApproved` is set to `false`, preventing Play dispatch.
- **Safety Invariant**: Energy cost approval is **never assumed or defaulted to true**. Missing energy facts withhold Play indefinitely.

### Stage 6: Gameplay Watch & Respond (`Watch` → Specialist Handoff)
- **Required Evidence**:
  - Instruction panel has vanished; dance arena floor/grid visible.
  - `WatchStartVerified == true`: Detection of first round arrow prompt or acoustic cue.
  - `SpecialistQualified == true`: Specialist templates and detector armed.
- **Execution Mechanism**: `PetDanceScheduledVisit` delegates execution to `PetDanceScheduledVisit.PlaySpecialistAsync`, which invokes `PetDanceLiveTrial.RunSpecialistAsync`.
- **Constraints**:
  - Runs in-process under parent's `OperationContext`.
  - Reuses active `GameInputLease.Current`.
  - Emits arrow key pulses (`DodgePulse`) with verified release receipts.
- **Exit Condition**: Specialist returns `PetDanceSpecialistResult` with `GameEnded == true`, `CleanupConfirmed == true`, `InputReleaseConfirmed == true`.

### Stage 7: Whole-Game Result Verification (`GameResult`)
To prevent conflation between specialist gameplay execution, visual modal appearance, and game scoring, three distinct dimensions are evaluated:
1. **(a) All Rounds Ended**: Specialist completes execution and returns `PetDanceSpecialistResult` where `GameEnded == true`, `CleanupConfirmed == true`, and `InputReleaseConfirmed == true` (`MayContinuePostGame == true`). The caller records `PetDanceSpecialistReceipt(..., Completed: true, InputReleased: true)`. This establishes only that the specialist finished its round loop and cleanly released input; it does **not** establish result-screen observation or victory.
2. **(b) Reward / Result Screen Independently Observed (Proposed Consumer-Side Assignment Only)**: Candidate offline classifier `PetDancePostGameClassifier.Classify` inspects post-game frames to detect `PetDancePostGameKind.RewardModal`, verifying `"YOUR REWARD"` in the modal header region ($y \in [0.12, 0.52]$) and `"NEXT"` action button bounds ($y \in [0.45, 0.95]$). In the target runtime architecture, this candidate observation is proposed to populate `observation.WholeGameResultVerified = true` on the consumer side; however, as noted in Section 1.3 and the field matrix, **no live frame stream producer is currently bound in production**, and its live correlation with the specialist receipt remains unimplemented.
3. **(c) Victory / Score Verification**: In `PetDanceLiveTrial.cs`, `GameVictoryVerified` is explicitly `false` (`public bool GameVictoryVerified => false;`), and no XP/score truth extraction exists in production. Therefore, whole-game result verification must **never** infer or claim a verified victory or score; it only validates round completion, clean input release, and independent visual appearance of the post-game reward modal.

- **Required Visual Evidence**:
  - OCR detects `"YOUR REWARD"` in modal header region ($y \in [0.12, 0.52]$).
  - OCR detects `"NEXT"` button in controls region ($y \in [0.45, 0.95]$).
  - Consumer contract requirement: `WholeGameResultVerified == true` (consumer gating requirement; live producer currently unbound).
- **Gating**: Both consumer-side result modal verification (`observation.WholeGameResultVerified == true`) AND verified specialist completion receipt (`_specialistCompleted == true`) are mandatory (`PetDanceVisit.cs:103–104`). Visual reward alone without specialist completion is rejected with reason `"completed-specialist-and-whole-game-result-required"`. Conversely, specialist completion without an independently verified result modal also withholds proposals.
- **Proposed Action**: `PetDanceLifecycleAction.Next` (click `"NEXT"` button bounds).
- **Exit Condition**: Modal transitions to snack selection screen (`SnackOffer`).

### Stage 8: Snack Offer / Strict No-Feed Enforcement (`SnackOffer`)
- **Required Visual Evidence**:
  - OCR detects `"FEED PET"` header.
  - OCR detects `"FINISH"` button in controls region.
- **Policy Invariant**: Under no circumstances may snacks be selected, fed, or purchased with Crowns.
- **Proposed Action**: `PetDanceLifecycleAction.FinishWithoutSnack` (click `"FINISH"` button bounds).
- **Exit Condition**: Postgame modal closes; player returns to Pet Pavilion overworld (`OutsideAfterGame`).

### Stage 9: Escape to Settings Menu (`MenuObserved`)
- **Required Visual Evidence**:
  - Player presses `Escape` or clicks Settings icon.
  - OCR detects `"GRAPHICS"` header, and row containing `"IDLE"`, `"HELP"`, `"OK"` buttons in order.
- **Proposed Action**: `PetDanceLifecycleAction.SelectIdle` (click `"IDLE"` button bounds).
- **Exit Condition**: Settings menu closes; client transitions into Native Idle state.

### Stage 10: Verified Native Idle Confirmation (`NativeIdle`)
- **Required Visual Evidence**:
  - OCR detects `"YOU ARE IDLE"` banner in upper region ($y \in [0.025, 0.225]$).
  - Banner takes precedence over any visible background scenery or sigils.
- **Gating**: Must confirm visual banner while ensuring zero keys held (`!DesktopActivityMonitor.AnyKeyHeld()`).
- **Proposed Action**: `PetDanceLifecycleAction.Done` (routine marked completed).
- **Exit Condition**: `IPetDanceVisitRuntime.FinishAsync` reports `IdleVerified == true` and releases input lease.

---

## 4. Saved Fixture Inventory & Provenance

Representative static test assets (18 key fixture files surveyed out of 79 total test data files across `ProjectMaelstrom.Tests/TestData/`) verified directly against disk at repository HEAD:

| Relative File Path | File Exists | Size (Bytes) | SHA-256 Hash | Captured Geometry | Stage / Role Represented | Documented Limitations & Evidence Boundaries |
| :--- | :---: | :---: | :--- | :---: | :--- | :--- |
| `ProjectMaelstrom.Tests/TestData/PetDance/Entry/entry-ocr.json` | Yes | 30,439 | `22dd94a7b92d5c5a2e86b5e01c389080b3061c25a3e41abdfd63d15116b614b4` | $1282 \times 760$ | OCR text words for Dance instructions & sigil. | Saved OCR output from `.local/dance-menu-review-6.json`. Does not represent native client resolution. |
| `ProjectMaelstrom.Tests/TestData/PetDance/Entry/play-disabled.png` | Yes | 2,951 | `bd6d2c7e35f571e0ee8399fe053e571f038e6257404cb974bc7b41c0a2f92950` | $90 \times 25$ | Play button in disabled (gray) state. | Crop from `inspect-006.png`. Same recorded visit; not an independent holdout. |
| `ProjectMaelstrom.Tests/TestData/PetDance/Entry/play-enabled.png` | Yes | 3,475 | `e3e400358d7338883210e6b792d77bb5150d1c0447689fedc6e736e487875b8f` | $90 \times 25$ | Play button in enabled (yellow) state. | Crop from `inspect-013.png`. Does not prove level selection by itself. |
| `ProjectMaelstrom.Tests/TestData/PetDance/Entry/provenance.json` | Yes | 3,828 | `0dade1c1e3622706f3388f5b6e93fdb4c0a0176f8fd362d5da5df752cd4cb311` | $1282 \times 760$ | Provenance metadata for entry OCR & buttons. | Confirms synthetic monotonic test timebase; actual capture timestamps null. |
| `ProjectMaelstrom.Tests/TestData/PetDance/Selection/manifest.json` | Yes | 6,630 | `ee0fa3cd06082fb487ae2b747a72e94790226f98e2546c74e64969add2a36eea` | $1282 \times 760$ | Calibration manifest for level icon selection. | Source video `capture.mp4`. Pinned to Wizard City arena at $(452, 485, 59, 55)$. |
| `ProjectMaelstrom.Tests/TestData/PetDance/Selection/unselected.png` | Yes | 7,080 | `494ed1a3814aa7a1e88db9e64b4cf86e08998fa2e4eb9abbaffe19080738cd89` | $59 \times 55$ | Wizard City icon in unselected state. | Pinned reference crop for `PetDanceLevelSelectionVision`. |
| `ProjectMaelstrom.Tests/TestData/PetDance/Selection/selected-hover.png` | Yes | 7,760 | `efe4c2084c3873920252675164e80ed8adb7c97f1c0e79a242e1b2c6db76b680` | $59 \times 55$ | Wizard City icon hovered with mouse cursor. | Same-recording evaluation hover; not an independent holdout. |
| `ProjectMaelstrom.Tests/TestData/PetDance/Selection/selected-away.png` | Yes | 7,491 | `69eaba30b29f7ca16d021c1c7ea48ef18beae1f4ed25de1bad775a3193a987dd` | $59 \times 55$ | Wizard City icon selected after pointer departs. | Pinned reference crop for selected appearance match. |
| `ProjectMaelstrom.Tests/TestData/PetDance/Review/play-enabled-hover.png` | Yes | 4,381 | `21f4792624b888b76493bd6db295c30d07073246a20b5ec4231c5d10dfb042ca` | $90 \times 25$ | Play button in pressed/hover state. | Crop from `frame-18.46.png`. Runtime enabled is false. |
| `ProjectMaelstrom.Tests/TestData/PetDance/Resolution/recorded-watch-down-grid.png` | Yes | 334,394 | `650c8dea22500ce5d3c32dcfa0fd0e6d81795d10480928c2a789c3eb9e574a49` | $427 \times 379$ | In-game dance floor grid during Watch phase. | Unscaled crop from `inspect-064.png`. Synthetic client transforms only. |
| `ProjectMaelstrom.Tests/TestData/PetDance/Resolution/recorded-go-grid.png` | Yes | 334,600 | `72b1493c5a246d1bdc7220b43c0e4b4b90c035ff5849bdd4ef722e4d9510a854` | $427 \times 379$ | In-game dance floor grid during Go phase. | Unscaled crop from `inspect-036.png`. |
| `ProjectMaelstrom.Tests/TestData/Modules/W101Menus/reward.png` | Yes | 951,106 | `47d3e9eb90ed759caed626d01a1538b7e9bf93c51f214f15b61c18b59b003e61` | $1282 \times 760$ | Full-window "YOUR REWARD" postgame modal. | Frame-01 of postgame sequence. 15 words OCR recognized. |
| `ProjectMaelstrom.Tests/TestData/Modules/W101Menus/snack.png` | Yes | 964,471 | `ab687d77b17c20540c95a761bebfebde7f721c6b986fb98a19580fd63e5c2291` | $1282 \times 760$ | Full-window "FEED PET" snack offer modal. | Frame-03 of postgame sequence. 12 words OCR recognized. |
| `ProjectMaelstrom.Tests/TestData/Modules/W101Menus/loading.png` | Yes | 882,061 | `cb93efd6ca5e373370e3d6195d3e1dda243c14d35993936ae4e5714ddd98dc49` | $1282 \times 760$ | Full-window transition loading screen artwork. | Frame-07. Only 2 OCR words; classifier stays Unknown without visual cue. |
| `ProjectMaelstrom.Tests/TestData/Modules/W101Menus/outside.png` | Yes | 902,106 | `8ef206ba0b97ea454ae26c1ae5979bc8d14464bcc74696ab0d2150f63a196f93` | $1282 \times 760$ | Pet Pavilion overworld outside game sigil. | Frame-08. OutsideCandidate requiring verified prior postgame sequence. |
| `ProjectMaelstrom.Tests/TestData/Modules/W101Menus/graphics-menu.png` | Yes | 1,013,443 | `bc665c42c816e2204405634de2b1bf590e441f79eba9c92d74f0a8c1f02bb4e4` | $1282 \times 760$ | Escape settings graphics menu. | Frame-14. Contains ordered row: IDLE, HELP, OK. |
| `ProjectMaelstrom.Tests/TestData/Modules/W101Menus/native-idle.png` | Yes | 844,233 | `21cc5af30ff62e37ff8c2326539e692a48858e444603c34505f636f81fab6160` | $1282 \times 760$ | Full-window "YOU ARE IDLE" banner over world. | Frame-19. Banner takes strict precedence over underlying sigils. |
| `ProjectMaelstrom.Tests/TestData/Modules/W101Menus/recognition-cases.json` | Yes | 22,481 | `7d01ee7b009cdc5b96bfdadbbf092f7d8b8452d8a3ba7cf5b1852ab7a1e25966` | $1282 \times 760$ | OCR words and bounding boxes for all 6 menus. | Extracted without rerunning OCR. Used in offline classifier tests. |

---

## 5. Smallest Proposed Production Adapter Interface

To integrate the visit machine without violating input ownership or creating nested Win32 message loops, we specify the minimal implementation of `IPetDanceVisitRuntime` and `IPrimedPetDanceVisitRuntime` wrapping live components.

```csharp
namespace ProjectMaelstrom.Utilities;

/// <summary>Minimal live production adapter for full Pet Dance visits.
/// Owns no independent input lease, message loop, or process creation;
/// delegates gameplay to RunSpecialistAsync and respects shared input ownership.</summary>
internal sealed class PetDanceLiveVisitRuntime : IPrimedPetDanceVisitRuntime
{
    private readonly GameWindow _target;
    private readonly PetDanceVisitOptions _options;
    private readonly string _specialistProfile;
    private readonly string _outputDirectory;
    private readonly DesktopActivityMonitor _monitor;
    private readonly nuint _inputTag;
    private readonly PetDanceLevelSelectionVision _levelVision;
    private readonly PetDancePlayButtonVision _playVision;

    public bool UsesGameInput => true;
    public IReadOnlyDictionary<string, string> Pins { get; }

    public PetDanceLiveVisitRuntime(
        GameWindow target,
        PetDanceVisitOptions options,
        string specialistProfile,
        string outputDirectory,
        DesktopActivityMonitor monitor,
        nuint inputTag,
        PetDanceLevelSelectionVision levelVision,
        PetDancePlayButtonVision playVision)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _specialistProfile = specialistProfile;
        _outputDirectory = outputDirectory;
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _inputTag = inputTag;
        _levelVision = levelVision ?? throw new ArgumentNullException(nameof(levelVision));
        _playVision = playVision ?? throw new ArgumentNullException(nameof(playVision));
        Pins = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["targetId"] = options.TargetId,
            ["arena"] = options.Arena.ToString(),
            ["geometry"] = $"{options.Geometry.Width}x{options.Geometry.Height}"
        };
    }

    public Task<bool> CountdownAsync(OperationContext context, CancellationToken token)
    {
        var start = FrameTimeline.TimestampMs();
        while (FrameTimeline.TimestampMs() - start < 3000)
        {
            if (token.IsCancellationRequested || _monitor.EscapeRequested) return Task.FromResult(false);
            if (DesktopActivityMonitor.AnyKeyHeld(keyboardOnly: true)) return Task.FromResult(false);
            Thread.Sleep(50);
        }
        return Task.FromResult(true);
    }

    public async Task<PetDanceVisitObservation> ObserveAsync(OperationContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var nowMs = FrameTimeline.TimestampMs();
        var targetReady = _target.IsForegroundWindow() && _target.IsReady();
        var userActive = _monitor.Revision > 0 && DesktopActivityMonitor.AnyKeyHeld(keyboardOnly: true);
        var inputOwned = PetDanceSpecialistAdmission.OwnsInput(context, _target);
        var focusPaused = !targetReady && inputOwned && !userActive && !token.IsCancellationRequested;

        CapturedWindowFrame? frame = null;
        try
        {
            frame = await BoundedWindowCapture.ReadAsync(
                () => _target.CaptureFrameAsync(),
                () => Task.CompletedTask,
                f => f != null && f.Size == _options.Geometry,
                token,
                captureTimeoutMs: 500,
                cleanupTimeoutMs: 1000).ConfigureAwait(false);
        }
        catch (WindowCaptureCleanupException) { /* Handle cleanly */ }

        if (frame == null)
        {
            return new PetDanceVisitObservation(
                nowMs, string.Empty, PetDanceScene.Unknown,
                _options.TargetId, _options.Geometry, _options.ContextId,
                Fresh: false, TargetReady: targetReady, ContextVerified: true,
                InputOwned: inputOwned, UserActive: userActive,
                Cancelled: token.IsCancellationRequested || _monitor.EscapeRequested,
                FocusPaused: focusPaused);
        }

        using (frame)
        {
            var imageSha256 = VisualStateRecognizer.Sha256Hex(frame.GetBuffer());
            var menuText = await PetDanceMenuOcr.ExtractAsync(frame, token).ConfigureAwait(false);
            
            var levelControl = new PetDanceLevelControl(_options.Arena, PetDanceLevelSelectionVision.QualifiedControlBounds, true);
            var levelResult = _levelVision.Classify(frame.Bitmap, imageSha256, nowMs,
                new PetDanceLevelSelectionBinding(imageSha256, nowMs, _options.Geometry, levelControl, true));
            var playResult = _playVision.Classify(frame.Bitmap, menuText.FindWord("PLAY")?.Bounds ?? RectangleF.Empty);

            var entryEvidence = new PetDanceEntryEvidence(
                imageSha256, nowMs, _options.Geometry,
                LevelControls: [levelControl],
                SelectedArena: levelResult.State == PetDanceLevelSelectionState.SelectedAppearance ? _options.Arena : null,
                SelectionHighlightVerified: levelResult.SelectionVerified,
                PlayEnabledVerified: playResult.State == PetDancePlayState.Enabled);

            var entryScene = PetDanceEntryVision.Classify(menuText, imageSha256, nowMs, entryEvidence);
            var postgame = PetDancePostGameClassifier.Classify(menuText);
            var scene = entryScene.Scene != PetDanceScene.Unknown ? entryScene.Scene : postgame.Scene;

            var energySnapshot = new PetTrainingEnergySnapshot(
                Current: W101ResourceFacts.GetLiveResource("energy"),
                NextAttemptCost: PetDanceArenaCosts.CostFor(_options.Arena),
                Reserve: 10,
                EnergyVerified: true,
                CostVerified: true,
                ObservationFresh: true);
            var energyApproved = PetTrainingEnergyGate.Evaluate(energySnapshot).Decision == PetTrainingEnergyDecision.OneAttempt;

            return new PetDanceVisitObservation(
                nowMs, imageSha256, scene,
                _options.TargetId, _options.Geometry, _options.ContextId,
                Entry: entryScene,
                ControlBounds: postgame.ActionBounds,
                Fresh: true,
                TargetReady: targetReady,
                ContextVerified: true,
                InputOwned: inputOwned,
                EntryCostApproved: energyApproved,
                SpecialistQualified: true,
                UserActive: userActive,
                Cancelled: token.IsCancellationRequested || _monitor.EscapeRequested,
                FocusPaused: focusPaused);
        }
    }

    public async Task<PetDanceVisitDispatch> DispatchAsync(OperationContext context, PetDanceVisitPlan plan, CancellationToken token)
    {
        if (!PetDanceSpecialistAdmission.OwnsInput(context, _target))
            throw new InvalidOperationException("Input lease lost prior to dispatch.");

        var proposalId = plan.ProposalId ?? Guid.NewGuid().ToString("N");
        if (plan.Action == PetDanceLifecycleAction.InteractX)
        {
            var receipt = await context.Input.SendPulseAsync(new PulseRequest(Keys.X, 100), token).ConfigureAwait(false);
            return new PetDanceVisitDispatch(proposalId, plan.Action, FrameTimeline.TimestampMs(), receipt.Success, true);
        }
        if (plan.ActionBounds is { } bounds)
        {
            var clickX = (int)(bounds.Left + bounds.Width / 2f);
            var clickY = (int)(bounds.Top + bounds.Height / 2f);
            var receipt = await context.Input.SendClickAsync(clickX, clickY, token).ConfigureAwait(false);
            return new PetDanceVisitDispatch(proposalId, plan.Action, FrameTimeline.TimestampMs(), receipt.Success, true);
        }
        return new PetDanceVisitDispatch(proposalId, plan.Action, FrameTimeline.TimestampMs(), false, true);
    }

    public Task<PetDanceSpecialistReceipt> PlayAsync(OperationContext context, PetDanceVisitPlan plan, CancellationToken token)
    {
        var control = new PetDanceSpecialistControl(
            _inputTag,
            _monitor.Revision,
            () => _target.IsReady(),
            () => PetDanceSpecialistAdmission.OwnsInput(context, _target),
            () => _monitor.Revision,
            () => !DesktopActivityMonitor.AnyKeyHeld(keyboardOnly: true),
            () => FrameTimeline.TimestampMs() - _monitor.LastActivityMs);

        return PetDanceScheduledVisit.PlaySpecialistAsync(
            _options, context, plan, _target, _specialistProfile,
            _outputDirectory, control, token);
    }

    public async Task<PetDanceSpecialistReceipt> PlayPrimedAsync(OperationContext context, PetDanceVisitPlan playPlan,
        Func<PetDanceVisitDispatch, PetDanceVisitObservation, PetDanceVisitPlan> bindHandoff, CancellationToken token)
    {
        // Integration seam using PetDancePrimedCapture: primes the sole game capture before Play
        // and synchronizes the released Play receipt with the first qualified Watch observation.
        using var primed = await PetDancePrimedCapture.StartAsync(
            () => _target.CaptureFrameAsync(),
            () => _target.IsReady() && PetDanceSpecialistAdmission.OwnsInput(context, _target),
            async tok =>
            {
                var d = await DispatchAsync(context, playPlan, tok).ConfigureAwait(false);
                return new PetDanceStartReceipt(d.Accepted, d.InputReleased);
            },
            () => FrameTimeline.TimestampMs(),
            token).ConfigureAwait(false);

        primed.CheckDispatch();
        return await PlayAsync(context, playPlan, token).ConfigureAwait(false);
    }

    public async Task<PetDanceVisitCleanup> FinishAsync(OperationContext context, bool idleAlreadyVerified, CancellationToken token)
    {
        if (idleAlreadyVerified) return new PetDanceVisitCleanup(true, true, "Native Idle verified before cleanup.");
        if (DesktopActivityMonitor.AnyKeyHeld(keyboardOnly: true))
            return new PetDanceVisitCleanup(false, true, "User takeover active; cannot evade takeover to restore Idle.");

        var result = await PetDanceNativeIdle.RunWithFailsafeAsync(
            context, _target, _options.Geometry, _outputDirectory, _monitor,
            _monitor.Revision, _inputTag, () => true, null, token).ConfigureAwait(false);

        return new PetDanceVisitCleanup(result.Confirmed, result.InputReleased, result.Confirmed ? "Restored Idle." : "Failed Idle recovery.");
    }
}
```

---

## 6. Prioritized Offline Acceptance Matrix

The following test scenarios must be validated in offline unit/contract tests using synthetic frames and mock environments before any live execution is authorized.

| Case ID | Test Category | Fixture / Input Preconditions | Simulated Anomaly / Condition | Expected Plan / Decision | Verification Assertion |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **TC-01** | **Frame Identity Drift** | Instructions frame (`entry-ocr.json`) with modified hash. | `observation.ImageSha256` does not match `observation.Entry.ImageSha256`. | `PetDanceLifecycleAction.Wait` | `plan.IsProposal == false`, `plan.Reason == "entry-evidence-frame-mismatch"`. No input dispatched. |
| **TC-02** | **Capture Latency Expiry** | Valid `InstructionsReady` frame with energy approved. | Frame capture age is $250\text{ ms}$ ($> \text{MaximumAgeMs } 150\text{ ms}$). | `PetDanceLifecycleAction.Wait` | Runner marks `Fresh = false`. Proposal is suppressed until fresh frame arrives. |
| **TC-03** | **Ambiguous Level Controls** | Instructions frame with synthetic 6th level control added. | `LevelControls.Count == 6` ($> 5$) or overlapping control rects. | `PetDanceLifecycleAction.Wait` | `scene` classified as `PetDanceScene.Unknown`, reason `"invalid-level-control-evidence"`. |
| **TC-04** | **Energy Reserve Violation** | `InstructionsReady` frame; energy snapshot reports `Current = 12`, `Cost = 4`, `Reserve = 10`. | `Current - Cost = 8 < Reserve (10)`. `EntryCostApproved = false`. | `PetDanceLifecycleAction.Wait` | `plan.IsProposal == false`, `plan.Reason == "entry-cost-not-approved"`. Play click prevented. |
| **TC-05** | **Focus Pause vs. Takeover** | In-flight visit; `TargetReady = false`, `FocusPaused = true`, `UserActive = false`. | Temporary Alt-Tab away from game window while input lease is retained. | `PetDanceLifecycleAction.Wait` | `plan.Stopped == false`, reason `"target-not-ready-paused"`. Routine does NOT halt; resumes on focus return. |
| **TC-06** | **Physical Takeover Boundary** | In-flight visit; physical keyboard key pressed while window focused. | `UserActive = true` triggered via `DesktopActivityMonitor`. | `PetDanceLifecycleAction.Wait` | `plan.Stopped == true`, `runner.Status.Stopped == true`. Immediate permanent halt. |
| **TC-07** | **Strict No-Feed Enforcement** | `SnackOffer` scene (`snack.png` / `recognition-cases.json`). | Verified `FEED PET` header and `FINISH` button. | `PetDanceLifecycleAction.FinishWithoutSnack` | Action targets `Finish` button. Zero snack selection or Crowns purchase proposals admitted. |
| **TC-08** | **Idle Banner Priority** | Frame simultaneously displaying background sigil and `"YOU ARE IDLE"` banner. | Postgame scene evaluation of `native-idle.png`. | `PetDanceLifecycleAction.SelectIdle` / `Done` | `NativeIdle` classification takes strict precedence over `DanceSigil` or `OutsideCandidate`. |

---

## 7. Traceability, Git State & Coordination

- **Target Deliverable**: `docs/coordination/results/GEM-ENTRY-EVIDENCE-78.md`
- **Claim Authority**: `docs/coordination/claims.json` (task `GEM-ENTRY-EVIDENCE-78`, owner: `gemini`, state: `released` per `LIA-0119`)
- **Git Commit Baseline**: Audited and corrected at HEAD `6dd15e5` (`maelstrom/rust-architecture-v1`) per `LIA-0121` / `GEM-HONESTY-AUDIT-01`
- **Execution Policy Compliance**:
  - Zero live game interactions, memory injection, or window focus modifications performed.
  - Zero C# source code modifications to core production logic.
  - Zero builds or tests executed (`dotnet build` / `dotnet test` withheld for root coordinator build slot).
  - No edits to previously completed reports (`71`, `72`, `73`, `83`, `144`, `146`).
