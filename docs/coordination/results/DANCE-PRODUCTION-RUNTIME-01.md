# DANCE-PRODUCTION-RUNTIME-01 — Production Pet Dance Visit Runtime (OFFLINE IMPLEMENTATION — NO LIVE QUALIFICATION)

Status: **COMPLETE (OFFLINE IMPLEMENTATION — NO LIVE QUALIFICATION)**  
Date: 2026-11-16  
Claim: `DANCE-PRODUCTION-RUNTIME-01`  
Workstream: `W101-DANCE`  
Owner: `gemini`  
Directives: `LIA-0124` / `docs/coordination/tasks/DANCE-PRODUCTION-RUNTIME-01.md`  

---

## 1. Executive Summary

This deliverable provides the production implementor of `IPetDanceVisitRuntime` and `IPrimedPetDanceVisitRuntime` (`PetDanceProductionRuntime`), extensions to `PetDanceMenuInput` for entry action allowlisting and hardware scan mapping (`InteractX` `0x2D`, `ChooseLevel`, `Play`), and registration wiring in `PetDanceScheduledVisit` (`PrepareProduction`).

**OFFLINE IMPLEMENTATION — NO LIVE QUALIFICATION.** All verification was conducted strictly offline using bounded compiler and test runs (`dotnet test`, `dotnet format whitespace`, `dotnet format analyzers`, `check_text_encoding.py`, and `check_project_board.py`). Zero live game attachment, zero desktop input, zero client hooks, zero packaging/installation, and zero dependencies were introduced.

---

## 2. Status of the Five Architectural Bindings (Re-evaluated at HEAD)

| # | Binding | Prior Status | Status Post-Slice | Implementation & Verification Evidence |
|---|---|---|---|---|
| 1 | `PetDanceMenuInput` allowlist & key mapping | **OPEN — Blocker** | **IMPLEMENTED & TESTED (Offline)** | `Utilities/PetDanceMenuInput.cs:30-34,50-52,116`. Allowlist admits `InteractX`, `ChooseLevel`, `Play`. `InteractX` mapped to hardware scan `0x2D` (`Flags = 8u \| (release ? 2u : 0u)`). Verified in `PetDanceMenuInputTests` (15/15 passed) and `PetDanceProductionRuntimeTests` (12/12 passed). |
| 2 | Control geometry & arena guards | **OPEN — Specification** | **IMPLEMENTED & TESTED (Offline)** | `Utilities/PetDanceProductionRuntime.cs`. Fail-closed rejection of any window client size or options geometry other than `1282x760` (`PetDanceLevelSelectionVision.QualifiedGeometry`) with explicit reason `"geometry-unqualified"`, and any arena other than `PetDanceArena.WizardCity` with explicit reason `"arena-unqualified"`. Never clicks parent-local UI-tree rectangles (uses verified level selection and PLAY OCR word bounds only). |
| 3 | Primed capture / Play handoff seam | **IMPLEMENTED (Seam)** | **INTEGRATED & TESTED (Offline)** | Uses existing `PetDanceScheduledVisit.RunPrimedAsync` seam (`PetDanceScheduledVisit.cs:147`). `PlayPrimedAsync` delegates to `RunSpecialistAsync`, primes capture via `startAfterCaptureReady`, synchronously binds single `FirstWatchObserved` callback (`watchObservation` + `playDispatch`) via `bindHandoff`, and enforces single-invocation and single capture session invariants. |
| 4 | Energy approval policy | **OPEN — Policy Gate** | **PRESERVED AS OPEN POLICY GATE** | `Utilities/PetDanceProductionRuntime.cs`. Unconditionally keeps `EntryCostApproved = false` on every observation. Surfaces explicit reason `EntryCostWithheldReason = "entry-cost-policy-unwired"` in metadata pins (`pins["entry_cost_policy"] = "unwired"`). When evaluated by `PetDanceVisit.Observe`, ready instructions withhold the `Play` proposal with `"entry-cost-not-approved"`. Spend approval is never invented. |
| 5 | Production runtime class | **OPEN — Infrastructure** | **IMPLEMENTED & TESTED (Offline)** | `Utilities/PetDanceProductionRuntime.cs` authored, implementing `IPetDanceVisitRuntime`, `IPrimedPetDanceVisitRuntime`, and `IDisposable`. Convenience seam `PetDanceScheduledVisit.PrepareProduction` wired. 12 focused unit tests pass in `PetDanceProductionRuntimeTests.cs`. |

---

## 3. Code Modifications & Owned Paths

Modifications were restricted strictly to the owned paths declared in `claims.json` under claim `DANCE-PRODUCTION-RUNTIME-01`:

1. **`ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceProductionRuntime.cs` (New)**:
   - Implements `IPetDanceVisitRuntime`, `IPrimedPetDanceVisitRuntime`, and `IDisposable`.
   - Invariant guards:
     - Rejects non-`1282x760` geometry in `CountdownAsync`, `ObserveAsync`, `DispatchAsync`, and `PlayPrimedAsync` with `"geometry-unqualified"`.
     - Rejects non-`WizardCity` arena with `"arena-unqualified"`.
     - Rejects unowned input via `PetDanceSpecialistAdmission.OwnsInput(context, _target)`.
     - Rejects user takeover / active keys via `DesktopActivityMonitor.AnyKeyHeld()` and `EscapeRequested`.
   - Single-capture invariant:
     - Explicitly disposes any observation session (`_internalObserver`) before invoking `RunSpecialistAsync`, ensuring no overlapping capture sessions exist.
   - Entry & Postgame Recognition:
     - Leverages `PetDanceMenuOcr`, `PetDancePostGameClassifier`, and `PetDanceEntryVision`.
     - Always keeps `EntryCostApproved = false`.
   - Primed Play:
     - Dispatches `Play` via `StartAfterCaptureReady` using `_menuInput.DispatchAsync`.
     - Synchronously binds the first qualified Watch onset (`FirstWatchObserved`) to `bindHandoff`, preventing duplicate invocations or premature completion.

