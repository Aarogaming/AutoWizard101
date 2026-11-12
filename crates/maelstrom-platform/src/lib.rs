//! `maelstrom-platform`: Platform-specific client attachment, window tracking,
//! and adherence to the November 10 W101 client-side exception boundaries.
//!
//! # Safety Invariants (`W101_CLIENT_SIDE_EXCEPTION_20261110.md`)
//! 1. Network manipulation (packet crafting/injection/interception) is EXCLUDED.
//! 2. Installed on-disk game files remain strictly READ-ONLY.
//! 3. Process reads, hooks, and injection must be scoped, cancellable, and owned by a single input lease.
//! 4. UI/visual operations remain independently usable as a fallback and ground truth.

pub mod calibration;

use maelstrom_core::InputEvent;
use thiserror::Error;

#[derive(Debug, Error)]
pub enum PlatformError {
    #[error("Target window '{0}' not found")]
    WindowNotFound(String),
    #[error("Access denied or permission error: {0}")]
    AccessDenied(String),
    #[error("Network manipulation is strictly prohibited under W101 safety agreement")]
    NetworkManipulationBlocked,
    #[error("Modification of on-disk game files is strictly prohibited")]
    DiskModificationBlocked,
    #[error("Capture error: {0}")]
    CaptureFailed(String),
    #[error("Actuation error: {0}")]
    ActuationFailed(String),
}

/// Safe abstraction for window and client-area geometry tracking.
pub trait WindowTracker: Send + Sync {
    fn find_window(&self, title_or_class: &str) -> Result<u64, PlatformError>;
    fn get_client_bounds(&self, hwnd: u64) -> Result<(i32, i32, u32, u32), PlatformError>;
    fn is_in_foreground(&self, hwnd: u64) -> bool;
}

/// Safe abstraction for screen capture (DXGI / GDI / Mock).
pub trait ScreenCapturer: Send + Sync {
    fn capture_client_area(&self, hwnd: u64) -> Result<Vec<u8>, PlatformError>;
}

/// Safe abstraction for user input actuation.
pub trait InputActuator: Send + Sync {
    fn dispatch_event(&self, hwnd: u64, event: &InputEvent) -> Result<(), PlatformError>;
    fn release_all_keys(&self) -> Result<(), PlatformError>;
}

/// Enforces November 10 safety invariants before any client-side operation.
pub struct ClientOperationGuard {
    pub client_side_memory_authorized: bool,
}

impl ClientOperationGuard {
    pub fn new(authorized: bool) -> Self {
        Self {
            client_side_memory_authorized: authorized,
        }
    }

    /// Verifies that an operation complies with the 2026-11-10 boundary.
    pub fn check_network_allowed(&self) -> Result<(), PlatformError> {
        // Network manipulation is unconditionally excluded.
        Err(PlatformError::NetworkManipulationBlocked)
    }

    pub fn check_disk_write_allowed(&self) -> Result<(), PlatformError> {
        // Patching on-disk game files is unconditionally excluded.
        Err(PlatformError::DiskModificationBlocked)
    }
}
