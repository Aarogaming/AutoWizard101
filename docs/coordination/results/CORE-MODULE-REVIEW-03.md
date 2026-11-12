# Shared module card integration review

2026-11-11 · CORE-MODULE-REVIEW-03 · source review only.
No builds, tests, desktop, game, dependencies or installation were run by this
reviewer. Coordinator owns combined verification.

## Correct reuse seam

Use `ModuleReplay` for game-independent labelled module rehearsal. It already
executes `ModuleRunner` over the game-session v1 envelope and reports source
hashes, completion counts, zero desktop dispatch and `live_qualified=false`.
The runner composes the four existing compilers and requires receipts followed
by newer outcome evidence bound to the exact proposal.

`ModuleScreenReview` is a different, narrower pipeline: saved PNGs, W101 menu
OCR, W101 policy context and explicit synthetic rehearsal assumptions. Its
snapshot is well bounded and pins all image dependencies, but it is not a
generic navigation perception source or proof of game-independent recognition.
Do not route all four cards through it merely because it is already scheduled.

## Existing fixture sources

- `ProjectMaelstrom/ProjectMaelstrom.Tests/ModuleRunnerTests.cs`:
  `LabBundle`, `Binding` and `Frame` provide independent-game four-module data.
  Navigation approaches Pad, sigil interacts into Station, menu reaches Ready,
  native Auto-Idle opens PauseMenu and reaches Resting. Five proposals cover all
  four kinds; the fixture uses a1600×900/grid12 binding, not a W101 layout.
- `ModuleReplayTests.Prepare` serializes this bundle plus session manifest and
  event JSONL, supplying exact proposal IDs, released receipts and newer outcomes.
  Existing regressions cover missing receipts, unreleased/rejected receipts,
  absent outcome binding, old-module receipts, malformed clocks/events and
  deterministic hash/zero-dispatch reports.
- `TestData/Modules/W101Menus` contains loading, native-idle, outside, reward,
  snack and graphics-menu PNGs with provenance/recognition cases. These are
  narrow saved-image evidence, not all four live module capabilities.

## Admission and execution checks required for cards

1. Freeze bundle, manifest and events before preparing a job; pin originals so
   edits while queued reject admission. Execute frozen staged bytes, never
   reread mutable user files after pin verification.
2. Keep the card's selected module kind, bundle kind and game identity coherent.
   Reject mismatched kinds, or explicitly describe any multi-module selection;
   do not replay a battle/menu bundle under a navigation label accidentally.
3. Validate local event filenames and reserve staged bundle/session/report names
   so an events filename cannot overwrite another admitted artifact.
4. A card returns one `PreparedDeckJob`; it must not call `SubmitOwned` or
   `SubmitOffline` inside an executing scheduler callback. Refactor prepared
   methods beneath the existing Submit wrappers if necessary.
5. At baseline `ModuleReplay.Run` has no cancellation token. A scheduled adapter
   must add per-event cancellation to the existing replay seam or accurately
   report its bounded but noninterruptible phase; a token check only before
   replay is not continuous cancellation support.
6. Offline jobs must have no lease and must not use `ModuleOwnedInput`. Retain
   incomplete/failed replay distinctly; a receipt alone never becomes success.
7. Owned preparation is only a seam for trusted code. Binding/lease checks do not
   qualify localization, movement, menus or target input. Imported JSON cannot
   select callbacks or grant live qualification.

These findings were sent directly to the adapter and card implementers before
their integration. The review is not a claim that the new adapter passed tests.

## Product boundary

Expose rehearsal mode clearly in card titles/settings and completion text.
The existing `w101.auto-idle` live preparation card must not silently change into
an offline replay card. Native Idle entry is finite and separate from wake/resume
and automatic between-run scheduling. Spin/step/routine compilers use bounded
pulses with outcome checks; none provides an unlimited idle timer.

Current `ModuleOwnedInput.ForPetDance` is a concrete W101 postgame transport,
not a general navigation dispatcher. Its target/geometry/control/freshness,
loading, duplicate-proposal and release-recovery guards are worth preserving.
Other module players still require game-specific observation/action bindings.

The smallest delivery is four reusable rehearsal cards plus a shared prepared
operation seam, with live capability kept explicitly separate. That advances
composition without implying autonomous route travel or automatic Idle works.

## New-source peer review

The new `ModuleJobAdapter` freezes bundle/manifest/events bytes, pins source
paths, stages reserved filenames, validates homogeneous module kinds and returns
one prepared job. `ModuleOperation` now offers preparation underneath its Submit
wrappers; no nested scheduler is introduced. `ModuleReplay` gained cancellation
checks. The four catalog entries are explicitly offline and the old live
`w101.auto-idle` preparation entry remains separate.

Two refinements were requested before integration: preserve replay `Stopped` as
Failed rather than collapsing it with incomplete/Unknown evidence, and label
screen review's W101-specific OCR scope clearly. These requests were sent to the
owners. Generic card replay is game-neutral and does not pass a current workspace
game ID; it therefore validates the source bundle's game, not workspace matching.
That is appropriate only while described as offline evidence review.

The trusted owned adapter still accepts application callbacks; preparing it
does not execute them or qualify a live game. Per-event cancellation is present,
but cancellation before report creation may leave admission/staged sources rather
than a completed trace. Do not present that as a completed rehearsal.

This peer review is static. The coordinator's final integrated suite and report
determine tested status; this report supplies no new pass count.

Follow-up source inspection confirms stopped replay now maps to Failed, and
cancellation preserves admission/staged-source artifact references. Input files
are staged in a separate `module-inputs` directory, reducing collision with the
scheduler's own reports. The coordinator still owns final test verification.
