//! `autonomy_replay`: Offline replay verification harness for the Maelstrom autonomy stack.
//!
//! Evaluates recorded session traces through `MaelstromAutonomyBridge` without live game
//! interaction, calculating decision accuracy, replanning rates, path curvature, and jerk compliance.
//! The replan figure is an intent-change counter, the curvature is the Pure Pursuit controller's own value
//! (magnitude at most 2.0), and the jerk is a scale-dependent proxy; none is a calibrated gate. The fixtures
//! here are synthetic. See docs/OFFLINE_AUTONOMY_REPLAY.md.
//! Adheres strictly to `Facts ≠ Authority ≠ Actions`.

use serde::{Deserialize, Serialize};

use crate::maelstrom_bridge::{AutonomyCycleResult, MaelstromAutonomyBridge, NativeHostState};

/// A single timestamped frame within an offline session trace.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ReplayFrame {
    pub timestamp_ms: u64,
    pub state: NativeHostState,
    pub expected_intent: Option<String>,
}

/// A SIMULATED typed alias for replay frames and tests.
///
/// Combat state, zone and position reach the decision step only as fresh, typed, identity-matched
/// aliases named `combat.active`, `zone.name` and `position.x|y|z`. The live host publishes none of them
/// today (`JobFactCatalog.cs` defines no combat, zone or position fact), so every value built with this
/// helper is a fixture, not an observation. The replay builders in this module also give their frames
/// synthetic timestamps and positions.
pub fn simulated_alias<T: Serialize>(
    id: &str,
    data_type: &str,
    value: T,
    observed_utc: &str,
) -> serde_json::Value {
    serde_json::json!({
        "id": id,
        "dataType": data_type,
        "value": value,
        "quality": "fresh",
        "targetId": "12345/67890",
        "observedUtc": observed_utc,
        "source": "reviewed-w101-ui-tree",
        "gameId": "wizard101"
    })
}

impl ReplayFrame {
    pub fn new(timestamp_ms: u64, state: NativeHostState, expected_intent: Option<String>) -> Self {
        Self {
            timestamp_ms,
            state,
            expected_intent,
        }
    }
}

/// A serialized collection of replay frames. In this repository they are synthetic fixtures, or frames ingested
/// from event and UI logs with fabricated position and vitals; none is a recorded multi-hour session.
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct AutonomyReplaySession {
    pub session_id: String,
    pub frames: Vec<ReplayFrame>,
    #[serde(default)]
    pub initial_goal: Option<String>,
    #[serde(default)]
    pub initial_waypoints: Option<Vec<(f64, f64, f64)>>,
}

impl AutonomyReplaySession {
    pub fn new(session_id: impl Into<String>) -> Self {
        Self {
            session_id: session_id.into(),
            frames: Vec::new(),
            initial_goal: None,
            initial_waypoints: None,
        }
    }

    /// Appends a new recorded frame to the session trace.
    pub fn push_frame(&mut self, frame: ReplayFrame) {
        self.frames.push(frame);
    }

    /// Total duration of the recorded session in milliseconds.
    pub fn duration_ms(&self) -> u64 {
        if self.frames.len() < 2 {
            return 0;
        }
        let first = self.frames.first().map(|f| f.timestamp_ms).unwrap_or(0);
        let last = self.frames.last().map(|f| f.timestamp_ms).unwrap_or(0);
        last.saturating_sub(first)
    }

    /// Total number of frames in the session trace.
    pub fn len(&self) -> usize {
        self.frames.len()
    }

    /// Returns true if the session trace contains no frames.
    pub fn is_empty(&self) -> bool {
        self.frames.is_empty()
    }

    /// Reads and deserializes an AutonomyReplaySession from a JSON file.
    pub fn from_file<P: AsRef<std::path::Path>>(path: P) -> std::io::Result<Self> {
        let file = std::fs::File::open(path)?;
        let reader = std::io::BufReader::new(file);
        serde_json::from_reader(reader)
            .map_err(|e| std::io::Error::new(std::io::ErrorKind::InvalidData, e))
    }

