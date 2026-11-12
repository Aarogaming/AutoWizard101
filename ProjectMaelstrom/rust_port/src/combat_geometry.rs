//! Combat card and screen geometry definitions ported from WizSDK, Deimos, and ProjectMaelstrom's CombatGeometry.cs.

#[derive(Debug, Clone, PartialEq, Eq)]
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
        Self { x, y, width, height }
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
    pub fn get_hand_card_coordinates(hand_index: usize, total_cards_in_hand: usize) -> Result<Point, String> {
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
}
