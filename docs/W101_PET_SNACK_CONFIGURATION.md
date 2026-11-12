# Wizard101 pet snack configuration and operation contracts

**Design proposal — 2026-11-08.** This document specifies future behavior; it
does not claim that these settings, action names or UI exist in Maelstrom.
The [disabled settings example](../research/w101-pet-snack-settings.example.json)
is data only, cannot be automatically imported and contains no actual pet or
snack identities. No feeding is enabled by this work.

The [research report](W101_PET_SNACK_RESEARCH_20261108.md) and
[source ledger](../research/w101-pet-snacks-20261108.json) establish the mechanics
and uncertainties. The existing [shared operations](SHARED_OPERATIONS.md) guide
defines the scheduler this proposal must use.

## Requirements versus assumptions

**Requirements** below are proposed Maelstrom behavior: independent options,
stock protection, explicit enablement, measured outcomes and shared input
ownership. They do not depend on a particular game's reward formula.

**Documented baselines:** training snacks follow eligible pet games; Happiness
uses a separate feeding context; ordinary inventory-item feeding is a different,
consuming operation and is excluded. Historical preference bonuses, mastery
skipping and event multipliers are research expectations. Exact current costs,
conversion rates, pet preferences, eligibility and UI controls remain
**unverified assumptions until observed**. Do not hardcode them from this plan.

Example limits such as 60 seconds are proposed application limits, not measured
game requirements. Null means unknown or unconfigured, never zero cost or
unlimited permission. A required unknown prevents live admission. A zero
consumption or spend limit permits no consumption or spending.

## Separate user options

Under **Wizard101 → Pets → Snacks**, the proposed options are:

| Option | Goal | Admission and stop conditions |
| --- | --- | --- |
| Training snacks | Reach a chosen age/XP checkpoint or selected trained-stat targets | Requires a fresh eligible training-feed panel and selected pet. Stops at the target, reserve/budget boundary or newly manifested talent review. |
| Happiness | Refill below a chosen threshold to a chosen target | Requires the distinct Happiness panel. Stops at target or budget/reserve boundary. It never invents a completed training game. |
| Inventory planning | Describe stock, reserves and possible supply needs | Read-only; no purchases, travel, crafting, harvesting or farming actions. |

Training and Happiness have separate allowlists and goals; both are disabled
in the example. Training mastery skip is another opt-in choice. It must never
silently replace a user's Dance practice or a teacher-data collection run.

Per-pet goals are stored under Maelstrom profile keys. A pet name alone is not
a unique identity; bind it to independently verified session evidence. An
unknown in-game ID can remain null if a qualified visual identity method exists.
If the active pet cannot be distinguished reliably, stop at observation.

For training, `age-or-xp` requires an observed age progression and an explicit
age and/or XP target; `stat-targets` requires at least one target within known
caps. XP within an age must not be mistaken for cumulative XP. For Happiness,
choose exactly one absolute or fractional target and a lower trigger threshold.
Unknown caps or conversions cannot be filled with guessed values.

## Settings and inventory identity

The example's `example.pet.*` and `example.snack.*` strings are **Maelstrom-local
configuration keys**, not Wizard101 identifiers or claims that those items exist.
They are intentionally unverified and unusable for consumption.

A snack identity record needs localized name/icon evidence and enough contextual
properties to disambiguate it. School, category, rank and reward fields remain
null until sourced or measured. A stable internal game ID is optional, never
fabricated. Verified identity and verified numeric reward are separate facts.

Apply selection rules in this order:

1. Require the correct pet, supported feeding context and fresh observation.
2. Exclude unknown/unverified items and anything outside the selected mode's
   explicit allowlist.
3. Exclude protected items. Require an integer stock count and ensure
   `count - 1 >= minimum_remaining`.
4. Enforce per-item, per-run and daily limits, then check the selected goal.
5. Rank the remaining candidates using known rewards and the user's priority.
   Show uncertainty or refuse a comparison that requires unknown rewards.

Protection applies across modes and pets. If several profiles reserve the same
physical stock, calculate the union of independent reserved quantities without
double-counting the same reservation; a future reservation record needs an
owner and unique ID. The example provides only a global per-item minimum,
not a complete multi-owner reservation allocator.

No Auction, snack rank or apparent market value is not permission to consume.
A pet-training reserve also constrains Happiness feeding. Unknown inventory
counts cannot satisfy a reserve check.

## Budgets and user control

