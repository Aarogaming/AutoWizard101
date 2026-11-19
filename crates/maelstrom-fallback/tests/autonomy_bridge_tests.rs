//! Integration tests for MaelstromAutonomyBridge.
//! Verifies perception-decision loop coupling between native host snapshot frames,
//! BehaviorTree state transitions, PurePursuit steering command generation,
//! and Kinematic trajectory generation.

use maelstrom_fallback::{MaelstromAutonomyBridge, NativeHostState};
use serde_json::json;

#[allow(clippy::too_many_arguments)]
fn make_test_snapshot(
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
) -> NativeHostState {
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
            },
            {
                "id": "nativeIdle",
                "dataType": "BOOL",
                "value": true,
                "quality": "fresh",
                "targetId": "12345/67890",
                "observedUtc": now_str,
                "source": "reviewed-w101-ui-tree",
                "gameId": "wizard101"
            }
        ],
        "unavailable": []
    });

    // Parse at fixed timestamp so it passes 2-second freshness check
    let parse_time = std::time::UNIX_EPOCH + std::time::Duration::from_secs(1795041000);
    NativeHostState::from_snapshot_json_at(&snapshot_json, parse_time)
}

#[test]
fn test_autonomy_bridge_disconnected_invalidation() {
    let mut bridge = MaelstromAutonomyBridge::new("C:\\temp\\dummy_deck").unwrap();
    let state = make_test_snapshot(false, 500, 500, 100, 100, 2.0, false, 0.0, 0.0, 0.0);

    let result = bridge.process_state(&state);
    assert!(!result.connected);
    assert_eq!(result.bt_status, "Failure");
    assert!(result.proposed_intent.is_none());
    assert!(result.steering_command.is_none());
    assert!(result.kinematic_plan.is_none());
}

#[test]
fn test_autonomy_bridge_navigation_cycle_and_steering() {
    let mut bridge = MaelstromAutonomyBridge::new("C:\\temp\\dummy_deck").unwrap();
    let state = make_test_snapshot(true, 500, 500, 100, 100, 2.0, false, 100.0, 100.0, 0.0);

    // Setup active goal and forward waypoint directly ahead along X-axis
    bridge.blackboard.active_goal = Some("ExploreCommons".into());
    bridge.blackboard.active_waypoints = vec![(200.0, 100.0, 0.0)];

    let result = bridge.process_state(&state);
    assert!(result.connected);
    assert_eq!(result.bt_status, "Running");
    assert_eq!(
        result.proposed_intent.as_deref(),
        Some("PROPOSAL:StepWaypoint")
    );

    let steering = result
        .steering_command
        .expect("Steering command must be emitted");
    assert_eq!(steering.forward_pulse_ms, 250);
    assert_eq!(steering.turn_left_pulse_ms, 0);
    assert_eq!(steering.turn_right_pulse_ms, 0);
    assert!(!steering.is_arrived);
}

#[test]
fn test_autonomy_bridge_low_vital_triggers_potion() {
    let mut bridge = MaelstromAutonomyBridge::new("C:\\temp\\dummy_deck").unwrap();
    // Mana at 10/100 (10%), potions available
    let state = make_test_snapshot(true, 500, 500, 10, 100, 2.0, false, 100.0, 100.0, 0.0);

    bridge.blackboard.active_goal = Some("ExploreCommons".into());
    bridge.blackboard.active_waypoints = vec![(200.0, 100.0, 0.0)];

    let result = bridge.process_state(&state);
    assert!(result.connected);
    assert_eq!(result.bt_status, "Success");
    assert_eq!(
        result.proposed_intent.as_deref(),
        Some("PROPOSAL:DrinkPotion"),
        "Low mana must preempt navigation and propose potion drinking"
    );
    assert!(
        result.steering_command.is_none(),
        "Motion must halt while recovering vitals"
    );
}

#[test]
fn test_autonomy_bridge_combat_interruption_and_kinematic_card_plan() {
    let mut bridge = MaelstromAutonomyBridge::new("C:\\temp\\dummy_deck").unwrap();
    // Combat active
    let state = make_test_snapshot(true, 500, 500, 100, 100, 2.0, true, 100.0, 100.0, 0.0);

    bridge.blackboard.active_goal = Some("ExploreCommons".into());
    bridge.blackboard.active_waypoints = vec![(200.0, 100.0, 0.0)];

    let result = bridge.process_state(&state);
    assert!(result.connected);
    assert!(result.in_combat);
    assert_eq!(result.bt_status, "Running");
    assert_eq!(
        result.proposed_intent.as_deref(),
        Some("PROPOSAL:CombatTurn"),
        "Combat initiation must preempt navigation with top priority"
    );

    let kinematic = result
        .kinematic_plan
        .expect("Kinematic card click plan must be emitted");
    assert!(kinematic.point_count > 10);
    assert!(kinematic.total_duration_ms > 200.0);
    assert!(kinematic.dwell_ms >= 40.0 && kinematic.dwell_ms <= 180.0);
}

