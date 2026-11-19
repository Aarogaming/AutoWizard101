# Short-Horizon "Mr. House" Prediction & Rollout Engine

## 1. Operational Rationale: Anticipation vs. Reaction

In server-authoritative virtual worlds like Wizard101, reactive agents—those that act only *after* observing a state transition—suffer from fatal systemic vulnerabilities:
- Stepping across a roaming mob's invisible aggro perimeter triggers an instant, uncancelable transition into a 4-player dueling circle.
- Navigating narrow street corridors without projecting dynamic mob paths causes sudden collisions around corners.
- Continuing to hold movement keys against a geometry snag or loading wall wastes valuable time and inflates kinematic drift.

Because teleportation, speedhacking, and memory injection are strictly forbidden under Maelstrom's working agreement, the runtime cannot rewrite physics or force server state. Instead, it must operate like **Mr. House's predictive mainframe**: continuously rolling out short-horizon micro-futures from its trusted world model, evaluating feasibility and risk across candidate actions, and dispatching only the single trajectory that maximizes objective progress while guaranteeing non-intersection with dynamic hazard envelopes.

```mermaid
flowchart TD
    subgraph Trusted Reality Model
        Shadow[ShadowWorldState: Player Pose, Mount, Nearby Hazards, Z-Floor]
        DegTier[Epistemic Degradation Tier 0-2]
    end

    subgraph House Rollout Loop
        Gen[Candidate Action Generator: Forward Pulse, Lateral Offset, Brake/Wait]
        Sim[K-Step Micro-Future Rollout: 5 to 15 Discrete Ticks]
        Score[Cost & Threat Evaluator: Aggro Repulsion, Z-Mismatches, Snags]
        Select[Optimal Branch Selection]
    end

    subgraph Actuation & Surprise Plane
        Lease[GameInputLease Dispatch: Single Bounded Action]
        NextObs[Next Real World Observation]
        Residual["Compute Surprise: Residual = ||Actual - Predicted||"]
        Trace[maelstrom-replay Counterfactual Logging]
    end

    Shadow --> Gen
    DegTier --> Score
    Gen --> Sim
    Sim --> Score
    Score --> Select
    Select --> Lease
    Lease --> NextObs
    NextObs --> Residual
    Select -. Predicted Future .-> Residual
    Residual --> Trace
```

---

## 2. Two-Layer Kinetic Reflex Architecture

To prevent over-engineering and retain deterministic auditability, Maelstrom splits actuation into two distinct layers:

### Layer 1: Deterministic / Analytical Reflexes (No Training Required)
- **Kinematic Steering**: Pure Pursuit waypoint tracking, heading alignment ($\Delta \theta = \text{target\_yaw} - \text{current\_yaw}$), and bounded `W` key pulse durations.
- **Turn Arbitration**: Dueling circle countdown windows (30-second timers), pedestal target arbitration, and deck profile switching.
- **Safety Interrupts**: Instantaneous `GameInputLease` release upon loading screens, window focus loss, or human mouse intervention ($\ge 3\text{s}$ quiet period required before resumption).
- **Rule**: Never replace exact mathematical solutions with opaque neural weights where analytical models are provably optimal.

### Layer 2: Learned Short-Horizon Local Predictors (Compact / Replay-Trained)
Where environmental dynamics are messy, non-linear, or subject to client latency:
1. **Dynamic Patrol & Aggro Rollouts (1–4 seconds ahead)**: Projects non-linear mob patrolling trajectories along sidewalk vs. street corridors when mob spacing is tight.
2. **Kinematic Execution & Snag Prediction**: Predicts actual net displacement for a key-hold pulse given mount speed, local frame rate, and incline angles, detecting snags *before* spending seconds colliding against walls.
3. **Combat Outcome Estimator**: Forecasts multi-turn combat outcomes, scoring whether a damage line clears the duel circle cleanly vs. risking health depletion or a flee spiral.

---

## 3. The Short-Horizon Micro-Future Rollout Loop

Every planning cycle (50ms–100ms), the engine executes a rapid four-stage rollout:

### Step 1: Capture Fast Shadow World State
Extracts a lightweight snapshot of the current manifold state:
- Player pose: $\mathbf{p} = (x, y, z, \text{yaw})$.
- Kinematic attributes: mount velocity multiplier $v_{\text{mount}}$ (e.g. $1.0\times$ walking, $1.4\times$ equipped mount).
- Local dynamic hazards: positions, velocities, and aggro radii of mobs within a 150-unit sensory perimeter.
- Elevation constraints: surveyed floor height $Z_{\text{floor}}$ and water plane $Z_{\text{water}}$.

