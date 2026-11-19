use maelstrom_fallback::{
    AutonomyReplayHarness, AutonomyReplaySession, MaelstromAutonomyBridge, NativeHostState,
    ReplayFrame,
};
use serde_json::json;
use std::env;
use std::fs;
use std::process::Command;

#[allow(clippy::too_many_arguments)]
fn make_test_frame(
    timestamp_ms: u64,
    connected: bool,
    health: i32,
    health_max: i32,
    mana: i32,
    mana_max: i32,
    potions: f64,
    combat_active: bool,
    pos_x: f64,
    pos_y: f64,
    expected_intent: Option<&str>,
) -> ReplayFrame {
    let now_str = "2026-11-18T22:30:00.0000000Z";
    let snapshot_json = json!({
        "connected": connected,
        "facts": {
            "gameId": "wizard101",
            "targetId": "12345/67890",
            "source": "reviewed-w101-ui-tree",
            "observedUtc": now_str,
            "combatActive": combat_active,
            "zoneName": "WizardCity_Commons",
            "spatialPosition": [pos_x, pos_y, 0.0],
            "wizardSubject": {
                "profileId": "prof-1",
                "name": "Alex",
                "school": "Storm",
                "generation": 1,
                "verifiedCharacterId": "char-1"
            }
        },
        "aliases": [
            {
                "id": "health",
                "dataType": "REAL",
                "value": health as f64,
                "quality": "fresh",
                "targetId": "12345/67890",
                "observedUtc": now_str,
                "source": "reviewed-w101-ui-tree",
                "gameId": "wizard101"
            },
            {
                "id": "health.max",
                "dataType": "DINT",
                "value": health_max,
                "quality": "fresh",
                "targetId": "12345/67890",
                "observedUtc": now_str,
                "source": "reviewed-w101-ui-tree",
                "gameId": "wizard101"
            },
            {
                "id": "mana",
                "dataType": "REAL",
                "value": mana as f64,
                "quality": "fresh",
                "targetId": "12345/67890",
                "observedUtc": now_str,
                "source": "reviewed-w101-ui-tree",
                "gameId": "wizard101"
            },
            {
                "id": "mana.max",
                "dataType": "DINT",
                "value": mana_max,
                "quality": "fresh",
                "targetId": "12345/67890",
                "observedUtc": now_str,
                "source": "reviewed-w101-ui-tree",
                "gameId": "wizard101"
            },
            {
                "id": "potions",
                "dataType": "REAL",
                "value": potions,
                "quality": "fresh",
                "targetId": "12345/67890",
                "observedUtc": now_str,
                "source": "reviewed-w101-ui-tree",
                "gameId": "wizard101"
            }
        ]
    });

    let parse_time = std::time::UNIX_EPOCH + std::time::Duration::from_secs(1795041000);
    let state = NativeHostState::from_snapshot_json_at(&snapshot_json, parse_time);
    ReplayFrame::new(timestamp_ms, state, expected_intent.map(str::to_string))
}

#[test]
fn test_replay_session_file_roundtrip_and_markdown() {
    let temp_dir = env::temp_dir().join(format!("maelstrom_cli_test_{}", uuid::Uuid::new_v4()));
    fs::create_dir_all(&temp_dir).unwrap();

    let mut session = AutonomyReplaySession::new("test_session_roundtrip");
    session.push_frame(make_test_frame(
        0,
        true,
        500,
        500,
        100,
        100,
        3.0,
        false,
        100.0,
        100.0,
        Some("PROPOSAL:StepWaypoint"),
    ));
    session.push_frame(make_test_frame(
        1000,
        true,
        500,
        500,
        100,
        100,
        3.0,
        true,
        100.0,
        100.0,
        Some("PROPOSAL:CombatTurn"),
    ));

    let file_path = temp_dir.join("trace.json");
    session.to_file(&file_path).expect("to_file must succeed");

    let loaded = AutonomyReplaySession::from_file(&file_path).expect("from_file must succeed");
    assert_eq!(loaded.session_id, session.session_id);
    assert_eq!(loaded.len(), 2);
    assert_eq!(loaded.duration_ms(), 1000);

    let mut bridge = MaelstromAutonomyBridge::new(&temp_dir).unwrap();
    bridge.blackboard.active_goal = Some("Explore".into());
    bridge.blackboard.active_waypoints = vec![(2000.0, 100.0, 0.0)];

    let harness = AutonomyReplayHarness::new();
    let (metrics, _) = harness.replay_session(&loaded, &mut bridge);

    let md = metrics.to_markdown(&loaded.session_id, loaded.duration_ms());
    assert!(md.contains("# Autonomy Replay Report: test_session_roundtrip"));
    assert!(md.contains("## Executive KPI Summary"));
    assert!(md.contains("Decision Accuracy"));
    assert!(md.contains("PASS"));

    fs::remove_dir_all(&temp_dir).ok();
}

