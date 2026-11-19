//! `quest_domain`: Concrete Goal-Oriented Action Planning domain for Wizard City starter questlines.
//!
//! Models canonical questlines ("The Lore of Magic", "Enrollment", "A Good Impression"),
//! topological zone transitions, NPC interaction geometries, and potion replenishment checkpoints.
//! Designed for zero-heap-allocation in no_std / alloc environments. Adheres to `Facts ≠ Authority ≠ Actions`.

use alloc::string::{String, ToString};
use alloc::vec;
use alloc::vec::Vec;
use serde::{Deserialize, Serialize};

use crate::goap::{GoapAction, WorldState};

// ---------------------------------------------------------------------------
// World State Key Constants
// ---------------------------------------------------------------------------

pub const KEY_ZONE: &str = "zone";
pub const KEY_HAS_QUEST_LORE_OF_MAGIC: &str = "has_quest_lore_of_magic";
pub const KEY_TALKED_TO_BARTLEBY: &str = "talked_to_bartleby";
pub const KEY_COMPLETED_LORE_OF_MAGIC: &str = "completed_lore_of_magic";

pub const KEY_HAS_QUEST_ENROLLMENT: &str = "has_quest_enrollment";
pub const KEY_ENROLLED_FIRE: &str = "enrolled_fire";
pub const KEY_ENROLLED_ICE: &str = "enrolled_ice";
pub const KEY_ENROLLED_STORM: &str = "enrolled_storm";
pub const KEY_ENROLLED_MYTH: &str = "enrolled_myth";
pub const KEY_ENROLLED_LIFE: &str = "enrolled_life";
pub const KEY_ENROLLED_DEATH: &str = "enrolled_death";
pub const KEY_COMPLETED_ENROLLMENT: &str = "completed_enrollment";

pub const KEY_HAS_QUEST_GOOD_IMPRESSION: &str = "has_quest_good_impression";
pub const KEY_TALKED_TO_LINCOLN: &str = "talked_to_lincoln";
pub const KEY_COMPLETED_GOOD_IMPRESSION: &str = "completed_good_impression";

pub const KEY_HAS_QUEST_UNICORN_WAY: &str = "has_quest_unicorn_way";
pub const KEY_TALKED_TO_ORYAN: &str = "talked_to_oryan";
pub const KEY_HAS_QUEST_DEFEAT_LOST_SOULS: &str = "has_quest_defeat_lost_souls";
pub const KEY_LOST_SOULS_DEFEATED: &str = "lost_souls_defeated";
pub const KEY_HAS_QUEST_GRIM_TALES: &str = "has_quest_grim_tales";
pub const KEY_TALKED_TO_STILLSON: &str = "talked_to_stillson";
pub const KEY_HAS_QUEST_DEFEAT_SKELETAL_PIRATES: &str = "has_quest_defeat_skeletal_pirates";
pub const KEY_SKELETAL_PIRATES_DEFEATED: &str = "skeletal_pirates_defeated";
pub const KEY_HAS_QUEST_LADY_ORIEL: &str = "has_quest_lady_oriel";
pub const KEY_TALKED_TO_LADY_ORIEL: &str = "talked_to_lady_oriel";
pub const KEY_COMPLETED_UNICORN_WAY: &str = "completed_unicorn_way";

pub const KEY_VITALS_LOW: &str = "vitals_low";
pub const KEY_HAS_POTIONS: &str = "has_potions";

// ---------------------------------------------------------------------------
// Zone Constants
// ---------------------------------------------------------------------------

pub const ZONE_HEADMASTER_OFFICE: &str = "HeadmasterOffice";
pub const ZONE_COMMONS: &str = "WizardCity_Commons";
pub const ZONE_RAVENWOOD: &str = "WizardCity_Ravenwood";
pub const ZONE_GOLEM_COURT: &str = "WizardCity_GolemCourt";
pub const ZONE_SHOPPING_DISTRICT: &str = "WizardCity_ShoppingDistrict";
pub const ZONE_UNICORN_WAY: &str = "WizardCity_UnicornWay";
pub const ZONE_OLDE_TOWN: &str = "WizardCity_OldeTown";
pub const ZONE_TRITON_AVENUE: &str = "WizardCity_TritonAvenue";
pub const ZONE_FIRECAT_ALLEY: &str = "WizardCity_FirecatAlley";
pub const ZONE_CYCLOPS_LANE: &str = "WizardCity_CyclopsLane";

