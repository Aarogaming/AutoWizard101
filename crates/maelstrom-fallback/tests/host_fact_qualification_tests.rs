//! Combat, zone and position reach the decision step only as fresh, typed, identity-matched aliases
//! (`combat.active`, `zone.name`, `position.x|y|z`), exactly like the vitals. Raw keys in the snapshot's
//! `facts` object are never read, an unknown fact stays unknown (reported as `null`, never `false`, an
//! invented zone or a (0, 0, 0) pose), and a cycle without a known combat state and pose makes no proposal.
//!
//! The live host publishes none of these aliases today (`JobFactCatalog.cs` defines no combat, zone or
//! position fact), so every fixture here is simulated.
//!
//! Results are inspected as JSON so the assertions pin the serialized shape that host IPC logging sees.

use maelstrom_fallback::{MaelstromAutonomyBridge, NativeHostState};
use serde_json::{json, Value};
use std::time::{Duration, SystemTime, UNIX_EPOCH};

const OBSERVED: &str = "2026-11-18T22:30:00.0000000Z";

/// The parse time that makes `OBSERVED` exactly current (same constant the other bridge tests use).
fn parse_time(offset_s: u64) -> SystemTime {
    UNIX_EPOCH + Duration::from_secs(1_795_041_000 + offset_s)
}

fn alias(id: &str, data_type: &str, value: Value) -> Value {
    json!({
        "id": id,
        "dataType": data_type,
        "value": value,
        "quality": "fresh",
        "targetId": "12345/67890",
        "observedUtc": OBSERVED,
        "source": "reviewed-w101-ui-tree",
        "gameId": "wizard101"
    })
}

fn vitals(health: f64, mana: f64) -> Vec<Value> {
    vec![
        alias("health", "REAL", json!(health)),
        alias("health.max", "DINT", json!(500)),
        alias("mana", "REAL", json!(mana)),
        alias("mana.max", "DINT", json!(100)),
        alias("potions", "REAL", json!(2.0)),
    ]
}

fn typed_world(combat: bool, zone: &str, pos: (f64, f64, f64)) -> Vec<Value> {
    vec![
        alias("combat.active", "BOOL", json!(combat)),
        alias("zone.name", "STRING", json!(zone)),
        alias("position.x", "REAL", json!(pos.0)),
        alias("position.y", "REAL", json!(pos.1)),
        alias("position.z", "REAL", json!(pos.2)),
    ]
}

/// `raw` adds the three legacy keys to the `facts` object; they must make no difference.
fn snapshot(
    connected: bool,
    raw: Option<(bool, &str, (f64, f64, f64))>,
    aliases: Vec<Value>,
) -> Value {
    let mut facts = json!({
        "gameId": "wizard101",
        "targetId": "12345/67890",
        "source": "reviewed-w101-ui-tree",
        "observedUtc": OBSERVED
    });
    if let Some((combat, zone, pos)) = raw {
        facts["combatActive"] = json!(combat);
        facts["zoneName"] = json!(zone);
        facts["spatialPosition"] = json!([pos.0, pos.1, pos.2]);
    }
    json!({ "connected": connected, "facts": facts, "aliases": aliases, "unavailable": [] })
}

fn state_at(snapshot: &Value, offset_s: u64) -> NativeHostState {
    NativeHostState::from_snapshot_json_at(snapshot, parse_time(offset_s))
}

fn bridge_with_route() -> MaelstromAutonomyBridge {
    let mut bridge = MaelstromAutonomyBridge::new("C:\\temp\\dummy_deck").unwrap();
    bridge.blackboard.active_goal = Some("ExploreCommons".into());
    bridge.blackboard.active_waypoints = vec![(200.0, 100.0, 0.0)];
    bridge
}

fn cycle(bridge: &mut MaelstromAutonomyBridge, state: &NativeHostState) -> Value {
    serde_json::to_value(bridge.process_state(state)).unwrap()
}

#[test]
fn raw_facts_without_typed_aliases_do_not_reach_the_decision_step() {
    // The legacy keys claim combat, another zone and a far position; none of it is a qualified alias.
    let snap = snapshot(
        true,
        Some((true, "Elsewhere", (500.0, 500.0, 0.0))),
        vitals(500.0, 100.0),
    );
    let mut bridge = bridge_with_route();
    let result = cycle(&mut bridge, &state_at(&snap, 0));

    assert_ne!(
        result["in_combat"],
        json!(true),
        "raw combatActive was used"
    );
    assert_ne!(
        result["current_zone"],
        json!("Elsewhere"),
        "raw zoneName was used"
    );
    assert!(result["proposed_intent"].is_null(), "{result}");
    assert!(result["steering_command"].is_null(), "{result}");
    assert!(result["kinematic_plan"].is_null(), "{result}");
}

