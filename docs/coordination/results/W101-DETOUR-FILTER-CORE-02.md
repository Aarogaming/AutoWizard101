# Result Evidence: W101-DETOUR-FILTER-CORE-02

## Task Identity
- **Claim ID**: `W101-DETOUR-FILTER-CORE-02`
- **Workstream**: `CORE-TRANSITIONS`
- **Task**: Opportunistic Detour Budget Filter Implementation (costmap.rs + behavior_tree.rs)
- **Status**: Completed

## 1. Executive Summary
This milestone implements the Opportunistic Detour Budget Filter within `crates/maelstrom-core/src/costmap.rs` and `crates/maelstrom-core/src/behavior_tree.rs`. The engine allows navigation routines to harvest detected reagents and chests along the route without disrupting quest completion schedules or violating mob aggro clearance boundaries.

### Core Mathematical & Algorithmic Components Implemented
1. **Detour Budget Equation Evaluation (`evaluate_detour_candidate`)**:
   $$\Delta C = (C(\mathbf{p}_{\text{curr}} \to \mathbf{p}_{\text{detour}}) + C(\mathbf{p}_{\text{detour}} \to \mathbf{p}_{\text{resume}})) - C(\mathbf{p}_{\text{curr}} \to \mathbf{p}_{\text{resume}}) < \text{detour\_budget}$$
   - Rejects candidate if the added travel distance/cost meets or exceeds `detour_budget`.

2. **Dual-Corridor Dynamic Mob Aggro Threat Checking**:
   - Evaluates both Leg 1 ($\mathbf{p}_{\text{curr}} \to \mathbf{p}_{\text{detour}}$) and Leg 2 ($\mathbf{p}_{\text{detour}} \to \mathbf{p}_{\text{resume}}$) transit corridors via `is_corridor_safe_3d`.
   - Computes 3D closest point on segment $\mathbf{p}_1 \to \mathbf{p}_2$ to dynamic mob predicted coordinates, enforcing aggro cylinder clearance and elevation tolerance checks.

3. **Behavior Tree Leaf Integration (`OpportunisticDetourBranch`)**:
   - Injected at Priority 4 in canonical W101 Behavior Tree:
     - Priority 1: Combat preemption.
     - Priority 2: Vital recovery (mana / health).
     - Priority 3: Stuck recovery.
     - **Priority 4: Opportunistic Detour**.
     - Priority 5: Mainline quest navigation.
   - Proposes Pure Pursuit steering toward detour entity until $d \le 3.5$, emits `PROPOSAL:Interact:Detour:<id>`, and marks detour complete upon consumption.

---

## 2. Test Verification & Empirical Results

### Unit & Integration Test Suite (`maelstrom-core`)
```
test costmap_tests::test_detour_budget_equation_and_approval ... ok
test behavior_tree_tests::test_integration_opportunistic_detour_execution_and_preemption ... ok
test costmap_tests::test_spatial_corridor_safety_evaluation ... ok

test result: ok. 62 unit tests + 41 integration tests = 103 passed; 0 failed; finished in 0.35s
```

### Full Workspace Quality Gates
- `cargo clippy --workspace --all-targets -- -D warnings`: 0 warnings, clean exit.
- `cargo test --workspace`: all workspace crates passed (100% test pass rate).
- `rustfmt --edition 2021`: formatted owned files cleanly.
- `python tools/check_project_board.py`: 30 workstreams valid, zero broken links.
- `python tools/check_text_encoding.py`: 0 violations.

---

## 3. Adversarial Red Team / Blue Team Self-Audit

### Attack Vector 1: Infinite Detour Chaining (Reagent Rabbit Hole)
- **Red Team Vector**: A cluster of reagents could cause the agent to continuously siphon pathing into new detours, perpetually deferring the mainline quest.
- **Blue Team Mitigation**: Detour candidate evaluation evaluates delta cost against the *active mainline resumption waypoint* $\mathbf{p}_{\text{resume}}$. Detour budget is strictly decremented or limited to one active detour at a time, ensuring that cumulative diversion never exceeds the session budget limit.

### Attack Vector 2: Dynamic Mob Intercept on Detour Resumption Leg
- **Red Team Vector**: A patrol mob moves into the resumption path while the player is picking up the reagent, creating a combat trap when attempting to rejoin the mainline route.
- **Blue Team Mitigation**: Dual-corridor evaluation checks both Leg 1 and Leg 2 before approval. Furthermore, the reactive Behavior Tree ticks every cycle: if a mob intrudes upon the player or resumption path, higher-priority combat/preemption leaves or local costmap repulsion vectors immediately override the detour.

### Attack Vector 3: Mob Aggro at Endpoint Missed by Midpoint Check
- **Red Team Vector**: A mob camping at the detour target entity itself is missed if corridor clearance only evaluates the midpoint of the transit segment.
- **Blue Team Mitigation**: Replaced midpoint-only checks with `closest_point_on_segment_3d`, which projects the mob's 3D position onto the segment line and evaluates distance to the closest point, strictly catching mobs camping at start, middle, or end.

---

## 4. Released Claims & Owned Artifacts
- Claim `W101-DETOUR-FILTER-CORE-02` released.
- Owned files:
  - `crates/maelstrom-core/src/costmap.rs`
  - `crates/maelstrom-core/src/behavior_tree.rs`
  - `crates/maelstrom-core/src/lib.rs`
  - `crates/maelstrom-core/tests/costmap_tests.rs`
  - `crates/maelstrom-core/tests/behavior_tree_tests.rs`
  - `docs/coordination/results/W101-DETOUR-FILTER-CORE-02.md`
