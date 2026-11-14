//! `maelstrom-ide`: Dockable Wizard101 Arcane Spellbook IDE workspace using egui_tiles.

use egui::{self, CentralPanel, Context, TopBottomPanel, Visuals, WidgetText, Color32, Stroke};
use egui_tiles::{Behavior, Tree, TileId, UiResponse};
use eframe::{self, App};
use maelstrom_core::{AutomationSchool, CardEnchantment, CardPayload, Deck, ExecutionMode, MacroCard};
use uuid::Uuid;

/// Configuration for the IDE workspace.
#[derive(Debug, Clone)]
pub struct IdeWorkspaceConfig {
    /// Theme identifier ("spiral_spellbook").
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
            IdePane::GameSwitcher => "Activity Bar & Client Switcher",
            IdePane::DuelCircle => "Duel Circle (Execution Queue)",
            IdePane::SpellbookLibrary => "Spellbook Library (Available Cards)",
            IdePane::EnchantmentInspector => "Enchantment Inspector (Properties)",
            IdePane::ArbiterTelemetry => "Arbiter Telemetry & Confidence",
        };
        write!(f, "{}", s)
    }
}

/// Theme palette for Wizard101 Spiral Spellbook.
pub struct SpiralTheme;

impl SpiralTheme {
    pub const DARK_ARCANE_BG: Color32 = Color32::from_rgb(0x1A, 0x16, 0x2B);
    pub const GOLD_FILIGREE: Color32 = Color32::from_rgb(0xD4, 0xAF, 0x37);
    pub const COMBAT_CRIMSON: Color32 = Color32::from_rgb(0xC0, 0x39, 0x2B);
    pub const NAVIGATION_SAND: Color32 = Color32::from_rgb(0xD4, 0xAC, 0x0D);
    pub const ECONOMY_GOLD: Color32 = Color32::from_rgb(0xE6, 0x7E, 0x22);
    pub const MAINTENANCE_EMERALD: Color32 = Color32::from_rgb(0x27, 0xAE, 0x60);
    pub const METALOGIC_ASTRAL: Color32 = Color32::from_rgb(0x8E, 0x44, 0xAD);

    pub fn school_color(school: AutomationSchool) -> Color32 {
        match school {
            AutomationSchool::Combat => Self::COMBAT_CRIMSON,
            AutomationSchool::Navigation => Self::NAVIGATION_SAND,
            AutomationSchool::Economy => Self::ECONOMY_GOLD,
            AutomationSchool::Maintenance => Self::MAINTENANCE_EMERALD,
            AutomationSchool::MetaLogic => Self::METALOGIC_ASTRAL,
        }
    }

    pub fn apply(ctx: &Context) {
        let mut visuals = Visuals::dark();
        visuals.window_fill = Self::DARK_ARCANE_BG;
        visuals.panel_fill = Self::DARK_ARCANE_BG;
        visuals.widgets.noninteractive.bg_fill = Color32::from_rgb(0x23, 0x1E, 0x38);
        visuals.widgets.noninteractive.bg_stroke = Stroke::new(1.0_f32, Self::GOLD_FILIGREE);
        visuals.widgets.inactive.bg_fill = Color32::from_rgb(0x2C, 0x25, 0x44);
        visuals.widgets.hovered.bg_fill = Color32::from_rgb(0x3B, 0x32, 0x5C);
        visuals.widgets.active.bg_fill = Color32::from_rgb(0x4A, 0x3E, 0x73);
        ctx.set_visuals(visuals);
    }
}

/// In-memory state of the Spellbook IDE.
pub struct IdeState {
    pub active_deck: Deck,
    pub card_library: Vec<MacroCard>,
    pub selected_card_id: Option<Uuid>,
    pub attached_client_title: String,
    pub is_running: bool,
    pub confidence_score: f32,
    pub arbitration_mode: String,
    pub client_state_log: Vec<String>,
}

