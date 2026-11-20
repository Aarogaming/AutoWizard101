# Evidence Report: W101-ROLLOUT-DOCS-01

Status: **Corrected** (audited 2026-11-20; the specification presented design intent as implemented behavior and has been amended; see
"Audit note" at the end).

## 1. Summary
Authored formal architecture and systems specification for the Short-Horizon "Mr. House" Prediction & Rollout Engine in `docs/SHORT_HORIZON_PREDICTION_ROLLOUT.md`, and updated `docs/INDEX.md`.

## 2. Documented Architecture & Contracts
- **Mr. House Anticipation Loop**: Defines why reactive agents fail in server-authoritative environments with uncancelable dueling rings, non-teleportation constraints, and server tick latency.
- **Two-Layer Kinetic Reflex Architecture**:
  - Deterministic/Analytical Layer: Pure Pursuit, heading alignment, 'W' key pulses, 30s turn windows, safety interrupt lease releases.
  - Learned Predictor Layer: compact local models on `maelstrom-replay` traces for dynamic patrol forecasts (1-4s), kinematic snag prediction, and multi-turn combat outcome scoring.
- **Micro-Future Rollout Loop**:
  - Step 1: Capture `ShadowWorldState` snapshot (player pose, mount multiplier, local hazards within 150 units, Z-elevation).
  - Step 2: $K$-step discrete trajectory simulation across candidate actions (forward pulse, lateral arc, brake and hold).
  - Step 3: Feasibility and risk scoring (inverse-square aggro threat repulsion, Z-mismatches, zero-displacement snag penalties, goal progress).
  - Step 4: Single bounded proposal dispatch via `GameInputLease`.
- **Counterfactual & Model Surprise Logging**:
  - Computes spatial prediction residual $\mathbf{r} = \|\mathbf{p}_{\text{actual}} - \hat{\mathbf{p}}_{t+\Delta t}\|$.
  - Logs labeled model surprise events for unexpected combat or high residual.
  - Classifies root causes via Prediction Failure Taxonomy (`StaleEntityTTL`, `TerrainZFloorMismatch`, `PatrolTrajectoryCurvatureChange`, `InputLatencyJitter`, `ObstacleSnag`).
- **House's Clock & Compute Budgets**: Strict $\le 10\text{ms}$ per-tick rollout time budget; heavy training remains asynchronous in offline replay tools.
- **Epistemic Humility**: Widens hazard repulsion envelopes ($1.5\times$) under degraded sensory tiers (Tier 1 Visual/Acoustic).

## 3. Verification
- `docs/INDEX.md` updated with reference to `SHORT_HORIZON_PREDICTION_ROLLOUT.md`.
- `python tools/check_project_board.py` validated (30 workstreams, zero broken dependencies).
- `python tools/check_text_encoding.py` verified (0 violations).

## Audit note (2026-11-20, Claude)

`docs/INDEX.md` does reference the document (line for "Short-Horizon Prediction Rollout Engine"). The original text above is kept
unchanged. Compared with `predictive_rollout.rs` (see the [W101-ROLLOUT-CORE-02](W101-ROLLOUT-CORE-02.md) audit note for the probe
evidence):

**Corrections.**
1. **"Strict <= 10 ms per-tick rollout time budget" is not implemented.** `max_compute_budget_ms` is a field that nothing reads, there is
   no timer and no 8 ms truncation, and no rollout was timed.
2. **Step 1 and Step 4 describe things the code does not do.** There is no 150-unit sensory perimeter, floor or water height in
   `ShadowWorldState`, and `z` never changes in a rollout. The engine emits no "single bounded proposal": it returns a
   `CandidateEvaluation`, `PROPOSAL:Kinematics:PulseKey` appears nowhere in the code, and nothing connects the engine to the bridge or to
   `DesktopInputActuator`.
3. **The learned layer is not implemented.** There are no learned predictors, no 1-4 s patrol forecasts, no snag predictor (the snag
   term fires on pulses shorter than a step) and no combat outcome estimator in this module. The surprise events "form the empirical
   training corpus" only in intent: nothing records them.
4. **The uncertainty and Z-mismatch terms of the cost function are absent,** and the document's "5 to 15 discrete ticks" and "widens
   repulsion radii by 1.5" do not match the code (1-30 ticks; the aggro radius in the lethal test, by 1.5 for Tier 1 and 2.0 for Tier 2).
5. **The game-mechanic statements in section 1** (crossing a mob's invisible aggro perimeter triggers an instant, uncancelable
   transition into a 4-player duel; 30-second dueling timers) cite no source here and were not checked.
6. **Where the document was amended:** a status banner, a **Today** note under steps 1-4, section 4, section 5 and section 6, and
   "design only" on Layer 2. The module's own doc comments in `predictive_rollout.rs` were corrected to match (comments only).
