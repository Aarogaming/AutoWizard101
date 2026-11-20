# Proposed brief: DANCE-PRODUCTION-RUNTIME-01 — production Pet Dance visit runtime (offline slice)

Status: **REGISTERED — active claim `DANCE-PRODUCTION-RUNTIME-01`, owner `gemini`, 2026-11-16 (owner-approved).** Source/build authority on the exact owned paths below; no live, desktop, install or dependency authority. Activation notice: LIA-0124 in `docs/coordination/gemini/TO_GEMINI.md`.
Drafted: 2026-11-16 by root, from `GEM-DANCE-ENTRY-WIRING-97` re-verified against HEAD.
Workstream: `W101-DANCE`. Suggested owner: gemini (Antigravity) under a new coordinator-registered claim, or root.

## 1. Objective

Author the missing production implementor of `IPetDanceVisitRuntime` / `IPrimedPetDanceVisitRuntime` so the existing receipt-aware visit machine (`PetDanceScheduledVisit`) can be prepared for the `w101.pet.dance` deck routine, **offline first**: implementation plus focused unit tests, no live game interaction in this slice.

Non-goals for this slice: live visit qualification, multi-resolution/multi-arena generalization, energy-spend approval policy, snack feeding, Crowns, client restart, navigation.

## 2. Current state of the five bindings (re-verified at HEAD, 2026-11-16)

| # | Binding | Status at HEAD | Evidence |
| --- | --- | --- | --- |
| 1 | `PetDanceMenuInput` action/key allowlist | **OPEN — Blocker.** Allowlist admits Next, FinishWithoutSnack, OpenMenu, SelectIdle, QuitToCharacterSelection, SelectWizardCandidate, EnterSelectedWizard, ExitWorldGate, TravelWorldGate, SelectWorldGate. `InteractX`, `ChooseLevel`, `Play` are absent; no `0x2D`/X scan mapping. | `Utilities/PetDanceMenuInput.cs:30-31`; X mapping exists only in `Utilities/W101ModuleInput.cs:61` |
| 2 | Control geometry / multi-resolution | **OPEN — Specification.** Still pinned to one resolution and one arena; parent-local UI-tree rectangles remain unqualified. | `Utilities/PetDanceLevelSelectionVision.cs:22-23` (`1282x760`, `452,485,59,55`); `Utilities/W101DanceEntryContext.cs:6-7` |
| 3 | Primed capture / Play handoff seam | **SEAM IMPLEMENTED — no longer an open architectural gap.** `PetDanceScheduledVisit.RunPrimedAsync` exists and routes `Play` through `IPrimedPetDanceVisitRuntime.PlayPrimedAsync`; `RunSpecialistAsync` accepts `startAfterCaptureReady` and uses `PetDancePrimedCapture.StartAsync`. What is missing is a *production implementor* that uses the seam correctly (folds into binding 5). | `Utilities/PetDanceScheduledVisit.cs:25-30,105-107,147`; `Utilities/PetDanceLiveTrial.cs:314,401-404` |
| 4 | Energy approval policy | **OPEN — Policy Gate.** `EntryCostApproved` defaults `false`; `txtEnergyCost`/`txtYourEnergy` are parsed as unqualified numerical candidates only. No policy layer authorizes spend. | `Utilities/PetDanceVisit.cs:8,90`; `tools/w101_ui_tree.py:91-92` |
| 5 | Production runtime class | **OPEN — Infrastructure.** The only implementor of `IPetDanceVisitRuntime` is the private test mock. `IPrimedPetDanceVisitRuntime` is an interface, not an implementor. | `ProjectMaelstrom.Tests/PetDanceScheduledVisitTests.cs:27`; `Utilities/PetDanceScheduledVisit.cs:9-30` |

Correction to the source report: `GEM-DANCE-ENTRY-WIRING-97` presents binding 3 as an open architectural gap. At HEAD the seam exists; the remaining work is the production implementor. The brief therefore scopes **four** open items (1, 2, 4, 5), with 3 satisfied by using the existing seam correctly.

## 3. Proposed scope and sequencing

