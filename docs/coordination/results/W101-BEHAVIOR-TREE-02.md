# Evidence: W101-BEHAVIOR-TREE-02

**Task**: Native Behavior Tree Engine & Canonical W101 State Orchestration  
**Owner**: `root`  
**Workstream**: `W101-NAVIGATION`  
**Status**: Released (claims audited 2026-11-19 and 2026-11-20: treat as draft evidence; see the audit notes)  
**Date**: 2026-11-18  
**Owned Paths**:
- `crates/maelstrom-core/src/behavior_tree.rs`
- `crates/maelstrom-core/src/lib.rs`
- `crates/maelstrom-core/tests/behavior_tree_tests.rs`
- `docs/coordination/results/W101-BEHAVIOR-TREE-02.md`

---

## 1. Architectural Scope & Implementation

The objective of `W101-BEHAVIOR-TREE-02` is implementing Option 2: a native, zero-dependency, pure-Rust Hierarchical Behavior Tree engine in `maelstrom-core` to resolve the brittleness of flat `if/else` state machines when managing complex questing, kinematic stuck recovery, and nested interruptions.

### Core Additions in `crates/maelstrom-core/src/behavior_tree.rs`:
1. **`BtStatus`**:
   - Tri-state execution status enum: `Success`, `Failure`, `Running`.
2. **`BtNode<B>` Trait**:
   - Standard interface: `tick(&mut self, blackboard: &mut B) -> BtStatus`, `reset(&mut self)`, `name(&self) -> &str`.
3. **Composites**:
   - `Selector<B>` (Priority Fallback): Evaluates children strictly in priority order (0 to $N-1$). Automatically calls `reset()` on lower-priority children whenever a higher-priority child activates or runs, guaranteeing clean preemption.
   - `Sequence<B>` (Reactive Sequence): Evaluates children from index 0 on every tick. Guarantees that if a precondition (e.g. `in_combat`, `has_goal`) becomes false while an action is `Running`, the sequence immediately aborts, calls `reset()`, and returns `Failure`.
   - `Parallel<B>`: Evaluates children concurrently under `RequireAll` or `RequireOne` policies.
4. **Decorators**:
   - `Inverter<B>`: Flips `Success` $\leftrightarrow$ `Failure`, passes `Running`.
   - `Retry<B>`: Retries inner node up to `max_attempts` on failure before propagating failure.
   - `ForceSuccess<B>`: Normalizes failure to success.
5. **Leaves**:
   - `ConditionNode<B>`: Read-only predicate querying typed blackboard facts.
   - `ActionNode<B>`: Executes state operations and emits action proposals.
6. **`W101Blackboard`**:
   - Domain-typed struct capturing verified observation facts (`in_combat`, `mana_pct`, `health_pct`, `has_potion`, `is_stuck`, `current_zone`, `active_goal`, `active_waypoints`, `current_pose`) and the emitted intent channel (`proposal_channel`).
7. **`build_w101_canonical_tree`**:
   - Canonical 5-tier priority hierarchy:
     1. **Combat Interruption** (Priority 1): `in_combat == true` $\to$ `PROPOSAL:CombatTurn` (`Running`).
     2. **Vital Recovery** (Priority 2): `mana < 0.20 || health < 0.25` and `has_potion` $\to$ `PROPOSAL:DrinkPotion` (`Success`).
     3. **Kinematic Stuck Recovery** (Priority 3): `is_stuck == true` $\to$ `PROPOSAL:UnstuckPulse` (`Success`, clears stuck latch).
     4. **Quest Navigation** (Priority 4): `active_goal.is_some()` $\to$ `PROPOSAL:StepWaypoint` / `PROPOSAL:Arrival`.
     5. **Idle Fallback** (Priority 5): `PROPOSAL:YieldIdle` (`Success`).

---

## 2. Test Verification & Multi-Language Quality Gates

### Unit Tests (`crates/maelstrom-core/src/behavior_tree.rs`):
- `test_selector_fallback_and_first_success`: Verified priority fallback order and first non-failure selection.
- `test_sequence_short_circuit_on_failure`: Verified immediate sequence failure on failing condition and non-execution of subsequent actions.
- `test_inverter_decorator`: Verified status inversion logic.
- `test_retry_decorator`: Verified multi-attempt retry loop terminating in success or failure.
- `test_parallel_require_all_and_require_one`: Verified concurrent parallel policy evaluation.
- `test_priority_preemption_combat_aborts_navigation`: Verified that combat interruption preempts navigation instantly and cleanly resumes upon combat conclusion.

### Integration Tests (`crates/maelstrom-core/tests/behavior_tree_tests.rs`):
- `test_integration_complex_questing_and_nested_recovery_pipeline`:
  - 8-tick end-to-end mission simulation:
    1. Navigation starts towards waypoint.
    2. Odometry watchdog triggers `is_stuck = true` $\to$ Stuck recovery pulse preempts navigation and clears latch.
    3. Navigation automatically resumes.
    4. Mana drops to 0.15 $\to$ Vital recovery preempts navigation, drinks potion, restores mana to 1.0.
    5. Roaming mob initiates combat $\to$ Combat branch preempts navigation immediately.
    6. Combat concludes $\to$ Navigation resumes.
    7. All waypoints completed $\to$ Arrival proposal emitted, goal cleared.
    8. Idle fallback reached safely.
