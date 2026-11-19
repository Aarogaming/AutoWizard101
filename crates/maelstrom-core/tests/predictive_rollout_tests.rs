//! Integration tests for the Short-Horizon "Mr. House" Prediction & Rollout Engine (`predictive_rollout`).

use maelstrom_core::ontology::{DegradationTier, Hazard};
use maelstrom_core::predictive_rollout::{
    CandidateAction, Pose3D, PredictionFailureCause, PredictiveRolloutEngine, ShadowWorldState,
};

#[test]
fn test_forward_pulse_kinematic_rollout_and_progress() {
    let engine = PredictiveRolloutEngine::new()
        .with_horizon(10, 0.10) // 10 steps of 0.1s = 1.0s horizon
        .with_residual_tolerance(8.0);

    // Initial pose facing along +X axis (yaw = 0.0)
    let shadow = ShadowWorldState::new(Pose3D::new(0.0, 0.0, 0.0, 0.0), 10_000);
    let goal = (100.0, 0.0, 0.0);

    // 400ms forward pulse holding 'W'
    let action = CandidateAction::forward(400);

    let eval = engine.simulate_action(&shadow, &action, goal);

    assert!(eval.is_safe);
    assert_eq!(eval.steps.len(), 10);

    // Speed = 25.0 units/s. Duration = 0.4s -> Expected displacement = 10.0 units
    assert!((eval.final_pose.x - 10.0).abs() < 1e-4);
    assert!((eval.final_pose.y - 0.0).abs() < 1e-4);
    assert!((eval.progress_gain - 10.0).abs() < 1e-4);
    assert!(eval.score > 0.0);
}

#[test]
fn test_mount_speed_multiplier_kinematics() {
    let engine = PredictiveRolloutEngine::new().with_horizon(10, 0.10);

    // Mount speed multiplier = 1.40 (+40% mount speed: 35.0 units/s)
    let shadow =
        ShadowWorldState::new(Pose3D::new(0.0, 0.0, 0.0, 0.0), 10_000).with_mount_multiplier(1.40);

    let goal = (100.0, 0.0, 0.0);
    let action = CandidateAction::forward(400); // 0.4s * 35.0 = 14.0 units

    let eval = engine.simulate_action(&shadow, &action, goal);

    assert!((eval.final_pose.x - 14.0).abs() < 1e-4);
    assert!((eval.progress_gain - 14.0).abs() < 1e-4);
}

#[test]
fn test_dynamic_mob_hazard_pruning_and_steering_avoidance() {
    let engine = PredictiveRolloutEngine::new().with_horizon(10, 0.10);

    // Mob stationed at (20.0, 5.0, 0.0) with aggro radius 10.0 units
    let mob = Hazard::dynamic_aggro(
        101,
        "FirecatAlley",
        (20.0, 5.0, 0.0),
        (0.0, 0.0, 0.0),
        10.0,
        30.0,
    );

    let shadow =
        ShadowWorldState::new(Pose3D::new(0.0, 0.0, 0.0, 0.0), 10_000).with_hazards(vec![mob]);

    let goal = (50.0, 0.0, 0.0);

    // Candidate A: Straight forward pulse into mob aggro radius (passes (20, 0), distance 5.0 <= 10.0)
    let cand_straight = CandidateAction::forward(1000);
    let eval_straight = engine.simulate_action(&shadow, &cand_straight, goal);
    assert!(
        !eval_straight.is_safe,
        "Straight trajectory must intersect aggro cylinder"
    );
    assert_eq!(eval_straight.score, -1_000_000.0);

    // Candidate B: Steer right with yaw rate -0.8 rad/s while moving forward (min distance 10.75 > 10.0)
    let cand_steer = CandidateAction::steer(-0.80, 1000);
    let eval_steer = engine.simulate_action(&shadow, &cand_steer, goal);
    assert!(eval_steer.is_safe, "Steering around mob must remain safe");
    assert!(
        eval_steer.score > eval_straight.score,
        "Safe steering must vastly outscore lethal straight collision"
    );

    // Selection must reject straight path and pick steering arc
    let selected = engine
        .evaluate_and_select(&shadow, goal, &[cand_straight, cand_steer])
        .expect("Must select safe steering candidate");

    assert!(matches!(
        selected.action,
        CandidateAction::SteerAndPulse { .. }
    ));
}

#[test]
fn test_brake_and_hold_safety_fallback_when_corridor_blocked() {
    let engine = PredictiveRolloutEngine::new().with_horizon(10, 0.10);

    // Mob covering whole width ahead at (10, 0, 0) with huge aggro 20.0
    let mob = Hazard::dynamic_aggro(
        102,
        "ColossusBoulevard",
        (10.0, 0.0, 0.0),
        (0.0, 0.0, 0.0),
        20.0,
        40.0,
    );

    let shadow =
        ShadowWorldState::new(Pose3D::new(0.0, 0.0, 0.0, 0.0), 10_000).with_hazards(vec![mob]);

    let goal = (60.0, 0.0, 0.0);
    let cand_forward = CandidateAction::forward(500);

    // Candidate forward is lethal
    let selected = engine
        .evaluate_and_select(&shadow, goal, &[cand_forward])
        .expect("Must fall back to BrakeAndHold when all branches lethal");

    assert!(matches!(
        selected.action,
        CandidateAction::BrakeAndHold { .. }
    ));
    assert_eq!(
        selected.final_pose.x, 0.0,
        "Player must remain stationary during brake"
    );
}

