# GEM-DANCE-ENTRY-WIRING-97 — Production Pet Dance Scheduled Visit Entry Runtime Wiring Plan

**Task ID**: `GEM-DANCE-ENTRY-WIRING-97`  
**Workstream**: `W101-DANCE`  
**Author**: Gemini  
**Date**: 2026-11-14T21:00:00-05:00  
**Repository Revision**: `7888cf251388dd904bac3977c70f73d0a6a31424` (`maelstrom/rust-architecture-v1`)  
**Status**: READ-ONLY AUDIT & REVISED ENTRY RUNTIME WIRING PLAN (PENDING COORDINATOR REVIEW)  
**Owned Output**: `docs/coordination/results/GEM-DANCE-ENTRY-WIRING-97.md`  

---

## 1. Executive Summary & Purpose

This report fulfills directive `LIA-0049` and incorporates the precision review corrections from `LIA-0050`.

### 1.1 Context & Objective
The Maelstrom Pet Dance architecture features a comprehensive state machine and specialist runner, but lacks a **production implementation of `IPetDanceVisitRuntime`**:
- [PetDanceScheduledVisit.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceScheduledVisit.cs) orchestrates the visit state machine via `IPetDanceVisitRuntime`, but the only existing implementation is an offline unit test mock (`Runtime` in [PetDanceScheduledVisitTests.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom.Tests/PetDanceScheduledVisitTests.cs#L27-L65)).
- [PetDanceVisit.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceVisit.cs) models deterministic transitions across the visit lifecycle (`DanceSigil` $\to$ `Loading` $\to$ `InstructionsUnselected` $\to$ `InstructionsReady` $\to$ `Watch` $\to$ `GameResult` $\to$ `SnackOffer` $\to$ `OutsideAfterGame` $\to$ `MenuObserved` $\to$ `NativeIdle`).
- [PetDanceLiveTrial.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceLiveTrial.cs#L307) provides the in-process specialist execution engine (`RunSpecialistAsync`), which manages its own internal capture observer and supports an optional delegate (`startAfterCaptureReady`) for priming capture before Play dispatch.
- [W101DanceEntryContext.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/W101DanceEntryContext.cs) exposes structural UI tree evidence under `PetGameTracks` (including energy labels `txtEnergyCost` and `txtYourEnergy`), but remains explicitly unqualified discovery data (`structural-unqualified`, `raw-parent-layout-unqualified`, `selection-field-unqualified`).

This document provides a **concrete, minimal wiring plan** for a proposed production runtime (`PetDanceProductionRuntime`), specifying:
1. Exact constructor and delegate signatures (explicitly labeled as proposed).
2. Direct mapping from existing source observations to each entry and handoff stage.
3. An audit of exactly **five missing bindings** and unresolved control geometry.
4. A rigorously qualified inventory separating synthetic isolated matchers, static scene screenshots, and observed telemetry traces from unverified causal game transitions.
5. One proposed offline integration test fixture (`PetDanceProductionRuntimeTests.cs`).

---

## 2. Core Runtime Contract & Delegate Signatures

### 2.1 Interface Implementation Target: `IPetDanceVisitRuntime`
In [PetDanceScheduledVisit.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceScheduledVisit.cs#L9-L18), the host requires:

```csharp
internal interface IPetDanceVisitRuntime
{
    bool UsesGameInput { get; }
    IReadOnlyDictionary<string, string> Pins { get; }
    Task<bool> CountdownAsync(OperationContext context, CancellationToken token);
    Task<PetDanceVisitObservation> ObserveAsync(OperationContext context, CancellationToken token);
    Task<PetDanceVisitDispatch> DispatchAsync(OperationContext context, PetDanceVisitPlan plan, CancellationToken token);
    Task<PetDanceSpecialistReceipt> PlayAsync(OperationContext context, PetDanceVisitPlan plan, CancellationToken token);
    Task<PetDanceVisitCleanup> FinishAsync(OperationContext context, bool idleAlreadyVerified, CancellationToken token);
}
```

### 2.2 Proposed Production Runtime Container: `PetDanceProductionRuntime`

> [!IMPORTANT]
> **API Status**: The class `PetDanceProductionRuntime` and its constructor below are **PROPOSED ONLY - NOT AN EXISTING API**. They specify the minimal shape needed to bind existing production components to `IPetDanceVisitRuntime`.

```csharp
namespace ProjectMaelstrom.Utilities;

/// <summary>[PROPOSED ONLY - NOT AN EXISTING API]
/// Production IPetDanceVisitRuntime implementation binding window observation,
/// entry vision, postgame classification, menu input, and live specialist trial.</summary>
internal sealed class PetDanceProductionRuntime : IPetDanceVisitRuntime, IAsyncDisposable
{
    private readonly GameWindow _target;
    private readonly PetDanceVisitOptions _options;
    private readonly string _specialistProfilePath;
    private readonly string _specialistOutputPath;
    private readonly PetDanceLevelSelectionVision _levelVision;
    private readonly PetDancePlayButtonVision _playVision;
    private readonly Func<bool> _controlPermission;
    private readonly string? _audioComparisonPath;
    private readonly WindowObserver _observer;
    private readonly PetDanceMenuInput _menuInput;
    private readonly DesktopActivityMonitor _activityMonitor;
    private readonly nuint _inputTag;
    
    // State tracking across observations and transitions
    private double _lastCaptureMs;
    private PetDancePrimedCapture? _primedCapture;
    private PetDanceSpecialistResult? _specialistResult;

    public bool UsesGameInput => true;
    public IReadOnlyDictionary<string, string> Pins { get; }

    /// <summary>[PROPOSED ONLY - NOT AN EXISTING API] Constructor signature.</summary>
    public PetDanceProductionRuntime(
        GameWindow target,
        PetDanceVisitOptions options,
        string specialistProfilePath,
        string specialistOutputPath,
        PetDanceLevelSelectionVision levelVision,
        PetDancePlayButtonVision playVision,
        Func<bool> controlPermission,
        string? audioComparisonPath = null,
        nuint? inputTag = null);
}
```

### 2.3 Exact Delegate Signatures for Pipeline Stages

| Delegate Role | Existing Type / Signature | Source Provider | Enforced Invariant |
| :--- | :--- | :--- | :--- |
| **Capture Frame Reader** | `Func<Task<CapturedWindowFrame?>>` | `_observer.ReadTimestampedAsync(latestOnly: true)` | Returns fresh frame; timestamp age $\le 150\text{ ms}$; single capture owner. |
| **Observation Readiness** | `Func<bool>` | `() => !token.IsCancellationRequested && GameWindows.IsCurrent(_target) && !GameWindows.Minimized(_target)` | Validates window state prior to OCR or visual processing. |
| **Input Lease Ownership** | `Func<bool>` | `() => PetDanceSpecialistAdmission.OwnsInput(context, _target)` | Verifies `ReferenceEquals(context.Input, GameInputLease.Current)`. |
| **Activity / Takeover Guard** | `Func<bool>` | `() => _activityMonitor.Installed && !_activityMonitor.EscapeRequested && !DesktopActivityMonitor.AnyKeyHeld()` | Physical keyboard activity halts routine immediately. |
| **Continuing Action Guard** | `Func<bool>` | `() => Ready() && Continuing()` in `PetDanceMenuInput.DispatchSequenceAsync` | Maintained during mouse/key hold; does not grant freshness extension. |
| **Primed Play Dispatcher** | `Func<CancellationToken, Task<PetDanceStartReceipt>>` | Passed to `PetDancePrimedCapture.StartAsync` or `RunSpecialistAsync(startAfterCaptureReady:)` | Coordinates Play mouse click while specialist capture is armed. |
| **Specialist Input Delegate** | `Func<bool>` (`InputReady`) | `() => _activityMonitor.Installed && !DesktopActivityMonitor.AnyKeyHeld() && GameWindows.IsCurrent(_target)` | Enforces foreground & physical key idle during arrow dispatch. |

---

## 3. Stage-by-Stage Entry Pipeline & Observation Supply Mapping

```
[Outside / Sigil] -------------> [Loading Screen] -------------> [Instructions Unselected]
       |                                |                                    |
       | 'X' Key Pulse (Unobserved)     | Observation Only                   | Click Arena Icon (Unobserved)
       v                                v                                    v
[DanceSigil Scene]              [Loading Cleared]               [InstructionsReady Scene]
                                                                             |
                                                                             | Prime Capture & Click Play
                                                                             v
[Native Idle Verified] <--- [Postgame Navigation] <--- [Specialist] <--- [Watch Phase Onset]
```

### 3.1 Stage 1: Dance Sigil Detection (`PetDanceScene.DanceSigil`)
- **Observation Sources**:
  1. Capture frame via `WindowObserver.ReadTimestampedAsync(latestOnly: true)`.
  2. OCR text extraction via `PetDanceMenuOcr.ExtractText(image)`.
  3. Base scene classification via `PetDancePostGameClassifier.Classify(text)` returning `OutsideAfterGame`.
  4. Supplemental sigil title bounding box `VerifiedSigilTitleBounds`:
     - *Qualification*: [provenance.json](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/PetDance/Entry/provenance.json#L28-L34) explicitly states that `sigil-title-unread` is a *"Conservative manual title bounds from saved image; not an automatic title detector."* It does **not** qualify a readable title.
     - `PetDanceEntryVision.Classify` uses this coordinate window ($0.60$ to $0.90$ client height) only to confirm visual framing of the prompt.
- **Gating & Validation**:
  - `InputOwned == true`, `TargetReady == true`, `Fresh == true`.
- **Proposed Action**: `PetDanceLifecycleAction.InteractX`.
- **Dispatch Execution**:
  - Requires keyboard pulse sending scan code `0x2D` (virtual key `'X'`).
  - *Note*: `PetDanceMenuInput` currently lacks `'X'` dispatch (see Missing Binding 1).

### 3.2 Stage 2: Loading Transition (`PetDanceScene.Loading`)
- **Observation Sources**:
  1. Capture frame evaluated by `PetDanceEntryVision.Classify` with `evidence.LoadingVerified == true`.
  2. Conflicting modal text (e.g. `"DANCE GAME"`, `"YOUR REWARD"`) must be absent; if present, scene evaluates to `Unknown`.
- **Gating & Action**:
  - `PetDanceVisit` emits `PetDanceLifecycleAction.Wait` (`IsProposal = false`).
  - Runtime pauses active countdown and execution budget; advances wall clock only.

### 3.3 Stage 3: Instructions Unselected (`PetDanceScene.InstructionsUnselected`)
- **Observation Sources**:
  1. `PetDanceEntryVision.Classify`: OCR detects top-down hierarchy: `"DANCE GAME"`, `"OBJECTIVE"`, `"SELECT LEVEL"`, `"PLAY"`.
  2. `PetDanceLevelSelectionVision.Classify`: Evaluates Wizard City control crop at `QualifiedControlBounds` (`452, 485, 59, 55`). Reports `PetDanceLevelSelectionState.Unselected`.
  3. `W101DanceEntryContext`: Validates UI tree path `("Root", "WorldView", "PetGameTracks")` is present and controls are visible/enabled.
  4. Output `PetDanceEntryScene`: `SelectedArena = null`, `SelectionVerified = false`, `LevelControls = [new(WizardCity, (452, 485, 59, 55), true)]`.
- **Gating & Validation**:
  - Exactly 1 matching arena control with `IdentityVerified == true`.
- **Proposed Action**: `PetDanceLifecycleAction.ChooseLevel`.
- **Dispatch Execution**:
  - Dispatches mouse click at `ActionBounds` (`452, 485, 59, 55`).
  - *Note*: Currently rejected by `PetDanceMenuInput` allowlist (see Missing Binding 1).

### 3.4 Stage 4: Instructions Ready (`PetDanceScene.InstructionsReady`)
- **Observation Sources**:
  1. `PetDanceLevelSelectionVision.Classify`: Reports `PetDanceLevelSelectionState.SelectedAppearance` with `PointerAwayVerified == true` and `PlayEnabledVerified == true`.
  2. `PetDancePlayButtonVision.Classify`: Evaluates button region derived from OCR text `"PLAY"` (`Region(image.Size, observedPlayText)`). Reports `PetDancePlayState.Enabled`.
  3. `W101DanceEntryContext`: Confirms energy labels `txtEnergyCost` and `txtYourEnergy` exist under `wndBkgEnergy`.
  4. `EntryCostApproved`: Evaluated by comparing candidate energy values against arena cost (Wizard City = 4 Energy).
     - *Qualification*: Parsing raw text into numbers yields **unqualified numerical candidates** only; parsing alone does **not** grant `EntryCostApproved = true` (see Section 4.4).
  5. `PetDanceEntryVision.Classify`: Emits `InstructionsReady` with `SelectedArena = options.Arena`, `SelectionVerified = true`, `PlayEnabledVerified = true`, and `PlayBounds = playTextBounds`.
- **Gating & Validation**:
  - `EntryCostApproved == true` is strictly required; otherwise withheld with `"entry-cost-not-approved"`.
- **Proposed Action**: `PetDanceLifecycleAction.Play`.
- **Dispatch Execution & Capture Priming**:
  - Dispatched via `startAfterCaptureReady` delegate into `RunSpecialistAsync` (see Section 4.3).

### 3.5 Stage 5: Watch Phase Onset & Specialist Handoff (`PetDanceScene.Watch`)
- **Execution & Capture Lifecycle**:
  - Traced precisely: `PetDanceLiveTrial.RunSpecialistAsync` manages its **own internal capture observer** (`WindowObserver` at lines 374-375).
  - It does **not** require an externally active capture session to already be running.
  - When `startAfterCaptureReady` is supplied (`Func<CancellationToken, Task<PetDanceStartReceipt>>`), `RunSpecialistAsync` primes its internal capture via `PetDancePrimedCapture.StartAsync(...)` (lines 393-399) and executes the Play click once internal frames are verified ready.
  - Detects the rising edge of `DancePhase.Watch` via visual panel matcher (`PetDancePanelVision`) or audio detector (`PetDanceAudioDetector`).
  - Frame gate (`PetDanceTrialFrameGate.Inspect`) verifies unbroken fresh frames.
- **Gating & Validation**:
  - `WatchStartVerified == true` and `SpecialistQualified == true`.
- **Proposed Action**: `PetDanceLifecycleAction.TeacherHandoff`.
- **Receipt Processing**:
  - `PetDanceScheduledVisit.PlaySpecialistAsync` awaits `RunSpecialistAsync`.
  - Returns `PetDanceSpecialistReceipt` containing `Completed`, `MayContinuePostGame`, and `InputReleaseConfirmed`.

### 3.6 Stage 6: Postgame Navigation & Native Idle Return
- **Observation Sources**:
  1. Postgame scenes: `GameResult` $\to$ `SnackOffer` $\to$ `OutsideAfterGame` $\to$ `MenuObserved` $\to$ `NativeIdle`.
  2. Classified by `PetDancePostGameClassifier.Classify(text)` with action bounds localized by word bounding boxes.
- **Dispatches via `PetDanceMenuInput`**:
  - `GameResult`: Proposes `Next` (clicks Next button).
  - `SnackOffer`: Proposes `FinishWithoutSnack` (clicks Cancel / X button; no feeding admitted).
  - `OutsideAfterGame` / `MenuObserved`: Proposes `OpenMenu` (Escape key) $\to$ `SelectIdle` (clicks Native Idle options tab).
- **Completion**:
  - `NativeIdle` observed $\to$ `PetDanceVisit` completes $\to$ `FinishAsync` confirms release and verifies Idle.

---

## 4. Audit of Five Missing Production Bindings

```
+---------------------------------------------------------------------------------------------------+
|                                 FIVE MISSING PRODUCTION BINDINGS                                  |
+---+-------------------------------------------+-----------------------------------+---------------+
| # | Binding Description                       | Affected Source File & Location   | Severity      |
+---+-------------------------------------------+-----------------------------------+---------------+
| 1 | PetDanceMenuInput Action & Key Allowlist  | PetDanceMenuInput.cs:29-35        | Blocker       |
| 2 | Unresolved Control Geometry               | W101DanceEntryContext.cs:7-9      | Specification |
| 3 | Primed Capture / Play Handoff Seam        | PetDanceLiveTrial.cs:393-399      | Architectural |
| 4 | Energy Approval Policy vs Numerical Text  | PetDanceVisitObservation.cs:8     | Policy Gate   |
| 5 | Production IPetDanceVisitRuntime Class    | PetDanceScheduledVisitTests.cs:27 | Infrastructure|
+---+-------------------------------------------+-----------------------------------+---------------+
```

### 4.1 Missing Binding 1: `PetDanceMenuInput` Action Allowlist and 'X' Key Dispatch
- **Exact Location**: [PetDanceMenuInput.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceMenuInput.cs#L29-L35).
- **Problem Trace**:
  - Line 30 enforces a hardcoded action allowlist:
    ```csharp
    if (plan.Action is not (PetDanceLifecycleAction.Next or PetDanceLifecycleAction.FinishWithoutSnack or
        PetDanceLifecycleAction.OpenMenu or PetDanceLifecycleAction.SelectIdle or ...))
        return new(false, true, "action-not-allowed");
    ```
  - The entry lifecycle actions `PetDanceLifecycleAction.InteractX`, `PetDanceLifecycleAction.ChooseLevel`, and `PetDanceLifecycleAction.Play` are **absent** from this allowlist. Any dispatch attempt returns `Accepted = false, Reason = "action-not-allowed"`.
  - Furthermore, keyboard dispatch (lines 33, 50-51, 114) hardcodes the Escape scan code:
    ```csharp
    private Input Key(bool release) => new() { Type = 1, Data = new() { Keyboard = new() { Scan = 1, Flags = 8u | (release ? 2u : 0u), Extra = tag } } };
    ```
    There is no scan-code mapping for dispatching `'X'` (Scan code `0x2D` / VirtualKey `0x58`) for `InteractX`.
- **Required Minimal Seam**:
  - Either extend `PetDanceMenuInput` to support `InteractX` (scan `0x2D`), `ChooseLevel`, and `Play`, or supply a dedicated `PetDanceEntryInput` dispatcher honoring identical release-guarantee and takeover invariants.

### 4.2 Missing Binding 2: Unresolved Control Geometry & Multi-Resolution Limitation
- **Exact Locations**:
  - [PetDanceLevelSelectionVision.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceLevelSelectionVision.cs#L22-L23):
    `QualifiedGeometry => new(1282, 760); QualifiedControlBounds => new(452, 485, 59, 55);`
  - [W101DanceEntryContext.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/W101DanceEntryContext.cs#L7-L9):
    *"Parent-local rectangles and unknown selected state must never be promoted directly into click bounds or energy approval."*
- **Problem Trace**:
  - `PetDanceLevelSelectionVision` is hardcoded to a single resolution (`1282x760`) and a single arena (`WizardCity`). Any other resolution or arena returns `Unknown("selection-layout-unqualified")`.
  - `W101DanceEntryContext` provides `RawRectangle` for controls under `PetGameTracks`, but these rectangles are in parent-local layout coordinates (`raw-parent-layout-unqualified`), with no coordinate transform to client pixel coordinates.
  - No generalized button localizer or multi-arena layout calculator exists.
- **Specification Invariant**:
  - The production runtime must explicitly reject non-`1282x760` window geometries and non-`WizardCity` arena options with `"unqualified-geometry-or-arena"`. Parent-local UI tree rectangles must never be clicked directly.

### 4.3 Missing Binding 3: Primed Capture Handoff Across Visit Machine Seam
- **Exact Locations**:
  - [PetDanceScheduledVisit.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceScheduledVisit.cs#L93-L106).
  - [PetDanceLiveTrial.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceLiveTrial.cs#L393-L399).
- **Exact Trace of `startAfterCaptureReady`**:
  - `PetDanceLiveTrial.RunSpecialistAsync` creates its own `WindowObserver` (lines 374-375) and does **not** depend on an already-running external capture.
  - When called with `startAfterCaptureReady == null`, it starts observing immediately and expects the minigame to already be in or entering `Watch`.
  - When called with `startAfterCaptureReady != null`, it uses `PetDancePrimedCapture.StartAsync(...)` (lines 393-399) to verify that its newly created internal observer is receiving fresh frames before invoking the callback that sends the Play mouse click.
  - *The Seam Gap*: `PetDanceVisit` models `Play` as an ordinary action proposal before `TeacherHandoff`. If the runtime executes `Play` via `DispatchAsync`, and then later calls `PlayAsync`, `RunSpecialistAsync` starts its capture after Play was already clicked, creating a window where round 1 `Watch` onset can be missed.
- **Required Minimal Seam**:
  - The runtime can bridge this by withholding actual Play click delivery in `DispatchAsync(Play)` and instead passing the Play click as the `startAfterCaptureReady` delegate to `RunSpecialistAsync` during `PlayAsync(TeacherHandoff)`.

### 4.4 Missing Binding 4: Energy Approval Policy vs. Unqualified Numerical Candidates
- **Exact Locations**:
  - [PetDanceVisit.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceVisit.cs#L90):
    `else if (!observation.EntryCostApproved) withheld = "entry-cost-not-approved";`
  - [w101_ui_tree.py](file:///F:/Modlists/AutoWizard101/tools/w101_ui_tree.py#DANCE_ENTRY_TEXT): allowlists `txtEnergyCost` and `txtYourEnergy`.
- **Problem Trace & Precision Distinction**:
  - Parsing numerical text from `txtEnergyCost` and `txtYourEnergy` produces **unqualified numerical candidates** only.
  - Parsing raw numbers does **not** constitute authorization to spend energy (`EntryCostApproved = true`).
  - Approval is a distinct policy check requiring:
    1. Validation that candidate current energy $\ge$ candidate arena cost.
    2. Confirmation against user preferences / deck job settings (`JobCardSpec.Settings["energy_policy"]`).
    3. Corroboration with fresh verified resource facts from `JobFactCatalog` (`energy.current`).
  - Without this policy layer, `EntryCostApproved` must remain `false`, safely withholding `Play`.

### 4.5 Missing Binding 5: Production `IPetDanceVisitRuntime` Implementation Container
- **Exact Location**: [PetDanceScheduledVisitTests.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom.Tests/PetDanceScheduledVisitTests.cs#L27-L65).
- **Problem Trace**:
  - `IPetDanceVisitRuntime` is defined as an internal interface, but the only existing class implementing it is the private test mock `Runtime` in `PetDanceScheduledVisitTests`.
  - In `JobDeckRoutines.cs`, the routine `w101.pet.dance` cannot be prepared in production because no runtime instance exists to pass into `PetDanceScheduledVisit.Prepare(options, runtime)`.
- **Required Minimal Seam**:
  - Author `PetDanceProductionRuntime` as specified in Section 2.2.

---

## 5. Qualified Repository Fixture Inventory

The table below catalogs every relevant fixture in the repository, explicitly separating **isolated visual matcher exemplars**, **static menu scene screenshots**, and **telemetry traces** from **unverified causal transitions**.

### 5.1 Category A: Bounded Visual Matcher Exemplars (Synthetic/Isolated Crops & OCR Reports)
These fixtures test small, isolated feature extraction algorithms on cropped pixels or serialized OCR dumps. They do not demonstrate full-screen classification or game state changes.

| Fixture Path | Format / Size | Manifest / Provenance | Consumer Test | Limits & Qualification |
| :--- | :--- | :--- | :--- | :--- |
| `TestData/PetDance/Entry/play-disabled.png` | PNG, $90 \times 25$ | [provenance.json](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/PetDance/Entry/provenance.json#L48-L68) | `PetDancePlayButtonVisionTests.cs` | Bounded button crop from recorded inspect frame. Tests gray disabled appearance only; does not qualify selected arena. |
| `TestData/PetDance/Entry/play-enabled.png` | PNG, $90 \times 25$ | [provenance.json](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/PetDance/Entry/provenance.json#L70-L89) | `PetDancePlayButtonVisionTests.cs` | Bounded button crop. Tests yellow enabled appearance only; does not generate screen click coordinates. |
| `TestData/PetDance/Review/play-enabled-hover.png` | PNG, $90 \times 25$ | [provenance.json](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/PetDance/Review/provenance.json) | `PetDancePlayButtonVisionTests.cs` | Supplemental hover appearance crop; shares identical geometry. |
| `TestData/PetDance/Selection/unselected.png` | PNG, $59 \times 55$ | [manifest.json](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/PetDance/Selection/manifest.json) | `PetDanceLevelSelectionVisionTests.cs` | Wizard City icon crop in unselected state. Background is synthetic in tests; validates local icon features only. |
| `TestData/PetDance/Selection/selected-away.png` | PNG, $59 \times 55$ | [manifest.json](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/PetDance/Selection/manifest.json) | `PetDanceLevelSelectionVisionTests.cs` | Wizard City icon crop after pointer leaves. Pinned to $1282 \times 760$ layout. |
| `TestData/PetDance/Selection/selected-hover.png` | PNG, $59 \times 55$ | [manifest.json](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/PetDance/Selection/manifest.json) | `PetDanceLevelSelectionVisionTests.cs` | Wizard City icon crop with active cursor hover. |
| `TestData/PetDance/Entry/entry-ocr.json` | JSON OCR Dump | [provenance.json](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/PetDance/Entry/provenance.json#L3-L6) | `PetDanceEntryVisionTests.cs` | Saved OCR tokens for `instructions-hovered`, `sigil-title-unread`, `loading-artwork`. **Crucial Limit**: `sigil-title-unread` provides conservative manual bounding box annotations, *not* an automatic title detector or verified readable title. |

### 5.2 Category B: Static Menu Scene Screenshots
These fixtures test classifier boundaries across static, full-frame captured images. They confirm whether a static image is correctly labeled, but do **not** verify transitions between states.

| Fixture Path | Format / Size | Manifest / Provenance | Consumer Test | Limits & Qualification |
| :--- | :--- | :--- | :--- | :--- |
| `TestData/Modules/W101Menus/outside.png` | PNG, $1282 \times 760$ | `TestData/Modules/W101Menus/provenance.json` | `recognition-cases.json` | Single static capture outside minigame. Contains sigil artwork but no causal interaction record. |
| `TestData/Modules/W101Menus/loading.png` | PNG, $1282 \times 760$ | `TestData/Modules/W101Menus/provenance.json` | `recognition-cases.json` | Single static capture of neutral loading artwork. Empty OCR. |
| `TestData/Modules/W101Menus/reward.png` | PNG, $1282 \times 760$ | `TestData/Modules/W101Menus/provenance.json` | `ModuleScreenReviewTests.cs` | Single static capture of postgame reward modal. |
| `TestData/Modules/W101Menus/snack.png` | PNG, $1282 \times 760$ | `TestData/Modules/W101Menus/provenance.json` | `ModuleScreenReviewTests.cs` | Single static capture of snack offer dialog. |
| `TestData/Modules/W101Menus/native-idle.png` | PNG, $1282 \times 760$ | `TestData/Modules/W101Menus/provenance.json` | `ModuleOperationTests.cs` | Single static capture showing verified native Idle options tab. |
| `TestData/Modules/W101Menus/graphics-menu.png` | PNG, $1282 \times 760$ | `TestData/Modules/W101Menus/provenance.json` | `recognition-cases.json` | Single static capture of options menu graphics tab. |

### 5.3 Category C: Structural UI Tree Fixtures
These fixtures contain serialized process memory node dumps.

| Fixture Path | Manifest / Source | Consumer Test | Limits & Qualification |
| :--- | :--- | :--- | :--- |
| `tools/fixtures/w101_ui_tree_review.json` | Offline review snapshot | `test_w101_ui_tree.py`, `W101DanceEntryContextTests.cs` | Unqualified structural candidates under `PetGameTracks`. Contains parent-local coordinates only; no screen pixels or approved energy. |
| `tools/fixtures/w101_ui_tree_live_20261110.json` | 2026-11-10 live snapshot | `test_w101_ui_tree.py` | Validates parser against frozen live snapshot; requires boolean opt-in. |

### 5.4 Category D: Observed Multi-Frame / Audio Telemetry Traces
These fixtures represent actual time-series telemetry from recorded sessions.

| Fixture Path | Manifest / Source | Consumer Test | Limits & Qualification |
| :--- | :--- | :--- | :--- |
| `TestData/RustParity/dance-observations-v1.tsv` | Pinned multi-frame log | `RustDanceParityTests.cs` | Pinned time-series of visual/audio observations during active rounds. |
| `TestData/PetDance/huge-watch-onset-gap.json` | Synthetic/recorded timing | `PetDanceSoundOnsetRecoveryTests.cs` | Tests audio onset detection across large inter-round gaps. |
| `TestData/Routines/dance/events.jsonl` | Session event log | Routine replay tests | Recorded event sequence from historical replay session. |

### 5.5 Explicitly Unobserved & Unverified Causal Transition Edges
The repository contains **no verified end-to-end recording** for the following causal transitions; they remain integration hypotheses until qualified:
1. `OutsideAfterGame` $\to$ `Loading` via `InteractX`: No recorded time-series captures the `'X'` keystroke causing the loading screen to appear.
2. `Loading` $\to$ `InstructionsUnselected`: No time-series captures loading screen dismissal into the instructions panel.
3. `InstructionsUnselected` $\to$ `InstructionsReady` via `ChooseLevel`: Selecting the icon and witnessing the appearance change is tested with isolated crops, not an interactive recording.
4. `InstructionsReady` $\to$ `Watch` via `Play`: Priming capture, sending mouse down/up on Play, and observing round 1 onset is unverified in live integration.

---

## 6. Proposed Offline Integration Test

Below is the concrete proposal for `PetDanceProductionRuntimeTests.cs`, validating the wired entry runtime using saved synthetic fixtures and offline fake observers without launching games, controllers, or live hooks.

> [!NOTE]
> This test fixture is a design proposal (`[PROPOSED ONLY - NOT IMPLEMENTED]`) and has not been executed or added to the build suite, honoring coordinator build-slot boundaries.

```csharp
// [PROPOSED ONLY - NOT IMPLEMENTED]
// File: ProjectMaelstrom/ProjectMaelstrom.Tests/PetDanceProductionRuntimeTests.cs

using System.Drawing;
using ProjectMaelstrom.Utilities;
using Xunit;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceProductionRuntimeTests
{
    private static readonly Size QualifiedSize = new(1282, 760);

    [Fact]
    public async Task ProductionRuntime_TraversesEntryPipelineToSpecialistHandoff_Offline()
    {
        // 1. Arrange: Load pinned reference crops from TestData
        var baseDir = Path.Combine(AppContext.BaseDirectory, "TestData", "PetDance");
        using var unselectedBmp = new Bitmap(Path.Combine(baseDir, "Selection", "unselected.png"));
        using var selectedAwayBmp = new Bitmap(Path.Combine(baseDir, "Selection", "selected-away.png"));
        using var playDisabledBmp = new Bitmap(Path.Combine(baseDir, "Entry", "play-disabled.png"));
        using var playEnabledBmp = new Bitmap(Path.Combine(baseDir, "Entry", "play-enabled.png"));

        var levelVision = new PetDanceLevelSelectionVision(unselectedBmp, selectedAwayBmp);
        var playVision = new PetDancePlayButtonVision(playDisabledBmp, playEnabledBmp);

        var options = new PetDanceVisitOptions(
            TargetId: "w101-test-target",
            Geometry: QualifiedSize,
            ContextId: "test-visit-context",
            Arena: PetDanceArena.WizardCity,
            Enabled: true,
            TimeoutMs: 60000);

        var dispatchesObserved = new List<PetDanceLifecycleAction>();
        var specialistInvoked = false;

        // 2. Instantiate Mocked Production Runtime Container
        var runtimeMock = new TestablePetDanceRuntime(options, levelVision, playVision,
            onDispatch: action => dispatchesObserved.Add(action),
            onSpecialist: () => specialistInvoked = true);

        var job = PetDanceScheduledVisit.Prepare(options, runtimeMock);
        Assert.NotNull(job);
        Assert.True(job.Request.WritesGame);

        // 3. Act: Execute Visit Runner
        var outcome = await job.Execute(new OperationContext(
            new OperationRequest("req-1", "w101.pet-dance-visit", "v1", 60, true, options.TargetId, runtimeMock.Pins),
            Path.GetTempPath(),
            null,
            null), CancellationToken.None);

        // 4. Assert: Verify Dispatches and Specialist Invocation
        Assert.Contains(PetDanceLifecycleAction.InteractX, dispatchesObserved);
        Assert.Contains(PetDanceLifecycleAction.ChooseLevel, dispatchesObserved);
        Assert.Contains(PetDanceLifecycleAction.Play, dispatchesObserved);
        Assert.True(specialistInvoked, "Specialist handoff must be triggered once Watch scene is reached.");
    }

    [Fact]
    public void ProductionRuntime_WithholdsPlay_WhenEnergyCostUnapproved()
    {
        // Validates that if EntryCostApproved is false, Play proposal is strictly withheld with reason
        // "entry-cost-not-approved", preventing unapproved energy consumption.
        var options = new PetDanceVisitOptions("target", QualifiedSize, "ctx", PetDanceArena.WizardCity, true);
        var visit = new PetDanceVisit(options);

        var observation = new PetDanceVisitObservation(
            TimeMs: 1000,
            ImageSha256: new string('a', 64),
            Scene: PetDanceScene.InstructionsReady,
            TargetId: "target",
            Geometry: QualifiedSize,
            ContextId: "ctx",
            Entry: new PetDanceEntryScene(
                Scene: PetDanceScene.InstructionsReady,
                ImageSha256: new string('a', 64),
                CapturedMs: 1000,
                Geometry: QualifiedSize,
                Reason: "ready",
                PlayBounds: new RectangleF(750, 560, 90, 30),
                SelectedArena: PetDanceArena.WizardCity,
                LevelControls: [new PetDanceLevelControl(PetDanceArena.WizardCity, new RectangleF(452, 485, 59, 55), true)],
                SelectionVerified: true,
                PlayEnabledVerified: true),
            ControlBounds: new RectangleF(750, 560, 90, 30),
            Fresh: true,
            TargetReady: true,
            ContextVerified: true,
            InputOwned: true,
            EntryCostApproved: false); // Disapproved!

        var plan = visit.Observe(observation);

        Assert.False(plan.IsProposal, "Play proposal must be withheld when energy is not approved.");
        Assert.Equal("entry-cost-not-approved", plan.Reason);
    }
}
```

---

## 7. Coordination & Verification Record

- **Reviewed Directives**: `LIA-0049` (activation), `LIA-0050` (precision review corrections), `GEM-0053` (initial handback).
- **Git HEAD Verified**: `7888cf251388dd904bac3977c70f73d0a6a31424`.
- **Zero Modification Invariant**: No `.cs`, `.py`, or `.json` production or test files modified; zero tests or builds executed; zero game interaction performed.
- **Deliverable**: Revised wiring plan delivered to [`docs/coordination/results/GEM-DANCE-ENTRY-WIRING-97.md`](file:///F:/Modlists/AutoWizard101/docs/coordination/results/GEM-DANCE-ENTRY-WIRING-97.md).
