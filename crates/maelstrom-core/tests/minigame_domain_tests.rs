//! Integration tests for the Minigame Domain & Damage Pipeline (`minigame_domain.rs`).
//! Validates pet energy scaling, zero-waste exit logic, Silver/Cantrip chest policies,
//! and the exact Wizard101 damage stack calculation pipeline.

use maelstrom_core::minigame_domain::*;

#[test]
fn test_pet_age_energy_scaling_and_max_xp() {
    assert_eq!(PetAgeBracket::Baby.energy_cost(), 2);
    assert_eq!(PetAgeBracket::Teen.energy_cost(), 4);
    assert_eq!(PetAgeBracket::Adult.energy_cost(), 6);
    assert_eq!(PetAgeBracket::Ancient.energy_cost(), 8);
    assert_eq!(PetAgeBracket::Epic.energy_cost(), 10);
    assert_eq!(PetAgeBracket::Mega.energy_cost(), 12);
    assert_eq!(PetAgeBracket::Ultra.energy_cost(), 14);

    // Hard cap: 4 XP standard, 8 XP during Double Pet XP
    for age in [
        PetAgeBracket::Baby,
        PetAgeBracket::Teen,
        PetAgeBracket::Adult,
        PetAgeBracket::Ancient,
        PetAgeBracket::Epic,
        PetAgeBracket::Mega,
        PetAgeBracket::Ultra,
    ] {
        assert_eq!(age.max_base_xp(false), 4);
        assert_eq!(age.max_base_xp(true), 8);
    }
}

#[test]
fn test_pet_pavilion_mini_game_tiers() {
    assert_eq!(
        PetPavilionMiniGame::DanceGame.priority_tier(),
        AutomationPriorityTier::Tier1Recommended
    );
    assert!(PetPavilionMiniGame::DanceGame.is_recommended_for_automation());

    assert_eq!(
        PetPavilionMiniGame::GobblerDrop.priority_tier(),
        AutomationPriorityTier::Tier2ViableFallback
    );
    assert!(PetPavilionMiniGame::GobblerDrop.is_recommended_for_automation());

    assert_eq!(
        PetPavilionMiniGame::MazeGame.priority_tier(),
        AutomationPriorityTier::Tier2ViableFallback
    );
    assert!(PetPavilionMiniGame::MazeGame.is_recommended_for_automation());

    assert_eq!(
        PetPavilionMiniGame::CannonGame.priority_tier(),
        AutomationPriorityTier::Tier3Constrained
    );
    assert!(!PetPavilionMiniGame::CannonGame.is_recommended_for_automation());

    assert_eq!(
        PetPavilionMiniGame::GrumpyGobblers.priority_tier(),
        AutomationPriorityTier::Tier4Deprioritized
    );

    assert_eq!(
        PetPavilionMiniGame::WayOfNinjaPig.priority_tier(),
        AutomationPriorityTier::Excluded
    );
    assert!(!PetPavilionMiniGame::WayOfNinjaPig.is_recommended_for_automation());
}

#[test]
fn test_dance_game_zero_waste_exit() {
    // 1. Promenade Win Only goal (3 rounds needed)
    match evaluate_dance_game_exit(2, PetGameGoal::PromenadeWinOnly) {
        PetGameExitDecision::ContinuePlaying { current_progress } => {
            assert!((current_progress - 2.0 / 3.0).abs() < 1e-4);
        }
        _ => panic!("Expected ContinuePlaying for 2/3 rounds"),
    }

    match evaluate_dance_game_exit(3, PetGameGoal::PromenadeWinOnly) {
        PetGameExitDecision::ExitImmediately { reason, .. } => {
            assert!(reason.contains("Pet Promenade win condition met"));
        }
        _ => panic!("Expected ExitImmediately on 3 rounds for Promenade"),
    }

    // 2. Max Pet XP goal (5 rounds needed)
    match evaluate_dance_game_exit(4, PetGameGoal::MaxPetXp) {
        PetGameExitDecision::ContinuePlaying { current_progress } => {
            assert!((current_progress - 4.0 / 5.0).abs() < 1e-4);
        }
        _ => panic!("Expected ContinuePlaying for 4/5 rounds"),
    }

    match evaluate_dance_game_exit(5, PetGameGoal::MaxPetXp) {
        PetGameExitDecision::ExitImmediately { reason, xp_awarded } => {
            assert_eq!(xp_awarded, 4);
            assert!(reason.contains("Maximum 4 XP threshold reached"));
        }
        _ => panic!("Expected ExitImmediately on 5 rounds for MaxPetXp"),
    }
}

