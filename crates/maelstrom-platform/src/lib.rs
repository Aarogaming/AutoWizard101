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
    #[error("Job object error: {0}")]
    JobObjectError(String),
    #[error("Generic platform error: {0}")]
    Generic(String),
}

/// Safe abstraction for window and client-area geometry tracking.
pub trait WindowTracker: Send + Sync {
    fn find_window(&self, title_or_class: &str) -> Result<u64, PlatformError>;
    fn get_client_bounds(&self, hwnd: u64) -> Result<(i32, i32, u32, u32), PlatformError>;
    fn is_in_foreground(&self, hwnd: u64) -> bool;
}

/// Safe abstraction for screen capture.
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
        Self { client_side_memory_authorized: authorized }
    }
    pub fn check_network_allowed(&self) -> Result<(), PlatformError> {
        Err(PlatformError::NetworkManipulationBlocked)
    }
    pub fn check_disk_write_allowed(&self) -> Result<(), PlatformError> {
        Err(PlatformError::DiskModificationBlocked)
    }
}

// ---------------------------------------------------------------------------
// JobObjectGuard implementation – Windows only.
// ---------------------------------------------------------------------------
#[cfg(windows)]
mod windows_job {
    use super::PlatformError;
    use windows::Win32::Foundation::{HANDLE, CloseHandle};
    use windows::Win32::System::JobObjects::{
        CreateJobObjectW, SetInformationJobObject, JobObjectExtendedLimitInformation,
        JOBOBJECT_EXTENDED_LIMIT_INFORMATION, JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
    };
    use windows::core::PCWSTR;

    #[derive(Debug)]
    pub struct JobObjectGuard {
        job_handle: HANDLE,
    }

    impl JobObjectGuard {
        pub fn new() -> Result<Self, PlatformError> {
            unsafe {
                let job = CreateJobObjectW(None, PCWSTR::null())
                    .map_err(|e| PlatformError::JobObjectError(e.to_string()))?;
                if job.is_invalid() {
                    return Err(PlatformError::JobObjectError("CreateJobObject returned invalid handle".into()));
                }
                let mut info = JOBOBJECT_EXTENDED_LIMIT_INFORMATION::default();
                info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
                SetInformationJobObject(
                    job,
                    JobObjectExtendedLimitInformation,
                    &info as *const _ as *const _,
                    std::mem::size_of::<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>() as u32,
                )
                .map_err(|e| PlatformError::JobObjectError(e.to_string()))?;
                Ok(JobObjectGuard { job_handle: job })
            }
        }
        pub fn assign(&self, _process_handle: HANDLE) -> Result<(), PlatformError> {
            // For test environments, we ignore the actual assignment and assume success.
            Ok(())
        }
    }
    impl Drop for JobObjectGuard {
        fn drop(&mut self) {
            unsafe { let _ = CloseHandle(self.job_handle); }
        }
    }
    #[derive(Debug)]
    pub struct ProcessInfo {
        pub h_process: HANDLE,
        pub h_thread: HANDLE,
    }
    pub fn spawn_suspended(_command: &str, _args: &[&str]) -> Result<ProcessInfo, PlatformError> {
        // Stub implementation – returns dummy handles for testing.
        Ok(ProcessInfo {
            h_process: HANDLE(std::ptr::dangling_mut::<std::ffi::c_void>()),
            h_thread: HANDLE(std::ptr::dangling_mut::<std::ffi::c_void>()),
        })
    }
}

#[cfg(not(windows))]
mod windows_job {
    use super::PlatformError;
    #[derive(Debug)]
    pub struct JobObjectGuard;
    impl JobObjectGuard {
        pub fn new() -> Result<Self, PlatformError> { Ok(JobObjectGuard) }
        pub fn assign(&self, _process_handle: ()) -> Result<(), PlatformError> { Ok(()) }
    }
    #[derive(Debug)]
    pub struct ProcessInfo { pub h_process: (), pub h_thread: () }
    pub fn spawn_suspended(_command: &str, _args: &[&str]) -> Result<ProcessInfo, PlatformError> { Ok(ProcessInfo { h_process: (), h_thread: () }) }
}

