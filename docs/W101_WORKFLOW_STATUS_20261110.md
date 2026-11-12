# W101 workflow readiness — November 10, 2026

**November 11 update:** the [generic card/deck foundation](JOB_DECK.md) now exists
in native source: collection versus active deck, flip/settings UI, finite run
budgets, filters, conditions, shared scheduler, local API/MCP and objective
auto-fill. This supersedes the planned status for those components below.
Navigation/combat/collection remain unqualified adapters; no broader autoquest
claim follows from parsing an objective. The new source is not installed.

This is a source/code review, not new live qualification. The newest quest/UI
observer source is not installed. Workspaces and helper functions exist, but a
category label, legacy bot, passing simulator or route file does not establish a
working autonomous routine.

| Requested capability | What exists | Remaining connection/qualification |
| --- | --- | --- |
| Reusable routine building | Shared navigation, sigil, menu and Auto-Idle modules; bounded graphs, receipts, replay, scheduler and one input owner. Motion builder saves/loads/reorders/exports drafts. | Full user-facing conditional routine composition and qualified actions. |
| Native Idle between runs | Native-Idle module and Dance postgame operation; optional spin/step/routine modes. UI-tree reader recognizes native Idle. | General between-run policy, confirmed entry/exit and resumption. Never dismiss quest dialogue to force Idle. A universal unattended keep-alive is not qualified. |
| Navigation | Recorded demonstration, route/macro compiler, steering and avoidance/unsticking prototypes, shared arrival checks. | Fresh pose/zone/threat inputs bound to the selected client; real route, arrival and recovery qualification. Historical route coordinates are not a live proof. |
| Quest autopilot | Loading/prompt/dialogue recognition; live objective/dialogue text reading; selected conversation runner completed Quinn's follow-up offer in 12.9 seconds with three verified advances and a new objective. | Broader NPC/branch qualification, quest identity, objective dispatch, resume checkpoints and navigation/combat integration. One conversation is not full auto-questing. |
| Quest selection/discovery | Legacy references and quest-context data foundation. | Verified journal reading, quest selection, prerequisites and bounded quest discovery. |
| Pet games | Pet Dance specialist has supervised PERFECT-game evidence; entry/result/snack/Idle components. | Repeatable full visits, level/geometry variants and remaining pet-game specialists. |
| Other minigames | Conjuration memory/recognition helpers; Potion Motion offline planner; Diego cue/training work and research for the rest. | Independently qualified perception/action/outcome loops for each game. |
| Health/mana, potion use/refill | Legacy controls, planning references and shared resource-gate patterns. | Fresh values and thresholds; verify consumption/refill, then resume the interrupted task. No automatic potion-maintenance loop yet. |
| Pet snacks and energy | Persisted preferences, recommendation math, reserve/attempt gates. | Current pet/inventory/energy observations, verified feeding and restocking. Protected snacks and Crown Shop lockout remain requirements. |
| Reagent collection/gardening | Legacy teachers, interaction/navigation primitives, garden helpers. | Identify item/plant, route safely, interact, verify collection/resource change, handle unavailable targets. |
| Mob/boss farming and auto-battle | Repaired combat arithmetic, offline turn planner and combat graph; legacy farming sources. | Qualified hand/enemy/turn observations, controlled card dispatch, battle outcomes, loot/resource accounting and repeat-run eligibility. Old farming buttons remain blocked pending migration. |
| Bazaar sell/buy/restock | Legacy price/search helpers and item-selection math. | Current inventory/price/currency reads, keep/sell rules, quantity/spending limits, confirmation and verified transactions. Legacy purchasing button remains blocked. |
| Auto-team | Raid/team legacy references. | Explicit roster/roles, coordinated admission, synchronized state, join/leave/recovery. No qualified team controller. |
| Resource-driven routine switching | Sequential operation scheduler and bounded composable routines. | Conditions, priority/interrupt policy, saved task checkpoints, cooldowns and resumable maintenance. No integrated inventory-full → sell → buy → resume loop. |
| Workspace UI and settings | Dark-themed main screen, Observe workspace, W101 routines/Auto-Idle, dedicated Dodge workspace, Workshop, experiments, settings and pet preferences. | Unified routine-card canvas, condition editor, inspector, capability readiness badges and consistent run/status panels. Existing motion list is not the finished card/timeline editor. |

## The next product layer

### Dealer, deck and job cards

The owner proposed this interaction model after the Quinn pilot. It is an
architecture direction, not a delivered API/card editor:

- **Dealer:** the user authors/approves the deck, run counts, priorities, allowed
  operations and resource limits. Results return to that user-owned plan; the
  assistant may propose changes but cannot invent or authorize new cards.
- **Card:** a versioned reference to an existing registered routine, with entry
  conditions, target/workspace, resource requirements/reserves, parameters,
  completion evidence, timeout, attempt limit and interruption/resume policy.
  A card is data, not executable code or new permission.
