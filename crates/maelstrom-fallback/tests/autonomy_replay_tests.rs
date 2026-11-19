//! Integration and regression tests for AutonomyReplayHarness.
//! Evaluates multi-hour simulated session traces against MaelstromAutonomyBridge offline.

use maelstrom_fallback::{
    AutonomyReplayHarness, AutonomyReplaySession, MaelstromAutonomyBridge, NativeHostState,
    ReplayFrame,
};
use serde_json::json;
use std::env;

#[allow(clippy::too_many_arguments)]
fn make_snapshot_frame(
    timestamp_ms: u64,
    connected: bool,
    health: i32,
    health_max: i32,
    mana: i32,
    mana_max: i32,
    potions: f64,
    combat_active: bool,
    pos_x: f64,
    pos_y: f64,
    pos_z: f64,
    expected_intent: Option<&str>,
) -> ReplayFrame {
    let now_str = "2026-11-18T22:30:00.0000000Z";
    let snapshot_json = json!({
        "connected": connected,
        "facts": {
            "gameId": "wizard101",
            "targetId": "12345/67890",
            "source": "reviewed-w101-ui-tree",
            "observedUtc": now_str,
            "combatActive": combat_active,
            "zoneName": "WizardCity_Commons",
            "spatialPosition": [pos_x, pos_y, pos_z],
            "wizardSubject": {
                "profileId": "prof-1",
                "name": "Alex",
                "school": "Storm",
                "generation": 1,
                "verifiedCharacterId": "char-1"
            }
        },
        "aliases": [
            {
                "id": "health",
                "dataType": "REAL",
                "value": health as f64,
                "quality": "fresh",
                "targetId": "12345/67890",
                "observedUtc": now_str,
                "source": "reviewed-w101-ui-tree",
                "gameId": "wizard101"
            },
            {
                "id": "health.max",
                "dataType": "DINT",
                "value": health_max,
                "quality": "fresh",
                "targetId": "12345/67890",
                "observedUtc": now_str,
                "source": "reviewed-w101-ui-tree",
                "gameId": "wizard101"
            },
            {
                "id": "mana",
                "dataType": "REAL",
                "value": mana as f64,
                "quality": "fresh",
                "targetId": "12345/67890",
                "observedUtc": now_str,
                "source": "reviewed-w101-ui-tree",
                "gameId": "wizard101"
            },
            {
                "id": "mana.max",
                "dataType": "DINT",
                "value": mana_max,
                "quality": "fresh",
                "targetId": "12345/67890",
                "observedUtc": now_str,
                "source": "reviewed-w101-ui-tree",
                "gameId": "wizard101"
            },
            {
                "id": "potions",
                "dataType": "REAL",
                "value": potions,
                "quality": "fresh",
                "targetId": "12345/67890",
                "observedUtc": now_str,
                "source": "reviewed-w101-ui-tree",
                "gameId": "wizard101"
            }
        ]
    });

    let parse_time = std::time::UNIX_EPOCH + std::time::Duration::from_secs(1795041000);
    let state = NativeHostState::from_snapshot_json_at(&snapshot_json, parse_time);
    ReplayFrame::new(timestamp_ms, state, expected_intent.map(str::to_string))
}

#[test]
fn test_empty_session_replay() {
    let temp_dir = env::temp_dir().join("maelstrom_replay_empty");
    let mut bridge = MaelstromAutonomyBridge::new(&temp_dir).expect("Bridge creation must succeed");

    let harness = AutonomyReplayHarness::new();
    let session = AutonomyReplaySession::new("empty_session");

    let (metrics, results) = harness.replay_session(&session, &mut bridge);
    assert_eq!(metrics.total_ticks, 0);
    assert_eq!(metrics.decision_accuracy_pct, 100.0);
    assert_eq!(metrics.replan_count, 0);
    assert!(results.is_empty());
}