Use shared, persisted consumption accounting across runs; restarting the app or
switching pets must not reset daily limits. Record timezone and the budget
window on each receipt. Prevent clock rollback or configuration changes from
silently restoring already-used allowance.

The proposed configuration includes total and per-item run/day feed limits,
training Energy spend and reserve, plus elapsed time. Energy spent entering or
skipping a pet game belongs to the parent training workflow; do not charge an
invented Energy cost to the snack click. Parent and child share the same resource
ledger so costs are neither omitted nor counted twice.

Dispatch reserves one consumption against its limits. Confirmed consumption
commits that reservation. An uncertain outcome keeps it reserved until reconciled.
Rejection before dispatch releases it. Daily exhaustion can stop a new job while
allowing evidence collection for an already-dispatched job.

**Skip** declines this opportunity without feeding and records a user-skip
outcome. It does not automatically press Play Again. **Choose another snack**
creates a fresh proposal from qualified allowed items. It cannot bypass
protection, identity checks or budgets. Changing protected stock or an allowlist
is an explicit settings edit followed by a new proposal and admission.

**Stop** cancels queued work, releases any owned input and retains unresolved
receipts. **Resume** requires fresh state and never repeats an uncertain
consumption. Settings are not consent to spend anything indefinitely: global
enablement, the chosen mode, a qualified configuration and an explicit finite
run start are all required. The example fails those gates intentionally.

## Scheduler operations

These action names are **proposed, not registered**. No executable code or
delegate is accepted from settings or a game pack.

| Operation | Reads / returns | Writes to game |
| --- | --- | --- |
| `w101.snacks.observe` | Already-visible panel and pet/inventory evidence → snapshot or unresolved context | No; it must not open menus by itself. |
| `w101.snacks.plan` | Snapshot, goals, reserves and budget → proposal or reasons to skip | No. |
| `w101.snacks.feed-training` | Qualified proposal plus live whole-game handoff → one-item dispatch and receipt | Yes, through the shared owner. |
| `w101.snacks.feed-happiness` | Qualified proposal plus independent request → one-item dispatch and receipt | Yes, through the shared owner. |
| `w101.snacks.verify` | Dispatch record and fresh visible evidence → reconciled outcome | No; menu navigation would require a separate owned action. |

Use the existing `OperationRequest` fields: unique job ID, operation/backend
version, finite timeout, game-write flag, target and immutable input hashes.
Only trusted application code supplies the delegate. Offline plans can reference
recordings; a recording cannot qualify a live target.

The current scheduler rejects duplicate job IDs and marks unfinished jobs
Interrupted on restart. Snack idempotency needs an **additional durable
opportunity/request identity across different job IDs**: a new job cannot
consume again for the same postgame opportunity. This is a required addition,
not an existing guarantee.

No private timer or worker may bypass the shared input owner. Dance releases
keys and its lease before handoff; feeding acquires and rechecks its own lease.
Auto-Idle/navigation cannot run concurrently. Focus loss or user activity
releases owned inputs, pauses, and invalidates proposals. Do not steal focus.
A requested native Idle cleanup waits for an eligible foreground, input-idle
window; an unperformed cleanup is reported, not claimed as successful.

## Postgame handoff contract

A proposed `TrainingFeedOpportunity` carries:

| Field group | Required contents |
| --- | --- |
| Origin | Live versus recorded source; parent routine/job ID; unique whole-game result/opportunity ID; recording/evidence references if relevant. |
| Game result | Whole-game terminal status and supporting evidence. A round boundary, predicted victory or process exit is insufficient. |
| Target and pet | Selected process/session identity, pet-profile key and current identity evidence. |
| Eligibility | Confirmed training-feed panel, observed availability, observation timestamps and current geometry/calibration version. |
| Accounting | Post-game/pre-snack XP/stat/Energy baseline, active bonus evidence, remaining run/day resources, policy/settings version and hashes. |
| Ownership | Confirmation that the parent released held input and its lease; no transfer of stale coordinates. |

A failed game may still expose a feeding opportunity; use the actual verified
panel rather than assuming that only victories qualify. The recorded teacher
can produce a **dry-run opportunity**, but it cannot authorize a live feed.

The child returns a `FeedReceipt` with parent/opportunity identity, mode, pet,
snack, proposal version, dispatch history, before/after evidence, resource deltas,
budget reservation/commit and outcome. Separate game rewards from snack rewards:
the baseline is after game rewards settle and before the snack is dispatched.

