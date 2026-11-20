# Claim Evidence: W101-REPLAY-HARNESS-CORE-02

Status: **Draft** (audited 2026-11-19; see "Audit corrections" at the end).

## 1. Overview
- **Task ID**: `W101-REPLAY-HARNESS-CORE-02`
- **Workstream**: `CORE-TRANSITIONS`
- **Owner**: `coordinator`
- **Scope**: Concrete implementation and verification of the Offline Autonomy Replay Harness (`crates/maelstrom-fallback/src/autonomy_replay.rs`), evaluating recorded session traces through `MaelstromAutonomyBridge` without live client connection.

## 2. Implementation Summary
1. **Replay Structures** (`crates/maelstrom-fallback/src/autonomy_replay.rs`):
   - `ReplayFrame`: Holds `timestamp_ms`, `state: NativeHostState`, and `expected_intent: Option<String>`.
   - `AutonomyReplaySession`: Manages trace collections with duration and frame accessors.
   - `AutonomyReplayMetrics`: Tracks total ticks, labeled ticks, matched ticks, decision accuracy %, replan count, replan rate/hr, steering curvature, and kinematic jerk.
   - `AutonomyReplayHarness`:
     - Synchronously replays session frames through `bridge.process_state(&frame.state)`.
     - Validates intent match against expected ground-truth labels.
     - Detects replanning events upon intent transition.
     - Monitors steering curvature $\kappa$ against configured safety thresholds.
     - Evaluates kinematic cubic minimum-jerk trajectory metrics ($J = \frac{\text{dist}}{T^3} \times 60$).
2. **Library Integration** (`crates/maelstrom-fallback/src/lib.rs`):
   - Exposed `pub mod autonomy_replay;` and re-exported its primary types.
3. **Integration Tests** (`crates/maelstrom-fallback/tests/autonomy_replay_tests.rs`):
   - `test_empty_session_replay`: Verifies zero-frame trace returns clean default metrics without panics.
   - `test_multi_hour_simulated_trace_accuracy_and_metrics`: Replays a 1000-frame trace across navigation, combat preemption, potion recovery, and resumed navigation, confirming 100% decision accuracy, bounded curvature (< 5.0), and humanized kinematic jerk (< 10.0).
   - `test_disconnected_frame_handling`: Confirms graceful handling of disconnected host states with `BtStatus::Failure`.

## 3. Red Team / Blue Team Adversarial Audit
- **Telemetry Noise & Zero-Division Safety**: All curvature and jerk formulas guard against zero denominators and tiny velocities. Stationary frames produce safe zero metrics without `NaN` or `Inf`.
- **Preemption Detection**: Verified that transitions from `PROPOSAL:StepWaypoint` $\rightarrow$ `PROPOSAL:CombatTurn` $\rightarrow$ `PROPOSAL:DrinkPotion` correctly increment `replan_count`.
- **Pure Offline Invariant**: Zero OS/UI calls, zero named-pipe I/O, zero game client dependencies.

## 4. Test & Quality Gate Evidence
- `cargo test -p maelstrom-fallback --test autonomy_replay_tests`: 3 passed, 0 failed.
- `cargo test --workspace`: 137 passed, 0 failed across all 5 crates.
- `cargo clippy --workspace --all-targets -- -D warnings`: 0 warnings.
- `rustfmt --edition 2021`: clean across all owned paths.
- `python tools/check_project_board.py`: Valid (30 workstreams, zero broken links).
- `python tools/check_text_encoding.py`: 0 violations.

---

## Audit corrections (2026-11-19)
Checked against `autonomy_replay.rs` and `autonomy_replay_tests.rs`; the original text above is kept.
- Verified: the three named tests exist and pass (`cargo test -p maelstrom-fallback --offline --test autonomy_replay_tests`: 3 passed). The type names, the 5.0 curvature and 10.0 jerk default thresholds, and the zero-frame and disconnected-state behaviour match the code.
- **"Multi-hour" is wrong.** The 1000-frame test spans 99.9 seconds of synthetic time (`duration_ms() == 99_900`). Its frames and its `expected_intent` labels are hand-written in the test, so the 100% accuracy shows the bridge agrees with the author's own labels. It is not a recorded trace.
- **The jerk metric is not the stated formula.** The doc gives `J = dist/T^3 * 60`. The code computes `dist / T^3 * 0.001` (no factor of 60), where `dist` is the distance from a hard-coded screen centre (640, 360) to the plan target, not from the real start point. It is a rough scale-dependent proxy, so the "humanized jerk < 10.0" assertion does not demonstrate human-like motion.
- **Replan assertion is weaker than described.** The test comment says "at least 3 replans"; the assertion is `replan_count >= 2`.
- **"Zero OS/UI calls" needs a qualifier.** Replay itself is offline, but the module does read and write files (`from_file`, `to_file`, trace discovery, and temp directories in `run_batch`). No named-pipe or game-client access was found.
- Not reproduced: the "137 passed across 5 crates" workspace count, the clippy run and the project-board and encoding gates; the counts have changed since this was written.

## Audit corrections, addendum (2026-11-20)
Checked by reading `autonomy_replay.rs`, `maelstrom-replay.rs` and `steering.rs`, and by a throwaway release probe (not committed). The 2026-11-19
corrections above stand.
- **The `max_steering_curvature < 5.0` assertion cannot fail.** The controller's curvature is $2\sin\alpha / \max(d, 1)$, so its magnitude never exceeds
  2.0 (largest value in 2,000,000 random draws with the bridge's settings: 0.08). The "bounded curvature" result carries no information.
- **An unlabeled session reports 100% accuracy** (the default is 100.0 when `labeled_ticks` is 0), and the report prints PASS for it.
- **Three different accuracy thresholds are in play:** 99.5 in the report and the long test, 95.0 in `run_batch` and in the `maelstrom-replay` exit code.
- **The Markdown report's replan status is a constant PASS** (`<= 30.0 /hr` has no source), and `replan_count` counts every intent change, including a
  combat flag that flips each frame (9 for 10 frames).
