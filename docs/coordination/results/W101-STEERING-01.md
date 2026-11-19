# Evidence: W101-STEERING-01

**Task**: Option 1 - Local Steering Controller (Pure Pursuit) & Odometry Watchdog  
**Owner**: `root`  
**Workstream**: `W101-NAVIGATION`  
**Status**: Released (claims audited 2026-11-19: core claims verified, caveats below)  
**Date**: 2026-11-18  
**Owned Paths**:
- `crates/maelstrom-core/src/steering.rs`
- `crates/maelstrom-core/src/lib.rs`
- `crates/maelstrom-core/tests/steering_tests.rs`
- `docs/coordination/results/W101-STEERING-01.md`

---

## 1. Architectural Scope & Implementation

The objective of `W101-STEERING-01` is implementing Option 1: translating discrete NavMesh waypoint sequences into smooth, human-like continuous keyboard pulses (`W`, `A`, `D`, `S`) using Pure Pursuit path tracking, combined with an Odometry Watchdog for automatic physical snag detection and multi-stage unstuck recovery.

### Core Additions in `crates/maelstrom-core/src/steering.rs`:
1. **`Pose2D`**:
   - 2D character spatial pose $(x, y, \theta)$ with 2D Euclidean distance, absolute angle to target $\text{atan2}(\Delta y, \Delta x)$, and normalized relative heading $\alpha \in [-\pi, \pi]$.
2. **`SteeringCommand`**:
   - Action intent structure output by the local controller: `forward_pulse_ms`, `turn_left_pulse_ms`, `turn_right_pulse_ms`, `reverse_pulse_ms`, `curvature`, `angle_error_rad`, `is_arrived`.
   - Adheres strictly to `Facts ≠ Authority ≠ Actions`.
3. **`PurePursuitController`**:
   - **Forward-Only Waypoint Search**: Finds the closest waypoint along the path and searches forward from `closest_idx` to prevent locking onto traversed waypoints behind the character.
   - **Continuous Curved Driving**: Evaluates path curvature $\kappa = \frac{2 \sin(\alpha)}{L_d}$. Rather than robotic "stop, turn 90°, walk" tank controls, it commands forward drive (`W`) while pulsing turn keys (`A` or `D`) bounded by cadence, driving along smooth arcs.
   - **Turn-in-Place Threshold**: For large angular errors ($|\alpha| > 48.7^\circ$), halts forward movement and turns in place before driving forward.
   - **Deadband Stabilization**: Deadband tolerance $|\alpha| \le 4.5^\circ$ suppresses micro-adjustments and eliminates left/right chattering.
   - **Arrival Deceleration**: Detects distance to final waypoint $\le R_{\text{arrival}}$ and transitions smoothly to `is_arrived = true`.
4. **`OdometryWatchdog`**:
   - Monitors physical displacement during commanded forward drive (`W`).
   - If forward motion is active for $\ge 1.5\text{s}$ but spatial displacement $\Delta d < 5.0$ units, declares `is_stuck = true`.
   - **Multi-Stage Unstuck Maneuver (`step_unstuck_maneuver`)**:
     - Phase 1: Reverse pulse (`S` for 500ms) to back away from obstacle geometry.
     - Phase 2: Lateral yaw turn (`A` for 350ms) to break contact normal.
     - Phase 3: Clears stuck flag and returns `None` to trigger dynamic path replanning.

---

## 2. Test Verification & Multi-Language Quality Gates

### Unit Tests (`crates/maelstrom-core/src/steering.rs`):
- `test_pose2d_relative_angle_calculations`: Verified relative angle $\alpha$ for forward ($0$), left ($+\pi/2$), and right ($-\pi/2$) targets.
- `test_pure_pursuit_arrival_and_idle`: Verified zero-motion arrival detection when distance $\le 10.0$ units.
- `test_pure_pursuit_continuous_curved_steering`: Verified forward pulse (`250ms`) combined with Left turn pulse for smooth curve.
- `test_pure_pursuit_turn_in_place_for_sharp_angles`: Verified forward suppression and in-place turn for sharp backward heading.
- `test_odometry_watchdog_stall_detection_and_recovery`: Verified stall detection at $1.6\text{s}$ and 2-phase unstuck pulse sequence.

