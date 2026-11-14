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
use std::time::Duration;
use thiserror::Error;
use tokio::io::{AsyncRead, AsyncReadExt, AsyncWrite, AsyncWriteExt};

pub const MAXIMUM_BYTES: usize = 262_144;
pub const DEFAULT_TIMEOUT: Duration = Duration::from_secs(8);
pub const CONNECT_TIMEOUT: Duration = Duration::from_millis(1500);

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
    #[error("Invalid reply schema (schemaVersion != 1 or missing ok flag)")]
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
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct BridgeReply {
    pub ok: bool,
    #[serde(default)]
    pub result: Option<serde_json::Value>,
    #[serde(default)]
    pub error: Option<String>,
    #[serde(rename = "schemaVersion", default = "default_schema_version")]
    pub schema_version: i32,
}

fn default_schema_version() -> i32 {
    1
}

/// Compute the exact named pipe name matching `JobDeckWire.PipeName(dataRoot)`.
/// `maelstrom-deck-` + hex(sha256(uppercase(trimmed_full_path)))[..24]
pub fn compute_pipe_name<P: AsRef<Path>>(data_root: P) -> Result<String, BridgeError> {
    let full = data_root.as_ref().canonicalize()
        .or_else(|_| {
            let p = data_root.as_ref();
            if p.is_absolute() {
                Ok(p.to_path_buf())
            } else {
                std::env::current_dir().map(|cwd| cwd.join(p))
            }
        })
        .map_err(|e| BridgeError::InvalidDataRoot(e.to_string()))?;

    let s = full.to_string_lossy();
    // Trim ending directory separator
    let trimmed = s.trim_end_matches(['\\', '/']);
    // Windows paths: strip \\?\ verbatim prefix if present for canonical uppercase
    let normalized = trimmed.strip_prefix(r"\\?\").unwrap_or(trimmed);
    let upper = normalized.to_ascii_uppercase();

    // Compute SHA-256 in pure Rust without external crypto crates:
    // We can use standard sha256 via maelstrom_core or compute manual/flate2 hash,
    // or format sha256 bytes.
    let hash_hex = sha256_hex(upper.as_bytes());
    let prefix = &hash_hex[..24];
    Ok(format!(r"\\.\pipe\maelstrom-deck-{}", prefix.to_ascii_uppercase()))
}

/// Helper SHA-256 implementation avoiding external crate bloat if not installed.
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
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
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
            w[i] = w[i - 16].wrapping_add(s0).wrapping_add(w[i - 7]).wrapping_add(s1);
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
            let temp1 = h.wrapping_add(s1).wrapping_add(ch).wrapping_add(k[i]).wrapping_add(w[i]);
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
pub async fn read_framed_message<R: AsyncRead + Unpin>(reader: &mut R) -> Result<Vec<u8>, BridgeError> {
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
pub async fn write_framed_message<W: AsyncWrite + Unpin>(writer: &mut W, bytes: &[u8]) -> Result<(), BridgeError> {
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
    // Enforce read-only commands
    match command {
        "capabilities" | "catalog" | "state" | "snapshot" => {}
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

    if reply.schema_version != 1 {
        return Err(BridgeError::InvalidReplySchema);
    }

    if !reply.ok {
        let err = reply.error.unwrap_or_else(|| "Unknown native owner error".into());
        return Err(BridgeError::OwnerError(err));
    }

    Ok(reply)
}

/// Snapshot fact container representing preserved typed facts from native backend.
#[derive(Debug, Clone, Default, Serialize, Deserialize, PartialEq)]
pub struct NativeHostState {
    pub connected: bool,
    pub status: String,
    pub wizard_name: Option<String>,
    pub wizard_school: Option<String>,
    pub wizard_level: Option<i32>,
    pub gold: Option<i64>,
    pub crowns: Option<i64>,
    pub health_current: Option<i32>,
    pub health_max: Option<i32>,
    pub mana_current: Option<i32>,
    pub mana_max: Option<i32>,
    pub energy_current: Option<i32>,
    pub energy_max: Option<i32>,
    pub location_world: Option<String>,
    pub location_zone: Option<String>,
    pub quest_name: Option<String>,
    pub quest_goal: Option<String>,
    pub last_observed_utc: Option<String>,
    pub unavailable_facts: Vec<String>,
    pub is_simulation: bool,
}

impl NativeHostState {
    /// Ingests a raw `snapshot` or `state` result from the native host without inventing defaults.
    pub fn from_snapshot_json(val: &serde_json::Value) -> Self {
        let facts = val.get("facts").and_then(|f| f.as_object());
        let aliases = val.get("aliases").and_then(|a| a.as_object());

        let extract_fact_str = |key: &str| -> Option<String> {
            facts.and_then(|f| f.get(key)).and_then(|v| v.get("value")).and_then(|s| s.as_str()).map(String::from)
                .or_else(|| aliases.and_then(|a| a.get(key)).and_then(|s| s.as_str()).map(String::from))
        };

        let extract_fact_i64 = |key: &str| -> Option<i64> {
            facts.and_then(|f| f.get(key)).and_then(|v| v.get("value")).and_then(|s| s.as_i64())
                .or_else(|| aliases.and_then(|a| a.get(key)).and_then(|s| s.as_i64()))
        };

        let unavailable = val.get("unavailable")
            .and_then(|u| u.as_array())
            .map(|arr| arr.iter().filter_map(|v| v.as_str().map(String::from)).collect())
            .unwrap_or_default();

        let connected = val.get("connected").and_then(|c| c.as_bool()).unwrap_or(false);
        let last_obs = facts.and_then(|f| f.get("observedUtc")).and_then(|v| v.as_str()).map(String::from);

        Self {
            connected,
            status: if connected { "Connected (nativeIdle)".into() } else { "Disconnected / Host Unavailable".into() },
            wizard_name: extract_fact_str("wizard.name").or_else(|| extract_fact_str("character.name")),
            wizard_school: extract_fact_str("wizard.school"),
            wizard_level: extract_fact_i64("wizard.level").map(|v| v as i32),
            gold: extract_fact_i64("gold").or_else(|| extract_fact_i64("currency.gold")),
            crowns: extract_fact_i64("crowns").or_else(|| extract_fact_i64("currency.crowns")),
            health_current: extract_fact_i64("health.current").map(|v| v as i32),
            health_max: extract_fact_i64("health.max").map(|v| v as i32),
            mana_current: extract_fact_i64("mana.current").map(|v| v as i32),
            mana_max: extract_fact_i64("mana.max").map(|v| v as i32),
            energy_current: extract_fact_i64("energy.current").map(|v| v as i32),
            energy_max: extract_fact_i64("energy.max").map(|v| v as i32),
            location_world: extract_fact_str("location.world"),
            location_zone: extract_fact_str("location.zone").or_else(|| extract_fact_str("location.area")),
            quest_name: extract_fact_str("quest.name"),
            quest_goal: extract_fact_str("quest.goal"),
            last_observed_utc: last_obs,
            unavailable_facts: unavailable,
            is_simulation: false,
        }
    }
}

/// Client handle for managing native host communication and bounded 1-second polling.
pub struct MaelstromNativeBridge {
    pub data_root: PathBuf,
    pub pipe_name: String,
    pub latest_state: NativeHostState,
    pub is_polling: bool,
}

impl MaelstromNativeBridge {
    pub fn new<P: AsRef<Path>>(data_root: P) -> Result<Self, BridgeError> {
        let path = data_root.as_ref().to_path_buf();
        let pipe = compute_pipe_name(&path)?;
        Ok(Self {
            data_root: path,
            pipe_name: pipe,
            latest_state: NativeHostState::default(),
            is_polling: false,
        })
    }

    #[cfg(windows)]
    pub async fn poll_snapshot(&mut self) -> Result<NativeHostState, BridgeError> {
        use tokio::net::windows::named_pipe::ClientOptions;
        use tokio::time::timeout;

        let client = ClientOptions::new()
            .open(&self.pipe_name)
            .map_err(BridgeError::Io)?;

        let mut stream = client;
        let reply = timeout(CONNECT_TIMEOUT + DEFAULT_TIMEOUT, call_stream(&mut stream, "snapshot", None, None))
            .await
            .map_err(|_| BridgeError::Timeout)??;

        if let Some(res) = reply.result {
            let state = NativeHostState::from_snapshot_json(&res);
            self.latest_state = state.clone();
            Ok(state)
        } else {
            Err(BridgeError::OwnerError("Snapshot result empty".into()))
        }
    }

    #[cfg(not(windows))]
    pub async fn poll_snapshot(&mut self) -> Result<NativeHostState, BridgeError> {
        Err(BridgeError::Io(io::Error::new(io::ErrorKind::Unsupported, "Named pipes only supported on Windows")))
    }
}
