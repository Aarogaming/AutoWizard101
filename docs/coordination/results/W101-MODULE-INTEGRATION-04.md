# W101 sigil, menu and native Idle bindings

2026-11-11. Offline source integration with three parallel agents. No installation
or live game interaction.

W101ModuleBindings maps current, target-bound UI-tree observations and independently
verified semantic context into the existing modules. Supported programs are one
selected interaction, opening the game options menu, and native Idle entry.
Loading, dialogue, unknown states and stale identity cannot authorize inputs.
The existing visual classifier supplies menu context; saved/manual/injected OCR
provenance is rejected by the live-context conversion. Historical fixture tests
use explicitly synthetic timing/provenance for conversion checks, not live claims.

W101ModuleOperation runs one prepared job under the existing scheduler lease.
The trusted runtime must provide independently correlated outcomes and current
target/control geometry. Released input receipts alone never complete a module.
Read-count/time budgets, cancellation, takeover and cleanup checks bound work;
uncertain reader cleanup or input release requires recovery.

ForCards implements configuration-to-operation translation: fresh exact target,
qualified mode, distinct visit, time limit, supported menu/native method and exact
selected interaction prompt. New w101.interact/menu cards and the existing native
Idle card use optional trusted adapter registration. Existing spin/step/routine
preferences are retained but are not silently converted into native Idle.

Validation: **2,127 native tests passed**, zero failed/skipped, warnings-as-errors.
Scoped native formatting/analyzer checks passed. Tests include actual scheduler
and deck execution with synthetic callbacks, saved-menu classifier evidence,
loading/dialogue/takeover, missing outcomes, prompt mismatch, wrong target,
unqualified mode and cleanup/release failure. Receipt:
`.local/w101-module-bindings-20261111/w101-module-bindings.trx`.

## Remaining readiness gates

The production host intentionally does not register these live adapters yet.
Aggregate UI-tree booleans do not identify an exact sigil or click rectangle.
Existing visual interaction-menu recognition is Dance-specific; arbitrary menu
navigation, Idle wake/resume and automatic between-run scheduling are not added.

Before a live action check, connect the concrete target-bound transport and
observation provider, preserve verified current geometry and selected prompt
identity, then test read-only binding evidence. A later separately scoped action
trial must verify response, release and cleanup. The injected offline runtime is
not a claim that the game has responded. Current scope is source-only bindings,
card preparation and tested orchestration, not enabled gameplay.

Board and roadmap track these remaining gates. No game/process reads, desktop
control, models, credentials, volume changes or new dependencies were used.
