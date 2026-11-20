# Shared routine graphs and offline rehearsal

The native application now has one data-driven transition runner used by the Pet Dance postgame adapter and by offline replay. This is the reusable layer between game-specific recognition and existing trusted input/specialist adapters. It does not need an LLM, GPU model, or an assistant in the execution loop.

## Contract

`Utilities/RoutineGraph.cs` defines a separately versioned routine JSON contract. Game-pack schema 2 remains unchanged: its keyboard action registry is still valid. A routine sidecar references action IDs; it is explicitly loaded, not hidden in an ignored pack field. Existing Workshop pack editing does **not yet** author or execute these graphs.

Each node has scene-triggered edges. An edge names a registered action, its expected observed outcome(s), the next node, and a deadline. A terminal scene records completion. Different observations can choose different edges, and cycles can repeatedly choose movement actions; the graph is not a recording of one fixed playthrough.

An edge may opt into `requires_outcome_binding`. Its newer outcome observation
must name the exact pending `outcome_for_proposal_id` before it can advance. This
prevents a held `Progress` state with newer frame times from acknowledging several
successive movement pulses. An observation adapter must assign that ID only after
measuring the action's new effect; copying the current proposal ID onto every
frame would defeat the evidence contract. The runner validates binding, not motion
pixels. Older edges retain their existing behavior unless explicitly opted in.

## Reuse boundary

There is one shared graph runner and offline replay contract. Game adapters supply
their scene vocabulary, recognized controls, rules and qualified outcomes. The
existing capture/recording services, input lease, action registry and scheduler
remain shared; a game-specific detector does not acquire its own controller.

[Reusable operation modules](REUSABLE_MODULES.md) now compile navigation, sigils,
menus and Auto-Idle through this graph engine. A common `ModuleRunner` composes
them with named facts/controls, per-module receipts and cumulative bundle budgets.
[W101's adapter](W101_SHARED_OPERATIONS.md) supplies configuration and existing
key/control bindings; it no longer builds a custom graph. `--replay-modules`
replays saved bundles through the same game-session format. Integrating these
definitions into user-editable clips and the live scheduler remains separate.
The older specialized controllers have not all been migrated.

The sequence is:

1. Observe a fresh, verified scene for the bound target, geometry and visit.
2. Propose an action with a unique per-run proposal ID.
3. Accept a matching dispatch receipt only if input was released.
4. Require a newer frame showing the declared outcome before advancing.

Loading/unknown scenes wait without inputs, while finite deadlines continue. Lost ownership, changed target/context, user activity or cancellation stop the attempt. A rejected, reused or mismatched receipt cannot authorize a retry. Each routine has an overall time/action budget; each transition has its own deadline. No shell commands, executable delegates or arbitrary scripts are accepted in graph JSON.

The runner produces proposals only. A live adapter must independently resolve the ID and recheck the shared lease, current target, freshness, permitted controls and cancellation immediately before dispatch. A specialist handoff is an explicit action: a proposal is not evidence that the specialist ran or succeeded.

## First adapters and examples

The newer `PetDanceVisit` adapter consumes `Resources/Routines/pet-dance-visit.json`,
including initial sigil/instructions branches and separate selected-arena, entry-cost,
Watch-start and completed-specialist gates. It is available through offline
`--replay-pet-dance-visit <visit.json> <new-report.json>`; it does not enable a live
dispatcher. See [entry transitions](PET_DANCE_ENTRY_TRANSITIONS.md) and
[the integrated checkpoint](PARALLEL_RECOGNITION_VALIDATION_20261109.md). The older
draft below remains a simpler contract fixture, not the stronger full-visit adapter.

`Resources/Routines/pet-dance-postgame.json` is embedded and actually consumed by `PetDancePostGame`: verified reward → Next → snack screen → Finish without feeding → observed outside → menu → Idle → observed Idle banner. Existing local OCR and input ownership remain in the live adapter. The runtime supplies the actual dispatch release time, preventing an older queued image from establishing a post-click outcome.

`pet-dance-visit-draft.json` rehearses the broader sigil/instructions/level/Play/teacher/result/Idle sequence. It is a **draft offline contract**, not a live auto-start implementation. Initial Idle exit, approach/navigation, level selection and Play still require qualified detectors/adapters. The old lifecycle proposal planner remains available; it is not proof of completed actions.

`dodge-movement-draft.json` exercises repeated left/right decisions and movement-outcome acknowledgement through the same core. It uses movement IDs compatible with the existing action registry. It is not yet wired into the Dodge live dispatcher and makes no survival-score claim.

Navigation can use the same structure: observe a landmark, propose a bounded motion primitive, then observe its expected landmark/outcome before continuing. Unknown geography must not become a blind long timed macro. Existing `W101Routine` primitives remain the action bindings; no second movement/input service was introduced.

## Repeatable offline command

The native executable accepts:

```text
ProjectMaelstrom.exe --replay-routine <routine.json> <session.json> <new-report.json>
```

Example inputs, relative to repository root:

- `ProjectMaelstrom/ProjectMaelstrom/Resources/Routines/pet-dance-visit-draft.json`
- `ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/Routines/dance/session.json`
- A new output path under `.local/` (existing reports are never overwritten).

The second fixture is `TestData/Routines/dodge/session.json` with the Dodge draft graph. Both are explicitly synthetic scene fixtures, not recognition results or live demonstrations. Replay uses supplied timestamps without sleeping and has no desktop dispatcher. Exit codes: 0 completed contract, 2 incomplete/stopped contract with report, 1 invalid input/error.

The manifest and JSONL events use the existing `tools/game_session.py` v1 envelope (`event_id`, `sequence`, `time_ms`, `kind`, `payload`). Add `routine_binding` to the manifest. Observation/outcome payloads contain `routine_observation`; receipts contain `routine_receipt`. Ordinary markers/proposals and unrelated payloads are counted but cannot advance the runner. The envelope timestamp is authoritative. Replay never manufactures an acknowledgement for a proposed action.

For deterministic replay, proposal IDs are `<session_id>/<routine_id>/<ordinal>`. Live runs use a fresh nonce instead, preventing a receipt from another run from matching. Reports include input hashes, a decision trace, proposal/receipt counts, explicit zero desktop dispatch, and `live_qualified: false`. Missing receipts and incomplete traces remain incomplete, not successes.

## Current qualification limits

Offline regression covers variable loading delays, unknown/repeated/stale frames, changed context, user takeover, rejected/missing/cross-run receipts, fresh post-dispatch evidence, repeated action IDs, action/deadline budgets and malformed graph data. Full-visit replay qualifies the coordination contract only. It does not train visual recognition or establish live navigation, resource spending, native-resolution gameplay, or autonomous full-visit reliability.

The last installed Idle test received stale buffered frames and made no menu actions. The new source cadence fix drains/reacquires frames in bounded 25ms retries while retaining at-most-2Hz OCR and the 300ms freshness gate. Offline buffer tests pass; a fresh live qualification remains separate. Capture and cleanup timeouts now also bound final confirmation. See the dated live report and workstream board for evidence rather than treating this design document as a completion receipt.
