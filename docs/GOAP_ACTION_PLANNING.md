# Goal-Oriented Action Planning (GOAP)

**Date**: 2026-11-18  
**Workstream**: `W101-NAVIGATION` / `CORE-TRANSITIONS`  
**Reference Document**: Architecture, mathematical state-space formulation, and Behavior Tree integration for macro quest prerequisite chaining.

> **Status (audit 2026-11-20, Claude):** a design plus library code. `GoapPlanner` in `goap.rs` and its tests are real, but nothing runs it:
> there is no caller outside tests (`quest_domain.rs` only builds action lists), nothing feeds a plan to the Behavior Tree, and nothing
> re-plans. The heuristic text below was the pre-fix version and is corrected; sections 1 and 3 now say what the code does. Notes marked
> **Today** say what the code does. Evidence: [W101-GOAP-CORE-02](coordination/results/W101-GOAP-CORE-02.md) and
> [W101-GOAP-DOCS-01](coordination/results/W101-GOAP-DOCS-01.md), audit notes.

---

## 1. Executive Summary & Architectural Motivation

While Maelstrom's hierarchical Behavior Tree (`crates/maelstrom-core/src/behavior_tree.rs`) provides cheap reactive arbitration (about 40-60 ns per tick of the canonical tree in a one-off release probe on 2026-11-20: tree
only, no I/O, not a committed benchmark) for immediate tactical events (such as combat interruption, vital recovery, and obstacle unstuck maneuvers), hardcoding long-horizon quest progression directly into static tree branches causes combinatorial explosion.

A typical Wizard101 quest requires resolving dynamic, inter-dependent prerequisites:
1. **Spatial Navigation**: Reaching an NPC or dungeon entrance across multiple world zones (e.g., The Commons $\to$ Ravenwood $\to$ Death School $\to$ Nightside).
2. **Resource Preparation**: Verifying sufficient mana and active potions before initiating dangerous dungeon sigils.
3. **Inventory & Dialogue Prerequisites**: Talking to intermediate quest-givers, collecting required reagents, or defeating preparatory mobs.

Goal-Oriented Action Planning (GOAP) decouples **what needs to be achieved** (the Goal State) from **how to achieve it** (the Action Set). Rather than authoring rigid linear scripts, developers define atomic actions with preconditions and effects. In the intended design the GOAP planner uses heuristic $A^*$ state-space search to dynamically synthesize the optimal, lowest-cost sequence of actions bridging the player's current state to the goal.

---

## 2. Mathematical State-Space Formulation

### 2.1 World State Representation

A `WorldState` is an associative map of semantic state atoms:

$$\mathcal{S} = \{ (k_1, v_1), (k_2, v_2), \dots, (k_m, v_m) \}$$

where key $k_i \in \text{String}$ and value $v_i \in \{ \text{Bool}, \text{Int}, \text{String} \}$.

#### State Matching & Subsumption:
A world state $\mathcal{S}_{\text{current}}$ satisfies precondition state $\mathcal{S}_{\text{req}}$ (denoted $\mathcal{S}_{\text{current}} \models \mathcal{S}_{\text{req}}$) if and only if:

$$\forall (k, v) \in \mathcal{S}_{\text{req}}, \quad (k, v) \in \mathcal{S}_{\text{current}}$$

### 2.2 Action Definition

A `GoapAction` represents an atomic macro-level operation:

$$\mathcal{A} = \langle \text{name}, c, \mathcal{P}, \mathcal{E} \rangle$$

- $\text{name}$: Unique action identifier (e.g. `"DrinkPotionAtFountain"`, `"TravelCommonsToRavenwood"`).
- $c \in \mathbb{R}^+$: Positive action cost (e.g. travel distance, duration in seconds, gold cost). `GoapAction::new` clamps it to at
  least 0.1 (`cost.max(0.1)`, so 0, negative and NaN all become 0.1; positive infinity is kept).
- $\mathcal{P}$: Precondition world state required to execute $\mathcal{A}$.
- $\mathcal{E}$: Effect world state mutations produced upon successful completion.

#### State Transition Function:
When action $\mathcal{A}$ is applied to state $\mathcal{S}$ (where $\mathcal{S} \models \mathcal{P}$):

$$\mathcal{S}' = \text{apply}(\mathcal{S}, \mathcal{E}) = (\mathcal{S} \setminus \{ (k, *) \mid k \in \text{keys}(\mathcal{E}) \}) \cup \mathcal{E}$$

---

### 2.3 $A^*$ State-Space Graph Search

Finding an optimal action plan is formulated as finding the shortest path from initial world state $\mathcal{S}_{\text{start}}$ to a goal state $\mathcal{S}_{\text{goal}}$ such that $\mathcal{S}_{\text{final}} \models \mathcal{S}_{\text{goal}}$.