- **Deck:** the finite work allocation. Multiple copies or a remaining-run count
  express repetition. Successful verified completion consumes a run; a deferred
  card consumes none. Failed attempts have their own bounded budget so a broken
  card cannot retry forever. Priority and run count are separate concepts.
- **Eligibility:** fresh known facts determine Ready vs Waiting. Insufficient
  pet energy defers Pet Dance while another permitted card may run. Unknown or
  stale energy is not zero and is not permission to spend. Reevaluate on relevant
  state changes, with bounded cadence/cooldowns, not an unbounded polling loop.
- **Execution:** the existing scheduler and input owner execute one admitted
  contract, preserve its receipts/outcomes, and yield at a qualified checkpoint.
  A waiting card does not reserve input ownership. No eligible work can select a
  separately qualified Idle card only when its scene prerequisites are met.
- **Maintenance:** selling, buying, potions or restocking must themselves be
  approved cards with protected-item rules, spending limits and verified
  transactions. An unmet prerequisite does not implicitly authorize a purchase.

Maelstrom should normalize observations into a versioned state snapshot (target,
zone/scene, health/mana/energy, inventory, objectives, source, sample time and
freshness). Its registry advertises supported/qualified routine versions and
missing prerequisites. This keeps transformation, data and execution inside
the application.

Provide a local application API over these existing services. Suggested read
operations are state, capabilities, deck status and run evidence; mutation
operations are validate/simulate deck, submit an approved deck, pause and cancel.
MCP is a thin adapter to that API, not a second scheduler or direct keyboard/
memory executor. GUI, CLI and MCP must reach the same active application owner;
starting another CLI scheduler while the GUI holds ownership is not an IPC
implementation. Local authenticated IPC is the initial target, not an internet
listener. Event/revision-based status reduces repeated expensive observation.

The existing `tools/maelstrom_mcp_server.py` offers workspace discovery, policy
inspection, routine validation and offline planning/compilation tools. It does
**not** yet provide this live state service or admitted deck execution. The new
quest CLI is a bounded executable operation, not an always-running API.

Approved decks execute locally without an LLM call per card/action. Return
results, exhausted quotas, unresolved conditions or requested policy changes to
the Dealer. Do not reinterpret this as asking the user to approve each arrow,
menu page or already-authorized repeated operation.

First API/card increment: expose the proven selected-conversation operation and
its evidence through the same registry/owner; add simulated condition/deferral
tests, then native-Idle and resource-maintenance cards as they qualify. Keep the
card editor and MCP schemas aligned with the existing module/pack contracts.

Use the existing scheduler as the execution spine. Add a small policy layer that
selects already-qualified routines from fresh game facts; it must not become a
second input controller. Each routine card needs entry conditions, required
resources, completion evidence, interruption rules and a resume checkpoint.

The target user workflow is: choose W101 → select a goal/routine → configure
allowed maintenance → preview the policy → start → inspect the current task and
why it changed. Examples of policy, not implemented promises:

- Inventory threshold reached → pause at a safe checkpoint → travel to Bazaar →
  sell only eligible items → verify inventory/currency → resume the selected quest.
- Health below the configured threshold before combat → use an allowed potion →
  verify recovery. If none remain, run a qualified refill route or stop with an
  explicit unmet prerequisite.
- Enough pet energy and an eligible snack policy → run the selected pet routine;
  stop before the energy reserve or protected inventory would be crossed.
- No eligible work → enter verified native Idle if the current scene permits it.
  Recheck state on leaving Idle; do not continue from a stale screen or interrupt
  an unresolved dialogue/transaction.

## Delivery order

1. Preserve the completed Quinn pilot and expose selected-conversation settings
   through the workspace; broaden coverage using bounded, explicit tasks.
2. Qualify shared native-Idle entry/exit and one navigation route through the same
   owner, with verified loading/arrival and takeover handling.
3. Add a resource snapshot and condition evaluator (unknown is distinct from
   zero), then test switching/resumption offline before enabling transactions.
4. Expose those qualified parts as routine cards and settings inside the W101
   workspace, including clear readiness and missing-prerequisite indicators.
5. Integrate maintenance, battle/farming and additional games incrementally.

Current source evidence: `W101RoutineBuilder.cs`, `Utilities/Modules/`,
`Utilities/OperationScheduler.cs`, `PetDanceNativeIdle.cs`,
`PetTrainingPreferences.cs`, `MaelstromHomeForm.cs`, `DodgeWorkspaceForm.cs`,
`tools/steered_walker.py`, `tools/path_navigator.py`, `tools/combat_turn_planner.py`
and the reports under `docs/coordination/results/`. The central board contains
some older next-task descriptions; this review distinguishes later offline
contributions from live player capability.