#[test]
fn test_autonomy_bridge_combat_dueling_pass_intent() {
    let mut bridge = MaelstromAutonomyBridge::new("C:\\temp\\dummy_deck").unwrap();
    let state = make_test_snapshot(true, 500, 500, 100, 100, 2.0, true, 100.0, 100.0, 0.0);

    // Set combat turn action to Pass on blackboard
    bridge.blackboard.combat_turn_action = Some(String::from("Pass"));

    let result = bridge.process_state(&state);
    assert!(result.in_combat);
    assert_eq!(
        result.proposed_intent.as_deref(),
        Some("PROPOSAL:CombatTurn:Pass")
    );

    let kinematic = result
        .kinematic_plan
        .expect("Kinematic pass click plan must be emitted");
    assert!(kinematic.point_count > 5);
    // Target should be near pass button (406.4, 477.6)
    assert!((kinematic.target_x - 406.4).abs() < 25.0);
    assert!((kinematic.target_y - 477.6).abs() < 25.0);
}

#[test]
fn test_autonomy_bridge_combat_dueling_select_target_intent() {
    let mut bridge = MaelstromAutonomyBridge::new("C:\\temp\\dummy_deck").unwrap();
    let state = make_test_snapshot(true, 500, 500, 100, 100, 2.0, true, 100.0, 100.0, 0.0);

    // Set combat turn action to SelectTarget:0 (Enemy 0)
    bridge.blackboard.combat_turn_action = Some(String::from("SelectTarget:0"));

    let result = bridge.process_state(&state);
    assert_eq!(
        result.proposed_intent.as_deref(),
        Some("PROPOSAL:CombatTurn:SelectTarget:0")
    );

    let kinematic = result
        .kinematic_plan
        .expect("Kinematic select target plan must be emitted");
    // Enemy 0 pedestal is at (208.0, 60.0)
    assert!((kinematic.target_x - 208.0).abs() < 30.0);
    assert!((kinematic.target_y - 60.0).abs() < 30.0);
}

#[test]
fn test_autonomy_bridge_combat_dueling_play_card_intent() {
    let mut bridge = MaelstromAutonomyBridge::new("C:\\temp\\dummy_deck").unwrap();
    let state = make_test_snapshot(true, 500, 500, 100, 100, 2.0, true, 100.0, 100.0, 0.0);

    // Set combat turn action to PlayCard:0/1 (Single card in hand)
    bridge.blackboard.combat_turn_action = Some(String::from("PlayCard:0/1"));

    let result = bridge.process_state(&state);
    assert_eq!(
        result.proposed_intent.as_deref(),
        Some("PROPOSAL:CombatTurn:PlayCard:0/1")
    );

    let kinematic = result
        .kinematic_plan
        .expect("Kinematic play card plan must be emitted");
    // Hand card (0, 1) in 720p is at (688.0, 390.0)
    assert!((kinematic.target_x - 688.0).abs() < 35.0);
    assert!((kinematic.target_y - 390.0).abs() < 40.0);
}

#[test]
fn test_autonomy_bridge_combat_dueling_viewport_targets() {
    use maelstrom_core::combat_geometry::CombatGeometry;

    // Test 1: Hand card coordinates in 720p
    let (c0_x, c0_y) = CombatGeometry::get_hand_card_coordinates_720p(0, 4).unwrap();
    assert!(c0_x > 0.0 && c0_x < 1280.0);
    assert!(c0_y > 0.0 && c0_y < 720.0);

    // Test 2: Target coordinates in 720p
    let (t0_x, t0_y) = CombatGeometry::calculate_target_coordinates_720p(0).unwrap();
    assert!((t0_x - 208.0).abs() < 1e-4);
    assert!((t0_y - 60.0).abs() < 1e-4);

    let (t1_x, t1_y) = CombatGeometry::calculate_target_coordinates_720p(1).unwrap();
    assert!((t1_x - 486.4).abs() < 1e-4);
    assert!((t1_y - 60.0).abs() < 1e-4);

    // Test 3: Pass button in 720p
    let (pass_x, pass_y) = CombatGeometry::get_pass_button_coordinates_720p();
    assert!((pass_x - 406.4).abs() < 1e-4);
    assert!((pass_y - 477.6).abs() < 1e-4);
}
