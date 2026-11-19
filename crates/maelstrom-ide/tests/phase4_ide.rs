//! Phase 4 integration tests for the Maelstrom IDE.

use maelstrom_core::{AutomationSchool, CardEnchantment, CardPayload, ExecutionMode, MacroCard};
use maelstrom_ide::{initialize_workspace, IdePane, MaelstromIdeApp, SpiralTheme};
use uuid::Uuid;

#[test]
fn test_phase4_ide_initialization() {
    let config = initialize_workspace();
    assert_eq!(config.theme, "spiral_spellbook");
    assert_eq!(config.profile, "Wizard101.toml");
}

#[test]
fn test_phase4_ide_app_structure() {
    let app = MaelstromIdeApp::new();
    let leaf_count = app
        .tree
        .tiles
        .iter()
        .filter(|(_, tile)| matches!(tile, egui_tiles::Tile::Pane(_)))
        .count();
    assert_eq!(leaf_count, 5);

    let panes = [
        IdePane::GameSwitcher,
        IdePane::DuelCircle,
        IdePane::SpellbookLibrary,
        IdePane::EnchantmentInspector,
        IdePane::ArbiterTelemetry,
    ];
    for pane in &panes {
        let mut found = false;
        for (_, tile) in app.tree.tiles.iter() {
            if let egui_tiles::Tile::Pane(p) = tile {
                if p == pane {
                    found = true;
                    break;
                }
            }
        }
        assert!(found, "pane {:?} missing", pane);
    }
}

#[test]
fn test_phase4_card_deck_and_inspector_manipulation() {
    let mut app = MaelstromIdeApp::new();
    assert_eq!(app.state.active_deck.cards.len(), 5);

    // Add a new programmable MCP card to the deck
    let new_card = MacroCard {
        id: Uuid::new_v4(),
        name: "Colossal Damage Calc".to_string(),
        school: AutomationSchool::Combat,
        pip_cost: 0,
        description: "+275 damage to single spell via MCP arithmetic".to_string(),
        mode: ExecutionMode::SequentialStep,
        payload: CardPayload::McpFunction {
            tool_name: "evaluate_damage".to_string(),
            arguments_json: r#"{"base_damage": 300.0, "enchant": 275.0}"#.to_string(),
        },
        default_enchantment: CardEnchantment::default(),
    };
    let new_id = new_card.id;
    app.state.active_deck.cards.push(new_card);
    assert_eq!(app.state.active_deck.cards.len(), 6);

    // Select the newly added card for enchantment inspection
    app.state.selected_card_id = Some(new_id);
    let inspected = app
        .state
        .active_deck
        .cards
        .iter_mut()
        .find(|c| c.id == new_id)
        .unwrap();
    inspected.default_enchantment.repeat_count = 3;
    inspected.default_enchantment.pre_delay_ms = 250;

    assert_eq!(inspected.default_enchantment.repeat_count, 3);
    assert_eq!(inspected.default_enchantment.pre_delay_ms, 250);

    // Test school colors
    let fire_color = SpiralTheme::school_color(AutomationSchool::Combat);
    assert_eq!(fire_color, SpiralTheme::COMBAT_CRIMSON);
}
