//! Phase 4 integration tests for the Maelstrom IDE.

use maelstrom_ide::{initialize_workspace, MaelstromIdeApp, IdePane};

#[test]
fn test_phase4_ide_initialization() {
    // Initialize configuration.
    let config = initialize_workspace();
    assert_eq!(config.theme, "spiral_spellbook");
    assert_eq!(config.profile, "Wizard101.toml");
}

#[test]
fn test_phase4_ide_app_structure() {
    let app = MaelstromIdeApp::new();
    // Verify that the tree contains exactly 5 leaf panes.
    let leaf_count = app
        .tree
        .tiles
        .iter()
        .filter(|(_, tile)| matches!(tile, egui_tiles::Tile::Pane(_)))
        .count();
    assert_eq!(leaf_count, 5);
    // Verify that each pane appears at least once.
    let panes = [
        IdePane::GameSwitcher,
        IdePane::DuelCircle,
        IdePane::SpellbookLibrary,
        IdePane::EnchantmentInspector,
        IdePane::ArbiterTelemetry,
    ];
    for pane in &panes {
        // Ensure the pane exists in the tree (simple existence check).
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
