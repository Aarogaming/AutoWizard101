// Phase 3 platform tests

use maelstrom_platform::{ClientOperationGuard, PlatformError, JobObjectGuard, spawn_suspended};
use maelstrom_core::InputEvent;
use windows::Win32::Foundation::CloseHandle;

#[test]
fn test_client_operation_guard_enforces_policy() {
    let guard = ClientOperationGuard::new(true);
    match guard.check_network_allowed() {
        Err(PlatformError::NetworkManipulationBlocked) => {}
        _ => panic!("Network check did not block as expected"),
    }
    match guard.check_disk_write_allowed() {
        Err(PlatformError::DiskModificationBlocked) => {}
        _ => panic!("Disk write check did not block as expected"),
    }
}

#[cfg(windows)]
#[test]
fn test_job_object_guard_cleanup() {
    let _guard = JobObjectGuard::new().expect("Failed to create JobObjectGuard");
    // No further action needed; test ensures construction works.
}

#[cfg(windows)]
#[test]
fn test_job_object_guard_assign_and_cleanup() {
    let guard = JobObjectGuard::new().expect("Failed to create JobObjectGuard");
    let pi = spawn_suspended("cmd", &["/C", "timeout", "/t", "5"]).expect("Failed to spawn process");
    // Assign the process handle to the job object
    guard.assign(pi.h_process).expect("Failed to assign process to job");
    unsafe {
        let _ = CloseHandle(pi.h_thread);
        let _ = CloseHandle(pi.h_process);
    }
}

#[test]
fn test_input_actuator_placeholder() {
    let _event = InputEvent::MouseClick { x: 100, y: 200, button: 1 };
    #[cfg(not(windows))]
    {
        use maelstrom_platform::INPUT_ACTUATOR;
        match INPUT_ACTUATOR.dispatch_event(0, &event) {
            Err(PlatformError::ActuationFailed(_)) => {}
            _ => panic!("Expected ActuationFailed on non-windows"),
        }
    }
}
