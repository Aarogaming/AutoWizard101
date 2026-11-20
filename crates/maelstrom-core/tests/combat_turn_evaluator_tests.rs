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

#[test]
fn test_boss_cheat_forbid_traps_constraint() {
    let evaluator = CombatTurnEvaluator::new(MagicSchool::Fire, 0.20);
    let pips = CombatPips::new(0, 2, 0); // 4 effective pips

    // Player already has 2 blades active (so blade stacking won't trigger)
    let player =
        CombatantState::new(4, "Player", MagicSchool::Fire, 500.0).with_blades(vec![0.35, 0.40]);

    // Shane von Shane with cheat: Vampire interrupt on trap placement -> CounterConstraint::ForbidTraps
    let shane_cheat = BossCheatRule::new(
        "Shane Vampire Trap Interrupt",
        CheatTriggerCondition::SpellCastType(SpellCardType::TrapEnemy),
        CheatInterruptPhase::ImmediateReaction,
        CheatEffect::CastSpell {
            spell_name: "Monster Vampire".into(),
            target_pedestal: 4,
        },
    )
    .with_counter_constraint(CounterConstraint::ForbidTraps);

    let enemies = vec![
        CombatantState::new(0, "Shane von Shane", MagicSchool::Death, 5000.0)
            .with_cheat(shane_cheat),
    ];

    let hand = vec![
        CombatCardCandidate::new(0, "Feint", MagicSchool::Death, 1, SpellCardType::TrapEnemy)
            .with_buff(0.70),
        CombatCardCandidate::new(
            1,
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
            spell_name,
            is_lethal,
            ..
        } => {
            assert!(!is_lethal);
            assert_eq!(
                slot_index, 1,
                "Must skip Feint due to ForbidTraps cheat and select Sunbird fallback"
            );
            assert_eq!(spell_name, "Sunbird");
        }
        _ => panic!("Expected Sunbird fallback play"),
    }
}

#[test]
fn test_boss_cheat_forbid_blades_constraint() {
    let evaluator = CombatTurnEvaluator::new(MagicSchool::Fire, 0.20);
    let pips = CombatPips::new(0, 2, 0); // 4 pips

    // Player has 0 blades, so normally blade stacking would trigger
    let player = CombatantState::new(4, "Player", MagicSchool::Fire, 600.0);

    // Belloq cheat: cheats when bladed -> CounterConstraint::ForbidBlades
    let belloq_cheat = BossCheatRule::new(
        "Belloq Blade Retaliation",
        CheatTriggerCondition::SpellCastType(SpellCardType::BladeSelf),
        CheatInterruptPhase::ImmediateReaction,
        CheatEffect::CastSpell {
            spell_name: "Ra".into(),
            target_pedestal: 0,
        },
    )
    .with_counter_constraint(CounterConstraint::ForbidBlades);

    let enemies = vec![
        CombatantState::new(0, "Belloq", MagicSchool::Balance, 4000.0).with_cheat(belloq_cheat),
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
        .with_damage(310.0),
    ];

    let decision = evaluator.evaluate_turn(&pips, &hand, &player, &enemies);

    match decision {
        CombatDecision::PlayCard {
            slot_index,
            spell_name,
            ..
        } => {
            assert_eq!(
                slot_index, 1,
                "Must skip Fire Blade due to ForbidBlades cheat and select Sunbird"
            );
            assert_eq!(spell_name, "Sunbird");
        }
        _ => panic!("Expected Sunbird attack"),
    }
}