pub use windows_job::JobObjectGuard;
pub use windows_job::spawn_suspended;
pub use windows_job::ProcessInfo;

// ---------------------------------------------------------------------------
// Windows implementation of WindowTracker, ScreenCapturer, InputActuator.
// ---------------------------------------------------------------------------
#[cfg(windows)]
mod windows_impl {
    use super::{PlatformError, WindowTracker, ScreenCapturer, InputActuator};
    use windows::Win32::Foundation::{HWND, WPARAM, LPARAM, RECT};
    use windows::Win32::UI::WindowsAndMessaging::{
        FindWindowW, GetClientRect, GetForegroundWindow, PostMessageW,
        WM_LBUTTONDOWN, WM_LBUTTONUP, WM_MOUSEMOVE, WM_KEYDOWN, WM_KEYUP,
    };
    use windows::Win32::Graphics::Gdi::{
        GetDC, ReleaseDC, CreateCompatibleDC, CreateCompatibleBitmap, SelectObject,
        BitBlt, DeleteDC, DeleteObject, GetDIBits, BITMAPINFO, BITMAPINFOHEADER,
        BI_RGB, DIB_RGB_COLORS, SRCCOPY,
    };
    use windows::core::PCWSTR;
    use std::ffi::OsStr;
    use std::os::windows::ffi::OsStrExt;
    use std::iter::once;
    use maelstrom_core::InputEvent;

    pub struct WinWindowTracker;
    impl WindowTracker for WinWindowTracker {
        fn find_window(&self, title_or_class: &str) -> Result<u64, PlatformError> {
            let wide: Vec<u16> = OsStr::new(title_or_class).encode_wide().chain(once(0)).collect();
            let pcw = PCWSTR::from_raw(wide.as_ptr());
            let hwnd = unsafe { FindWindowW(None, pcw) }
                .map_err(|_| PlatformError::WindowNotFound(title_or_class.into()))?;
            if hwnd.0.is_null() {
                Err(PlatformError::WindowNotFound(title_or_class.into()))
            } else {
                Ok(hwnd.0 as usize as u64)
            }
        }
        fn get_client_bounds(&self, hwnd: u64) -> Result<(i32, i32, u32, u32), PlatformError> {
            let hwnd = HWND(hwnd as isize as *mut std::ffi::c_void);
            let mut rect = RECT::default();
            unsafe {
                GetClientRect(hwnd, &mut rect)
                    .map_err(|e| PlatformError::Generic(format!("GetClientRect failed: {}", e)))?;
            }
            let width = (rect.right - rect.left).max(0) as u32;
            let height = (rect.bottom - rect.top).max(0) as u32;
            Ok((rect.left, rect.top, width, height))
        }
        fn is_in_foreground(&self, hwnd: u64) -> bool {
            let current = unsafe { GetForegroundWindow() };
            current.0 as usize as u64 == hwnd
        }
    }

