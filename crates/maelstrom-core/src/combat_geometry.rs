//! Combat card and screen geometry definitions ported from WizSDK, Deimos, and ProjectMaelstrom's CombatGeometry.cs.

use alloc::format;
use alloc::string::{String, ToString};
use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Point {
    pub x: i32,
    pub y: i32,
}

impl Point {
    pub const fn new(x: i32, y: i32) -> Self {
        Self { x, y }
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Rect {
    pub x: i32,
    pub y: i32,
    pub width: i32,
    pub height: i32,
}

impl Rect {
    pub const fn new(x: i32, y: i32, width: i32, height: i32) -> Self {
        Self {
            x,
            y,
            width,
            height,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CombatCardGeometry {
    pub name: String,
    pub spell_x: i32,
    pub spell_y: i32,
    pub card_width: i32,
}

impl CombatCardGeometry {
    pub fn new(name: impl Into<String>, spell_x: i32, spell_y: i32, card_width: i32) -> Self {
        Self {
            name: name.into(),
            spell_x,
            spell_y,
            card_width,
        }
    }

    pub fn with_default_y(name: impl Into<String>, spell_x: i32) -> Self {
        Self::new(name, spell_x, 325, 52)
    }
}

pub struct CombatGeometry;

impl CombatGeometry {
    pub const SPELL_AREA: Rect = Rect::new(245, 290, 370, 70);
    pub const PASS_BUTTON: Point = Point::new(254, 398);
    pub const FLEE_BUTTON: Point = Point::new(546, 398);
    pub const HEALTH_GLOBE: Point = Point::new(23, 563);
    pub const MANA_GLOBE: Point = Point::new(79, 591);

    /// Calculates click positions for enchanting a card.
    pub fn calculate_enchant_geometry(
        enchant_card: &CombatCardGeometry,
        spell_card: &CombatCardGeometry,
    ) -> (Point, Point, CombatCardGeometry) {
        let click1 = Point::new(enchant_card.spell_x, enchant_card.spell_y);
        let click2 = Point::new(spell_card.spell_x, spell_card.spell_y);

        let enchant_is_before = enchant_card.spell_x < spell_card.spell_x;
        let mut new_x = spell_card.spell_x + (spell_card.card_width / 2);
        if enchant_is_before {
            new_x -= spell_card.card_width;
        }

        let enchanted = CombatCardGeometry::new(
            format!("{}-{}", enchant_card.name, spell_card.name),
            new_x,
            spell_card.spell_y,
            spell_card.card_width,
        );

        (click1, click2, enchanted)
    }

    /// Calculates target coordinates for spell casting (0..3 enemy top row, 4..7 ally bottom row).
    pub fn calculate_target_coordinates(target_index: usize) -> Result<Point, String> {
        if target_index > 7 {
            return Err("Target index must be between 0 and 7.".to_string());
        }

        if target_index < 4 {
            let x = (174 * target_index as i32) + 130;
            Ok(Point::new(x, 50))
        } else {
            let x = (174 * (7 - target_index as i32)) + 160;
            Ok(Point::new(x, 590))
        }
    }

    /// Returns the pedestal position check coordinate for enemy index (0..3).
    pub fn get_enemy_pedestal_coordinates(enemy_index: usize) -> Result<Point, String> {
        if enemy_index > 3 {
            return Err("Enemy index must be between 0 and 3.".to_string());
        }
        Ok(Point::new((174 * enemy_index as i32) + 203, 75))
    }

    /// Calculates the click coordinate for a card in hand by slot index (0..total_cards-1).
    pub fn get_hand_card_coordinates(
        hand_index: usize,
        total_cards_in_hand: usize,
    ) -> Result<Point, String> {
        if !(1..=7).contains(&total_cards_in_hand) {
            return Err("Total cards in hand must be between 1 and 7.".to_string());
        }
        if hand_index >= total_cards_in_hand {
            return Err("Hand index must be less than total cards in hand.".to_string());
        }

        let card_width = 52;
        let area_x = 245;
        let area_width = 370;
        let card_y = 325;

        let total_width = (total_cards_in_hand as i32) * card_width;
        let left_margin = area_x + ((area_width - total_width) / 2);
        let card_x = left_margin + ((hand_index as i32) * card_width) + (card_width / 2);

        Ok(Point::new(card_x, card_y))
    }

    pub const BASE_WIDTH: f64 = 800.0;
    pub const BASE_HEIGHT: f64 = 600.0;
    pub const VIEWPORT_1280X720_WIDTH: f64 = 1280.0;
    pub const VIEWPORT_1280X720_HEIGHT: f64 = 720.0;

    /// Scales a point from base 800x600 coordinates to target viewport dimensions (default 1280x720).
    pub fn scale_point_to_viewport(p: Point, target_width: f64, target_height: f64) -> (f64, f64) {
        let scale_x = target_width / Self::BASE_WIDTH;
        let scale_y = target_height / Self::BASE_HEIGHT;
        (p.x as f64 * scale_x, p.y as f64 * scale_y)
    }

    /// Scales a rect from base 800x600 coordinates to target viewport dimensions.
    pub fn scale_rect_to_viewport(
        r: Rect,
        target_width: f64,
        target_height: f64,
    ) -> (f64, f64, f64, f64) {
        let scale_x = target_width / Self::BASE_WIDTH;
        let scale_y = target_height / Self::BASE_HEIGHT;
        (
            r.x as f64 * scale_x,
            r.y as f64 * scale_y,
            r.width as f64 * scale_x,
            r.height as f64 * scale_y,
        )
    }

    /// Returns hand card click coordinates scaled directly to the 1280x720 base viewport.
    pub fn get_hand_card_coordinates_720p(
        hand_index: usize,
        total_cards_in_hand: usize,
    ) -> Result<(f64, f64), String> {
        let p = Self::get_hand_card_coordinates(hand_index, total_cards_in_hand)?;
        Ok(Self::scale_point_to_viewport(
            p,
            Self::VIEWPORT_1280X720_WIDTH,
            Self::VIEWPORT_1280X720_HEIGHT,
        ))
    }

    /// Returns target coordinates scaled directly to the 1280x720 base viewport.
    pub fn calculate_target_coordinates_720p(target_index: usize) -> Result<(f64, f64), String> {
        let p = Self::calculate_target_coordinates(target_index)?;
        Ok(Self::scale_point_to_viewport(
            p,
            Self::VIEWPORT_1280X720_WIDTH,
            Self::VIEWPORT_1280X720_HEIGHT,
        ))
    }

    /// Returns pass button coordinates scaled directly to the 1280x720 base viewport.
    pub fn get_pass_button_coordinates_720p() -> (f64, f64) {
        Self::scale_point_to_viewport(
            Self::PASS_BUTTON,
            Self::VIEWPORT_1280X720_WIDTH,
            Self::VIEWPORT_1280X720_HEIGHT,
        )
    }

    /// Returns flee button coordinates scaled directly to the 1280x720 base viewport.
    pub fn get_flee_button_coordinates_720p() -> (f64, f64) {
        Self::scale_point_to_viewport(
            Self::FLEE_BUTTON,
            Self::VIEWPORT_1280X720_WIDTH,
            Self::VIEWPORT_1280X720_HEIGHT,
        )
    }
}

/// Spellbook navigation tabs.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum SpellbookTab {
    Character,
    Spells,
    Decks,
    Quests,
    Friends,
    Help,
}

/// Geometric screen layout for the Wizard101 Spellbook ('P') UI.
pub struct SpellbookGeometry;

impl SpellbookGeometry {
    pub const BASE_WIDTH: f64 = 800.0;
    pub const BASE_HEIGHT: f64 = 600.0;

    // Base 800x600 coordinates
    pub const BOOK_ICON: Point = Point::new(760, 560);
    pub const CLOSE_BUTTON: Point = Point::new(624, 72);
    pub const TAB_CHARACTER: Point = Point::new(176, 72);
    pub const TAB_SPELLS: Point = Point::new(208, 72);
    pub const TAB_DECKS: Point = Point::new(240, 72);
    pub const DECK_SELECTOR_LEFT: Point = Point::new(280, 108);
    pub const DECK_SELECTOR_RIGHT: Point = Point::new(520, 108);
    pub const EQUIP_BUTTON: Point = Point::new(400, 510);

    pub fn get_tab_coordinates(tab: SpellbookTab) -> Point {
        match tab {
            SpellbookTab::Character => Self::TAB_CHARACTER,
            SpellbookTab::Spells => Self::TAB_SPELLS,
            SpellbookTab::Decks => Self::TAB_DECKS,
            SpellbookTab::Quests => Point::new(272, 72),
            SpellbookTab::Friends => Point::new(304, 72),
            SpellbookTab::Help => Point::new(336, 72),
        }
    }

    pub fn get_tab_coordinates_scaled(
        tab: SpellbookTab,
        target_width: f64,
        target_height: f64,
    ) -> (f64, f64) {
        let p = Self::get_tab_coordinates(tab);
        CombatGeometry::scale_point_to_viewport(p, target_width, target_height)
    }

    pub fn get_deck_tab_coordinates_720p() -> (f64, f64) {
        Self::get_tab_coordinates_scaled(SpellbookTab::Decks, 1280.0, 720.0)
    }

    pub fn get_equip_button_coordinates_720p() -> (f64, f64) {
        CombatGeometry::scale_point_to_viewport(Self::EQUIP_BUTTON, 1280.0, 720.0)
    }

    pub fn get_close_button_coordinates_720p() -> (f64, f64) {
        CombatGeometry::scale_point_to_viewport(Self::CLOSE_BUTTON, 1280.0, 720.0)
    }

    pub fn get_deck_selector_left_720p() -> (f64, f64) {
        CombatGeometry::scale_point_to_viewport(Self::DECK_SELECTOR_LEFT, 1280.0, 720.0)
    }

    pub fn get_deck_selector_right_720p() -> (f64, f64) {
        CombatGeometry::scale_point_to_viewport(Self::DECK_SELECTOR_RIGHT, 1280.0, 720.0)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_target_coordinates() {
        let enemy_0 = CombatGeometry::calculate_target_coordinates(0).unwrap();
        assert_eq!(enemy_0, Point::new(130, 50));
        let ally_4 = CombatGeometry::calculate_target_coordinates(4).unwrap();
        assert_eq!(ally_4, Point::new(174 * 3 + 160, 590));
    }

    #[test]
    fn test_target_coordinates_720p() {
        let enemy_0 = CombatGeometry::calculate_target_coordinates_720p(0).unwrap();
        assert!((enemy_0.0 - 208.0).abs() < 1e-4);
        assert!((enemy_0.1 - 60.0).abs() < 1e-4);

        let pass_720p = CombatGeometry::get_pass_button_coordinates_720p();
        assert!((pass_720p.0 - 406.4).abs() < 1e-4);
        assert!((pass_720p.1 - 477.6).abs() < 1e-4);
    }

    #[test]
    fn test_hand_card_coordinates_720p() {
        let single_card_720p = CombatGeometry::get_hand_card_coordinates_720p(0, 1).unwrap();
        assert_eq!(single_card_720p, (430.0 * 1.6, 325.0 * 1.2));
        assert_eq!(single_card_720p, (688.0, 390.0));
    }

    #[test]
    fn test_hand_card_coordinates() {
        let single_card = CombatGeometry::get_hand_card_coordinates(0, 1).unwrap();
        // total_width = 52. left_margin = 245 + (370-52)/2 = 245 + 159 = 404. card_x = 404 + 26 = 430.
        assert_eq!(single_card, Point::new(430, 325));
    }

    #[test]
    fn test_enchant_geometry() {
        let enchant = CombatCardGeometry::with_default_y("Epic", 300);
        let spell = CombatCardGeometry::with_default_y("Tempest", 400);
        let (c1, c2, enchanted) = CombatGeometry::calculate_enchant_geometry(&enchant, &spell);

        assert_eq!(c1, Point::new(300, 325));
        assert_eq!(c2, Point::new(400, 325));
        assert_eq!(enchanted.name, "Epic-Tempest");
    }

    #[test]
    fn test_spellbook_geometry() {
        let deck_tab = SpellbookGeometry::get_tab_coordinates(SpellbookTab::Decks);
        assert_eq!(deck_tab, Point::new(240, 72));

        let deck_tab_720p = SpellbookGeometry::get_deck_tab_coordinates_720p();
        // 240 * 1.6 = 384.0, 72 * 1.2 = 86.4
        assert!((deck_tab_720p.0 - 384.0).abs() < 1e-4);
        assert!((deck_tab_720p.1 - 86.4).abs() < 1e-4);

        let equip_720p = SpellbookGeometry::get_equip_button_coordinates_720p();
        // 400 * 1.6 = 640.0, 510 * 1.2 = 612.0
        assert!((equip_720p.0 - 640.0).abs() < 1e-4);
        assert!((equip_720p.1 - 612.0).abs() < 1e-4);

        let close_720p = SpellbookGeometry::get_close_button_coordinates_720p();
        // 624 * 1.6 = 998.4, 72 * 1.2 = 86.4
        assert!((close_720p.0 - 998.4).abs() < 1e-4);
        assert!((close_720p.1 - 86.4).abs() < 1e-4);
    }
}
