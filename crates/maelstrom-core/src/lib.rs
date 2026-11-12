//! `maelstrom-core`: Domain types, Arbiter confidence gate, deck model, and observation contracts.
//! Designed for no_std / alloc compatibility (zero OS or UI dependencies).

#![cfg_attr(not(feature = "std"), no_std)]

extern crate alloc;

use alloc::vec::Vec;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

pub mod combat_geometry;
#[cfg(feature = "deck_codec")]
pub mod deck_encoder;

/// School theme determining card frame styling, syntax accents, and IDE filtering.
#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq)]
pub enum AutomationSchool {
    Combat,      // Fire/Storm: Duel circle detection, card selection, pass/flee
    Navigation,  // Balance: Waypoint pathing, realm hopping, teleport marks
    Economy,     // Myth/Death: Bazaar sniping, quick-selling, gardening
    Maintenance, // Life/Ice: Potion refill, pet snack minigames, anti-AFK
    MetaLogic,   // Astral (Sun/Star/Moon): Loops, vision branches, nested Sub-Decks
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum ExecutionMode {
    OneShot,                             // Fires immediately on click/hotkey and exits
    SequentialStep,                      // Runs inside a Deck queue; yields CardResult
    ContinuousAura { interval_ms: u64 }, // Passive background watcher (e.g. popup dismissal)
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum FailPolicy {
    Retry { max_attempts: u8, backoff_ms: u64 },
    SkipCard,
    AbortDeck,
    JumpToSlot(usize),
    JumpToRelative(i32),
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct CardEnchantment {
    pub repeat_count: u32,
    pub pre_delay_ms: u64,
    pub post_delay_ms: u64,
    pub jitter_px: u32,
    pub confidence_threshold: Option<f32>,
    pub on_fail: FailPolicy,
}

impl Default for CardEnchantment {
    fn default() -> Self {
        Self {
            repeat_count: 1,
            pre_delay_ms: 0,
            post_delay_ms: 0,
            jitter_px: 0,
            confidence_threshold: None,
            on_fail: FailPolicy::AbortDeck,
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum InputEvent {
    MouseMove { x: i32, y: i32 },
    MouseDown { button: u8 },
    MouseUp { button: u8 },
    MouseClick { x: i32, y: i32, button: u8 },
    KeyDown { key_code: u32 },
    KeyUp { key_code: u32 },
    KeyPress { key_code: u32 },
    Sleep { duration_ms: u64 },
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum CardPayload {
    InputSequence(Vec<InputEvent>),
    VisionWait {
        template_id: String,
        confidence: f32,
        timeout_ms: u64,
        click_on_match: bool,
    },
    RegionCheck {
        region: (u32, u32, u32, u32),
        expected_pattern: String,
    },
    SubDeck {
        deck_id: Uuid,
    },
    LegacyExternal {
        command: String,
        args: Vec<String>,
        timeout_ms: u64,
    },
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct MacroCard {
    pub id: Uuid,
    pub name: String,
    pub school: AutomationSchool,
    pub pip_cost: u8,
    pub description: String,
    pub mode: ExecutionMode,
    pub payload: CardPayload,
    pub default_enchantment: CardEnchantment,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct Deck {
    pub id: Uuid,
    pub name: String,
    pub description: String,
    pub cards: Vec<MacroCard>,
    pub loop_count: Option<u32>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct Intent {
    pub id: Uuid,
    pub card_id: Uuid,
    pub name: String,
    pub target_description: String,
    pub required_confidence: f32,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct CombatTelemetry {
    pub current_hp: i32,
    pub max_hp: i32,
    pub pips: i32,
    pub power_pips: i32,
    pub shadow_pips: i32,
    pub battle_slot: i32,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct PlayerPose {
    pub x: f32,
    pub y: f32,
    pub z: f32,
    pub yaw: f32,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct ObservationFrame {
    pub version: u16,
    pub sequence: u64,
    pub timestamp_ns: u64,
    pub window_size: (u32, u32),
    pub combat_telemetry: Option<CombatTelemetry>,
    pub player_pose: Option<PlayerPose>,
    pub screen_hash: Option<u64>,
}

impl Default for ObservationFrame {
    fn default() -> Self {
        Self {
            version: 1,
            sequence: 0,
            timestamp_ns: 0,
            window_size: (800, 600),
            combat_telemetry: None,
            player_pose: None,
            screen_hash: None,
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum ControlAction {
    ActuateInputs(Vec<InputEvent>),
    ExecuteFallbackStep { card_id: Uuid, step_index: usize },
    NoOp,
    Halt { reason: String },
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum ControlSource {
    ReflexModel { confidence: f32 },
    MechanicalFallback { card_id: Uuid, step: usize },
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct ArbiterDecision {
    pub source: ControlSource,
    pub action: ControlAction,
    pub record_for_training: bool,
}

pub trait PolicyController: Send {
    fn propose(&mut self, intent: &Intent, obs: &ObservationFrame) -> (ControlAction, f32);
    fn on_outcome(&mut self, intent: &Intent, succeeded: bool);
}

/// Dual-Loop Arbiter confidence gate.
pub struct Arbiter {
    pub default_threshold: f32,
}

impl Arbiter {
    pub const DEFAULT_THRESHOLD: f32 = 0.85;

    pub fn new(threshold: Option<f32>) -> Self {
        Self {
            default_threshold: threshold.unwrap_or(Self::DEFAULT_THRESHOLD),
        }
    }

    pub fn decide<P: PolicyController>(
        &self,
        intent: &Intent,
        obs: &ObservationFrame,
        policy: &mut P,
        fallback_action: ControlAction,
    ) -> ArbiterDecision {
        let (proposed_action, confidence) = policy.propose(intent, obs);
        let threshold = if intent.required_confidence > 0.0 {
            intent.required_confidence
        } else {
            self.default_threshold
        };
        if confidence >= threshold {
            ArbiterDecision {
                source: ControlSource::ReflexModel { confidence },
                action: proposed_action,
                record_for_training: false,
            }
        } else {
            ArbiterDecision {
                source: ControlSource::MechanicalFallback {
                    card_id: intent.card_id,
                    step: 0,
                },
                action: fallback_action,
                record_for_training: true,
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use uuid::Uuid;

    struct MockPolicy {
        confidence: f32,
    }

    impl PolicyController for MockPolicy {
        fn propose(&mut self, _intent: &Intent, _obs: &ObservationFrame) -> (ControlAction, f32) {
            (ControlAction::ActuateInputs(Vec::new()), self.confidence)
        }
        fn on_outcome(&mut self, _intent: &Intent, _succeeded: bool) {}
    }

    #[test]
    fn test_arbiter_reflex_accepts_above_threshold() {
        let arbiter = Arbiter::new(Some(0.85));
        let intent = Intent {
            id: Uuid::new_v4(),
            card_id: Uuid::new_v4(),
            name: "Cast Tempest".into(),
            target_description: "Enemy circle".into(),
            required_confidence: 0.85,
        };
        let obs = ObservationFrame::default();
        let mut policy = MockPolicy { confidence: 0.92 };
        let decision = Arbiter::decide(
            &arbiter,
            &intent,
            &obs,
            &mut policy,
            ControlAction::ExecuteFallbackStep {
                card_id: intent.card_id,
                step_index: 0,
            },
        );
        match decision.source {
            ControlSource::ReflexModel { confidence } => assert_eq!(confidence, 0.92),
            _ => panic!("Expected ReflexModel source"),
        }
        assert!(!decision.record_for_training);
    }

    #[test]
    fn test_arbiter_fallback_trips_below_threshold() {
        let arbiter = Arbiter::new(Some(0.85));
        let intent = Intent {
            id: Uuid::new_v4(),
            card_id: Uuid::new_v4(),
            name: "Bazaar Purchase".into(),
            target_description: "Reagent slot".into(),
            required_confidence: 0.85,
        };
        let obs = ObservationFrame::default();
        let mut policy = MockPolicy { confidence: 0.72 };
        let decision = Arbiter::decide(
            &arbiter,
            &intent,
            &obs,
            &mut policy,
            ControlAction::ExecuteFallbackStep {
                card_id: intent.card_id,
                step_index: 0,
            },
        );
        match decision.source {
            ControlSource::MechanicalFallback { card_id, .. } => assert_eq!(card_id, intent.card_id),
            _ => panic!("Expected MechanicalFallback source"),
        }
        assert!(decision.record_for_training);
    }

    #[test]
    fn test_card_serialization_roundtrip() {
        let card = MacroCard {
            id: Uuid::new_v4(),
            name: "Halfang Opener".into(),
            school: AutomationSchool::Combat,
            pip_cost: 4,
            description: "Blade then Tempest".into(),
            mode: ExecutionMode::SequentialStep,
            payload: CardPayload::VisionWait {
                template_id: "combat_circle.png".into(),
                confidence: 0.88,
                timeout_ms: 10000,
                click_on_match: true,
            },
            default_enchantment: CardEnchantment {
                repeat_count: 1,
                pre_delay_ms: 500,
                post_delay_ms: 200,
                jitter_px: 2,
                confidence_threshold: Some(0.85),
                on_fail: FailPolicy::JumpToRelative(-1),
            },
        };
        let json = serde_json::to_string(&card).expect("Serialization failed");
        let decoded: MacroCard = serde_json::from_str(&json).expect("Deserialization failed");
        assert_eq!(card, decoded);
    }
}
