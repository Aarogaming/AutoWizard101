//! Window calibration ported from rust_port / ClientOperationsCalibrator.cs.

use chrono::{DateTime, Utc};

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CalibrationStatus {
    Passed,
    Warning,
    Failed,
    NotApplicable,
}

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

#[derive(Debug, Clone)]
pub struct ClientCalibrationReport {
    pub is_ready_for_operations: bool,
    pub client_size: (u32, u32),
    pub checks: Vec<CalibrationCheckResult>,
    pub timestamp_utc: DateTime<Utc>,
}

pub fn calibrate_window_geometry(
    width: u32,
    height: u32,
) -> ClientCalibrationReport {
    let mut checks = Vec::new();
    let mut ready = true;

    // Minimum dimensions check (800x600)
    let min_size_ok = width >= 800 && height >= 600;
    checks.push(CalibrationCheckResult::new(
        "Window Resolution Minimum",
        if min_size_ok {
            CalibrationStatus::Passed
        } else {
            ready = false;
            CalibrationStatus::Failed
        },
        format!(
            "Resolution {}x{} meets 800x600 minimum baseline: {}",
            width, height, min_size_ok
        ),
    ));

    // Aspect ratio check
    let aspect = width as f64 / height as f64;
    let (aspect_name, scale_factor, aspect_status) = if (aspect - (4.0 / 3.0)).abs() <= 0.05 {
        ("Standard 4:3", width as f64 / 800.0, CalibrationStatus::Passed)
    } else if (aspect - (16.0 / 9.0)).abs() <= 0.05 {
        ("Widescreen 16:9", width as f64 / 1280.0, CalibrationStatus::Passed)
    } else if (aspect - (16.0 / 10.0)).abs() <= 0.05 {
        ("Widescreen 16:10", width as f64 / 1280.0, CalibrationStatus::Passed)
    } else {
        (
            "Non-standard",
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

    ClientCalibrationReport {
        is_ready_for_operations: ready,
        client_size: (width, height),
        checks,
        timestamp_utc: Utc::now(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_basic_calibration_800x600() {
        let report = calibrate_window_geometry(800, 600);
        assert!(report.is_ready_for_operations);
        assert_eq!(report.checks[0].status, CalibrationStatus::Passed);
        assert_eq!(report.checks[1].status, CalibrationStatus::Passed);
    }

    #[test]
    fn test_calibration_fails_below_minimum() {
        let report = calibrate_window_geometry(640, 480);
        assert!(!report.is_ready_for_operations);
        assert_eq!(report.checks[0].status, CalibrationStatus::Failed);
    }
}