### Step 2: Branching Micro-Future Simulation
From the shadow state, simulates $K$ discrete steps (default $K = 10$ steps at $\Delta t = 0.1\text{s}$, spanning a $1.0\text{s}$ horizon) across candidate action primitives:
1. **Kinetic Forward Pulse**: Maintain heading, pulse `W` for duration $\tau$.
2. **Lateral Offset Arc**: Apply yaw steering rate $\omega$ while pulsing `W` to veer around an obstacle.
3. **Brake and Hold**: Issue zero input, holding current coordinate to allow a roaming mob patrol to clear a street crossing.

### Step 3: Feasibility & Risk Scoring
Each simulated trajectory branch $B_i$ is evaluated against an objective cost function:

$$S(B_i) = w_{\text{progress}} \cdot \Delta d_{\text{goal}} - w_{\text{threat}} \cdot C_{\text{threat}} - w_{\text{snag}} \cdot C_{\text{snag}} - w_{\text{unc}} \cdot \sigma_{\text{uncertainty}}$$

Where:
- $\Delta d_{\text{goal}} = \|\mathbf{p}_0 - \mathbf{p}_{\text{goal}}\| - \|\mathbf{p}_K - \mathbf{p}_{\text{goal}}\|$ (progress toward waypoint).
- $C_{\text{threat}}$: Sum of inverse-square repulsion penalties from projected mob aggro cylinders ($Cost \propto \frac{1}{(d - r_{\text{aggro}})^2}$). If any tick enters $d \le r_{\text{aggro}}$, the branch is marked **lethal** and pruned.
- $C_{\text{snag}}$: Penalty applied if predicted displacement is near zero despite positive pulse input.
- $\sigma_{\text{uncertainty}}$: Epistemic penalty widened under degraded sensory tiers.

### Step 4: Bounded Dispatch via `GameInputLease`
The highest-scoring non-lethal candidate trajectory is selected. The engine emits a single bounded actuation proposal (`PROPOSAL:Kinematics:PulseKey`), which the C# `DesktopInputActuator` executes under the exclusive lease.

---

## 4. Counterfactual & Model Surprise Logging (`maelstrom-replay`)

At the instant an action is dispatched, the rollout engine records its **expected future state** $\hat{\mathbf{p}}_{t + \Delta t}$.

When the next real sensory observation arrives at $t + \Delta t$, the engine computes the **spatial prediction residual**:

$$\mathbf{r} = \|\mathbf{p}_{\text{actual}} - \hat{\mathbf{p}}_{t + \Delta t}\|$$

- **Normal Tracking ($\mathbf{r} \le \text{tolerance}$)**: Model expectations verified; execution confidence reinforced.
- **Model Surprise ($\mathbf{r} > \text{tolerance}$ or Unexpected Combat)**:
  - Automatically logged as a counterfactual event in the session trace.
  - Classified by the **Prediction Failure Taxonomy**:
    1. `StaleEntityTTL`: Mob moved differently because cached entity observation was stale.
    2. `TerrainZFloorMismatch`: Character stalled on a curb or staircase height transition.
    3. `PatrolTrajectoryCurvatureChange`: Roaming mob turned at a patrol waypoint unexpectedly.
    4. `InputLatencyJitter`: Window message dispatch stutter caused key release delay.
    5. `ObstacleSnag`: Undocumented static geometry collision stopped forward motion.
  - These labeled surprise events form the exact empirical training corpus for local offline models.

---

## 5. Compute & Latency Budgets ("House's Clock")

A prediction that arrives too late is worse than no prediction. The rollout loop adheres to strict computational budgets:
- **Maximum Per-Tick Budget**: $\le 10\text{ ms}$.
- **Pruning Discipline**: If $K$-step branch expansion exceeds $8\text{ ms}$, the engine truncates remaining branches and defaults to the safest known analytical trajectory (or `BrakeAndHold`).
- **Separation of Concerns**: Heavy batch training, dataset clustering, and multi-hour trace regressions run offline in `maelstrom-replay --batch`, never inside the real-time sensory-motor loop.

---

## 6. Epistemic Humility & Calibration Under Uncertainty

When sensory trust degrades (e.g. `RealityWorldModel` demotes to `Tier1VisualAcoustic` due to memory hook failure or camera occlusion):
- Mob aggro margins are dynamically expanded:
  $$r_{\text{repulsion}} \leftarrow r_{\text{repulsion}} \cdot 1.5$$
- The rollout engine favors conservative branches: pausing at sidewalk edges, taking wider turning radii, and requiring larger patrol gaps before attempting street crossings.