#[test]
fn test_replay_cli_binary_execution() {
    let temp_dir = env::temp_dir().join(format!("maelstrom_bin_test_{}", uuid::Uuid::new_v4()));
    fs::create_dir_all(&temp_dir).unwrap();

    let mut session = AutonomyReplaySession::new("bin_session_01");
    session.initial_goal = Some("ExploreCommons".into());
    session.initial_waypoints = Some(vec![(2000.0, 150.0, 0.0)]);
    for i in 0..50 {
        session.push_frame(make_test_frame(
            i * 100,
            true,
            500,
            500,
            100,
            100,
            3.0,
            false,
            100.0 + (i as f64) * 2.0,
            150.0,
            Some("PROPOSAL:StepWaypoint"),
        ));
    }

    let trace_path = temp_dir.join("trace.json");
    session.to_file(&trace_path).unwrap();

    let md_path = temp_dir.join("report.md");
    let json_path = temp_dir.join("report.json");

    // Execute cargo run --bin maelstrom-replay
    let output = Command::new(env!("CARGO_BIN_EXE_maelstrom-replay"))
        .arg(&trace_path)
        .arg("--markdown")
        .arg(&md_path)
        .arg("--json")
        .arg(&json_path)
        .output()
        .expect("Failed to execute maelstrom-replay binary");

    assert!(
        output.status.success(),
        "CLI stderr: {}",
        String::from_utf8_lossy(&output.stderr)
    );

    // Verify Markdown report was generated
    assert!(md_path.exists());
    let md_content = fs::read_to_string(&md_path).unwrap();
    assert!(md_content.contains("Autonomy Replay Report: bin_session_01"));
    assert!(md_content.contains("Decision Accuracy"));

    // Verify JSON report was generated
    assert!(json_path.exists());
    let json_content = fs::read_to_string(&json_path).unwrap();
    let parsed: serde_json::Value = serde_json::from_str(&json_content).unwrap();
    assert_eq!(parsed["total_ticks"], 50);

    fs::remove_dir_all(&temp_dir).ok();
}

#[test]
fn test_ingest_and_replay_w101_operations_events() {
    let repo_root = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../..");
    let events_path =
        repo_root.join(".local/quest-step-check/TestData/Routines/w101-operations/events.jsonl");

    let content = if events_path.exists() {
        fs::read_to_string(&events_path).unwrap()
    } else {
        // Fallback fixture if running in isolated container
        String::from(
            "{\"event_id\":\"event-2\",\"kind\":\"observation\",\"sequence\":2,\"time_ms\":100,\"payload\":{\"module_observation\":{\"observation\":{\"scene\":\"Landmark.start\"}}}}\n{\"event_id\":\"event-4\",\"kind\":\"observation\",\"sequence\":4,\"time_ms\":140,\"payload\":{\"module_observation\":{\"observation\":{\"scene\":\"Progress.first\"}}}}"
        )
    };

    let session = AutonomyReplaySession::from_w101_events_jsonl(&content, "w101-operations-real")
        .expect("Ingestion must succeed");
    assert!(!session.is_empty());
    assert_eq!(session.session_id, "w101-operations-real");

    let temp_dir = env::temp_dir().join(format!("maelstrom_ingest_test_{}", uuid::Uuid::new_v4()));
    fs::create_dir_all(&temp_dir).unwrap();

    let mut bridge = MaelstromAutonomyBridge::new(&temp_dir).unwrap();
    let harness = AutonomyReplayHarness::new();
    let (metrics, _) = harness.replay_session(&session, &mut bridge);

    assert!(metrics.total_ticks > 0);
    assert_eq!(metrics.decision_accuracy_pct, 100.0);

    let md = metrics.to_markdown(&session.session_id, session.duration_ms());
    assert!(md.contains("Autonomy Replay Report: w101-operations-real"));
    assert!(md.contains("Decision Accuracy"));

    fs::remove_dir_all(&temp_dir).ok();
}

