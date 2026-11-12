//! Rust port of the Wizard101 client‑operations calibration, combat geometry, deck encoding, and telemetry bridge.
//! This module mirrors the C# logic but lives completely separate from the original codebase.
//! It can be compiled, unit‑tested, or later linked to Rust‑based automation components.

pub mod combat_geometry;
pub mod deck_encoder;
pub mod pet_dance;
pub mod wad_reader;
pub mod player_controller;
pub mod bazaar_bot;

use chrono::{DateTime, Utc};

/// Status of an individual calibration check.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CalibrationStatus {
    Passed,
    Warning,
    Failed,
    NotApplicable,
}

/// Result of a single calibration check.
#[derive(Debug, Clone)]
pub struct CalibrationCheckResult {
    pub name: String,
    pub status: CalibrationStatus,
    pub details: String,
    pub confidence: f64,
}

impl CalibrationCheckResult {
    pub fn new<N: Into<String>, D: Into<String>>(name: N, status: CalibrationStatus, details: D) -> Self {
        Self {
            name: name.into(),
            status,
            details: details.into(),
            confidence: 1.0,
        }
    }
}

/// Full report produced by the calibration routine.
#[derive(Debug, Clone)]
pub struct ClientCalibrationReport {
    pub is_ready_for_operations: bool,
    pub client_size: (u32, u32), // (width, height)
    pub aspect_ratio: f64,
    pub aspect_ratio_name: String,
    pub scale_factor: f64,
    pub checks: Vec<CalibrationCheckResult>,
    pub timestamp_utc: DateTime<Utc>,
}

/// Baseline viewport sizes (mirroring the C# constants).
pub const BASELINE_4X3: (u32, u32) = (800, 600);
pub const BASELINE_4X3_ALT: (u32, u32) = (1024, 768);
pub const BASELINE_16X9: (u32, u32) = (1280, 720);
pub const BASELINE_16X9_ALT: (u32, u32) = (1920, 1080);

/// Simple placeholder for the telemetry data used by the bridge.
/// In the real project this would be populated from memory observation or a mock provider.
#[derive(Debug, Clone)]
pub struct W101TelemetryData {
    pub pips: Option<u8>,
    pub power_pips: Option<u8>,
    pub shadow_pips: Option<u8>,
    pub current_health: Option<u32>,
    pub max_health: Option<u32>,
    pub is_valid: bool,
    pub has_position: bool,
    pub x: f64,
    pub y: f64,
    pub z: f64,
    pub in_combat: bool,
    pub zone_id: Option<String>,
}

impl W101TelemetryData {
    pub fn dummy() -> Self {
        Self {
            pips: Some(2),
            power_pips: Some(0),
            shadow_pips: Some(0),
            current_health: Some(1200),
            max_health: Some(1500),
            is_valid: true,
            has_position: true,
            x: 100.0,
            y: 200.0,
            z: 0.0,
            in_combat: false,
            zone_id: Some("Azmar".into()),
        }
    }
}

