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

> **Status (audit 2026-11-20, Claude):** the engine in `behavior_tree.rs` is real and tested offline. The tree below is the one the code builds, but
> several leaves only set a string, and only the offline autonomy bridge ticks it. Notes marked **Today** say what the code does.
> Evidence: [W101-BEHAVIOR-TREE-02](coordination/results/W101-BEHAVIOR-TREE-02.md) and
> [W101-BEHAVIOR-DOCS-01](coordination/results/W101-BEHAVIOR-DOCS-01.md), audit notes.

Flat `if/else` state machines and simple state transition graphs break down rapidly when an autonomous agent encounters nested, asynchronous interruptions (e.g. *Questing along a route $\to$ Unexpectedly pulled into combat $\to$ Health/Mana depleted $\to$ Need Potion / Refill $\to$ Kinematic snag on world geometry $\to$ Resume original quest*). 

To resolve this, Maelstrom embeds a native, deterministic **Behavior Tree (BT)** engine (`crates/maelstrom-core/src/behavior_tree.rs`).

### 1. Priority-Based Preemption

Behavior Trees evaluate execution hierarchies from top to bottom on every decision tick against a shared, typed `Blackboard` of verified facts:

```
[Priority Selector: Root]   (W101RootSelector, built by build_w101_canonical_tree)
├── [Branch 1: Combat Interruption] (Highest Priority; CombatBranch)
│    ├── Condition: in_combat == true
│    └── Action: ExecuteCombatTurn (sets PROPOSAL:CombatTurn[:<combat_turn_action>], returns Running on every tick)
├── [Branch 2: Vital Recovery]  (PotionRecoveryBranch)
│    ├── Condition: mana_pct < 0.20 || health_pct < 0.25
│    ├── Condition: has_potion == true
│    └── Action: DrinkPotionAction (sets PROPOSAL:DrinkPotion, then writes has_potion = false and mana/health = 1.0; returns Success)
├── [Branch 3: Kinematic Stuck Recovery]  (StuckRecoveryBranch)
│    ├── Condition: is_stuck == true
│    └── Action: ExecuteUnstuckPulse (sets PROPOSAL:UnstuckPulse and clears is_stuck; returns Success)
├── [Branch 4: Opportunistic Detour]  (OpportunisticDetourBranch)
│    ├── Condition: active_detour is set and approved
│    └── Action: ExecuteDetourHarvest (within 3.5 of the target: PROPOSAL:Interact:Detour:<id> and Success; else PROPOSAL:SteerDetour:<x>,<y> and Running)
├── [Branch 5: Quest Navigation]  (QuestNavigationBranch)
│    ├── Condition: active_goal is set
│    └── Action: FollowNavMeshPath (PROPOSAL:StepWaypoint and Running while active_waypoints is non-empty; else PROPOSAL:Arrival, clears active_goal, Success)
└── [Branch 6: Idle Fallback] (Lowest Priority)
     └── Action: YieldIdleAction (sets PROPOSAL:YieldIdle, Success)
```

Because a Priority `Selector` evaluates its children strictly in priority order from left to right on every tick:
1. If an agent is navigating (`Branch 5: Running`) and suddenly encounters an aggressive mob (`in_combat` flips to `true`), the next tick immediately evaluates `Branch 1` to `Running` / `Success`.
2. The `Selector` calls `reset()` on the previously running child when a *different* child returns `Running` or `Success` (here the
   interrupted `QuestNavigationBranch`), clearing a `Sequence`'s running index without manual interrupt flags. `ActionNode` and
   `ConditionNode` hold no state to reset.
3. Once combat concludes (`in_combat` flips to `false`), the root selector falls through the lower branches and back to
   `Branch 5`. The tree keeps no memory of the interrupted path: it resumes only while `active_goal` and `active_waypoints` are still set.

**Today (audit 2026-11-20, Claude):** checked by reading the code and by a throwaway release probe (not committed).
- The tree has six branches, not five. Branch 1 has no `CombatRoutine` subtree (see Section 10 and [COMBAT_DUELING_LOOP.md](COMBAT_DUELING_LOOP.md)).
  `DrinkPotion` verifies no gain: it assumes the potion worked and writes the restored values into the blackboard, and the bridge overwrites
  them from the next host snapshot, so while the host still reports low mana and a held potion the proposal repeats every cycle (probe: 3 of 3).
- `is_stuck` is set only by tests. `OdometryWatchdog` (with the reverse pulse and the 45 degree yaw in `step_unstuck_maneuver`) is in
  `steering.rs` and has no caller, and `ExecuteUnstuckPulse` only emits the intent and clears the flag.
- `FollowNavMeshPath` runs no path search and nothing in the tree removes waypoints: waypoints come from the caller (the offline replay
  sets them from the session) and the node returns `Running` for as long as the list is non-empty. `find_safest_path` has no caller.
