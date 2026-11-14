#![windows_subsystem = "windows"]
//! Optional Rust preview; native Maelstrom remains the application and input owner.
fn main() -> eframe::Result<()> {
    eframe::run_native(
        "Maelstrom · Rust preview (read-only host observer)",
        eframe::NativeOptions::default(),
        Box::new(|_| Ok(Box::new(maelstrom_ide::MaelstromIdeApp::new()))),
    )
}