#[test]
fn test_maze_game_zero_waste_exit() {
    // Under 70 snacks: must avoid +10s clock pickups to prevent prolonging the run
    match evaluate_maze_game_exit(55) {
        PetGameExitDecision::AvoidPickups {
            pickup_name,
            current_progress,
        } => {
            assert_eq!(pickup_name, "+10s Clock");
            assert!((current_progress - 55.0 / 70.0).abs() < 1e-4);
        }
        _ => panic!("Expected AvoidPickups under 70 snacks"),
    }

    // Exactly 70 snacks: exit immediately
    match evaluate_maze_game_exit(70) {
        PetGameExitDecision::ExitImmediately { reason, xp_awarded } => {
            assert_eq!(xp_awarded, 4);
            assert!(reason.contains("Maximum 70 snacks collected"));
        }
        _ => panic!("Expected ExitImmediately at 70 snacks"),
    }
}

#[test]
fn test_gobbler_drop_zero_waste_exit() {
    let target = 800;

    match evaluate_gobbler_drop_exit(600, target) {
        PetGameExitDecision::ContinuePlaying { current_progress } => {
            assert!((current_progress - 600.0 / 800.0).abs() < 1e-4);
        }
        _ => panic!("Expected ContinuePlaying below score target"),
    }

    match evaluate_gobbler_drop_exit(850, target) {
        PetGameExitDecision::ExitImmediately { reason, xp_awarded } => {
            assert_eq!(xp_awarded, 4);
            assert!(reason.contains("Score threshold reached"));
        }
        _ => panic!("Expected ExitImmediately above target score"),
    }
}

#[test]
fn test_silver_chest_retry_policy_and_cooldown() {
    let mut policy = SilverChestPolicy::default(); // max_retries = 2
    assert!(policy.can_retry());

    // First failure
    let cooldown_required1 = policy.record_failure();
    assert!(!cooldown_required1);
    assert!(policy.can_retry());
    assert_eq!(policy.current_attempts, 1);

    // Second failure
    let cooldown_required2 = policy.record_failure();
    assert!(cooldown_required2);
    assert!(!policy.can_retry());
    assert_eq!(policy.current_attempts, 2);
}

#[test]
fn test_cantrip_chest_solo_vs_multi_player_check() {
    let solo_chest = CantripChestType::Solo;
    assert!(solo_chest.can_attempt(0));
    assert!(solo_chest.can_attempt(3));

    let three_lock = CantripChestType::ThreeLock {
        required_wizards: 3,
    };
    // Only player present: 0 nearby players -> 1 wizard < 3 -> False
    assert!(!three_lock.can_attempt(0));
    // 1 nearby player -> 2 wizards < 3 -> False
    assert!(!three_lock.can_attempt(1));
    // 2 nearby players -> 3 wizards >= 3 -> True
    assert!(three_lock.can_attempt(2));
    assert!(three_lock.can_attempt(5));
}