// ---------------------------------------------------------------------------
// Spatial Anchor & Landmark Specification
// ---------------------------------------------------------------------------

/// Spatial coordinate anchor within a designated zone manifold.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct SpatialAnchor {
    pub name: &'static str,
    pub zone: &'static str,
    pub x: f64,
    pub y: f64,
    pub z: f64,
    pub is_npc: bool,
}

pub const ANCHORS_OLDE_TOWN_AND_TRITON: &[SpatialAnchor] = &[
    SpatialAnchor {
        name: "Sergeant Muldoon",
        zone: ZONE_OLDE_TOWN,
        x: 120.0,
        y: -450.0,
        z: 15.0,
        is_npc: true,
    },
    SpatialAnchor {
        name: "Susie Gryphonbane",
        zone: ZONE_TRITON_AVENUE,
        x: 350.0,
        y: -200.0,
        z: 10.0,
        is_npc: true,
    },
    SpatialAnchor {
        name: "Duncan Grimwater",
        zone: ZONE_TRITON_AVENUE,
        x: 820.0,
        y: -110.0,
        z: 20.0,
        is_npc: true,
    },
    SpatialAnchor {
        name: "Artur Wethersfield",
        zone: ZONE_TRITON_AVENUE,
        x: 1400.0,
        y: 30.0,
        z: 15.0,
        is_npc: true,
    },
    SpatialAnchor {
        name: "Haunted Towers",
        zone: ZONE_TRITON_AVENUE,
        x: 950.0,
        y: 500.0,
        z: 35.0,
        is_npc: false,
    },
    SpatialAnchor {
        name: "Kraken Gate",
        zone: ZONE_TRITON_AVENUE,
        x: 2100.0,
        y: -80.0,
        z: 5.0,
        is_npc: false,
    },
    SpatialAnchor {
        name: "Galvanost Tower",
        zone: ZONE_TRITON_AVENUE,
        x: 1750.0,
        y: -320.0,
        z: 25.0,
        is_npc: false,
    },
    SpatialAnchor {
        name: "Tempest Nexus",
        zone: ZONE_TRITON_AVENUE,
        x: 2400.0,
        y: 120.0,
        z: 40.0,
        is_npc: false,
    },
];

pub fn find_spatial_anchor(name: &str) -> Option<&'static SpatialAnchor> {
    ANCHORS_OLDE_TOWN_AND_TRITON
        .iter()
        .find(|a| a.name.eq_ignore_ascii_case(name))
}

// ---------------------------------------------------------------------------
// NPC Target & Screen Bounding Box Specification
// ---------------------------------------------------------------------------

/// Calibrated screen bounding box `[min_x, min_y, max_x, max_y]` for 1280x720 base viewport.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct ScreenRect {
    pub min_x: f64,
    pub min_y: f64,
    pub max_x: f64,
    pub max_y: f64,
}

impl ScreenRect {
    pub const fn new(min_x: f64, min_y: f64, max_x: f64, max_y: f64) -> Self {
        Self {
            min_x,
            min_y,
            max_x,
            max_y,
        }
    }

    /// Center point coordinates (cx, cy).
    pub fn center(&self) -> (f64, f64) {
        (
            (self.min_x + self.max_x) * 0.5,
            (self.min_y + self.max_y) * 0.5,
        )
    }

    /// Width in pixels.
    pub fn width(&self) -> f64 {
        self.max_x - self.min_x
    }

    /// Height in pixels.
    pub fn height(&self) -> f64 {
        self.max_y - self.min_y
    }
}

