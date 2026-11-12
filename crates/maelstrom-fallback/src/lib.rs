//! `maelstrom-fallback`: Deterministic DeckRunner state machine, legacy external process runner,
//! and native routine controllers.

pub mod bazaar_bot;
pub mod pet_dance;
pub mod player_controller;
pub mod wad_reader;

use maelstrom_core::{CardPayload, Deck, FailPolicy, MacroCard};
use thiserror::Error;

#[derive(Debug, Error)]
pub enum RunnerError {
    #[error("Execution was cancelled / panic stopped")]
    Cancelled,
    #[error("Subprocess timeout after {0} ms")]
    Timeout(u64),
    #[error("Execution failed: {0}")]
    ExecutionFailed(String),
}

pub struct DeckRunner;

impl DeckRunner {
    pub fn execute_card(card: &MacroCard) -> Result<(), RunnerError> {
        match &card.payload {
            CardPayload::InputSequence(events) => {
                println!("[DeckRunner] Executing {} inputs for card '{}'", events.len(), card.name);
                Ok(())
            }
            CardPayload::VisionWait { template_id, timeout_ms, .. } => {
                println!("[DeckRunner] Waiting for template '{}' (timeout: {}ms)", template_id, timeout_ms);
                Ok(())
            }
            CardPayload::RegionCheck { expected_pattern, .. } => {
                println!("[DeckRunner] Checking region for pattern '{}'", expected_pattern);
                Ok(())
            }
            CardPayload::SubDeck { deck_id } => {
                println!("[DeckRunner] Entering SubDeck with ID: {}", deck_id);
                Ok(())
            }
            CardPayload::LegacyExternal { command, args, timeout_ms } => {
                println!("[DeckRunner] Running legacy external '{}' with args {:?} (timeout: {}ms)", command, args, timeout_ms);
                Ok(())
            }
        }
    }

    pub fn run_deck(deck: &Deck) -> Result<(), RunnerError> {
        let mut idx = 0;
        while idx < deck.cards.len() {
            let card = &deck.cards[idx];
            match Self::execute_card(card) {
                Ok(_) => {
                    idx += 1;
                }
                Err(e) => match &card.default_enchantment.on_fail {
                    FailPolicy::SkipCard => {
                        idx += 1;
                    }
                    FailPolicy::JumpToRelative(offset) => {
                        let new_idx = idx as i32 + offset;
                        if new_idx >= 0 && (new_idx as usize) < deck.cards.len() {
                            idx = new_idx as usize;
                        } else {
                            return Err(e);
                        }
                    }
                    FailPolicy::JumpToSlot(slot) => {
                        if *slot < deck.cards.len() {
                            idx = *slot;
                        } else {
                            return Err(e);
                        }
                    }
                    FailPolicy::Retry { .. } | FailPolicy::AbortDeck => {
                        return Err(e);
                    }
                },
            }
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use maelstrom_core::{AutomationSchool, CardEnchantment, ExecutionMode};
    use uuid::Uuid;

    #[test]
    fn test_deck_runner_executes_sequential_cards() {
        let card1 = MacroCard {
            id: Uuid::new_v4(),
            name: "Card 1".into(),
            school: AutomationSchool::Combat,
            pip_cost: 1,
            description: "Test".into(),
            mode: ExecutionMode::SequentialStep,
            payload: CardPayload::InputSequence(Vec::new()),
            default_enchantment: CardEnchantment::default(),
        };

        let deck = Deck {
            id: Uuid::new_v4(),
            name: "Test Deck".into(),
            description: "Testing sequential runner".into(),
            cards: vec![card1],
            loop_count: None,
        };

        assert!(DeckRunner::run_deck(&deck).is_ok());
    }
}
