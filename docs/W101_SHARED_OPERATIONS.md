# Shared Wizard101 operations

Work item: `MODULE-NAVIGATION-04`, migrating `W101-OPERATIONS-03` into reusable
modules under `W101-NAVIGATION` and the shared routine foundation. This is an
offline composition increment. It does not enable a new
controller, select a window, wake an idle wizard or qualify unattended travel.

Wizard101 remains one workspace. Navigation, minigame entry, menus and Auto-Idle
should call named operations through the existing scheduler and input owner.
A saved routine describes intent and checks; it is not permission to dispatch.

## Existing capability and the bridge

| Operation | Reusable implementation | What is actually established | Remaining gate |
| --- | --- | --- | --- |
| Turn left/right; step forward/back | `W101Routine.Primitives`, `HomeMovementOptions`, `DodgeInputPulse` | Four explicit A/D/W/S defaults, bounded pulses and released-input receipts. | Current bindings, clear path and actual movement must be independently observed. |
| Spin / Step / Routine Auto-Idle | `HomeMovementRun`, `HomeMovementGate`, `HomeMovementLease` | Separate persisted options, finite sessions/cycles, fresh before/after images, latched takeover and no automatic focus regain. | Image change is not semantic movement, arrival, obstacle avoidance or proof of preventing logout. |
| Reach a route landmark | Saved Commons demonstration; shared `NavigationModule` and `RoutineRunner` | Human walking and signed landmarks are recorded; the generic module awaits named progress/arrival outcomes. | No qualified landmark detector, route correction, obstacle avoidance or independent autonomous arrival yet. |
| Interact with a named sigil | `PetDanceEntryVision`, `PetDanceVisit`, `game.interact` graph action | Offline entry logic requires an identified control and bound state; X is a normal UI action. | Entry dispatch and current-client sigil recognition remain separate qualification tasks. |
| Select a menu control | `PetDanceMenuInput.MapPoint`, `PetDancePostGameClassifier`, `PetDancePlayButtonVision` | Current postgame dispatcher handles its small allowlist, maps outer-frame bounds to the current client, releases input, and never activates a window. | It is not an arbitrary menu/sigil dispatcher. Broader control identities and geometry need explicit adapters. |
| Return to native Idle | `PetDancePostGame`, `PetDanceNativeIdle`, `PetDanceIdleConfirmation` | Bounded postgame implementation observes reward/Finish/menu/Idle, preserves inherited ownership and does not feed. | Source fixes/offline checks are distinct from a fresh installed full-visit result. |
| Branch/wait/repeat and replay | `RoutineGraph`, `RoutineRunner`, `RoutineReplay` | Pure bounded decisions, proposal-bound receipts, newer outcome requirement, context binding, finite deadlines and zero-dispatch replay. | Live adapters must supply verified perception and trusted action bindings; JSON cannot grant them. |
| Pack keyboard vocabulary | `tools/game_packs.py`, `W101Routine.ExportPack` | Schema-v2 action IDs map to bounded keyboard definitions; draft exports preserve ordering and have no qualified skills. | Menu clicks/semantic navigation are not silently added to the keyboard registry by this increment. |

Source names above are under `ProjectMaelstrom/ProjectMaelstrom/Utilities/`
unless another path is given. Older [Auto-Idle](HOME_MOVEMENT.md) and
[destination-navigation](W101_DESTINATION_NAVIGATION.md) notes describe earlier
UI delivery; the postgame source now contains an automatic Idle operation.
Neither those historical notes nor source presence establishes current live
qualification. See the [roadmap](ROADMAP.md) and current board for delivery status.

## Named operation contract

The offline composition uses existing motion IDs `turn-left`, `turn-right`,
`step-forward`, `step-back`, plus `game.interact`, `game.open-menu`,
`wizard101.native-idle` and the narrow proposed `game.cancel-menu` control.
Cancellation means the recognized sigil menu's Cancel button, never an arbitrary
click or an assumption that Escape closes every screen. Each binding retains its
keyboard/click kind, bounded duration and required control identity.