- Of the intents the tree emits, `MaelstromAutonomyBridge::process_state` computes something for only `StepWaypoint` (a steering summary)
  and `CombatTurn[:...]` (a click-plan summary). `DrinkPotion`, `UnstuckPulse`, `SteerDetour`, `Interact:Detour`, `Arrival` and `YieldIdle`
  appear only as the `proposed_intent` string. The bridge also accepts `FollowWaypoints`, which the tree never emits.
- `W101Blackboard::status_log` gets an entry on every navigation tick (`Stepping waypoint`) and every combat tick (`Combat turn proposed`),
  nothing in the tree or the bridge clears it, and every `AutonomyCycleResult` clones the whole log (probe: 1,000 ticks left 1,000 entries).
  Over a long session that grows without bound and makes each result larger than the last. Not changed in this pass: who clears it, and
  whether a result should carry only the new entries, is a design decision.
- Tick cost was roughly 40-60 ns for the canonical tree in a one-off release probe (tree only, no I/O, `status_log` cleared each tick; not a
  committed benchmark). "Sub-millisecond" therefore holds for the tree itself and says nothing about the observation loop around it.
- The only caller of the tree is `MaelstromAutonomyBridge::process_state` (via the snapshot-polling wrapper and the offline replay
  harness); nothing sends its output to the host (Section 5).

### 2. Node Composition & Lifecycle

- **`BtStatus`**: Every node returns one of three discrete execution states:
  - `Success`: The node achieved its goal.
  - `Failure`: The node could not achieve its goal or its precondition was not met.
  - `Running`: The node has submitted an asynchronous action proposal and is awaiting completion evidence.
- **Composites**:
  - `Selector` (Fallback): Executes children in order; returns `Success` or `Running` on the first non-failure; returns `Failure` if all children fail.
  - `Sequence`: Executes children in order; returns `Failure` or `Running` on the first non-success; returns `Success` only if all children succeed.
  - `Parallel`: Ticks every child, in order, on every tick (not concurrently, and a child that already succeeded is ticked again). Two
    policies only: `RequireAll` (fails as soon as any child fails, runs while any child runs) and `RequireOne` (succeeds as soon as any
    child succeeds, fails only when all fail). There are no numeric thresholds.
- **Decorators**:
  - `Inverter`: Flips `Success` $\leftrightarrow$ `Failure`; passes `Running` through.
  - `Retry`: Re-executes a child node up to $N$ times upon `Failure`. It returns `Running`, not `Failure`, between failed attempts (the
    child is reset), so a parent `Selector` treats the child as in flight until the last attempt fails (probe: `Retry(3)` over an
    always-failing child returned Running, Running, Failure).
  - `ForceSuccess`: Converts `Failure` into `Success`; passes `Running` through. There is no `ForceFailure`.
- **Leaves**:
  - `ConditionNode`: Read-only predicates querying blackboard facts.
  - `ActionNode`: Emits structured task proposals onto the blackboard intent channel.

### 3. Separation of Concerns: Facts ≠ Authority ≠ Actions

The Behavior Tree adheres strictly to Maelstrom's safety invariants:
1. **Blackboard**: Holds *Facts* derived from observation adapters (e.g., entity positions, mana percentage, combat state). **Today** it
   also holds the emitted proposal (`proposal_channel`), `combat_turn_action` and `status_log`, and nodes write facts back (`DrinkPotion`
   sets `has_potion`, `mana_pct` and `health_pct`; `FollowNavMeshPath` clears `active_goal`; the detour branch clears `active_detour`).
2. **Behavior Tree**: Purely evaluates logic and selects an *Intent* (e.g., `Proposal::DrinkPotion`, `Proposal::FollowWaypoints`). It has zero direct input authority.
3. **Scheduler & Lease**: Translates intents into bounded physical actions only after verifying lease ownership, target window identity, countdowns, and user takeover flags.

### 4. Kinematic Motion & Biomechanical Input Profiles

When an Action Node proposes a physical interaction (such as mouse clicks on combat spell cards or keyboard navigation pulses):
1. **Mouse Trajectories (design intent, partly implemented)**: the kinematic planner (`kinematics.rs`) can generate WindMouse or cubic Bézier minimum-jerk paths with Fitts's law timing. In code today only the Bézier generator, Fitts timing and Gaussian target scatter are called (by `maelstrom_bridge.rs`); WindMouse has no caller. See [Kinematic input profiles](KINEMATIC_INPUT_PROFILES.md).
2. **Temporal Jitter (mouse click hold only)**: the click hold time in the plan is sampled from a log-normal distribution ($\mu = \ln(75\text{ms})$, $\sigma = 0.22$, clamped to 40-180 ms). Key-press dwell and cadence profiles exist but have no caller, and steering pulses are not jittered.
3. **Dispatch Pipeline**: the plan is reduced to a `KinematicPlanSummary` (point count, duration, dwell, final target); the waypoints are not forwarded. `DesktopInputActuator.DispatchKinematicClickAsync` presses at the target, holds for the dwell and releases, under the verified input lease. See the audit note in `coordination/results/W101-KINEMATICS-DOCS-01.md`.

### 5. Host IPC Bridge (`MaelstromAutonomyBridge`)