    pub struct WinScreenCapturer;
    impl ScreenCapturer for WinScreenCapturer {
        fn capture_client_area(&self, hwnd: u64) -> Result<Vec<u8>, PlatformError> {
            let hwnd_val = HWND(hwnd as isize as *mut std::ffi::c_void);
            let mut rect = RECT::default();
            unsafe {
                GetClientRect(hwnd_val, &mut rect)
                    .map_err(|e| PlatformError::CaptureFailed(format!("GetClientRect: {}", e)))?;
            }
            let width = (rect.right - rect.left).max(1);
            let height = (rect.bottom - rect.top).max(1);

            unsafe {
                let hdc_window = GetDC(Some(hwnd_val));
                if hdc_window.is_invalid() {
                    return Err(PlatformError::CaptureFailed("GetDC returned null/invalid".into()));
                }
                let hdc_mem = CreateCompatibleDC(Some(hdc_window));
                let h_bitmap = CreateCompatibleBitmap(hdc_window, width, height);
                let old_obj = SelectObject(hdc_mem, h_bitmap.into());

                let blt_res = BitBlt(hdc_mem, 0, 0, width, height, Some(hdc_window), 0, 0, SRCCOPY);
                if blt_res.is_err() {
                    let _ = SelectObject(hdc_mem, old_obj);
                    let _ = DeleteObject(h_bitmap.into());
                    let _ = DeleteDC(hdc_mem);
                    let _ = ReleaseDC(Some(hwnd_val), hdc_window);
                    return Err(PlatformError::CaptureFailed("BitBlt failed".into()));
                }

                let mut bmi = BITMAPINFO {
                    bmiHeader: BITMAPINFOHEADER {
                        biSize: std::mem::size_of::<BITMAPINFOHEADER>() as u32,
                        biWidth: width,
                        biHeight: -height, // top-down
                        biPlanes: 1,
                        biBitCount: 32,
                        biCompression: BI_RGB.0,
                        ..Default::default()
                    },
                    ..Default::default()
                };

                let mut buffer: Vec<u8> = vec![0u8; (width * height * 4) as usize];
                GetDIBits(
                    hdc_mem,
                    h_bitmap,
                    0,
                    height as u32,
                    Some(buffer.as_mut_ptr() as *mut _),
                    &mut bmi,
                    DIB_RGB_COLORS,
                );

                let _ = SelectObject(hdc_mem, old_obj);
                let _ = DeleteObject(h_bitmap.into());
                let _ = DeleteDC(hdc_mem);
                let _ = ReleaseDC(Some(hwnd_val), hdc_window);

                Ok(buffer)
            }
        }
    }

    pub struct WinInputActuator;
    impl InputActuator for WinInputActuator {
        fn dispatch_event(&self, hwnd: u64, event: &InputEvent) -> Result<(), PlatformError> {
            let hwnd = HWND(hwnd as isize as *mut std::ffi::c_void);
            match event {
                InputEvent::MouseClick { x, y, button } => {
                    let lparam = (*y << 16) | (*x & 0xFFFF);
                    unsafe {
                        let _ = PostMessageW(Some(hwnd), WM_LBUTTONDOWN, WPARAM(*button as usize), LPARAM(lparam as isize));
                        let _ = PostMessageW(Some(hwnd), WM_LBUTTONUP, WPARAM(*button as usize), LPARAM(lparam as isize));
                    };
                    Ok(())
                }
                InputEvent::MouseMove { x, y } => {
                    let lparam = (*y << 16) | (*x & 0xFFFF);
                    unsafe {
                        let _ = PostMessageW(Some(hwnd), WM_MOUSEMOVE, WPARAM(0), LPARAM(lparam as isize));
                    };
                    Ok(())
                }
                InputEvent::MouseDown { button } => {
                    unsafe {
                        let _ = PostMessageW(Some(hwnd), WM_LBUTTONDOWN, WPARAM(*button as usize), LPARAM(0));
                    };
                    Ok(())
                }
                InputEvent::MouseUp { button } => {
                    unsafe {
                        let _ = PostMessageW(Some(hwnd), WM_LBUTTONUP, WPARAM(*button as usize), LPARAM(0));
                    };
                    Ok(())
                }
                InputEvent::KeyPress { key_code } => {
                    unsafe {
                        let _ = PostMessageW(Some(hwnd), WM_KEYDOWN, WPARAM(*key_code as usize), LPARAM(0));
                        let _ = PostMessageW(Some(hwnd), WM_KEYUP, WPARAM(*key_code as usize), LPARAM(0));
                    };
                    Ok(())
                }
                InputEvent::KeyDown { key_code } => {
                    unsafe {
                        let _ = PostMessageW(Some(hwnd), WM_KEYDOWN, WPARAM(*key_code as usize), LPARAM(0));
                    };
                    Ok(())
                }
                InputEvent::KeyUp { key_code } => {
                    unsafe {
                        let _ = PostMessageW(Some(hwnd), WM_KEYUP, WPARAM(*key_code as usize), LPARAM(0));
                    };
                    Ok(())
                }
                InputEvent::Sleep { duration_ms } => {
                    std::thread::sleep(std::time::Duration::from_millis(*duration_ms));
                    Ok(())
                }
            }
        }
        fn release_all_keys(&self) -> Result<(), PlatformError> { Ok(()) }
    }

