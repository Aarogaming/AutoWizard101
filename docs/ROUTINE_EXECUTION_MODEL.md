# Maelstrom cards, routines and decks

Design direction accepted from the owner's 2026-11-11 architecture proposal.
This defines how the existing system should compose; it does not claim that all
example routines are implemented. The [roadmap](ROADMAP.md) and
[capability audit](ARCHITECTURE_AUDIT_20261111.md) track delivery.

Maelstrom turns game knowledge and observed play into reusable actions. Users
choose a game workspace, configure cards and place them in a Routine Deck.
The deck decides what can run; each routine decides its next action from fresh
game information. Every result is checked before further work depends on it.

## Three levels of execution

### 1. Action Cards

An Action Card performs one bounded operation: interact with an object, select a
menu option, advance one dialogue page or use an allowed consumable.

It checks its requirements, performs the action and waits for the expected result.
Sending a key or accepting a click is not completion. For example, an interaction
needs the intended response; a potion needs verified resource consumption and the
expected recovery. A missing object alone is not enough to prove it was collected.

Each card specifies when it can be used again: another explicit request, a new
target, a changed condition or a configured repeat. A cooldown alone must not
consume another potion because the same stale low-health reading remains visible.

### 2. Routine Cards

A Routine Card handles a task with several decisions and actions: finish a
conversation, play one minigame, complete one encounter or follow a route.

It continually observes, chooses an allowed action and checks the outcome. It
does not replay the same sequence regardless of what appears. The routine has a
goal, completion evidence, time/action/attempt limits and a cleanup procedure.
Those limits apply to every nested step as well as the whole task.

Examples:

- **Battle:** observe the current turn, choose a supported spell and target,
  verify selection, wait for the result and observe again. Combat ending alone
  does not distinguish victory, defeat, fleeing or lost observation.
- **Minigame:** recognize the board or cue sequence, choose the next move and
  verify progress. Use the detected board and actual resources, not fixed assumed
  dimensions or bottle counts.
- **Navigation:** establish location, choose a bounded route segment, move and
  verify progress. Arrival needs its own evidence; a quest arrow is only a hint.
- **Farming:** compose entry, encounter, collection and exit. Stop at the chosen
  run/resource/time limit; nested routines share the overall budget.

### 3. The Routine Deck

The Routine Deck coordinates the session. It evaluates enabled cards against
fresh facts, priorities, resource policies, cooldowns and run limits. It starts
one eligible routine through the shared scheduler and input owner.

Its decision cycle is:

1. Check stop requests, user takeover, target loss and recovery requirements.
2. Obtain the facts needed by the active or eligible cards.
3. Continue the current routine or request a controlled interruption.
4. When control is available, select an eligible card according to configured
   priority and a stable tie-breaking rule.
5. Record the outcome, update limits/resources from evidence and evaluate again.

When no card can run, the deck explains why it is waiting. Optional native Idle
is an explicit between-run policy with its own entry and exit checks. Unknown
screens, open quest dialogue, an active battle or failed cleanup must not trigger
blind Idle menu actions.

The deck can run once, for a set number of tasks or until a chosen time/resource
limit. Long sessions are a later qualification target, not an unlimited default.

## A common card contract

Every card exposes the same user-facing information:

| Part | Meaning |
| --- | --- |
| Identity | Stable card/routine ID, game, version and supported method. |
| Configuration | Targets, allowed areas, exclusions, consumable choices and preferences. |
| Requirements | Needed facts, their freshness, required adapters and explicit permissions. |
| Limits | Priority, runs, attempts, time, cooldown and resource reserves. |
| Actions | Registered operations with bounded parameters; no arbitrary executable code from packs. |
| Completion | Independent evidence that the intended result occurred. |
| Interruption | Where the routine can pause, cancel and release control safely. |
| Recovery | What to preserve/check after uncertainty, and what must block further play. |
| Record | Inputs, decisions, reasons, dispatched actions, observed outcomes and costs. |

Readiness is **ready, waiting, unsupported or blocked**, with a reason. A missing
fact is not false, and an unknown outcome is not success. Player-facing states can
include Ready, Running, Waiting for result, Paused, Completed and Needs attention.

Action Cards and Routine Cards use the same admission and result contract.
The Routine Deck is their coordinator, not a third competing execution engine.
Implement these concepts by extending the existing `JobCardSpec`,
`JobRoutineInfo`, `PreparedDeckJob`, `RoutineGraph` and scheduler where needed.
Do not add an independent `IJobCard.ExecuteAsync` controller beside them.

## Interrupts, loading and ownership

Urgent requests ask the active routine to cancel or reach an allowed checkpoint.
The next card may run only after input is released and cleanup is verified.
No priority or override card bypasses target checks, resource policies or the
single input owner. If cleanup is uncertain, gameplay remains blocked.

Loading is a recognized temporary state. It pauses relevant progression while
retaining a separate loading/wall-clock deadline. After loading, recheck target,
location, geometry and prerequisites. Do not reuse the pre-load screen or repeat
an action whose outcome is still unknown.

Observation frequency is chosen by the task. Fast minigame reactions may need
frequent lightweight cues; inventory or knowledge changes do not need the same
rate. Share observations, bound queues, discard obsolete live frames and suspend
unused producers. A universal 100–250 ms poll is not a requirement.

## Layers that remain inspectable

`game adapter → observation → typed facts → card decision → bounded action →
shared owner → observed outcome → run evidence`

Each transformation has explicit input/output versions, limits and failure
states. UI, CLI and MCP use the same contracts. A model may help research, label
or train a specialist offline; ordinary playback should use the smallest rules
or specialist that meets measured requirements.

Updates preserve original packs and prior evidence, check supported contract
versions and replay saved cases before promotion. Unsupported combinations are
reported rather than silently converted. This reduces coupled breakage; it does
not make arbitrary changes risk-free.

## Example future session

An optional sign-in workflow establishes the selected character and workspace.
The deck then considers configured maintenance and gameplay cards:

- Garden when relevant plant needs, Energy and permitted actions are known.
- Refill resources using an approved method when actual reserves are low.
- Visit inventory maintenance when observed free capacity reaches the chosen
  threshold, respecting protected items and spending/selling policies.
- Quest using qualified navigation, interaction and battle routines.
- Enter native Idle between runs when enabled and verified appropriate.

These are conditional possibilities, not a fixed itinerary. The same framework
can coordinate Dodge, other minigames or another game's routines with different
facts, bindings and goals. W101's current limitations remain explicit: only saved
replay and selected conversations are connected deck adapters at this checkpoint.

The benefit is composability and traceable behavior. Timeouts and state checks
reduce stalled or mismatched actions; they do not guarantee perfect gameplay,
instant cancellation of broken dependencies or unattended 24/7 operation.