#[test]
fn test_boss_cheat_damage_cap_and_dot_priority() {
    let evaluator = CombatTurnEvaluator::new(MagicSchool::Fire, 0.50); // +50% gear dmg
    let pips = CombatPips::new(0, 4, 0); // 8 effective pips

    let player =
        CombatantState::new(4, "Player", MagicSchool::Fire, 800.0).with_blades(vec![0.40, 0.45]); // Massive blades active

    // Boss has a 1,000 damage cap per hit cheat and requires damage-over-time spells
    let damage_cap_cheat = BossCheatRule::new(
        "Cronus Immortal Damage Barrier",
        CheatTriggerCondition::RoundNumber {
            modulo: 1,
            offset: 0,
        },
        CheatInterruptPhase::PassiveAura,
        CheatEffect::ApplyAura {
            aura_name: "DamageCapAura".into(),
        },
    )
    .with_counter_constraint(CounterConstraint::CapDamagePerHit(1000.0));

    let dot_req_cheat = BossCheatRule::new(
        "Tartarus Shield Requires DoT",
        CheatTriggerCondition::SpellCastType(SpellCardType::DamageSingle),
        CheatInterruptPhase::ImmediateReaction,
        CheatEffect::RemoveWards,
    )
    .with_counter_constraint(CounterConstraint::RequireDamageOverTime);

    let enemies = vec![
        CombatantState::new(0, "Tartarus Boss", MagicSchool::Death, 2500.0)
            .with_cheat(damage_cap_cheat)
            .with_cheat(dot_req_cheat),
    ];

    // Card 0: Huge single hit spell (Efreet: 895 base damage * 1.5 gear * 1.4 blade * 1.45 blade = 2,725 damage)
    // Normally 2,725 would be lethal against 2,500 health!
    // But with CapDamagePerHit(1000.0), damage is capped at 1,000, so NOT lethal.
    // Card 1: DoT spell (Fire Dragon: 440 base initial + burn)
    let hand = vec![
        CombatCardCandidate::new(
            0,
            "Efreet",
            MagicSchool::Fire,
            8,
            SpellCardType::DamageSingle,
        )
        .with_damage(895.0),
        CombatCardCandidate::new(
            1,
            "Fire Dragon",
            MagicSchool::Fire,
            7,
            SpellCardType::DamageOverTime,
        )
        .with_damage(440.0),
    ];

    // First: verify estimate_damage honors the cap
    let raw_efreet_est = evaluator.estimate_damage(&hand[0], &player.blades, &enemies[0]);
    assert_eq!(
        raw_efreet_est, 1000.0,
        "Damage cap must clamp single hit damage estimate to 1000.0"
    );

    // Second: evaluate turn - must prioritize DoT card due to RequireDamageOverTime constraint
    let decision = evaluator.evaluate_turn(&pips, &hand, &player, &enemies);

    match decision {
        CombatDecision::PlayCard {
            slot_index,
            spell_name,
            target_index,
            ..
        } => {
            assert_eq!(
                slot_index, 1,
                "Must prioritize Fire Dragon DoT spell over direct hit due to RequireDamageOverTime"
            );
            assert_eq!(spell_name, "Fire Dragon");
            assert_eq!(target_index, 0);
        }
        _ => panic!("Expected Fire Dragon DoT play"),
    }
}

#[test]
fn test_boss_cheat_forbid_healing_constraint() {
    let evaluator = CombatTurnEvaluator::new(MagicSchool::Fire, 0.10);
    let pips = CombatPips::new(2, 0, 0); // 2 pips

    // Player health 100 / 500 = 20% (< 30% critical threshold)
    let player = CombatantState::new(4, "Player", MagicSchool::Fire, 100.0).with_max_health(500.0);

    // Malistaire: Heals trigger instant lethal retribution -> CounterConstraint::ForbidHealing
    let malistaire_cheat = BossCheatRule::new(
        "Malistaire Doom And Gloom Retaliation",
        CheatTriggerCondition::SpellCastType(SpellCardType::HealSelf),
        CheatInterruptPhase::ImmediateReaction,
        CheatEffect::CastSpell {
            spell_name: "Shadow Gaze".into(),
            target_pedestal: 4,
        },
    )
    .with_counter_constraint(CounterConstraint::ForbidHealing);

    let enemies =
        vec![
            CombatantState::new(0, "Malistaire the Undying", MagicSchool::Death, 8000.0)
                .with_cheat(malistaire_cheat),
        ];

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
            spell_name,
            ..
        } => {
            assert_eq!(
                slot_index, 1,
                "Must skip Pixie heal due to ForbidHealing cheat and select Firecat"
            );
            assert_eq!(spell_name, "Firecat");
        }
        _ => panic!("Expected Firecat fallback attack"),
    }
}