/// NPC interaction target anchor with zone location and interaction bounding geometry.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct NpcInteractionTarget {
    pub name: String,
    pub zone: String,
    pub prompt_bounds: ScreenRect,
    pub advance_bounds: ScreenRect,
    pub dialogue_steps: usize,
}

impl NpcInteractionTarget {
    pub fn new(
        name: impl Into<String>,
        zone: impl Into<String>,
        prompt_bounds: ScreenRect,
        advance_bounds: ScreenRect,
        dialogue_steps: usize,
    ) -> Self {
        Self {
            name: name.into(),
            zone: zone.into(),
            prompt_bounds,
            advance_bounds,
            dialogue_steps,
        }
    }
}

// ---------------------------------------------------------------------------
// Standard NPC Anchor Lookup
// ---------------------------------------------------------------------------

/// Retrieves calibrated NPC interaction geometry for canonical Wizard City NPCs.
pub fn get_npc_interaction_target(name: &str) -> Option<NpcInteractionTarget> {
    // Default interaction prompt is centered near character feet/overhead in 720p
    let default_prompt = ScreenRect::new(580.0, 520.0, 700.0, 560.0);
    // Default dialog advance button is bottom-center in 720p
    let default_advance = ScreenRect::new(590.0, 640.0, 690.0, 680.0);

    match name {
        "Merle Ambrose" => Some(NpcInteractionTarget {
            name: "Merle Ambrose".to_string(),
            zone: ZONE_HEADMASTER_OFFICE.to_string(),
            prompt_bounds: default_prompt,
            advance_bounds: default_advance,
            dialogue_steps: 3,
        }),
        "Bartleby" => Some(NpcInteractionTarget {
            name: "Bartleby".to_string(),
            zone: ZONE_RAVENWOOD.to_string(),
            prompt_bounds: ScreenRect::new(560.0, 480.0, 720.0, 540.0),
            advance_bounds: default_advance,
            dialogue_steps: 2,
        }),
        "Professor Dalia Falmea" => Some(NpcInteractionTarget {
            name: "Professor Dalia Falmea".to_string(),
            zone: ZONE_RAVENWOOD.to_string(),
            prompt_bounds: default_prompt,
            advance_bounds: default_advance,
            dialogue_steps: 2,
        }),
        "Professor Lydia Greyrose" => Some(NpcInteractionTarget {
            name: "Professor Lydia Greyrose".to_string(),
            zone: ZONE_RAVENWOOD.to_string(),
            prompt_bounds: default_prompt,
            advance_bounds: default_advance,
            dialogue_steps: 2,
        }),
        "Professor Halston Balestrom" => Some(NpcInteractionTarget {
            name: "Professor Halston Balestrom".to_string(),
            zone: ZONE_RAVENWOOD.to_string(),
            prompt_bounds: default_prompt,
            advance_bounds: default_advance,
            dialogue_steps: 2,
        }),
        "Professor Cyrus Drake" => Some(NpcInteractionTarget {
            name: "Professor Cyrus Drake".to_string(),
            zone: ZONE_RAVENWOOD.to_string(),
            prompt_bounds: default_prompt,
            advance_bounds: default_advance,
            dialogue_steps: 2,
        }),
        "Professor Moolinda Wu" => Some(NpcInteractionTarget {
            name: "Professor Moolinda Wu".to_string(),
            zone: ZONE_RAVENWOOD.to_string(),
            prompt_bounds: default_prompt,
            advance_bounds: default_advance,
            dialogue_steps: 2,
        }),
        "Malorn Ashthorn" => Some(NpcInteractionTarget {
            name: "Malorn Ashthorn".to_string(),
            zone: ZONE_RAVENWOOD.to_string(),
            prompt_bounds: default_prompt,
            advance_bounds: default_advance,
            dialogue_steps: 2,
        }),
        "Mr. Lincoln" => Some(NpcInteractionTarget {
            name: "Mr. Lincoln".to_string(),
            zone: ZONE_GOLEM_COURT.to_string(),
            prompt_bounds: default_prompt,
            advance_bounds: default_advance,
            dialogue_steps: 2,
        }),
        "Commons Potion Vendor" => Some(NpcInteractionTarget {
            name: "Commons Potion Vendor".to_string(),
            zone: ZONE_COMMONS.to_string(),
            prompt_bounds: ScreenRect::new(600.0, 500.0, 680.0, 550.0),
            advance_bounds: default_advance,
            dialogue_steps: 1,
        }),
        "Private O'Ryan" => Some(NpcInteractionTarget {
            name: "Private O'Ryan".to_string(),
            zone: ZONE_UNICORN_WAY.to_string(),
            prompt_bounds: ScreenRect::new(570.0, 510.0, 690.0, 550.0),
            advance_bounds: default_advance,
            dialogue_steps: 2,
        }),
        "Private Stillson" => Some(NpcInteractionTarget {
            name: "Private Stillson".to_string(),
            zone: ZONE_UNICORN_WAY.to_string(),
            prompt_bounds: ScreenRect::new(580.0, 520.0, 700.0, 560.0),
            advance_bounds: default_advance,
            dialogue_steps: 2,
        }),
        "Lady Oriel" => Some(NpcInteractionTarget {
            name: "Lady Oriel".to_string(),
            zone: ZONE_UNICORN_WAY.to_string(),
            prompt_bounds: ScreenRect::new(560.0, 490.0, 710.0, 540.0),
            advance_bounds: default_advance,
            dialogue_steps: 3,
        }),
        _ => None,
    }
}

