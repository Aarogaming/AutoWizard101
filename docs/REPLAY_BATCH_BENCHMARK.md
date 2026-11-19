# Multi-Trace Batch Evaluation & Replay Benchmark Matrix

## 1. Architectural Overview

The **Maelstrom Autonomy Replay Batch Evaluation Pipeline** (`maelstrom-replay`) serves as an automated offline regression gate. Before new behaviors, quest zones, or steering laws are committed, the batch pipeline replays all recorded gameplay sessions across `.local/` and test repositories in headless, host-owned deterministic time.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Offline Trace Repositories (.local/)                │
│   - w101-operations events.jsonl                                        │
│   - combat-structure live UI observations.jsonl                         │
│   - AutonomyReplaySession JSON traces                                  │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │ Batch Ingestion
┌────────────────────────────────────▼────────────────────────────────────┐
│                    maelstrom-replay --batch-dir <path>                  │
│   - Recursive Directory Scanner (walkdir / path discovery)              │
│   - Trace Ingestion Engine (AutonomyReplaySession format)               │
│   - Headless Deterministic Replay Harness (Host-owned Ticks)            │
│   - Metrics Collector & Aggregator                                      │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │ Benchmark Compilation
┌────────────────────────────────────▼────────────────────────────────────┐
│                docs/coordination/results/REPLAY-BENCHMARK-MATRIX.md     │
│   - Per-Session Execution Matrix                                        │
│   - Aggregate KPI Summary (MIN / MEDIAN / P95 / MAX)                   │
│   - Pass/Fail Regression Gate Status                                    │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Ingestion & Execution Pipeline

### 2.1 Trace Ingestion Discovery
The batch runner accepts a root directory or explicit file list:
- Recursively traverses subdirectories.
- Identifies:
  - `*trace*.json` or `*session*.json` (Native `AutonomyReplaySession`).
  - `*events*.jsonl` (Wizard101 operation events stream via `from_w101_events_jsonl`).
  - `*ui-observations*.jsonl` (Wizard101 live UI tree observations via `from_ui_observations_jsonl`).
- Parses each trace into memory, discarding corrupt entries with structured warning logs while preserving valid sessions.

### 2.2 Headless Deterministic Simulation
Because `behavior_tree` and `MaelstromAutonomyBridge` use host-owned deterministic clock ticks:
- Replay workers execute without real-time delays or OS sleep calls.
- Thousands of frames execute in tens of milliseconds per session.
- Output decisions are strictly reproducible across machines.

---

## 3. Evaluated Metrics & Invariants

| KPI Category | Metric Name | Acceptance Threshold | Failure Semantics |
| :--- | :--- | :--- | :--- |
| **Decision Integrity** | `Decision Accuracy (%)` | $\ge 95.0\%$ | Flags regressions where planner intent disagrees with recorded oracle. |
| | `Divergence Count` | $0$ critical divergences | Replay state must not enter undefined states. |
| **Stability & Transitions**| `Replanning Transitions` | $\le 20$ / session | Detects path oscillation or flip-flopping goals. |
| | `Mean Replanning Latency` | $\le 2.0\text{ ms}$ | Ensures real-time feasibility within game tick budget. |
| **Kinematic Smoothness** | `Max Yaw Jolt (rad/s)` | $\le 0.05\text{ rad/s}$ | Flags unnatural steering snaps. |
| | `Avg Trajectory Curvature`| $\le 0.02$ | Flags jagged, non-minimum-jerk paths. |

---

## 4. Benchmark Matrix Output Format

The matrix is emitted as deterministic Markdown to `docs/coordination/results/REPLAY-BENCHMARK-MATRIX.md`:

```markdown
# Autonomy Replay Benchmark Matrix

## Aggregate Summary
| Metric | Min | Median | P95 | Max | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Decision Accuracy (%)** | 98.2% | 100.0% | 100.0% | 100.0% | PASS |
| **Replanning Transitions**| 0 | 1 | 3 | 5 | PASS |
| **Max Yaw Jolt (rad/s)** | 0.000 | 0.012 | 0.038 | 0.045 | PASS |
| **Avg Trajectory Curvature**| 0.000 | 0.004 | 0.011 | 0.015 | PASS |

## Per-Session Breakdown
| Session ID | Frames | Duration (ms) | Accuracy | Replans | Max Yaw Jolt | Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `w101-operations-real` | 19 | 200 | 100.0% | 0 | 0.000 rad/s | PASS |
| `w101-combat-live-ui` | 42 | 4100 | 100.0% | 1 | 0.015 rad/s | PASS |
| `synthetic-commons-turn`| 100 | 1000 | 100.0% | 0 | 0.022 rad/s | PASS |
```

---

## 5. CLI Invocation Syntax

```powershell
# Run batch evaluation across a directory and emit reports
maelstrom-replay --batch-dir .local/quest-step-check/TestData --matrix-out docs/coordination/results/REPLAY-BENCHMARK-MATRIX.md --matrix-json target/benchmark_matrix.json
```
