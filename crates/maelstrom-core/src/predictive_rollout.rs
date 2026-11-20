//! `predictive_rollout`: Short-Horizon "Mr. House" Prediction & Rollout Engine.
//!
//! Evaluates caller-supplied candidate actions over a short horizon (default 10 ticks of 0.1 s;
//! `with_horizon` clamps to 1-30 ticks of 0.01-0.5 s), rejects trajectories whose clearance to a hazard's
//! aggro cylinder reaches zero, penalizes the repulsion-band threat cost and a forward pulse that moves
//! nothing, and returns the best safe candidate. Nothing dispatches it: it is not connected to the
//! `GameInputLease` or any actuator. There is no Z-mismatch term in the score.
//!
//! Computes spatial prediction residuals ($\mathbf{r} = \|\mathbf{p}_{\text{actual}} - \hat{\mathbf{p}}\|$),
//! flagging model surprise events for counterfactual replay training (`maelstrom-replay`); nothing
//! records or consumes them yet.
//!
//! Adheres strictly to `Facts ≠ Authority ≠ Actions`.

use alloc::vec::Vec;
use serde::{Deserialize, Serialize};

use crate::ontology::{DegradationTier, Hazard};

/// 3D Spatial Pose with orientation yaw in radians.
#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize)]
pub struct Pose3D {
    pub x: f64,
    pub y: f64,
    pub z: f64,
    pub yaw_rad: f64,
}

impl Pose3D {
    pub const fn new(x: f64, y: f64, z: f64, yaw_rad: f64) -> Self {
        Self { x, y, z, yaw_rad }
    }

    pub fn distance_2d(&self, other: &Self) -> f64 {
        let dx = self.x - other.x;
        let dy = self.y - other.y;
        let d2 = dx * dx + dy * dy;
        if d2 <= 0.0 {
            0.0
        } else {
            d2.sqrt()
        }
    }

    pub fn distance_3d(&self, other: &Self) -> f64 {
        let dx = self.x - other.x;
        let dy = self.y - other.y;
        let dz = self.z - other.z;
        let d2 = dx * dx + dy * dy + dz * dz;
        if d2 <= 0.0 {
            0.0
        } else {
            d2.sqrt()
        }
    }

    /// Unit forward direction vector on the 2D ground plane based on yaw.
    pub fn forward_vector_2d(&self) -> (f64, f64) {
        // In Wizard101 coordinates, yaw 0 is along +X, pi/2 is along +Y (or standard Cartesian)
        (self.yaw_rad.cos(), self.yaw_rad.sin())
    }
}

/// Candidate kinematic action primitive for micro-future rollout.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum CandidateAction {
    /// Forward movement pulse holding key (default 'W').
    ForwardPulse { duration_ms: u32, key: char },
    /// Turning and forward pulse with constant angular yaw velocity.
    SteerAndPulse {
        yaw_rate_rad_s: f64,
        duration_ms: u32,
        key: char,
    },
    /// Brake / hold in place (zero actuation, waiting for patrol gap).
    BrakeAndHold { duration_ms: u32 },
}

impl CandidateAction {
    pub fn forward(duration_ms: u32) -> Self {
        Self::ForwardPulse {
            duration_ms,
            key: 'W',
        }
    }

    pub fn steer(yaw_rate_rad_s: f64, duration_ms: u32) -> Self {
        Self::SteerAndPulse {
            yaw_rate_rad_s,
            duration_ms,
            key: 'W',
        }
    }

    pub fn brake(duration_ms: u32) -> Self {
        Self::BrakeAndHold { duration_ms }
    }

    pub fn duration_ms(&self) -> u32 {
        match self {
            Self::ForwardPulse { duration_ms, .. } => *duration_ms,
            Self::SteerAndPulse { duration_ms, .. } => *duration_ms,
            Self::BrakeAndHold { duration_ms } => *duration_ms,
        }
    }
}

/// Fast shadow world state capturing the immediate environment for rollout.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct ShadowWorldState {
    pub player_pose: Pose3D,
    pub mount_speed_multiplier: f64,
    pub nearby_hazards: Vec<Hazard>,
    pub degradation_tier: DegradationTier,
    pub timestamp_ms: u64,
}

impl ShadowWorldState {
    pub fn new(player_pose: Pose3D, timestamp_ms: u64) -> Self {
        Self {
            player_pose,
            mount_speed_multiplier: 1.0,
            nearby_hazards: Vec::new(),
            degradation_tier: DegradationTier::Tier0FullOntology,
            timestamp_ms,
        }
    }

    pub fn with_mount_multiplier(mut self, mult: f64) -> Self {
        self.mount_speed_multiplier = mult.max(0.5);
        self
    }

    pub fn with_hazards(mut self, hazards: Vec<Hazard>) -> Self {
        self.nearby_hazards = hazards;
        self
    }

    pub fn with_degradation_tier(mut self, tier: DegradationTier) -> Self {
        self.degradation_tier = tier;
        self
    }
}

