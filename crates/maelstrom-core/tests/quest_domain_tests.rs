use maelstrom_core::goap::GoapPlanner;
use maelstrom_core::quest_domain::*;

#[test]
fn test_npc_interaction_anchors() {
    let ambrose = get_npc_interaction_target("Merle Ambrose").expect("Merle Ambrose must exist");
    assert_eq!(ambrose.zone, ZONE_HEADMASTER_OFFICE);
    assert_eq!(ambrose.dialogue_steps, 3);
    assert!(ambrose.prompt_bounds.width() > 0.0);
    assert!(ambrose.prompt_bounds.height() > 0.0);
    let (cx, cy) = ambrose.prompt_bounds.center();
    assert!((580.0..=700.0).contains(&cx));
    assert!((520.0..=560.0).contains(&cy));

    let bartleby = get_npc_interaction_target("Bartleby").expect("Bartleby must exist");
    assert_eq!(bartleby.zone, ZONE_RAVENWOOD);

    let lincoln = get_npc_interaction_target("Mr. Lincoln").expect("Mr. Lincoln must exist");
    assert_eq!(lincoln.zone, ZONE_GOLEM_COURT);

    let vendor = get_npc_interaction_target("Commons Potion Vendor").expect("Vendor must exist");
    assert_eq!(vendor.zone, ZONE_COMMONS);

    assert!(get_npc_interaction_target("NonExistentNPC").is_none());
}

#[test]
fn test_plan_lore_of_magic_from_fresh_start() {
    let actions = build_wizard_city_starter_repertoire();
    let planner = GoapPlanner::new(2000);

    let start = initial_fresh_wizard_state();
    let goal = goal_complete_lore_of_magic();

    let plan = planner
        .plan(&start, &goal, &actions)
        .expect("Plan for Lore of Magic must be found");
    assert_eq!(plan.len(), 7);

    let action_names: Vec<&str> = plan.iter().map(|a| a.name.as_str()).collect();
    assert_eq!(
        action_names,
        vec![
            "ambrose_accept_lore_of_magic",
            "travel_office_to_commons",
            "travel_commons_to_ravenwood",
            "talk_to_bartleby",
            "travel_ravenwood_to_commons",
            "travel_commons_to_office",
            "ambrose_turnin_lore_of_magic",
        ]
    );

    // Verify end state
    let mut current = start;
    for action in &plan {
        current = action.apply(&current);
    }
    assert_eq!(current.get_bool(KEY_COMPLETED_LORE_OF_MAGIC), Some(true));
    assert_eq!(current.get_bool(KEY_HAS_QUEST_ENROLLMENT), Some(true));
    assert_eq!(current.get_str(KEY_ZONE), Some(ZONE_HEADMASTER_OFFICE));
}

#[test]
fn test_plan_enrollment_quest() {
    let actions = build_wizard_city_starter_repertoire();
    let planner = GoapPlanner::new(2000);

    // Start with Lore of Magic completed and Enrollment accepted
    let mut start = initial_fresh_wizard_state();
    start.set(KEY_COMPLETED_LORE_OF_MAGIC, true);
    start.set(KEY_HAS_QUEST_ENROLLMENT, true);

    let goal = goal_complete_enrollment();
    let plan = planner
        .plan(&start, &goal, &actions)
        .expect("Plan for Enrollment must be found");

    let action_names: Vec<&str> = plan.iter().map(|a| a.name.as_str()).collect();

    // Must start by leaving Office to Commons to Ravenwood
    assert_eq!(action_names[0], "travel_office_to_commons");
    assert_eq!(action_names[1], "travel_commons_to_ravenwood");

    // Must visit all 6 schools while in Ravenwood
    assert!(action_names.contains(&"visit_school_fire"));
    assert!(action_names.contains(&"visit_school_ice"));
    assert!(action_names.contains(&"visit_school_storm"));
    assert!(action_names.contains(&"visit_school_myth"));
    assert!(action_names.contains(&"visit_school_life"));
    assert!(action_names.contains(&"visit_school_death"));

    // Must return to Office to turn in
    assert_eq!(
        action_names[action_names.len() - 3],
        "travel_ravenwood_to_commons"
    );
    assert_eq!(
        action_names[action_names.len() - 2],
        "travel_commons_to_office"
    );
    assert_eq!(
        action_names[action_names.len() - 1],
        "ambrose_turnin_enrollment"
    );

    // Execute plan and verify outcome
    let mut current = start;
    for action in &plan {
        current = action.apply(&current);
    }
    assert_eq!(current.get_bool(KEY_COMPLETED_ENROLLMENT), Some(true));
    assert_eq!(current.get_bool(KEY_HAS_QUEST_GOOD_IMPRESSION), Some(true));
}

