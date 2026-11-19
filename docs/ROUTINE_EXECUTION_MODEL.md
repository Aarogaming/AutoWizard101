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

---

## Hierarchical Behavior Trees for State Orchestration & Interruption Handling

Flat `if/else` state machines and simple state transition graphs break down rapidly when an autonomous agent encounters nested, asynchronous interruptions (e.g. *Questing along a route $\to$ Unexpectedly pulled into combat $\to$ Health/Mana depleted $\to$ Need Potion / Refill $\to$ Kinematic snag on world geometry $\to$ Resume original quest*). 

To resolve this, Maelstrom embeds a native, deterministic **Behavior Tree (BT)** engine (`crates/maelstrom-core/src/behavior_tree.rs`).

### 1. Priority-Based Preemption

Behavior Trees evaluate execution hierarchies from top to bottom on every decision tick against a shared, typed `Blackboard` of verified facts:

```
[Priority Selector: Root]
├── [Branch 1: Combat Interruption] (Highest Priority)
│    ├── Condition: in_combat == true
│    └── Subtree: CombatRoutine (ObserveTurn -> SelectSpell -> Cast -> AwaitResult)
├── [Branch 2: Vital Recovery]
│    ├── Sequence: PotionRecovery
│    │    ├── Condition: mana_pct < 0.20 || health_pct < 0.25
│    │    ├── Condition: has_potion == true
│    │    └── Action: DrinkPotion (Emits potion proposal, verifies resource gain)
├── [Branch 3: Kinematic Stuck Recovery]
│    ├── Sequence: UnstuckManeuver
│    │    ├── Condition: is_stuck == true (odometry watchdog: Δpos < ε over >1.5s)
│    │    └── Action: ExecuteUnstuckPulse (Reverse pulse 0.5s -> Yaw rotate 45° -> Clear latch)
├── [Branch 4: Quest Navigation]
│    ├── Sequence: NavigateToObjective
│    │    ├── Condition: has_active_goal == true
│    │    └── Action: FollowNavMeshPath (Dynamic Dijkstra over Nav2 Costmap)
└── [Branch 5: Idle Fallback] (Lowest Priority)
     └── Action: YieldIdle (Safe resting state)
```

Because a Priority `Selector` evaluates its children strictly in priority order from left to right on every tick:
1. If an agent is navigating (`Branch 4: Running`) and suddenly encounters an aggressive mob (`in_combat` flips to `true`), the next tick immediately evaluates `Branch 1` to `Running` / `Success`.
2. The `Selector` automatically calls `reset()` on the interrupted `NavigateToObjective` branch, clearing running action latches without requiring complex manual interrupt flags.
3. Once combat concludes (`in_combat` flips to `false`), the root selector falls through to `Branch 2`, `Branch 3`, and back to `Branch 4`, cleanly resuming quest progression.

### 2. Node Composition & Lifecycle

- **`BtStatus`**: Every node returns one of three discrete execution states:
  - `Success`: The node achieved its goal.
  - `Failure`: The node could not achieve its goal or its precondition was not met.
  - `Running`: The node has submitted an asynchronous action proposal and is awaiting completion evidence.
- **Composites**:
  - `Selector` (Fallback): Executes children in order; returns `Success` or `Running` on the first non-failure; returns `Failure` if all children fail.
  - `Sequence`: Executes children in order; returns `Failure` or `Running` on the first non-success; returns `Success` only if all children succeed.
  - `Parallel`: Evaluates multiple children concurrently with configurable success/failure thresholds.
- **Decorators**:
  - `Inverter`: Flips `Success` $\leftrightarrow$ `Failure`; passes `Running` through.
  - `Retry`: Re-executes a child node up to $N$ times upon `Failure`.
  - `ForceSuccess` / `ForceFailure`: Normalizes terminal status.
- **Leaves**:
  - `ConditionNode`: Read-only predicates querying blackboard facts.
  - `ActionNode`: Emits structured task proposals onto the blackboard intent channel.

### 3. Separation of Concerns: Facts ≠ Authority ≠ Actions

The Behavior Tree adheres strictly to Maelstrom's safety invariants:
1. **Blackboard**: Contains only *Facts* derived from observation adapters (e.g., entity positions, mana percentage, combat state, odometry deltas).
2. **Behavior Tree**: Purely evaluates logic and selects an *Intent* (e.g., `Proposal::DrinkPotion`, `Proposal::FollowWaypoints`). It has zero direct input authority.
3. **Scheduler & Lease**: Translates intents into bounded physical actions only after verifying lease ownership, target window identity, countdowns, and user takeover flags.

