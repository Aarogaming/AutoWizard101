//! `maelstrom-ide`: Dockable card IDE workspace implementation using egui_tiles.

use egui::{self, CentralPanel, Context, TopBottomPanel, Visuals, WidgetText};
use egui_tiles::{Behavior, Tree, TileId, UiResponse};
use eframe::{self, App, epaint::Color32};

/// Configuration for the IDE workspace.
#[derive(Debug, Clone)]
pub struct IdeWorkspaceConfig {
    /// Theme identifier, currently only "spiral_spellbook".
    pub theme: String,
    /// Profile configuration file name.
    pub profile: String,
}

impl Default for IdeWorkspaceConfig {
    fn default() -> Self {
        Self {
            theme: "spiral_spellbook".to_string(),
            profile: "Wizard101.toml".to_string(),
        }
    }
}

/// Enum representing the five panes of the Spellbook IDE.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub enum IdePane {
    GameSwitcher,
    DuelCircle,
    SpellbookLibrary,
    EnchantmentInspector,
    ArbiterTelemetry,
}

impl std::fmt::Display for IdePane {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let s = match self {
            IdePane::GameSwitcher => "Game Switcher",
            IdePane::DuelCircle => "Duel Circle",
            IdePane::SpellbookLibrary => "Spellbook Library",
            IdePane::EnchantmentInspector => "Enchantment Inspector",
            IdePane::ArbiterTelemetry => "Arbiter Telemetry",
        };
        write!(f, "{}", s)
    }
}

/// Simple Behavior implementation for IdePane.
pub struct IdeBehavior;

impl Behavior<IdePane> for IdeBehavior {
    fn pane_ui(&mut self, ui: &mut egui::Ui, _tile_id: TileId, pane: &mut IdePane) -> UiResponse {
        ui.label(format!("Pane: {}", pane));
        UiResponse::None
    }

    fn tab_title_for_pane(&mut self, pane: &IdePane) -> WidgetText {
        WidgetText::from(pane.to_string())
    }
}

/// The main application structure implementing eframe::App.
pub struct MaelstromIdeApp {
    config: IdeWorkspaceConfig,
    pub tree: Tree<IdePane>,
}

impl MaelstromIdeApp {
    /// Create a new instance with default configuration and UI layout.
    pub fn new() -> Self {
        let config = IdeWorkspaceConfig::default();
        // Build a simple tabbed layout containing the five panes.
        let panes = vec![
            IdePane::GameSwitcher,
            IdePane::DuelCircle,
            IdePane::SpellbookLibrary,
            IdePane::EnchantmentInspector,
            IdePane::ArbiterTelemetry,
        ];
        let tree = Tree::new_tabs(egui::Id::new("ide_tree"), panes);
        Self { config, tree }
    }

    /// Apply the Spiral Spellbook visual theme.
    pub fn apply_theme(ctx: &Context) {
        let mut visuals = Visuals::dark();
        visuals.window_fill = Color32::from_rgb(0x1A, 0x16, 0x2B);
        ctx.set_visuals(visuals);
    }
}

impl App for MaelstromIdeApp {
    fn update(&mut self, ctx: &Context, _frame: &mut eframe::Frame) {
        Self::apply_theme(ctx);
        TopBottomPanel::top("top_bar").show(ctx, |ui| {
            ui.label(format!("[Maelstrom IDE] Profile: {}", self.config.profile));
        });
        CentralPanel::default().show(ctx, |ui| {
            let mut behavior = IdeBehavior;
            self.tree.ui(&mut behavior, ui);
        });
    }
}

/// Initialize the IDE workspace and return the configuration.
pub fn initialize_workspace() -> IdeWorkspaceConfig {
    println!("[Maelstrom-IDE] Initializing Arcane Spellbook IDE workspace...");
    IdeWorkspaceConfig::default()
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn test_ide_workspace_initialization() {
        let config = initialize_workspace();
        assert_eq!(config.theme, "spiral_spellbook");
        assert_eq!(config.profile, "Wizard101.toml");
    }

    #[test]
    fn test_ide_app_construction() {
        let app = MaelstromIdeApp::new();
        // Count leaf tiles via the public `tiles` field iterator.
        let leaf_count = app
            .tree
            .tiles
            .iter()
            .filter(|(_, tile)| matches!(tile, egui_tiles::Tile::Pane(_)))
            .count();
        assert_eq!(leaf_count, 5);
        assert_eq!(app.config.theme, "spiral_spellbook");
    }
}
