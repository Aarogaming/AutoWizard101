//! Integration tests for Combat and Spellbook geometry calculations.
//! Validates coordinate transformations, hand card spacing, target pedestal alignment,
//! and spellbook deck-switch UI resolution scaling across 800x600, 1280x720, and 1920x1080 viewports.

use maelstrom_core::combat_geometry::*;

#[test]
fn test_combat_geometry_target_pedestal_mapping() {
    // Enemies 0..3: top row, Y = 50 on 800x600
    for idx in 0..4 {
        let p = CombatGeometry::calculate_target_coordinates(idx).unwrap();
        assert_eq!(p.y, 50);
        assert_eq!(p.x, (174 * idx as i32) + 130);
    }

    // Allies 4..7: bottom row, Y = 590 on 800x600
    for idx in 4..8 {
        let p = CombatGeometry::calculate_target_coordinates(idx).unwrap();
        assert_eq!(p.y, 590);
        assert_eq!(p.x, (174 * (7 - idx as i32)) + 160);
    }

    // Invalid index > 7 must fail
    assert!(CombatGeometry::calculate_target_coordinates(8).is_err());
}

#[test]
fn test_combat_hand_card_spacing_all_hand_sizes() {
    // Test all valid hand counts from 1 to 7 cards
    for hand_size in 1..=7 {
        let mut prev_x = 0;
        for card_idx in 0..hand_size {
            let p = CombatGeometry::get_hand_card_coordinates(card_idx, hand_size).unwrap();
            assert_eq!(p.y, 325);
            // Each card must be placed to the right of previous card
            if card_idx > 0 {
                assert_eq!(p.x - prev_x, 52, "Cards must have 52px pitch");
            }
            prev_x = p.x;

            // Scaled 720p coordinates must be strictly within viewport [0..1280, 0..720]
            let (x_720, y_720) =
                CombatGeometry::get_hand_card_coordinates_720p(card_idx, hand_size).unwrap();
            assert!(x_720 > 0.0 && x_720 < 1280.0);
            assert!(y_720 > 0.0 && y_720 < 720.0);
        }
    }

    // Out of bounds cases
    assert!(CombatGeometry::get_hand_card_coordinates(0, 0).is_err());
    assert!(CombatGeometry::get_hand_card_coordinates(0, 8).is_err());
    assert!(CombatGeometry::get_hand_card_coordinates(3, 2).is_err());
}

#[test]
fn test_spellbook_tab_and_action_geometry() {
    let character = SpellbookGeometry::get_tab_coordinates(SpellbookTab::Character);
    let spells = SpellbookGeometry::get_tab_coordinates(SpellbookTab::Spells);
    let decks = SpellbookGeometry::get_tab_coordinates(SpellbookTab::Decks);

    assert_eq!(character, Point::new(176, 72));
    assert_eq!(spells, Point::new(208, 72));
    assert_eq!(decks, Point::new(240, 72));

    // Tab spacing is exactly 32px horizontally
    assert_eq!(spells.x - character.x, 32);
    assert_eq!(decks.x - spells.x, 32);

    // Verify 720p scaling (scale_x = 1.6, scale_y = 1.2)
    let decks_720p = SpellbookGeometry::get_deck_tab_coordinates_720p();
    assert!((decks_720p.0 - (240.0 * 1.6)).abs() < 1e-4);
    assert!((decks_720p.1 - (72.0 * 1.2)).abs() < 1e-4);

    let equip_720p = SpellbookGeometry::get_equip_button_coordinates_720p();
    assert!((equip_720p.0 - (400.0 * 1.6)).abs() < 1e-4);
    assert!((equip_720p.1 - (510.0 * 1.2)).abs() < 1e-4);

    let close_720p = SpellbookGeometry::get_close_button_coordinates_720p();
    assert!((close_720p.0 - (624.0 * 1.6)).abs() < 1e-4);
    assert!((close_720p.1 - (72.0 * 1.2)).abs() < 1e-4);

    let left_sel_720p = SpellbookGeometry::get_deck_selector_left_720p();
    let right_sel_720p = SpellbookGeometry::get_deck_selector_right_720p();
    assert!(left_sel_720p.0 < right_sel_720p.0);
}

#[test]
fn test_spellbook_multi_resolution_scaling_1080p() {
    // 1920x1080: scale_x = 2.4, scale_y = 1.8
    let decks_1080p =
        SpellbookGeometry::get_tab_coordinates_scaled(SpellbookTab::Decks, 1920.0, 1080.0);
    assert!((decks_1080p.0 - (240.0 * 2.4)).abs() < 1e-4);
    assert!((decks_1080p.1 - (72.0 * 1.8)).abs() < 1e-4);

    let equip_1080p =
        CombatGeometry::scale_point_to_viewport(SpellbookGeometry::EQUIP_BUTTON, 1920.0, 1080.0);
    assert!((equip_1080p.0 - (400.0 * 2.4)).abs() < 1e-4);
    assert!((equip_1080p.1 - (510.0 * 1.8)).abs() < 1e-4);
}