/// A discrete step along a simulated micro-future trajectory.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct RolloutStep {
    pub step_idx: usize,
    pub delta_t_s: f64,
    pub pose: Pose3D,
    pub min_hazard_clearance: f64,
    pub threat_cost: u8,
    pub is_lethal: bool,
}

/// Evaluation summary of a candidate trajectory branch.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct CandidateEvaluation {
    pub action: CandidateAction,
    pub steps: Vec<RolloutStep>,
    pub final_pose: Pose3D,
    pub progress_gain: f64,
    pub min_hazard_clearance: f64,
    pub total_threat_penalty: f64,
    pub snag_penalty: f64,
    pub score: f64,
    pub is_safe: bool,
}

/// Root-cause taxonomy for spatial and environmental prediction failures.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum PredictionFailureCause {
    StaleEntityTTL,
    TerrainZFloorMismatch,
    PatrolTrajectoryCurvatureChange,
    InputLatencyJitter,
    ObstacleSnag,
}

/// Recorded counterfactual observation comparing predicted vs actual reality.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct ModelSurpriseObservation {
    pub timestamp_ms: u64,
    pub action_executed: CandidateAction,
    pub predicted_pose: Pose3D,
    pub actual_pose: Pose3D,
    pub residual_distance: f64,
    pub unexpected_combat: bool,
    pub is_surprise: bool,
    pub failure_cause: Option<PredictionFailureCause>,
}

/// Concrete Short-Horizon "Mr. House" Prediction & Rollout Engine.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct PredictiveRolloutEngine {
    pub horizon_steps: usize,
    pub step_dt_s: f64,
    pub base_speed_units_s: f64,
    pub residual_tolerance: f64,
    /// Declared per-tick budget. Nothing reads or enforces it, and no rollout is timed.
    pub max_compute_budget_ms: f64,
}

impl Default for PredictiveRolloutEngine {
    fn default() -> Self {
        Self {
            horizon_steps: 10,
            step_dt_s: 0.10,
            base_speed_units_s: 25.0,
            residual_tolerance: 8.0,
            max_compute_budget_ms: 10.0,
        }
    }
}

impl PredictiveRolloutEngine {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn with_horizon(mut self, steps: usize, dt_s: f64) -> Self {
        self.horizon_steps = steps.clamp(1, 30);
        self.step_dt_s = dt_s.clamp(0.01, 0.50);
        self
    }

    pub fn with_residual_tolerance(mut self, tol: f64) -> Self {
        self.residual_tolerance = tol.max(0.5);
        self
    }

    /// Simulates a single candidate action forward across the configured horizon.
    pub fn simulate_action(
        &self,
        shadow: &ShadowWorldState,
        action: &CandidateAction,
        goal_pos: (f64, f64, f64),
    ) -> CandidateEvaluation {
        let mut steps = Vec::with_capacity(self.horizon_steps);
        let mut cur_pose = shadow.player_pose;

        let effective_speed = self.base_speed_units_s * shadow.mount_speed_multiplier;
        let mut is_lethal = false;
        let mut min_clearance = f64::MAX;
        let mut total_threat = 0.0;

        let action_duration_s = (action.duration_ms() as f64) / 1000.0;

        // Epistemic humility: widen hazard margins under degraded tier
        let hazard_margin_factor = match shadow.degradation_tier {
            DegradationTier::Tier0FullOntology => 1.0,
            DegradationTier::Tier1VisualAcoustic => 1.5,
            DegradationTier::Tier2SafeHalt => 2.0,
        };

        for step_idx in 1..=self.horizon_steps {
            let elapsed_t = (step_idx as f64) * self.step_dt_s;

            // Only actuate if within action duration
            if elapsed_t <= action_duration_s {
                match action {
                    CandidateAction::ForwardPulse { .. } => {
                        let (fwd_x, fwd_y) = cur_pose.forward_vector_2d();
                        cur_pose.x += fwd_x * effective_speed * self.step_dt_s;
                        cur_pose.y += fwd_y * effective_speed * self.step_dt_s;
                    }
                    CandidateAction::SteerAndPulse { yaw_rate_rad_s, .. } => {
                        cur_pose.yaw_rad += yaw_rate_rad_s * self.step_dt_s;
                        let (fwd_x, fwd_y) = cur_pose.forward_vector_2d();
                        cur_pose.x += fwd_x * effective_speed * self.step_dt_s;
                        cur_pose.y += fwd_y * effective_speed * self.step_dt_s;
                    }
                    CandidateAction::BrakeAndHold { .. } => {
                        // Position remains stationary
                    }
                }
            }

            // Hazard evaluation at elapsed timestamp
            let mut step_threat_cost = 0u8;
            for hazard in &shadow.nearby_hazards {
                let cost =
                    hazard.evaluate_threat_cost((cur_pose.x, cur_pose.y, cur_pose.z), elapsed_t);
                if cost > step_threat_cost {
                    step_threat_cost = cost;
                }

                // Compute physical clearance to mob center if within vertical tolerance
                let vert_diff = (cur_pose.z - hazard.position.2).abs();
                if vert_diff <= hazard.vertical_tolerance {
                    let pred_mob_x =
                        hazard.position.0 + hazard.velocity.0 * elapsed_t.clamp(0.0, 3.0);
                    let pred_mob_y =
                        hazard.position.1 + hazard.velocity.1 * elapsed_t.clamp(0.0, 3.0);
                    let dx = cur_pose.x - pred_mob_x;
                    let dy = cur_pose.y - pred_mob_y;
                    let dist = (dx * dx + dy * dy).sqrt();

                    let effective_aggro = hazard.aggro_radius * hazard_margin_factor;
                    let clearance = dist - effective_aggro;
                    if clearance < min_clearance {
                        min_clearance = clearance;
                    }

                    if clearance <= 0.0 {
                        is_lethal = true;
                    }
                }
            }

            total_threat += step_threat_cost as f64;

            steps.push(RolloutStep {
                step_idx,
                delta_t_s: elapsed_t,
                pose: cur_pose,
                min_hazard_clearance: min_clearance,
                threat_cost: step_threat_cost,
                is_lethal,
            });
        }

        // Progress gain toward goal
        let initial_dist = {
            let dx = shadow.player_pose.x - goal_pos.0;
            let dy = shadow.player_pose.y - goal_pos.1;
            (dx * dx + dy * dy).sqrt()
        };
        let final_dist = {
            let dx = cur_pose.x - goal_pos.0;
            let dy = cur_pose.y - goal_pos.1;
            (dx * dx + dy * dy).sqrt()
        };
        let progress_gain = initial_dist - final_dist;

        // Snag check: if pulsing forward but zero progress
        let net_displacement = shadow.player_pose.distance_2d(&cur_pose);
        let snag_penalty = if matches!(action, CandidateAction::ForwardPulse { .. })
            && net_displacement < 0.1
            && action_duration_s > 0.05
        {
            500.0
        } else {
            0.0
        };

        let is_safe = !is_lethal && min_clearance > 0.0;

        let score = if !is_safe {
            -1_000_000.0 // Strictly prune lethal intersections
        } else {
            (progress_gain * 2.0) - (total_threat * 0.1) - snag_penalty
        };

        CandidateEvaluation {
            action: action.clone(),
            steps,
            final_pose: cur_pose,
            progress_gain,
            min_hazard_clearance: min_clearance,
            total_threat_penalty: total_threat,
            snag_penalty,
            score,
            is_safe,
        }
    }