/// Core calibration routine (mirrors `ClientOperationsCalibrator.CalibrateWindowGeometry`).
pub fn calibrate_window_geometry(
    client_size: (u32, u32),
    telemetry: Option<&W101TelemetryData>,
) -> ClientCalibrationReport {
    let (width, height) = client_size;
    let mut checks: Vec<CalibrationCheckResult> = Vec::new();

    // -------- 1️⃣ Minimum dimensions ----------
    if width < 640 || height < 480 {
        checks.push(CalibrationCheckResult::new(
            "Window Dimensions",
            CalibrationStatus::Failed,
            format!(
                "Client size {}x{} is below minimum supported 640x480.",
                width, height
            ),
        ));
    } else {
        checks.push(CalibrationCheckResult::new(
            "Window Dimensions",
            CalibrationStatus::Passed,
            format!(
                "Client size {}x{} meets minimum requirements.",
                width, height
            ),
        ));
    }

    // -------- 2️⃣ Aspect‑ratio detection ----------
    let aspect = width as f64 / height.max(1) as f64;
    let (aspect_name, scale_factor, aspect_status) = if (aspect - (4.0 / 3.0)).abs() <= 0.05 {
        (
            "4:3 Standard",
            width as f64 / 800.0,
            CalibrationStatus::Passed,
        )
    } else if (aspect - (16.0 / 9.0)).abs() <= 0.05 {
        (
            "16:9 Widescreen",
            width as f64 / 1280.0,
            CalibrationStatus::Passed,
        )
    } else if (aspect - (16.0 / 10.0)).abs() <= 0.05 {
        (
            "16:10 Display",
            width as f64 / 1280.0,
            CalibrationStatus::Warning,
        )
    } else {
        (
            "Non‑standard",
            width as f64 / 800.0,
            CalibrationStatus::Warning,
        )
    };
    checks.push(CalibrationCheckResult::new(
        "Aspect Ratio",
        aspect_status,
        format!(
            "{} {} (Ratio: {:.2}, Scale: {:.2}x).",
            if matches!(aspect_status, CalibrationStatus::Passed) {
                "Recognized"
            } else {
                "Detected"
            },
            aspect_name,
            aspect,
            scale_factor
        ),
    ));

    // -------- 3️⃣ Combat tray geometry ----------
    // In the C# version this uses `CombatGeometry.SpellArea`. Here we use the same constant bounds.
    let spell_area_right = 615; // X+W from the baseline UI
    let spell_area_bottom = 360; // Y+H from the baseline UI
    let tray_inside = spell_area_right <= width && spell_area_bottom <= height;
    if tray_inside {
        checks.push(CalibrationCheckResult::new(
            "Combat Tray Anchoring",
            CalibrationStatus::Passed,
            format!(
                "Combat spell area (right:{}, bottom:{}) fits inside client bounds.",
                spell_area_right, spell_area_bottom
            ),
        ));
    } else {
        checks.push(CalibrationCheckResult::new(
            "Combat Tray Anchoring",
            CalibrationStatus::Failed,
            format!(
                "Combat spell area exceeds client dimensions {}x{}.",
                width, height
            ),
        ));
    }

    // -------- 4️⃣ Pass / Flee button coordinates ----------
    // Stub coordinates – real values live in `CombatGeometry`.
    let pass_button = (100u32, 500u32);
    let flee_button = (700u32, 500u32);
    let buttons_inside = [
        pass_button,
        flee_button,
    ]
    .iter()
    .all(|(x, y)| *x < width && *y < height);

    if buttons_inside {
        checks.push(CalibrationCheckResult::new(
            "Combat Navigation Anchors",
            CalibrationStatus::Passed,
            "Pass and Flee button coordinates resolve within active client frame.".to_string(),
        ));
    } else {
        checks.push(CalibrationCheckResult::new(
            "Combat Navigation Anchors",
            CalibrationStatus::Failed,
            "Pass or Flee buttons fall outside active client window.".to_string(),
        ));
    }

    // -------- 5️⃣ Telemetry validation ----------
    match telemetry {
        Some(t) if t.is_valid => {
            let pos_info = if t.has_position {
                format!("Position=({:.1}, {:.1}, {:.1})", t.x, t.y, t.z)
            } else {
                "Position=None".to_string()
            };
            let combat_info = if t.in_combat {
                "InCombat=True"
            } else {
                "InCombat=False"
            };
            let zone = t.zone_id.as_deref().unwrap_or("Unknown");
            checks.push(CalibrationCheckResult::new(
                "Telemetry Health",
                CalibrationStatus::Passed,
                format!(
                    "Telemetry valid ({}, {}, Zone={}).",
                    pos_info, combat_info, zone
                ),
            ));
        }
        Some(_) => {
            checks.push(CalibrationCheckResult::new(
                "Telemetry Health",
                CalibrationStatus::Warning,
                "Telemetry data present but failed validation rules.".to_string(),
            ));
        }
        None => {
            checks.push(CalibrationCheckResult::new(
                "Telemetry Health",
                CalibrationStatus::NotApplicable,
                "No live telemetry supplied; operating in UI/Visual mode.".to_string(),
            ));
        }
    }

    let is_ready = checks.iter().all(|c| c.status != CalibrationStatus::Failed);

    ClientCalibrationReport {
        is_ready_for_operations: is_ready,
        client_size,
        aspect_ratio: aspect,
        aspect_ratio_name: aspect_name.to_string(),
        scale_factor,
        checks,
        timestamp_utc: Utc::now(),
    }
}