#[test]
fn test_plan_good_impression_quest() {
    let actions = build_wizard_city_starter_repertoire();
    let planner = GoapPlanner::new(2000);

    let mut start = initial_fresh_wizard_state();
    start.set(KEY_COMPLETED_LORE_OF_MAGIC, true);
    start.set(KEY_COMPLETED_ENROLLMENT, true);
    start.set(KEY_HAS_QUEST_GOOD_IMPRESSION, true);

    let goal = goal_complete_good_impression();
    let plan = planner
        .plan(&start, &goal, &actions)
        .expect("Plan for Good Impression must be found");

    let action_names: Vec<&str> = plan.iter().map(|a| a.name.as_str()).collect();
    assert_eq!(
        action_names,
        vec![
            "travel_office_to_commons",
            "travel_commons_to_golem_court",
            "talk_to_mr_lincoln",
            "travel_golem_court_to_commons",
            "travel_commons_to_office",
            "ambrose_turnin_good_impression",
        ]
    );

    let mut current = start;
    for action in &plan {
        current = action.apply(&current);
    }
    assert_eq!(current.get_bool(KEY_COMPLETED_GOOD_IMPRESSION), Some(true));
}

#[test]
fn test_vital_recovery_choice_potion_vs_fountain() {
    let actions = build_wizard_city_starter_repertoire();
    let planner = GoapPlanner::new(2000);

    // Case 1: In Ravenwood with vitals low, but has potion.
    // Planner should choose drink_potion_recovery (cost 1.0) instead of traveling to Commons fountain (cost 8.0 + 6.0).
    let mut state_with_potion = initial_fresh_wizard_state();
    state_with_potion.set(KEY_ZONE, ZONE_RAVENWOOD);
    state_with_potion.set(KEY_VITALS_LOW, true);
    state_with_potion.set(KEY_HAS_POTIONS, true);

    let mut goal_vitals = maelstrom_core::goap::WorldState::new();
    goal_vitals.set(KEY_VITALS_LOW, false);

    let plan1 = planner
        .plan(&state_with_potion, &goal_vitals, &actions)
        .expect("Plan must be found");
    assert_eq!(plan1.len(), 1);
    assert_eq!(plan1[0].name, "drink_potion_recovery");

    // Case 2: In Ravenwood with vitals low AND no potions.
    // Planner must route to Commons and refill at fountain.
    let mut state_no_potion = state_with_potion.clone();
    state_no_potion.set(KEY_HAS_POTIONS, false);

    let plan2 = planner
        .plan(&state_no_potion, &goal_vitals, &actions)
        .expect("Plan must be found");
    let plan2_names: Vec<&str> = plan2.iter().map(|a| a.name.as_str()).collect();
    assert_eq!(
        plan2_names,
        vec!["travel_ravenwood_to_commons", "refill_potions_at_fountain"]
    );
}

#[test]
fn test_end_to_end_starter_prologue_synthesis() {
    let actions = build_wizard_city_starter_repertoire();
    // Allow up to 10000 iterations for multi-stage prologue synthesis
    let planner = GoapPlanner::new(10000);

    let start = initial_fresh_wizard_state();
    let goal = goal_complete_good_impression();

    let plan = planner
        .plan(&start, &goal, &actions)
        .expect("Multi-quest prologue plan must be synthesized");

    // Verify all milestones are reached sequentially
    let mut current = start;
    for action in &plan {
        current = action.apply(&current);
    }

    assert_eq!(current.get_bool(KEY_COMPLETED_LORE_OF_MAGIC), Some(true));
    assert_eq!(current.get_bool(KEY_COMPLETED_ENROLLMENT), Some(true));
    assert_eq!(current.get_bool(KEY_COMPLETED_GOOD_IMPRESSION), Some(true));
    assert_eq!(current.get_str(KEY_ZONE), Some(ZONE_HEADMASTER_OFFICE));
}

