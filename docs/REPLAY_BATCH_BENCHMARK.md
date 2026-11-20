# Multi-Trace Batch Evaluation & Replay Benchmark Matrix

> **Status (audit 2026-11-20, Claude):** batch mode is real (`maelstrom-replay --batch-dir`, `AutonomyReplayHarness::run_batch`) and runs offline, but this page was written
> before the code and specified a KPI set, a matrix layout and error handling that the code does not have. It is corrected below; notes marked **Today**
> say what the code does. Nothing runs it automatically: no CI job or script calls `maelstrom-replay`. Evidence:
> [W101-REPLAY-BATCH-CORE-02](coordination/results/W101-REPLAY-BATCH-CORE-02.md) and
> [W101-REPLAY-BATCH-DOCS-01](coordination/results/W101-REPLAY-BATCH-DOCS-01.md), audit notes. CLI flags and exit codes:
> [AUTONOMY_REPLAY_CLI.md](AUTONOMY_REPLAY_CLI.md).

## 1. Architectural Overview

The **Maelstrom Autonomy Replay Batch Evaluation Pipeline** (`maelstrom-replay`) is an offline regression check that someone has to run. Given a directory (for example `.local/`), it replays every trace it can load, one after another, with no real-time pacing, and exits 2 if any session's decision accuracy is below 95.0%. The traces are serialized sessions, synthetic fixtures, or frames ingested from event and UI logs with fabricated position and vitals (see [OFFLINE_AUTONOMY_REPLAY.md](OFFLINE_AUTONOMY_REPLAY.md)), not recorded gameplay sessions.

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
│   - Recursive Directory Scanner (iterative read_dir)                    │
│   - Trace Ingestion Engine (AutonomyReplaySession format)               │
│   - Headless Deterministic Replay Harness (Host-owned Ticks)            │
│   - Per-session metrics and pass/fail counts                            │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │ Benchmark Compilation
┌────────────────────────────────────▼────────────────────────────────────┐
│                docs/coordination/results/REPLAY-BENCHMARK-MATRIX.md     │
│   - Per-Session Execution Matrix                                        │
│   - Aggregate counts and Gate Status (no MIN/MEDIAN/P95)               │
│   - Pass/Fail Regression Gate Status                                    │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Ingestion & Execution Pipeline

### 2.1 Trace Ingestion Discovery
The batch runner (`AutonomyReplaySession::discover_and_load_traces`) accepts one root directory; there is no explicit file list.
- Walks subdirectories with an iterative stack of `read_dir` calls (no `walkdir` dependency).
- Loads, by file name:
  - any `*.json` (native `AutonomyReplaySession`; the session id is the `session_id` field inside the file).
  - `events.jsonl` or `*-events.jsonl` (Wizard101 operation events via `from_w101_events_jsonl`; id `events_<parent directory name>`).
  - `ui-observations.jsonl` or `*-ui.jsonl` (Wizard101 UI tree observations via `from_ui_observations_jsonl`; id `ui_<parent directory name>`).
- Sorts the sessions by `session_id` only.

**Today:**
- Unparsable files and sessions with no frames are skipped without a message: there is no structured warning log, and the matrix does not list them. With the
  four synthetic files of the sample in section 4 (one of them corrupt) the run reported `Discovered 3 trace sessions` and nothing else.
- The two `.jsonl` ids come from the parent directory name, so many files in same-named directories get the same id. The committed matrix has 1,094 rows but 74
  distinct ids (see the CORE-02 audit); rows with equal ids keep directory-iteration order.
- A batch directory with no loadable session is an error (exit 1, "No valid traces found"), not an empty matrix.

### 2.2 Headless Deterministic Simulation
The frames' `timestamp_ms` are not fed to the bridge (`process_state` takes only the state); they give the session duration and the text of warnings. Nothing in the replay module sleeps or reads the wall clock (no `sleep`, `Instant` or `SystemTime` in `autonomy_replay.rs`; the sleeps in `maelstrom_bridge.rs` belong to the pipe client, which replay does not use).
- Replay runs without real-time delays. Measured (debug build, this machine): a batch of three 100-frame sessions ran in about 85 ms wall time for the whole process,
  including start-up and writing the reports. That is one small sample, not a benchmark of thousands of frames.
- Output is reproducible where checked: two batch runs over the same directory produced a byte-identical matrix on one machine. Identity across machines is not verified.
- The bridge's PRNG has a fixed seed (`0x20261118`), and each session gets a fresh bridge, so a session needs `initial_goal` and `initial_waypoints` of its own.
- Sessions run one after another; claims elsewhere of "parallel" evaluation are wrong.

---

## 3. Evaluated Metrics & Invariants

What each session is scored on, as implemented:

| Value | Source | Gates the run? |
| :--- | :--- | :--- |
| `Accuracy` | `decision_accuracy_pct`: share of labelled ticks whose proposal matches `expected_intent`; 100% when no tick is labelled | **Yes**: below 95.0 the session FAILs and the process exits 2 |
| `Replans` | `replan_count`: number of intent changes (withheld frames skipped) | No |
| `Max Jerk` | `max_kinematic_jerk`: the click-plan proxy `dist / T^3 * 0.001` | No (a column in the Markdown) |
| `avg_curvature` | `avg_steering_curvature`, derived from the Pure Pursuit command | No (JSON only) |
| `Frames`, `Duration (ms)` | session length | No |

