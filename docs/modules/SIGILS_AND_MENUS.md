# Reusable sigil and menu modules

`SigilModule` and `MenuModule` compile game-pack configuration into the existing
`RoutineGraph`. They add no runner, controller, OCR engine, capture service,
coordinates or game-specific keys. The shared module coordinator owns required
facts and named-control bindings; the shared routine runner owns receipts,
outcomes, target/context checks, user takeover and finite execution budgets.

These modules are offline source components. Loading or compiling their JSON
does not authorize live input. A game adapter must separately qualify its visual
scenes, action bindings and current target context.

## Shared contract

Both use the versioned `OperationModuleSpec` and return
`OperationModuleDefinition(Spec, Graph, Actions)`. The compiled graph preserves
`spec.Id` and `spec.GameId`; required facts remain in the specification. Action
bindings are ordinary `ModuleAction` data, validated by `OperationModules`:
bounded keyboard pulses or clicks on a verified named control. There are no
callbacks, scripts, input handles or executable payloads in these definitions.

Every compiled edge has `RequiresControl = true` and
`RequiresOutcomeBinding = true`. For an action to advance a module:

1. Supply a fresh observed trigger scene in the bound target, geometry and visit,
   with required context facts, input ownership and the named control verified.
2. Submit the actual accepted/released receipt for the exact proposal and action.
3. Supply a newer expected scene whose `OutcomeForProposalId` identifies that
   proposal. An observation timestamp alone cannot stand in for a measured outcome.

The adapter defines how an outcome is measured. The compiler never fabricates a
receipt or infers that a sent key succeeded. `Unknown` and `Loading` pause action
selection while the original overall and transition deadlines keep running.
User activity, cancellation, changed context, or lost target/input ownership stop
the existing runner; these modules do not perform compensating cleanup afterward.

## Sigil interaction

```csharp
SigilConfiguration(
    string EntryScene,
    string MenuScene,
    ModuleAction Interaction,
    double TimeoutMs = 15000);

SigilModule.Compile(OperationModuleSpec spec); // spec.Kind == "sigil"
```

An observed interaction point proposes one configured action, then waits for its
exact menu outcome. Initial visibility of the menu does not count as this sigil
operation succeeding. Entry/menu scenes must differ and cannot be pause scenes.
The graph allows at most one action and a finite timeout up to 300,000 ms.

The game adapter supplies labels such as a destination-specific interaction
point, its menu, an interaction control, and the game's chosen key. No module
implementation assumes a particular game, landmark, keyboard binding or UI size.

## Menu path

```csharp
MenuStep(string Id, string FromScene, string ToScene, ModuleAction Action);

MenuConfiguration(
    MenuStep[] Steps,
    bool AllowIntermediateEntry = false,
    bool AllowAlreadyComplete = false,
    double TimeoutMs = 15000,
    double TransitionTimeoutMs = 10000);

MenuModule.Compile(OperationModuleSpec spec); // spec.Kind == "menu"
```

A configuration contains one to sixteen contiguous steps: a step's expected
`ToScene` is the following step's `FromScene`. The final `ToScene` is the module's
completion observation. Each transition has its own bounded deadline, no larger
than the overall deadline. The maximum action count is the number of steps.

Default entry accepts only the first step's scene and requires the full observed
path. `AllowIntermediateEntry` explicitly permits starting at any configured
step; every entry trigger must then be unique. `AllowAlreadyComplete` separately
permits a fresh final scene to complete without sending an action. A final scene
cannot be both an allowed entry trigger and an already-complete condition.
For example, a native-idle module may explicitly accept an already visible idle
banner. A sigil operation does not gain that behavior automatically.

Repeated scene labels are allowed inside a default ordered path when the adapter
can verify each separate proposal's outcome. Such a path cannot enable ambiguous
intermediate entry. Repeated action IDs are allowed only when their key/kind,
duration and control binding are identical. Step IDs remain unique. Malformed,
disconnected, unbounded or ambiguous configurations are rejected before a runner
is constructed.

The menu path represents the protocol of observed screens and controls. There is
no timing script or gameplay policy in it. A game pack can reuse these operations
between navigation, specialists, resource checks and other separately qualified
modules. Game-specific recognition remains in the game adapter and its evidence.

## Integration and verification

The intended reuse is direct: navigation reports a verified arrival scene, a
sigil module observes entry and opens its menu, a menu module selects or exits a
screen, and a native-idle module can compose the same menu compiler. These
boundaries preserve the existing shared input owner and outcome machinery.

Tests exercise compiled metadata, initial/intermediate/completed entry policies,
named-control gates, receipts, stale/wrong outcome bindings, loading, timeouts,
user takeover, repeated action IDs and invalid configurations. They run only the
pure routine engine and cannot send desktop input. Source and tests are prepared;
the coordinator owns the serialized build, formatting and integration checks.
There is no new live qualification from these compiler tests.