// ---------------------------------------------------------------------------
// Canonical Repertoire Factory
// ---------------------------------------------------------------------------

/// Builds the complete GOAP action repertoire for Wizard City starter progression:
/// - Directional zone transitions between HeadmasterOffice, Commons, Ravenwood, GolemCourt, ShoppingDistrict.
/// - "The Lore of Magic" quest progression (Ambrose -> Bartleby -> Ambrose turn-in).
/// - "Enrollment" quest progression (visiting 6 school teachers in Ravenwood -> Ambrose turn-in).
/// - "A Good Impression" quest progression (Lincoln in GolemCourt -> Ambrose turn-in).
/// - Potion fountain checkpoint and health replenishment.
pub fn build_wizard_city_starter_repertoire() -> Vec<GoapAction> {
    vec![
        // -------------------------------------------------------------------
        // Zone Transitions
        // -------------------------------------------------------------------
        GoapAction::new("travel_office_to_commons", 5.0)
            .require(KEY_ZONE, ZONE_HEADMASTER_OFFICE)
            .effect(KEY_ZONE, ZONE_COMMONS),
        GoapAction::new("travel_commons_to_office", 5.0)
            .require(KEY_ZONE, ZONE_COMMONS)
            .effect(KEY_ZONE, ZONE_HEADMASTER_OFFICE),
        GoapAction::new("travel_commons_to_ravenwood", 8.0)
            .require(KEY_ZONE, ZONE_COMMONS)
            .effect(KEY_ZONE, ZONE_RAVENWOOD),
        GoapAction::new("travel_ravenwood_to_commons", 8.0)
            .require(KEY_ZONE, ZONE_RAVENWOOD)
            .effect(KEY_ZONE, ZONE_COMMONS),
        GoapAction::new("travel_commons_to_golem_court", 4.0)
            .require(KEY_ZONE, ZONE_COMMONS)
            .effect(KEY_ZONE, ZONE_GOLEM_COURT),
        GoapAction::new("travel_golem_court_to_commons", 4.0)
            .require(KEY_ZONE, ZONE_GOLEM_COURT)
            .effect(KEY_ZONE, ZONE_COMMONS),
        GoapAction::new("travel_commons_to_shopping_district", 6.0)
            .require(KEY_ZONE, ZONE_COMMONS)
            .effect(KEY_ZONE, ZONE_SHOPPING_DISTRICT),
        GoapAction::new("travel_shopping_district_to_commons", 6.0)
            .require(KEY_ZONE, ZONE_SHOPPING_DISTRICT)
            .effect(KEY_ZONE, ZONE_COMMONS),
        GoapAction::new("travel_commons_to_unicorn_way", 6.0)
            .require(KEY_ZONE, ZONE_COMMONS)
            .effect(KEY_ZONE, ZONE_UNICORN_WAY),
        GoapAction::new("travel_unicorn_way_to_commons", 6.0)
            .require(KEY_ZONE, ZONE_UNICORN_WAY)
            .effect(KEY_ZONE, ZONE_COMMONS),
        GoapAction::new("travel_commons_to_olde_town", 5.0)
            .require(KEY_ZONE, ZONE_COMMONS)
            .effect(KEY_ZONE, ZONE_OLDE_TOWN),
        GoapAction::new("travel_olde_town_to_commons", 5.0)
            .require(KEY_ZONE, ZONE_OLDE_TOWN)
            .effect(KEY_ZONE, ZONE_COMMONS),
        GoapAction::new("travel_olde_town_to_triton", 5.0)
            .require(KEY_ZONE, ZONE_OLDE_TOWN)
            .effect(KEY_ZONE, ZONE_TRITON_AVENUE),
        GoapAction::new("travel_triton_to_olde_town", 5.0)
            .require(KEY_ZONE, ZONE_TRITON_AVENUE)
            .effect(KEY_ZONE, ZONE_OLDE_TOWN),
        GoapAction::new("travel_olde_town_to_firecat", 5.0)
            .require(KEY_ZONE, ZONE_OLDE_TOWN)
            .effect(KEY_ZONE, ZONE_FIRECAT_ALLEY),
        GoapAction::new("travel_firecat_to_olde_town", 5.0)
            .require(KEY_ZONE, ZONE_FIRECAT_ALLEY)
            .effect(KEY_ZONE, ZONE_OLDE_TOWN),
        GoapAction::new("travel_olde_town_to_cyclops", 5.0)
            .require(KEY_ZONE, ZONE_OLDE_TOWN)
            .effect(KEY_ZONE, ZONE_CYCLOPS_LANE),
        GoapAction::new("travel_cyclops_to_olde_town", 5.0)
            .require(KEY_ZONE, ZONE_CYCLOPS_LANE)
            .effect(KEY_ZONE, ZONE_OLDE_TOWN),
        // -------------------------------------------------------------------
        // Quest 1: The Lore of Magic
        // -------------------------------------------------------------------
        GoapAction::new("ambrose_accept_lore_of_magic", 3.0)
            .require(KEY_ZONE, ZONE_HEADMASTER_OFFICE)
            .require(KEY_HAS_QUEST_LORE_OF_MAGIC, false)
            .require(KEY_COMPLETED_LORE_OF_MAGIC, false)
            .effect(KEY_HAS_QUEST_LORE_OF_MAGIC, true),
        GoapAction::new("talk_to_bartleby", 4.0)
            .require(KEY_ZONE, ZONE_RAVENWOOD)
            .require(KEY_HAS_QUEST_LORE_OF_MAGIC, true)
            .require(KEY_TALKED_TO_BARTLEBY, false)
            .effect(KEY_TALKED_TO_BARTLEBY, true),
        GoapAction::new("ambrose_turnin_lore_of_magic", 3.0)
            .require(KEY_ZONE, ZONE_HEADMASTER_OFFICE)
            .require(KEY_HAS_QUEST_LORE_OF_MAGIC, true)
            .require(KEY_TALKED_TO_BARTLEBY, true)
            .effect(KEY_HAS_QUEST_LORE_OF_MAGIC, false)
            .effect(KEY_COMPLETED_LORE_OF_MAGIC, true)
            .effect(KEY_HAS_QUEST_ENROLLMENT, true),
        // -------------------------------------------------------------------
        // Quest 2: Enrollment (Ravenwood School Visits)
        // -------------------------------------------------------------------
        GoapAction::new("visit_school_fire", 3.0)
            .require(KEY_ZONE, ZONE_RAVENWOOD)
            .require(KEY_HAS_QUEST_ENROLLMENT, true)
            .require(KEY_ENROLLED_FIRE, false)
            .effect(KEY_ENROLLED_FIRE, true),
        GoapAction::new("visit_school_ice", 3.0)
            .require(KEY_ZONE, ZONE_RAVENWOOD)
            .require(KEY_HAS_QUEST_ENROLLMENT, true)
            .require(KEY_ENROLLED_ICE, false)
            .effect(KEY_ENROLLED_ICE, true),
        GoapAction::new("visit_school_storm", 3.0)
            .require(KEY_ZONE, ZONE_RAVENWOOD)
            .require(KEY_HAS_QUEST_ENROLLMENT, true)
            .require(KEY_ENROLLED_STORM, false)
            .effect(KEY_ENROLLED_STORM, true),
        GoapAction::new("visit_school_myth", 3.0)
            .require(KEY_ZONE, ZONE_RAVENWOOD)
            .require(KEY_HAS_QUEST_ENROLLMENT, true)
            .require(KEY_ENROLLED_MYTH, false)
            .effect(KEY_ENROLLED_MYTH, true),
        GoapAction::new("visit_school_life", 3.0)
            .require(KEY_ZONE, ZONE_RAVENWOOD)
            .require(KEY_HAS_QUEST_ENROLLMENT, true)
            .require(KEY_ENROLLED_LIFE, false)
            .effect(KEY_ENROLLED_LIFE, true),
        GoapAction::new("visit_school_death", 3.0)
            .require(KEY_ZONE, ZONE_RAVENWOOD)
            .require(KEY_HAS_QUEST_ENROLLMENT, true)
            .require(KEY_ENROLLED_DEATH, false)
            .effect(KEY_ENROLLED_DEATH, true),
        GoapAction::new("ambrose_turnin_enrollment", 4.0)
            .require(KEY_ZONE, ZONE_HEADMASTER_OFFICE)
            .require(KEY_HAS_QUEST_ENROLLMENT, true)
            .require(KEY_ENROLLED_FIRE, true)
            .require(KEY_ENROLLED_ICE, true)
            .require(KEY_ENROLLED_STORM, true)
            .require(KEY_ENROLLED_MYTH, true)
            .require(KEY_ENROLLED_LIFE, true)
            .require(KEY_ENROLLED_DEATH, true)
            .effect(KEY_HAS_QUEST_ENROLLMENT, false)
            .effect(KEY_COMPLETED_ENROLLMENT, true)
            .effect(KEY_HAS_QUEST_GOOD_IMPRESSION, true),
        // -------------------------------------------------------------------
        // Quest 3: A Good Impression (Golem Court)
        // -------------------------------------------------------------------
        GoapAction::new("talk_to_mr_lincoln", 3.0)
            .require(KEY_ZONE, ZONE_GOLEM_COURT)
            .require(KEY_HAS_QUEST_GOOD_IMPRESSION, true)
            .require(KEY_TALKED_TO_LINCOLN, false)
            .effect(KEY_TALKED_TO_LINCOLN, true),
        GoapAction::new("ambrose_turnin_good_impression", 4.0)
            .require(KEY_ZONE, ZONE_HEADMASTER_OFFICE)
            .require(KEY_HAS_QUEST_GOOD_IMPRESSION, true)
            .require(KEY_TALKED_TO_LINCOLN, true)
            .effect(KEY_HAS_QUEST_GOOD_IMPRESSION, false)
            .effect(KEY_COMPLETED_GOOD_IMPRESSION, true),
        // -------------------------------------------------------------------
        // Quest 4: Unicorn Way Progression & Combat Encounters
        // -------------------------------------------------------------------
        GoapAction::new("ambrose_accept_unicorn_way", 3.0)
            .require(KEY_ZONE, ZONE_HEADMASTER_OFFICE)
            .require(KEY_COMPLETED_GOOD_IMPRESSION, true)
            .require(KEY_HAS_QUEST_UNICORN_WAY, false)
            .require(KEY_COMPLETED_UNICORN_WAY, false)
            .effect(KEY_HAS_QUEST_UNICORN_WAY, true),
        GoapAction::new("talk_to_private_oryan", 4.0)
            .require(KEY_ZONE, ZONE_UNICORN_WAY)
            .require(KEY_HAS_QUEST_UNICORN_WAY, true)
            .require(KEY_TALKED_TO_ORYAN, false)
            .effect(KEY_TALKED_TO_ORYAN, true)
            .effect(KEY_HAS_QUEST_DEFEAT_LOST_SOULS, true),
        GoapAction::new("engage_and_defeat_lost_soul", 12.0)
            .require(KEY_ZONE, ZONE_UNICORN_WAY)
            .require(KEY_HAS_QUEST_DEFEAT_LOST_SOULS, true)
            .require(KEY_VITALS_LOW, false)
            .require(KEY_LOST_SOULS_DEFEATED, false)
            .effect(KEY_LOST_SOULS_DEFEATED, true),
        GoapAction::new("oryan_turnin_lost_souls", 3.0)
            .require(KEY_ZONE, ZONE_UNICORN_WAY)
            .require(KEY_HAS_QUEST_DEFEAT_LOST_SOULS, true)
            .require(KEY_LOST_SOULS_DEFEATED, true)
            .effect(KEY_HAS_QUEST_DEFEAT_LOST_SOULS, false)
            .effect(KEY_HAS_QUEST_GRIM_TALES, true)
            .effect(KEY_TALKED_TO_STILLSON, false),
        GoapAction::new("talk_to_private_stillson", 4.0)
            .require(KEY_ZONE, ZONE_UNICORN_WAY)
            .require(KEY_HAS_QUEST_GRIM_TALES, true)
            .require(KEY_TALKED_TO_STILLSON, false)
            .effect(KEY_TALKED_TO_STILLSON, true)
            .effect(KEY_HAS_QUEST_DEFEAT_SKELETAL_PIRATES, true),
        GoapAction::new("engage_and_defeat_skeletal_pirate", 15.0)
            .require(KEY_ZONE, ZONE_UNICORN_WAY)
            .require(KEY_HAS_QUEST_DEFEAT_SKELETAL_PIRATES, true)
            .require(KEY_VITALS_LOW, false)
            .require(KEY_SKELETAL_PIRATES_DEFEATED, false)
            .effect(KEY_SKELETAL_PIRATES_DEFEATED, true),
        GoapAction::new("stillson_turnin_skeletal_pirates", 3.0)
            .require(KEY_ZONE, ZONE_UNICORN_WAY)
            .require(KEY_HAS_QUEST_DEFEAT_SKELETAL_PIRATES, true)
            .require(KEY_SKELETAL_PIRATES_DEFEATED, true)
            .effect(KEY_HAS_QUEST_DEFEAT_SKELETAL_PIRATES, false)
            .effect(KEY_HAS_QUEST_LADY_ORIEL, true)
            .effect(KEY_TALKED_TO_LADY_ORIEL, false),
        GoapAction::new("talk_to_lady_oriel", 5.0)
            .require(KEY_ZONE, ZONE_UNICORN_WAY)
            .require(KEY_HAS_QUEST_LADY_ORIEL, true)
            .require(KEY_TALKED_TO_LADY_ORIEL, false)
            .effect(KEY_TALKED_TO_LADY_ORIEL, true),
        GoapAction::new("ambrose_turnin_unicorn_way", 4.0)
            .require(KEY_ZONE, ZONE_HEADMASTER_OFFICE)
            .require(KEY_HAS_QUEST_UNICORN_WAY, true)
            .require(KEY_TALKED_TO_LADY_ORIEL, true)
            .effect(KEY_HAS_QUEST_UNICORN_WAY, false)
            .effect(KEY_COMPLETED_UNICORN_WAY, true),
        // -------------------------------------------------------------------
        // Vital Checkpoints & Potion Recovery
        // -------------------------------------------------------------------
        GoapAction::new("refill_potions_at_fountain", 6.0)
            .require(KEY_ZONE, ZONE_COMMONS)
            .effect(KEY_HAS_POTIONS, true)
            .effect(KEY_VITALS_LOW, false),
        GoapAction::new("drink_potion_recovery", 1.0)
            .require(KEY_HAS_POTIONS, true)
            .require(KEY_VITALS_LOW, true)
            .effect(KEY_VITALS_LOW, false),
    ]
}

