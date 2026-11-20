# Claim Evidence: W101-REPLAY-HARNESS-DOCS-01

Status: **Released** (docs only; audited 2026-11-20: a spec describing checks the code only partly performs, see "Audit note").

## 1. Overview
- **Task ID**: `W101-REPLAY-HARNESS-DOCS-01`
- **Workstream**: `CORE-TRANSITIONS`
- **Owner**: `coordinator`
- **Scope**: Formalization of the Offline Autonomy Replay Harness documentation, covering session trace structures, decision accuracy metrics, replanning frequency, trajectory curvature smoothness, and kinematic minimum-jerk verification.

## 2. Changes Made
1. **Created Documentation**:
   - `docs/OFFLINE_AUTONOMY_REPLAY.md`: Complete specification detailing:
     - Separation between offline verification and live qualification.
     - `ReplayFrame` schema holding timestamped `SnapshotPayload` and expected intent labels.
     - Decision accuracy formula ($\ge 99.5\%$ target).
     - Replanning rate and sub-goal invalidation tracking.
     - Continuous trajectory curvature metric $\kappa(t) = \frac{|\dot{x}\ddot{y} - \dot{y}\ddot{x}|}{(\dot{x}^2 + \dot{y}^2)^{3/2}}$.
     - Kinematic jerk metric compliance ($s(\tau) = 10\tau^3 - 15\tau^4 + 6\tau^5$).
2. **Updated Cross-References**:
   - `docs/ROUTINE_EXECUTION_MODEL.md`: Added Section 8 detailing offline replay integration.
   - `docs/INDEX.md`: Added indexed entry for `docs/OFFLINE_AUTONOMY_REPLAY.md`.

## 3. Verification & Compliance
- **Text Encoding Check**: `python tools/check_text_encoding.py` $\rightarrow$ 0 violations.
- **Project Board Check**: `python tools/check_project_board.py` $\rightarrow$ Valid (30 workstreams, zero broken references).
- **Claim Integrity**: Bounded strictly to declared documentation paths.

---

## Audit note (2026-11-20, Claude)
Checked by reading `autonomy_replay.rs`, `maelstrom-replay.rs`, `steering.rs` and the tests, and by a throwaway release probe (not committed). The
spec was written before the harness; treat it as design intent.
- **`ReplayFrame` schema:** the spec showed `snapshot: SnapshotPayload`. The code has `state: NativeHostState`; no `SnapshotPayload` type exists, and the
  state has no heading, duel-circle or dialogue fields.
- **"Decision accuracy >= 99.5%":** an unlabeled session reports 100% and PASS (the default). The report and the 1000-frame test use 99.5, while
  `run_batch` and the `maelstrom-replay` exit code use 95.0 (a 96% session prints FAIL in the report and PASS in the batch matrix). Labels are
  hand-written or follow from the harness's own inputs.
- **"Replanning rate and sub-goal invalidation tracking":** the code counts any change of `proposed_intent` between consecutive intent-bearing
  frames. It does not separate planned completions from aborts and does not involve GOAP. The report's replan status is a constant PASS against a
  `<= 30.0 /hr` figure with no source.
- **"Continuous trajectory curvature metric":** the harness reads the Pure Pursuit controller's own $\kappa = 2\sin\alpha / \max(d, 1)$ in world units; it
  differentiates no trajectory and ignores mouse paths. $|\kappa| \le 2.0$ always, so the 5.0 gate cannot fail.
- **"Kinematic jerk metric compliance":** the harness sees only a plan summary and computes `dist / T^3 * 0.001` from a fixed screen centre; the minimum-jerk
  polynomial in the spec is not used.
- **"Multi-hour" traces and Tier 1 GOAP:** the longest test trace is 99.9 s of hand-written frames and the replay does not run GOAP.
- **Section 3.5 invariants** (zero panics, click bounds 0..1280 by 0..720, valid status transitions) are not checked by the harness.
- **Verified:** `docs/OFFLINE_AUTONOMY_REPLAY.md` exists; `ROUTINE_EXECUTION_MODEL.md` Section 8 and the `INDEX.md` entry exist; the quoted AGENTS.md
  invariant matches AGENTS.md word for word; the minimum-jerk polynomial and its third derivative $60 - 360\tau + 360\tau^2$ are correct mathematics.
