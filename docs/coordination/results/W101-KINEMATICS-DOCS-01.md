# Evidence: W101-KINEMATICS-DOCS-01

**Task**: Relative Documentation Synchronization: Humanized Kinematic Input Profiles  
**Owner**: `root`  
**Workstream**: `W101-NAVIGATION`  
**Status**: Released  
**Date**: 2026-11-18  
**Owned Paths**:
- `docs/KINEMATIC_INPUT_PROFILES.md`
- `docs/DESKTOP_INPUT_OPTIONS.md`
- `docs/ROUTINE_EXECUTION_MODEL.md`
- `docs/INDEX.md`
- `docs/coordination/results/W101-KINEMATICS-DOCS-01.md`

---

## 1. Scope & Synchronization Summary

Prior to implementing the core kinematics engine in `crates/maelstrom-core/src/kinematics.rs`, this task established the mathematical formulations, biomechanical motivation, and architectural boundaries across project documentation.

### Synchronized Documents:
1. **`docs/KINEMATIC_INPUT_PROFILES.md`** (New Core Architecture Doc):
   - Detailed biomechanical motivations contrasting robotic synthetic inputs (instantaneous teleportation, linear constant-velocity paths, uniform delay timers) with human central nervous system movement properties.
   - Formulated the **WindMouse** physics simulation: gravitational attraction vector toward target, Ornstein-Uhlenbeck stochastic wind drift, dynamic deceleration and dampening under proximity threshold $D_{\text{threshold}}$.
   - Formulated **Cubic Bézier Trajectories** with minimum-jerk velocity profiles ($s(\tau) = 10\tau^3 - 15\tau^4 + 6\tau^5$) for rapid UI interactions.
   - Formulated **Fitts's Law Target Acquisition Duration** ($T = a + b \log_2(1 + D/W)$) linking movement duration to target distance and bounding width.
   - Formulated **Log-Normal Temporal Jitter** for mouse click dwell times ($\mu = \ln(75\text{ms})$, $\sigma = 0.22$) and keystroke pulse durations.
   - Enforced the `Facts ≠ Authority ≠ Actions` separation: trajectory plans output timestamped coordinates (`KinematicPlan`) with zero direct hardware actuation or OS API coupling.
2. **`docs/DESKTOP_INPUT_OPTIONS.md`**:
   - Linked low-level Windows input transports (`SendInput`, Raw Input, VHF) to kinematic trajectory modeling, clarifying that transport validity does not prevent robotic behavioral detection.
3. **`docs/ROUTINE_EXECUTION_MODEL.md`**:
   - Integrated kinematic motion and biomechanical input profiles into Section 4, explaining how Action Nodes propose physical interactions that are shaped by `kinematics.rs` before dispatch by the platform actuator under verified input leases.
4. **`docs/INDEX.md`**:
   - Added reference entry for `docs/KINEMATIC_INPUT_PROFILES.md`.

---

## 2. Quality Gates Execution

- `python tools/check_project_board.py`: Project board valid (30 workstreams, all evidence links resolved).
- `python tools/check_text_encoding.py`: 0 violations across repository.
- Zero code modifications or unstaged regressions.