#[test]
fn test_recovery_from_unexpected_displacement() {
    let actions = build_wizard_city_starter_repertoire();
    let planner = GoapPlanner::new(2000);

    // Player somehow displaced into Shopping District while working on Enrollment
    let mut displaced = initial_fresh_wizard_state();
    displaced.set(KEY_ZONE, ZONE_SHOPPING_DISTRICT);
    displaced.set(KEY_COMPLETED_LORE_OF_MAGIC, true);
    displaced.set(KEY_HAS_QUEST_ENROLLMENT, true);
    displaced.set(KEY_ENROLLED_FIRE, true);
    displaced.set(KEY_ENROLLED_ICE, true);
    displaced.set(KEY_ENROLLED_STORM, true);
    displaced.set(KEY_ENROLLED_MYTH, true);
    displaced.set(KEY_ENROLLED_LIFE, true);
    // Only Death school remaining

    let goal = goal_complete_enrollment();
    let plan = planner
        .plan(&displaced, &goal, &actions)
        .expect("Plan from displaced zone must succeed");

    let action_names: Vec<&str> = plan.iter().map(|a| a.name.as_str()).collect();
    assert_eq!(action_names[0], "travel_shopping_district_to_commons");
    assert_eq!(action_names[1], "travel_commons_to_ravenwood");
    assert!(action_names.contains(&"visit_school_death"));
    assert_eq!(
        action_names[action_names.len() - 1],
        "ambrose_turnin_enrollment"
    );
}

#[test]
fn test_unicorn_way_npc_interaction_anchors() {
    let oryan = get_npc_interaction_target("Private O'Ryan").expect("Private O'Ryan must exist");
    assert_eq!(oryan.zone, ZONE_UNICORN_WAY);
    assert_eq!(oryan.dialogue_steps, 2);
    assert!(oryan.prompt_bounds.width() > 0.0);
    assert!(oryan.prompt_bounds.height() > 0.0);
    let (cx, cy) = oryan.prompt_bounds.center();
    assert!((570.0..=690.0).contains(&cx));
    assert!((510.0..=550.0).contains(&cy));

    let stillson =
        get_npc_interaction_target("Private Stillson").expect("Private Stillson must exist");
    assert_eq!(stillson.zone, ZONE_UNICORN_WAY);
    assert_eq!(stillson.dialogue_steps, 2);
    assert!(stillson.prompt_bounds.width() > 0.0);
    assert!(stillson.prompt_bounds.height() > 0.0);
    let (sx, sy) = stillson.prompt_bounds.center();
    assert!((580.0..=700.0).contains(&sx));
    assert!((520.0..=560.0).contains(&sy));

    let oriel = get_npc_interaction_target("Lady Oriel").expect("Lady Oriel must exist");
    assert_eq!(oriel.zone, ZONE_UNICORN_WAY);
    assert_eq!(oriel.dialogue_steps, 3);
    assert!(oriel.prompt_bounds.width() > 0.0);
    assert!(oriel.prompt_bounds.height() > 0.0);
    let (lx, ly) = oriel.prompt_bounds.center();
    assert!((560.0..=710.0).contains(&lx));
    assert!((490.0..=540.0).contains(&ly));
}

