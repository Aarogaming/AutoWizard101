//! Integration tests for the Combat Turn Evaluator Engine (`combat_turn_evaluator.rs`).
//! Validates pip calculations (regular vs. power pips), blade/trap multiplier damage calculations,
//! lethal kill threshold priority, AoE preference in multi-mob encounters, and critical health healing.

use maelstrom_core::combat_turn_evaluator::*;

#[test]
fn test_power_pip_accounting_same_school_vs_off_school() {
    let pips = CombatPips::new(1, 2, 0); // 1 regular pip, 2 power pips

    // Same school (Fire card for Fire player): 1 + 2*2 = 5 effective pips
    assert_eq!(pips.effective_pips(MagicSchool::Fire, MagicSchool::Fire), 5);

    // Balance school (treated universally): 1 + 2*2 = 5 effective pips
    assert_eq!(
        pips.effective_pips(MagicSchool::Balance, MagicSchool::Fire),
        5
    );

    // Off-school (Storm card for Fire player without mastery): 1 + 2*1 = 3 effective pips
    assert_eq!(
        pips.effective_pips(MagicSchool::Storm, MagicSchool::Fire),
        3
    );

    let fire_spell_4pip = CombatCardCandidate::new(
        0,
        "Meteor Strike",
        MagicSchool::Fire,
        4,
        SpellCardType::DamageAoE,
    );
    let storm_spell_4pip = CombatCardCandidate::new(
        1,
        "Kraken",
        MagicSchool::Storm,
        4,
        SpellCardType::DamageSingle,
    );

    assert!(pips.can_afford(&fire_spell_4pip, MagicSchool::Fire));
    assert!(!pips.can_afford(&storm_spell_4pip, MagicSchool::Fire));
}

#[test]
fn test_lethal_kill_threshold_priority() {
    let evaluator = CombatTurnEvaluator::new(MagicSchool::Fire, 0.25); // +25% gear damage
    let pips = CombatPips::new(0, 3, 0); // 6 effective pips

    let player = CombatantState::new(4, "Player", MagicSchool::Fire, 450.0);
    let enemies = vec![
        CombatantState::new(0, "Lost Soul", MagicSchool::Death, 120.0), // Low health enemy
        CombatantState::new(1, "Skeletal Pirate", MagicSchool::Death, 350.0),
    ];

    let hand = vec![
        CombatCardCandidate::new(
            0,
            "Fire Blade",
            MagicSchool::Fire,
            2,
            SpellCardType::BladeSelf,
        )
        .with_buff(0.35),
        CombatCardCandidate::new(
            1,
            "Firecat",
            MagicSchool::Fire,
            1,
            SpellCardType::DamageSingle,
        )
        .with_damage(100.0), // 100 * 1.25 = 125 >= 120 lethal!
        CombatCardCandidate::new(
            2,
            "Sunbird",
            MagicSchool::Fire,
            3,
            SpellCardType::DamageSingle,
        )
        .with_damage(300.0),
    ];

    let decision = evaluator.evaluate_turn(&pips, &hand, &player, &enemies);

    match decision {
        CombatDecision::PlayCard {
            slot_index,
            target_index,
            is_lethal,
            spell_name,
            ..
        } => {
            assert!(is_lethal, "Evaluator must recognize lethal kill threshold");
            assert_eq!(slot_index, 1, "Must pick Firecat to lethal Lost Soul");
            assert_eq!(target_index, 0, "Must target Lost Soul on pedestal 0");
            assert_eq!(spell_name, "Firecat");
        }
        _ => panic!("Expected PlayCard with lethal kill"),
    }
}

#[test]
fn test_aoe_priority_in_multi_enemy_encounter() {
    let evaluator = CombatTurnEvaluator::new(MagicSchool::Fire, 0.20);
    let pips = CombatPips::new(0, 2, 0); // 4 effective pips

    let player = CombatantState::new(4, "Player", MagicSchool::Fire, 400.0);
    let enemies = vec![
        CombatantState::new(0, "Field Guard A", MagicSchool::Death, 300.0),
        CombatantState::new(1, "Field Guard B", MagicSchool::Death, 320.0),
    ];

    let hand = vec![
        CombatCardCandidate::new(
            0,
            "Sunbird",
            MagicSchool::Fire,
            3,
            SpellCardType::DamageSingle,
        )
        .with_damage(310.0),
        CombatCardCandidate::new(
            1,
            "Meteor Strike",
            MagicSchool::Fire,
            4,
            SpellCardType::DamageAoE,
        )
        .with_damage(320.0),
    ];

    let decision = evaluator.evaluate_turn(&pips, &hand, &player, &enemies);

    match decision {
        CombatDecision::PlayCard {
            slot_index,
            target_index,
            spell_name,
            ..
        } => {
            assert_eq!(slot_index, 1);
            assert_eq!(target_index, 0, "AoE spells must target pedestal 0");
            assert_eq!(spell_name, "Meteor Strike");
        }
        _ => panic!("Expected Meteor Strike AoE play"),
    }
}

