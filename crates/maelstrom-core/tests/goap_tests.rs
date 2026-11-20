//! Integration tests for Goal-Oriented Action Planning (GOAP).
//! Tests complex multi-zone prerequisite chaining, dynamic replanning upon state disruption,
//! and coordination with W101 Behavior Tree Blackboard.

use maelstrom_core::behavior_tree::{build_w101_canonical_tree, BtNode, BtStatus, W101Blackboard};
use maelstrom_core::goap::*;

#[test]
fn test_integration_complex_multi_zone_quest_prerequisite_chain() {
    let planner = GoapPlanner::new(1000);

    // Initial State: Newly spawned in Commons with low mana, no key, no potion
    let mut current = WorldState::new();
    current.set("at", "Commons");
    current.set("mana_full", false);
    current.set("has_potion", false);
    current.set("has_ravenwood_key", false);
    current.set("gate_unlocked", false);
    current.set("talked_to_malorn", false);

    // Target Goal: Speak with Malorn Ashthorn in Ravenwood
    let mut goal = WorldState::new();
    goal.set("talked_to_malorn", true);

    // Available domain action repertoire
    let actions = vec![
        // Vital prep
        GoapAction::new("DrinkAtFountain", 5.0)
            .require("at", "Commons")
            .effect("mana_full", true),
        GoapAction::new("BuyPotionHilda", 8.0)
            .require("at", "Commons")
            .effect("has_potion", true),
        // Ambrose Tower sub-loop
        GoapAction::new("EnterAmbroseTower", 10.0)
            .require("at", "Commons")
            .effect("at", "AmbroseOffice"),
        GoapAction::new("TalkToAmbrose", 3.0)
            .require("at", "AmbroseOffice")
            .effect("has_ravenwood_key", true),
        GoapAction::new("ExitAmbroseTower", 5.0)
            .require("at", "AmbroseOffice")
            .effect("at", "Commons"),
        // Ravenwood gate and travel (requires full mana for safety before entering dangerous zone)
        GoapAction::new("UnlockRavenwoodGate", 12.0)
            .require("at", "Commons")
            .require("has_ravenwood_key", true)
            .effect("gate_unlocked", true),
        GoapAction::new("WalkThroughTunnelToRavenwood", 15.0)
            .require("at", "Commons")
            .require("gate_unlocked", true)
            .require("mana_full", true)
            .effect("at", "Ravenwood"),
        // Ravenwood interior
        GoapAction::new("EnterDeathSchool", 8.0)
            .require("at", "Ravenwood")
            .effect("at", "DeathSchool"),
        GoapAction::new("TalkToMalornAshthorn", 4.0)
            .require("at", "DeathSchool")
            .effect("talked_to_malorn", true),
    ];

    let plan = planner
        .plan(&current, &goal, &actions)
        .expect("Plan must resolve");

    // Plan sequence verification:
    // 1. Must fill mana at fountain
    // 2. Must enter Ambrose Tower
    // 3. Must talk to Ambrose to get key
    // 4. Must exit tower
    // 5. Must unlock gate with key
    // 6. Must walk through tunnel to Ravenwood (precondition mana_full and gate_unlocked met)
    // 7. Must enter Death School
    // 8. Must talk to Malorn
    let names: Vec<&str> = plan.iter().map(|a| a.name.as_str()).collect();
    assert!(names.contains(&"DrinkAtFountain"));
    assert!(names.contains(&"EnterAmbroseTower"));
    assert!(names.contains(&"TalkToAmbrose"));
    assert!(names.contains(&"ExitAmbroseTower"));
    assert!(names.contains(&"UnlockRavenwoodGate"));
    assert!(names.contains(&"WalkThroughTunnelToRavenwood"));
    assert!(names.contains(&"EnterDeathSchool"));
    assert_eq!(*names.last().unwrap(), "TalkToMalornAshthorn");

    // Verify step-by-step state simulation succeeds through the plan
    let mut state = current.clone();
    for action in &plan {
        assert!(
            action.is_achievable(&state),
            "Action '{}' preconditions must be met in simulation state",
            action.name
        );
        state = action.apply(&state);
    }
    assert!(state.satisfies(&goal));
}

