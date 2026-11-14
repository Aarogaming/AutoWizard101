# Focus-only navigation checkpoints - 2026-11-12

Task W101-FOCUS-34, implementation pending coordinator integration and verification.

## Owned implementation

- W101NavigationVisitRuntime has optional WaitForFocus callback; real scope supplies its focus-only wait. Existing synchronous Ready remains immediate.
- Released stage admission can await focus under original parent/finalizer cancellation, before starting the action-stage timer. Original operation deadline remains bounded.
- Idle finalizer uses the focus-only callback when present, rather than the legacy activity-baseline reset callback. Each retry observes current game state; gameplay is never replayed by this finalizer.
- Feedback movement retains its original destination and controller across clean focus interruptions, discards stale observations/proposals, and reobserves the pose before continuing. Probe cleanup must leave the original shared input lease dispatchable; cleanup failure, cancellation, timeout and keyboard-stop decline resumption.
- Held-key paths still see immediate Ready=false and release in finally. No blocking wait was added to input guards, no window activation, no new owner, and no consequential HOME/X replay.

## Boundaries

This increment covers navigation released checkpoints and final Idle cleanup. It does not claim every W101 travel/menu/Dance stage can resume mid-call. A focus loss that returns before the caller can classify it may remain an unsuccessful attempt; a changed identity/geometry, unknown cleanup, deadline or foreground keyboard takeover remains a stop. Six observation/three movement budgets remain unchanged; pauses do not enlarge movement permission. Target coordinate calibration is not a qualified route.

Scope WaitForFocusAsync, target-filtered keyboard monitoring and caption are coordinator-owned dependencies. The callback must never reset activity baseline, hide a non-focus guard, or wait with a held key.

## Checks

New offline tests cover stage-boundary/finalizer resume without gameplay replay or baseline reset; foreground keyboard stop; partial released-pulse wait; cleanup latches/cancellation/timeouts. No build/test/live commands run by this worker yet. Await coordinator build slot.

Coordinator checkpoint: integrated native suite2513 passed, zero failed/skipped; scoped whitespace format/verify and analyzers passed. Warnings-as-errors compilation passed. Board30workstreams valid. No installation. Live evidence and limits are in W101-CONTROL-34; deliberate focus-switch recovery is offline-tested, not yet live-qualified.
