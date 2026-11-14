//! Offline native-protocol fixtures; no pipe server, host or game is launched.
use maelstrom_fallback::maelstrom_bridge::{
    call_stream, call_stream_bounded, compute_pipe_name, normalize_data_root, read_framed_message,
    write_framed_message, BridgeError, MaelstromNativeBridge, NativeHostState, MAXIMUM_BYTES,
};
use serde_json::{json, Value};
use std::sync::{
    atomic::{AtomicBool, Ordering},
    Arc,
};
use std::time::{Duration, SystemTime, UNIX_EPOCH};
fn now() -> SystemTime {
    UNIX_EPOCH + Duration::from_secs(1794607200)
}
use tokio::io::{duplex, AsyncWriteExt};

fn snapshot() -> Value {
    let values = json!({"health":1022.0,"health.max":1022,"mana":76,"mana.max":76,"energy":55,"energy.max":55,"potions":1.5,"potions.max":4.0,"gold.current":1136,"wizard.level":10,"nativeIdle":true});
    let aliases: Vec<Value> = values.as_object().unwrap().iter().map(|(id,value)| json!({
        "id":id,"aliases":[],"dataType":if id=="nativeIdle" {"BOOL"} else if id=="gold.current" || id=="wizard.level" || id.ends_with(".max") && id!="potions.max" {"DINT"} else {"REAL"},
        "value":value,"quality":"fresh","source":"fixture","gameId":"wizard101","targetId":"test-target","observedUtc":"2026-11-13T22:00:00Z"
    })).collect();
    json!({"connected":true,"facts":{"gameId":"wizard101","targetId":"test-target","observedUtc":"2026-11-13T22:00:00Z","source":"fixture","values":values,
        "wizardSubject":{"profileId":"death-profile","name":"Test Wizard","school":"Death","generation":2,"observedUtcTicks":1,"verifiedCharacterId":"opaque-a"}},
        "aliases":aliases,"unavailable":["area"],"freshnessSeconds":2})
}
fn alias<'a>(v: &'a mut Value, id: &str) -> &'a mut Value {
    v["aliases"]
        .as_array_mut()
        .unwrap()
        .iter_mut()
        .find(|a| a["id"] == id)
        .unwrap()
}

