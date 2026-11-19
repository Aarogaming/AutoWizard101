//! Integration tests for the Reality Runtime Primitive Ontology (`ontology.rs`).
//! Validates the 7 irreducible grammar primitives (Manifold, Entity, Affordance, Connector, Hazard, Resource, Predicate),
//! epistemic confidence gating, vertical separation invariants, and translation to GOAP WorldState.

use maelstrom_core::goap::StateValue;
use maelstrom_core::ontology::*;
use maelstrom_core::spatial_index::{EntityKind, AABB3D};

#[test]
fn test_manifold_and_entity_kinematics() {
    let bounds = AABB3D::new(-500.0, -500.0, -50.0, 500.0, 500.0, 100.0);
    let mut manifold =
        Manifold::new("OldeTown", bounds, MetricType::NavMesh3D).with_velocity_cap(25.0);

    assert_eq!(manifold.id, "OldeTown");
    assert_eq!(manifold.max_velocity, 25.0);
    assert_eq!(manifold.epoch, 1);

    manifold.increment_epoch();
    assert_eq!(manifold.epoch, 2);

    let player = Entity::new(1, "OldeTown", EntityKind::Player, (10.0, 20.0, 0.0))
        .with_velocity(5.0, 0.0, 0.0)
        .with_dimensions(4.0, 25.0);

    let pred_pos = player.predicted_position(2.0);
    assert_eq!(pred_pos, (20.0, 20.0, 0.0));

    let spatial = player.to_spatial_entity();
    assert_eq!(spatial.id, 1);
    assert_eq!(spatial.kind, EntityKind::Player);
    assert_eq!(spatial.radius, 4.0);
}

#[test]
fn test_affordance_accessibility_and_gating() {
    // Reagent affordance requiring 'X' interact at (50, 50, 0) with 3.5 radius
    let mut reagent_aff = Affordance::new(
        101,
        "OldeTown",
        AffordanceKind::InteractKey('X'),
        (50.0, 50.0, 0.0),
        3.5,
    )
    .with_parent_entity(100);

    assert!(reagent_aff.is_accessible((50.0, 52.0, 0.0))); // dist = 2.0 <= 3.5
    assert!(!reagent_aff.is_accessible((50.0, 60.0, 0.0))); // dist = 10.0 > 3.5

    reagent_aff.enabled = false;
    assert!(
        !reagent_aff.is_accessible((50.0, 50.0, 0.0)),
        "Disabled affordance must not be accessible"
    );
}

#[test]
fn test_connector_topology_jump() {
    let portal = Connector::new(
        "OldeTownToTriton",
        "OldeTown",
        "TritonAvenue",
        (120.0, -80.0, 0.0),
        (-250.0, 30.0, 5.0),
        ConnectorKind::DoorPortal,
        8.0,
    );

    assert_eq!(portal.from_manifold, "OldeTown");
    assert_eq!(portal.to_manifold, "TritonAvenue");
    assert_eq!(portal.kind, ConnectorKind::DoorPortal);
    assert!(portal.traversal_cost > 0.0);
}

#[test]
fn test_hazard_dynamic_threat_and_vertical_separation() {
    // Mob at (100, 0, 0) moving at (0, 0, 0), aggro = 25.0, repulsion = 60.0, vertical tolerance = 30.0
    let hazard = Hazard::dynamic_aggro(
        1,
        "OldeTown",
        (100.0, 0.0, 0.0),
        (0.0, 0.0, 0.0),
        25.0,
        60.0,
    );

    // Point directly inside aggro radius: lethal cost 254
    assert_eq!(hazard.evaluate_threat_cost((105.0, 0.0, 0.0), 0.0), 254);

    // Point outside repulsion radius: 0 cost
    assert_eq!(hazard.evaluate_threat_cost((170.0, 0.0, 0.0), 0.0), 0);

    // Point on overhead bridge at Z = 50.0 (delta Z = 50.0 > 30.0 vertical tolerance)
    assert_eq!(
        hazard.evaluate_threat_cost((100.0, 0.0, 50.0), 0.0),
        0,
        "Bridge elevation must isolate player from ground mob hazard"
    );
}

#[test]
fn test_resource_consumption_and_replenishment() {
    let mut mana = Resource::new("mana", 75.0, 100.0);
    assert!((mana.ratio() - 0.75).abs() < 1e-4);
    assert!(!mana.is_depleted());

    assert!(mana.consume(50.0));
    assert!((mana.current - 25.0).abs() < 1e-4);

    // Over-consumption rejected
    assert!(!mana.consume(30.0));
    assert!((mana.current - 25.0).abs() < 1e-4);

    mana.replenish(60.0);
    assert!((mana.current - 85.0).abs() < 1e-4);

    // Capped at maximum
    mana.replenish(50.0);
    assert_eq!(mana.current, 100.0);
}

#[test]
fn test_predicate_confidence_and_freshness() {
    let pred = Predicate::new("in_combat", StateValue::Bool(true), 0.95, 1000);
    assert!(pred.is_fresh(1500, 1000)); // 500ms elapsed <= 1000ms TTL -> fresh
    assert!(!pred.is_fresh(2500, 1000)); // 1500ms elapsed > 1000ms TTL -> stale
    assert!(!pred.is_fresh(500, 1000)); // Future timestamp -> rejected
}

#[test]
fn test_reality_world_model_integration_and_goap_conversion() {
    let mut world = RealityWorldModel::new();

    let bounds = AABB3D::new(-500.0, -500.0, -50.0, 500.0, 500.0, 100.0);
    world.set_active_manifold(Manifold::new("OldeTown", bounds, MetricType::NavMesh3D));

    // Add dynamic mob hazard
    world.add_hazard(Hazard::dynamic_aggro(
        1,
        "OldeTown",
        (50.0, 0.0, 0.0),
        (0.0, 0.0, 0.0),
        20.0,
        50.0,
    ));

    // Add harvest affordance
    world.add_affordance(Affordance::new(
        10,
        "OldeTown",
        AffordanceKind::InteractKey('X'),
        (10.0, 10.0, 0.0),
        3.5,
    ));

    // Add resources
    world.set_resource(Resource::new("mana", 80.0, 100.0));
    world.set_resource(Resource::new("health", 95.0, 100.0));

    // Ground predicates
    world.assert_predicate("in_combat", false, 0.99, 1000);
    world.assert_predicate("safe_from_mobs", true, 0.95, 1000);
    world.assert_predicate("active_deck_profile", "mob_aoe", 0.98, 1000);

    // Epistemic safety query: (50, 0, 0) is inside mob aggro -> unsafe
    assert!(!world.is_point_safe((50.0, 0.0, 0.0), 0.0));
    // Point at (0, 0, 0) -> safe
    assert!(world.is_point_safe((0.0, 0.0, 0.0), 0.0));

    // Affordance query from (10, 12, 0) -> distance = 2.0 <= 3.5 -> accessible
    let accessible = world.query_accessible_affordances((10.0, 12.0, 0.0));
    assert_eq!(accessible.len(), 1);
    assert_eq!(accessible[0].id, 10);

    // Convert to GOAP WorldState at t = 1200 (200ms elapsed, fresh)
    let goap_state = world.to_goap_world_state(1200, 1000);
    assert_eq!(goap_state.get_bool("in_combat"), Some(false));
    assert_eq!(goap_state.get_bool("safe_from_mobs"), Some(true));
    assert_eq!(goap_state.get_str("active_deck_profile"), Some("mob_aoe"));
    assert_eq!(goap_state.get_int("mana_ratio"), Some(80));
    assert_eq!(goap_state.get_bool("mana_depleted"), Some(false));
}
