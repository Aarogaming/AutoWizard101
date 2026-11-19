//! `quest_interpreter`: Data-driven quest objective interpreter and universal dialogue state machine.
//!
//! Replaces hardcoded per-quest scripts with 6 canonical objective archetypes,
//! parses live HUD/tracker strings into `W101QuestFact` primitives, incorporates
//! the built-in Quest Helper compass vector, and manages NPC dialogue/offer cycles.
//!
//! Operates under `Facts ≠ Authority ≠ Actions` without heap churn in `no_std` + `alloc`.

use alloc::format;
use alloc::string::{String, ToString};
use serde::{Deserialize, Serialize};

/// The six canonical objective archetypes in Wizard101.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum QuestArchetype {
    /// "Talk to <NPC> in <Zone>"
    TalkToNpc,
    /// "Explore <Landmark>" / "Go to <Tower/Gate>"
    ExploreLandmark,
    /// "Defeat <N> <Mob> in <Zone>"
    DefeatCount,
    /// "Defeat <Mob> and collect <N> <Item>"
    DefeatAndCollect,
    /// "Collect <N> <Object>" / "Use <Device>"
    InteractGather,
    /// "Go to <Tower>" -> "Defeat <Boss>" (Sigil instance entry & combat)
    BossSigilInstance,
}

/// Structured quest fact parsed from live UI/HUD observations or quest tracker text.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct W101QuestFact {
    pub quest_title: String,
    pub is_mainline: bool,
    pub archetype: QuestArchetype,
    pub target_entity_or_landmark: String,
    pub target_zone: String,
    pub current_count: u32,
    pub required_count: u32,
    /// Quest Helper compass vector in degrees [0, 360) pointing toward objective.
    pub fallback_bearing_deg: Option<f64>,
}

impl W101QuestFact {
    /// Attempts to parse raw tracker HUD or journal text into a structured `W101QuestFact`.
    pub fn parse_from_text(
        title: &str,
        line: &str,
        is_mainline: bool,
        compass_deg: Option<f64>,
    ) -> Option<Self> {
        let trimmed = line.trim();
        if trimmed.is_empty() {
            return None;
        }

        // Pattern 1: Talk to <NPC> in <Zone>
        if let Some(rest) = trimmed.strip_prefix("Talk to ") {
            let (npc, zone) = if let Some(idx) = rest.find(" in ") {
                let npc_part = rest[..idx].trim();
                let zone_part = rest[idx + 4..].trim();
                (npc_part, zone_part)
            } else {
                (rest.trim(), "")
            };
            return Some(Self {
                quest_title: title.to_string(),
                is_mainline,
                archetype: QuestArchetype::TalkToNpc,
                target_entity_or_landmark: npc.to_string(),
                target_zone: zone.to_string(),
                current_count: 0,
                required_count: 1,
                fallback_bearing_deg: compass_deg,
            });
        }

        // Pattern 2: Explore <Landmark> / Go to <Landmark>
        let explore_prefix = if trimmed.starts_with("Explore ") {
            Some("Explore ")
        } else if trimmed.starts_with("Go to ") {
            Some("Go to ")
        } else {
            None
        };
        if let Some(prefix) = explore_prefix {
            let rest = &trimmed[prefix.len()..];
            let (target, zone) = if let Some(idx) = rest.find(" in ") {
                (rest[..idx].trim(), rest[idx + 4..].trim())
            } else {
                (rest.trim(), "")
            };
            return Some(Self {
                quest_title: title.to_string(),
                is_mainline,
                archetype: QuestArchetype::ExploreLandmark,
                target_entity_or_landmark: target.to_string(),
                target_zone: zone.to_string(),
                current_count: 0,
                required_count: 1,
                fallback_bearing_deg: compass_deg,
            });
        }

        // Pattern 3: Defeat <Mob> and collect <N> <Item>
        if let Some(rest) = trimmed.strip_prefix("Defeat ") {
            if let Some(collect_idx) = rest.find(" and collect ") {
                let mob = rest[..collect_idx].trim();
                let after_collect = rest[collect_idx + 13..].trim();
                let (count, item) = parse_count_and_name(after_collect);
                return Some(Self {
                    quest_title: title.to_string(),
                    is_mainline,
                    archetype: QuestArchetype::DefeatAndCollect,
                    target_entity_or_landmark: format!("{} / {}", mob, item),
                    target_zone: String::new(),
                    current_count: 0,
                    required_count: count,
                    fallback_bearing_deg: compass_deg,
                });
            }

            // Pattern 4: Defeat <N> <Mob> in <Zone> or Defeat <Mob>
            let (target, zone) = if let Some(idx) = rest.find(" in ") {
                (rest[..idx].trim(), rest[idx + 4..].trim())
            } else {
                (rest.trim(), "")
            };

            let is_boss = title.to_lowercase().contains("tower")
                || target.to_lowercase().contains("boss")
                || target.to_lowercase().contains("kraken");
            let archetype = if is_boss {
                QuestArchetype::BossSigilInstance
            } else {
                QuestArchetype::DefeatCount
            };

            let (count, mob_name) = parse_count_and_name(target);
            return Some(Self {
                quest_title: title.to_string(),
                is_mainline,
                archetype,
                target_entity_or_landmark: mob_name.to_string(),
                target_zone: zone.to_string(),
                current_count: 0,
                required_count: count.max(1),
                fallback_bearing_deg: compass_deg,
            });
        }

        // Pattern 5: Collect <N> <Object> or Use <Device>
        if let Some(rest) = trimmed.strip_prefix("Collect ") {
            let (count, obj) = parse_count_and_name(rest);
            return Some(Self {
                quest_title: title.to_string(),
                is_mainline,
                archetype: QuestArchetype::InteractGather,
                target_entity_or_landmark: obj.to_string(),
                target_zone: String::new(),
                current_count: 0,
                required_count: count.max(1),
                fallback_bearing_deg: compass_deg,
            });
        }
        if let Some(rest) = trimmed.strip_prefix("Use ") {
            return Some(Self {
                quest_title: title.to_string(),
                is_mainline,
                archetype: QuestArchetype::InteractGather,
                target_entity_or_landmark: rest.trim().to_string(),
                target_zone: String::new(),
                current_count: 0,
                required_count: 1,
                fallback_bearing_deg: compass_deg,
            });
        }

        None
    }
}