The route context names the route, world, starting/destination zones and sigil.
Each segment supplies its starting landmark, next landmark and one motion
primitive. The operation can repeat that short primitive only after a new,
verified progress observation bound to the exact pending proposal ID. Motion
edges opt into `RequiresOutcomeBinding`; `OutcomeForProposalId` must identify
the pulse whose progress or arrival is being reported. Reusing the previous
pulse's held Progress label, even with a newer timestamp, cannot trigger another
pulse. A new, correctly bound arrival observation selects the next segment.
Time passing, a successful key release, a changed background or a loading screen
cannot establish arrival.

Every live adapter must still verify target/process identity, current client and
capture geometry, route/visit context, fresh observations, input ownership and
the named control or clear movement area. `ControlVerified` in a synthetic trace
is a test input, not a detector or a reusable permission flag. Accepted/released
dispatch receipts remain separate from observed outcomes. Rejected/mismatched
receipts, target/context changes and user takeover stop the current runner.
Unknown and loading scenes suppress proposals while finite deadlines continue;
they do not erase a pending action or manufacture progress.

Generating those bound motion outcomes is still external and unqualified. A live
adapter must compare evidence around that particular released pulse; it cannot
copy the current proposal ID onto a held semantic label and call it progress.
This compiler and its synthetic fixture do not implement that visual verifier.

## Separate idle choices

- **Native Idle:** recognize outside/menu state, open the in-game menu if needed,
  select its identified Idle control and observe `YOU ARE IDLE`. If already Idle,
  finish without input. There is no wake action or recurring movement.
- **Spin:** preserve the chosen left or right pulse; opposing turns remain an
  explicit existing option. No automatic reversal or continuous held key.
- **Step:** use the existing W/S pair only in the independently confirmed clear
  home context. The pair can drift and is not a return-to-origin guarantee.
- **Routine:** use the existing 1–8 shared primitives and 600 ms maximum cycle.
  Translation retains its clear-home requirement. Each pulse needs its own newer
  verified outcome; unknown evidence cannot skip a step.

The new offline idle composition rehearses one bounded cycle. It does not replace
the existing interval/session gate, regain focus, start a recurring scheduler or
implicitly wake W101. Native Idle stays distinct from keeping a character moving.

## Shared system versus game-specific knowledge

`W101SharedOperations` now constructs configuration data only. Its previous custom
graph compilation and `W101OperationPlan` / `W101OperationBinding` types are
removed. `RouteAndMenuVisit` returns an `OperationModuleBundle` containing:

1. `navigation`: supplied route segments, completing at the final landmark.
2. `sigil`: the named sigil's normal X interaction and observed menu.
3. `menu`: the named Cancel control and observed outside state.
4. `auto-idle`: the explicit game-menu path ending at native Idle.

`IdleCycle` returns a one-module bundle using the same generic `auto-idle` module.
All six existing preferences are preserved: left/right spin, opposing turns,
walking pair, saved routine and native Idle. Shared compilers produce
`RoutineGraph` definitions; `ModuleRunner` composes them through `RoutineRunner`,
with cumulative action/time limits and the same receipt/outcome discipline.
No Wizard101 controller, scheduler or input implementation is copied into the
generic modules. [Navigation](modules/NAVIGATION.md) has no W101 keys, map names,
home assumptions or bundled menu behavior.

The W101-specific data is its key/control vocabulary, world/zone/visit context,
landmark names, idle preferences and semantic outcomes. Navigation requires exact
facts `wizard101.world.<world>`, `wizard101.route.<route>` and
`wizard101.movement-bindings`. Motion idle requires `wizard101.idle-context` and
`wizard101.clear-area`; translating choices additionally require
`wizard101.home-confirmed`. Menu and sigil actions retain their exact named
control. A generic true flag cannot substitute for those supplied fact/control
identities. They still need a qualified observation adapter; names are not proof.

