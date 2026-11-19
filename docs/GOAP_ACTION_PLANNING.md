# Goal-Oriented Action Planning (GOAP)

**Date**: 2026-11-18  
**Workstream**: `W101-NAVIGATION` / `CORE-TRANSITIONS`  
**Reference Document**: Architecture, mathematical state-space formulation, and Behavior Tree integration for macro quest prerequisite chaining.

---

## 1. Executive Summary & Architectural Motivation

While Maelstrom's hierarchical Behavior Tree (`crates/maelstrom-core/src/behavior_tree.rs`) provides sub-millisecond reactive arbitration for immediate tactical events (such as combat interruption, vital recovery, and obstacle unstuck maneuvers), hardcoding long-horizon quest progression directly into static tree branches causes combinatorial explosion.

A typical Wizard101 quest requires resolving dynamic, inter-dependent prerequisites:
1. **Spatial Navigation**: Reaching an NPC or dungeon entrance across multiple world zones (e.g., The Commons $\to$ Ravenwood $\to$ Death School $\to$ Nightside).
2. **Resource Preparation**: Verifying sufficient mana and active potions before initiating dangerous dungeon sigils.
3. **Inventory & Dialogue Prerequisites**: Talking to intermediate quest-givers, collecting required reagents, or defeating preparatory mobs.

Goal-Oriented Action Planning (GOAP) decouples **what needs to be achieved** (the Goal State) from **how to achieve it** (the Action Set). Rather than authoring rigid linear scripts, developers define atomic actions with preconditions and effects. At runtime, the GOAP planner uses heuristic $A^*$ state-space search to dynamically synthesize the optimal, lowest-cost sequence of actions bridging the player's current state to the goal.

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
- $c \in \mathbb{R}^+$: Positive action cost (e.g. travel distance, duration in seconds, gold cost).
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
The heuristic $h(u)$ computes the number of unsatisfied conditions remaining between $\mathcal{S}_u$ and $\mathcal{S}_{\text{goal}}$:

$$h(u) = \sum_{(k, v) \in \mathcal{S}_{\text{goal}}} \mathbb{I}[(k, v) \notin \mathcal{S}_u]$$

Because each action can satisfy at most a finite number of conditions and costs are positive ($c \ge 1.0$), $h(u)$ never overestimates the true remaining cost, ensuring $A^*$ optimality.

---

## 3. Two-Tier Autonomy Architecture: GOAP + Behavior Tree

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
│  Priority 3: Kinematic Stuck Recovery (Watchdog)       │
│  Priority 4: Execute Current GOAP Action (Nav/Talk)    │
│  Priority 5: Idle Fallback                             │
└────────────────────────────────────────────────────────┘
```

### Architectural Separation (`Facts ≠ Authority ≠ Actions`):
1. **Long-Horizon Planning (GOAP)**: Evaluates prerequisite chains and produces macro plans.
2. **Reactive Execution (Behavior Tree)**: Ticks every frame (or observation cycle), monitoring for unexpected tactical deviations (mob aggro, vital depletion, physical snags).
3. **Dynamic Replanning**: If the Behavior Tree encounters a state transition that invalidates the active GOAP action's preconditions, the active plan is cleared and the GOAP planner automatically re-computes an optimal path from the freshly observed state.