fn parse_count_and_name(input: &str) -> (u32, &str) {
    let trimmed = input.trim();
    if let Some(first_space) = trimmed.find(' ') {
        if let Ok(count) = trimmed[..first_space].parse::<u32>() {
            return (count, trimmed[first_space + 1..].trim());
        }
    }
    (1, trimmed)
}

/// Universal NPC Dialogue and Quest-Accept State Machine.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum DialogueState {
    Idle,
    PromptVisible {
        prompt_text: String,
    },
    PagingDialogue {
        page_index: u32,
        dialogue_text: String,
    },
    QuestOffer {
        quest_title: String,
    },
    TurnInReward {
        quest_title: String,
    },
    StuckBackoff {
        failed_attempts: u32,
        cooldown_ms: u64,
    },
}

/// Policy for quest acceptance.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum QuestAcceptPolicy {
    AcceptMainlineOnly,
    AcceptAllInZone,
}

/// Universal dialogue state machine managing NPC conversation progression.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DialogueStateMachine {
    pub state: DialogueState,
    pub policy: QuestAcceptPolicy,
    pub failed_interact_count: u32,
    pub max_interact_retries: u32,
}

impl Default for DialogueStateMachine {
    fn default() -> Self {
        Self::new(QuestAcceptPolicy::AcceptAllInZone)
    }
}

impl DialogueStateMachine {
    pub fn new(policy: QuestAcceptPolicy) -> Self {
        Self {
            state: DialogueState::Idle,
            policy,
            failed_interact_count: 0,
            max_interact_retries: 3,
        }
    }

    /// Transitions to PromptVisible when the interact prompt ("Press X to Talk") is detected.
    pub fn on_prompt_detected(&mut self, text: impl Into<String>) {
        if self.state == DialogueState::Idle {
            self.state = DialogueState::PromptVisible {
                prompt_text: text.into(),
            };
        }
    }