#[test]
fn fresh_typed_aliases_are_used() {
    let mut aliases = vitals(500.0, 100.0);
    aliases.extend(typed_world(true, "WizardCity_Commons", (100.0, 100.0, 0.0)));
    let snap = snapshot(true, None, aliases);
    let mut bridge = bridge_with_route();
    let result = cycle(&mut bridge, &state_at(&snap, 0));

    assert_eq!(result["in_combat"], json!(true), "{result}");
    assert_eq!(
        result["current_zone"],
        json!("WizardCity_Commons"),
        "{result}"
    );
    assert!(
        result["proposed_intent"]
            .as_str()
            .is_some_and(|s| s.starts_with("PROPOSAL:CombatTurn")),
        "{result}"
    );
}

#[test]
fn stale_typed_facts_do_not_drive_proposals() {
    // Everything is internally consistent but 600 s old when it is parsed.
    let mut aliases = vitals(500.0, 100.0);
    aliases.extend(typed_world(true, "WizardCity_Commons", (100.0, 100.0, 0.0)));
    let snap = snapshot(
        true,
        Some((true, "WizardCity_Commons", (100.0, 100.0, 0.0))),
        aliases,
    );
    let mut bridge = bridge_with_route();
    let result = cycle(&mut bridge, &state_at(&snap, 600));

    assert_ne!(result["in_combat"], json!(true), "{result}");
    assert!(result["proposed_intent"].is_null(), "{result}");
    assert!(result["kinematic_plan"].is_null(), "{result}");
}

#[test]
fn an_incomplete_position_is_not_zero_filled() {
    // x and z are present, y is missing: the pose is unknown, not (x, 0, z) or the origin.
    let mut aliases = vitals(500.0, 100.0);
    aliases.push(alias("combat.active", "BOOL", json!(false)));
    aliases.push(alias("zone.name", "STRING", json!("WizardCity_Commons")));
    aliases.push(alias("position.x", "REAL", json!(100.0)));
    aliases.push(alias("position.z", "REAL", json!(0.0)));
    let snap = snapshot(true, None, aliases);
    let mut bridge = bridge_with_route();
    let result = cycle(&mut bridge, &state_at(&snap, 0));

    assert!(result["steering_command"].is_null(), "{result}");
    assert!(result["proposed_intent"].is_null(), "{result}");
}

#[test]
fn a_known_pose_still_steers_from_that_pose() {
    // Facing +X from (100, 100) the waypoint at (200, 100) is dead ahead: no turn. From the origin it
    // would need a turn, which is how a fabricated (0, 0, 0) pose shows up.
    let mut aliases = vitals(500.0, 100.0);
    aliases.extend(typed_world(
        false,
        "WizardCity_Commons",
        (100.0, 100.0, 0.0),
    ));
    let snap = snapshot(true, None, aliases);
    let mut bridge = bridge_with_route();
    let result = cycle(&mut bridge, &state_at(&snap, 0));

    assert_eq!(
        result["proposed_intent"],
        json!("PROPOSAL:StepWaypoint"),
        "{result}"
    );
    assert_eq!(
        result["steering_command"]["turn_left_pulse_ms"],
        json!(0),
        "{result}"
    );
    assert_eq!(
        result["steering_command"]["turn_right_pulse_ms"],
        json!(0),
        "{result}"
    );
}

#[test]
fn vitals_do_not_carry_forward_after_they_stop_being_fresh() {
    let mut aliases = vitals(50.0, 100.0);
    aliases.extend(typed_world(
        false,
        "WizardCity_Commons",
        (100.0, 100.0, 0.0),
    ));
    let snap = snapshot(true, None, aliases);
    let mut bridge = bridge_with_route();

    let first = cycle(&mut bridge, &state_at(&snap, 0));
    assert!(
        (first["health_pct"].as_f64().unwrap() - 0.1).abs() < 1e-9,
        "{first}"
    );

    // The same snapshot parsed a minute later: nothing in it is fresh any more.
    let later = cycle(&mut bridge, &state_at(&snap, 60));
    assert!(
        later["health_pct"].is_null(),
        "stale health was carried forward: {later}"
    );
    assert!(later["mana_pct"].is_null(), "{later}");
    assert!(later["has_potion"].is_null(), "{later}");
}

