# GEM-DANCE-RESYNC-95: Pet Dance Queued-Key, Phase Recognition, and Focus Resynchronization Audit (Revised)

**Author**: Gemini  
**Date**: 2026-11-14  
**Directive**: LIA-0047 / LIA-0048 / GEM-DANCE-RESYNC-95  
**Status**: REVISED (READY_FOR_REVIEW)  
**Operating Rules**: Read-only static review; zero source edits; zero builds/tests executed; no live game or desktop interaction.

---

## 1. Executive Summary & Review Revisions

Directive `LIA-0048` reviews `GEM-0049` and requests targeted revisions to `GEM-DANCE-RESYNC-95`:
1. **`PetDanceSpecialistControl.InputReady` Trace**: Clarified that `InputReady` is a supplied delegate (`Func<bool>`), not an intrinsically stubbed record. Traced each actual construction and call site across the codebase.
2. **Cue Fidelity Qualification**: Corrected the assertion that a clean `Watch` rising edge proves "100% cue fidelity." A clean onset only prevents starting mid-demonstration; complete cue fidelity strictly requires unbroken frame coverage, fresh-frame admission, and multi-modal sequence agreement throughout the demonstration.
3. **Exact Existing APIs vs. Proposed Additions**: Explicitly partitioned reset capabilities into exact existing methods (`pending.Clear()`, `RejectRound`, local variable resets) versus proposed additions (`visualSequence.Reset()`, `soundSequence.Reset()`, `watchOnsetObserved` latch).
4. **Active Pulse Release Guarantees**: Traced `DodgeInputPulse.RunAsync` to establish whether a failed active pulse releases before entering any wait. Established that if key-down occurred without confirmed key-up (`DownMs != null && UpMs == null`), release is uncertain and focus waiting is strictly prohibited.
5. **Scenario Status**: Preserved the three concrete regression scenarios and marked all as proposed specifications.

---

## 2. Static Architecture Trace: Queued Keys, Phase Recognition, and Primed Capture

### 2.1 Trace of `PetDanceSpecialistControl.InputReady` Call Sites
`PetDanceSpecialistControl` is an immutable record defined in `PetDanceLiveTrial.cs:88`:
```csharp
internal sealed record PetDanceSpecialistControl(nuint InputTag, long InitialRevision,
    Func<bool> ObservationReady, Func<bool> InputReady, Func<long> ActivityRevision,
    Func<bool> NoKeysHeld, Func<double> QuietMs);
```
It does **not** intrinsically stub `InputReady`. The delegate is supplied by the caller:
- **Call Site 1 (`PetDanceLiveTrial.cs:242–245`)**:
  ```csharp
  var control = new PetDanceSpecialistControl(tag, baseline,
      () => monitor.Installed && !monitor.EscapeRequested && FrameTimeline.TimestampMs() - Volatile.Read(ref heartbeat) < 1500,
      () => true, () => monitor.Revision, () => !DesktopActivityMonitor.AnyKeyHeld(),
      () => FrameTimeline.TimestampMs() - monitor.LastActivityMs);
  ```
  Here, the caller passes `() => true`. This caller-supplied delegate bypasses foreground and focus checking, asserting that input is always ready even when Wizard101 is not the active window.
- **Call Site 2 (`PetDanceScheduledVisit.cs:27–37`)**:
  `PetDanceScheduledVisit.PlaySpecialistAsync` receives a `PetDanceSpecialistControl` instance from its caller and forwards it directly to `PetDanceLiveTrial.RunSpecialistAsync`.
- **Call Site 3 (`PetDanceSpecialistContractTests.cs:44, 58`)**:
  Synthetic contract tests construct `PetDanceSpecialistControl` passing mock delegates (`() => true` or counting delegates) for offline assertion.

### 2.2 Primed Capture Lifecycle (`PetDancePrimedCapture.cs`)
- **Initialization**: `PetDancePrimedCapture.StartAsync` (`PetDanceLiveTrial.cs:395–396`) initiates frame capture *before* dispatching the entry Play click.
- **In-Flight Enforcement**: `primed.CheckDispatch()` (`line 420`) verifies bounded dispatch. If `primed.Pending == true`, key dispatch is blocked (`lines 421, 622`).
- **Disposal & Release**: The `finally` block (`lines 707–712`) calls `await primed.FinishAsync()`. If unconfirmed, `releases.MarkUncertain()` marks release uncertain.

