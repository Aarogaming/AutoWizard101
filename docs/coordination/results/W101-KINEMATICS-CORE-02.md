# Evidence: W101-KINEMATICS-CORE-02

**Task**: Native Humanized Kinematic Input Profiles & Trajectory Generation  
**Owner**: `root`  
**Workstream**: `W101-NAVIGATION`  
**Status**: Released  
**Date**: 2026-11-18  
**Owned Paths**:
- `crates/maelstrom-core/src/kinematics.rs`
- `crates/maelstrom-core/src/lib.rs`
- `crates/maelstrom-core/tests/kinematics_tests.rs`
- `docs/coordination/results/W101-KINEMATICS-CORE-02.md`

---

## 1. Architectural Scope & Implementation

The objective of `W101-KINEMATICS-CORE-02` is implementing native, zero-dependency biomechanical trajectory and input profile generators in `crates/maelstrom-core/src/kinematics.rs`. Naive game automation relies on instant cursor teleportation, constant-velocity linear moves, and fixed-delay sleep timers, which trigger heuristic server-side anti-cheat flags and fail minimum-jerk biomechanical realism.

### Core Additions in `crates/maelstrom-core/src/kinematics.rs`:
1. **`Point2D` & `TimedPoint` & `KinematicPlan`**:
   - `Point2D`: 2D spatial coordinate with Euclidean distance and zero-allocation math.
   - `TimedPoint`: Timestamped screen coordinate waypoint (`x`, `y`, `elapsed_ms`).
   - `KinematicPlan`: Ordered sequence of waypoints, total trajectory duration, and post-movement click/tap dwell duration (`dwell_ms`).
2. **`FastPrng`**:
   - Lightweight, deterministic XorShift64* pseudo-random number generator.
   - Generates uniform ranges $[min, max]$, Box-Muller Gaussian variables $\mathcal{N}(\mu, \sigma)$, and log-normal variables $\exp(\mu + \sigma Z)$ with zero standard library dependencies in no_std fallback.
3. **`WindMouseGenerator`**:
   - Physical simulation of mouse motion with virtual mass, gravitational pull toward target, and Ornstein-Uhlenbeck stochastic wind drift:
     $$\mathbf{v}_{k+1} = \mathbf{v}_k + \mathbf{F}_{\text{grav}} + \mathbf{v}_{\text{wind}, k+1}$$
   - Dynamic Proximity Damping: When distance to target $d < D_{\text{threshold}}$, wind is linearly attenuated and gravity damped, preventing high-velocity orbital oscillations and producing natural deceleration and micro-overshoot.
4. **`BezierTrajectoryGenerator`**:
   - Cubic Bézier trajectory generation with minimum-jerk 5th-order polynomial velocity profiles:
     $$s(\tau) = 10\tau^3 - 15\tau^4 + 6\tau^5, \quad \tau \in [0, 1]$$
   - Control points $P_1$ and $P_2$ perturbed laterally along normal vectors by Gaussian scatter, producing realistic arcs rather than robotic straight lines.
5. **`FittsLawModel`**:
   - Computes movement duration based on target distance and target bounding width:
     $$T = a + b \cdot \log_2\left(1 + \frac{D}{W}\right)$$
   - Calibrated to empirical human parameters ($a = 120\text{ms}$, $b = 160\text{ms}$).
   - Guarded against zero-width targets with $W \ge 1.0\text{px}$.
6. **`DwellProfile` & `sample_target_in_rect`**:
   - Calibrated log-normal dwell distributions for mouse clicks ($\mu = \ln(75)$, $\sigma = 0.22$, range $[40, 180]\text{ms}$), key taps ($\mu = \ln(85)$, range $[45, 220]\text{ms}$), and cadence delays.
   - `sample_target_in_rect`: 2D Gaussian point selection inside UI bounding boxes avoiding dead-center clicking.

---

## 2. Red Team / Blue Team Adversarial Audit

