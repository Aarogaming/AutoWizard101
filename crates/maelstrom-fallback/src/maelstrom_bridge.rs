//! Lightweight read-only named-pipe client bridge to the native Project Maelstrom deck host.
//!
//! # Protocol Specification (JobDeckWire / JobDeckHost)
//! - Transport: Windows Named Pipe `\\.\pipe\maelstrom-deck-<HASH_HEX_24>`
//!   where HASH is SHA256 of upper-cased, trimmed, absolute data root path.
//! - Framing: 4-byte Little-Endian unsigned/signed length prefix, followed by JSON payload.
//! - Limits: Min 2 bytes, Max 262,144 bytes (`MAXIMUM_BYTES`).
//! - Supported Read-Only Commands: `capabilities`, `catalog`, `state`, `snapshot`.
//! - Safety Guarantee: Read-only; no actions dispatched; no state simulated.

use serde::{Deserialize, Serialize};
use std::io;
use std::path::{Path, PathBuf};
use std::sync::{
    atomic::{AtomicBool, Ordering},
    Arc,
};
use std::time::{Duration, SystemTime, UNIX_EPOCH};
use thiserror::Error;
use tokio::io::{AsyncRead, AsyncReadExt, AsyncWrite, AsyncWriteExt};

use maelstrom_core::behavior_tree::{
    build_w101_canonical_tree, BtNode, BtStatus, Selector, W101Blackboard,
};
use maelstrom_core::combat_geometry::CombatGeometry;
use maelstrom_core::costmap::Vec2D;
use maelstrom_core::kinematics::{
    sample_target_in_rect, BezierConfig, BezierTrajectoryGenerator, FastPrng, FittsLawModel,
    KinematicPlan, Point2D,
};
use maelstrom_core::steering::{Pose2D, PurePursuitController, SteeringCommand};

pub const MAXIMUM_BYTES: usize = 262_144;
pub const DEFAULT_TIMEOUT: Duration = Duration::from_secs(8);
pub const CONNECT_TIMEOUT: Duration = Duration::from_millis(1500);

/// Retry only ERROR_PIPE_BUSY before connecting; never replay a sent request.
pub async fn open_with_busy_retry<T>(
    mut open: impl FnMut() -> io::Result<T>,
    budget: Duration,
    cancelled: Arc<AtomicBool>,
) -> Result<T, BridgeError> {
    let deadline = tokio::time::Instant::now() + budget;
    let mut delay = Duration::from_millis(25);
    loop {
        if cancelled.load(Ordering::Acquire) {
            return Err(BridgeError::Cancelled);
        }
        let remaining = deadline.saturating_duration_since(tokio::time::Instant::now());
        if remaining.is_zero() {
            return Err(BridgeError::Timeout);
        }
        match open() {
            Ok(connection) => {
                if cancelled.load(Ordering::Acquire) {
                    return Err(BridgeError::Cancelled);
                }
                return Ok(connection);
            }
            Err(error) if error.raw_os_error() == Some(231) => {}
            Err(error) => return Err(BridgeError::Io(error)),
        }
        tokio::select! {
            biased;
            _ = async {
                while !cancelled.load(Ordering::Acquire) {
                    tokio::time::sleep(Duration::from_millis(20)).await;
                }
            } => return Err(BridgeError::Cancelled),
            _ = tokio::time::sleep(delay.min(remaining)) => {},
        }
        delay = (delay * 2).min(Duration::from_millis(100));
    }
}

#[cfg(windows)]
pub async fn connect_pipe(
    name: &str,
    cancelled: Arc<AtomicBool>,
) -> Result<tokio::net::windows::named_pipe::NamedPipeClient, BridgeError> {
    open_with_busy_retry(
        || tokio::net::windows::named_pipe::ClientOptions::new().open(name),
        CONNECT_TIMEOUT,
        cancelled,
    )
    .await
}

#[derive(Debug, Error)]
pub enum BridgeError {
    #[error("I/O error: {0}")]
    Io(#[from] io::Error),
    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),
    #[error("Message length {0} exceeds budget (2 to {MAXIMUM_BYTES} bytes)")]
    SizeBudgetExceeded(usize),
    #[error("Unsupported command: {0}")]
    UnsupportedCommand(String),
    #[error("Owner timeout: outcome unconfirmed")]
    Timeout,
    #[error("Operation cancelled")]
    Cancelled,
    #[error("Owner error: {0}")]
    OwnerError(String),
    #[error("Invalid reply schema (schemaVersion != 1 or missing schemaVersion/ok flag)")]
    InvalidReplySchema,
    #[error("Data root path error: {0}")]
    InvalidDataRoot(String),
}

/// Request structure framed over the named pipe wire.
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct BridgeRequest {
    pub command: String,
    pub arguments: serde_json::Value,
    #[serde(rename = "requestId", skip_serializing_if = "Option::is_none")]
    pub request_id: Option<String>,
}

