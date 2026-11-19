use maelstrom_core::costmap::{
    CostmapComposition, DynamicMobActor, FootprintInflation, Nav2Costmap, NavEdge, NavVertex,
    ObstacleSegment, PolygonZone, Vec2D, ZoneNavMesh, COST_FREE, COST_LETHAL_OBSTACLE,
};

#[test]
fn test_integration_additive_and_max_composition() {
    let footprint = FootprintInflation::new(10.0, 30.0);

    // Obstacle at x = 0
    let obs = ObstacleSegment::new(Vec2D::new(0.0, -50.0), Vec2D::new(0.0, 50.0));
    // Zone giving 30 penalty
    let zone = PolygonZone::new(
        "road",
        vec![
            Vec2D::new(-50.0, -50.0),
            Vec2D::new(50.0, -50.0),
            Vec2D::new(50.0, 50.0),
            Vec2D::new(-50.0, 50.0),
        ],
        30,
    );

    let mut map_max = Nav2Costmap::new(footprint, CostmapComposition::Max);
    map_max.add_obstacle(obs);
    map_max.add_zone(zone.clone());

    let mut map_add = Nav2Costmap::new(footprint, CostmapComposition::Additive);
    map_add.add_obstacle(obs);
    map_add.add_zone(zone);

    // Query point at x = 20.0 (dist to obs = 20.0, so inflation cost is active)
    let pt = Vec2D::new(20.0, 0.0);
    let inf_cost = footprint.compute_cost(20.0); // Between 1 and 252
    assert!(inf_cost > 0 && inf_cost < 253);

    let cost_max = map_max.evaluate_cost(pt, 0.0);
    assert_eq!(cost_max, inf_cost.max(30));

    let cost_add = map_add.evaluate_cost(pt, 0.0);
    assert_eq!(cost_add, (inf_cost as u32 + 30).min(252) as u8);
}

#[test]
fn test_integration_moving_mob_intercept_prediction() {
    // Player plans to move from (0, 0) to (50, 0) at speed 10 units/s (arrival at t = 5s)
    let footprint = FootprintInflation::new(15.0, 40.0);
    let mut costmap = Nav2Costmap::new(footprint, CostmapComposition::Max);

    // Mob patrols along y-axis from (50, 100) down to (50, -100) at 20 units/s with 5s lookahead
    let mob = DynamicMobActor::new(
        42,
        Vec2D::new(50.0, 100.0),
        Vec2D::new(0.0, -20.0), // Moves -20 in y per sec
        20.0,                   // aggro cylinder
        60.0,                   // repulsion field
        5000.0,
    )
    .with_elevation(0.0, 50.0)
    .with_lookahead(5.0);
    costmap.add_mob(mob);

    let target_node = Vec2D::new(50.0, 0.0);

    // At t = 0: mob is at (50, 100), distance to target = 100.0 > 60.0 -> FREE
    assert_eq!(costmap.evaluate_cost(target_node, 0.0), COST_FREE);
    assert!(costmap.is_passable(target_node, 0.0));

    // At t = 5s (when player would arrive): mob is at (50, 100 - 20*5) = (50, 0) -> EXACT COLLISION -> LETHAL
    let arrival_cost = costmap.evaluate_cost(target_node, 5.0);
    assert_eq!(arrival_cost, COST_LETHAL_OBSTACLE);
    assert!(!costmap.is_passable(target_node, 5.0));

    // A lateral detour node at (20.0, 0.0) at t = 2s:
    // At t = 2s, mob is at (50, 60). Distance from (20, 0) to (50, 60) = sqrt(30^2 + 60^2) = sqrt(4500) ≈ 67.0 > 60.0 -> FREE
    let detour_node = Vec2D::new(20.0, 0.0);
    assert_eq!(costmap.evaluate_cost(detour_node, 2.0), COST_FREE);
    assert!(costmap.is_passable(detour_node, 2.0));
}

#[test]
fn test_integration_elevation_bridge_separation() {
    let footprint = FootprintInflation::new(10.0, 30.0);
    let mut costmap = Nav2Costmap::new(footprint, CostmapComposition::Max);

    // Patrol mob on ground underpass at (50, 50, z = 0.0) with vertical tolerance 40.0
    costmap.add_mob(
        DynamicMobActor::new(
            101,
            Vec2D::new(50.0, 50.0),
            Vec2D::new(0.0, 0.0),
            25.0, // aggro cylinder
            80.0, // repulsion radius
            3000.0,
        )
        .with_elevation(0.0, 40.0),
    );

    let bridge_point = Vec2D::new(50.0, 50.0);

    // Player walking on overhead bridge directly above mob at z = 120.0
    // In 2D projection (no z), it would falsely flag as LETHAL (254)
    assert_eq!(
        costmap.evaluate_cost(bridge_point, 0.0),
        COST_LETHAL_OBSTACLE
    );

    // With 3D elevation awareness (z = 120.0 > 0.0 + 40.0): bridge is safe and passable!
    assert_eq!(
        costmap.evaluate_cost_3d(bridge_point, Some(120.0), 0.0),
        COST_FREE
    );
    assert!(costmap.is_passable_3d(bridge_point, Some(120.0), 0.0));

    // Player walking in underpass at z = 10.0 (within vertical tolerance 40.0): LETHAL
    assert_eq!(
        costmap.evaluate_cost_3d(bridge_point, Some(10.0), 0.0),
        COST_LETHAL_OBSTACLE
    );
    assert!(!costmap.is_passable_3d(bridge_point, Some(10.0), 0.0));
}