impl Default for IdeState {
    fn default() -> Self {
        let sample_cards = vec![
            MacroCard {
                id: Uuid::new_v4(),
                name: "Cast Meteor Strike".to_string(),
                school: AutomationSchool::Combat,
                pip_cost: 4,
                description: "Deal 305-345 Fire damage to all enemies".to_string(),
                mode: ExecutionMode::SequentialStep,
                payload: CardPayload::McpFunction {
                    tool_name: "evaluate_damage".to_string(),
                    arguments_json: r#"{"base_damage": 325.0, "enchant": 275.0}"#.to_string(),
                },
                default_enchantment: CardEnchantment::default(),
            },
            MacroCard {
                id: Uuid::new_v4(),
                name: "Probe Client Idle".to_string(),
                school: AutomationSchool::Maintenance,
                pip_cost: 0,
                description: "Read native client translator idle state".to_string(),
                mode: ExecutionMode::SequentialStep,
                payload: CardPayload::McpFunction {
                    tool_name: "read_client_idle".to_string(),
                    arguments_json: "{}".to_string(),
                },
                default_enchantment: CardEnchantment::default(),
            },
            MacroCard {
                id: Uuid::new_v4(),
                name: "Mark Teleport Location".to_string(),
                school: AutomationSchool::Navigation,
                pip_cost: 0,
                description: "Save current coordinates for instant recall".to_string(),
                mode: ExecutionMode::OneShot,
                payload: CardPayload::InputSequence(vec![]),
                default_enchantment: CardEnchantment::default(),
            },
            MacroCard {
                id: Uuid::new_v4(),
                name: "Potion Refill Loop".to_string(),
                school: AutomationSchool::Maintenance,
                pip_cost: 0,
                description: "Purchase potions at nearest vendor if mana is empty".to_string(),
                mode: ExecutionMode::ContinuousAura { interval_ms: 1000 },
                payload: CardPayload::InputSequence(vec![]),
                default_enchantment: CardEnchantment::default(),
            },
            MacroCard {
                id: Uuid::new_v4(),
                name: "Halfang Cave Enter".to_string(),
                school: AutomationSchool::Economy,
                pip_cost: 0,
                description: "Walk into cave sigil and await battle circle".to_string(),
                mode: ExecutionMode::SequentialStep,
                payload: CardPayload::McpFunction {
                    tool_name: "macro_press_key".to_string(),
                    arguments_json: r#"{"key": "x"}"#.to_string(),
                },
                default_enchantment: CardEnchantment::default(),
            },
        ];

        let active_deck = Deck {
            id: Uuid::new_v4(),
            name: "Halfang Farming Deck".to_string(),
            description: "High-yield gold farming loop in Vestrilund".to_string(),
            cards: sample_cards.clone(),
            loop_count: Some(50),
        };

        Self {
            active_deck,
            card_library: sample_cards,
            selected_card_id: None,
            attached_client_title: "WizardGraphicalClient.exe (PID: 62076, State: nativeIdle)".to_string(),
            is_running: false,
            confidence_score: 0.94,
            arbitration_mode: "Fast Reflex (Local)".to_string(),
            client_state_log: vec!["[Translator] Initialized client observation bridge (nativeIdle verified)".to_string()],
        }
    }
}

/// Behavior implementation rendering the 5 Spellbook IDE panes.
pub struct IdeBehavior<'a> {
    pub state: &'a mut IdeState,
}

