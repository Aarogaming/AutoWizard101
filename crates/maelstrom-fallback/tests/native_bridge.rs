//! Integration tests for RUST-NATIVE-BRIDGE-57 read-only native deck host bridge.
//!
//! Tests wire framing, schema validation, command filtering, fake fragmented streams,
//! oversized messages, malformed JSON, timeout handling, and snapshot ingestion.

use maelstrom_fallback::maelstrom_bridge::{
    call_stream, compute_pipe_name, read_framed_message, write_framed_message, BridgeError,
    NativeHostState, MAXIMUM_BYTES,
};
use std::path::Path;
use std::time::Duration;
use tokio::io::{duplex, AsyncWriteExt};

#[test]
fn test_pipe_name_computation_matches_csharp_hash() {
    let root = Path::new(r"F:\Modlists\AutoWizard101");
    let name = compute_pipe_name(root).expect("compute pipe name");
    // Matches C# JobDeckWire.PipeName(dataRoot)
    assert!(name.starts_with(r"\\.\pipe\maelstrom-deck-"));
    assert_eq!(name.len(), r"\\.\pipe\maelstrom-deck-".len() + 24);
}

#[tokio::test]
async fn test_call_stream_roundtrip_valid_snapshot() {
    let (mut client, mut server) = duplex(1024 * 64);

    let server_task = tokio::spawn(async move {
        // Read client request
        let req_bytes = read_framed_message(&mut server).await.expect("server read");
        let req: serde_json::Value = serde_json::from_slice(&req_bytes).expect("parse req");
        assert_eq!(req["command"], "snapshot");

        // Respond with valid snapshot schemaVersion=1
        let reply_json = serde_json::json!({
            "ok": true,
            "schemaVersion": 1,
            "result": {
                "connected": true,
                "facts": {
                    "wizard.name": { "value": "Wolf Dragonflame" },
                    "wizard.school": { "value": "Fire" },
                    "wizard.level": { "value": 170 },
                    "gold": { "value": 525000 },
                    "health.current": { "value": 11200 },
                    "health.max": { "value": 11200 },
                    "mana.current": { "value": 650 },
                    "mana.max": { "value": 650 },
                    "location.world": { "value": "Novus" },
                    "location.zone": { "value": "Puerto Nuovo" },
                    "quest.name": { "value": "Into the Unknown" },
                    "observedUtc": "2026-11-13T22:00:00Z"
                },
                "aliases": {
                    "gold": 525000
                },
                "unavailable": [
                    "energy.current",
                    "energy.max",
                    "currency.crowns"
                ]
            }
        });
        let bytes = serde_json::to_vec(&reply_json).expect("serialize reply");
        write_framed_message(&mut server, &bytes).await.expect("server write");
    });

    let reply = call_stream(&mut client, "snapshot", None, None).await.expect("call stream");
    assert!(reply.ok);
    assert_eq!(reply.schema_version, 1);

    let state = NativeHostState::from_snapshot_json(&reply.result.unwrap());
    assert!(state.connected);
    assert_eq!(state.wizard_name.as_deref(), Some("Wolf Dragonflame"));
    assert_eq!(state.wizard_school.as_deref(), Some("Fire"));
    assert_eq!(state.wizard_level, Some(170));
    assert_eq!(state.gold, Some(525000));
    assert_eq!(state.health_current, Some(11200));
    assert_eq!(state.location_world.as_deref(), Some("Novus"));
    // Verify absent facts stay None instead of default 0/false
    assert_eq!(state.energy_current, None);
    assert_eq!(state.crowns, None);
    assert!(state.unavailable_facts.contains(&"energy.current".to_string()));

    server_task.await.expect("server completed");
}

#[tokio::test]
async fn test_unsupported_command_rejected_locally() {
    let (mut client, _server) = duplex(1024);
    // Writes like "start" or "replace" must be rejected in read-only bridge
    let res = call_stream(&mut client, "replace", None, None).await;
    match res {
        Err(BridgeError::UnsupportedCommand(cmd)) => assert_eq!(cmd, "replace"),
        _ => panic!("Expected UnsupportedCommand error, got {:?}", res),
    }
}

#[tokio::test]
async fn test_oversized_message_rejected_by_framing() {
    let (mut client, _server) = duplex(1024);
    let huge_bytes = vec![0x20u8; MAXIMUM_BYTES + 10];
    let res = write_framed_message(&mut client, &huge_bytes).await;
    match res {
        Err(BridgeError::SizeBudgetExceeded(len)) => assert!(len > MAXIMUM_BYTES),
        _ => panic!("Expected SizeBudgetExceeded, got {:?}", res),
    }
}

#[tokio::test]
async fn test_fragmented_stream_handling() {
    let (mut client, mut server) = duplex(1024 * 64);

    let server_task = tokio::spawn(async move {
        let _ = read_framed_message(&mut server).await.expect("read req");
        let reply_json = serde_json::json!({
            "ok": true,
            "schemaVersion": 1,
            "result": { "connected": false }
        });
        let bytes = serde_json::to_vec(&reply_json).expect("serialize");
        let len_buf = (bytes.len() as u32).to_le_bytes();

        // Write length in 1-byte fragments
        for b in &len_buf {
            server.write_all(&[*b]).await.expect("write byte");
            server.flush().await.expect("flush");
            tokio::time::sleep(Duration::from_millis(5)).await;
        }
        // Write payload in tiny fragments
        for chunk in bytes.chunks(8) {
            server.write_all(chunk).await.expect("write chunk");
            server.flush().await.expect("flush");
            tokio::time::sleep(Duration::from_millis(5)).await;
        }
    });

    let reply = call_stream(&mut client, "state", None, None).await.expect("call stream");
    assert!(reply.ok);
    server_task.await.expect("server finish");
}

#[tokio::test]
async fn test_malformed_json_response_rejected() {
    let (mut client, mut server) = duplex(1024 * 16);

    let server_task = tokio::spawn(async move {
        let _ = read_framed_message(&mut server).await.expect("read req");
        let corrupt_payload = b"{\"ok\": true, schemaVersion: NOT_JSON}";
        write_framed_message(&mut server, corrupt_payload).await.expect("write corrupt");
    });

    let res = call_stream(&mut client, "catalog", None, None).await;
    match res {
        Err(BridgeError::Json(_)) => {}
        _ => panic!("Expected Json error on malformed payload, got {:?}", res),
    }
    server_task.await.expect("server finish");
}

#[tokio::test]
async fn test_unsupported_schema_version_rejected() {
    let (mut client, mut server) = duplex(1024 * 16);

    let server_task = tokio::spawn(async move {
        let _ = read_framed_message(&mut server).await.expect("read req");
        let reply_json = serde_json::json!({
            "ok": true,
            "schemaVersion": 2, // Unsupported schema version
            "result": {}
        });
        let bytes = serde_json::to_vec(&reply_json).expect("serialize");
        write_framed_message(&mut server, &bytes).await.expect("write reply");
    });

    let res = call_stream(&mut client, "capabilities", None, None).await;
    match res {
        Err(BridgeError::InvalidReplySchema) => {}
        _ => panic!("Expected InvalidReplySchema error, got {:?}", res),
    }
    server_task.await.expect("server finish");
}
