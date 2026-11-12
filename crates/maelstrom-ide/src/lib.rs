//! `maelstrom-ide`: Dockable card IDE workspace scaffold.

pub struct IdeWorkspaceConfig {
    pub theme: String,
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
}