The overall `Gate Status` is FAIL when any session failed or when the batch is empty, else PASS. The 95.0% limit is the single-trace exit gate as well; the single-trace
report's own status column uses 99.5% (see [AUTONOMY_REPLAY_CLI.md](AUTONOMY_REPLAY_CLI.md)).

**Not implemented** (an earlier version of this page listed them with thresholds): `Divergence Count` (no such metric), `Replanning Transitions <= 20 / session`
(no per-session limit; the count is reported only), `Mean Replanning Latency <= 2.0 ms` (latency is not measured), `Max Yaw Jolt <= 0.05 rad/s` (no yaw metric exists; the
bridge does not observe a heading, see AUTONOMY_REPLAY_CLI.md Section 3.2), `Avg Trajectory Curvature <= 0.02` (computed, never compared), and the aggregate
`MIN / MEDIAN / P95 / MAX` table.

**Today (latent):** `total_sessions` is `sessions.len()`, but a session whose bridge cannot be created is dropped from both the pass and fail counts and from the rows,
so it could not turn the gate red. This was found by reading `run_batch`; it was not triggered.

---

## 4. Benchmark Matrix Output Format

The matrix is Markdown (and, with `--matrix-json`, JSON). The CLI writes it only where `--matrix-out` (or `--markdown`) names; with no path it prints it to stdout.
The committed `docs/coordination/results/REPLAY-BENCHMARK-MATRIX.md` is the output of one earlier run over `.local/`.

The output below is real: `maelstrom-replay --batch-dir` over four hand-built files (a throwaway program, not committed): `ok.json` (100 frames, labels match the
tree), `sub/bad.json` (12 of 100 labels wrong), `sub/deeper/unlabeled.json` (no labels) and `corrupt.json` (not JSON, skipped). It exited 2.

```markdown
# Autonomy Replay Benchmark Matrix

## Aggregate Execution Summary
- **Total Sessions Evaluated**: 3
- **Passed**: 2 | **Failed**: 1
- **Total Recorded Frames**: 300
- **Total Recorded Duration**: 29.70 s

**Gate Status**: **FAIL**

## Per-Session Breakdown
| Session ID | Frames | Duration (ms) | Accuracy | Replans | Max Jerk | Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `fixture_bad` | 100 | 9900 | 88.0% | 3 | 0.474 | **FAIL** |
| `fixture_ok` | 100 | 9900 | 100.0% | 3 | 0.474 | **PASS** |
| `fixture_unlabeled` | 100 | 9900 | 100.0% | 3 | 0.474 | **PASS** |
```

**Today:**
- An earlier version of this page showed a different layout (an aggregate `Min | Median | P95 | Max` table, `Max Yaw Jolt` and `Avg Trajectory Curvature`
  columns, accuracies of 98.2% to 100%) that the code never produced.
- `fixture_unlabeled` passes at 100.0% because nothing was labelled, so the accuracy says nothing.
- The JSON is `{total_sessions, passed_sessions, failed_sessions, total_frames, total_duration_ms, entries[]}`; each entry holds `session_id`, `frame_count`,
  `duration_ms`, `decision_accuracy_pct`, `replan_count`, `max_kinematic_jerk`, `avg_curvature` and `passed`.
- The output files are written with `File::create` (overwritten silently, not atomic).

---

## 5. CLI Invocation Syntax

```powershell
# Run batch evaluation across a directory and emit reports (exit 0 all passed, 1 input/I-O error, 2 a session below 95.0%)
maelstrom-replay --batch-dir .local/quest-step-check/TestData --matrix-out docs/coordination/results/REPLAY-BENCHMARK-MATRIX.md --matrix-json target/benchmark_matrix.json
```

---

## 6. Scratch directories and a known defect
`run_batch` creates a scratch directory for every session under `<temp>/maelstrom_batch_<uuid>/` (created by the CLI), gives the session a fresh bridge, and removes that
per-session directory afterwards.

**Today:**
- **Path escape (defect, fixed on a branch).** The per-session directory was named `batch_<session_id>`, and `session_id` is read from the trace file. A
  session id such as `/../../../victim` therefore resolved outside the temp base, and the batch run deleted that directory recursively. Reproduced in a sandbox:
  a decoy directory holding two files was removed by `maelstrom-replay --batch-dir` over a single hostile JSON file. It needs a crafted trace, but traces are file
  content. The fix names the directory by the session's position (`batch_<index>`) and adds a regression test that fails on the old code (commit 84e85cb, merged as `f93d65d` on 2026-11-20).
  Before that merge a crafted id escaped the temp base; traces are still untrusted file content, so prefer traces you produced.
- **Leftover directory.** Only the per-session directories are removed; the `maelstrom_batch_<uuid>` base stays behind, empty, in the temp folder after every batch run.
