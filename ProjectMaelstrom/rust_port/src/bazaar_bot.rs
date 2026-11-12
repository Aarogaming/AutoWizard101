use std::fs;
use std::path::{Path, PathBuf};
use std::thread;
use std::time::{Duration, Instant};
use crate::player_controller::PlayerController;
use crate::combat_geometry::Point;

/// Very simple image‑finder stub. In the real system this would search a screenshot for a template.
/// Here we just pretend the image is always at (200, 150) if the file exists.
fn retrieve_target_image_position(image_path: &Path) -> Option<Point> {
    if image_path.exists() {
        // Dummy coordinates – replace with real vision logic later.
        Some(Point::new(200, 150))
    } else {
        None
    }
}

/// Represents the Bazaar Reagent automation.
pub struct BazaarReagentBot {
    player: PlayerController,
    /// Base directory where PNG assets live.
    base_dir: PathBuf,
}

impl BazaarReagentBot {
    pub fn new() -> Self {
        let app_path = std::env::current_dir().unwrap_or_else(|_| PathBuf::from("."));
        let base_dir = app_path.join("Bazaar");
        Self { player: PlayerController::new(), base_dir }
    }

    /// Load reagent names (PNG filenames without extension) from the Reagents folder.
    pub fn load_reagents(&self) -> Vec<String> {
        let reag_path = self.base_dir.join("Reagents");
        if !reag_path.exists() {
            eprintln!("[Bazaar] Reagents directory not found: {:?}", reag_path);
            return Vec::new();
        }
        fs::read_dir(reag_path)
            .into_iter()
            .flatten()
            .filter_map(|e| e.ok())
            .filter_map(|entry| {
                let path = entry.path();
                if path.extension().and_then(|s| s.to_str()) == Some("png") {
                    path.file_stem().and_then(|s| s.to_str()).map(|s| s.to_owned())
                } else {
                    None
                }
            })
            .collect()
    }

    /// Main loop – called repeatedly by a timer.
    pub fn run_once(&self, selected: &[String]) {
        // Refresh the shop UI.
        self.refresh_shop();

        for name in selected {
            self.get_reagent_location(name);
        }
    }

    fn refresh_shop(&self) {
        let img = self.base_dir.join("refreshBtn.png");
        if let Some(pt) = retrieve_target_image_position(&img) {
            self.player.click(pt);
        } else {
            eprintln!("[Bazaar] Refresh button not found");
        }
    }

    fn get_reagent_location(&self, name: &str) {
        let img = self.base_dir.join("Reagents").join(format!("{}.png", name));
        if let Some(pt) = retrieve_target_image_position(&img) {
            self.player.click(pt);
            self.buy_reagent();
        } else {
            eprintln!("[Bazaar] Reagent '{name}' not found");
        }
    }

    fn buy_reagent(&self) {
        // Click "Buy More" button.
        let buy_more = self.base_dir.join("buyMoreBtn.png");
        if let Some(pt) = retrieve_target_image_position(&buy_more) {
            self.player.click(pt);
            // Click on count field and type a number (hard‑coded 999 for demo).
            if let Some(count_pt) = self.wait_for_image("buyCount.png", Duration::from_secs(10)) {
                // offset by +10 as original code.
                let mut adjusted = count_pt;
                adjusted.x += 10;
                self.player.click(adjusted);
                self.player.press_number9();
                self.player.press_number9();
                self.player.press_number9();
            }
            // Click the final "Buy" button.
            if let Some(buy_pt) = self.wait_for_image("buyBtn.png", Duration::from_secs(10)) {
                self.player.click(buy_pt);
            }
            // Click OK.
            if let Some(ok_pt) = self.wait_for_image("okBtn.png", Duration::from_secs(10)) {
                self.player.click(ok_pt);
            }
        } else {
            eprintln!("[Bazaar] Buy More button not found");
        }
    }

    /// Simple polling helper – returns the point once the image appears or after timeout.
    fn wait_for_image(&self, file_name: &str, timeout: Duration) -> Option<Point> {
        let img_path = self.base_dir.join(file_name);
        let start = Instant::now();
        let mut location = retrieve_target_image_position(&img_path);
        while location.is_none() && start.elapsed() < timeout {
            thread::sleep(Duration::from_millis(200));
            location = retrieve_target_image_position(&img_path);
        }
        location
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_bot_flow_no_files() {
        let bot = BazaarReagentBot::new();
        // With no actual PNG assets the stub will return None and just log errors, but must not panic.
        bot.run_once(&["dummy".to_string()]);
    }
}