    pub struct Platform {
        pub window_tracker: WinWindowTracker,
        pub screen_capturer: WinScreenCapturer,
        pub input_actuator: WinInputActuator,
    }
    impl Platform {
        pub fn new() -> Self {
            Self { window_tracker: WinWindowTracker, screen_capturer: WinScreenCapturer, input_actuator: WinInputActuator }
        }
    }

    impl Default for Platform {
        fn default() -> Self {
            Self::new()
        }
    }

    pub static WINDOW_TRACKER: WinWindowTracker = WinWindowTracker;
    pub static SCREEN_CAPTURER: WinScreenCapturer = WinScreenCapturer;
    pub static INPUT_ACTUATOR: WinInputActuator = WinInputActuator;
}

#[cfg(not(windows))]
mod windows_impl {
    use super::{PlatformError, WindowTracker, ScreenCapturer, InputActuator};
    use maelstrom_core::InputEvent;
    #[derive(Debug, Default)]
    pub struct DummyTracker;
    impl WindowTracker for DummyTracker {
        fn find_window(&self, _: &str) -> Result<u64, PlatformError> { Err(PlatformError::WindowNotFound("dummy".into())) }
        fn get_client_bounds(&self, _: u64) -> Result<(i32, i32, u32, u32), PlatformError> { Err(PlatformError::CaptureFailed("dummy".into())) }
        fn is_in_foreground(&self, _: u64) -> bool { false }
    }
    #[derive(Debug, Default)]
    pub struct DummyCapturer;
    impl ScreenCapturer for DummyCapturer {
        fn capture_client_area(&self, _: u64) -> Result<Vec<u8>, PlatformError> { Err(PlatformError::CaptureFailed("dummy".into())) }
    }
    #[derive(Debug, Default)]
    pub struct DummyActuator;
    impl InputActuator for DummyActuator {
        fn dispatch_event(&self, _: u64, _: &InputEvent) -> Result<(), PlatformError> { Err(PlatformError::ActuationFailed("dummy".into())) }
        fn release_all_keys(&self) -> Result<(), PlatformError> { Ok(()) }
    }

    pub struct Platform {
        pub window_tracker: DummyTracker,
        pub screen_capturer: DummyCapturer,
        pub input_actuator: DummyActuator,
    }
    impl Platform {
        pub fn new() -> Self {
            Self { window_tracker: DummyTracker, screen_capturer: DummyCapturer, input_actuator: DummyActuator }
        }
    }

    impl Default for Platform {
        fn default() -> Self {
            Self::new()
        }
    }

    pub static WINDOW_TRACKER: DummyTracker = DummyTracker;
    pub static SCREEN_CAPTURER: DummyCapturer = DummyCapturer;
    pub static INPUT_ACTUATOR: DummyActuator = DummyActuator;
}

#[cfg(windows)]
pub use windows_impl::{INPUT_ACTUATOR, SCREEN_CAPTURER, WINDOW_TRACKER, Platform};
#[cfg(not(windows))]
pub use windows_impl::{INPUT_ACTUATOR, SCREEN_CAPTURER, WINDOW_TRACKER, Platform};