#[test]
fn test_ingest_and_replay_live_combat_ui_observations() {
    let repo_root = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../..");
    let ui_path = repo_root.join(".local/deck-release-20261111/combat-structure-live-data/operations/d5580af29bf3400fad808186238b8dd3/ui-observations.jsonl");

    let content = if ui_path.exists() {
        fs::read_to_string(&ui_path).unwrap()
    } else {
        String::from(
            "{\"kind\": \"tree\", \"observedUtcTicks\": 639299777727902712, \"states\": {\"nativeIdle\": true}, \"combatContext\": {\"planningVisible\": false}}\n{\"kind\": \"tree\", \"observedUtcTicks\": 639299777737909065, \"states\": {\"nativeIdle\": true}, \"combatContext\": {\"planningVisible\": false}}"
        )
    };

    let session =
        AutonomyReplaySession::from_ui_observations_jsonl(&content, "w101-combat-live-ui")
            .expect("Ingestion of UI observations must succeed");
    assert!(!session.is_empty());
    assert_eq!(session.session_id, "w101-combat-live-ui");

    let temp_dir = env::temp_dir().join(format!("maelstrom_ui_test_{}", uuid::Uuid::new_v4()));
    fs::create_dir_all(&temp_dir).unwrap();

    let mut bridge = MaelstromAutonomyBridge::new(&temp_dir).unwrap();
    let harness = AutonomyReplayHarness::new();
    let (metrics, _) = harness.replay_session(&session, &mut bridge);

    assert!(metrics.total_ticks > 0);

    let md = metrics.to_markdown(&session.session_id, session.duration_ms());
    assert!(md.contains("Autonomy Replay Report: w101-combat-live-ui"));

    fs::remove_dir_all(&temp_dir).ok();
}

#[test]
fn test_cli_binary_on_ingested_w101_trace() {
    let repo_root = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../..");
    let events_path =
        repo_root.join(".local/quest-step-check/TestData/Routines/w101-operations/events.jsonl");

    if !events_path.exists() {
        return;
    }

    let content = fs::read_to_string(&events_path).unwrap();
    let session = AutonomyReplaySession::from_w101_events_jsonl(&content, "w101-ops-cli-test")
        .expect("Ingestion must succeed");

    let temp_dir = env::temp_dir().join(format!("maelstrom_cli_ops_{}", uuid::Uuid::new_v4()));
    fs::create_dir_all(&temp_dir).unwrap();

    let trace_path = temp_dir.join("w101_ops_trace.json");
    session.to_file(&trace_path).unwrap();

    let md_path = temp_dir.join("w101_ops_report.md");
    let json_path = temp_dir.join("w101_ops_report.json");

    let output = Command::new(env!("CARGO_BIN_EXE_maelstrom-replay"))
        .arg(&trace_path)
        .arg("--markdown")
        .arg(&md_path)
        .arg("--json")
        .arg(&json_path)
        .output()
        .expect("CLI execution must succeed");

    assert!(
        output.status.success(),
        "CLI stderr: {}",
        String::from_utf8_lossy(&output.stderr)
    );
    assert!(md_path.exists());
    assert!(json_path.exists());

    let md_content = fs::read_to_string(&md_path).unwrap();
    assert!(md_content.contains("Autonomy Replay Report: w101-ops-cli-test"));
    assert!(md_content.contains("Decision Accuracy"));

    fs::remove_dir_all(&temp_dir).ok();
}