#[test]
fn test_multi_hour_simulated_trace_accuracy_and_metrics() {
    let temp_dir = env::temp_dir().join("maelstrom_replay_multi_hour");
    let mut bridge = MaelstromAutonomyBridge::new(&temp_dir).expect("Bridge creation must succeed");

    // Initialize active goal and waypoints for navigation phases
    bridge.blackboard.active_goal = Some("ExploreCommons".into());
    bridge.blackboard.active_waypoints = vec![(2000.0, 150.0, 0.0)];

    let mut session = AutonomyReplaySession::new("canonical_session_01");

    // Construct 1000 frames: 100ms per frame = 100,000ms (spanning realistic transit, combat, and recovery)
    // Phase 1 (Ticks 0..400): Healthy navigation along waypoints
    for tick in 0..400 {
        let t = tick as u64 * 100;
        let x = 100.0 + (tick as f64) * 0.5;
        let y = 150.0;
        session.push_frame(make_snapshot_frame(
            t,
            true,
            500,
            500,
            120,
            120,
            3.0,
            false,
            x,
            y,
            0.0,
            Some("PROPOSAL:StepWaypoint"),
        ));
    }

    // Phase 2 (Ticks 400..700): Dynamic combat preemption
    for tick in 400..700 {
        let t = tick as u64 * 100;
        session.push_frame(make_snapshot_frame(
            t,
            true,
            420,
            500,
            110,
            120,
            3.0,
            true, // in combat!
            300.0,
            150.0,
            0.0,
            Some("PROPOSAL:CombatTurn"),
        ));
    }

    // Phase 3 (Ticks 700..800): Post-combat vital dip triggering potion recovery
    for tick in 700..800 {
        let t = tick as u64 * 100;
        session.push_frame(make_snapshot_frame(
            t,
            true,
            80, // Health critical (< 25% of 500)
            500,
            10, // Mana critical (< 25% of 120)
            120,
            3.0,   // Potions available
            false, // Out of combat
            300.0,
            150.0,
            0.0,
            Some("PROPOSAL:DrinkPotion"),
        ));
    }

    // Phase 4 (Ticks 800..1000): Resumed healthy transit to destination
    for tick in 800..1000 {
        let t = tick as u64 * 100;
        let x = 300.0 + ((tick - 800) as f64) * 0.5;
        let y = 150.0;
        session.push_frame(make_snapshot_frame(
            t,
            true,
            500,
            500,
            120,
            120,
            2.0, // Used 1 potion
            false,
            x,
            y,
            0.0,
            Some("PROPOSAL:StepWaypoint"),
        ));
    }

    assert_eq!(session.len(), 1000);
    assert_eq!(session.duration_ms(), 99_900);

    let harness = AutonomyReplayHarness::new();
    let (metrics, results) = harness.replay_session(&session, &mut bridge);

    // Verify Decision Accuracy
    assert_eq!(metrics.total_ticks, 1000);
    assert_eq!(metrics.labeled_ticks, 1000);
    assert!(
        metrics.decision_accuracy_pct >= 99.5,
        "Decision accuracy must exceed 99.5%, got {:.2}%",
        metrics.decision_accuracy_pct
    );
    assert_eq!(results.len(), 1000);

    // Verify Replanning Detection
    // Shifts: FollowWaypoints -> CombatSelectCard -> DrinkPotion/Success -> FollowWaypoints (at least 3 replans)
    assert!(
        metrics.replan_count >= 2,
        "Expected at least 2 phase transition replans, got {}",
        metrics.replan_count
    );

    // Verify Steering Curvature Bounds
    assert!(metrics.total_steering_commands > 0);
    assert!(
        metrics.max_steering_curvature < 5.0,
        "Peak steering curvature must be bounded (< 5.0), got {:.3}",
        metrics.max_steering_curvature
    );

    // Verify Kinematic Jerk Metrics (combat card selection generated kinematic clicks)
    assert!(metrics.total_kinematic_plans > 0);
    assert!(
        metrics.max_kinematic_jerk < 10.0,
        "Kinematic jerk must be within human physical limits (< 10.0), got {:.3}",
        metrics.max_kinematic_jerk
    );

    // Ensure zero anomalous warnings
    assert!(
        metrics.warnings.is_empty(),
        "Replay generated unexpected warnings: {:?}",
        metrics.warnings
    );
}

#[test]
fn test_disconnected_frame_handling() {
    let temp_dir = env::temp_dir().join("maelstrom_replay_disconnected");
    let mut bridge = MaelstromAutonomyBridge::new(&temp_dir).expect("Bridge creation must succeed");

    let mut session = AutonomyReplaySession::new("disconnected_session");
    // Feed 10 disconnected frames
    for tick in 0..10 {
        session.push_frame(make_snapshot_frame(
            tick * 100,
            false, // Disconnected!
            500,
            500,
            120,
            120,
            3.0,
            false,
            0.0,
            0.0,
            0.0,
            Some("Failure"), // Expects Failure when disconnected
        ));
    }

    let harness = AutonomyReplayHarness::new();
    let (metrics, results) = harness.replay_session(&session, &mut bridge);

    assert_eq!(metrics.total_ticks, 10);
    assert_eq!(metrics.decision_accuracy_pct, 100.0);
    assert_eq!(results[0].bt_status, "Failure");
    assert!(!results[0].connected);
}