2. **`ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceMenuInput.cs` (Modified)**:
   - Lines 30–33: Extended action allowlist to include `PetDanceLifecycleAction.InteractX`, `PetDanceLifecycleAction.ChooseLevel`, and `PetDanceLifecycleAction.Play`.
   - Line 34: Added keyboard classification for `InteractX` (`plan.Action is PetDanceLifecycleAction.OpenMenu or PetDanceLifecycleAction.InteractX`).
   - Lines 50–52: Mapped hardware scan code: `0x2D` for `InteractX` (virtual key `'X'`), `0x01` for `OpenMenu` (Escape).
   - Line 116: Updated `Key(ushort scan, bool release)` to accept the variable scan code with standard Win32 scan-code flags (`8u | (release ? 2u : 0u)`).

3. **`ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceScheduledVisit.cs` (Modified)**:
   - Added registration helper:
     ```csharp
     internal static PreparedDeckJob PrepareProduction(PetDanceVisitOptions options, GameWindow target, string profile, string output) =>
         Prepare(options, new PetDanceProductionRuntime(target, options, profile, output));
     ```

4. **`ProjectMaelstrom/ProjectMaelstrom/Utilities/JobDeckRoutines.cs` (Unchanged)**:
   - Lines 59–68 already accept `Func<JobCardSpec, JobFacts?, PreparedDeckJob>? dance = null`. The `w101.pet.dance` routine card delegates directly to the supplied adapter. No modifications were necessary.

5. **`ProjectMaelstrom/ProjectMaelstrom.Tests/PetDanceProductionRuntimeTests.cs` (New)**:
   - 12 focused unit tests verifying:
     - Contract conformance and pins (`UsesGameInput`, pins metadata, interface implementations).
     - Fail-closed geometry rejection on options, window client size, and frame size (`"geometry-unqualified"`).
     - Fail-closed arena rejection (`"arena-unqualified"`).
     - Energy spend withholding (`EntryCostApproved == false`, `"entry-cost-policy-unwired"`).
     - Dispatch allowlist admission for `InteractX` (scan `0x2D`), `ChooseLevel`, and `Play`.
     - Primed play single-invocation callback protocol and teacher handoff binding.
     - Rejection of duplicate Watch callback attempts (`InvalidOperationException`).
     - Rejection of specialist completion without Watch onset (`InvalidOperationException`).
     - Clean input release and verified native Idle confirmation in `FinishAsync`.
     - SHA-256 frame hashing consistency.
     - Production deck job preparation via `PrepareProduction`.

6. **`ProjectMaelstrom/ProjectMaelstrom.Tests/PetDanceScheduledVisitTests.cs` (Modified)**:
   - Added `PrepareProductionBindsSeamAndPreservesInvariants` regression test confirming `PrepareProduction` preserves request identity, pins, and game write authorization.

7. **`docs/coordination/gemini/FROM_GEMINI.md` (Modified)**:
   - Logged `GEM-0110` (ACK of `LIA-0124`) and `GEM-0111` (Handback).

---

## 4. Primary Verification Results

Every check was executed via primary tools under serialized bounded flags:

1. **Native Compiler Build (`-warnaserror`)**:
   - Command: `$env:DOTNET_PROCESSOR_COUNT='2'; dotnet build ProjectMaelstrom/ProjectMaelstrom/ProjectMaelstrom.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -warnaserror`
   - Result: `Build succeeded. 0 Warning(s), 0 Error(s).`
2. **Focused Native Unit Tests**:
   - Command: `$env:DOTNET_PROCESSOR_COUNT='2'; dotnet test ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -warnaserror --filter "FullyQualifiedName~PetDanceProductionRuntime|FullyQualifiedName~PetDanceScheduledVisit|FullyQualifiedName~PetDanceMenuInput"`
   - Result: `Passed! - Failed: 0, Passed: 56, Skipped: 0, Total: 56, Duration: 599 ms`
     - `PetDanceProductionRuntimeTests`: 12 passed
     - `PetDanceScheduledVisitTests`: 29 passed
     - `PetDanceMenuInputTests`: 15 passed
3. **Scoped Formatting & Analyzer Verification**:
   - Whitespace verification: `dotnet format whitespace ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --no-restore --verify-no-changes --include <owned paths>` -> Exit code `0` (clean).
   - Analyzer verification: `dotnet format analyzers ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --no-restore --verify-no-changes --include <owned paths>` -> Exit code `0` (clean).
4. **Repository Text Encoding**:
   - Command: `python tools/check_text_encoding.py`
   - Result: `0 violation(s), 2 excluded file(s)` -> Exit code `0`.
5. **Project Board Integrity**:
   - Command: `python tools/check_project_board.py`
   - Result: `Project board valid: 30 workstreams; evidence paths and dependency graph checked.` -> Exit code `0`.

---

## 5. Remaining Work & Boundaries

1. **Energy Policy Gate (Binding 4)**: Still requires a verified policy layer before spend can be authorized. The runtime strictly withholds approval.
2. **Multi-Resolution / Multi-Arena Generalization (Binding 2)**: Stays pinned to `1282x760` and `PetDanceArena.WizardCity`. Other resolutions and arenas fail closed.
3. **Live Visit Qualification**: Not performed in this offline slice. Live execution requires the coordinator's serialized control window and owner authorization per `AGENTS.md`.