**Slice A (this brief, offline only):**
1. `PetDanceProductionRuntime` implementing `IPetDanceVisitRuntime` and `IPrimedPetDanceVisitRuntime`, delegating gameplay to the existing `PetDanceLiveTrial.RunSpecialistAsync`. It must not create a second capture session, a second specialist, a nested message loop, or an independent input lease.
2. Extend the entry dispatch path for `InteractX` / `ChooseLevel` / `Play` — either by extending `PetDanceMenuInput` (adding the X scan mapping) or by a dedicated `PetDanceEntryInput` dispatcher honoring the same release-guarantee, takeover and ownership invariants. Choose one and document why.
3. Fail-closed geometry/arena guard: reject any window geometry other than the qualified `1282x760` and any arena other than the qualified one, with an explicit reason string. Never click parent-local UI-tree rectangles.
4. Keep `EntryCostApproved = false` unless a verified policy exists; the runtime must surface the withheld reason rather than inventing approval.
5. Focused unit tests: runtime contract conformance, primed-handoff single-invocation, geometry/arena rejection, energy withholding, cancellation/takeover, and cleanup-uncertainty latching. Reuse the existing test seams; do not weaken existing assertions.

**Slice B (separate, later, owner-authorized):** live qualification of one bounded visit — countdown, verified target, energy check, no-snack exit, verified native Idle — then repeat visits. Not part of this brief.

## 4. Proposed owned paths (exact; to be registered only on approval)

- `ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceProductionRuntime.cs` (new)
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceMenuInput.cs` (allowlist/key mapping) **or** `Utilities/PetDanceEntryInput.cs` (new) — pick one
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceScheduledVisit.cs` (registration seam only, if required)
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/JobDeckRoutines.cs` (routine preparation wiring only, if required)
- `ProjectMaelstrom/ProjectMaelstrom.Tests/PetDanceProductionRuntimeTests.cs` (new)
- `ProjectMaelstrom/ProjectMaelstrom.Tests/PetDanceScheduledVisitTests.cs` (additions only)
- `docs/coordination/results/DANCE-PRODUCTION-RUNTIME-01.md` (result report)

Overlap check performed 2026-11-16 against active claims: **`Utilities/PetDanceMenuInput.cs` is owned by active claim `W101-CHARACTER-27` (root).** That path must be transferred to this claim (documented transfer, as done for `TEXT-ENCODING-REPAIR-01`) or the entry-dispatch work must use a new `PetDanceEntryInput.cs` instead, leaving `PetDanceMenuInput.cs` untouched. `PetDanceScheduledVisit.cs`, `JobDeckRoutines.cs` and `PetDanceScheduledVisitTests.cs` are currently unowned by any active claim, but re-check immediately before registration because the board changes between heartbeats.

## 5. Authority requested

- **Source edit authority** for the paths above.
- **Build/test authority** in the coordinator's serialized build slot: `dotnet build`/`dotnet test` with `DOTNET_PROCESSOR_COUNT=2 -m:1 -p:UseSharedCompilation=false -warnaserror`, scoped `dotnet format whitespace` and `analyzers --verify-no-changes`.
- **Explicitly NOT granted:** live game interaction, desktop control, client hooks beyond the already-reviewed opt-in readers, installation, packaging, new dependencies, commits outside the owned paths.

## 6. Acceptance gates

1. Focused tests pass with warnings-as-errors; the full native suite is run by the coordinator afterwards (last executed: 3,048 passed / 0 failed on 2026-11-16).
2. Scoped whitespace and analyzer verification pass.
3. `python tools/check_project_board.py` valid; `python tools/check_text_encoding.py` 0 violations.
4. The result report states, in its title and summary, that this is an **offline implementation with no live qualification**, and lists every binding still open.
5. No existing assertion is weakened; no second input owner or capture session is introduced; `EntryCostApproved` remains false without a verified policy.

## 7. Boundaries (unchanged, from AGENTS.md)

UI-only default; the reviewed W101 client-side exception remains opt-in and disabled by default; network manipulation excluded; installed game files read-only; no feeding, no Crowns, no volume changes, no client restart; one shared input owner with cancellation and user takeover; a successful dispatch is not a successful action, and a completed specialist is not a verified whole-game result.

## 8. Risks

- The runtime could silently start a second capture or specialist; the primed seam's single-invocation contract must be tested explicitly.
- Extending the input allowlist widens what can be dispatched; every added action needs a release-guarantee and takeover test, not just a happy path.
- Geometry generalization is tempting but out of scope; the correct behavior is explicit rejection, not a guess.
- `JobDeckRoutines.cs` / `PetDanceScheduledVisit.cs` overlap with active claims must be resolved before any edit.