| Threat Vector (Red Team) | Vulnerability Analysis | Mitigation (Blue Team Implementation) | Verification Status |
| :--- | :--- | :--- | :--- |
| **WindMouse Limit Cycle Trapping** | Gravitational pull and wind forces can enter a resonant equilibrium, causing the cursor to orbit the target indefinitely. | Implemented dynamic proximity damping below $D_{\text{threshold}}$ (wind reduced, gravity scaled), proximity snap threshold ($d < 2.5\text{px}$ breaks loop), and a hard step cap (`max_steps: 3000`). | **Verified**: Tests `test_wind_mouse_generates_smooth_curved_path` and `test_integration_wind_mouse_long_distance_traversal` confirm terminal convergence within $1.0\text{px}$. |
| **Division by Zero & NaN Propagation** | Zero-distance trajectory requests ($D=0$) or zero-width UI targets cause $0/0$ division in normal vectors and $\log_2(0)$. | If $D < 10^{-4}\text{px}$, generators immediately return a single-point terminal plan. Fitts's law model clamps $W \ge 1.0\text{px}$ and $D \ge 0.0$. | **Verified**: Verified via unit test boundary checks; zero NaNs or Infs generated. |
| **Extreme Outlier Dwell Times** | Log-normal distributions have long positive tails that could theoretically sample a multi-second delay, freezing the actuator. | Every `DwellProfile` enforces strict `clamp(min_ms, max_ms)`. | **Verified**: Tested across 1,000 samples in `test_integration_dwell_jitter_statistical_distribution`; 100% of samples respect $[40, 180]\text{ms}$ bounds. |
| **Input Authority Leakage** | Kinematic generators executing direct Win32 API calls (`SendInput`) could bypass Maelstrom's safety gates and input lease. | `kinematics.rs` strictly adheres to `Facts ≠ Authority ≠ Actions`. It produces immutable `KinematicPlan` data structures without OS input dependencies. Dispatch authority remains exclusively with the platform actuator under verified input leases. | **Verified**: Architectural separation verified; `maelstrom-core` retains zero OS input imports. |

---

## 3. Test Verification & Multi-Language Quality Gates

### Unit Tests (`crates/maelstrom-core/src/kinematics.rs`):
- `test_point2d_distance_and_timed_point`: Verified 2D Euclidean distance and conversion.
- `test_fast_prng_distribution_properties`: Verified uniform range and Gaussian mean convergence ($N=1000, \mu \approx 50.0$).
- `test_fitts_law_target_acquisition_duration`: Verified close vs far target duration scaling ($280\text{ms}$ vs $912\text{ms}$).
- `test_dwell_profile_bounds_and_sampling`: Verified 100 samples strictly respect $[40, 180]\text{ms}$ clamping.
- `test_wind_mouse_generates_smooth_curved_path`: Verified non-linear path curvature and terminal accuracy ($< 1.0\text{px}$).
- `test_bezier_trajectory_minimum_jerk_velocity`: Verified asymmetric minimum-jerk acceleration (middle velocity $>$ launch velocity and terminal velocity).
- `test_sample_target_in_rect_avoids_corners_and_exact_center`: Verified 50 samples lie within bounding box but avoid exact dead center.

### Integration Tests (`crates/maelstrom-core/tests/kinematics_tests.rs`):
- `test_integration_combat_card_selection_pipeline`: End-to-end integration between `CombatGeometry` (card 3 of 7 in hand), target rectangle sampling, Fitts's law duration modeling, and cubic Bézier minimum-jerk trajectory generation with monotonic timestamps.
- `test_integration_wind_mouse_long_distance_traversal`: Traversal from $(60, 80)$ to $(740, 530)$ across screen; verified deceleration near target, terminal accuracy, and finite coordinates.
- `test_integration_dwell_jitter_statistical_distribution`: 1,000 samples of mouse click dwell; verified empirical mean $\approx 76.8\text{ms}$ aligns with log-normal theoretical expectation, and confirmed positive skewness ($> 0.1$).

### Quality Gates Execution:
- `cargo test -p maelstrom-core`: 45 unit tests + 16 integration tests passed (61 passed, 0 failed).
- `cargo test --workspace`: 116/116 tests passed across all 5 workspace crates.
- `cargo clippy --workspace --all-targets -- -D warnings`: 0 warnings, clean across all targets.
- `rustfmt`: Formatted all owned paths without modifying unowned code.
- `python tools/check_project_board.py`: Project board valid (30 workstreams, zero broken evidence links).
- `python tools/check_text_encoding.py`: 0 violations across repository.