#[test]
fn test_integration_dynamic_replanning_on_state_disruption() {
    let planner = GoapPlanner::default();

    let actions = vec![
        GoapAction::new("DrinkAtFountain", 5.0)
            .require("at", "Commons")
            .effect("mana_full", true),
        GoapAction::new("WalkToRavenwood", 15.0)
            .require("at", "Commons")
            .require("mana_full", true)
            .effect("at", "Ravenwood"),
        GoapAction::new("EnterDungeon", 10.0)
            .require("at", "Ravenwood")
            .require("mana_full", true)
            .effect("at", "Dungeon"),
        GoapAction::new("DefeatBoss", 50.0)
            .require("at", "Dungeon")
            .effect("boss_defeated", true),
    ];

    let mut goal = WorldState::new();
    goal.set("boss_defeated", true);

    // Initial plan from Commons
    let mut initial_state = WorldState::new();
    initial_state.set("at", "Commons");
    initial_state.set("mana_full", true);

    let plan1 = planner.plan(&initial_state, &goal, &actions).unwrap();
    assert_eq!(plan1.len(), 3); // WalkToRavenwood -> EnterDungeon -> DefeatBoss

    // Mid-execution disruption: player is dragged into battle or defeated, respawning at Commons with empty mana
    let mut disrupted_state = WorldState::new();
    disrupted_state.set("at", "Commons");
    disrupted_state.set("mana_full", false); // Lost all mana

    // Planner detects state deviation and re-computes optimal recovery route
    let recovery_plan = planner
        .plan(&disrupted_state, &goal, &actions)
        .expect("Recovery plan must resolve");
    assert_eq!(recovery_plan.len(), 4);
    assert_eq!(
        recovery_plan[0].name, "DrinkAtFountain",
        "Recovery plan must first refill mana"
    );
    assert_eq!(recovery_plan[1].name, "WalkToRavenwood");
    assert_eq!(recovery_plan[2].name, "EnterDungeon");
    assert_eq!(recovery_plan[3].name, "DefeatBoss");
}

#[test]
fn test_integration_goap_feeds_behavior_tree_blackboard() {
    let planner = GoapPlanner::default();

    let mut current = WorldState::new();
    current.set("at", "Commons");

    let mut goal = WorldState::new();
    goal.set("at", "Ravenwood");

    let actions = vec![GoapAction::new("WalkToRavenwoodTunnel", 10.0)
        .require("at", "Commons")
        .effect("at", "Ravenwood")];

    let plan = planner.plan(&current, &goal, &actions).unwrap();
    assert_eq!(plan.len(), 1);

    // Feed GOAP planned action into Behavior Tree Blackboard
    let mut bb = W101Blackboard::new();
    bb.active_goal = Some(plan[0].name.clone());
    bb.active_waypoints = vec![(150.0, 200.0, 0.0)];

    let mut tree = build_w101_canonical_tree();
    let status = tree.tick(&mut bb);

    assert_eq!(status, BtStatus::Running);
    assert_eq!(
        bb.proposal_channel.as_deref(),
        Some("PROPOSAL:StepWaypoint"),
        "Behavior tree must execute navigation step for GOAP-directed action"
    );
}