`OperationModuleBundle.Bind` combines the supplied context and visit ID with the
selected target and geometry. The JSON module/action configurations are data for
rehearsal and trusted-adapter review, not the scheduler's live allowlist. Cancel
still has no new live dispatcher. The existing Python pack keyboard registry is
unchanged; module click data does not silently become a keyboard action. The
common versioned module contract is shared across games, while a later pack
adapter can expose those same definitions without another execution engine.

## Saved Commons evidence

Use [the demonstration report](W101_DEMONSTRATION_20261108.md) and local
`.local/w101-route-study/human-route-01/{demonstration,section-timing,input-review}.json`.
The demonstrated route starts at the Commons anchor, crosses a bridge, approaches
the signed Pet Pavilion tunnel, loads into the Pavilion and reaches the Dance
tent. The early visible Shopping District tunnel is a different destination.
No End key was recorded in that demonstration, so it does not qualify a reset via
the Commons port. A map label is informational, not teleportation.

The route review interval is approximately 5–45 seconds. Its recorded forward
holds include multi-second holds with short A/D corrections; these are candidates
for learning local motion, not safe durations to replay unchecked. The manifest
keeps `route_qualified`, `labels_verified` and
`exact_input_video_alignment_verified` false. Capture, client and encoded-video
geometry also differ. No new video decoding, labels, images or route facts were
created by this operations task.

## Offline increment and tests

Owned implementation: `Utilities/Modules/NavigationModule.cs`, its generic tests,
the W101 data adapter in `Utilities/W101SharedOperations.cs` and its adapter tests,
plus the synthetic session under
`ProjectMaelstrom.Tests/TestData/Routines/w101-operations/`. These use the common
module contract and existing graph runner; they contain no capture, OCR, model,
input dispatcher, wall-clock sleep or window activation.

The representative rehearsal is a supplied two-segment route → verified sigil
interaction → recognized instructions → Cancel → outside → menu → native Idle.
It deliberately does not press Play, feed a snack or purchase anything. Supplied
landmark/control labels and receipts are synthetic; they do not train or validate
a real detector. Other game policies should consume the same operation contracts
rather than introduce parallel controllers.

The preserved 19-event fixture has migrated to `ModuleReplay`: observations use
`module_observation` with explicit facts/controls, and all seven outcomes bind to
their exact module-scoped proposals. It is no longer a `RoutineReplay` fixture.
Its expected result is four completed modules, seven receipts, seven proposals
and zero desktop dispatches. This is authored contract evidence, not game success.

Acceptance checks cover progress-driven repeated pulses, wrong landmarks,
missing/rejected/cross-run receipts, stale observations, required control flags
and stored control identities,
loading, context change, takeover, all existing idle methods, no wake from native
Idle, malformed routes/budgets, and deterministic zero-dispatch session replay.
Build and test execution are coordinated centrally. Source/tests being present
must not be reported as a passed run until that check has completed.

The [integrated checkpoint](W101_OFFLINE_QUALIFICATION_20261110.md) now records
1,127 native tests passing, including the 24 shared-operation cases and the new
per-proposal outcome-binding regression. This is offline contract qualification;
the synthetic landmarks do not establish navigation accuracy.

## Next bounded work

1. Qualify route landmark identities and their current geometry from preserved
   evidence; collect missing holdout observations separately when authorized.
2. Bind the generic operation metadata to the existing scheduler and target-bound
   dispatchers, with explicit supported controls and the same input lease.
3. Add state-aware progress/stuck/obstacle tests before any live route attempt.
   No movement controller should treat scene difference as arrival.
4. Expose these checked operation definitions as routine clips. UI editing must
   preserve missing bindings and qualification warnings; it must not auto-start
   input or advertise map-click travel.

This work leaves enemy avoidance, automatic route learning, broad menu discovery,
world-gate travel and long-duration logout prevention unqualified. They are
separate increments, not hidden side effects of loading an operation definition.
