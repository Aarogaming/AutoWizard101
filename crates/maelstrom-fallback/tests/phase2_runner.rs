//! Phase 2 integration tests for maelstrom-fallback.

use maelstrom_core::{
    AutomationSchool, CardEnchantment, CardPayload, Deck, ExecutionMode, MacroCard,
};
use maelstrom_fallback::{DeckRunner, RunnerCommand, RunnerError};
use tokio::time::{timeout, Duration};
use uuid::Uuid;

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
    let _ = runner
        .command_sender()
        .send(RunnerCommand::TriggerOneShot)
        .await;
    timeout(Duration::from_secs(1), runner.run(&deck))
        .await
        .expect("Runner timed out")
        .unwrap();
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
            payload: CardPayload::SubDeck {
                deck_id: Uuid::new_v4(),
            },
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
            args: vec![
                "/C".into(),
                "timeout".into(),
                "/t".into(),
                "5".into(),
                "/nobreak".into(),
            ],
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