#[test]
fn test_buff_stacking_when_no_lethal_kill() {
    let evaluator = CombatTurnEvaluator::new(MagicSchool::Fire, 0.10);
    let pips = CombatPips::new(2, 1, 0); // 4 effective pips

    let player = CombatantState::new(4, "Player", MagicSchool::Fire, 500.0);
    let enemies = vec![
        CombatantState::new(0, "Baron Rotunda", MagicSchool::Death, 1200.0), // Boss cannot be one-shot
    ];

    let hand = vec![
        CombatCardCandidate::new(
            0,
            "Fire Blade",
            MagicSchool::Fire,
            2,
            SpellCardType::BladeSelf,
        )
        .with_buff(0.35),
        CombatCardCandidate::new(
            1,
            "Sunbird",
            MagicSchool::Fire,
            3,
            SpellCardType::DamageSingle,
        )
        .with_damage(320.0),
    ];

    let decision = evaluator.evaluate_turn(&pips, &hand, &player, &enemies);

    match decision {
        CombatDecision::PlayCard {
            slot_index,
            target_index,
            spell_name,
            is_lethal,
            ..
        } => {
            assert!(!is_lethal);
            assert_eq!(
                slot_index, 0,
                "Must prioritize blade stacking when lethal kill is impossible"
            );
            assert_eq!(
                target_index, 4,
                "Self blade must target player on pedestal 4"
            );
            assert_eq!(spell_name, "Fire Blade");
        }
        _ => panic!("Expected Fire Blade play"),
    }
}

#[test]
fn test_critical_health_healing_priority() {
    let evaluator = CombatTurnEvaluator::new(MagicSchool::Fire, 0.10);
    let pips = CombatPips::new(2, 0, 0); // 2 pips

    // Player health 100 / 500 = 20% (< 30% critical threshold)
    let player = CombatantState::new(4, "Player", MagicSchool::Fire, 100.0).with_max_health(500.0);
    let enemies = vec![CombatantState::new(0, "Boss", MagicSchool::Death, 800.0)];

    let hand = vec![
        CombatCardCandidate::new(0, "Pixie", MagicSchool::Life, 2, SpellCardType::HealSelf)
            .with_heal(420.0),
        CombatCardCandidate::new(
            1,
            "Firecat",
            MagicSchool::Fire,
            1,
            SpellCardType::DamageSingle,
        )
        .with_damage(100.0),
    ];

    let decision = evaluator.evaluate_turn(&pips, &hand, &player, &enemies);

    match decision {
        CombatDecision::PlayCard {
            slot_index,
            target_index,
            spell_name,
            ..
        } => {
            assert_eq!(
                slot_index, 0,
                "Must prioritize healing when below critical health threshold"
            );
            assert_eq!(
                target_index, 4,
                "Self heal must target player on pedestal 4"
            );
            assert_eq!(spell_name, "Pixie");
        }
        _ => panic!("Expected Pixie heal play"),
    }
}

#[test]
fn test_pass_when_no_affordable_cards() {
    let evaluator = CombatTurnEvaluator::new(MagicSchool::Fire, 0.10);
    let pips = CombatPips::new(0, 0, 0); // 0 pips

    let player = CombatantState::new(4, "Player", MagicSchool::Fire, 500.0);
    let enemies = vec![CombatantState::new(0, "Enemy", MagicSchool::Death, 200.0)];

    let hand = vec![
        CombatCardCandidate::new(
            0,
            "Fire Blade",
            MagicSchool::Fire,
            2,
            SpellCardType::BladeSelf,
        ),
        CombatCardCandidate::new(
            1,
            "Sunbird",
            MagicSchool::Fire,
            3,
            SpellCardType::DamageSingle,
        ),
    ];

    let decision = evaluator.evaluate_turn(&pips, &hand, &player, &enemies);

    match decision {
        CombatDecision::Pass { reason } => {
            assert!(reason.contains("No affordable cards"));
        }
        _ => panic!("Expected Pass when pips are insufficient"),
    }
}