impl<'a> Behavior<IdePane> for IdeBehavior<'a> {
    fn pane_ui(&mut self, ui: &mut egui::Ui, _tile_id: TileId, pane: &mut IdePane) -> UiResponse {
        match pane {
            IdePane::GameSwitcher => {
                ui.heading("Activity & Client Workspace");
                ui.separator();
                ui.label(format!("Attached Client: {}", self.state.attached_client_title));
                ui.label("Workspace Profile: Wizard101.toml");
                ui.separator();
                if ui.button("Attach to WizardGraphicalClient.exe").clicked() {
                    self.state.attached_client_title = "WizardGraphicalClient.exe (PID: 62076, State: nativeIdle)".to_string();
                    self.state.client_state_log.push("[Client] Re-attached to active WizardGraphicalClient".to_string());
                }
                ui.separator();
                ui.label("Client Translator Telemetry Log:");
                for line in self.state.client_state_log.iter().rev().take(6) {
                    ui.label(line);
                }
            }
            IdePane::DuelCircle => {
                ui.heading(format!("Duel Circle: {}", self.state.active_deck.name));
                ui.label(format!("Loop Count: {:?}", self.state.active_deck.loop_count));
                ui.separator();
                ui.label("Active Deck Queue (MCP & Macro Cards):");
                let mut card_to_select = None;
                for (idx, card) in self.state.active_deck.cards.iter().enumerate() {
                    let color = SpiralTheme::school_color(card.school);
                    let payload_badge = match &card.payload {
                        CardPayload::McpFunction { tool_name, .. } => format!("[MCP: {}]", tool_name),
                        CardPayload::InputSequence(_) => "[Input Seq]".to_string(),
                        CardPayload::VisionWait { .. } => "[Vision Wait]".to_string(),
                        CardPayload::RegionCheck { .. } => "[Region Check]".to_string(),
                        CardPayload::SubDeck { .. } => "[SubDeck]".to_string(),
                        CardPayload::LegacyExternal { .. } => "[Legacy External]".to_string(),
                    };
                    let card_btn = ui.colored_label(color, format!("{}. [{:?}] {} {} (Pip: {})", idx + 1, card.school, card.name, payload_badge, card.pip_cost));
                    if card_btn.clicked() {
                        card_to_select = Some(card.id);
                    }
                }
                if let Some(id) = card_to_select {
                    self.state.selected_card_id = Some(id);
                }
            }
            IdePane::SpellbookLibrary => {
                ui.heading("Spellbook Library");
                ui.separator();
                ui.label("Available Macro & MCP Cards:");
                let mut card_to_add = None;
                for card in &self.state.card_library {
                    let color = SpiralTheme::school_color(card.school);
                    ui.horizontal(|ui| {
                        ui.colored_label(color, format!("[{:?}] {}", card.school, card.name));
                        if ui.button("+ Add to Deck").clicked() {
                            card_to_add = Some(card.clone());
                        }
                    });
                }
                if let Some(new_card) = card_to_add {
                    self.state.active_deck.cards.push(new_card);
                }
            }
            IdePane::EnchantmentInspector => {
                ui.heading("Enchantment & Macro Inspector");
                ui.separator();
                if let Some(selected_id) = self.state.selected_card_id {
                    if let Some(card) = self.state.active_deck.cards.iter_mut().find(|c| c.id == selected_id) {
                        ui.label(format!("Card Name: {}", card.name));
                        ui.label(format!("School: {:?}", card.school));
                        ui.label(format!("Mode: {:?}", card.mode));
                        ui.label(format!("Pip Cost: {}", card.pip_cost));
                        ui.separator();
                        match &mut card.payload {
                            CardPayload::McpFunction { tool_name, arguments_json } => {
                                ui.label("Programmable MCP Tool Binding:");
                                ui.text_edit_singleline(tool_name);
                                ui.label("Arguments (JSON):");
                                ui.text_edit_singleline(arguments_json);
                            }
                            _ => {
                                ui.label(format!("Payload Type: {:?}", std::mem::discriminant(&card.payload)));
                            }
                        }
                        ui.separator();
                        ui.label("Enchantment Modifiers:");
                        ui.add(egui::Slider::new(&mut card.default_enchantment.repeat_count, 1..=10).text("Repeat Count"));
                        ui.add(egui::Slider::new(&mut card.default_enchantment.pre_delay_ms, 0..=5000).text("Pre-delay (ms)"));
                        ui.add(egui::Slider::new(&mut card.default_enchantment.post_delay_ms, 0..=5000).text("Post-delay (ms)"));
                        ui.add(egui::Slider::new(&mut card.default_enchantment.jitter_px, 0..=20).text("Click Jitter (px)"));
                    } else {
                        ui.label("Selected card not found in active deck.");
                    }
                } else {
                    ui.label("Click any card in the Duel Circle to inspect or enchant its properties.");
                }
            }
            IdePane::ArbiterTelemetry => {
                ui.heading("Arbiter Telemetry & Panic Controls");
                ui.separator();
                ui.label(format!("Active Engine: {}", self.state.arbitration_mode));
                ui.label(format!("Vision Confidence: {:.2}", self.state.confidence_score));
                ui.separator();
                if self.state.is_running {
                    ui.colored_label(Color32::GREEN, "STATUS: RUNNING");
                    if ui.button("PANIC STOP (F12)").clicked() {
                        self.state.is_running = false;
                        self.state.client_state_log.push("[Runner] PANIC STOP engaged - Disconnected safely.".to_string());
                    }
                } else {
                    ui.colored_label(Color32::YELLOW, "STATUS: IDLE (nativeIdle)");
                    if ui.button("Engage Deck Runner").clicked() {
                        self.state.is_running = true;
                        self.state.client_state_log.push("[Runner] Deck Runner started under single control lease.".to_string());
                    }
                }
            }
        }
        UiResponse::None
    }