#[test]
fn native_envelope_projects_qualified_aliases_and_preserves_evidence() {
    let v = snapshot();
    let s = NativeHostState::from_snapshot_json_at(&v, now());
    assert!(s.connected);
    assert_eq!(s.native_idle, Some(true));
    assert_eq!(s.health_current, Some(1022));
    assert_eq!(s.gold, Some(1136));
    assert_eq!(s.wizard_level, Some(10));
    assert_eq!(s.potions_current, Some(1.5));
    assert_eq!(s.wizard_name.as_deref(), Some("Test Wizard"));
    assert_eq!(s.raw_snapshot, v);
    assert_eq!(s.source.as_deref(), Some("fixture"));
    assert!(s.identity_key().is_some());
}
#[test]
fn stale_unknown_invalid_aliases_do_not_fall_back_to_raw_values() {
    for quality in ["stale", "unknown", "invalid"] {
        let mut v = snapshot();
        alias(&mut v, "health")["quality"] = json!(quality);
        alias(&mut v, "nativeIdle")["quality"] = json!(quality);
        let s = NativeHostState::from_snapshot_json_at(&v, now());
        assert_eq!(s.health_current, None);
        assert_eq!(s.native_idle, None);
        assert!(s.status.contains("Unknown"));
    }
}
#[test]
fn cross_target_source_or_time_aliases_are_not_current() {
    for key in ["targetId", "source", "observedUtc", "gameId"] {
        let mut v = snapshot();
        alias(&mut v, "health")[key] = json!("other");
        assert_eq!(
            NativeHostState::from_snapshot_json_at(&v, now()).health_current,
            None
        );
    }
}
#[test]
fn invalid_numeric_types_overflow_and_fraction_are_not_integer_stats() {
    for n in [json!(2147483648i64), json!(10.5), json!("10")] {
        let mut v = snapshot();
        alias(&mut v, "wizard.level")["value"] = n;
        assert_eq!(
            NativeHostState::from_snapshot_json_at(&v, now()).wizard_level,
            None
        );
    }
    let mut v = snapshot();
    alias(&mut v, "health")["dataType"] = json!("STRING");
    assert_eq!(
        NativeHostState::from_snapshot_json_at(&v, now()).health_current,
        None
    );
}
#[test]
fn disconnected_or_missing_aliases_never_project_old_facts() {
    let mut v = snapshot();
    v["connected"] = json!(false);
    let s = NativeHostState::from_snapshot_json_at(&v, now());
    assert_eq!(s.health_current, None);
    assert_eq!(s.wizard_name, None);
    assert_eq!(s.native_idle, None);
    v["connected"] = json!(true);
    v["aliases"] = json!([]);
    assert_eq!(NativeHostState::from_snapshot_json_at(&v, now()).gold, None);
}
#[test]
fn duplicate_alias_is_ambiguous_and_rejected() {
    let mut v = snapshot();
    let a = alias(&mut v, "health").clone();
    v["aliases"].as_array_mut().unwrap().push(a);
    assert_eq!(
        NativeHostState::from_snapshot_json_at(&v, now()).health_current,
        None
    );
}
#[test]
fn identity_changes_on_subject_not_level_or_timestamp() {
    let mut v = snapshot();
    let before = NativeHostState::from_snapshot_json_at(&v, now()).identity_key();
    alias(&mut v, "wizard.level")["value"] = json!(11);
    v["facts"]["wizardSubject"]["observedUtcTicks"] = json!(2);
    assert_eq!(
        NativeHostState::from_snapshot_json_at(&v, now()).identity_key(),
        before
    );
    v["facts"]["wizardSubject"]["verifiedCharacterId"] = json!("opaque-b");
    assert_ne!(
        NativeHostState::from_snapshot_json_at(&v, now()).identity_key(),
        before
    );
}
#[cfg(windows)]
#[test]
fn native_dotnet_pipe_fixtures_and_lexical_paths() {
    for (path, suffix) in [
        (r"C:\", "28F709D79730058E2A461518"),
        (r"F:\Modlists\AutoWizard101", "F8D3FAC982C4A4C93AF85520"),
        (
            r"F:\Modlists\missing\..\AutoWizard101\",
            "F8D3FAC982C4A4C93AF85520",
        ),
        (r"\\server\share\", "0F2D7F644A8577B0D66702CC"),
    ] {
        assert_eq!(
            compute_pipe_name(path).unwrap(),
            format!(r"\\.\pipe\maelstrom-deck-{suffix}")
        );
    }
    assert_eq!(
        normalize_data_root(".").unwrap(),
        normalize_data_root(std::env::current_dir().unwrap()).unwrap()
    );
    assert!(normalize_data_root("").is_err());
    assert!(normalize_data_root("C:\\caf\u{00e9}").is_err());
}
#[cfg(windows)]
#[test]
fn disconnect_clears_all_live_and_raw_state() {
    let mut b = MaelstromNativeBridge::new(r"F:\Modlists\AutoWizard101").unwrap();
    b.latest_state = NativeHostState::from_snapshot_json_at(&snapshot(), now());
    b.mark_disconnected("fixture");
    assert!(!b.latest_state.connected);
    assert_eq!(b.latest_state.health_max, None);
    assert_eq!(b.latest_state.potions_max, None);
    assert_eq!(b.latest_state.wizard_subject, None);
    assert!(b.latest_state.raw_snapshot.is_null());
}
async fn reply_with(
    reply: Value,
) -> Result<maelstrom_fallback::maelstrom_bridge::BridgeReply, BridgeError> {
    let (mut c, mut s) = duplex(MAXIMUM_BYTES);
    let task = tokio::spawn(async move {
        read_framed_message(&mut s).await.unwrap();
        write_framed_message(&mut s, &serde_json::to_vec(&reply).unwrap())
            .await
            .unwrap();
    });
    let r = call_stream(&mut c, "snapshot", None, None).await;
    task.await.unwrap();
    r
}
#[tokio::test]
async fn real_shape_roundtrip() {
    let r = reply_with(json!({"ok":true,"schemaVersion":1,"result":snapshot()}))
        .await
        .unwrap();
    assert_eq!(
        NativeHostState::from_snapshot_json_at(&r.result.unwrap(), now()).gold,
        Some(1136)
    );
}
#[tokio::test]
async fn required_version_and_owner_errors() {
    for r in [
        json!({"ok":true,"result":{}}),
        json!({"ok":true,"schemaVersion":2,"result":{}}),
    ] {
        assert!(matches!(
            reply_with(r).await,
            Err(BridgeError::InvalidReplySchema)
        ));
    }
    assert!(matches!(
        reply_with(json!({"ok":false,"schemaVersion":1,"error":"offline"})).await,
        Err(BridgeError::OwnerError(_))
    ));
    assert!(reply_with(json!({"schemaVersion":1})).await.is_err());
}
#[tokio::test]
async fn writes_rejected_before_any_io() {
    for command in [
        "start", "stop", "replace", "deal", "step", "pause", "reset", "connect",
    ] {
        let (mut c, _s) = duplex(32);
        assert!(matches!(
            call_stream(&mut c, command, None, None).await,
            Err(BridgeError::UnsupportedCommand(_))
        ));
    }
}
#[tokio::test]
async fn fragmented_frames_are_reassembled() {
    let (mut c, mut s) = duplex(1024);
    let task = tokio::spawn(async move {
        read_framed_message(&mut s).await.unwrap();
        let bytes = serde_json::to_vec(&json!({"ok":true,"schemaVersion":1,"result":{}})).unwrap();
        for b in (bytes.len() as u32)
            .to_le_bytes()
            .iter()
            .chain(bytes.iter())
        {
            s.write_all(&[*b]).await.unwrap();
        }
    });
    assert!(call_stream(&mut c, "state", None, None).await.is_ok());
    task.await.unwrap();
}
#[tokio::test]
async fn malformed_json_and_truncated_frames_reject() {
    let (mut c, mut s) = duplex(1024);
    let task = tokio::spawn(async move {
        read_framed_message(&mut s).await.unwrap();
        write_framed_message(&mut s, b"{nope}").await.unwrap();
    });
    assert!(matches!(
        call_stream(&mut c, "state", None, None).await,
        Err(BridgeError::Json(_))
    ));
    task.await.unwrap();
    let (mut c, mut s) = duplex(16);
    s.write_all(&10u32.to_le_bytes()).await.unwrap();
    s.write_all(b"{}").await.unwrap();
    drop(s);
    assert!(read_framed_message(&mut c).await.is_err());
}
#[tokio::test]
async fn framing_rejects_invalid_lengths_without_allocating_payload() {
    for len in [0u32, 1, MAXIMUM_BYTES as u32 + 1, u32::MAX] {
        let (mut c, mut s) = duplex(8);
        s.write_all(&len.to_le_bytes()).await.unwrap();
        assert!(matches!(
            read_framed_message(&mut c).await,
            Err(BridgeError::SizeBudgetExceeded(_))
        ));
    }
    let (mut c, _s) = duplex(8);
    assert!(matches!(
        write_framed_message(&mut c, &vec![0; MAXIMUM_BYTES + 1]).await,
        Err(BridgeError::SizeBudgetExceeded(_))
    ));
}
#[tokio::test]
async fn bounded_timeout_and_cancellation() {
    let (mut c, _s) = duplex(1024);
    assert!(matches!(
        call_stream_bounded(
            &mut c,
            "snapshot",
            Duration::from_millis(10),
            Arc::new(AtomicBool::new(false))
        )
        .await,
        Err(BridgeError::Timeout)
    ));
    let (mut c, _s) = duplex(1024);
    let flag = Arc::new(AtomicBool::new(false));
    let change = flag.clone();
    let task = tokio::spawn(async move {
        tokio::time::sleep(Duration::from_millis(5)).await;
        change.store(true, Ordering::Release);
    });
    assert!(matches!(
        call_stream_bounded(&mut c, "snapshot", Duration::from_secs(1), flag).await,
        Err(BridgeError::Cancelled)
    ));
    task.await.unwrap();
    let (mut c, _s) = duplex(1);
    assert!(matches!(
        call_stream_bounded(
            &mut c,
            "snapshot",
            Duration::from_secs(1),
            Arc::new(AtomicBool::new(true))
        )
        .await,
        Err(BridgeError::Cancelled)
    ));
}

#[test]
fn observations_expire_by_source_time_and_reject_future_or_malformed_time() {
    let v = snapshot();
    let s = NativeHostState::from_snapshot_json_at(&v, now());
    assert!(s.observation_is_fresh(now() + Duration::from_secs(2)));
    assert!(!s.observation_is_fresh(now() + Duration::from_millis(2001)));
    assert_eq!(
        NativeHostState::from_snapshot_json_at(&v, now() + Duration::from_millis(2001)).gold,
        None
    );
    assert_eq!(
        NativeHostState::from_snapshot_json_at(&v, now() - Duration::from_millis(1)).gold,
        None
    );
    for stamp in [
        "bad",
        "2026-02-30T22:00:00Z",
        "2026-11-13T25:00:00Z",
        "2026-11-13T22:00:00+00:00",
        "2026-11-13T22:00:00.Z",
    ] {
        let mut altered = snapshot();
        altered["facts"]["observedUtc"] = json!(stamp);
        for entry in altered["aliases"].as_array_mut().unwrap() {
            entry["observedUtc"] = json!(stamp);
        }
        assert_eq!(
            NativeHostState::from_snapshot_json_at(&altered, now()).gold,
            None
        );
    }
    let mut fraction = snapshot();
    fraction["facts"]["observedUtc"] = json!("2026-11-13T22:00:00.1234567Z");
    for entry in fraction["aliases"].as_array_mut().unwrap() {
        entry["observedUtc"] = json!("2026-11-13T22:00:00.1234567Z");
    }
    assert_eq!(
        NativeHostState::from_snapshot_json_at(&fraction, now() + Duration::from_millis(124)).gold,
        Some(1136)
    );
}
