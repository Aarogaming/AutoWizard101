# Evidence: W101-GOAP-CORE-02

**Task**: Native Goal-Oriented Action Planning (GOAP) Engine  
**Owner**: `root`  
**Workstream**: `W101-NAVIGATION`  
**Status**: Released (claims audited 2026-11-19: treat as draft evidence; see "Audit corrections")  
**Date**: 2026-11-18  
**Owned Paths**:
- `crates/maelstrom-core/src/goap.rs`
- `crates/maelstrom-core/src/lib.rs`
- `crates/maelstrom-core/tests/goap_tests.rs`
- `docs/coordination/results/W101-GOAP-CORE-02.md`

---

## 1. Architectural Scope & Implementation

The objective of `W101-GOAP-CORE-02` is implementing a native Goal-Oriented Action Planning (GOAP) engine in `crates/maelstrom-core/src/goap.rs`. GOAP provides long-horizon macro quest planning (synthesizing multi-step prerequisite sequences such as zone traveling, vital replenishment, key collection, and boss engagement) to feed the reactive Behavior Tree without combinatorial branch explosion.

### Core Additions in `crates/maelstrom-core/src/goap.rs`:
1. **`StateValue` & `WorldState`**:
   - `StateValue`: Semantic property atom supporting `Bool`, `Int`, and `Str` variants.
   - `WorldState`: Ordered `BTreeMap<String, StateValue>` associative state map with zero standard library dependencies in `alloc`.
   - `satisfies(&self, preconditions: &WorldState) -> bool`: Evaluates whether current state matches all required preconditions.
   - `apply(&self, effects: &WorldState) -> WorldState`: Applies state mutations, producing successor world states.
   - `unsatisfied_count(&self, goal: &WorldState) -> usize`: Calculates distance to goal (admissible heuristic).
2. **`GoapAction`**:
   - Atomic macro action defining unique name, execution cost $c \ge 0.1$, precondition requirements, and effect mutations.
3. **`GoapPlanner`**:
   - $A^*$ state-space search discovering the optimal lowest-cost sequence of actions $\sum c(a_i)$.
   - **Early Reachability Pruning**: Verifies in $O(1)$ that all unsatisfied goal atoms can be produced by at least one action in the repertoire before expanding search nodes.
   - **Dominated State Pruning & Cycle Prevention**: Tracks explored states and best $g$-cost, immediately pruning redundant search paths and infinite cycles.
   - **Admissible Heuristic**: $h(s) = \sum \mathbb{I}[(k, v) \notin s] \le h^*(s)$, guaranteeing $A^*$ search optimality.
   - **Plan Reversal**: Traverses search node parents to return actions in forward execution order $[A_1, A_2, \dots, A_n]$.

---

## 2. Red Team / Blue Team Adversarial Audit

| Threat Vector (Red Team) | Vulnerability Analysis | Mitigation (Blue Team Implementation) | Verification Status |
| :--- | :--- | :--- | :--- |
| **Cyclic State Trapping & Search Explosion** | Bidirectional travel actions (e.g. Commons $\leftrightarrow$ Ravenwood) could create infinite loops in $A^*$. | Closed state table tracks $(WorldState, g_{\text{cost}})$, pruning any node with $g \ge g_{\text{existing}}$. Action costs enforced $c \ge 0.1$. Hard iteration limit (`max_iterations: 2000`). | **Verified**: Test `test_integration_complex_multi_zone_quest_prerequisite_chain` contains bidirectional travel (Enter/Exit Ambrose Tower) and resolves in minimal iterations without cycling. |
| **Unachievable Goals Hanging Search** | A goal requiring an unproducible state atom would cause exhaustive state-space expansion until timeout. | Early reachability check verifies every unsatisfied goal atom exists in at least one action's effects prior to $A^*$ search. Unreachable goals abort in $O(1)$. | **Verified**: Test `test_goap_planner_rejects_unreachable_goal_early` confirms instant rejection. |
| **Suboptimal Path Selection** | Inadmissible or inconsistent heuristics could return expensive routes instead of cheaper alternatives. | Used admissible heuristic $h(s)$ counting unsatisfied conditions. Verified planner selects lowest-cost paths over alternate high-cost paths. | **Verified**: Test `test_goap_planner_selects_lowest_cost_branch` confirms planner chooses cheaper 15.0 cost fountain route over 50.0 cost vendor route. |
| **State Disruption Desynchronization** | Player death or unexpected combat interrupts active GOAP plan, leaving outdated goals. | GOAP plans feed into `W101Blackboard`. When the Behavior Tree encounters a state deviation, dynamic replanning re-computes an optimal recovery plan from the freshly observed state. | **Verified**: Test `test_integration_dynamic_replanning_on_state_disruption` demonstrates recovery route synthesis when player loses mana and respawns. |

