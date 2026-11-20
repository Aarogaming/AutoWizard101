# Reusable module standardization — 2026-11-10

This checkpoint standardizes navigation, sigils, menus and Auto-Idle as native,
game-independent modules with one contract, loader, composition runner and offline
replay path. Three parallel agents implemented/reviewed separate modules while
the coordinator owned the common contract, integration, board and serialized
validation. All work stayed offline; no game input, capture, model service,
installer, volume change or new dependency was used.

## What changed

- Added `OperationModuleSpec`, `OperationModuleBundle`, action/observation/decision
  contracts and a closed `ModuleCatalog` with version, size and configuration
  validation. Bundles save as explicit JSON sidecars with create-new semantics.
- Added four factories under `Utilities/Modules/`: `NavigationModule`,
  `SigilModule`, `MenuModule`, `AutoIdleModule`. Native Idle reuses menu compilation.
  Keys, landmarks, controls, facts and native-idle labels come from game data.
- Added `ModuleRunner` composition around the existing `RoutineRunner`, with a
  shared action/deadline budget, immutable run snapshot, named fact/control gates,
  scoped proposal IDs and exact receipt/outcome handoffs. There is no second input
  dispatcher or scheduler.
- Migrated `W101SharedOperations` from its custom graph compiler to a data adapter
  producing the four-module route/visit and one-module Auto-Idle bundles. Existing
  six idle preferences, home checks and normal A/D/W/S/X/menu bindings are retained.
- Added `--replay-modules`, using the existing game-session envelope and bounded
  replay readers. Reports carry exact source hashes, module progress, decision
  traces and explicit zero-dispatch/non-live evidence.
- Saved a directly replayable example at
  `ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/Routines/w101-operations/`.
  A regression compares its bundle to the W101 adapter's canonical output, so the
  sample cannot silently drift from implementation.

## Verification

The complete native test suite passes **1,230/1,230**, zero failed or skipped, in
about six seconds of test execution. Build used warnings as errors, one build
worker, two reported processors, no dependency restore and a hang timeout.
There are **127** module/W101-adapter cases in this suite; the total is 103 above
the previous 1,127-test checkpoint. This replaces/extends existing adapter tests,
not a claim that all 127 are newly added. Final evidence:
`.local/module-standardization-20261110/modules-final.trx`.

Coverage includes:

- Individual module validation and an independent `offline-lab` game profile
  using other keys, scenes, controls and a `Resting` terminal through all four
  modules; no W101 constants or compiler are needed by that fixture.
- Fresh facts and exact next-action controls within a menu and across modules;
  target/geometry/visit changes, ownership loss, cancellation and takeover.
- Loading/unknown pauses, stale/repeated/backward timestamps, held progress,
  exact newer outcomes, missing/rejected/unreleased/cross-module receipts.
- Per-module and cumulative bundle limits, no wake/restart edges in Idle,
  mutable caller-data isolation, conflicting action bindings and malformed JSON.
- Deterministic reports, authoritative envelope time, invalid/escaping event paths,
  source hashes, strict output preservation and no fabricated completion.

The actual built application's saved-bundle CLI replay ran twice and produced
byte-identical reports: **4 completed modules, 7 proposals, 7 supplied receipts,
0 desktop dispatches**, `live_qualified: false`. Runtime was **0.1183 s** and
**0.1088 s**. TargetPath was queried from MSBuild rather than assuming the stale
`win-x64` sibling. Receipt and report paths are in
`.local/module-standardization-20261110/cli-validation.json`.

The tested application assembly SHA-256 was
`6f5e9875d551610e43716d2849b91672007253d4e0ccaf605b000da275c0887f`.
Scoped whitespace/analyzer results and source fingerprints are preserved beside
the test and CLI evidence. No package was built or installed for this task.

Independent review found an allocation-order issue: constructor/save paths now
validate the in-memory bundle before serializing a snapshot. Initial tests also
exposed inconsistent exception types for nonfinite configuration values; the
shared specification helper now reports invalid configuration consistently. Both
early test failures remain in local reports; the final full-suite report is the
acceptance evidence. The restricted test host initially stalled before execution;
it was cancelled and confirmed gone before the bounded normal offline run.

## Scope and next acceptance gates

The module standardization slice is complete. The broader workstreams remain
**integration_pending**, since offline contracts do not establish live game
perception or complete user-facing authoring:

1. Attach qualified scene/fact/control and motion-outcome verifiers to these
   contracts using saved game footage. Authored labels are not recognition proof.
2. Connect proposals/receipts to the existing scheduler and sole input owner;
   migrate older live adapters through that same interface without adding another
   controller. Native Idle cleanup still respects user takeover.
3. Add module clip editing, binding diagnostics and explicit sidecar save/load to
   the game workspace. The current CLI and catalog are functional; automatic pack
   discovery and timeline editing are not supplied by this checkpoint.
4. Separately qualify bounded full-game visits against real observations after
   the offline gates. Actual W101 landmark/obstruction recognition and fresh
   Conjuration card data remain open, as does Dodge's average-50 objective.

Use [Reusable modules](REUSABLE_MODULES.md) for the contract and API,
[the W101 adapter](W101_SHARED_OPERATIONS.md) for game bindings, and
[the project board](coordination/workstreams.json) for current assignments.