#[test]
fn test_evaluator_selects_highest_magnitude_buff_and_dot() {
    let evaluator = CombatTurnEvaluator::new(MagicSchool::Fire, 0.20);
    let pips = CombatPips::new(0, 4, 0); // 8 pips

    let player = CombatantState::new(4, "Player", MagicSchool::Fire, 500.0);
    let enemies = vec![CombatantState::new(
        0,
        "High Health Boss",
        MagicSchool::Death,
        10000.0,
    )];

    // 1. Blade selection: slot 0 has Fireblade (+35%), slot 1 has Sharpened Blade (+45%)
    let hand_blades = vec![
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
            "Sharpened Fire Blade",
            MagicSchool::Fire,
            2,
            SpellCardType::BladeSelf,
        )
        .with_buff(0.45),
    ];

    let decision_blade = evaluator.evaluate_turn(&pips, &hand_blades, &player, &enemies);
    match decision_blade {
        CombatDecision::PlayCard {
            slot_index,
            spell_name,
            ..
        } => {
            assert_eq!(
                slot_index, 1,
                "Evaluator must pick the highest buff percentage blade (+45% over +35%)"
            );
            assert_eq!(spell_name, "Sharpened Fire Blade");
        }
        _ => panic!("Expected PlayCard blade play"),
    }

    // 2. DoT selection: boss requires DoT; slot 0 has Fire Elf (150 base), slot 1 has Fire Dragon (440 base)
    let dot_cheat = BossCheatRule::new(
        "Require DoT Shield",
        CheatTriggerCondition::RoundNumber {
            modulo: 1,
            offset: 0,
        },
        CheatInterruptPhase::PassiveAura,
        CheatEffect::ApplyAura {
            aura_name: "ShieldAura".into(),
        },
    )
    .with_counter_constraint(CounterConstraint::RequireDamageOverTime);

    let enemies_dot = vec![
        CombatantState::new(0, "Shielded Boss", MagicSchool::Death, 10000.0).with_cheat(dot_cheat),
    ];

    let hand_dots = vec![
        CombatCardCandidate::new(
            0,
            "Fire Elf",
            MagicSchool::Fire,
            2,
            SpellCardType::DamageOverTime,
        )
        .with_damage(150.0),
        CombatCardCandidate::new(
            1,
            "Fire Dragon",
            MagicSchool::Fire,
            7,
            SpellCardType::DamageOverTime,
        )
        .with_damage(440.0),
    ];

    let decision_dot = evaluator.evaluate_turn(&pips, &hand_dots, &player, &enemies_dot);
    match decision_dot {
        CombatDecision::PlayCard {
            slot_index,
            spell_name,
            ..
        } => {
            assert_eq!(
                slot_index, 1,
                "Evaluator must pick the highest base damage DoT card (Fire Dragon over Fire Elf)"
            );
            assert_eq!(spell_name, "Fire Dragon");
        }
        _ => panic!("Expected PlayCard DoT play"),
    }
}

#[test]
fn test_evaluator_selects_highest_buff_trap_in_either_hand_order() {
    let evaluator = CombatTurnEvaluator::new(MagicSchool::Fire, 0.20);
    let pips = CombatPips::new(0, 4, 0);
    let player = CombatantState::new(4, "Player", MagicSchool::Fire, 500.0);
    let enemies = vec![CombatantState::new(
        0,
        "High Health Boss",
        MagicSchool::Death,
        10000.0,
    )];

    let weak = |slot: usize| {
        CombatCardCandidate::new(
            slot,
            "Weak Trap",
            MagicSchool::Fire,
            2,
            SpellCardType::TrapEnemy,
        )
        .with_buff(0.25)
    };
    let strong = |slot: usize| {
        CombatCardCandidate::new(
            slot,
            "Strong Trap",
            MagicSchool::Fire,
            2,
            SpellCardType::TrapEnemy,
        )
        .with_buff(0.50)
    };

    for (hand, expected_slot) in [(vec![weak(0), strong(1)], 1), (vec![strong(0), weak(1)], 0)] {
        match evaluator.evaluate_turn(&pips, &hand, &player, &enemies) {
            CombatDecision::PlayCard {
                slot_index,
                spell_name,
                target_index,
                ..
            } => {
                assert_eq!(slot_index, expected_slot);
                assert_eq!(spell_name, "Strong Trap");
                assert_eq!(target_index, 0);
            }
            _ => panic!("Expected PlayCard trap play"),
        }
    }
}