#[test]
fn test_integration_dynamic_mob_deflects_nav_mesh_path() {
    // Diamond navigation mesh topology:
    //      (1: North 50, 30)
    //     /                 \
    // (0: Start 0,0)         (3: Goal 100,0)
    //     \                 /
    //      (2: South 50,-30)
    let mesh = ZoneNavMesh {
        header_count: 4,
        header_unknown: 0,
        vertex_records: 4,
        vertices: vec![
            NavVertex::new(0.0, 0.0, 0.0),    // 0: Start
            NavVertex::new(50.0, 30.0, 0.0),  // 1: North fork
            NavVertex::new(50.0, -30.0, 0.0), // 2: South fork
            NavVertex::new(100.0, 0.0, 0.0),  // 3: Goal
        ],
        edges: vec![
            NavEdge::new(0, 1),
            NavEdge::new(1, 3),
            NavEdge::new(0, 2),
            NavEdge::new(2, 3),
        ],
    };

    let footprint = FootprintInflation::new(10.0, 25.0);
    let mut costmap = Nav2Costmap::new(footprint, CostmapComposition::Max);

    // Initial state: No obstacles or mobs. Both paths have identical length ~116.62 units.
    let baseline_path = mesh
        .find_safest_path(0, 3, &costmap, 0.0, 10.0)
        .expect("Baseline path must exist");
    assert!(
        baseline_path == vec![0, 1, 3] || baseline_path == vec![0, 2, 3],
        "Baseline must choose one of the symmetric branches"
    );

    // Introduce an enemy patrol mob camping right on the North fork (node 1 at 50, 30)
    costmap.add_mob(
        DynamicMobActor::new(
            77,
            Vec2D::new(50.0, 30.0),
            Vec2D::new(0.0, 0.0),
            20.0,   // aggro radius
            60.0,   // repulsion radius
            3000.0, // strong repulsive field
        )
        .with_elevation(0.0, 40.0),
    );

    // Dynamic Dijkstra re-evaluates all edge costs against the costmap.
    // The North branch (0->1 and 1->3) has midpoint within the mob's lethal/repulsive field.
    // The South branch (0->2 and 2->3) is completely clean.
    let deflected_path = mesh
        .find_safest_path(0, 3, &costmap, 0.0, 10.0)
        .expect("Deflected path must find the South branch");
    assert_eq!(
        deflected_path,
        vec![0, 2, 3],
        "Costmap penalty must deflect Dijkstra safely to South branch [0, 2, 3]"
    );

    // If a second enemy mob moves into the South fork (node 2 at 50, -30), both branches are blocked.
    costmap.add_mob(
        DynamicMobActor::new(
            78,
            Vec2D::new(50.0, -30.0),
            Vec2D::new(0.0, 0.0),
            25.0, // aggro radius covering midpoint
            60.0,
            3000.0,
        )
        .with_elevation(0.0, 40.0),
    );

    let impassable_path = mesh.find_safest_path(0, 3, &costmap, 0.0, 10.0);
    assert_eq!(
        impassable_path, None,
        "When all paths exceed lethal cost, find_safest_path safely aborts"
    );
}

#[test]
fn test_spatial_index_nearest_reagent_and_chest() {
    use maelstrom_core::spatial_index::{EntityKind, SpatialEntity3D};

    let footprint = FootprintInflation::new(10.0, 30.0);
    let mut costmap = Nav2Costmap::new(footprint, CostmapComposition::Max);

    // Add a mob via standard add_mob (automatically indexes into spatial_index)
    costmap.add_mob(
        DynamicMobActor::new(
            1,
            Vec2D::new(100.0, 100.0),
            Vec2D::new(0.0, 0.0),
            20.0,
            50.0,
            1000.0,
        )
        .with_elevation(10.0, 30.0),
    );

    // Add candidate reagents
    costmap.add_spatial_entity(SpatialEntity3D::new(
        10,
        EntityKind::Reagent,
        105.0,
        100.0,
        10.0,
        2.0,
    ));
    costmap.add_spatial_entity(SpatialEntity3D::new(
        11,
        EntityKind::Reagent,
        180.0,
        100.0,
        10.0,
        2.0,
    ));

    // Add chests
    costmap.add_spatial_entity(SpatialEntity3D::new(
        20,
        EntityKind::SilverChest,
        120.0,
        100.0,
        10.0,
        3.0,
    ));

    assert_eq!(costmap.spatial_index.len(), 4);

    // Query nearest reagent to (102, 100, 10)
    let nearest_reagent = costmap
        .find_nearest_reagent(102.0, 100.0, 10.0, 20.0)
        .expect("Should find reagent 10");
    assert_eq!(nearest_reagent.0.id, 10);
    assert!((nearest_reagent.1 - 3.0).abs() < 1e-4);

    // Query nearest chest
    let nearest_chest = costmap
        .find_nearest_chest(102.0, 100.0, 10.0, 30.0)
        .expect("Should find silver chest 20");
    assert_eq!(nearest_chest.0.id, 20);
}