/// Translate a baseline point (800×600) into the actual client coordinates.
pub fn translate_baseline_point(
    baseline: (i32, i32),
    actual: (u32, u32),
    reference: Option<(u32, u32)>,
) -> (i32, i32) {
    let (ref_w, ref_h) = reference.unwrap_or(BASELINE_4X3);
    let scale_x = actual.0 as f64 / ref_w.max(1) as f64;
    let scale_y = actual.1 as f64 / ref_h.max(1) as f64;
    (
        (baseline.0 as f64 * scale_x).round() as i32,
        (baseline.1 as f64 * scale_y).round() as i32,
    )
}

/// Translate a normalized rectangle (0.0‑1.0) into client pixel coordinates.
pub fn translate_normalized_rect(
    normalized: (f64, f64, f64, f64),
    actual: (u32, u32),
) -> (i32, i32, i32, i32) {
    let (x, y, w, h) = normalized;
    let (aw, ah) = (actual.0 as f64, actual.1 as f64);
    (
        (x * aw).round() as i32,
        (y * ah).round() as i32,
        (w * aw).round() as i32,
        (h * ah).round() as i32,
    )
}

// ----------------------------------------------------------------------
// Telemetry bridge (mirroring the C# W101TelemetryBridge)
// ----------------------------------------------------------------------
#[derive(Debug, Clone)]
pub struct CombatantState {
    pub name: String,
    pub school: String,
    pub current_hp: i32,
    pub max_hp: i32,
    pub normal_pips: i32,
    pub power_pips: i32,
    pub shadow_pips: i32,
    pub is_boss: bool,
    pub is_client: bool,
    pub battle_slot: i32,
    pub gear_damage_bonus: f64,
    pub resist_percent: f64,
    pub school_pips: i32,
    pub pierce_percent: f64,
    pub critical_rating: f64,
}

impl CombatantState {
    fn new_player(
        telemetry: &W101TelemetryData,
        name: &str,
        school: &str,
        damage_bonus: f64,
        resist: f64,
        pierce: f64,
        crit: f64,
    ) -> Self {
        Self {
            name: name.into(),
            school: school.into(),
            current_hp: telemetry.current_health.unwrap_or(1000) as i32,
            max_hp: telemetry.max_health.unwrap_or(1000) as i32,
            normal_pips: telemetry.pips.unwrap_or(0) as i32,
            power_pips: telemetry.power_pips.unwrap_or(0) as i32,
            shadow_pips: telemetry.shadow_pips.unwrap_or(0) as i32,
            is_boss: false,
            is_client: true,
            battle_slot: 4, // Player slot per UI layout
            gear_damage_bonus: damage_bonus,
            resist_percent: resist,
            school_pips: 0,
            pierce_percent: pierce,
            critical_rating: crit,
        }
    }

    fn new_enemy(
        name: &str,
        battle_slot: i32,
        current_hp: i32,
        max_hp: i32,
        school: &str,
        resist: f64,
        is_boss: bool,
    ) -> Self {
        Self {
            name: name.into(),
            school: school.into(),
            current_hp,
            max_hp,
            normal_pips: 2,
            power_pips: 0,
            shadow_pips: 0,
            is_boss,
            is_client: false,
            battle_slot,
            gear_damage_bonus: 0.0,
            resist_percent: resist,
            school_pips: 0,
            pierce_percent: 0.0,
            critical_rating: 0.0,
        }
    }
}

#[derive(Debug, Clone)]
pub struct CombatCardState {
    pub card_id: String,
    pub power: i32,
    pub school: String,
}

