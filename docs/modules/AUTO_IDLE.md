# Auto-Idle module

`auto-idle` compiles one finite operation into the existing `RoutineGraph` and
`RoutineRunner`. It introduces no input dispatcher, recorder, focus handling,
timer, recurring job or game-specific key list. Module specifications describe
intent; they do not grant runtime permissions or establish live qualification.

Implementation: [AutoIdleModule.cs](../../ProjectMaelstrom/ProjectMaelstrom/Utilities/Modules/AutoIdleModule.cs).
The shared contract is
[OperationModule.cs](../../ProjectMaelstrom/ProjectMaelstrom/Utilities/Modules/OperationModule.cs).
Native-menu composition uses
[MenuModule.cs](../../ProjectMaelstrom/ProjectMaelstrom/Utilities/Modules/MenuModule.cs).

## Configuration

```csharp
AutoIdleConfiguration(
    string Mode,
    ModuleAction[] Pulses,
    string EntryScene = "IdleAreaReady",
    string[]? OutcomeScenes = null,
    MenuConfiguration? NativeMenu = null,
    double TimeoutMs = 30000,
    string NativeIdleScene = "NativeIdle")
```

`AutoIdleModule.Compile(spec)` requires `spec.Kind == "auto-idle"`. It returns an
`OperationModuleDefinition` preserving the original module ID, version, game ID
and required facts. The compiled graph ID equals the specification ID. Required
facts are explicit inputs to the shared module runner; this compiler does not
claim that a clear area, home or any other prerequisite has been observed.

| Mode | Configuration | Behavior |
| --- | --- | --- |
| `native` | Empty `Pulses`, explicit `NativeMenu`, no motion outcomes | Delegates the observed menu path to `MenuModule`; final scene must equal `NativeIdleScene`. |
| `spin` | Explicit keyboard pulses and one outcome label per pulse | One bounded motion cycle using the supplied bindings. |
| `step` | Explicit keyboard pulses and one outcome label per pulse | Same shared execution contract; a game adapter supplies safe stepping actions and context facts. |
| `routine` | Ordered keyboard pulses and one outcome label per pulse | Reuses identical action bindings where repeated; preserves the entire sequence order. |

The motion mode names describe the caller's intent. They do not inspect a game or
infer which keys turn or move it. Another game can use `J`, `I` and `K`, or another
registered keyboard vocabulary. A W101 adapter supplies its existing idle choices
and clear-home prerequisites; the generic module contains no W101 dependency.

## Motion cycle

Motion requires 1–8 keyboard pulses, each 40–150 ms, with at most 600 ms total
held-key duration. A repeated action ID must retain the exact same kind, key,
duration and control ID; conflicting bindings are rejected. Clicks are excluded
from motion modes. A `NativeMenu` cannot silently accompany a motion cycle.

`EntryScene` is the first action's observation trigger. `OutcomeScenes` must have
exactly one entry per pulse. Repeated outcome labels are allowed because every
edge also requires a new outcome bound to the exact proposal ID. A later image
of the same held state, an old outcome token or a released-key receipt alone
cannot advance the next pulse. The shared runner still requires fresh target,
geometry/context, ownership and control checks.

After the final pulse's accepted/released receipt and matching fresh bound
outcome, the graph completes. It has no loop or restart edge. A separate trusted
scheduler would have to explicitly request another bounded cycle and recheck its
prerequisites; this module does not implement that recurring behavior.

`Loading`, `Unknown` and the configured `NativeIdleScene` suppress motion
proposals. Neither entry nor a movement outcome may use those labels. Thus an
already idle game is not awakened by a motion cycle. Waiting remains bounded:
each action transition gets at most five seconds, capped by the module timeout;
the overall finite timeout must be greater than zero and at most 300,000 ms.
User takeover, cancellation, changed binding or lost input ownership remain
latched stops in the shared runner, not automatic resume conditions.

## Native menu path

Native mode compiles a child menu specification with the same identity/game/facts,
then wraps its graph and actions with the original `auto-idle` specification.
The effective menu timeout is the smaller of `TimeoutMs` and the menu's own
timeout. Its transition timeout must fit that effective budget; inconsistent
limits fail validation instead of silently extending the outer operation.

The final menu step must observe `NativeIdleScene`. No menu step may begin from
that scene, so the configuration cannot encode a wake operation. The menu's
`AllowIntermediateEntry` and `AllowAlreadyComplete` remain explicit choices.
When already-complete adoption is enabled, observing the bound terminal state can
complete without an action. When disabled, the terminal state alone does not
establish a completed visit and still cannot dispatch a wake action.

`NativeIdleScene` defaults to `NativeIdle`, but another game may use `AFK` or an
equivalent qualified label. It must differ from `Loading` and `Unknown`.
`EntryScene` is a motion-only field; native entry is defined entirely by its
explicit menu configuration. All native actions retain identified controls and
proposal-bound observed outcomes through `MenuModule`.

## Verification scope

[AutoIdleModuleTests.cs](../../ProjectMaelstrom/ProjectMaelstrom.Tests/AutoIdleModuleTests.cs)
contains pure tests for all modes, another game's keys and AFK label, pulse/action
bounds, repeated bindings, required controls, receipt/outcome separation, repeated
outcome labels, loading/unknown waits, takeover, finite deadlines and no restart
or wake behavior. They use synthetic observations and simulated receipts.

Build/test execution is coordinated centrally. Source and test presence are not
a claim that the tests have run, that any game moved, or that logout prevention
worked. A future live adapter must use the existing scheduler/input owner and
qualify actual keys, clear areas, observed effects and target geometry separately.
