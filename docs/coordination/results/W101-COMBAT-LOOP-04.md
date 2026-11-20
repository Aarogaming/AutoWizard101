# W101-COMBAT-LOOP-04 — Bounded native turn orchestration

2026-11-11. Source prepared for coordinator integration and testing. No game,
process, capture, desktop, model, build or test runner was used by this task.

## Scope

`W101CombatTurnOperation.Prepare` returns the existing `PreparedDeckJob`; the
caller submits it through `OperationScheduler`. It requires the existing current
`GameInputLease`, an explicit enabled flag, pinned client image identity and
trusted observation/planner/dispatch callbacks. There is no default runtime,
automatic client connection or newly advertised live capability.

The adapter compiles a small graph for the existing `RoutineRunner`:

- Pass/direct card: fresh planning evidence → one input receipt → a newer round
  or explicit victory observation.
- Targeted card: fresh planning evidence → card receipt → matching observed
  selected-card identity → freshly resolved target control → target receipt →
  newer round or explicit victory.

The prior built-in `combat-turn-decision` graph remains untouched; it has no
selected-card observation stage. No new scheduler or input implementation exists.

## Contracts and limits

- Snapshot identity includes exact process/window/start identity, encounter,
  round, geometry, acquisition time and unique snapshot ID. Card instances and
  combatants use distinct semantic identities; UI indices and display names are
  insufficient. Controls carry snapshot-local IDs and normalized client bounds.
- A plan pins the snapshot ID and semantic card/pass/target identities. Every
  input gets a newer reading; reordered controls resolve to their current
  positions. Changed target, round, geometry, ambiguous identity, disabled control
  or stale/future observation prevents input.
- The first card/pass input additionally replans against the freshly acquired
  decision facts. Changed HP, pips, effects or spell metadata cannot silently
  renew the opening plan just because its button still exists. The confirmed
  plan must select the same semantic action/card/target; a changed or rejected
  decision stops before any input. Target clicks separately require the current
  trusted control binding to be enabled, the same role and explicitly alive;
  unknown/dead target evidence prevents the second click.
- Planner input and control arrays are copied before admission and before
  invoking callbacks. At most 32 controls and 64 KiB planner JSON per reading;
  default 80 readings, maximum 128; at most two dispatches. Polling defaults to
  200 ms, acquisition age to 1.5 seconds, one-turn time to 60 seconds (maximum 90).
- Loading/unknown pauses within the same finite read/time budget. A disappearing
  hand or a resolving animation is not victory and cannot complete the turn.
- Cancellation/user takeover stops new actions. Input and observer release must
  be confirmed; missing release/disconnect evidence latches the existing recovery
  guard. A linked turn-deadline token reaches read, plan, dispatch and delay
  callbacks, with fresh deadline/freshness gates checked again after callbacks.
  Callbacks must honor cancellation and finish cleanup before returning; they
  are awaited rather than detached, so ownership is not abandoned merely because
  a timeout was requested. This does not forcibly preempt a callback that ignores
  cancellation: qualified process-backed bindings still need their own bounded
  cooperative-stop/termination/verified-exit path.
- Accepted input without a newer outcome produces an explicit unverified result,
  with immutable observation files, plan, graph, input receipts and a turn report.
  A verified next round is distinct from observed battle victory. Neither proves
  spell damage, a dropped item, collection-count progress or a completed quest.

## Files and tests

- `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101CombatTurnOperation.cs`
- `ProjectMaelstrom/ProjectMaelstrom.Tests/W101CombatTurnOperationTests.cs`

Synthetic tests cover pass, card plus target, fresh reordered controls, explicit
victory, loading pauses, stale/future/wrong-target/wrong-geometry/wrong-round
observations, missing/ambiguous controls, wrong selected card, absent outcome,
cancellation after input, observer and input cleanup failures, changed target
role, user takeover, planner mutation isolation and default-off admission.
The review follow-up adds changed/rejected fresh planning facts, decision latency
expiring evidence, dead/unknown/disabled targets, and a deadline test proving
the owner stays held until an intentionally noncooperative callback returns.

Coordinator must run scoped formatting, analyzers, the filtered native tests and
integration suite. No passing-test or live qualification claim is made here.

## Next integration gate

Bind Gemini's reviewed observation/planner projection and a qualified native input
adapter. Current UI-tree evidence lacks combat identities, controls, resources,
selected-card and round outcomes; those fields must be independently qualified.
Do not bind synthetic controls or historical geometry to the live Quest card.
Multiple turns/battles and before/after quest progress remain outer routine work.