#[test]
fn test_plan_unicorn_way_from_good_impression_completion() {
    let actions = build_wizard_city_starter_repertoire();
    let planner = GoapPlanner::new(2000);

    let mut start = initial_fresh_wizard_state();
    start.set(KEY_COMPLETED_LORE_OF_MAGIC, true);
    start.set(KEY_COMPLETED_ENROLLMENT, true);
    start.set(KEY_COMPLETED_GOOD_IMPRESSION, true);

    let goal = goal_complete_unicorn_way();
    let plan = planner
        .plan(&start, &goal, &actions)
        .expect("Plan for Unicorn Way must be found");

    let action_names: Vec<&str> = plan.iter().map(|a| a.name.as_str()).collect();
    assert_eq!(
        action_names,
        vec![
            "ambrose_accept_unicorn_way",
            "travel_office_to_commons",
            "travel_commons_to_unicorn_way",
            "talk_to_private_oryan",
            "engage_and_defeat_lost_soul",
            "oryan_turnin_lost_souls",
            "talk_to_private_stillson",
            "engage_and_defeat_skeletal_pirate",
            "stillson_turnin_skeletal_pirates",
            "talk_to_lady_oriel",
            "travel_unicorn_way_to_commons",
            "travel_commons_to_office",
            "ambrose_turnin_unicorn_way",
        ]
    );

    // Verify state progression
    let mut current = start;
    for action in &plan {
        current = action.apply(&current);
    }
    assert_eq!(current.get_bool(KEY_COMPLETED_UNICORN_WAY), Some(true));
    assert_eq!(current.get_bool(KEY_LOST_SOULS_DEFEATED), Some(true));
    assert_eq!(current.get_bool(KEY_SKELETAL_PIRATES_DEFEATED), Some(true));
    assert_eq!(current.get_str(KEY_ZONE), Some(ZONE_HEADMASTER_OFFICE));
}

#[test]
fn test_combat_safety_precondition_forces_potion_recovery() {
    let actions = build_wizard_city_starter_repertoire();
    let planner = GoapPlanner::new(2000);

    // Player in Unicorn Way with quest active, but low vitals and has potion
    let mut state = initial_fresh_wizard_state();
    state.set(KEY_ZONE, ZONE_UNICORN_WAY);
    state.set(KEY_HAS_QUEST_DEFEAT_LOST_SOULS, true);
    state.set(KEY_VITALS_LOW, true);
    state.set(KEY_HAS_POTIONS, true);

    let mut goal = maelstrom_core::goap::WorldState::new();
    goal.set(KEY_LOST_SOULS_DEFEATED, true);

    let plan = planner
        .plan(&state, &goal, &actions)
        .expect("Plan must be synthesized");
    let action_names: Vec<&str> = plan.iter().map(|a| a.name.as_str()).collect();

    // Invariant: drink_potion_recovery must occur before engage_and_defeat_lost_soul
    assert_eq!(
        action_names,
        vec!["drink_potion_recovery", "engage_and_defeat_lost_soul"]
    );
}

#[test]
fn test_combat_safety_precondition_forces_fountain_trip_when_potions_exhausted() {
    let actions = build_wizard_city_starter_repertoire();
    let planner = GoapPlanner::new(2000);

    // Player in Unicorn Way with quest active, low vitals, and NO potions
    let mut state = initial_fresh_wizard_state();
    state.set(KEY_ZONE, ZONE_UNICORN_WAY);
    state.set(KEY_HAS_QUEST_DEFEAT_LOST_SOULS, true);
    state.set(KEY_VITALS_LOW, true);
    state.set(KEY_HAS_POTIONS, false);

    let mut goal = maelstrom_core::goap::WorldState::new();
    goal.set(KEY_LOST_SOULS_DEFEATED, true);

    let plan = planner
        .plan(&state, &goal, &actions)
        .expect("Plan must route to Commons fountain and back");
    let action_names: Vec<&str> = plan.iter().map(|a| a.name.as_str()).collect();

    assert_eq!(
        action_names,
        vec![
            "travel_unicorn_way_to_commons",
            "refill_potions_at_fountain",
            "travel_commons_to_unicorn_way",
            "engage_and_defeat_lost_soul",
        ]
    );
}

#[test]
fn test_end_to_end_fresh_wizard_to_unicorn_way_completion() {
    let actions = build_wizard_city_starter_repertoire();
    // Complete prologue + Unicorn Way requires expanding state search
    let planner = GoapPlanner::new(20000);

    let start = initial_fresh_wizard_state();
    let goal = goal_complete_unicorn_way();

    let plan = planner
        .plan(&start, &goal, &actions)
        .expect("Full progression plan from Ambrose to Unicorn Way completion must succeed");

    let mut current = start;
    for action in &plan {
        current = action.apply(&current);
    }

    assert_eq!(current.get_bool(KEY_COMPLETED_LORE_OF_MAGIC), Some(true));
    assert_eq!(current.get_bool(KEY_COMPLETED_ENROLLMENT), Some(true));
    assert_eq!(current.get_bool(KEY_COMPLETED_GOOD_IMPRESSION), Some(true));
    assert_eq!(current.get_bool(KEY_COMPLETED_UNICORN_WAY), Some(true));
    assert_eq!(current.get_str(KEY_ZONE), Some(ZONE_HEADMASTER_OFFICE));
}

