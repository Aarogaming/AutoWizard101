# Evidence: W101-IPC-CORE-02

**Task**: Host IPC Integration: Wiring Rust Core Autonomy Engine to C# Named Pipe Bridge  
**Owner**: `root`  
**Workstream**: `W101-NAVIGATION`  
**Status**: Released  
**Date**: 2026-11-18  
**Owned Paths**:
- `crates/maelstrom-fallback/src/maelstrom_bridge.rs`
- `crates/maelstrom-fallback/src/lib.rs`
- `crates/maelstrom-fallback/tests/autonomy_bridge_tests.rs`
- `docs/coordination/results/W101-IPC-CORE-02.md`

---

## 1. Architectural Scope & Implementation

The objective of `W101-IPC-CORE-02` is implementing native host IPC integration coupling the pure Rust autonomy engine (`maelstrom-core` Behavior Tree, Pure Pursuit steering, and kinematic trajectory generator) to the Project Maelstrom C# host named-pipe transport (`JobDeckHost.cs` / `JobDeckWire.cs`) in `crates/maelstrom-fallback/src/maelstrom_bridge.rs`.

### Core Additions in `crates/maelstrom-fallback/src/maelstrom_bridge.rs`:
1. **`NativeHostState::update_blackboard(&self, bb: &mut W101Blackboard)`**:
   - Maps qualified native host telemetry facts directly into the W101 Behavior Tree Blackboard:
     - `mana_pct`: Ratio $\frac{\text{mana\_current}}{\text{mana\_max}}$.
     - `health_pct`: Ratio $\frac{\text{health\_current}}{\text{health\_max}}$.
     - `has_potion`: Boolean flag derived from $\text{potions\_current} \ge 1.0$.
     - `in_combat`, `current_zone`, `current_pose`: Extracted from `raw_snapshot["facts"]` (`combatActive`, `zoneName`, `spatialPosition`).
     - Connection Invalidation: If disconnected, invalidates vitals, logs status, and halts action proposals.
2. **`SteeringCommandSummary` & `KinematicPlanSummary`**:
   - Serializable representations of motion and trajectory intents adhering strictly to `Facts ≠ Authority ≠ Actions`.
3. **`AutonomyCycleResult`**:
   - High-level perception-decision summary struct reporting `connected`, `bt_status`, `proposed_intent`, `steering_command`, `kinematic_plan`, `current_zone`, `health_pct`, `mana_pct`, `has_potion`, `in_combat`, and `status_log`.
4. **`MaelstromAutonomyBridge`**:
   - Integrates `MaelstromNativeBridge`, `W101Blackboard`, `Selector<W101Blackboard>`, `PurePursuitController`, and `FastPrng`.
   - `process_state(&mut self, state: &NativeHostState) -> AutonomyCycleResult`: Synchronous, pure evaluation of snapshot frames for deterministic offline verification and replay.
   - `step_cycle(&mut self) -> Result<AutonomyCycleResult, BridgeError>`: Asynchronous single-tick polling over the Windows named pipe `\\.\pipe\maelstrom-deck-<HASH_HEX_24>`.
5. **Command Coverage Expansion**:
   - Added `"facts"` command to `call_stream` command validation matching `JobDeckHost.Handle`.

---

## 2. Red Team / Blue Team Adversarial Audit

| Threat Vector (Red Team) | Vulnerability Analysis | Mitigation (Blue Team Implementation) | Verification Status |
| :--- | :--- | :--- | :--- |
| **Stale Snapshot Action Continuation** | Host connection drops or pauses could leave previous vitals active, driving actions based on expired state. | `update_blackboard` checks `self.connected` and timestamp freshness. If disconnected or stale, vitals and combat flags are immediately invalidated. | **Verified**: Test `test_autonomy_bridge_disconnected_invalidation` confirms disconnected snapshot forces BT Failure and clears all steering/kinematic commands. |
| **Input Lease Hijacking** | Rust autonomy logic executing direct Win32 inputs could bypass Maelstrom's single input owner, recovery guards, and focus checks. | `MaelstromAutonomyBridge` returns structured `AutonomyCycleResult` intents. Actuation remains exclusively with the host `OperationScheduler` under verified input leases. | **Verified**: Architectural separation verified; `maelstrom-fallback` contains zero Win32 input API imports. |
| **Pipe Hangs & Resource Leaks** | Host deadlocks or aborted connections could freeze the autonomy cycle indefinitely. | All named pipe operations use timeouts (`DEFAULT_TIMEOUT = 8s`, `CONNECT_TIMEOUT = 1.5s`) and bounded cancellation channels. | **Verified**: Pipe timeouts and busy retry logic verified in `busy_pipe.rs`. |

---

## 3. Test Verification & Multi-Language Quality Gates

### Integration Tests (`crates/maelstrom-fallback/tests/autonomy_bridge_tests.rs`):
- `test_autonomy_bridge_disconnected_invalidation`: Verified disconnected host snapshot forces BT Failure status and nullifies steering/kinematic commands.
- `test_autonomy_bridge_navigation_cycle_and_steering`: Verified full vitals with active waypoint emits `PROPOSAL:StepWaypoint` and generates valid Pure Pursuit forward pulse (`250ms`).
- `test_autonomy_bridge_low_vital_triggers_potion`: Verified low mana (10%) triggers vital recovery branch, emits `PROPOSAL:DrinkPotion`, and halts movement.
- `test_autonomy_bridge_combat_interruption_and_kinematic_card_plan`: Verified combat initiation fanfare or flag preempts navigation, emits `PROPOSAL:CombatTurn`, and generates humanized Bézier card selection trajectory.

### Quality Gates Execution:
- `cargo test -p maelstrom-fallback`: 15 unit tests + 34 integration tests passed (49 passed, 0 failed).
- `cargo test --workspace`: 120/120 tests passed across all 5 workspace crates.
- `cargo clippy --workspace --all-targets -- -D warnings`: 0 warnings, clean across all targets.
- `rustfmt --edition 2021`: Clean across all owned paths.
- `python tools/check_project_board.py`: Project board valid (30 workstreams, zero broken evidence links).
- `python tools/check_text_encoding.py`: 0 violations across repository.