### 2.3 Phase Recognition & Frame Gate (`PetDanceLiveTrial.cs:422–520`)
- **Frame Reading**: `observer.ReadTimestampedAsync(latestOnly: true)` $\to$ `PetDanceObservationRead.ReadAsync`.
- **Admission Gate**: `PetDanceTrialFrameGate.Inspect(capturedMs, last, now, ...)` ensures capture timestamps are fresh (`PetDanceFrameAdmission.Fresh`).
- **Vision Extraction**: `vision.Extract(frame.Image)` $\to$ `vision.Read(panel, time)` yields `DanceObservation(Phase, Cue, FadeOf, Opacity, ...)`.
- **Recognized Phases**: `DancePhase.Ready`, `DancePhase.Loading`, `DancePhase.Watch`, `DancePhase.Go`, `DancePhase.Resolving`, `DancePhase.Success`, `DancePhase.Failure`.

### 2.4 Queued-Key State & Response Window (`PetDanceLiveTrial.cs:536–680`)
- **Queue Variable**: `Queue<string> pending` (`line 353`).
- **Enqueuing**: When `selectedResponse.Accepted && !roundRejected`, cues are enqueued into `pending` (`line 580`).
- **Response Window Watchdog**: `responseWindow.Estimate(now, pending.Count, nextKey).Expired` (`line 427`). If elapsed time since `Go` onset exceeds 5000ms, `RejectRound(now, ...)` is called.
- **Inter-Round Boundary Guard**: `PetDanceTrialFrameGate.Pending(observation.Phase, ...)` (`lines 615–620`): Discards old keys if `DancePhase.Watch` appears before feedback.

---

## 3. Pulse Execution and Release Guarantees Before Waiting

A critical safety invariant is determining **whether a failed active pulse releases before entering any focus wait**:

In `PetDanceLiveTrial.cs:651–658`:
```csharp
try
{
    receipt = await pulse.RunAsync(target, client, key, (int)PetDanceResponseWindow.KeyHoldMs, time, token,
        maximumCaptureAgeMs: PetDanceTrialFrameGate.MaximumAgeMs, responseDeadlineMs: responseWindow.DeadlineQpcMs).ConfigureAwait(false);
}
catch { releases.MarkUncertain(); throw; }
releases.Record(receipt);
```

Tracing `DodgeInputPulse.RunAsync` (`DodgeInputPulse.cs:27–88`):
1. **Pre-Down Rejections (`DownMs == null`)**:
   - If capture age exceeds 200ms (`"stale-or-invalid-frame"`), if target client size mismatches, or if `responseDeadlineMs` is exceeded before key-down, the pulse returns immediately without sending key-down (`DownMs == null, UpMs == null`).
   - *Safety*: No physical input was sent. Release is trivially confirmed. It is safe to discard the round or enter a focus wait.
2. **Post-Down Key Release (`DownMs != null`)**:
   - Once `sink.KeyDown(key)` is invoked (`line 73`), the pulse executes a `finally` block (`lines 76–84`):
     ```csharp
     try { sink.KeyUp(key); up = _clock(); }
     catch (Exception ex)
     {
         status = "release-failed";
         try { sink.KeyUp(key); up = _clock(); status = "release-retried"; }
         catch (Exception retry) { GameInputLease.Current?.RequireRecovery(reason); }
     }
     ```
   - If `sink.KeyUp` succeeds on the first attempt or retry, `up` is timestamped (`UpMs != null`). The key was physically released.
   - If `sink.KeyUp` throws on both attempts, `up` remains `null`. `GameInputLease.Current?.RequireRecovery(...)` is invoked.
3. **The Focus-Wait Boundary**:
   > [!CRITICAL]
   > - If `receipt.DownMs != null && receipt.UpMs == null`, key release is **unconfirmed**. `releases.MarkUncertain()` marks release uncertain.
   > - A focus wait is **strictly prohibited** in this state. The system cannot wait or resume; it must abort immediately to scheduler recovery to resolve potentially stuck keys.
   > - Focus waiting is only permitted when `receipt.UpMs != null` or when no key-down was attempted (`receipt.DownMs == null`).

---

## 4. Reset Inventory: Existing APIs vs. Proposed Additions

