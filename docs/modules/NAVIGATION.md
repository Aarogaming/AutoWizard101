# Reusable navigation module

`NavigationModule` compiles supplied landmark/action data to the existing
`RoutineGraph`. It does not recognize images, choose keys, call an input API,
open a map or decide that a destination was reached from elapsed time. A game's
adapter supplies observations and normal controls; `ModuleRunner` supplies the
shared fact/control gates, visit binding, cumulative limits and module handoff.

## Version 1 data contract

Module `Kind` is `navigation`; schema version is 1 and module version `1.0.0`.
`OperationModuleSpec.Id` and `GameId` are preserved in the compiled graph.

```text
NavigationSegment(Id, FromScene, ToScene, ProgressScene, ActionId)
NavigationConfiguration(Segments, Actions,
    MaxActions=32, TimeoutMs=60000, TransitionTimeoutMs=10000)
NavigationModule.Compile(OperationModuleSpec) -> OperationModuleDefinition
```

`Actions` uses the common `ModuleAction` contract: named action, keyboard pulse
or verified-control click, key if applicable, duration and exact control ID.
The module contains no Wizard101-specific keys, world names, menu definitions,
camera assumptions or home-area rules. Required context facts belong to the
module specification; their meaning and observation come from the game adapter.

A configuration has 1–16 ordered segments. Every segment has a distinct ID and
progress cue; each next starting scene equals the previous destination. The
landmark chain cannot revisit a prior landmark, and progress/landmark identities
cannot collide or use `Loading` / `Unknown`. All referenced actions must have
exactly one validated binding; unused bindings are rejected. These are finite
route descriptions, not a general map graph or a pathfinding implementation.

Limits are at most 64 action proposals, a 1–120 second module deadline and a
positive transition deadline no longer than the module deadline. Action count
must permit at least one proposal per supplied segment. Common bindings cap
individual input durations; duration does not prove distance or arrival.

## Observe, act, verify

The first identified source landmark can propose that segment's action only
through the common verified-fact/control gate. The runner then requires a
matching accepted/released receipt and a newer bound outcome:

- The segment's `ProgressScene` repeats its bounded action.
- Its `ToScene` advances to the next segment.
- The final `ToScene` completes navigation without a menu action.
- Other landmarks cannot skip steps. `Unknown` and `Loading` pause proposals
  while deadlines remain finite.

Every navigation edge sets `RequiresControl` and `RequiresOutcomeBinding`.
`OutcomeForProposalId` must name the exact pending proposal. A held progress
label associated with the previous pulse cannot initiate another pulse, and an
accepted input receipt alone cannot advance the route. A live verifier must
measure progress around that particular action; copying its ID onto an unchanged
label is not verification. That image-based motion/arrival verifier is still
external and unqualified for W101.

User takeover, cancellation, target/geometry/context changes, input ownership
loss and rejected/unreleased receipts retain the shared runner's stop behavior.
Loading a module grants no desktop control. Independent modules may follow
verified arrival, but this module does not contain a sigil/menu/idle shortcut.

## Game adapters and validation

The [W101 adapter](../W101_SHARED_OPERATIONS.md) maps the existing motion registry
and supplied landmarks into this configuration, then composes separate sigil,
menu and Auto-Idle modules. It preserves W101's normal UI-only controls and does
not implement map-click teleportation. Another game can supply other scenes,
controls and required facts without changing this compiler.

`ProjectMaelstrom/ProjectMaelstrom.Tests/NavigationModuleTests.cs` exercises two
different game IDs/key bindings, bounded progress repeats, arrival, held/wrong
outcome IDs, missing receipts, stale frames, wrong landmarks, loading, takeover,
action limits, malformed routes and unknown configuration fields. W101 adapter
tests separately exercise named facts/controls and the four-module replay.
These are synthetic tests; they do not measure perception or live navigation.
The coordinator owns serialized build/test execution and the current result.