#[test]
fn test_spatial_anchors_olde_town_and_triton() {
    let muldoon = find_spatial_anchor("Sergeant Muldoon").expect("Muldoon must exist");
    assert_eq!(muldoon.zone, ZONE_OLDE_TOWN);
    assert!(muldoon.is_npc);
    assert_eq!(muldoon.x, 120.0);

    let susie = find_spatial_anchor("Susie Gryphonbane").expect("Susie must exist");
    assert_eq!(susie.zone, ZONE_TRITON_AVENUE);
    assert!(susie.is_npc);

    let duncan = find_spatial_anchor("Duncan Grimwater").expect("Duncan must exist");
    assert_eq!(duncan.zone, ZONE_TRITON_AVENUE);

    let artur = find_spatial_anchor("Artur Wethersfield").expect("Artur must exist");
    assert_eq!(artur.zone, ZONE_TRITON_AVENUE);

    let towers = find_spatial_anchor("Haunted Towers").expect("Haunted Towers must exist");
    assert_eq!(towers.zone, ZONE_TRITON_AVENUE);
    assert!(!towers.is_npc);

    let kraken = find_spatial_anchor("Kraken Gate").expect("Kraken Gate must exist");
    assert_eq!(kraken.zone, ZONE_TRITON_AVENUE);
    assert!(!kraken.is_npc);

    assert!(find_spatial_anchor("UnknownEntity").is_none());
}

#[test]
fn test_travel_to_triton_avenue_via_olde_town() {
    let actions = build_wizard_city_starter_repertoire();
    let planner = GoapPlanner::new(2000);

    let mut start = initial_fresh_wizard_state();
    start.set(KEY_ZONE, ZONE_COMMONS);

    let mut goal = maelstrom_core::goap::WorldState::new();
    goal.set(KEY_ZONE, ZONE_TRITON_AVENUE);

    let plan = planner
        .plan(&start, &goal, &actions)
        .expect("Plan from Commons to Triton Avenue must succeed");

    let action_names: Vec<&str> = plan.iter().map(|a| a.name.as_str()).collect();
    assert_eq!(
        action_names,
        vec!["travel_commons_to_olde_town", "travel_olde_town_to_triton"]
    );
}

#[test]
fn test_quest_interpreter_anchor_resolution_pipeline() {
    use maelstrom_core::quest_interpreter::*;

    // Step 1: Parse live tracker string into generic quest fact
    let fact = W101QuestFact::parse_from_text(
        "Trouble on Triton Avenue",
        "Talk to Susie Gryphonbane in Triton Avenue",
        true,
        Some(115.0),
    )
    .expect("Must parse talk archetype");

    assert_eq!(fact.archetype, QuestArchetype::TalkToNpc);
    assert_eq!(fact.target_entity_or_landmark, "Susie Gryphonbane");

    // Step 2: Resolve spatial anchor from decoupled world topology
    let anchor = find_spatial_anchor(&fact.target_entity_or_landmark)
        .expect("Anchor for Susie Gryphonbane must resolve in registry");
    assert_eq!(anchor.zone, ZONE_TRITON_AVENUE);
    assert_eq!(anchor.x, 350.0);
    assert_eq!(anchor.y, -200.0);
    assert_eq!(anchor.z, 10.0);

    // Step 3: Parse landmark explore quest
    let explore_fact = W101QuestFact::parse_from_text(
        "A Haunting Problem",
        "Explore Haunted Towers in Triton Avenue",
        true,
        None,
    )
    .expect("Must parse explore archetype");

    let tower_anchor = find_spatial_anchor(&explore_fact.target_entity_or_landmark)
        .expect("Anchor for Haunted Towers must resolve");
    assert_eq!(tower_anchor.zone, ZONE_TRITON_AVENUE);
    assert_eq!(tower_anchor.z, 35.0);
}