To support clean resynchronization after focus loss, state reset mechanisms must be separated into what currently exists versus what must be proposed:

| Scope | Mechanism / API | Status | Location / Implementation Detail |
| :--- | :--- | :---: | :--- |
| **Key Queue** | `pending.Clear()` | **EXISTING** | `Queue<string>.Clear()` called in `PetDanceLiveTrial.cs:368, 552, 594, 610`. |
| **Round Latch** | `RejectRound(time, why)` | **EXISTING** | Local helper in `PetDanceLiveTrial.cs:366–371`. Clears `pending`, sets `roundRejected = true`, records to `rejectedRounds`. |
| **Cue Metadata** | Variable Nulling | **EXISTING** | Local resets in `PetDanceLiveTrial.cs:552–553, 610–611`: `latestAgreement = null`, `latestVisual = null`, `selectedResponse = null`, `currentSoundCues = []`, `watchStart = -1`, `cueCount = 0`, `previousSnapshotCue = DanceCue.Unknown`, `savedVisualRejection = false`, `nextKey = 0`. |
| **Response Window** | `responseWindow.Observe(time, phase)` | **EXISTING** | Updates internal phase timestamp in `PetDanceResponseWindow.cs`. |
| **Boundary Guard** | `PetDanceTrialFrameGate.Pending` | **EXISTING** | Returns `PetDanceInputContinuation.DiscardForWatch` if `Watch` appears while keys are pending (`line 615`). |
| **Visual Sequence** | `visualSequence.Reset()` | **PROPOSED** | *Does not exist*. Currently resets only on `visualSequence.NewPreview` detection (`PetDanceVisualSequence.cs:40`). |
| **Sound Sequence** | `soundSequence.Reset()` | **PROPOSED** | *Does not exist*. Currently flushes state only on `soundSequence.NewPreview` (`PetDanceSoundSequence.cs:72`). |
| **Watch Onset Latch** | `watchOnsetObserved` flag | **PROPOSED** | *Does not exist*. Needs local state to distinguish rising edge of `Watch` from mid-phase entry. |
| **Focus Wait Hook** | `WaitForFocusAsync` in Control | **PROPOSED** | *Does not exist*. `PetDanceSpecialistControl` has no focus wait delegate. |

---

## 5. Watch Edge vs. Complete Cue Fidelity

Observing a clean rising edge of `DancePhase.Watch` (`priorScene != DancePhase.Watch && observation.Phase == DancePhase.Watch`) is a **necessary prerequisite**, but it **does NOT by itself establish complete cue fidelity**.

### Why an Onset Edge Alone Is Insufficient:
1. **Frame Dropping / Stutter**: If the game client or capture subsystem drops frames during the cue presentation, one or more arrow flashes may occur entirely between captured frames.
2. **Opacity / Transition Thresholds**: A cue is only recognized if `observation.Opacity >= 0.70` and `observation.CueAlignmentQualified == true`. Sub-threshold frames yield `DanceCue.Unknown`.
3. **Multi-Modal Verification**: In modern sequences (`modernSequence == true`), complete sequence confidence requires agreement between visual extraction and audio onset detection (`PetDanceAudioDetector` / `PetDanceSoundSequence`).
4. **Conclusion**: Observing the onset edge guarantees only that observation did not begin *late*. Full sequence qualification still requires uninterrupted fresh-frame delivery (`PetDanceTrialFrameGate.Inspect`), continuous opacity tracking, and sound/visual consensus across the entire demonstration. If any frame gap or sequence anomaly occurs, `decision.Rejected` or `soundSequence` disagreement must withhold the round.

---

## 6. Three Concrete Regression Scenarios [PROPOSED]

### Scenario 1: Focus Lost During `DancePhase.Go` (Active Key Dispatch) [PROPOSED]
- **Initial State**: Round 2. Sequence `["UP", "LEFT", "RIGHT"]` enqueued in `pending`. Cue 1 (`"UP"`) successfully dispatched and released (`UpMs != null`).
- **Event**: Desktop focus is lost (`control.InputReady() == false`).
- **Failure Mode Without Reset**: `pending` retains `["LEFT", "RIGHT"]`. When focus returns 4 seconds later, the 5-second response window has expired. Dispatching `"LEFT"` sends an out-of-time key into the resolving animation or next round.
- **Required Behavior Under Resync**:
  1. Active pulse for Cue 1 confirmed released (`UpMs != null`).
  2. Focus loss triggers immediate existing `pending.Clear()`.
  3. Existing `RejectRound(now, "Focus lost during Go; remaining response keys discarded")` is invoked.
  4. Inputs marked withheld (`inputs_withheld: true`).
  5. Upon focus restoration, system observes `DancePhase.Resolving` or `DancePhase.Failure`. Zero keys are dispatched. System waits for feedback banner to clear.