#[test]
fn test_epistemic_humility_hazard_expansion_under_degraded_tier() {
    let engine = PredictiveRolloutEngine::new().with_horizon(10, 0.10);

    // Mob at (10.0, 18.0, 0.0) with aggro 15.0
    let mob = Hazard::dynamic_aggro(
        103,
        "TritonAvenue",
        (10.0, 18.0, 0.0),
        (0.0, 0.0, 0.0),
        15.0,
        35.0,
    );

    let goal = (50.0, 0.0, 0.0);
    // Straight path moves along y = 0.0; at x = 10.0, distance to mob is 18.0
    let action = CandidateAction::forward(500);

    // Case 1: Tier 0 Full Ontology (aggro = 15.0; clearance = 18.0 - 15.0 = 3.0 > 0 -> SAFE)
    let shadow_tier0 = ShadowWorldState::new(Pose3D::new(0.0, 0.0, 0.0, 0.0), 10_000)
        .with_hazards(vec![mob.clone()])
        .with_degradation_tier(DegradationTier::Tier0FullOntology);

    let eval_tier0 = engine.simulate_action(&shadow_tier0, &action, goal);
    assert!(
        eval_tier0.is_safe,
        "Tier 0 should consider 3.0 clearance safe"
    );

    // Case 2: Tier 1 Visual/Acoustic (1.5x margin expansion -> effective aggro = 22.5; clearance = 18.0 - 22.5 = -4.5 <= 0 -> LETHAL)
    let shadow_tier1 = ShadowWorldState::new(Pose3D::new(0.0, 0.0, 0.0, 0.0), 10_000)
        .with_hazards(vec![mob])
        .with_degradation_tier(DegradationTier::Tier1VisualAcoustic);

    let eval_tier1 = engine.simulate_action(&shadow_tier1, &action, goal);
    assert!(
        !eval_tier1.is_safe,
        "Degraded sensory tier must widen hazard margins and reject tight clearance"
    );
}

#[test]
fn test_model_surprise_spatial_residual_and_cause_classification() {
    let engine = PredictiveRolloutEngine::new().with_residual_tolerance(8.0);

    let shadow = ShadowWorldState::new(Pose3D::new(0.0, 0.0, 0.0, 0.0), 10_000);
    let goal = (50.0, 0.0, 0.0);
    let action = CandidateAction::forward(800); // Expected final x = 20.0

    let eval = engine.simulate_action(&shadow, &action, goal);

    // Case 1: Real observation closely matches prediction (residual 1.5 <= 8.0)
    let actual_good = Pose3D::new(19.0, 0.5, 0.0, 0.0);
    let surprise_good = engine.calculate_surprise(&eval, actual_good, false, 10_800);
    assert!(!surprise_good.is_surprise);
    assert!(surprise_good.failure_cause.is_none());

    // Case 2: Character snagged against wall (actual x = 4.0, residual = 16.0 > 8.0)
    let actual_snag = Pose3D::new(4.0, 0.0, 0.0, 0.0);
    let surprise_snag = engine.calculate_surprise(&eval, actual_snag, false, 10_800);
    assert!(surprise_snag.is_surprise);
    assert_eq!(
        surprise_snag.failure_cause,
        Some(PredictionFailureCause::ObstacleSnag)
    );

    // Case 3: Unexpected combat pull despite path being clear
    let surprise_combat = engine.calculate_surprise(&eval, eval.final_pose, true, 10_800);
    assert!(surprise_combat.is_surprise);
    assert_eq!(
        surprise_combat.failure_cause,
        Some(PredictionFailureCause::PatrolTrajectoryCurvatureChange)
    );
}

#[test]
fn test_bridge_vertical_separation_not_pruned_as_lethal() {
    let engine = PredictiveRolloutEngine::new().with_horizon(10, 0.10);

    // Mob on street floor at (10.0, 0.0, 0.0) with aggro 15.0, vertical tolerance 30.0
    let mob = Hazard::dynamic_aggro(
        104,
        "WizardCityCommonsBridge",
        (10.0, 0.0, 0.0),
        (0.0, 0.0, 0.0),
        15.0,
        35.0,
    );

    // Player crossing overhead bridge at z = 45.0 (> 30.0 vertical tolerance)
    let shadow =
        ShadowWorldState::new(Pose3D::new(0.0, 0.0, 45.0, 0.0), 10_000).with_hazards(vec![mob]);

    let goal = (40.0, 0.0, 45.0);
    let action = CandidateAction::forward(800);

    let eval = engine.simulate_action(&shadow, &action, goal);

    assert!(
        eval.is_safe,
        "Player vertically separated on an elevated bridge must not be falsely pruned as lethal"
    );
    assert!(
        eval.score > 0.0,
        "Valid forward bridge traversal should achieve positive score"
    );
}