#### Search Node:
A search node $u$ consists of:
- State $\mathcal{S}_u$.
- Path cost $g(u) = \sum_{a \in \text{path}} c(a)$.
- Heuristic estimate $h(u)$.
- Priority $f(u) = g(u) + h(u)$.
- Predecessor node and generating action.

#### Admissible Heuristic:
Let $U(u)$ be the number of goal atoms $(k, v) \in \mathcal{S}_{\text{goal}}$ not satisfied in $\mathcal{S}_u$
(`WorldState::unsatisfied_count`), let $m \ge 1$ be the largest number of goal atoms that any single action in the repertoire sets as an
effect, and let $c_{\min}$ be the cheapest action cost in the repertoire ($\ge 0.1$). The heuristic is

$$h(u) = \left\lceil \frac{U(u)}{m} \right\rceil \cdot c_{\min}$$

Every remaining action fixes at most $m$ unmet atoms and costs at least $c_{\min}$, so at least $\lceil U/m \rceil$ more actions are
needed and $h$ cannot overestimate. The first version of this document, and of the code, used the raw count $U(u)$ as $h$. With costs as
low as 0.1 and one action able to fix several atoms that overestimates: the planner returned a cost-1.5 plan when a cost-1.0 plan
existed. Commit `7f2c5c4` fixed the code with a regression test
(`test_goap_planner_is_optimal_when_one_action_satisfies_several_goal_atoms`). Optimality is therefore covered by that test and the
cheapest-branch test, not proven in general.

#### Search details (as implemented):
- An action is applied only when its preconditions hold, and its effects overwrite the keys they name (`WorldState::apply`).
- Before searching, every unmet goal atom must be set as an effect by at least one action; if not, `plan` returns `None` at once. That
  check loops over the unmet atoms times the actions; it is not constant time.
- The open list is a `Vec` scanned for the lowest $f$ (ties broken on the lowest $h$) and the closed list is a `Vec` of (state, best $g$)
  pairs. That is fine at the repertoire sizes tested and is not an efficient $A^*$.
- `max_iterations` defaults to 2000 (`GoapPlanner::new(0)` also means 2000). When the budget runs out `plan` returns `None`, the same
  value as for an unreachable goal: in a probe a 3-step chain with a budget of 1 returned `None` and with the default budget returned a
  3-action plan. An already-satisfied goal returns an empty plan.

---

## 3. Two-Tier Autonomy Architecture: GOAP + Behavior Tree (design intent; the link between the tiers is not implemented)

```
┌────────────────────────────────────────────────────────┐
│             Tier 1: GOAP Macro Planner                 │
│                                                        │
│  Current World State ──► [ A* State Search ] ◄── Goal  │
│                                 │                      │
│                                 ▼                      │
│                     Optimal Plan: [A1, A2, A3]         │
└─────────────────────────────────┼──────────────────────┘
                                  │ Pop next action
                                  ▼
┌────────────────────────────────────────────────────────┐
│             Tier 2: Reactive Behavior Tree             │
│                                                        │
│  Priority 1: Combat Interruption (Preempts all)        │
│  Priority 2: Vital Recovery (Potion if low HP/MP)      │
│  Priority 3: Kinematic Stuck Recovery (is_stuck flag)  │
│  Priority 4: Opportunistic Detour (approved detour)    │
│  Priority 5: Quest Navigation (goal + waypoints)       │
│  Priority 6: Idle Fallback                             │
└────────────────────────────────────────────────────────┘
```

### Architectural Separation (`Facts ≠ Authority ≠ Actions`):
1. **Long-Horizon Planning (GOAP)**: Evaluates prerequisite chains and produces macro plans.
2. **Reactive Execution (Behavior Tree)**: Ticks once per observation cycle (`MaelstromAutonomyBridge::process_state`, and not at all in a
   cycle where the combat state or the pose is unknown), monitoring for tactical deviations (mob aggro, vital depletion, physical snags).
   **Today** the tree has no "current GOAP action": its navigation branch runs whenever `active_goal` is set and `active_waypoints` is
   non-empty, and the physical-snag branch runs only when something sets `is_stuck` (only tests do).
3. **Dynamic Replanning (not implemented)**: the intent is that a state transition invalidating the active GOAP action's preconditions
   clears the plan and re-runs the planner from the freshly observed state. **Today** nothing detects such a transition and nothing calls
   `GoapPlanner::plan` outside tests. `W101Blackboard` has no field that can hold a plan (`active_goal` is an `Option<String>`).
   `test_integration_goap_feeds_behavior_tree_blackboard` copies `plan[0].name` into `active_goal` and sets waypoints by hand, and
   `test_integration_dynamic_replanning_on_state_disruption` calls `plan` again from a hand-built disrupted state.