#[test]
fn test_batch_trace_discovery_and_evaluation() {
    let temp_dir = env::temp_dir().join(format!("maelstrom_batch_disco_{}", uuid::Uuid::new_v4()));
    fs::create_dir_all(&temp_dir).unwrap();

    // Create a native session trace
    let mut session1 = AutonomyReplaySession::new("disco_session_1");
    session1.push_frame(make_test_frame(
        0, true, 500, 500, 100, 100, 0.0, false, 100.0, 100.0, None,
    ));
    session1.push_frame(make_test_frame(
        100, true, 500, 500, 100, 100, 0.0, false, 102.0, 100.0, None,
    ));
    session1.to_file(temp_dir.join("trace_1.json")).unwrap();

    // Create an events.jsonl trace
    let events_content = "{\"event_id\":\"e1\",\"kind\":\"observation\",\"sequence\":1,\"time_ms\":100,\"payload\":{\"module_observation\":{\"observation\":{\"scene\":\"Landmark.start\"}}}}\n{\"event_id\":\"e2\",\"kind\":\"observation\",\"sequence\":2,\"time_ms\":200,\"payload\":{\"module_observation\":{\"observation\":{\"scene\":\"Progress.first\"}}}}";
    let sub_dir = temp_dir.join("sub_ops");
    fs::create_dir_all(&sub_dir).unwrap();
    fs::write(sub_dir.join("events.jsonl"), events_content).unwrap();

    // Create a ui-observations.jsonl trace
    let ui_content = "{\"kind\": \"tree\", \"observedUtcTicks\": 639299777727902712, \"states\": {\"nativeIdle\": true}, \"combatContext\": {\"planningVisible\": false}}\n{\"kind\": \"tree\", \"observedUtcTicks\": 639299777737909065, \"states\": {\"nativeIdle\": true}, \"combatContext\": {\"planningVisible\": false}}";
    fs::write(sub_dir.join("ui-observations.jsonl"), ui_content).unwrap();

    let discovered = AutonomyReplaySession::discover_and_load_traces(&temp_dir);
    assert_eq!(discovered.len(), 3);

    let harness = AutonomyReplayHarness::new();
    let batch_result = harness.run_batch(&discovered, &temp_dir);

    assert_eq!(batch_result.total_sessions, 3);
    assert_eq!(batch_result.passed_sessions, 3);
    assert_eq!(batch_result.failed_sessions, 0);

    let md_matrix = batch_result.to_markdown_matrix();
    assert!(md_matrix.contains("# Autonomy Replay Benchmark Matrix"));
    assert!(md_matrix.contains("Total Sessions Evaluated"));
    assert!(md_matrix.contains("3"));
    assert!(md_matrix.contains("**Gate Status**: **PASS**"));

    let json_val = batch_result.to_json_value();
    assert_eq!(json_val["total_sessions"], 3);

    fs::remove_dir_all(&temp_dir).ok();
}

#[test]
fn test_batch_cli_binary_execution() {
    let temp_dir = env::temp_dir().join(format!("maelstrom_batch_cli_{}", uuid::Uuid::new_v4()));
    fs::create_dir_all(&temp_dir).unwrap();

    let mut session = AutonomyReplaySession::new("batch_cli_trace");
    for i in 0..10 {
        session.push_frame(make_test_frame(
            i * 100,
            true,
            500,
            500,
            100,
            100,
            0.0,
            false,
            100.0,
            100.0,
            None,
        ));
    }
    session.to_file(temp_dir.join("session.json")).unwrap();

    let matrix_out = temp_dir.join("matrix.md");
    let matrix_json = temp_dir.join("matrix.json");

    let output = Command::new(env!("CARGO_BIN_EXE_maelstrom-replay"))
        .arg("--batch-dir")
        .arg(&temp_dir)
        .arg("--matrix-out")
        .arg(&matrix_out)
        .arg("--matrix-json")
        .arg(&matrix_json)
        .output()
        .expect("Batch CLI execution must succeed");

    assert!(
        output.status.success(),
        "CLI stderr: {}",
        String::from_utf8_lossy(&output.stderr)
    );
    assert!(matrix_out.exists());
    assert!(matrix_json.exists());

    let md_content = fs::read_to_string(&matrix_out).unwrap();
    assert!(md_content.contains("Autonomy Replay Benchmark Matrix"));
    assert!(md_content.contains("Total Sessions Evaluated"));
    assert!(md_content.contains("1"));

    fs::remove_dir_all(&temp_dir).ok();
}