    /// Evaluates all candidate actions and returns the optimal safe action.
    /// Falls back to a fixed 500 ms BrakeAndHold if no candidate is safe. The fallback is returned even when
    /// it is itself unsafe (for example when the player already stands inside an aggro radius).
    pub fn evaluate_and_select(
        &self,
        shadow: &ShadowWorldState,
        goal_pos: (f64, f64, f64),
        candidates: &[CandidateAction],
    ) -> Option<CandidateEvaluation> {
        if candidates.is_empty() {
            return None;
        }

        let mut evaluations: Vec<CandidateEvaluation> = candidates
            .iter()
            .map(|action| self.simulate_action(shadow, action, goal_pos))
            .collect();

        // Sort descending by score
        evaluations.sort_by(|a, b| {
            b.score
                .partial_cmp(&a.score)
                .unwrap_or(core::cmp::Ordering::Equal)
        });

        // Find highest-scoring safe trajectory
        if let Some(best_safe) = evaluations.iter().find(|e| e.is_safe).cloned() {
            Some(best_safe)
        } else {
            // All forward trajectories are lethal: simulate BrakeAndHold as safety fallback
            let brake = CandidateAction::brake(500);
            Some(self.simulate_action(shadow, &brake, goal_pos))
        }
    }

    /// Calculates spatial residual and model surprise against the actual observation.
    ///
    /// The residual is measured against the horizon's final pose whatever the observation time; the per-step
    /// poses and `now_ms` are not used to align them. Only three of the five failure causes are ever assigned
    /// (`StaleEntityTTL` and `InputLatencyJitter` are not).
    pub fn calculate_surprise(
        &self,
        predicted: &CandidateEvaluation,
        actual_pose: Pose3D,
        in_combat: bool,
        now_ms: u64,
    ) -> ModelSurpriseObservation {
        let residual = predicted.final_pose.distance_3d(&actual_pose);
        let unexpected_combat = in_combat && predicted.is_safe;
        let is_surprise = unexpected_combat || (residual > self.residual_tolerance);

        let failure_cause = if unexpected_combat {
            Some(PredictionFailureCause::PatrolTrajectoryCurvatureChange)
        } else if residual > self.residual_tolerance {
            let dz = (predicted.final_pose.z - actual_pose.z).abs();
            if dz > 10.0 {
                Some(PredictionFailureCause::TerrainZFloorMismatch)
            } else {
                Some(PredictionFailureCause::ObstacleSnag)
            }
        } else {
            None
        };

        ModelSurpriseObservation {
            timestamp_ms: now_ms,
            action_executed: predicted.action.clone(),
            predicted_pose: predicted.final_pose,
            actual_pose,
            residual_distance: residual,
            unexpected_combat,
            is_surprise,
            failure_cause,
        }
    }
}