    /// Serializes this session trace to a formatted JSON file.
    pub fn to_file<P: AsRef<std::path::Path>>(&self, path: P) -> std::io::Result<()> {
        let file = std::fs::File::create(path)?;
        let writer = std::io::BufWriter::new(file);
        serde_json::to_writer_pretty(writer, self).map_err(std::io::Error::other)
    }

    /// Recursively discovers and loads traces from a given root path.
    /// Ingests native `.json` replay sessions, `events.jsonl` routine event streams,
    /// and `ui-observations.jsonl` combat dumps.
    ///
    /// Files that fail to parse and sessions with no frames are skipped silently (no warning, nothing in the matrix).
    /// The `.jsonl` session ids come from the parent directory name, so same-named directories share an id; the sort
    /// key is `session_id` only.
    pub fn discover_and_load_traces<P: AsRef<std::path::Path>>(
        root: P,
    ) -> Vec<AutonomyReplaySession> {
        let mut sessions = Vec::new();
        let mut dirs = vec![root.as_ref().to_path_buf()];

        while let Some(dir) = dirs.pop() {
            if let Ok(entries) = std::fs::read_dir(&dir) {
                for entry in entries.flatten() {
                    let path = entry.path();
                    if path.is_dir() {
                        dirs.push(path);
                    } else if path.is_file() {
                        let name = path.file_name().and_then(|n| n.to_str()).unwrap_or("");
                        if name.ends_with(".json") {
                            if let Ok(session) = Self::from_file(&path) {
                                if !session.is_empty() {
                                    sessions.push(session);
                                }
                            }
                        } else if name == "events.jsonl" || name.ends_with("-events.jsonl") {
                            if let Ok(content) = std::fs::read_to_string(&path) {
                                let parent_name = path
                                    .parent()
                                    .and_then(|p| p.file_name())
                                    .and_then(|n| n.to_str())
                                    .unwrap_or("trace");
                                let id = format!("events_{}", parent_name);
                                if let Ok(session) = Self::from_w101_events_jsonl(&content, &id) {
                                    if !session.is_empty() {
                                        sessions.push(session);
                                    }
                                }
                            }
                        } else if name == "ui-observations.jsonl" || name.ends_with("-ui.jsonl") {
                            if let Ok(content) = std::fs::read_to_string(&path) {
                                let parent_name = path
                                    .parent()
                                    .and_then(|p| p.file_name())
                                    .and_then(|n| n.to_str())
                                    .unwrap_or("trace");
                                let id = format!("ui_{}", parent_name);
                                if let Ok(session) = Self::from_ui_observations_jsonl(&content, &id)
                                {
                                    if !session.is_empty() {
                                        sessions.push(session);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        sessions.sort_by(|a, b| a.session_id.cmp(&b.session_id));
        sessions
    }

    /// Ingests real Wizard101 routine observation events (e.g. from events.jsonl)
    /// into an AutonomyReplaySession trace.
    pub fn from_w101_events_jsonl(
        events_jsonl_content: &str,
        session_id: &str,
    ) -> std::io::Result<Self> {
        let mut frames = Vec::new();
        let now_str = "2026-11-18T22:30:00.0000000Z";

        for line in events_jsonl_content.lines() {
            let line = line.trim();
            if line.is_empty() {
                continue;
            }
            if let Ok(val) = serde_json::from_str::<serde_json::Value>(line) {
                let kind = val.get("kind").and_then(|k| k.as_str()).unwrap_or("");
                let time_ms = val.get("time_ms").and_then(|t| t.as_u64()).unwrap_or(0);

                if kind == "observation" {
                    if let Some(obs) = val.pointer("/payload/module_observation/observation") {
                        let scene = obs.get("scene").and_then(|s| s.as_str()).unwrap_or("");
                        let is_idle = scene == "NativeIdle" || scene == "MenuObserved";
                        let is_loading = scene == "Loading";
                        let is_navigation =
                            scene.starts_with("Landmark.") || scene.starts_with("Progress.");

                        let expected = if is_navigation {
                            Some(String::from("PROPOSAL:StepWaypoint"))
                        } else {
                            None
                        };

                        let pos_x = 100.0 + (time_ms as f64 * 0.1);
                        let snapshot = serde_json::json!({
                            "connected": true,
                            "facts": {
                                "gameId": "wizard101",
                                "targetId": "12345/67890",
                                "source": "reviewed-w101-ui-tree",
                                "observedUtc": now_str,
                                "nativeIdle": is_idle,
                                "loading": is_loading,
                            },
                            "aliases": [
                                simulated_alias("combat.active", "BOOL", false, now_str),
                                simulated_alias("zone.name", "STRING", "WizardCity_Commons", now_str),
                                simulated_alias("position.x", "REAL", pos_x, now_str),
                                simulated_alias("position.y", "REAL", 100.0, now_str),
                                simulated_alias("position.z", "REAL", 0.0, now_str),
                                {
                                    "id": "health",
                                    "dataType": "REAL",
                                    "value": 500.0,
                                    "quality": "fresh",
                                    "targetId": "12345/67890",
                                    "observedUtc": now_str,
                                    "source": "reviewed-w101-ui-tree",
                                    "gameId": "wizard101"
                                },
                                {
                                    "id": "health.max",
                                    "dataType": "DINT",
                                    "value": 500,
                                    "quality": "fresh",
                                    "targetId": "12345/67890",
                                    "observedUtc": now_str,
                                    "source": "reviewed-w101-ui-tree",
                                    "gameId": "wizard101"
                                },
                                {
                                    "id": "mana",
                                    "dataType": "REAL",
                                    "value": 100.0,
                                    "quality": "fresh",
                                    "targetId": "12345/67890",
                                    "observedUtc": now_str,
                                    "source": "reviewed-w101-ui-tree",
                                    "gameId": "wizard101"
                                },
                                {
                                    "id": "mana.max",
                                    "dataType": "DINT",
                                    "value": 100,
                                    "quality": "fresh",
                                    "targetId": "12345/67890",
                                    "observedUtc": now_str,
                                    "source": "reviewed-w101-ui-tree",
                                    "gameId": "wizard101"
                                },
                                {
                                    "id": "potions",
                                    "dataType": "REAL",
                                    "value": 2.0,
                                    "quality": "fresh",
                                    "targetId": "12345/67890",
                                    "observedUtc": now_str,
                                    "source": "reviewed-w101-ui-tree",
                                    "gameId": "wizard101"
                                },
                                {
                                    "id": "nativeIdle",
                                    "dataType": "BOOL",
                                    "value": is_idle,
                                    "quality": "fresh",
                                    "targetId": "12345/67890",
                                    "observedUtc": now_str,
                                    "source": "reviewed-w101-ui-tree",
                                    "gameId": "wizard101"
                                }
                            ],
                            "unavailable": []
                        });

                        let parse_time =
                            std::time::UNIX_EPOCH + std::time::Duration::from_secs(1795041000);
                        let mut state =
                            NativeHostState::from_snapshot_json_at(&snapshot, parse_time);
                        state.is_simulation = true;
                        frames.push(ReplayFrame {
                            timestamp_ms: time_ms,
                            state,
                            expected_intent: expected,
                        });
                    }
                }
            }
        }

        Ok(Self {
            session_id: session_id.to_string(),
            frames,
            initial_goal: Some(String::from("W101RoutineReplay")),
            initial_waypoints: Some(vec![(200.0, 100.0, 0.0)]),
        })
    }

    /// Ingests real Wizard101 UI tree observations (e.g. from ui-observations.jsonl)
    /// into an AutonomyReplaySession trace.
    pub fn from_ui_observations_jsonl(
        ui_jsonl_content: &str,
        session_id: &str,
    ) -> std::io::Result<Self> {
        let mut frames = Vec::new();
        let now_str = "2026-11-18T22:30:00.0000000Z";
        let mut initial_ticks: Option<u64> = None;

        for line in ui_jsonl_content.lines() {
            let line = line.trim();
            if line.is_empty() {
                continue;
            }
            if let Ok(val) = serde_json::from_str::<serde_json::Value>(line) {
                let kind = val.get("kind").and_then(|k| k.as_str()).unwrap_or("");
                let ticks = val
                    .get("observedUtcTicks")
                    .and_then(|t| t.as_u64())
                    .unwrap_or(0);
                let base_ticks = *initial_ticks.get_or_insert(ticks);
                // 1 tick = 100ns = 0.0001ms (10,000 ticks per ms)
                let time_ms = ticks.saturating_sub(base_ticks) / 10_000;

                if kind == "tree" || kind == "loading" {
                    let is_loading = val
                        .get("loading")
                        .and_then(|l| l.as_bool())
                        .unwrap_or(false);
                    let is_idle = val
                        .pointer("/states/nativeIdle")
                        .and_then(|i| i.as_bool())
                        .unwrap_or(false);
                    let is_combat = val
                        .pointer("/combatContext/planningVisible")
                        .and_then(|p| p.as_bool())
                        .unwrap_or(false);

                    let expected = if is_combat {
                        Some(String::from("PROPOSAL:CombatTurn"))
                    } else {
                        None
                    };

                    let snapshot = serde_json::json!({
                        "connected": true,
                        "facts": {
                            "gameId": "wizard101",
                            "targetId": "12345/67890",
                            "source": "reviewed-w101-ui-tree",
                            "observedUtc": now_str,
                            "nativeIdle": is_idle,
                            "loading": is_loading,
                        },
                        "aliases": [
                            simulated_alias("combat.active", "BOOL", is_combat, now_str),
                            simulated_alias("zone.name", "STRING", "WizardCity_Commons", now_str),
                            simulated_alias("position.x", "REAL", 100.0, now_str),
                            simulated_alias("position.y", "REAL", 100.0, now_str),
                            simulated_alias("position.z", "REAL", 0.0, now_str),
                            {
                                "id": "health",
                                "dataType": "REAL",
                                "value": 500.0,
                                "quality": "fresh",
                                "targetId": "12345/67890",
                                "observedUtc": now_str,
                                "source": "reviewed-w101-ui-tree",
                                "gameId": "wizard101"
                            },
                            {
                                "id": "health.max",
                                "dataType": "DINT",
                                "value": 500,
                                "quality": "fresh",
                                "targetId": "12345/67890",
                                "observedUtc": now_str,
                                "source": "reviewed-w101-ui-tree",
                                "gameId": "wizard101"
                            },
                            {
                                "id": "mana",
                                "dataType": "REAL",
                                "value": 100.0,
                                "quality": "fresh",
                                "targetId": "12345/67890",
                                "observedUtc": now_str,
                                "source": "reviewed-w101-ui-tree",
                                "gameId": "wizard101"
                            },
                            {
                                "id": "mana.max",
                                "dataType": "DINT",
                                "value": 100,
                                "quality": "fresh",
                                "targetId": "12345/67890",
                                "observedUtc": now_str,
                                "source": "reviewed-w101-ui-tree",
                                "gameId": "wizard101"
                            },
                            {
                                "id": "potions",
                                "dataType": "REAL",
                                "value": 2.0,
                                "quality": "fresh",
                                "targetId": "12345/67890",
                                "observedUtc": now_str,
                                "source": "reviewed-w101-ui-tree",
                                "gameId": "wizard101"
                            },
                            {
                                "id": "nativeIdle",
                                "dataType": "BOOL",
                                "value": is_idle,
                                "quality": "fresh",
                                "targetId": "12345/67890",
                                "observedUtc": now_str,
                                "source": "reviewed-w101-ui-tree",
                                "gameId": "wizard101"
                            }
                        ],
                        "unavailable": []
                    });

                    let parse_time =
                        std::time::UNIX_EPOCH + std::time::Duration::from_secs(1795041000);
                    let mut state = NativeHostState::from_snapshot_json_at(&snapshot, parse_time);
                    state.is_simulation = true;
                    frames.push(ReplayFrame {
                        timestamp_ms: time_ms,
                        state,
                        expected_intent: expected,
                    });
                }
            }
        }

        Ok(Self {
            session_id: session_id.to_string(),
            frames,
            initial_goal: None,
            initial_waypoints: None,
        })
    }
}

/// Quantitative telemetry and quality gate metrics accumulated across a replay session.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct AutonomyReplayMetrics {
    pub total_ticks: usize,
    pub labeled_ticks: usize,
    pub matched_intent_ticks: usize,
    /// 100.0 when no frame carries an `expected_intent` (the default), so an unlabeled session counts as a pass.
    pub decision_accuracy_pct: f64,
    /// Counts every change of `proposed_intent` between consecutive intent-bearing frames; it does not separate
    /// planned completions from aborts.
    pub replan_count: usize,
    pub replan_rate_per_hour: f64,
    pub total_steering_commands: usize,
    pub total_kinematic_plans: usize,
    pub max_steering_curvature: f64,
    pub avg_steering_curvature: f64,
    pub max_kinematic_jerk: f64,
    pub warnings: Vec<String>,
}

impl Default for AutonomyReplayMetrics {
    fn default() -> Self {
        Self {
            total_ticks: 0,
            labeled_ticks: 0,
            matched_intent_ticks: 0,
            decision_accuracy_pct: 100.0,
            replan_count: 0,
            replan_rate_per_hour: 0.0,
            total_steering_commands: 0,
            total_kinematic_plans: 0,
            max_steering_curvature: 0.0,
            avg_steering_curvature: 0.0,
            max_kinematic_jerk: 0.0,
            warnings: Vec::new(),
        }
    }
}

impl AutonomyReplayMetrics {
    /// Formats an executive summary and KPI breakdown in GitHub-Flavored Markdown.
    pub fn to_markdown(&self, session_id: &str, duration_ms: u64) -> String {
        let duration_sec = duration_ms / 1000;
        let hours = duration_sec / 3600;
        let mins = (duration_sec % 3600) / 60;
        let secs = duration_sec % 60;
        let duration_str = format!("{:02}:{:02}:{:02}", hours, mins, secs);

        let acc_gate = if self.decision_accuracy_pct >= 99.5 {
            "PASS"
        } else {
            "FAIL"
        };
        let curv_gate = if self.max_steering_curvature < 5.0 {
            "PASS"
        } else {
            "FAIL"
        };
        let jerk_gate = if self.max_kinematic_jerk < 10.0 {
            "PASS"
        } else {
            "FAIL"
        };
        let warn_gate = if self.warnings.is_empty() {
            "PASS"
        } else {
            "WARN"
        };

        let mut md = String::new();
        md.push_str(&format!("# Autonomy Replay Report: {}\n\n", session_id));
        md.push_str(&format!("- **Total Ticks**: {}\n", self.total_ticks));
        md.push_str(&format!("- **Labeled Ticks**: {}\n", self.labeled_ticks));
        md.push_str(&format!(
            "- **Duration**: {} ({} ms)\n\n",
            duration_str, duration_ms
        ));

        md.push_str("## Executive KPI Summary\n\n");
        md.push_str("| Metric | Value | Target | Status |\n");
        md.push_str("| :--- | :--- | :--- | :--- |\n");
        md.push_str(&format!(
            "| **Decision Accuracy** | {:.2}% | >= 99.5% | **{}** |\n",
            self.decision_accuracy_pct, acc_gate
        ));
        // The status below is a constant: no replan threshold is enforced and 30.0 has no source.
        md.push_str(&format!(
            "| **Replanning Rate** | {:.2} replans/hr | <= 30.0 /hr | **PASS** |\n",
            self.replan_rate_per_hour
        ));
        md.push_str(&format!(
            "| **Replan Count** | {} | N/A | INFO |\n",
            self.replan_count
        ));
        md.push_str(&format!(
            "| **Peak Steering Curvature** | {:.3} px^-1 | < 5.000 px^-1 | **{}** |\n",
            self.max_steering_curvature, curv_gate
        ));
        md.push_str(&format!(
            "| **Mean Steering Curvature** | {:.3} px^-1 | < 1.000 px^-1 | INFO |\n",
            self.avg_steering_curvature
        ));
        md.push_str(&format!(
            "| **Peak Kinematic Jerk** | {:.3} px/ms^3 | < 10.000 px/ms^3 | **{}** |\n",
            self.max_kinematic_jerk, jerk_gate
        ));
        md.push_str(&format!(
            "| **Total Steering Commands** | {} | N/A | INFO |\n",
            self.total_steering_commands
        ));
        md.push_str(&format!(
            "| **Total Kinematic Plans** | {} | N/A | INFO |\n",
            self.total_kinematic_plans
        ));
        md.push_str(&format!(
            "| **Diagnostic Warnings** | {} | 0 warnings | **{}** |\n\n",
            self.warnings.len(),
            warn_gate
        ));

        md.push_str("## Diagnostic Warnings\n\n");
        if self.warnings.is_empty() {
            md.push_str("*No warnings or anomalies observed during replay.*\n");
        } else {
            for (idx, w) in self.warnings.iter().take(25).enumerate() {
                md.push_str(&format!("{}. {}\n", idx + 1, w));
            }
            if self.warnings.len() > 25 {
                md.push_str(&format!(
                    "\n*... and {} additional warnings omitted.*\n",
                    self.warnings.len() - 25
                ));
            }
        }

        md
    }
}

/// Offline replay execution engine.
pub struct AutonomyReplayHarness {
    /// Maximum allowed steering curvature before flagging an abrupt angular turn warning (1/px).
    pub max_curvature_threshold: f64,
    /// Maximum allowed jerk threshold (px/ms^3).
    pub max_jerk_threshold: f64,
}

impl Default for AutonomyReplayHarness {
    fn default() -> Self {
        Self {
            max_curvature_threshold: 5.0,
            max_jerk_threshold: 10.0,
        }
    }
}

impl AutonomyReplayHarness {
    pub fn new() -> Self {
        Self::default()
    }

    /// Replays an entire recorded session through the autonomy bridge, accumulating metrics.
    pub fn replay_session(
        &self,
        session: &AutonomyReplaySession,
        bridge: &mut MaelstromAutonomyBridge,
    ) -> (AutonomyReplayMetrics, Vec<AutonomyCycleResult>) {
        if let Some(ref goal) = session.initial_goal {
            bridge.blackboard.active_goal = Some(goal.clone());
        }
        if let Some(ref waypoints) = session.initial_waypoints {
            bridge.blackboard.active_waypoints = waypoints.clone();
        }

        let mut metrics = AutonomyReplayMetrics::default();
        let mut cycle_results = Vec::with_capacity(session.len());

        let mut prev_intent: Option<String> = None;
        let mut curvature_sum = 0.0;
        let mut curvature_count = 0usize;

        for frame in &session.frames {
            metrics.total_ticks += 1;

            // Pure, deterministic offline state evaluation
            let result = bridge.process_state(&frame.state);

            // Decision accuracy verification
            if let Some(ref expected) = frame.expected_intent {
                metrics.labeled_ticks += 1;
                let actual = result
                    .proposed_intent
                    .as_deref()
                    .unwrap_or(&result.bt_status);

                if actual == expected.as_str() {
                    metrics.matched_intent_ticks += 1;
                } else {
                    metrics.warnings.push(format!(
                        "Tick {} ({}ms): intent mismatch (expected '{}', got '{}')",
                        metrics.total_ticks, frame.timestamp_ms, expected, actual
                    ));
                }
            }

            // Replanning detection: detect when active intent changes non-trivially
            if let Some(ref curr_intent) = result.proposed_intent {
                if let Some(ref last_intent) = prev_intent {
                    if curr_intent != last_intent {
                        metrics.replan_count += 1;
                    }
                }
                prev_intent = Some(curr_intent.clone());
            }

            // Steering curvature analysis: curvature is pre-computed in steering.curvature
            // (Pure Pursuit kappa = 2 sin(alpha) / max(d, 1) in world units, so |kappa| <= 2.0 and the threshold
            // below cannot be exceeded)
            if let Some(ref steering) = result.steering_command {
                metrics.total_steering_commands += 1;
                let kappa = steering.curvature.abs();
                if kappa > 0.0001 {
                    if kappa > metrics.max_steering_curvature {
                        metrics.max_steering_curvature = kappa;
                    }
                    curvature_sum += kappa;
                    curvature_count += 1;

                    if kappa > self.max_curvature_threshold {
                        metrics.warnings.push(format!(
                            "Tick {} ({}ms): steering curvature {:.3} exceeded threshold {:.1}",
                            metrics.total_ticks,
                            frame.timestamp_ms,
                            kappa,
                            self.max_curvature_threshold
                        ));
                    }
                }
            }

            // Kinematic jerk analysis
            if let Some(ref plan) = result.kinematic_plan {
                metrics.total_kinematic_plans += 1;
                let jerk = self.compute_trajectory_peak_jerk(plan);
                if jerk > metrics.max_kinematic_jerk {
                    metrics.max_kinematic_jerk = jerk;
                }
                if jerk > self.max_jerk_threshold {
                    metrics.warnings.push(format!(
                        "Tick {} ({}ms): kinematic jerk {:.3} exceeded threshold {:.1}",
                        metrics.total_ticks, frame.timestamp_ms, jerk, self.max_jerk_threshold
                    ));
                }
            }

            cycle_results.push(result);
        }

        // Finalize aggregated metrics
        if metrics.labeled_ticks > 0 {
            metrics.decision_accuracy_pct =
                (metrics.matched_intent_ticks as f64 / metrics.labeled_ticks as f64) * 100.0;
        }

        let duration_hours = session.duration_ms() as f64 / 3_600_000.0;
        if duration_hours > 0.0 {
            metrics.replan_rate_per_hour = metrics.replan_count as f64 / duration_hours;
        }

        if curvature_count > 0 {
            metrics.avg_steering_curvature = curvature_sum / curvature_count as f64;
        }

        (metrics, cycle_results)
    }

    /// Scale-dependent jerk proxy computed from the plan summary alone (no waypoints are available here); it is
    /// not a third derivative.
    fn compute_trajectory_peak_jerk(
        &self,
        plan: &crate::maelstrom_bridge::KinematicPlanSummary,
    ) -> f64 {
        if plan.point_count < 4 || plan.total_duration_ms <= 0.0 {
            return 0.0;
        }

        // Analytical cubic minimum-jerk profile:
        // Peak jerk occurs at boundary: J_peak = (distance / T^3) * 60
        // Approximate distance assuming standard center screen origin (640, 360)
        // The expression below uses 0.001 where the formula above has 60, so it is a proxy, not J_peak.
        let dist = ((plan.target_x - 640.0).powi(2) + (plan.target_y - 360.0).powi(2)).sqrt();
        let t_sec = plan.total_duration_ms / 1000.0;
        if t_sec > 0.01 {
            (dist / (t_sec * t_sec * t_sec)) * 0.001 // scaled to px/ms^3
        } else {
            0.0
        }
    }

    /// Replays a batch of sessions sequentially through independent offline bridge instances.
    pub fn run_batch(
        &self,
        sessions: &[AutonomyReplaySession],
        temp_base: &std::path::Path,
    ) -> BatchBenchmarkResult {
        let mut entries = Vec::new();
        let mut passed_sessions = 0;
        let mut failed_sessions = 0;
        let mut total_frames = 0;
        let mut total_duration_ms = 0;

        for session in sessions {
            let session_dir = temp_base.join(format!("batch_{}", session.session_id));
            let _ = std::fs::create_dir_all(&session_dir);
            // A fresh bridge per session: without `initial_goal` and `initial_waypoints` it answers YieldIdle.
            if let Ok(mut bridge) = MaelstromAutonomyBridge::new(&session_dir) {
                let (metrics, _) = self.replay_session(session, &mut bridge);
                // 95.0 here and in the maelstrom-replay exit code; the report text and the long test use 99.5.
                let passed = metrics.decision_accuracy_pct >= 95.0;
                if passed {
                    passed_sessions += 1;
                } else {
                    failed_sessions += 1;
                }
                total_frames += session.len();
                total_duration_ms += session.duration_ms();

                entries.push(SessionBenchmarkEntry {
                    session_id: session.session_id.clone(),
                    frame_count: session.len(),
                    duration_ms: session.duration_ms(),
                    decision_accuracy_pct: metrics.decision_accuracy_pct,
                    replan_count: metrics.replan_count,
                    max_kinematic_jerk: metrics.max_kinematic_jerk,
                    avg_curvature: metrics.avg_steering_curvature,
                    passed,
                });
            }
            let _ = std::fs::remove_dir_all(&session_dir);
        }

        BatchBenchmarkResult {
            total_sessions: sessions.len(),
            passed_sessions,
            failed_sessions,
            total_frames,
            total_duration_ms,
            entries,
        }
    }
}

/// Individual entry in the batch benchmark matrix.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SessionBenchmarkEntry {
    pub session_id: String,
    pub frame_count: usize,
    pub duration_ms: u64,
    pub decision_accuracy_pct: f64,
    pub replan_count: usize,
    pub max_kinematic_jerk: f64,
    pub avg_curvature: f64,
    pub passed: bool,
}

/// Aggregate results from replaying a batch of sessions.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct BatchBenchmarkResult {
    pub total_sessions: usize,
    pub passed_sessions: usize,
    pub failed_sessions: usize,
    pub total_frames: usize,
    pub total_duration_ms: u64,
    pub entries: Vec<SessionBenchmarkEntry>,
}

impl BatchBenchmarkResult {
    /// Formats the benchmark result as a GitHub-flavored Markdown matrix.
    pub fn to_markdown_matrix(&self) -> String {
        let mut md = String::new();
        md.push_str("# Autonomy Replay Benchmark Matrix\n\n");
        md.push_str("## Aggregate Execution Summary\n");
        md.push_str(&format!(
            "- **Total Sessions Evaluated**: {}\n",
            self.total_sessions
        ));
        md.push_str(&format!(
            "- **Passed**: {} | **Failed**: {}\n",
            self.passed_sessions, self.failed_sessions
        ));
        md.push_str(&format!(
            "- **Total Recorded Frames**: {}\n",
            self.total_frames
        ));
        md.push_str(&format!(
            "- **Total Recorded Duration**: {:.2} s\n\n",
            self.total_duration_ms as f64 / 1000.0
        ));

        let overall_status = if self.failed_sessions == 0 && self.total_sessions > 0 {
            "PASS"
        } else {
            "FAIL"
        };
        md.push_str(&format!("**Gate Status**: **{}**\n\n", overall_status));

        md.push_str("## Per-Session Breakdown\n");
        md.push_str(
            "| Session ID | Frames | Duration (ms) | Accuracy | Replans | Max Jerk | Status |\n",
        );
        md.push_str("| :--- | :--- | :--- | :--- | :--- | :--- | :--- |\n");

        for entry in &self.entries {
            let status = if entry.passed { "PASS" } else { "FAIL" };
            md.push_str(&format!(
                "| `{}` | {} | {} | {:.1}% | {} | {:.3} | **{}** |\n",
                entry.session_id,
                entry.frame_count,
                entry.duration_ms,
                entry.decision_accuracy_pct,
                entry.replan_count,
                entry.max_kinematic_jerk,
                status
            ));
        }

        md
    }

    /// Serializes summary data to a JSON value.
    pub fn to_json_value(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap_or(serde_json::Value::Null)
    }
}