> **Status (audit 2026-11-19, Claude):** design intent. The bridge is a read-only snapshot client with an offline decision
> step; nothing returns its result to the host. See [Backend control plane, Section 5](BACKEND_CONTROL_PLANE.md) and
> [W101-IPC-CORE-02, Audit note](coordination/results/W101-IPC-CORE-02.md).

The perception-decision loop is orchestrated across the named pipe bridge (`\\.\pipe\maelstrom-deck-<HASH>`):
1. `MaelstromNativeBridge` retrieves `snapshot` observations from `JobDeckHost.cs`.
2. `NativeHostState` projects verified facts into `W101Blackboard`.
3. The Behavior Tree ticks and selects an action intent.
4. Local controllers (Pure Pursuit steering and kinematic trajectory generators) compute parameterized action structures.
5. The resulting `AutonomyCycleResult` is returned to the Rust caller (design intent: the host then actuates it under verified input leases; no such return path exists yet).

### 6. Goal-Oriented Action Planning (GOAP)

For long-horizon quest prerequisite chains, Maelstrom employs a two-tier autonomy architecture:
1. **Tier 1 (GOAP Macro Planning)**: An $A^*$ state-space search engine (`goap.rs`) plans the optimal sequence of actions satisfying goal preconditions (e.g. traveling between zones, refilling potions, acquiring quest items). See [Goal-Oriented Action Planning](GOAP_ACTION_PLANNING.md).
2. **Tier 2 (Behavior Tree Tactical Execution)**: Executes the current GOAP action while reactively arbitrating instantaneous events (combat encounters, vital dips, physical snags). Dynamic replanning is the intent if state deviations invalidate active plan preconditions; it is not implemented.

**Today (audit 2026-11-20, Claude):** a design, not a pipeline. `GoapPlanner::plan` has no caller outside tests (`quest_domain.rs` builds action lists), `W101Blackboard` has no
field that can hold a plan, and nothing detects an invalidated precondition or re-runs the planner. The heuristic text in
`GOAP_ACTION_PLANNING.md` was the pre-fix version and has been corrected.

### 7. Wizard City Starter & Unicorn Way Quest Domain Repertoire

The concrete action repertoire for Wizard City starter questlines (*"The Lore of Magic"*, *"Enrollment"*, *"A Good Impression"*), the subsequent Unicorn Way combat progression (*"Finding Out"*, Private O'Ryan, Lost Soul & Skeletal Pirate combat, Private Stillson, Lady Oriel), topological zone transitions, NPC interaction bounding geometry, and potion fountain checkpoints is grounded in `crates/maelstrom-core/src/quest_domain.rs`. See [W101 Quest Repertoire](W101_QUEST_REPERTOIRE.md) and [W101 Unicorn Way Quest Domain](W101_QUEST_UNICORN_WAY.md).

**Today (audit 2026-11-20, Claude):** planning data only. The repertoire has 41 actions and boolean vitals (`vitals_low`, `has_potions`; drinking does not consume a potion), its NPC rectangles are uncalibrated placeholders, and neither the repertoire nor `get_npc_interaction_target` is called outside tests.

### 8. Offline Autonomy Replay Harness

To check the autonomy stack's decisions against synthetic or ingested session frames without live client execution, Maelstrom provides an offline replay harness (`crates/maelstrom-fallback/src/autonomy_replay.rs`). It reports intent-label agreement, an intent-change counter, a Pure Pursuit curvature figure and a click-plan jerk proxy. See [Offline Autonomy Replay Harness](OFFLINE_AUTONOMY_REPLAY.md).

**Today (audit 2026-11-20, Claude):** the replay runs the Behavior Tree bridge only, not GOAP. The 99.5% accuracy threshold is the report's, while `run_batch` and the CLI exit code use 95%, and a session with no labels reports 100%. The curvature threshold (5.0) cannot be exceeded because the controller bounds the magnitude at 2.0, and the replan status is a constant PASS. The longest test trace is 99.9 s of synthetic frames.

### 9. Desktop Input Actuator Under Verified Lease

`DesktopInputActuator.cs` turns a steering or click summary into bounded key or mouse presses, with a lease-identity check, a
10 ms re-check loop, guaranteed release and a recovery hold on an unconfirmed release. Focus, countdown and user-takeover checks
are not in the actuator: they come from a caller-supplied `ready()` delegate. The only caller is the `actuate` pipe command,
whose live branch sends no input today. See [Desktop Input Actuator](DESKTOP_INPUT_ACTUATOR.md).

### 10. Combat Dueling Loop Integration

The end-to-end combat dueling loop integrates Behavior Tree combat preemption (`Branch 1: CombatBranch`), `CombatGeometry` coordinate transformation and 1280x720 viewport scaling, card and target selection, pass fallback, and `DesktopInputActuator` kinematic click execution. See [Combat Dueling Loop](COMBAT_DUELING_LOOP.md).

Today the branch, the geometry and a click-target plan summary exist and are tested separately. Nothing chooses a card, target or pass,
nothing sends the plan to the host, the 1280x720 coordinates are scaled from 800x600 and not measured, and the host's live branch sends
no input.