#[test]
fn test_exact_damage_calculation_pipeline() {
    let mut inputs = DamagePipelineInputs::new(895.0); // Efreet
    inputs.enchant_damage = 300.0; // Colossal -> Base+Enchant = 1195
    inputs.gear_damage_percent = 0.50; // +50% gear -> 1195 * 1.5 = 1792.5 -> floor = 1792
    inputs.flat_damage = 35.0; // Flat damage -> 1792 + 35 = 1827
    inputs.aura_multiplier = 0.20; // +20% Galvanic/Furnace Aura -> 1827 * 1.20 = 2192.4
    inputs.charms_lifo = vec![0.35, 0.45]; // Fireblade (+35%), Sharpened Fireblade (+45%) -> 2192.4 * 1.35 * 1.45 = 4291.629
    inputs.global_bubble_multiplier = 0.25; // Wyldfire (+25%) -> 4291.629 * 1.25 = 5364.53625
    inputs.wards_lifo = vec![0.70, 0.80]; // Feint (+70%), Potent Feint (+80%) -> 5364.53625 * 1.70 * 1.80 = 16415.48
    inputs.critical_multiplier = 2.0; // Critical strike -> 16415.48 * 2.0 = 32830.96
    inputs.enemy_resist = 0.40; // 40% resist
    inputs.player_pierce = 0.25; // 25% pierce -> effective resist = 15% -> 85% passed -> 32830.96 * 0.85 = 27906.316 -> round = 27906

    let damage = calculate_pipeline_damage(&inputs);
    assert_eq!(damage, 27906);

    // Negative case: 0 base damage
    let zero_inputs = DamagePipelineInputs::new(0.0);
    assert_eq!(calculate_pipeline_damage(&zero_inputs), 0);
}

#[test]
fn test_damage_pipeline_enemy_boost_amplification() {
    let mut inputs = DamagePipelineInputs::new(500.0);
    inputs.enemy_resist = -0.30; // Natural 30% elemental boost against target school
    inputs.player_pierce = 0.15; // Pierce should not amplify boost beyond 30%

    // 500 * (1.0 - (-0.30)) = 500 * 1.30 = 650
    let damage = calculate_pipeline_damage(&inputs);
    assert_eq!(
        damage, 650,
        "Negative resistance (enemy boost) must amplify incoming spell damage"
    );
}

#[test]
fn test_pierce_never_turns_positive_resist_into_a_boost() {
    // Pierce larger than the resist floors the effective resist at 0: no boost is created.
    let mut over_pierced = DamagePipelineInputs::new(500.0);
    over_pierced.enemy_resist = 0.20;
    over_pierced.player_pierce = 0.50;
    assert_eq!(calculate_pipeline_damage(&over_pierced), 500);

    // Pierce equal to the resist cancels it exactly.
    let mut cancelled = DamagePipelineInputs::new(500.0);
    cancelled.enemy_resist = 0.40;
    cancelled.player_pierce = 0.40;
    assert_eq!(calculate_pipeline_damage(&cancelled), 500);

    // Resist of 100% or more passes nothing through (the factor is floored at 0).
    let mut immune = DamagePipelineInputs::new(500.0);
    immune.enemy_resist = 1.5;
    assert_eq!(calculate_pipeline_damage(&immune), 0);
}

#[test]
fn test_charm_and_ward_order_does_not_change_damage_today() {
    // The `*_lifo` names promise a stack order, but every factor is multiplied in and multiplication
    // commutes, so the order has no effect in this implementation. If real LIFO behaviour is ever
    // added (the game's own order has not been observed), this test must change deliberately.
    let damage = |charms: Vec<f64>, wards: Vec<f64>| {
        let mut inputs = DamagePipelineInputs::new(895.0);
        inputs.charms_lifo = charms;
        inputs.wards_lifo = wards;
        calculate_pipeline_damage(&inputs)
    };
    assert_eq!(
        damage(vec![0.35, 0.45], vec![]),
        damage(vec![0.45, 0.35], vec![])
    );
    assert_eq!(
        damage(vec![], vec![0.70, 0.80]),
        damage(vec![], vec![0.80, 0.70])
    );
}
