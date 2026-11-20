# Evidence: W101-KINEMATICS-DOCS-01

**Task**: Relative Documentation Synchronization: Humanized Kinematic Input Profiles  
**Owner**: `root`  
**Workstream**: `W101-NAVIGATION`  
**Status**: Draft documentation; several statements describe intent rather than the code (see the Audit note)  
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


---

## Audit note (2026-11-19, Claude)

Checked against `docs/KINEMATIC_INPUT_PROFILES.md`, `crates/maelstrom-core/src/kinematics.rs`,
`crates/maelstrom-fallback/src/maelstrom_bridge.rs`, `ProjectMaelstrom/ProjectMaelstrom/Utilities/DesktopInputActuator.cs`
and the other documents this task edited. Nothing was run against a live game. The status above was changed from
"Released" because the documents describe a pipeline that the code does not complete.

**Verified.** The new document exists, `docs/INDEX.md` links it, and `docs/DESKTOP_INPUT_OPTIONS.md` and section 4 of
`docs/ROUTINE_EXECUTION_MODEL.md` contain the added text. The Fitts worked examples check out (ID 4.39 gives 822.8 ms;
ID 1.22 gives 315.6 ms). The log-normal constants match the code (click mu 4.317, sigma 0.22). `kinematics.rs` imports
only `alloc::vec::Vec`, so it does no OS input. `python tools/check_project_board.py` (30 workstreams) and
`python tools/check_text_encoding.py` (0 violations) were re-run today and give the figures quoted above.

**Corrections.**
1. **The cursor path never reaches the game.** The summary that crosses from Rust to C# (`KinematicPlanSummary`) holds the
   point count, total duration, dwell and the final target; `From<&KinematicPlan>` drops every waypoint.
   `DesktopInputActuator.DispatchKinematicClickAsync` reads only the target and the dwell: it presses at the target, holds,
   and releases. `TotalDurationMs` and `PointCount` are never read in C#. Section 4 of `ROUTINE_EXECUTION_MODEL.md`
   ("provides timestamped coordinate waypoints to the platform actuator") and the architecture diagram in
   `KINEMATIC_INPUT_PROFILES.md` therefore describe intent. What survives to dispatch is the Gaussian target scatter and the
   click hold time. Both documents are corrected.
2. **WindMouse has no caller outside tests.** The bridge uses `sample_target_in_rect`, `FittsLawModel` and
   `BezierTrajectoryGenerator` only.
3. **Keypress jitter is not implemented.** `DwellProfile::key_tap` and `inter_keystroke_cadence` have no caller and
   `steering.rs` has no random or log-normal sampling. "Keypresses and mouse click hold times are modulated" is true of the
   click hold only.
4. **Wrong WindMouse wording** in `KINEMATIC_INPUT_PROFILES.md` section 2.1: "exponentially suppressed" (the code scales wind
   linearly, `wind * d / threshold`) and "damping coefficient increases" (there is no damping coefficient; gravity is scaled by
   `max(d / threshold, 0.4)` and the "velocity damping" comment in the code is a speed clamp). Fixed in place, along with
   the quoted "95th percentile [48 ms, 120 ms]", which is a central 95% interval of about 49-115 ms (the code comment said
   45-140 ms).
5. **The no_std fallbacks are not statistically valid.** `next_log_normal` uses a three-term Taylor series for `exp`, which gives
   about 28 ms where the true median is 75 ms, so every click dwell would clamp to the 40 ms floor (key taps 45 ms, cadence
   60 ms): a constant, which is the isochronous timing the document says it removes. This is unreachable today because the
   crate does not build without std (see `W101-KINEMATICS-CORE-02`) and every test runs with std.
6. **Code comments corrected** in `kinematics.rs`: the Bezier generator called its symmetric polynomial "asymmetric", and the
   dwell profiles quoted percentile ranges that do not follow from their mu and sigma.

**What this evidence does not prove.** Static reading plus arithmetic. It does not show how a running app behaves, whether
the click dwell or target scatter make input harder to distinguish from a person, or that the click path is wired into a live
build. No input was sent.
