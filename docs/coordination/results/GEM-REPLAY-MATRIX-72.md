# Offline Dance Visit Contract Tests Report (GEM-REPLAY-MATRIX-72)

## 1. Overview & Verification Status
- **Source Output**: `ProjectMaelstrom/ProjectMaelstrom.Tests/GeminiDanceVisitContractTests.cs`
- **Execution Status**: **UNRUN** (Authoring strictly scoped to test source. Root coordinator owns test execution in the shared build slot).
- **Revision / Branch**: `595ef2c7b8423995de21b055e3712eef195507c8` (HEAD of `maelstrom/rust-architecture-v1`).
- **Fixtures & Isolation**: All 8 test cases use deterministic synthetic fixtures (`PetDanceVisitObservation`, `PetDanceEntryScene`, `PetDanceVisitOptions`). No live game, Win32 window handles, GDI capture, OCR binaries, or network connections are accessed.

---

## 2. Test Cases & Architectural Rationale

| Test Method | Category | Verified Contract & Invariants |
| :--- | :--- | :--- |
| `LoadingPauseSuppressesProposalsAndResumesSeamlessly` | Loading Lifecycle | Verifies that `PetDanceScene.Loading` suppresses action proposals and avoids false step advancement, while keeping `Stopped == false` so the routine resumes into `TeacherHandoff` upon entering `Watch`. |
| `PhysicalKeyTakeoverStopsVisitImmediatelyWhereasTargetUnreadyPauses` | Takeover vs Focus | Distinguishes temporary focus loss (`TargetReady == false`, which pauses without stopping) from physical keyboard input (`UserActive == true`, which halts the visit permanently with `Stopped == true`). |
| `SpecialistFailurePreventsResultAdvancementAndBlocksExit` | Specialist Handoff | Confirms that if `runtime.PlayAsync` returns `Completed: false`, `PetDanceVisit` rejects subsequent `GameResult` observations with `"completed-specialist-and-whole-game-result-required"`, preventing premature reward extraction. |
| `UnknownSceneDoesNotCorruptVisitStateOrDispatchBlindRetries` | Unknown Scene Resilience | Tests that intermittent OCR misses or unclassified frames (`PetDanceScene.Unknown`) do not increment proposal counters or corrupt graph position, allowing clean resumption when valid scenes reappear. |
| `SnackScreenEnforcesNoFeedAndStrictlyProposesFinish` | Snack / Crowns Exclusion | Enforces the strict no-feed policy: `SnackOffer` scene only proposes `PetDanceLifecycleAction.FinishWithoutSnack`, ensuring no snacks or Crowns can ever be spent. |
| `InitialNativeIdleCannotCompleteVisitAndRequiresFullLifecycle` | Golden Rule Compliance | Verifies that an initial idle observation at startup yields `Completed == false`. Completion requires full traversal through sigil, play, specialist, next, finish, and menu navigation before `NativeIdle` marks the job done. |
| `CancellationDuringVisitStopsRoutineAndRequiresInputRelease` | Cancellation Boundary | Verifies that an observation marked `Cancelled == true` sets `Stopped == true` and immediately stops proposals, allowing the scheduler's `finally` block to execute cleanup. |
| `EnergyGatingWithholdsPlayProposalWhenCostNotApproved` | Resource Protection | Confirms that `InstructionsReady` with `EntryCostApproved == false` withholds the `Play` proposal with `"entry-cost-not-approved"`, preventing unbudgeted entry attempts. |

---

## 3. Recommended Filtered Execution Command
When the root coordinator allocates the build slot, run the following isolated test command:

```powershell
$env:DOTNET_PROCESSOR_COUNT='2'
dotnet test ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -warnaserror --filter "FullyQualifiedName~GeminiDanceVisitContractTests"
```

*Note: Execution was not performed by Gemini.*

---

## 4. Remaining Gaps & Non-Covered Behaviors
1. **Live OCR Raster Variance**: Synthetic bounding boxes cannot test font rendering edge cases (e.g. "YOIR" vs "YOUR" in header text handled in `PetDancePostGameClassifier.cs`).
2. **Audio-Visual Agreement Gate**: The visual-audio fallback synchronizer (`PetDanceAudioVisualAgreement.cs`) operates in the standalone player and has separate integration tests; it is not yet bound to `IPetDanceVisitRuntime`.
3. **Multi-Arena Scaling**: Only `WizardCity` was verified in synthetic geometry; other arenas (Krokotopia through Dragonspyre) require independent button bounding box fixtures.