/// Reply envelope from the native deck host.
/// Explicitly rejects missing `schemaVersion` (must be `Some(1)`).
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct BridgeReply {
    pub ok: bool,
    #[serde(default)]
    pub result: Option<serde_json::Value>,
    #[serde(default)]
    pub error: Option<String>,
    #[serde(rename = "schemaVersion")]
    pub schema_version: Option<i32>,
}

/// Lexical normalization, never resolving symlinks or reading directories.
/// Non-ASCII and device paths remain unsupported until native parity is qualified.
pub fn normalize_data_root<P: AsRef<Path>>(data_root: P) -> Result<String, BridgeError> {
    let raw = data_root
        .as_ref()
        .to_str()
        .ok_or_else(|| BridgeError::InvalidDataRoot("Invalid Unicode path".into()))?;
    if raw.trim().is_empty()
        || !raw.is_ascii()
        || raw.contains(char::from(0))
        || raw.starts_with(r"\\?\")
        || raw.starts_with(r"\\.\")
    {
        return Err(BridgeError::InvalidDataRoot(
            "Only ordinary nonempty ASCII Windows paths are qualified".into(),
        ));
    }
    #[cfg(not(windows))]
    return Err(BridgeError::InvalidDataRoot(
        "Windows normalization requires Windows".into(),
    ));
    #[cfg(windows)]
    {
        let full = std::path::absolute(data_root)
            .map_err(|e| BridgeError::InvalidDataRoot(e.to_string()))?;
        let mut value = full
            .to_str()
            .ok_or_else(|| BridgeError::InvalidDataRoot("Invalid path".into()))?
            .replace('/', "\\");
        let drive_root = value.len() == 3 && value.as_bytes()[1] == b':' && value.ends_with('\\');
        if !drive_root && value.ends_with('\\') {
            value.pop();
        }
        if !value.is_ascii() {
            return Err(BridgeError::InvalidDataRoot(
                "Non-ASCII paths not qualified".into(),
            ));
        }
        Ok(value.to_ascii_uppercase())
    }
}

pub fn compute_pipe_name<P: AsRef<Path>>(data_root: P) -> Result<String, BridgeError> {
    let normalized = normalize_data_root(data_root)?;
    Ok(format!(
        r"\\.\pipe\maelstrom-deck-{}",
        sha256_hex(normalized.as_bytes())[..24].to_ascii_uppercase()
    ))
}

/// Pure in-tree SHA-256 implementation avoiding external dependency bloat.
fn sha256_hex(data: &[u8]) -> String {
    let mut h0: u32 = 0x6a09e667;
    let mut h1: u32 = 0xbb67ae85;
    let mut h2: u32 = 0x3c6ef372;
    let mut h3: u32 = 0xa54ff53a;
    let mut h4: u32 = 0x510e527f;
    let mut h5: u32 = 0x9b05688c;
    let mut h6: u32 = 0x1f83d9ab;
    let mut h7: u32 = 0x5be0cd19;

    let k: [u32; 64] = [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4,
        0xab1c5ed5, 0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe,
        0x9bdc06a7, 0xc19bf174, 0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f,
        0x4a7484aa, 0x5cb0a9dc, 0x76f988da, 0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7,
        0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967, 0x27b70a85, 0x2e1b2138, 0x4d2c6dfc,
        0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85, 0xa2bfe8a1, 0xa81a664b,
        0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070, 0x19a4c116,
        0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7,
        0xc67178f2,
    ];

    let bit_len = (data.len() as u64) * 8;
    let mut msg = data.to_vec();
    msg.push(0x80);
    while (msg.len() % 64) != 56 {
        msg.push(0x00);
    }
    msg.extend_from_slice(&bit_len.to_be_bytes());

    for chunk in msg.chunks(64) {
        let mut w = [0u32; 64];
        for (i, part) in chunk.chunks(4).enumerate() {
            w[i] = u32::from_be_bytes([part[0], part[1], part[2], part[3]]);
        }
        for i in 16..64 {
            let s0 = w[i - 15].rotate_right(7) ^ w[i - 15].rotate_right(18) ^ (w[i - 15] >> 3);
            let s1 = w[i - 2].rotate_right(17) ^ w[i - 2].rotate_right(19) ^ (w[i - 2] >> 10);
            w[i] = w[i - 16]
                .wrapping_add(s0)
                .wrapping_add(w[i - 7])
                .wrapping_add(s1);
        }

        let mut a = h0;
        let mut b = h1;
        let mut c = h2;
        let mut d = h3;
        let mut e = h4;
        let mut f = h5;
        let mut g = h6;
        let mut h = h7;

        for i in 0..64 {
            let s1 = e.rotate_right(6) ^ e.rotate_right(11) ^ e.rotate_right(25);
            let ch = (e & f) ^ ((!e) & g);
            let temp1 = h
                .wrapping_add(s1)
                .wrapping_add(ch)
                .wrapping_add(k[i])
                .wrapping_add(w[i]);
            let s0 = a.rotate_right(2) ^ a.rotate_right(13) ^ a.rotate_right(22);
            let maj = (a & b) ^ (a & c) ^ (b & c);
            let temp2 = s0.wrapping_add(maj);

            h = g;
            g = f;
            f = e;
            e = d.wrapping_add(temp1);
            d = c;
            c = b;
            b = a;
            a = temp1.wrapping_add(temp2);
        }

        h0 = h0.wrapping_add(a);
        h1 = h1.wrapping_add(b);
        h2 = h2.wrapping_add(c);
        h3 = h3.wrapping_add(d);
        h4 = h4.wrapping_add(e);
        h5 = h5.wrapping_add(f);
        h6 = h6.wrapping_add(g);
        h7 = h7.wrapping_add(h);
    }

    format!(
        "{:08x}{:08x}{:08x}{:08x}{:08x}{:08x}{:08x}{:08x}",
        h0, h1, h2, h3, h4, h5, h6, h7
    )
}

/// Wire framing reader: reads a 4-byte LE length prefix followed by exact payload bytes.
pub async fn read_framed_message<R: AsyncRead + Unpin>(
    reader: &mut R,
) -> Result<Vec<u8>, BridgeError> {
    let mut len_buf = [0u8; 4];
    reader.read_exact(&mut len_buf).await?;
    let length = u32::from_le_bytes(len_buf) as usize;
    if !(2..=MAXIMUM_BYTES).contains(&length) {
        return Err(BridgeError::SizeBudgetExceeded(length));
    }
    let mut payload = vec![0u8; length];
    reader.read_exact(&mut payload).await?;
    Ok(payload)
}

/// Wire framing writer: writes 4-byte LE length prefix followed by payload bytes.
pub async fn write_framed_message<W: AsyncWrite + Unpin>(
    writer: &mut W,
    bytes: &[u8],
) -> Result<(), BridgeError> {
    let length = bytes.len();
    if !(2..=MAXIMUM_BYTES).contains(&length) {
        return Err(BridgeError::SizeBudgetExceeded(length));
    }
    let len_buf = (length as u32).to_le_bytes();
    writer.write_all(&len_buf).await?;
    writer.write_all(bytes).await?;
    writer.flush().await?;
    Ok(())
}

/// Dispatches a single read-only command over an async Stream, validating frames and schema.
pub async fn call_stream<S: AsyncRead + AsyncWrite + Unpin>(
    stream: &mut S,
    command: &str,
    arguments: Option<serde_json::Value>,
    request_id: Option<String>,
) -> Result<BridgeReply, BridgeError> {
    match command {
        "capabilities" | "catalog" | "state" | "snapshot" | "facts" => {}
        _ => return Err(BridgeError::UnsupportedCommand(command.to_string())),
    }

    let req = BridgeRequest {
        command: command.to_string(),
        arguments: arguments.unwrap_or_else(|| serde_json::json!({})),
        request_id,
    };

    let bytes = serde_json::to_vec(&req)?;
    write_framed_message(stream, &bytes).await?;

    let reply_bytes = read_framed_message(stream).await?;
    let reply: BridgeReply = serde_json::from_slice(&reply_bytes)?;

    if reply.schema_version != Some(1) {
        return Err(BridgeError::InvalidReplySchema);
    }

    if !reply.ok {
        let err = reply
            .error
            .unwrap_or_else(|| "Unknown native owner error".into());
        return Err(BridgeError::OwnerError(err));
    }

    Ok(reply)
}

/// Preserved typed facts from native backend snapshot envelope.
#[derive(Debug, Clone, Default, Serialize, Deserialize, PartialEq)]
pub struct NativeHostState {
    pub connected: bool,
    pub status: String,
    pub game_id: Option<String>,
    pub target_id: Option<String>,
    pub source: Option<String>,
    pub wizard_profile_id: Option<String>,
    pub wizard_name: Option<String>,
    pub wizard_school: Option<String>,
    pub wizard_level: Option<i32>,
    pub wizard_generation: Option<i64>,
    pub gold: Option<i64>,
    pub health_current: Option<i32>,
    pub health_max: Option<i32>,
    pub mana_current: Option<i32>,
    pub mana_max: Option<i32>,
    pub energy_current: Option<i32>,
    pub energy_max: Option<i32>,
    pub potions_current: Option<f64>,
    pub potions_max: Option<f64>,
    pub last_observed_utc: Option<String>,
    pub unavailable_facts: Vec<String>,
    pub is_simulation: bool,
    pub native_idle: Option<bool>,
    pub raw_snapshot: serde_json::Value,
    pub wizard_subject: Option<serde_json::Value>,
}

impl NativeHostState {
    /// Only the owner's fresh, typed aliases become current values. Raw facts
    /// remain inspectable but never bypass freshness or identity qualification.
    pub fn from_snapshot_json(val: &serde_json::Value) -> Self {
        Self::from_snapshot_json_at(val, SystemTime::now())
    }

    pub fn observation_is_fresh(&self, now: SystemTime) -> bool {
        self.connected
            && self
                .last_observed_utc
                .as_deref()
                .is_some_and(|stamp| timestamp_fresh(stamp, now))
    }

    pub fn from_snapshot_json_at(val: &serde_json::Value, now: SystemTime) -> Self {
        let connected = val["connected"].as_bool().unwrap_or(false);
        let facts = &val["facts"];
        let text = |key: &str| facts.get(key).and_then(|v| v.as_str()).map(str::to_owned);
        let target = text("targetId");
        let source = text("source");
        let observed = text("observedUtc");
        let game = text("gameId");
        let alias_value = |key: &str| -> Option<&serde_json::Value> {
            if !connected
                || target.as_deref().is_none_or(str::is_empty)
                || observed
                    .as_deref()
                    .is_none_or(|stamp| !timestamp_fresh(stamp, now))
                || source.as_deref().is_none_or(str::is_empty)
                || game.as_deref().is_none_or(str::is_empty)
            {
                return None;
            }
            let entries = val["aliases"].as_array()?;
            let mut found = entries.iter().filter(|a| a["id"].as_str() == Some(key));
            let a = found.next()?;
            if found.next().is_some()
                || a["quality"] != "fresh"
                || a["targetId"].as_str() != target.as_deref()
                || a["observedUtc"].as_str() != observed.as_deref()
                || a["source"].as_str() != source.as_deref()
                || a["gameId"].as_str() != game.as_deref()
            {
                return None;
            }
            let expected = match key {
                "nativeIdle" => "BOOL",
                "gold.current" | "wizard.level" | "health.max" | "mana.max" | "energy.max" => {
                    "DINT"
                }
                "health" | "mana" | "energy" | "potions" | "potions.max" => "REAL",
                _ => return None,
            };
            if a["dataType"].as_str() != Some(expected) {
                return None;
            }
            let value = a.get("value")?;
            let valid = match a["dataType"].as_str()? {
                "BOOL" => value.is_boolean(),
                "STRING" => value.is_string(),
                "INT" => value.as_i64().is_some_and(|n| i16::try_from(n).is_ok()),
                "DINT" => value.as_i64().is_some_and(|n| i32::try_from(n).is_ok()),
                "REAL" => value.as_f64().is_some_and(f64::is_finite),
                _ => false,
            };
            valid.then_some(value)
        };
        let integer = |key: &str| -> Option<i32> {
            let v = alias_value(key)?;
            if let Some(n) = v.as_i64() {
                return i32::try_from(n).ok();
            }
            let n = v.as_f64()?;
            (n.is_finite() && n.fract() == 0.0 && n >= i32::MIN as f64 && n <= i32::MAX as f64)
                .then_some(n as i32)
        };
        let real = |key: &str| alias_value(key).and_then(|v| v.as_f64());
        let idle = alias_value("nativeIdle").and_then(|v| v.as_bool());
        let subject = (integer("wizard.level").is_some() || integer("gold.current").is_some())
            .then(|| {
                facts
                    .get("wizardSubject")
                    .filter(|v| v.is_object())
                    .cloned()
            })
            .flatten();
        let subject_text = |key: &str| {
            subject
                .as_ref()
                .and_then(|s| s[key].as_str())
                .map(str::to_owned)
        };
        Self {
            connected,
            status: match (connected, idle) {
                (false, _) => "Disconnected / Host Unavailable",
                (true, Some(true)) => "Connected (nativeIdle)",
                (true, Some(false)) => "Connected (Active)",
                _ => "Connected (Unknown Idle)",
            }
            .into(),
            game_id: game.clone().filter(|_| connected),
            target_id: target.clone().filter(|_| connected),
            source: source.clone().filter(|_| connected),
            wizard_profile_id: subject_text("profileId"),
            wizard_name: subject_text("name"),
            wizard_school: subject_text("school"),
            wizard_generation: subject.as_ref().and_then(|s| s["generation"].as_i64()),
            wizard_level: integer("wizard.level"),
            gold: integer("gold.current").map(i64::from),
            health_current: integer("health"),
            health_max: integer("health.max"),
            mana_current: integer("mana"),
            mana_max: integer("mana.max"),
            energy_current: integer("energy"),
            energy_max: integer("energy.max"),
            potions_current: real("potions"),
            potions_max: real("potions.max"),
            last_observed_utc: observed.clone().filter(|_| connected),
            unavailable_facts: val["unavailable"]
                .as_array()
                .map(|a| {
                    a.iter()
                        .filter_map(|v| v.as_str().map(str::to_owned))
                        .collect()
                })
                .unwrap_or_default(),
            native_idle: idle,
            raw_snapshot: val.clone(),
            wizard_subject: subject,
            is_simulation: false,
        }
    }

    /// Identity excludes level and per-observation timestamps.
    pub fn identity_key(&self) -> Option<String> {
        let s = self.wizard_subject.as_ref()?;
        Some(format!(
            "{}|{}|{}|{}",
            self.target_id.as_deref()?,
            s["profileId"].as_str()?,
            s["generation"].as_i64()?,
            s["verifiedCharacterId"].as_str()?
        ))
    }

    /// Projects qualified native host telemetry facts directly into the W101 Behavior Tree Blackboard.
    pub fn update_blackboard(&self, bb: &mut W101Blackboard) {
        if !self.connected {
            bb.status_log
                .push(String::from("Host disconnected: telemetry invalidated"));
            bb.in_combat = false;
            bb.has_potion = false;
            return;
        }

        if let (Some(cur), Some(max)) = (self.mana_current, self.mana_max) {
            if max > 0 {
                bb.mana_pct = (cur as f64) / (max as f64);
            }
        }

        if let (Some(cur), Some(max)) = (self.health_current, self.health_max) {
            if max > 0 {
                bb.health_pct = (cur as f64) / (max as f64);
            }
        }

        if let Some(potions) = self.potions_current {
            bb.has_potion = potions >= 1.0;
        }

        // Check raw snapshot facts for combat, zone, and spatial coordinates
        if let Some(facts) = self.raw_snapshot.get("facts").and_then(|f| f.as_object()) {
            if let Some(combat) = facts.get("combatActive").and_then(|v| v.as_bool()) {
                bb.in_combat = combat;
            }
            if let Some(zone) = facts.get("zoneName").and_then(|v| v.as_str()) {
                bb.current_zone = zone.to_string();
            }
            if let Some(pos) = facts.get("spatialPosition").and_then(|v| v.as_array()) {
                if pos.len() >= 3 {
                    let x = pos[0].as_f64().unwrap_or(0.0);
                    let y = pos[1].as_f64().unwrap_or(0.0);
                    let z = pos[2].as_f64().unwrap_or(0.0);
                    bb.current_pose = (x, y, z);
                }
            }
        }
    }
}

/// Client handle managing native host communication, fact invalidation, and non-overlapping polling.
pub struct MaelstromNativeBridge {
    pub data_root: PathBuf,
    pub pipe_name: String,
    pub latest_state: NativeHostState,
}

impl MaelstromNativeBridge {
    pub fn new<P: AsRef<Path>>(data_root: P) -> Result<Self, BridgeError> {
        let path = data_root.as_ref().to_path_buf();
        let pipe = compute_pipe_name(&path)?;
        Ok(Self {
            data_root: path,
            pipe_name: pipe,
            latest_state: NativeHostState::default(),
        })
    }

    /// Invalidate live state on disconnect, timeout, or empty result, preventing stale facts from appearing live.
    pub fn mark_disconnected(&mut self, reason: &str) {
        self.latest_state = NativeHostState {
            status: format!("Disconnected ({})", reason),
            ..Default::default()
        };
    }

    #[cfg(windows)]
    pub async fn poll_snapshot(&mut self) -> Result<NativeHostState, BridgeError> {
        // The mutable borrow serializes calls. Clear before awaiting so dropping
        // this future cannot leave a previous snapshot looking current.
        self.mark_disconnected("Refreshing observation");
        let res = self.execute_snapshot_poll().await;

        match res {
            Ok(state) => {
                self.latest_state = state.clone();
                Ok(state)
            }
            Err(e) => {
                self.mark_disconnected(&e.to_string());
                Err(e)
            }
        }
    }

    #[cfg(windows)]
    async fn execute_snapshot_poll(&mut self) -> Result<NativeHostState, BridgeError> {
        use tokio::time::timeout;

        let client = connect_pipe(&self.pipe_name, Arc::new(AtomicBool::new(false))).await?;

        let mut stream = client;
        let reply = timeout(
            DEFAULT_TIMEOUT,
            call_stream(&mut stream, "snapshot", None, None),
        )
        .await
        .map_err(|_| BridgeError::Timeout)??;

        decode_snapshot(reply)
    }

    #[cfg(not(windows))]
    pub async fn poll_snapshot(&mut self) -> Result<NativeHostState, BridgeError> {
        self.mark_disconnected("Named pipes only supported on Windows");
        Err(BridgeError::Io(io::Error::new(
            io::ErrorKind::Unsupported,
            "Named pipes only supported on Windows",
        )))
    }
}

/// Serializable summary of a SteeringCommand intent for host IPC logging and replay.
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct SteeringCommandSummary {
    pub forward_pulse_ms: u32,
    pub turn_left_pulse_ms: u32,
    pub turn_right_pulse_ms: u32,
    pub reverse_pulse_ms: u32,
    pub curvature: f64,
    pub angle_error_rad: f64,
    pub is_arrived: bool,
}

impl From<SteeringCommand> for SteeringCommandSummary {
    fn from(cmd: SteeringCommand) -> Self {
        Self {
            forward_pulse_ms: cmd.forward_pulse_ms,
            turn_left_pulse_ms: cmd.turn_left_pulse_ms,
            turn_right_pulse_ms: cmd.turn_right_pulse_ms,
            reverse_pulse_ms: cmd.reverse_pulse_ms,
            curvature: cmd.curvature,
            angle_error_rad: cmd.angle_error_rad,
            is_arrived: cmd.is_arrived,
        }
    }
}

/// Serializable summary of a KinematicPlan intent for host IPC logging and replay.
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct KinematicPlanSummary {
    pub point_count: usize,
    pub total_duration_ms: f64,
    pub dwell_ms: f64,
    pub target_x: f64,
    pub target_y: f64,
}

impl From<&KinematicPlan> for KinematicPlanSummary {
    fn from(plan: &KinematicPlan) -> Self {
        let end = plan.end_point().unwrap_or(Point2D::new(0.0, 0.0));
        Self {
            point_count: plan.len(),
            total_duration_ms: plan.total_duration_ms,
            dwell_ms: plan.dwell_ms,
            target_x: end.x,
            target_y: end.y,
        }
    }
}

/// High-level output of one complete perception-decision cycle evaluated by MaelstromAutonomyBridge.
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct AutonomyCycleResult {
    pub connected: bool,
    pub bt_status: String,
    pub proposed_intent: Option<String>,
    pub steering_command: Option<SteeringCommandSummary>,
    pub kinematic_plan: Option<KinematicPlanSummary>,
    pub current_zone: String,
    pub health_pct: f64,
    pub mana_pct: f64,
    pub has_potion: bool,
    pub in_combat: bool,
    pub status_log: Vec<String>,
}

/// High-performance autonomy bridge coupling the named-pipe client to the native Behavior Tree,
/// Pure Pursuit steering controller, and kinematic trajectory generator.
pub struct MaelstromAutonomyBridge {
    pub bridge: MaelstromNativeBridge,
    pub blackboard: W101Blackboard,
    pub behavior_tree: Selector<W101Blackboard>,
    pub steering_controller: PurePursuitController,
    pub prng: FastPrng,
}

impl MaelstromAutonomyBridge {
    pub fn new<P: AsRef<Path>>(data_root: P) -> Result<Self, BridgeError> {
        let bridge = MaelstromNativeBridge::new(data_root)?;
        let blackboard = W101Blackboard::new();
        let behavior_tree = build_w101_canonical_tree();
        let steering_controller = PurePursuitController::new(25.0, std::f64::consts::PI, 10.0);
        let prng = FastPrng::new(0x20261118);

        Ok(Self {
            bridge,
            blackboard,
            behavior_tree,
            steering_controller,
            prng,
        })
    }

    /// Pure, offline synchronous processing of a NativeHostState snapshot into an AutonomyCycleResult.
    /// Does not perform I/O; suitable for deterministic testing and replay.
    pub fn process_state(&mut self, state: &NativeHostState) -> AutonomyCycleResult {
        state.update_blackboard(&mut self.blackboard);

        if !state.connected {
            return AutonomyCycleResult {
                connected: false,
                bt_status: "Failure".into(),
                proposed_intent: None,
                steering_command: None,
                kinematic_plan: None,
                current_zone: self.blackboard.current_zone.clone(),
                health_pct: self.blackboard.health_pct,
                mana_pct: self.blackboard.mana_pct,
                has_potion: self.blackboard.has_potion,
                in_combat: self.blackboard.in_combat,
                status_log: self.blackboard.status_log.clone(),
            };
        }

        let status = self.behavior_tree.tick(&mut self.blackboard);
        let bt_status_str = match status {
            BtStatus::Success => "Success",
            BtStatus::Failure => "Failure",
            BtStatus::Running => "Running",
        }
        .to_string();

        let mut steering_summary = None;
        let mut kinematic_summary = None;

        if let Some(ref intent) = self.blackboard.proposal_channel {
            if intent == "PROPOSAL:StepWaypoint" || intent == "PROPOSAL:FollowWaypoints" {
                let pose = Pose2D::new(
                    self.blackboard.current_pose.0,
                    self.blackboard.current_pose.1,
                    self.blackboard.current_pose.2,
                );
                let path: Vec<Vec2D> = self
                    .blackboard
                    .active_waypoints
                    .iter()
                    .map(|w| Vec2D::new(w.0, w.1))
                    .collect();

                if !path.is_empty() {
                    let cmd = self.steering_controller.compute_steering(&pose, &path);
                    steering_summary = Some(SteeringCommandSummary::from(cmd));
                }
            } else if intent.starts_with("PROPOSAL:CombatTurn") {
                // Generate humanized combat click trajectory targeting calibrated 1280x720 viewport bounds
                let (target, target_width) = if intent == "PROPOSAL:CombatTurn:Pass" {
                    let (pass_x, pass_y) = CombatGeometry::get_pass_button_coordinates_720p();
                    let t = sample_target_in_rect(
                        pass_x - 18.0,
                        pass_y - 10.0,
                        36.0,
                        20.0,
                        0.15,
                        &mut self.prng,
                    );
                    (t, 36.0)
                } else if let Some(target_idx_str) =
                    intent.strip_prefix("PROPOSAL:CombatTurn:SelectTarget:")
                {
                    let idx: usize = target_idx_str.parse().unwrap_or(0);
                    let (tx, ty) = CombatGeometry::calculate_target_coordinates_720p(idx)
                        .unwrap_or((208.0, 60.0));
                    let t = sample_target_in_rect(
                        tx - 25.0,
                        ty - 25.0,
                        50.0,
                        50.0,
                        0.15,
                        &mut self.prng,
                    );
                    (t, 50.0)
                } else {
                    // Card slot in hand (e.g. "PROPOSAL:CombatTurn:PlayCard:0/4" or generic "PROPOSAL:CombatTurn")
                    let (slot, total) = if let Some(card_spec) =
                        intent.strip_prefix("PROPOSAL:CombatTurn:PlayCard:")
                    {
                        let parts: Vec<&str> = card_spec.split('/').collect();
                        let s = parts.first().and_then(|v| v.parse().ok()).unwrap_or(0);
                        let tot = parts.get(1).and_then(|v| v.parse().ok()).unwrap_or(4);
                        (s, tot)
                    } else {
                        (0, 4)
                    };
                    let (card_x, card_y) =
                        CombatGeometry::get_hand_card_coordinates_720p(slot, total)
                            .unwrap_or((688.0, 390.0));
                    let t = sample_target_in_rect(
                        card_x - 30.0,
                        card_y - 35.0,
                        60.0,
                        70.0,
                        0.15,
                        &mut self.prng,
                    );
                    (t, 60.0)
                };

                let start = Point2D::new(
                    self.blackboard.current_pose.0,
                    self.blackboard.current_pose.1,
                );
                let fitts = FittsLawModel::default();
                let dur = fitts.calculate_duration_ms(start.distance(&target), target_width);
                let cfg = BezierConfig::default();
                let plan = BezierTrajectoryGenerator::generate_trajectory(
                    start,
                    target,
                    dur,
                    &cfg,
                    &mut self.prng,
                );
                kinematic_summary = Some(KinematicPlanSummary::from(&plan));
            }
        }

        AutonomyCycleResult {
            connected: true,
            bt_status: bt_status_str,
            proposed_intent: self.blackboard.proposal_channel.clone(),
            steering_command: steering_summary,
            kinematic_plan: kinematic_summary,
            current_zone: self.blackboard.current_zone.clone(),
            health_pct: self.blackboard.health_pct,
            mana_pct: self.blackboard.mana_pct,
            has_potion: self.blackboard.has_potion,
            in_combat: self.blackboard.in_combat,
            status_log: self.blackboard.status_log.clone(),
        }
    }

    /// Performs a single perception-decision tick over the named pipe.
    #[cfg(windows)]
    pub async fn step_cycle(&mut self) -> Result<AutonomyCycleResult, BridgeError> {
        let state = self.bridge.poll_snapshot().await?;
        Ok(self.process_state(&state))
    }

    #[cfg(not(windows))]
    pub async fn step_cycle(&mut self) -> Result<AutonomyCycleResult, BridgeError> {
        Err(BridgeError::Io(io::Error::new(
            io::ErrorKind::Unsupported,
            "Named pipes only supported on Windows",
        )))
    }
}

/// Cancellation drops the pending I/O future. Discard that stream afterwards:
/// a cancelled partial frame must never be reused for a later request.
pub async fn call_stream_bounded<S: AsyncRead + AsyncWrite + Unpin>(
    stream: &mut S,
    command: &str,
    budget: Duration,
    cancelled: Arc<AtomicBool>,
) -> Result<BridgeReply, BridgeError> {
    if cancelled.load(Ordering::Acquire) {
        return Err(BridgeError::Cancelled);
    }
    tokio::select! {
        biased;
        _ = async {
            loop {
                if cancelled.load(Ordering::Acquire) { break; }
                tokio::time::sleep(Duration::from_millis(20)).await;
            }
        } => Err(BridgeError::Cancelled),
        result = tokio::time::timeout(budget, call_stream(stream, command, None, None)) => {
            if cancelled.load(Ordering::Acquire) { return Err(BridgeError::Cancelled); }
            result.map_err(|_| BridgeError::Timeout)?
        }
    }
}

/// One read-only request on a serialized worker, not on the UI thread.
/// No host/game startup or input; retries only pre-request busy opens.
pub fn poll_snapshot_blocking(
    data_root: PathBuf,
    cancelled: Arc<AtomicBool>,
) -> Result<NativeHostState, BridgeError> {
    if cancelled.load(Ordering::Acquire) {
        return Err(BridgeError::Cancelled);
    }
    let bridge = MaelstromNativeBridge::new(data_root)?;
    #[cfg(windows)]
    {
        let runtime = tokio::runtime::Builder::new_current_thread()
            .enable_all()
            .build()?;
        runtime.block_on(async {
            let mut stream = connect_pipe(&bridge.pipe_name, cancelled.clone()).await?;
            let reply =
                call_stream_bounded(&mut stream, "snapshot", DEFAULT_TIMEOUT, cancelled).await?;
            decode_snapshot(reply)
        })
    }
    #[cfg(not(windows))]
    {
        let _ = bridge;
        Err(BridgeError::Io(io::Error::new(
            io::ErrorKind::Unsupported,
            "Named pipes only supported on Windows",
        )))
    }
}

/// Strict UTC parser for native System.Text.Json timestamps (0..7 fractional digits).
/// Unsupported offsets/formats remain unknown rather than guessed.
fn timestamp_fresh(stamp: &str, now: SystemTime) -> bool {
    parse_utc(stamp)
        .and_then(|time| now.duration_since(time).ok())
        .is_some_and(|age| age <= Duration::from_secs(2))
}
fn parse_utc(stamp: &str) -> Option<SystemTime> {
    let b = stamp.as_bytes();
    if !stamp.is_ascii()
        || b.len() < 20
        || b[4] != b'-'
        || b[7] != b'-'
        || b[10] != b'T'
        || b[13] != b':'
        || b[16] != b':'
        || *b.last()? != b'Z'
    {
        return None;
    }
    let num = |start: usize, end: usize| -> Option<i64> {
        let slice = stamp.get(start..end)?;
        if !slice.bytes().all(|v| v.is_ascii_digit()) {
            return None;
        }
        slice.parse().ok()
    };
    let year = num(0, 4)?;
    let month = num(5, 7)?;
    let day = num(8, 10)?;
    let hour = num(11, 13)?;
    let minute = num(14, 16)?;
    let second = num(17, 19)?;
    let leap = year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
    let days = match month {
        1 | 3 | 5 | 7 | 8 | 10 | 12 => 31,
        4 | 6 | 9 | 11 => 30,
        2 => {
            if leap {
                29
            } else {
                28
            }
        }
        _ => return None,
    };
    if !(1970..=9999).contains(&year)
        || day < 1
        || day > days
        || hour > 23
        || minute > 59
        || second > 59
    {
        return None;
    }
    let nanos = if b.len() == 20 {
        0
    } else {
        if b[19] != b'.' || !(22..=28).contains(&b.len()) {
            return None;
        }
        let digits = b.len() - 21;
        num(20, b.len() - 1)? as u32 * 10u32.pow(9 - digits as u32)
    };
    // Gregorian civil date to days since Unix epoch.
    let y = year - i64::from(month <= 2);
    let era = y / 400;
    let yoe = y - era * 400;
    let mp = month + if month > 2 { -3 } else { 9 };
    let doy = (153 * mp + 2) / 5 + day - 1;
    let doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;
    let days = era * 146097 + doe - 719468;
    let seconds = u64::try_from(days * 86400 + hour * 3600 + minute * 60 + second).ok()?;
    UNIX_EPOCH.checked_add(Duration::new(seconds, nanos))
}

fn decode_snapshot(reply: BridgeReply) -> Result<NativeHostState, BridgeError> {
    let value = reply
        .result
        .filter(|v| v.is_object())
        .ok_or_else(|| BridgeError::OwnerError("Snapshot result empty or invalid".into()))?;
    Ok(NativeHostState::from_snapshot_json(&value))
}

#[cfg(test)]
mod snapshot_result_tests {
    use super::*;
    #[test]
    fn both_poll_paths_reject_missing_or_non_object_snapshots() {
        for result in [
            None,
            Some(serde_json::json!(null)),
            Some(serde_json::json!([])),
            Some(serde_json::json!(12)),
        ] {
            assert!(decode_snapshot(BridgeReply {
                ok: true,
                result,
                error: None,
                schema_version: Some(1)
            })
            .is_err());
        }
        assert!(
            !decode_snapshot(BridgeReply {
                ok: true,
                result: Some(serde_json::json!({"connected":false})),
                error: None,
                schema_version: Some(1)
            })
            .unwrap()
            .connected
        );
    }
}