- `test_integration_reactive_preemption_resets_running_subtrees`: Verified clean internal state reset during mid-flight task preemption.
- `test_integration_decorators_and_parallel_policies`: Verified composition of `Retry`, `Inverter`, `ForceSuccess`, and `Parallel`.

### Quality Gates Execution:
- `cargo test -p maelstrom-core`: 29 unit tests + 7 integration tests passed (36 passed, 0 failed).
- `cargo test --workspace`: 96/96 tests passed across all 5 workspace crates.
- `cargo clippy -p maelstrom-core -- -D warnings`: 0 warnings, clean dev profile.
- `rustfmt --check`: 0 formatting changes required.
- `python tools/check_text_encoding.py`: 0 violations across repository.
- `python tools/check_project_board.py`: Valid, 30 workstreams checked.

---

## 3. Red Team / Blue Team Adversarial Self-Audit

### Audit Finding 1 (Critical Defect Discovered & Remediated During Red Team Run):
- **Red Team Attack**: In initial drafting, `Sequence` used a memory-based index counter (`current_index`) that resumed execution from the running child. If an action in the sequence was `Running` (e.g. `ExecuteCombatTurn`), and the precondition `CheckInCombat` subsequently flipped to `false`, the sequence bypassed re-checking child 0 and continued running child 1!
- **Blue Team Remediation**: Refactored `Sequence` to implement Reactive Sequence semantics: on every tick, it starts at index 0 and re-verifies all preceding preconditions. If any precondition evaluates to `Failure`, the sequence immediately resets the in-flight running child, clears its index, and propagates `Failure`. Verified by unit and integration tests.

### Audit Finding 2:
- **Red Team Attack**: Can stale action proposals on `W101Blackboard.proposal_channel` leak across preemptions?
- **Blue Team Mitigation**: Every branch that activates explicitly overwrites `proposal_channel` with its own unique proposal ID. Furthermore, when `Selector` preempts a running child, it executes `child.reset()`.

### Audit Finding 3:
- **Red Team Attack**: Invariant Compliance (`Facts ≠ Authority ≠ Actions`). Does the tree submit Win32 inputs directly?
- **Blue Team Verification**: No. The Behavior Tree only emits declarative proposal intents into the blackboard (`proposal_channel`). Physical input dispatch is strictly gated behind the external single-owner input scheduler and lease.

---

## Audit note (2026-11-19)
Scope: wording about `no_std` and dependencies only; the rest of this report was not re-audited in this pass.
- The "zero-dependency" wording means no third-party crates beyond what `maelstrom-core` already uses; `behavior_tree.rs` imports only `alloc` types, and no `no_std` claim is made here. It was not checked in this pass whether the tests cover every node type described.
- The crate runs as a `std` library; it currently depends on `serde`, `uuid` and optional codec crates. Adding `libm` or similar to support `no_std` would be a new dependency decision.

## Audit note (2026-11-20, Claude)
Checked by reading `behavior_tree.rs`, the two test files and `maelstrom_bridge.rs`, and by a throwaway release probe (not committed). The
test totals in Section 2 are historical and were not re-run here.
- **"Canonical 5-tier priority hierarchy" is now six:** an Opportunistic Detour branch was added as Priority 4 (Quest Navigation is 5, Idle 6).
  The doc comment on `build_w101_canonical_tree` listed five and was corrected.
- **"Odometry watchdog triggers `is_stuck = true`" (integration test step 2) is a hand-set flag.** The test writes `bb.is_stuck = true`;
  `OdometryWatchdog` has no caller, so nothing sets the flag in production code.
- **`Parallel` is not concurrent.** It ticks every child in order on every tick (a child that already succeeded is ticked again) under
  `RequireAll` or `RequireOne`. `Retry` returns `Running` between failed attempts. `DrinkPotion` writes the restored values itself
  (no verification), and `W101Blackboard::status_log` is appended to on every navigation and combat tick and never cleared (probe: 1,000 ticks left
  1,000 entries; `AutonomyCycleResult` clones it every cycle). The last is an open design item, not changed here.
- **Audit Finding 2 ("stale proposals cannot leak")** holds for the canonical tree, where every branch overwrites `proposal_channel`; the bridge
  also clears it when it withholds a cycle. Resetting a preempted `Sequence` has no observable effect on `ActionNode` or `ConditionNode` leaves
  (their `reset` does nothing), so `test_integration_reactive_preemption_resets_running_subtrees` shows reactive re-evaluation from index 0, not
  that any leaf state was cleared.
- **Audit Finding 3 holds:** the tree emits only strings; no node sends input.
