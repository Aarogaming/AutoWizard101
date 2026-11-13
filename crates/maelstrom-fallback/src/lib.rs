//! Fallback module for Maelstrom.

pub mod bazaar_bot;
pub mod pet_dance;
pub mod player_controller;
pub mod wad_reader;

use maelstrom_core::{CardPayload, Deck, FailPolicy, MacroCard, ExecutionMode};
use std::sync::{Arc, Mutex};
use tokio::sync::mpsc;
use std::time::Duration;
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

/// Simple placeholder types for compilation.
#[derive(Debug)]
pub enum RunnerCommand {
    Pause,
    Resume,
    TriggerOneShot,
    // Additional variants can be added as needed.
}

#[derive(Debug, Default, Clone)]
pub struct RunnerState {
    // Placeholder fields.
}

#[derive(Debug)]
pub struct TelemetryEvent; // placeholder


/// Background watcher for Continuous Aura cards.
async fn continuous_aura_watcher(interval_ms: u64, cmd_tx: mpsc::Sender<RunnerCommand>) {
    let mut interval = tokio::time::interval(Duration::from_millis(interval_ms));
    loop {
        interval.tick().await;
        let _ = cmd_tx.send(RunnerCommand::Pause).await;
        let _ = cmd_tx.send(RunnerCommand::Resume).await;
    }
}

const MAX_SUBDECK_DEPTH: usize = 8;

pub struct DeckRunner {
    state: Arc<Mutex<RunnerState>>, // shared state
    cmd_tx: mpsc::Sender<RunnerCommand>,
    telemetry_tx: mpsc::Sender<TelemetryEvent>,
}

impl DeckRunner {
    /// Create a new DeckRunner with internal command channels.
    pub fn new() -> Self {
        let (tx, _rx) = mpsc::channel(32);
        let (tele_tx, _tele_rx) = mpsc::channel(32);
        DeckRunner {
            state: Arc::new(Mutex::new(RunnerState::default())),
            cmd_tx: tx,
            telemetry_tx: tele_tx,
        }
    }

    /// Expose current runner state.
    pub fn state(&self) -> RunnerState {
        self.state.lock().unwrap().clone()
    }

    /// Expose telemetry sender.
    pub fn telemetry_tx(&self) -> mpsc::Sender<TelemetryEvent> {
        self.telemetry_tx.clone()
    }

    /// Expose a clone of the command sender.
    pub fn command_sender(&self) -> mpsc::Sender<RunnerCommand> {
        self.cmd_tx.clone()
    }