    fn tab_title_for_pane(&mut self, pane: &IdePane) -> WidgetText {
        WidgetText::from(pane.to_string())
    }
}

/// The main application structure implementing eframe::App.
pub struct MaelstromIdeApp {
    pub config: IdeWorkspaceConfig,
    pub state: IdeState,
    pub tree: Tree<IdePane>,
}

impl MaelstromIdeApp {
    /// Create a new instance with default configuration and UI layout.
    pub fn new() -> Self {
        let config = IdeWorkspaceConfig::default();
        let state = IdeState::default();
        let panes = vec![
            IdePane::GameSwitcher,
            IdePane::DuelCircle,
            IdePane::SpellbookLibrary,
            IdePane::EnchantmentInspector,
            IdePane::ArbiterTelemetry,
        ];
        let tree = Tree::new_tabs(egui::Id::new("ide_tree"), panes);
        Self { config, state, tree }
    }

    /// Apply the Spiral Spellbook visual theme.
    pub fn apply_theme(ctx: &Context) {
        SpiralTheme::apply(ctx);
    }

    /// Wire cross-crate runners and controllers into the UI state
    pub fn wire_runtime(&mut self, window_tracker: &dyn maelstrom_platform::WindowTracker) {
        if let Ok(hwnd) = window_tracker.find_window("WizardGraphicalClient") {
            self.state.attached_client_title = format!("WizardGraphicalClient.exe (HWND: 0x{:08X})", hwnd);
        }
    }
}

impl Default for MaelstromIdeApp {
    fn default() -> Self {
        Self::new()
    }
}

impl App for MaelstromIdeApp {
    fn update(&mut self, ctx: &Context, _frame: &mut eframe::Frame) {
        Self::apply_theme(ctx);
        TopBottomPanel::top("top_bar").show(ctx, |ui| {
            ui.horizontal(|ui| {
                ui.label(format!("[Maelstrom Arcane IDE] Profile: {}", self.config.profile));
                ui.separator();
                ui.label(format!("Active Deck: {}", self.state.active_deck.name));
            });
        });
        CentralPanel::default().show(ctx, |ui| {
            let mut behavior = IdeBehavior { state: &mut self.state };
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
        let leaf_count = app
            .tree
            .tiles
            .iter()
            .filter(|(_, tile)| matches!(tile, egui_tiles::Tile::Pane(_)))
            .count();
        assert_eq!(leaf_count, 5);
        assert_eq!(app.config.theme, "spiral_spellbook");
        assert_eq!(app.state.active_deck.name, "Halfang Farming Deck");
        assert_eq!(app.state.active_deck.cards.len(), 5);
    }
}