#[test]
fn test_integration_pre_battle_deck_profile_planning() {
    let planner = GoapPlanner::default();

    // Start in Commons with MobAoE equipped, safe from mobs, out of combat
    let mut current = WorldState::new();
    current.set("at", "OldeTown");
    current.set("in_combat", false);
    current.set("safe_from_mobs", true);
    current.set("active_deck_profile", DeckProfileTag::MobAoE);
    current.set("defeated_street_mobs", false);
    current.set("defeated_fire_boss", false);

    // Goal: defeat both street mobs and fire boss
    let mut goal = WorldState::new();
    goal.set("defeated_fire_boss", true);

    let actions = vec![
        // Street combat loop (requires MobAoE)
        GoapAction::new("PullStreetMobs", 8.0)
            .require("at", "OldeTown")
            .require("in_combat", false)
            .require("active_deck_profile", DeckProfileTag::MobAoE)
            .effect("in_combat", true)
            .effect("in_street_combat", true),
        GoapAction::new("WinStreetCombat", 12.0)
            .require("in_combat", true)
            .require("in_street_combat", true)
            .effect("in_combat", false)
            .effect("in_street_combat", false)
            .effect("defeated_street_mobs", true)
            .effect("safe_from_mobs", true),
        // Tower travel
        GoapAction::new("TravelToFireTower", 15.0)
            .require("at", "OldeTown")
            .require("defeated_street_mobs", true)
            .require("in_combat", false)
            .effect("at", "FireTowerSigil")
            .effect("at_sigil", "FireTowerSigil"),
        // Pre-battle deck switch (switch to SchoolPrism for fire boss)
        action_ensure_deck_profile(DeckProfileTag::SchoolPrism),
        // Boss Sigil entry and win
        action_enter_combat_sigil("FireTowerSigil", DeckProfileTag::SchoolPrism),
        GoapAction::new("WinBossCombat", 25.0)
            .require("in_combat", true)
            .require("at_sigil", "FireTowerSigil")
            .effect("in_combat", false)
            .effect("defeated_fire_boss", true),
    ];

    let plan = planner
        .plan(&current, &goal, &actions)
        .expect("Planner must resolve macro quest with pre-battle deck profile switch");

    let action_names: Vec<&str> = plan.iter().map(|a| a.name.as_str()).collect();

    // Verify ordering invariants:
    // 1. Street combat must occur first (PullStreetMobs -> WinStreetCombat)
    // 2. Deck switch to school_prism must occur in safe zone before entering boss sigil
    // 3. Sigil combat must conclude with boss defeat
    let pull_idx = action_names
        .iter()
        .position(|&a| a == "PullStreetMobs")
        .unwrap();
    let win_street_idx = action_names
        .iter()
        .position(|&a| a == "WinStreetCombat")
        .unwrap();
    let switch_idx = action_names
        .iter()
        .position(|&a| a == "EnsureDeckProfile:school_prism")
        .unwrap();
    let sigil_idx = action_names
        .iter()
        .position(|&a| a == "EnterSigil:FireTowerSigil")
        .unwrap();
    let win_boss_idx = action_names
        .iter()
        .position(|&a| a == "WinBossCombat")
        .unwrap();

    assert!(pull_idx < win_street_idx);
    assert!(win_street_idx < sigil_idx);
    assert!(
        switch_idx < sigil_idx,
        "Deck switch must occur before entering sigil"
    );
    assert!(sigil_idx < win_boss_idx);

    // Verify simulation state trajectory
    let mut sim = current.clone();
    for a in &plan {
        assert!(
            a.is_achievable(&sim),
            "Action '{}' preconditions must be met",
            a.name
        );
        sim = a.apply(&sim);
    }
    assert!(sim.satisfies(&goal));
}

