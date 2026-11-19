# Evidence Report: W101-ROLLOUT-DOCS-01

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