// ---------------------------------------------------------------------------
// State and Goal Factory Helpers
// ---------------------------------------------------------------------------

/// Constructs the baseline initial world state of a fresh wizard in Ambrose's office.
pub fn initial_fresh_wizard_state() -> WorldState {
    let mut state = WorldState::new();
    state.set(KEY_ZONE, ZONE_HEADMASTER_OFFICE);
    state.set(KEY_HAS_QUEST_LORE_OF_MAGIC, false);
    state.set(KEY_TALKED_TO_BARTLEBY, false);
    state.set(KEY_COMPLETED_LORE_OF_MAGIC, false);
    state.set(KEY_HAS_QUEST_ENROLLMENT, false);
    state.set(KEY_ENROLLED_FIRE, false);
    state.set(KEY_ENROLLED_ICE, false);
    state.set(KEY_ENROLLED_STORM, false);
    state.set(KEY_ENROLLED_MYTH, false);
    state.set(KEY_ENROLLED_LIFE, false);
    state.set(KEY_ENROLLED_DEATH, false);
    state.set(KEY_COMPLETED_ENROLLMENT, false);
    state.set(KEY_HAS_QUEST_GOOD_IMPRESSION, false);
    state.set(KEY_TALKED_TO_LINCOLN, false);
    state.set(KEY_COMPLETED_GOOD_IMPRESSION, false);
    state.set(KEY_HAS_QUEST_UNICORN_WAY, false);
    state.set(KEY_TALKED_TO_ORYAN, false);
    state.set(KEY_HAS_QUEST_DEFEAT_LOST_SOULS, false);
    state.set(KEY_LOST_SOULS_DEFEATED, false);
    state.set(KEY_HAS_QUEST_GRIM_TALES, false);
    state.set(KEY_TALKED_TO_STILLSON, false);
    state.set(KEY_HAS_QUEST_DEFEAT_SKELETAL_PIRATES, false);
    state.set(KEY_SKELETAL_PIRATES_DEFEATED, false);
    state.set(KEY_HAS_QUEST_LADY_ORIEL, false);
    state.set(KEY_TALKED_TO_LADY_ORIEL, false);
    state.set(KEY_COMPLETED_UNICORN_WAY, false);
    state.set(KEY_VITALS_LOW, false);
    state.set(KEY_HAS_POTIONS, true);
    state
}

/// Goal condition to complete "The Lore of Magic".
pub fn goal_complete_lore_of_magic() -> WorldState {
    let mut goal = WorldState::new();
    goal.set(KEY_COMPLETED_LORE_OF_MAGIC, true);
    goal
}

/// Goal condition to complete "Enrollment".
pub fn goal_complete_enrollment() -> WorldState {
    let mut goal = WorldState::new();
    goal.set(KEY_COMPLETED_ENROLLMENT, true);
    goal
}

/// Goal condition to complete "A Good Impression".
pub fn goal_complete_good_impression() -> WorldState {
    let mut goal = WorldState::new();
    goal.set(KEY_COMPLETED_GOOD_IMPRESSION, true);
    goal
}

/// Goal condition to complete "Unicorn Way" progression and report back to Ambrose.
pub fn goal_complete_unicorn_way() -> WorldState {
    let mut goal = WorldState::new();
    goal.set(KEY_COMPLETED_UNICORN_WAY, true);
    goal
}

/// Goal condition ensuring vitals are replenished and potions stocked.
pub fn goal_replenish_vitals() -> WorldState {
    let mut goal = WorldState::new();
    goal.set(KEY_VITALS_LOW, false);
    goal.set(KEY_HAS_POTIONS, true);
    goal
}