### 4. Kinematic Motion & Biomechanical Input Profiles

When an Action Node proposes a physical interaction (such as mouse clicks on combat spell cards or keyboard navigation pulses):
1. **Mouse Trajectories**: Rather than teleporting or moving along straight lines, mouse actions pass through the kinematic trajectory planner (`kinematics.rs`). The planner applies WindMouse physical simulation (gravity, damping, stochastic wind drift) or cubic Bézier minimum-jerk curves with Fitts's law acquisition timing. See [Kinematic input profiles](KINEMATIC_INPUT_PROFILES.md).
2. **Temporal Jitter**: Keypresses and mouse click hold times are modulated by log-normal distributions ($\mu = \ln(75\text{ms})$, $\sigma = 0.22$), eliminating robotic isochronous timing.
3. **Dispatch Pipeline**: The resulting `KinematicPlan` provides timestamped coordinate waypoints to the platform actuator under the verified input lease.

### 5. Host IPC Bridge (`MaelstromAutonomyBridge`)

The perception-decision loop is orchestrated across the named pipe bridge (`\\.\pipe\maelstrom-deck-<HASH>`):
1. `MaelstromNativeBridge` retrieves `snapshot` observations from `JobDeckHost.cs`.
2. `NativeHostState` projects verified facts into `W101Blackboard`.
3. The Behavior Tree ticks and selects an action intent.
4. Local controllers (Pure Pursuit steering and kinematic trajectory generators) compute parameterized action structures.
5. The resulting `AutonomyCycleResult` is returned to the host for single-owner actuation under verified input leases.

### 6. Goal-Oriented Action Planning (GOAP)

For long-horizon quest prerequisite chains, Maelstrom employs a two-tier autonomy architecture:
1. **Tier 1 (GOAP Macro Planning)**: An $A^*$ state-space search engine (`goap.rs`) plans the optimal sequence of actions satisfying goal preconditions (e.g. traveling between zones, refilling potions, acquiring quest items). See [Goal-Oriented Action Planning](GOAP_ACTION_PLANNING.md).
2. **Tier 2 (Behavior Tree Tactical Execution)**: Executes the current GOAP action while reactively arbitrating instantaneous events (combat encounters, vital dips, physical snags). Dynamic replanning is triggered if state deviations invalidate active plan preconditions.

### 7. Wizard City Starter & Unicorn Way Quest Domain Repertoire

The concrete action repertoire for Wizard City starter questlines (*"The Lore of Magic"*, *"Enrollment"*, *"A Good Impression"*), the subsequent Unicorn Way combat progression (*"Finding Out"*, Private O'Ryan, Lost Soul & Skeletal Pirate combat, Private Stillson, Lady Oriel), topological zone transitions, NPC interaction bounding geometry, and potion fountain checkpoints is grounded in `crates/maelstrom-core/src/quest_domain.rs`. See [W101 Quest Repertoire](W101_QUEST_REPERTOIRE.md) and [W101 Unicorn Way Quest Domain](W101_QUEST_UNICORN_WAY.md).

### 8. Offline Autonomy Replay Harness

To qualify the two-tier autonomy stack against multi-hour recorded game session traces without live client execution, Maelstrom provides an offline replay harness (`crates/maelstrom-fallback/src/autonomy_replay.rs`). The harness measures decision accuracy ($\ge 99.5\%$), replanning frequency, trajectory curvature smoothness, and kinematic minimum-jerk compliance. See [Offline Autonomy Replay Harness](OFFLINE_AUTONOMY_REPLAY.md).

### 9. Desktop Input Actuator Under Verified Lease

Actuation of parameterized autonomy proposals into physical Windows input events is performed by `DesktopInputActuator.cs`. Dispatches execute strictly under `GameInputLease.Current` with continuous mid-pulse monitoring, foreground focus checks, pre-resume countdown enforcement, and verified key/mouse release. See [Desktop Input Actuator](DESKTOP_INPUT_ACTUATOR.md).

### 10. Combat Dueling Loop Integration

The end-to-end combat dueling loop integrates Behavior Tree combat preemption (`Branch 1: CombatBranch`), `CombatGeometry` coordinate transformation and 1280x720 viewport scaling, card and target selection, pass fallback, and `DesktopInputActuator` kinematic click execution. See [Combat Dueling Loop](COMBAT_DUELING_LOOP.md).




