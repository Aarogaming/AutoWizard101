use maelstrom_fallback::maelstrom_bridge::{open_with_busy_retry, BridgeError};
use std::{
    io,
    sync::{
        atomic::{AtomicBool, Ordering},
        Arc,
    },
    time::Duration,
};

#[cfg(windows)]
#[tokio::test]
async fn actual_windows_busy_pipe_recovers_after_instance_is_released() {
    use maelstrom_fallback::maelstrom_bridge::connect_pipe;
    use tokio::net::windows::named_pipe::{ClientOptions, ServerOptions};
    let name = format!(r"\\.\pipe\maelstrom-busy-fixture-{}", uuid::Uuid::new_v4());
    let server = ServerOptions::new().max_instances(1).create(&name).unwrap();
    let blocker = ClientOptions::new().open(&name).unwrap();
    server.connect().await.unwrap();
    assert_eq!(
        ClientOptions::new().open(&name).unwrap_err().raw_os_error(),
        Some(231)
    );
    let next_name = name.clone();
    let reader =
        tokio::spawn(
            async move { connect_pipe(&next_name, Arc::new(AtomicBool::new(false))).await },
        );
    tokio::time::sleep(Duration::from_millis(40)).await;
    drop(blocker);
    server.disconnect().unwrap();
    tokio::time::timeout(Duration::from_secs(2), server.connect())
        .await
        .unwrap()
        .unwrap();
    let client = tokio::time::timeout(Duration::from_secs(2), reader)
        .await
        .unwrap()
        .unwrap()
        .unwrap();
    drop(client);
}

#[tokio::test]
async fn transient_busy_recovers_without_replaying_a_request() {
    let mut attempts = 0;
    let result = open_with_busy_retry(
        || {
            attempts += 1;
            if attempts < 3 {
                Err(io::Error::from_raw_os_error(231))
            } else {
                Ok(42)
            }
        },
        Duration::from_secs(1),
        Arc::new(AtomicBool::new(false)),
    )
    .await
    .unwrap();
    assert_eq!(result, 42);
    assert_eq!(attempts, 3);
}

#[tokio::test]
async fn missing_and_denied_pipes_are_not_retried() {
    for code in [2, 5, 109] {
        let mut attempts = 0;
        let result = open_with_busy_retry::<()>(
            || {
                attempts += 1;
                Err(io::Error::from_raw_os_error(code))
            },
            Duration::from_secs(1),
            Arc::new(AtomicBool::new(false)),
        )
        .await;
        assert!(matches!(result, Err(BridgeError::Io(_))));
        assert_eq!(attempts, 1);
    }
}

#[tokio::test]
async fn busy_deadline_and_cancellation_are_bounded() {
    let mut attempts = 0;
    let result = open_with_busy_retry::<()>(
        || {
            attempts += 1;
            Err(io::Error::from_raw_os_error(231))
        },
        Duration::from_millis(10),
        Arc::new(AtomicBool::new(false)),
    )
    .await;
    assert!(matches!(result, Err(BridgeError::Timeout)));
    assert_eq!(attempts, 1);
    let flag = Arc::new(AtomicBool::new(false));
    let set = flag.clone();
    let result = open_with_busy_retry::<()>(
        || {
            set.store(true, Ordering::Release);
            Err(io::Error::from_raw_os_error(231))
        },
        Duration::from_secs(1),
        flag,
    )
    .await;
    assert!(matches!(result, Err(BridgeError::Cancelled)));
}

#[tokio::test]
async fn cancellation_after_open_drops_connection() {
    let flag = Arc::new(AtomicBool::new(false));
    let set = flag.clone();
    let result = open_with_busy_retry(
        || {
            set.store(true, Ordering::Release);
            Ok(())
        },
        Duration::from_secs(1),
        flag,
    )
    .await;
    assert!(matches!(result, Err(BridgeError::Cancelled)));
}