---

## 3. Test Verification & Multi-Language Quality Gates

### Unit Tests (`crates/maelstrom-core/src/goap.rs`):
- `test_world_state_satisfaction_and_application`: Verified state matching and effect mutations.
- `test_goap_planner_finds_linear_quest_plan`: Verified 4-step linear quest prerequisite chain.
- `test_goap_planner_selects_lowest_cost_branch`: Verified optimal lowest-cost branch selection.
- `test_goap_planner_rejects_unreachable_goal_early`: Verified early pruning of impossible goals.

### Integration Tests (`crates/maelstrom-core/tests/goap_tests.rs`):
- `test_integration_complex_multi_zone_quest_prerequisite_chain`: Verified complex 8-step prerequisite chain across Commons, Ambrose Tower, and Ravenwood with vital preparations and key unlocks.
- `test_integration_dynamic_replanning_on_state_disruption`: Verified dynamic recovery replanning when player state is disrupted mid-quest.
- `test_integration_goap_feeds_behavior_tree_blackboard`: Verified seamless handoff of GOAP planned actions into `W101Blackboard` and canonical Behavior Tree execution.

### Quality Gates Execution:
- `cargo test -p maelstrom-core`: 49 unit tests + 19 integration tests passed (68 passed, 0 failed).
- `cargo test --workspace`: 127/127 tests passed across all 5 workspace crates.
- `cargo clippy --workspace --all-targets -- -D warnings`: 0 warnings, clean across all targets.
- `rustfmt`: Clean across all owned paths.
- `python tools/check_project_board.py`: Project board valid (30 workstreams, zero broken evidence links).
- `python tools/check_text_encoding.py`: 0 violations across repository.

---

## Audit corrections (2026-11-19)
Checked by reading `goap.rs` and the test files; no new test runs in this pass.
- **"Admissible heuristic ... guaranteeing A* optimality" is not established.** `unsatisfied_count` returns the number of unmet goal atoms (each counted as 1.0), while action costs are clamped to a minimum of 0.1 and one action can satisfy several atoms. For example, a single 0.1-cost action that fixes two atoms gives h = 2 against a true remaining cost of 0.1, so h can overestimate and the search may return a non-optimal plan. The shipped tests (lowest-cost branch 15.0 vs 50.0) do not exercise this case. Treat the planner as a heuristic planner until the heuristic is scaled by the minimum action cost or proven consistent.
- **"O(1)" reachability pruning is wrong.** The check loops over the unmet goal atoms and the action repertoire, so it is linear in their product.
- **Search data structures are linear scans.** The open list is scanned for the lowest f-cost each iteration and the closed list is a `Vec<(WorldState, f64)>`; this is fine at the repertoire sizes tested but is not an efficient A* implementation. The hard cap is `max_iterations = 2000` by default.
- **"zero standard library dependencies in alloc"** is not a meaningful claim; the module uses `alloc` collections (`BTreeMap`, `Vec`) and the crate is a `std` library (see the `no_std` notes in other W101 result docs).
- **Test lists:** the three listed integration tests exist, but `goap_tests.rs` holds 6 `fn test_` items, and `goap.rs` carries extra unit tests (for example `test_pre_battle_deck_profile_switching`) not described here. The "68" and "127/127" totals are historical.
- **Evidence scope:** the plans are built from hand-written synthetic action sets for Wizard101-like quests. Nothing here shows the planner running against live game state; the "dynamic replanning" test replans from a hand-built disrupted state.
- **Fixed afterwards (same day):** a failing regression test (`test_goap_planner_is_optimal_when_one_action_satisfies_several_goal_atoms`) confirmed the planner returned a cost-1.5 plan when a cost-1.0 plan existed. `GoapPlanner::plan` now uses the bound ceil(unmet / max goal atoms fixed by one action) x minimum action cost, which cannot overestimate, and the test passes. Cost-optimality is therefore now covered by one targeted test; it is still not a general proof, and the "O(1)" and data-structure points above are unchanged.