### Scenario 2: Focus Lost Mid-`Watch` Demonstration (Phase Already Missed) [PROPOSED]
- **Initial State**: Round 4 (5-cue demonstration). Pet shows Cue 1 (`UP`).
- **Event**: Focus is lost for 1.8 seconds during Cue 2 (`RIGHT`) and Cue 3 (`DOWN`). Focus returns while Pet is showing Cue 4 (`LEFT`).
- **Failure Mode Without Reset**: Sequence accumulator only recorded cues 1, 4, and 5. Upon `Go`, it enqueues a 3-key response for a 5-key round, guaranteeing a round failure.
- **Required Behavior Under Resync**:
  1. Focus loss marks proposed latch `watchOnsetObserved = false` and purges partial cue accumulators.
  2. When focus returns during Cue 4, `observation.Phase == DancePhase.Watch`, but `watchOnsetObserved == false`.
  3. State is classified as **`phase-already-missed`**.
  4. Round is rejected (`RejectRound(now, "watch-phase-onset-missed")`).
  5. The bot remains completely quiet through `Go` and feedback, waiting for Round 5 onset.

### Scenario 3: Focus Regained During Inter-Round Blackout or Feedback [PROPOSED]
- **Initial State**: Focus lost during Round 1 feedback. Game transitions into loading blackout between rounds.
- **Event**: Focus is regained and stabilizes during `DancePhase.Loading`.
- **Required Behavior Under Resync**:
  1. Existing `pending.Clear()`, `roundRejected = false`, proposed `watchOnsetObserved = false`.
  2. `clock.IsLoading` is true; active duration pause is held.
  3. Game transitions: `Loading` $\to$ `Ready` $\to$ `Watch`.
  4. Rising edge of `Watch` is observed after focus was restored $\to$ `watchOnsetObserved` is set to `true`.
  5. Observation of Round 2 proceeds with full cue accumulation, subject to continuous frame gate inspection.

---

## 7. Verification Boundary & Test Contract

> [!NOTE]
> All unit tests listed below are **`[PROPOSED ONLY - NOT IMPLEMENTED]`**. They specify exact contract behavior for future implementation by the root coordinator without modifying production code.

### Test Proposal 1: Queued Keys Discarded on Focus Loss [PROPOSED ONLY - NOT IMPLEMENTED]
- **Target**: `PetDanceSpecialistContractTests.cs`.
- **Signature**: `Task RunSpecialistAsync_WhenFocusLostDuringGo_DiscardsPendingKeysAndWithholdsInput()`
- **Contract**: Injects 3 queued keys during `DancePhase.Go`; toggles `control.InputReady()` to false; verifies `pending.Count == 0`, `roundRejected == true`, and zero keys are dispatched to `pulse`.

### Test Proposal 2: Mid-Watch Focus Return Rejects Incomplete Round [PROPOSED ONLY - NOT IMPLEMENTED]
- **Target**: `PetDanceTrialFrameGateTests.cs`.
- **Signature**: `void FrameGate_WhenWatchOnsetMissed_MarksRoundRejected()`
- **Contract**: Simulates `DancePhase.Watch` where initial frame onset was not observed; verifies frame gate flags disposition as `watch-onset-missed` and disallows key scheduling.

### Test Proposal 3: Clean Round Resynchronization After Inter-Round Pause [PROPOSED ONLY - NOT IMPLEMENTED]
- **Target**: `PetDanceSpecialistContractTests.cs`.
- **Signature**: `Task RunSpecialistAsync_WhenFocusRegainedDuringLoading_ObservesNextWatchCleanly()`
- **Contract**: Simulates focus loss during feedback followed by focus regain during `Loading`; verifies next rising edge of `Watch` is accepted, cues are recorded, and response is scheduled.