Proposed domain outcomes are `confirmed-consumed`, `confirmed-not-consumed`,
`skipped`, `blocked` and `uncertain`. Reward expectation is a separate field:
`matched`, `unexpected` or `unknown`. A quantity drop can confirm consumption
even when capped stats produce no increase; it must not fabricate successful
goal progress. Unexpected or unknown rewards pause subsequent consumption.

Map domain results explicitly to existing scheduler states. A verified normal
consume or skip can be Completed; unknown dispatch must remain Unknown with its
receipt. Cancelled, TimedOut and Interrupted may still have unresolved consumption
and must retain it. Scheduler completion alone never proves a snack was fed.
Play Again is a separate qualified next action.

A blocked proposal is not admitted to the queue. If a running job loses a
precondition before dispatch, it finishes Failed with a precondition reason and
no consumption; a positively verified rejection after dispatch is also Failed.
“Paused” is a user-workflow description, not a new scheduler terminal state.
Re-entry requires a new, freshly validated request and reconciliation of any
previous dispatch. A timeout is never converted into proof of non-consumption.

## Consumption verification

Persist dispatch intent before any consuming click. Recheck target, pet, item,
quantity, reserves and feeding context immediately before dispatch. Record the
chosen item and dispatch timestamp; consume at most one item.

Require fresh before/after quantity observations and resource observations.
`reward_observation_required` means measure the result, **not** require every
stat to increase. Handle animation latency within a bounded observation window;
do not click again just because a screenshot has not changed yet.

If focus is lost before dispatch, nothing is consumed and the proposal expires.
If lost during or after dispatch, retain an uncertain transaction and reconcile
later without another consuming action. Never infer failure from an input API
return code alone. Future screen-confidence thresholds and freshness limits
must come from held-out calibration, not guessed percentages.

## Scenarios for later tests

| Scenario | Expected decision and evidence |
| --- | --- |
| Normal training: a qualified allowed snack has stock 6, reserve 2; a fresh panel and known goal permit one feed | Reserve one, dispatch once, observe stock 5 and actual rewards, then commit and report. Numbers are synthetic fixture values. |
| Happiness below threshold while training feeding is disabled | Only independently enabled Happiness may plan; no game result or training XP is invented. |
| Duplicate postgame notification arrives with a new scheduler job ID | The opportunity identity resolves to the existing transaction; no second click. |
| Inventory is 5 and reserve is 5 | Block consumption and explain the reserve; do not silently choose a protected alternative. |
| Snack identity or OCR quantity is uncertain | Observe-only/unresolved; no item selection or feeding dispatch. |
| Click occurred but result frame is missing | Keep consumption reserved and outcome uncertain; later reconcile stock/resources without automatic retry. |
| Focus leaves before click | Release owned input, expire proposal, record no dispatch; require fresh admission. |
| Focus leaves immediately after click | Treat consumption as uncertain even if the input function reported success. |
| Stat is capped but stock decreased by one | Record actual consumption and zero observed stat gain; evaluate goal/XP independently. |
| User chooses Skip or stops the run | No new consumption; retain any earlier uncertain transaction and do not replay the game automatically. |
| User changes active pet or inventory sorting while paused | Old identity/layout proposal is invalid; re-observe before planning. |
| Program restarts after dispatch intent was persisted | Interrupted/uncertain record is reconciled; history loading does not resume or retry feeding. |

## Staged implementation checklist

- [ ] **SNACK-02:** build read-only panel/pet/inventory recognition with Tiny UI
  fixtures; explicitly reject ordinary-item Feed and ambiguous contexts.
- [ ] **SNACK-03:** implement validated settings and dry-run planner, with null
  handling, reserves, goal boundaries and independent per-mode allowlists.
- [ ] Extend SNACK-03 with persistent run/day accounting and explicit user
  override/skip views; show why a candidate is excluded.
- [ ] **SNACK-04:** implement opportunity identities, durable dispatch intent,
  receipts and scheduler admission; test crash, duplicate, focus and pause cases.
- [ ] Validate recognition and planning on recordings independent of calibration.
- [ ] **SNACK-05:** qualify one explicitly allowed expendable snack in a brief
  live run; verify inventory/resources and native Idle cleanup separately.
- [ ] **SNACK-06:** expose independently enabled training/Happiness routines and
  stock forecasts. Supply acquisition remains a separately qualified operation.

Completed now: data-only design and disabled example. Current costs, actual
inventory identities, runtime behavior and live feeding remain unqualified.