/// Result placeholder for a simulated turn.
#[derive(Debug, Clone)]
pub struct SimulationTurnResult {
    pub chosen_action: String,
    pub description: String,
}

/// Build the player combatant from telemetry (mirrors C# `BuildPlayerCombatant`).
pub fn build_player_combatant(
    telemetry: &W101TelemetryData,
    name: Option<&str>,
    school: Option<&str>,
    damage_bonus: Option<f64>,
    resist_percent: Option<f64>,
    pierce_percent: Option<f64>,
    critical_rating: Option<f64>,
) -> CombatantState {
    CombatantState::new_player(
        telemetry,
        name.unwrap_or("Player"),
        school.unwrap_or("Storm"),
        damage_bonus.unwrap_or(0.5),
        resist_percent.unwrap_or(0.2),
        pierce_percent.unwrap_or(15.0),
        critical_rating.unwrap_or(300.0),
    )
}

/// Build a generic enemy combatant (mirrors C# `BuildEnemyCombatant`).
pub fn build_enemy_combatant(
    name: &str,
    battle_slot: i32,
    current_hp: i32,
    max_hp: i32,
    school: Option<&str>,
    resist_percent: Option<f64>,
    is_boss: Option<bool>,
) -> CombatantState {
    CombatantState::new_enemy(
        name,
        battle_slot,
        current_hp,
        max_hp,
        school.unwrap_or("Myth"),
        resist_percent.unwrap_or(0.10),
        is_boss.unwrap_or(false),
    )
}

/// Stub for the MCTS turn planner (mirrors C# `PlanTurnFromState`).
pub fn plan_turn_from_state(
    player: &CombatantState,
    _enemies: &[CombatantState],
    hand_cards: &[CombatCardState],
) -> SimulationTurnResult {
    // Simple placeholder logic:
    // * If there are no hand cards → Pass.
    // * Otherwise pick the first card’s ID.
    let chosen_action = if hand_cards.is_empty() {
        "Pass".to_string()
    } else {
        hand_cards[0].card_id.clone()
    };
    SimulationTurnResult {
        chosen_action,
        description: format!("Planned turn for player {}", player.name),
    }
}

// ----------------------------------------------------------------------
// Tests – compile‑time sanity checks (run with `cargo test`)
// ----------------------------------------------------------------------
#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn basic_calibration_800x600() {
        let report = calibrate_window_geometry((800, 600), None);
        assert!(report.is_ready_for_operations);
        assert_eq!(report.aspect_ratio_name, "4:3 Standard");
    }

    #[test]
    fn telemetry_happy_path() {
        let tele = W101TelemetryData::dummy();
        let report = calibrate_window_geometry((1280, 720), Some(&tele));
        assert!(report.is_ready_for_operations);
        let health_check = report
            .checks
            .iter()
            .find(|c| c.name == "Telemetry Health")
            .unwrap();
        assert_eq!(health_check.status, CalibrationStatus::Passed);
    }

    #[test]
    fn bridge_player_creation() {
        let tele = W101TelemetryData::dummy();
        let player = build_player_combatant(&tele, None, None, None, None, None, None);
        assert_eq!(player.battle_slot, 4);
        assert!(player.is_client);
    }

    #[test]
    fn plan_turn_stub() {
        let player = CombatantState {
            name: "Player".into(),
            school: "Storm".into(),
            current_hp: 1000,
            max_hp: 1500,
            normal_pips: 2,
            power_pips: 0,
            shadow_pips: 0,
            is_boss: false,
            is_client: true,
            battle_slot: 4,
            gear_damage_bonus: 0.5,
            resist_percent: 0.2,
            school_pips: 0,
            pierce_percent: 15.0,
            critical_rating: 300.0,
        };
        let cards = vec![CombatCardState {
            card_id: "C001".into(),
            power: 10,
            school: "Storm".into(),
        }];
        let result = plan_turn_from_state(&player, &[], &cards);
        assert_eq!(result.chosen_action, "C001");
    }
}