    /// Emits intent to dispatch physical 'X' interact pulse.
    pub fn dispatch_interact(&mut self) -> Option<&'static str> {
        if let DialogueState::PromptVisible { .. } = self.state {
            self.state = DialogueState::PagingDialogue {
                page_index: 0,
                dialogue_text: String::new(),
            };
            self.failed_interact_count = 0;
            Some("PROPOSAL:Interact:X")
        } else {
            None
        }
    }

    /// Handles an incoming dialogue page.
    pub fn on_dialogue_page(
        &mut self,
        text: impl Into<String>,
        is_final: bool,
        is_reward: bool,
        quest_title: &str,
    ) -> Option<&'static str> {
        if let DialogueState::PagingDialogue {
            ref mut page_index,
            ref mut dialogue_text,
        } = self.state
        {
            if *page_index >= 25 {
                self.state = DialogueState::StuckBackoff {
                    failed_attempts: *page_index,
                    cooldown_ms: 3000,
                };
                return Some("PROPOSAL:Dialogue:Dismiss");
            }

            *dialogue_text = text.into();
            if is_final {
                if is_reward {
                    self.state = DialogueState::TurnInReward {
                        quest_title: quest_title.to_string(),
                    };
                    Some("PROPOSAL:Dialogue:Complete")
                } else {
                    self.state = DialogueState::QuestOffer {
                        quest_title: quest_title.to_string(),
                    };
                    match self.policy {
                        QuestAcceptPolicy::AcceptAllInZone => Some("PROPOSAL:Dialogue:Accept"),
                        QuestAcceptPolicy::AcceptMainlineOnly => Some("PROPOSAL:Dialogue:Accept"),
                    }
                }
            } else {
                *page_index += 1;
                Some("PROPOSAL:Dialogue:AdvanceSpace")
            }
        } else {
            None
        }
    }

    /// Completes turn-in or acceptance, returning to Idle state.
    pub fn finish_interaction(&mut self) {
        self.state = DialogueState::Idle;
        self.failed_interact_count = 0;
    }

    /// Handles failed interact pulses, triggering stuck backoff if threshold exceeded.
    pub fn on_interact_failed(&mut self) -> bool {
        self.failed_interact_count += 1;
        if self.failed_interact_count >= self.max_interact_retries {
            self.state = DialogueState::StuckBackoff {
                failed_attempts: self.failed_interact_count,
                cooldown_ms: 2500,
            };
            true // Triggered backoff
        } else {
            false
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_talk_to_npc() {
        let fact = W101QuestFact::parse_from_text(
            "Trouble on Triton Avenue",
            "Talk to Susie Gryphonbane in Triton Avenue",
            true,
            Some(45.0),
        )
        .expect("Should parse talk archetype");

        assert_eq!(fact.archetype, QuestArchetype::TalkToNpc);
        assert_eq!(fact.target_entity_or_landmark, "Susie Gryphonbane");
        assert_eq!(fact.target_zone, "Triton Avenue");
        assert_eq!(fact.fallback_bearing_deg, Some(45.0));
    }

    #[test]
    fn test_parse_explore_landmark() {
        let fact = W101QuestFact::parse_from_text(
            "Haunted Towers",
            "Explore Haunted Towers in Triton Avenue",
            true,
            Some(180.0),
        )
        .expect("Should parse explore archetype");

        assert_eq!(fact.archetype, QuestArchetype::ExploreLandmark);
        assert_eq!(fact.target_entity_or_landmark, "Haunted Towers");
    }

    #[test]
    fn test_parse_defeat_count() {
        let fact = W101QuestFact::parse_from_text(
            "Menacing Minions",
            "Defeat 4 Scarlet Screamers in Triton Avenue",
            true,
            None,
        )
        .expect("Should parse defeat archetype");

        assert_eq!(fact.archetype, QuestArchetype::DefeatCount);
        assert_eq!(fact.target_entity_or_landmark, "Scarlet Screamers");
        assert_eq!(fact.required_count, 4);
    }

    #[test]
    fn test_parse_defeat_and_collect() {
        let fact = W101QuestFact::parse_from_text(
            "Bitter Harvest",
            "Defeat Rotting Fodder and collect 3 Luminescent Fungus",
            true,
            None,
        )
        .expect("Should parse defeat and collect archetype");

        assert_eq!(fact.archetype, QuestArchetype::DefeatAndCollect);
        assert_eq!(
            fact.target_entity_or_landmark,
            "Rotting Fodder / Luminescent Fungus"
        );
        assert_eq!(fact.required_count, 3);
    }

    #[test]
    fn test_parse_boss_sigil_instance() {
        let fact = W101QuestFact::parse_from_text(
            "Storm the Tower",
            "Defeat the Kraken",
            true,
            Some(90.0),
        )
        .expect("Should parse boss sigil archetype");

        assert_eq!(fact.archetype, QuestArchetype::BossSigilInstance);
        assert_eq!(fact.target_entity_or_landmark, "the Kraken");
    }

    #[test]
    fn test_dialogue_state_machine_full_flow() {
        let mut dsm = DialogueStateMachine::new(QuestAcceptPolicy::AcceptAllInZone);
        assert_eq!(dsm.state, DialogueState::Idle);

        dsm.on_prompt_detected("Press X to Talk to Susie");
        assert!(matches!(dsm.state, DialogueState::PromptVisible { .. }));

        let intent = dsm.dispatch_interact().unwrap();
        assert_eq!(intent, "PROPOSAL:Interact:X");
        assert!(matches!(
            dsm.state,
            DialogueState::PagingDialogue { page_index: 0, .. }
        ));

        // Advance 1 page
        let p1 = dsm
            .on_dialogue_page(
                "Greetings young wizard!",
                false,
                false,
                "Trouble on Triton Avenue",
            )
            .unwrap();
        assert_eq!(p1, "PROPOSAL:Dialogue:AdvanceSpace");
        assert!(matches!(
            dsm.state,
            DialogueState::PagingDialogue { page_index: 1, .. }
        ));

        // Final page: quest offer
        let offer = dsm
            .on_dialogue_page("Will you help us?", true, false, "Trouble on Triton Avenue")
            .unwrap();
        assert_eq!(offer, "PROPOSAL:Dialogue:Accept");
        assert!(matches!(dsm.state, DialogueState::QuestOffer { .. }));

        dsm.finish_interaction();
        assert_eq!(dsm.state, DialogueState::Idle);
    }

    #[test]
    fn test_dialogue_state_machine_stuck_backoff() {
        let mut dsm = DialogueStateMachine::new(QuestAcceptPolicy::AcceptAllInZone);
        assert!(!dsm.on_interact_failed());
        assert!(!dsm.on_interact_failed());
        // 3rd failure triggers backoff
        assert!(dsm.on_interact_failed());
        assert!(matches!(
            dsm.state,
            DialogueState::StuckBackoff {
                failed_attempts: 3,
                cooldown_ms: 2500
            }
        ));
    }
}