### Integration Tests (`crates/maelstrom-core/tests/steering_tests.rs`):
- `test_integration_smooth_pure_pursuit_tracks_waypoint_path`: Verified 3-segment path traversal (straight drive $\to$ curved left transition $\to$ final arrival).
- `test_integration_odometry_watchdog_detects_obstacle_snag_and_executes_recovery`: Verified that moving characters do not trigger watchdog, while snagged characters trigger after $1.5\text{s}$ and execute reverse/turn recovery.
- `test_integration_stationary_player_never_triggers_watchdog`: Verified that idle players never trigger false positive stalls over 10 seconds.

### Quality Gates Execution:
- `cargo test -p maelstrom-core`: 34 unit tests + 10 integration tests passed (44 passed, 0 failed).
- `cargo test --workspace`: 99/99 tests passed across all 5 workspace crates.
- `cargo clippy -p maelstrom-core -- -D warnings`: 0 warnings, clean dev profile.
- `rustfmt --check`: 0 formatting changes required.
- `python tools/check_text_encoding.py`: 0 violations across repository.
- `python tools/check_project_board.py`: Valid, 30 workstreams checked.

---

## 3. Red Team / Blue Team Adversarial Self-Audit

### Audit Finding 1 (Critical Defect Discovered & Remediated During Red Team Run):
- **Red Team Attack**: In initial drafting, `select_lookahead_point` searched waypoints from index 0. When a character had reached $(50, 0)$ along a path starting at $(0, 0)$, waypoint 0 at $(0, 0)$ was distance $50.0 \ge L_d$ behind the character! The controller locked onto the behind waypoint, commanded a $180^\circ$ reversal turn, and stopped forward movement.
- **Blue Team Remediation**: Added closest-waypoint detection (`closest_idx`). The lookahead search now evaluates strictly forward from `closest_idx..`, completely eliminating backward waypoint latching. Verified by integration test `test_integration_smooth_pure_pursuit_tracks_waypoint_path`.

### Audit Finding 2:
- **Red Team Attack**: Can the watchdog trigger during intentional stationary waiting (e.g. at an NPC dialogue or bank)?
- **Blue Team Mitigation**: The watchdog tracking window only activates when `is_driving_forward == true`. In `test_integration_stationary_player_never_triggers_watchdog`, stationary idle over 20 observation cycles never triggered a false stall.

### Audit Finding 3:
- **Red Team Attack**: Does keyboard steering chatter/hunt across zero heading error?
- **Blue Team Mitigation**: A deadband tolerance of $0.08\text{ rad} \approx 4.5^\circ$ suppresses micro-pulses, ensuring stable straight-line driving without oscillations.

---

## Audit corrections (2026-11-19)
Checked against `steering.rs` and the tests; `cargo test -p maelstrom-core --offline` steering tests pass (5 unit tests in `steering.rs`, 3 in `tests/steering_tests.rs`).
- **Verified:** the deadband (0.08 rad), turn-in-place threshold (0.85 rad, about 48.7 degrees), 250 ms forward cadence, forward-only lookahead search from the closest waypoint, 1.5 s / 5.0 unit stall detection, and the 500 ms reverse then 350 ms left unstuck sequence all match the code. The controller is also constructed in `maelstrom_bridge.rs`.
- **"Arrival deceleration" is overstated.** There is no deceleration or speed profile: reaching the arrival radius returns an `arrived()` command with zero pulses. Movement is discrete key pulses, so "smooth arcs" means alternating `W` with `A`/`D` pulses sized from an assumed `turn_rate_rad_per_sec`.
- **The model is not calibrated to the game.** Turn rate, movement speed and the 25-unit lookahead are assumed constants; no recorded Wizard101 movement was used to fit them, and no test checks them against real displacement. The tests use hand-built poses and waypoint lists.
- **Closest-waypoint search is global.** `select_lookahead_point` scans the whole path each tick, so on a path that doubles back near itself the "closest" index can jump to a later segment. The fix described in Audit Finding 1 prevents locking onto waypoints behind a straight path but does not cover that case.
- **Test list details:** the doc says the stationary test ran "10 seconds"; the loop is 20 observation cycles. The "34 unit + 10 integration", "44" and "99/99" totals are historical and no longer match the tree.
- The watchdog and unstuck maneuver are logic-only here; nothing demonstrates recovery from a real in-game snag.
