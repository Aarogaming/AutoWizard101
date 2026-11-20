# W101-COMBAT-BATTLE-09 — One encounter under the shared owner

2026-11-11. Source implementation and synthetic test cases are ready for the
coordinator's serial checks. No build, test runner, game, process inspection,
desktop control, capture, model, installation or dependency operation was run by
this task. This report makes no live combat or quest-completion claim.

## Integration contract

`W101CombatBattleOperation.Prepare(target, enableCombatActions, runtime, limits,
sourcePins, scope)` returns the existing `PreparedDeckJob`. Its runtime is the
existing `W101CombatTurnRuntime`; no second planner, reader, controller or input
implementation is introduced. The caller submits exactly one operation to
`OperationScheduler`, which owns one `GameInputLease` for the entire encounter.

Each turn prepares and directly executes `W101CombatTurnOperation` under that
same lease. The child context changes only its evidence directory and execution
GUID. All parent request pins, target, capability flags, operation/version and
budget remain intact. Distinct GUIDs give the existing `RoutineRunner` unique
proposal/receipt identities across turns. No child scheduler submission occurs.

The reader receives its ordinary per-turn read index and a unique `turn-NNN`
directory. Snapshot identities must nevertheless be unique across the entire
battle. Trusted bindings must derive snapshot IDs from a unique acquisition or
the supplied context identity, never only from the local integer read index.
Callbacks retain the existing cancellation/cleanup obligations and are awaited;
this adapter cannot forcibly terminate an uncooperative runtime binding.

## Limits and evidence

- Defaults: six turns, 180 seconds and 256 total reads. Hard maxima: twelve
  turns, 600 seconds and 1,024 total reads. Existing one-turn limits are additionally
  enforced; remaining read/time budgets are allocated to each turn without reset.
- Every turn makes and reconfirms a fresh plan from that turn's current facts.
  Actions and coordinates from earlier rounds are never replayed.
- Target, input owner, user-activity baseline, encounter, geometry, observation
  identity/time and expected next round remain bound across turns. User takeover
  cannot become the next turn's new baseline. Unknown state, failed scope,
  defeat, unverified input/outcome or cleanup failure stops the encounter.
  Loading is a bounded pause under the existing turn adapter.
- `combat-battle.json` links the admitted parent operation, per-turn execution
  IDs and `turn-NNN/combat-turn.json` evidence. It includes total reads and
  dispatches, the final independently read phase for each turn, cleanup state,
  reason and final battle verdict. The adapter checks bounded turn reports
  against its own callback counts before accepting them.
- `Completed` requires a completed verified turn, at least one action in that
  turn and a fresh, explicit Victory observation from the same encounter.
  A new planning round, disappearing hand, resolving animation or preexisting
  victory screen is insufficient. Quest progress is explicitly unverified;
  loot, item collection and whole-quest completion belong to the outer pilot.
- Exhausting the battle budget after verified turns leaves the battle unknown;
  it does not claim success. Confirmed defeat records failure. Missing release
  or disconnect evidence retains the existing persistent input-recovery gate.

## Source tests and remaining gate

`W101CombatBattleOperationTests` covers fresh multi-turn planning, distinct
proposal identities and evidence folders, one shared lease, parent worker pins,
turn/read/time budgets, changed encounter/geometry/round, cross-turn duplicate or
nonmonotonic observations, user takeover between turns, unknown state, defeat,
preexisting victory, missing hand, cancellation, and observer/input cleanup.
All fixtures are synthetic and call no game or desktop API.

The coordinator owns scoped formatting, analyzers, compilation and native test
execution. A passing source test remains distinct from qualification of real
combat fields, planner metadata, UI controls, victory evidence and quest counts.