#[test]
fn well_formed_world_aliases_parse_into_typed_fields() {
    let snap = snapshot(
        true,
        None,
        typed_world(true, "  WizardCity_Commons  ", (1.5, -2.5, 3.0)),
    );
    let state = state_at(&snap, 0);

    assert_eq!(state.combat_active, Some(true));
    assert_eq!(state.zone_name.as_deref(), Some("WizardCity_Commons"));
    assert_eq!(state.position, Some([1.5, -2.5, 3.0]));
}

#[test]
fn ill_typed_or_malformed_world_aliases_stay_unknown() {
    let cases: Vec<(&str, Vec<Value>)> = vec![
        // combat.active must be a BOOL
        (
            "combat as REAL",
            vec![alias("combat.active", "REAL", json!(1.0))],
        ),
        // zone.name must be a STRING, non-empty, at most 128 characters, without control characters
        (
            "zone as BOOL",
            vec![alias("zone.name", "BOOL", json!(true))],
        ),
        (
            "empty zone",
            vec![alias("zone.name", "STRING", json!("   "))],
        ),
        (
            "control character in zone",
            vec![alias("zone.name", "STRING", json!("Zone\u{7}One"))],
        ),
        (
            "over-long zone",
            vec![alias("zone.name", "STRING", json!("Z".repeat(129)))],
        ),
        // each coordinate must be a REAL
        (
            "coordinate as DINT",
            vec![
                alias("position.x", "REAL", json!(1.0)),
                alias("position.y", "DINT", json!(5)),
                alias("position.z", "REAL", json!(0.0)),
            ],
        ),
        // a coordinate that is not fresh and identity-matched is not a reading
        (
            "wrong target",
            vec![
                alias("position.x", "REAL", json!(1.0)),
                alias("position.y", "REAL", json!(2.0)),
                {
                    let mut z = alias("position.z", "REAL", json!(0.0));
                    z["targetId"] = json!("someone-else");
                    z
                },
            ],
        ),
        (
            "stale quality",
            vec![{
                let mut a = alias("combat.active", "BOOL", json!(true));
                a["quality"] = json!("stale");
                a
            }],
        ),
    ];
    for (name, aliases) in cases {
        let state = state_at(&snapshot(true, None, aliases), 0);
        assert_eq!(state.combat_active, None, "{name}");
        assert_eq!(state.zone_name, None, "{name}");
        assert_eq!(state.position, None, "{name}");
    }
}

#[test]
fn absent_vitals_are_not_reported_as_full_health() {
    let snap = snapshot(
        true,
        None,
        typed_world(false, "WizardCity_Commons", (100.0, 100.0, 0.0)),
    );
    let mut bridge = bridge_with_route();
    let result = cycle(&mut bridge, &state_at(&snap, 0));

    assert!(result["health_pct"].is_null(), "{result}");
    assert!(result["mana_pct"].is_null(), "{result}");
    assert!(result["has_potion"].is_null(), "{result}");
}

#[test]
fn a_disconnected_snapshot_clears_everything_the_previous_one_reported() {
    let mut aliases = vitals(500.0, 100.0);
    aliases.extend(typed_world(true, "Zone_One", (100.0, 100.0, 0.0)));
    let mut bridge = bridge_with_route();

    let connected = snapshot(true, None, aliases.clone());
    let first = cycle(&mut bridge, &state_at(&connected, 0));
    assert_eq!(first["current_zone"], json!("Zone_One"), "{first}");

    let gone = snapshot(false, None, aliases);
    let after = cycle(&mut bridge, &state_at(&gone, 0));
    assert_eq!(after["connected"], json!(false));
    assert_ne!(
        after["current_zone"],
        json!("Zone_One"),
        "stale zone survived a disconnect: {after}"
    );
    assert_ne!(after["in_combat"], json!(true), "{after}");
    assert!(after["health_pct"].is_null(), "{after}");
}

#[test]
fn a_connected_cycle_without_a_known_combat_state_is_withheld() {
    // Vitals and a full pose, but no combat.active alias.
    let mut aliases = vitals(500.0, 100.0);
    aliases.push(alias("position.x", "REAL", json!(100.0)));
    aliases.push(alias("position.y", "REAL", json!(100.0)));
    aliases.push(alias("position.z", "REAL", json!(0.0)));
    let snap = snapshot(true, None, aliases);
    let mut bridge = bridge_with_route();
    let result = cycle(&mut bridge, &state_at(&snap, 0));

    assert_eq!(result["connected"], json!(true));
    assert_eq!(result["bt_status"], json!("Withheld"), "{result}");
    assert!(
        result["in_combat"].is_null(),
        "unknown combat reported as a value: {result}"
    );
    assert!(result["proposed_intent"].is_null(), "{result}");
    assert!(result["steering_command"].is_null(), "{result}");
}
