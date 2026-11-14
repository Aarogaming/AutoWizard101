//! `maelstrom-core`: Domain types, Arbiter confidence gate, deck model, and observation contracts.
//! Designed for no_std / alloc compatibility (zero OS or UI dependencies).

#![cfg_attr(not(feature = "std"), no_std)]

extern crate alloc;

use alloc::string::String;
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
    /// MCP tool call representing a programmable macro action/query dispatched
    /// to the client translator or game observation engine.
    McpFunction {
        tool_name: String,
        arguments_json: String,
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
    pub timestamp_ms: u64,
    pub in_combat: bool,
    pub combat: Option<CombatTelemetry>,
    pub pose: Option<PlayerPose>,
    pub raw_features: Vec<f32>,
}

impl Default for ObservationFrame {
    fn default() -> Self {
        Self {
            version: 1,
            timestamp_ms: 0,
            in_combat: false,
            combat: None,
            pose: None,
            raw_features: Vec::new(),
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub enum ControlAction {
    CastCard { card_id: Uuid },
    PassTurn,
    FleeCombat,
    MoveTo { x: f32, y: f32, z: f32 },
    Interact,
    NoOp,
}

pub trait PolicyController: Send + Sync {
    fn propose(&mut self, intent: &Intent, obs: &ObservationFrame) -> (ControlAction, f32);
    fn on_outcome(&mut self, intent: &Intent, succeeded: bool);
}

pub struct Arbiter {
    pub confidence_threshold: f32,
}

impl Arbiter {
    pub fn new(threshold: f32) -> Self {
        Self {
            confidence_threshold: threshold,
        }
    }

    pub fn decide(
        &self,
        proposed_action: ControlAction,
        model_confidence: f32,
        required_confidence: f32,
    ) -> ArbiterDecision {
        let effective_req = required_confidence.max(self.confidence_threshold);
        if model_confidence >= effective_req {
            ArbiterDecision::DispatchReflex(proposed_action)
        } else {
            ArbiterDecision::TriggerFallback {
                reason: ArbiterFallbackReason::ConfidenceTooLow {
                    score: model_confidence,
                    required: effective_req,
                },
            }
        }
    }
}

#[derive(Debug, PartialEq)]
pub enum ArbiterFallbackReason {
    ConfidenceTooLow { score: f32, required: f32 },
    SafetyBoundaryTripped(String),
}

#[derive(Debug, PartialEq)]
pub enum ArbiterDecision {
    DispatchReflex(ControlAction),
    TriggerFallback { reason: ArbiterFallbackReason },
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_arbiter_reflex_accepts_above_threshold() {
        let arb = Arbiter::new(0.85);
        let dec = arb.decide(ControlAction::PassTurn, 0.90, 0.80);
        assert_eq!(dec, ArbiterDecision::DispatchReflex(ControlAction::PassTurn));
    }

    #[test]
    fn test_arbiter_fallback_trips_below_threshold() {
        let arb = Arbiter::new(0.85);
        let dec = arb.decide(ControlAction::PassTurn, 0.80, 0.70);
        match dec {
            ArbiterDecision::TriggerFallback { reason } => match reason {
                ArbiterFallbackReason::ConfidenceTooLow { score, required } => {
                    assert_eq!(score, 0.80);
                    assert_eq!(required, 0.85);
                }
                _ => panic!("wrong fallback reason"),
            },
            _ => panic!("expected fallback"),
        }
    }

    #[test]
    fn test_card_serialization_roundtrip() {
        let card = MacroCard {
            id: Uuid::new_v4(),
            name: "Bazaar Snipe".into(),
            school: AutomationSchool::Economy,
            pip_cost: 0,
            description: "Test".into(),
            mode: ExecutionMode::SequentialStep,
            payload: CardPayload::McpFunction {
                tool_name: "inspect_client_state".into(),
                arguments_json: "{}".into(),
            },
            default_enchantment: CardEnchantment::default(),
        };

        let json = serde_json::to_string(&card).expect("serialize");
        let deserialized: MacroCard = serde_json::from_str(&json).expect("deserialize");
        assert_eq!(card.name, deserialized.name);
        assert_eq!(card.payload, deserialized.payload);
    }
}