#[test]
fn test_fast_travel_and_marker_recall_shortcuts() {
    let planner = GoapPlanner::new(1000);

    // Scenario: Player is deep in SunkenCity before boss pull, needs potion restock from Commons
    let mut current = WorldState::new();
    current.set("at", "SunkenCityDepths");
    current.set("in_combat", false);
    current.set("potions_restocked", false);
    current.set("has_marker", false);

    // Goal: return to SunkenCityDepths with potions restocked from vendor
    let mut goal = WorldState::new();
    goal.set("at", "SunkenCityDepths");
    goal.set("potions_restocked", true);

    let actions = vec![
        // Option A: Heavy walking through 3 zones to Commons and back (cost 140+)
        GoapAction::new("WalkOutOfSunkenCity", 60.0)
            .require("at", "SunkenCityDepths")
            .effect("at", "Commons"),
        GoapAction::new("BuyPotionFromHilda", 5.0)
            .require("at", "Commons")
            .effect("potions_restocked", true),
        GoapAction::new("WalkBackToSunkenCity", 80.0)
            .require("at", "Commons")
            .require("potions_restocked", true)
            .effect("at", "SunkenCityDepths"),
        // Option B: Fast-travel shortcuts (Page Down marker -> FastTravel Commons -> BuyPotion -> Page Up recall)
        action_set_marker("SunkenCityDepths"),
        action_fast_travel_commons(),
        action_recall_marker("SunkenCityDepths"),
    ];

    let plan = planner
        .plan(&current, &goal, &actions)
        .expect("Planner must synthesize shortcut recall plan");

    let action_names: Vec<&str> = plan.iter().map(|a| a.name.as_str()).collect();
    assert_eq!(
        action_names,
        vec![
            "SetMarker:SunkenCityDepths",
            "FastTravel:Commons",
            "BuyPotionFromHilda",
            "RecallMarker:PageUp",
        ],
        "Planner must choose zero-pathing fast travel and recall over 140+ cost walking routes"
    );

    // Verify simulation
    let mut sim = current.clone();
    for a in &plan {
        assert!(a.is_achievable(&sim));
        sim = a.apply(&sim);
    }
    assert!(sim.satisfies(&goal));
}

#[test]
fn test_library_gauntlet_multi_step_puzzle_chain() {
    let planner = GoapPlanner::new(1000);

    // Initial state: At entrance of Library Gauntlet puzzle chamber
    let mut current = WorldState::new();
    current.set("at", "LibraryPuzzleChamber");
    current.set("in_combat", false);
    current.set("pedestal_fire_matched", false);
    current.set("pedestal_ice_matched", false);
    current.set("pedestal_storm_matched", false);
    current.set("vault_door_unlocked", false);
    current.set("boss_unlocked", false);

    // Goal: Unlock final boss chamber via environmental puzzle affordances
    let mut goal = WorldState::new();
    goal.set("boss_unlocked", true);

    let actions = vec![
        action_interact_puzzle_affordance(
            "LibraryGauntlet",
            "FirePedestal",
            "",
            "pedestal_fire_matched",
        ),
        action_interact_puzzle_affordance(
            "LibraryGauntlet",
            "IcePedestal",
            "pedestal_fire_matched",
            "pedestal_ice_matched",
        ),
        action_interact_puzzle_affordance(
            "LibraryGauntlet",
            "StormPedestal",
            "pedestal_ice_matched",
            "pedestal_storm_matched",
        ),
        GoapAction::new("PullVaultMasterLever", 3.0)
            .require("in_combat", false)
            .require("pedestal_storm_matched", true)
            .effect("vault_door_unlocked", true),
        GoapAction::new("EnterVaultBossChamber", 4.0)
            .require("vault_door_unlocked", true)
            .effect("boss_unlocked", true),
    ];

    let plan = planner
        .plan(&current, &goal, &actions)
        .expect("Planner must solve Library Gauntlet puzzle chain");

    let action_names: Vec<&str> = plan.iter().map(|a| a.name.as_str()).collect();
    assert_eq!(
        action_names,
        vec![
            "SolvePuzzleAffordance:LibraryGauntlet:FirePedestal",
            "SolvePuzzleAffordance:LibraryGauntlet:IcePedestal",
            "SolvePuzzleAffordance:LibraryGauntlet:StormPedestal",
            "PullVaultMasterLever",
            "EnterVaultBossChamber",
        ]
    );

    let mut sim = current.clone();
    for a in &plan {
        assert!(a.is_achievable(&sim));
        sim = a.apply(&sim);
    }
    assert!(sim.satisfies(&goal));
}