#[test]
fn test_spatial_corridor_safety_evaluation() {
    let footprint = FootprintInflation::new(10.0, 30.0);
    let mut costmap = Nav2Costmap::new(footprint, CostmapComposition::Max);

    // Dynamic mob at (50, 0, 0) directly blocking corridor from (0, 0, 0) to (100, 0, 0)
    costmap.add_mob(
        DynamicMobActor::new(
            1,
            Vec2D::new(50.0, 0.0),
            Vec2D::new(0.0, 0.0),
            25.0, // Lethal aggro
            60.0,
            2000.0,
        )
        .with_elevation(0.0, 30.0),
    );

    // Corridor of radius 5.0 directly passes through the mob
    assert!(
        !costmap.is_corridor_safe_3d((0.0, 0.0, 0.0), (100.0, 0.0, 0.0), 5.0, 0.0),
        "Corridor intersecting mob aggro cylinder must not be safe"
    );

    // Parallel corridor displaced at Y = 50.0 (distance 50.0 > 25.0 aggro)
    assert!(
        costmap.is_corridor_safe_3d((0.0, 50.0, 0.0), (100.0, 50.0, 0.0), 5.0, 0.0),
        "Corridor outside mob aggro must be safe"
    );

    // Corridor passing over bridge at Z = 60.0 (delta Z = 60.0 > 30.0 tolerance)
    assert!(
        costmap.is_corridor_safe_3d((0.0, 0.0, 60.0), (100.0, 0.0, 60.0), 5.0, 0.0),
        "Corridor separated by vertical elevation on bridge must be safe"
    );
}

#[test]
fn test_detour_budget_equation_and_approval() {
    use maelstrom_core::spatial_index::EntityKind;

    let footprint = FootprintInflation::new(10.0, 30.0);
    let mut costmap = Nav2Costmap::new(footprint, CostmapComposition::Max);

    let curr = (0.0, 0.0, 0.0);
    let resume = (100.0, 0.0, 0.0);
    let budget = 35.0;

    // Candidate A: Reagent at (50, 10, 0)
    // Direct = 100.0. Leg 1 = sqrt(50^2 + 10^2) ≈ 50.99. Leg 2 ≈ 50.99. Total ≈ 101.98. Delta C ≈ 1.98 < 35.0
    let candidate_a = costmap.evaluate_detour_candidate(
        curr,
        resume,
        1,
        (50.0, 10.0, 0.0),
        EntityKind::Reagent,
        budget,
        5.0,
        0.0,
    );
    assert!(
        candidate_a.approved,
        "Candidate A within budget must be approved"
    );
    assert!(candidate_a.delta_cost < 5.0);

    // Candidate B: Distant reagent at (50, 60, 0)
    // Direct = 100.0. Leg 1 = sqrt(50^2 + 60^2) ≈ 78.10. Leg 2 ≈ 78.10. Total ≈ 156.20. Delta C ≈ 56.20 >= 35.0
    let candidate_b = costmap.evaluate_detour_candidate(
        curr,
        resume,
        2,
        (50.0, 60.0, 0.0),
        EntityKind::Reagent,
        budget,
        5.0,
        0.0,
    );
    assert!(
        !candidate_b.approved,
        "Candidate B exceeding budget must be rejected"
    );
    assert!(candidate_b.delta_cost > budget);

    // Candidate C: Reagent at (50, 10, 0) but mob camps right on it
    costmap.add_mob(
        DynamicMobActor::new(
            99,
            Vec2D::new(50.0, 10.0),
            Vec2D::new(0.0, 0.0),
            20.0,
            50.0,
            1000.0,
        )
        .with_elevation(0.0, 30.0),
    );

    let candidate_c = costmap.evaluate_detour_candidate(
        curr,
        resume,
        3,
        (50.0, 10.0, 0.0),
        EntityKind::Reagent,
        budget,
        5.0,
        0.0,
    );
    assert!(
        !candidate_c.approved,
        "Candidate C covered by dynamic mob aggro cylinder must be rejected"
    );
}