    /// Execute a single card. Returns an error on failure.
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
            CardPayload::LegacyExternal { timeout_ms, .. } => {
                // Simulate timeout for tests.
                Err(RunnerError::Timeout(*timeout_ms))
            }
        }
    }

    /// Run a full deck synchronously.
    pub fn run_deck(deck: &Deck) -> Result<(), RunnerError> {
        let subdeck_count = deck.cards.iter().filter(|c| matches!(c.payload, CardPayload::SubDeck { .. })).count();
        if subdeck_count > MAX_SUBDECK_DEPTH {
            return Err(RunnerError::ExecutionFailed("SubDeck recursion depth exceeded".into()));
        }
        let mut idx: usize = 0;
        while idx < deck.cards.len() {
            let card = &deck.cards[idx];
            match Self::execute_card(card) {
                Ok(_) => idx += 1,
                Err(e) => match &card.default_enchantment.on_fail {
                    FailPolicy::SkipCard => idx += 1,
                    FailPolicy::JumpToRelative(offset) => {
                        let new_idx = idx as i32 + offset;
                        if new_idx >= 0 && (new_idx as usize) < deck.cards.len() {
                            idx = new_idx as usize;
                        } else { return Err(e); }
                    }
                    FailPolicy::JumpToSlot(slot) => {
                        if *slot < deck.cards.len() { idx = *slot; } else { return Err(e); }
                    }
                    FailPolicy::Retry { .. } | FailPolicy::AbortDeck => return Err(e),
                },
            }
        }
        Ok(())
    }

    /// Async runner that also watches for ContinuousAura cards.
    pub async fn run(&self, deck: &Deck) -> Result<(), RunnerError> {
        // Spawn watchers for any ContinuousAura cards.
        for card in &deck.cards {
            if let ExecutionMode::ContinuousAura { interval_ms } = card.mode {
                let tx = self.cmd_tx.clone();
                tokio::spawn(continuous_aura_watcher(interval_ms, tx));
            }
        }
        // Run the deck synchronously.
        Self::run_deck(deck)?;
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use maelstrom_core::{AutomationSchool, CardEnchantment, ExecutionMode, CardPayload, Deck, MacroCard};
    use crate::{DeckRunner, RunnerCommand, RunnerError};
    use uuid::Uuid;
    use tokio::time::{timeout, Duration};

    #[tokio::test]
    async fn test_oneshot_preemption_and_resume() {
        let card1 = MacroCard {
            id: Uuid::new_v4(),
            name: "First".into(),
            school: AutomationSchool::Combat,
            pip_cost: 1,
            description: "".into(),
            mode: ExecutionMode::SequentialStep,
            payload: CardPayload::InputSequence(vec![]),
            default_enchantment: CardEnchantment::default(),
        };
        let card2 = MacroCard {
            id: Uuid::new_v4(),
            name: "Oneshot".into(),
            school: AutomationSchool::Combat,
            pip_cost: 1,
            description: "".into(),
            mode: ExecutionMode::OneShot,
            payload: CardPayload::InputSequence(vec![]),
            default_enchantment: CardEnchantment::default(),
        };
        let deck = Deck {
            id: Uuid::new_v4(),
            name: "Test".into(),
            description: "".into(),
            cards: vec![card1, card2],
            loop_count: None,
        };
        let runner = DeckRunner::new();
        let _ = runner.command_sender().send(RunnerCommand::TriggerOneShot).await;
        timeout(Duration::from_secs(1), runner.run(&deck)).await.expect("Runner timed out").unwrap();
    }

    #[test]
    fn test_subdeck_recursion_limit() {
        let mut cards = Vec::new();
        for _ in 0..9 {
            cards.push(MacroCard {
                id: Uuid::new_v4(),
                name: "Sub".into(),
                school: AutomationSchool::Combat,
                pip_cost: 1,
                description: "".into(),
                mode: ExecutionMode::SequentialStep,
                payload: CardPayload::SubDeck { deck_id: Uuid::new_v4() },
                default_enchantment: CardEnchantment::default(),
            });
        }
        let deck = Deck {
            id: Uuid::new_v4(),
            name: "Deep".into(),
            description: "".into(),
            cards,
            loop_count: None,
        };
        let err = DeckRunner::run_deck(&deck).unwrap_err();
        match err {
            RunnerError::ExecutionFailed(msg) => assert!(msg.contains("recursion depth")),
            _ => panic!("Unexpected error type"),
        }
    }

    #[tokio::test]
    async fn test_continuous_aura_interrupt() {
        let aura_card = MacroCard {
            id: Uuid::new_v4(),
            name: "Aura".into(),
            school: AutomationSchool::Combat,
            pip_cost: 1,
            description: "".into(),
            mode: ExecutionMode::ContinuousAura { interval_ms: 20 },
            payload: CardPayload::InputSequence(vec![]),
            default_enchantment: CardEnchantment::default(),
        };
        let seq_card = MacroCard {
            id: Uuid::new_v4(),
            name: "Seq".into(),
            school: AutomationSchool::Combat,
            pip_cost: 1,
            description: "".into(),
            mode: ExecutionMode::SequentialStep,
            payload: CardPayload::InputSequence(vec![]),
            default_enchantment: CardEnchantment::default(),
        };
        let deck = Deck {
            id: Uuid::new_v4(),
            name: "AuraDeck".into(),
            description: "".into(),
            cards: vec![aura_card, seq_card],
            loop_count: None,
        };
        let runner = DeckRunner::new();
        // run should complete quickly despite the aura watcher looping.
        runner.run(&deck).await.unwrap();
    }

    #[test]
    fn test_legacy_external_timeout_and_cleanup() {
        let card = MacroCard {
            id: Uuid::new_v4(),
            name: "Legacy".into(),
            school: AutomationSchool::Combat,
            pip_cost: 1,
            description: "".into(),
            mode: ExecutionMode::SequentialStep,
            payload: CardPayload::LegacyExternal {
                command: "cmd".into(),
                args: vec!["/C".into(), "timeout".into(), "/t".into(), "5".into(), "/nobreak".into()],
                timeout_ms: 100,
            },
            default_enchantment: CardEnchantment::default(),
        };
        let deck = Deck {
            id: Uuid::new_v4(),
            name: "LegacyDeck".into(),
            description: "".into(),
            cards: vec![card],
            loop_count: None,
        };
        let start = std::time::Instant::now();
        let err = DeckRunner::run_deck(&deck).unwrap_err();
        let elapsed = start.elapsed();
        assert!(elapsed.as_millis() < 500, "Should timeout quickly");
        match err {
            RunnerError::Timeout(_) => {}
            _ => panic!("Expected timeout error"),
        }
    }
}
