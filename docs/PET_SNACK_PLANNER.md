# Read-only pet snack planner

Implemented as a small pure function in
[PetSnackPlanner.cs](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetSnackPlanner.cs).
It returns a suggestion for **one snack**, or a reason to take no action.
It does not capture a screen, identify inventory, feed a pet, press keys, open
files, invoke a model or submit scheduler jobs.

This is the first calculation component of the
[snack configuration proposal](W101_PET_SNACK_CONFIGURATION.md), not its full
runtime implementation. The settings example remains disabled and is not
deserialized by this planner. See the [research report](W101_PET_SNACK_RESEARCH_20261108.md)
for historical mechanics and unqualified current-client details.

## Interface

`PetSnackPlanner.Plan(PetSnackMode mode, PetSnackSnapshot snapshot,
PetSnackPolicy policy)` returns a `PetSnackPlan`.

| Input | Meaning |
| --- | --- |
| Mode | Training XP or Happiness; these never borrow benefits from one another. |
| Policy | Explicit expected pet profile, independently enabled modes, allowed snack profile keys and optional user skip. Both modes default disabled. |
| Snapshot | Caller-supplied verified context, pet, opportunity, freshness/readiness, whole-game completion where needed, prior/uncertain consumption flags, progress, inventory and remaining budget. |
| Progress | Observed current value, configured target and known cap for the selected mode, expressed in consistent units. |
| Budget | Remaining permitted feeds, available resource units, reserved units and the observed cost of the proposed operation. |
| Candidate | A Maelstrom profile key, verified identity, protection, observed stock, stock reserve, and separately supplied XP/Happiness benefits. |

Profile keys are application identities; this component invents no in-game item
IDs. Boolean evidence fields are supplied by the caller and are not independent
proof that a screen or pet has been verified. The implementation supports
synthetic and recorded dry-run data; a successful proposal does not qualify
those inputs for live use.

All counts, progress, benefits and resource units are integer values. Null is
unknown, not zero. Negative values are rejected. Callers must use one consistent
resource unit and supply explicit zero cost where zero was actually established.
Already-spent training Energy must not be charged again as a snack-click cost.
There is no hardcoded Energy, preference, rank, event or Happiness formula.

Training XP must refer to one observed age/XP range. This component does not
normalize age rollovers, estimate age targets or optimize individual stat
targets. Happiness cap and target are supplied separately. Required unknown
goal/cap/budget values produce no suggestion.

## Admission and selection

Before examining candidates, the planner checks:

- The requested mode is enabled and the user has not skipped.
- Feeding context is verified and matches the mode; ordinary inventory-item
  feeding is rejected.
- The verified pet matches the explicitly selected profile.
- A verified opportunity identity exists. Training additionally requires a
  whole-game completion, not a Dance round boundary. Happiness is independent.
- The opportunity is neither already consumed nor awaiting reconciliation.
- Observations are fresh, the interaction context is ready, the goal is
  incomplete and not capped, and the remaining budget permits one feed.

It then excludes unverified or duplicate identities, items outside the explicit
allowlist, protected stock, unknown quantities/reserves, items whose remaining
quantity would cross their reserve, and unknown/nonpositive benefits.

For the remaining candidates it chooses:

1. Greatest progress toward the selected target, limited to the remaining goal.
2. Least benefit beyond that target.
3. Ordinal profile-key order, making equal proposals independent of list order.

This is a deterministic selection policy, not a game reward formula. A benefit
is an input expectation; the result does not claim that the game produced it.
Rank, item rarity and school provide no implicit permission or benefit.

The result contains the selected mode, pet, opportunity, snack profile, quantity
one, expected/useful benefit and overshoot; it also includes a selection reason
and per-candidate rejection reasons. With no eligible candidate, suggestion is
null. Global failures return a reason without inspecting the inventory.
Missing snapshot/policy arguments are programming errors and throw
`ArgumentNullException`.

## Boundaries for integration

The caller owns evidence freshness, focus/activity detection, qualified screen
recognition, stock identity, active bonuses, user settings and budget aggregation.
`InteractionReady` is an admission fact supplied to this read-only function;
the planner does not acquire an input lease or activate a window.

The budget must already reflect the applicable run/day/per-item allowances
before calling. This implementation neither persists counters nor reserves a
consumption. It accepts one aggregate remaining feed allowance, not the complete
multi-pet, per-item daily accounting described in the proposal.

Calling twice with unchanged input deliberately returns the same proposal.
Opportunity identity and consumed/uncertain flags help express a guard, but
persistent idempotency across jobs/restarts still needs the transaction layer.
No caller should interpret a proposal as permission to bypass shared ownership,
fresh admission or before/after consumption verification.

Future runtime integration must reobserve after acquiring the shared owner,
persist intent before dispatch, and reconcile uncertain results without automatic
consumption retries. Native Idle cleanup and Play Again remain separate actions.
None of those actions is implemented by this component.

## Verification

[PetSnackPlannerTests.cs](../ProjectMaelstrom/ProjectMaelstrom.Tests/PetSnackPlannerTests.cs)
contains deterministic unit cases for disabled modes, wrong context/pet,
opportunity boundaries, unknown consumption, focus/freshness, user skip,
unknown/completed/capped goals, resource budgets, stock protections, missing
benefits, mode separation, selection order, duplicate identities and immutable
input behavior.

Fixtures are synthetic. They do not establish real item inventories, costs,
current game mechanics, OCR accuracy or live feeding behavior. Build/test
execution is coordinated by the main project workstream so concurrent workers
do not compete for the same outputs.

