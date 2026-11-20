# Result Evidence: W101-REPLAY-BATCH-DOCS-01

## Task Identity
- **Claim ID**: `W101-REPLAY-BATCH-DOCS-01`
- **Workstream**: `CORE-TRANSITIONS`
- **Task**: Multi-Trace Batch Evaluation Runner & Replay Benchmark Matrix Specification
- **Status**: Draft (was "Completed"; audited 2026-11-20, see "Audit note")

## 1. Executive Summary
This document registers the specification and design for the multi-trace batch evaluation runner in `maelstrom-replay`, enabling automated offline regression testing across all recorded `.local/` sessions and trace files.

### Key Deliverables
- Authored `docs/REPLAY_BATCH_BENCHMARK.md` detailing:
  - Recursive directory discovery of native `.json` replay sessions, `events.jsonl` operations traces, and `ui-observations.jsonl` combat dumps.
  - Headless deterministic simulation without sleeps or real-time pauses.
  - Core KPIs: Decision accuracy (>=95%), zero divergence count, replanning transition limits (<=20), mean replanning latency (<=2ms), max yaw jolt (<=0.05 rad/s), and average trajectory curvature (<=0.02).
  - Specification for the deterministic markdown output `docs/coordination/results/REPLAY-BENCHMARK-MATRIX.md`.
- Updated `docs/INDEX.md` with links to the new specification.

## 2. Adversarial Red Team / Blue Team Self-Audit
- **Red Team Vector**: A single corrupt or unparseable trace file in a directory could abort the entire batch evaluation process, preventing all subsequent traces from being verified.
- **Blue Team Mitigation**: Individual trace ingestion errors are caught and logged as warnings; the runner collects successfully parsed sessions and proceeds with batch evaluation, flagging the unparseable trace in the matrix output.

## 3. Claim Release
- Claim `W101-REPLAY-BATCH-DOCS-01` released.
- Owned paths:
  - `docs/REPLAY_BATCH_BENCHMARK.md`
  - `docs/INDEX.md`
  - `docs/coordination/results/W101-REPLAY-BATCH-DOCS-01.md`

---

## Audit note (2026-11-20, Claude)
Checked against `autonomy_replay.rs` (`discover_and_load_traces`, `run_batch`, `to_markdown_matrix`) and `bin/maelstrom-replay.rs`, and by running the built binary offline on
synthetic fixtures. The page was written before the code and describes intent; it is corrected in place.
- **"Core KPIs" are mostly not implemented.** Only decision accuracy (>= 95.0%) gates anything. There is no divergence count, no per-session replan limit (<= 20), no
  replanning latency measurement, no yaw metric (so no "max yaw jolt <= 0.05 rad/s") and no curvature gate (<= 0.02). The "Specification for the deterministic markdown
  output" showed a matrix (aggregate Min/Median/P95/Max table, yaw-jolt and curvature columns, specific accuracies) the code never emits; the page now carries a real one.
- **"Recursive discovery of `events.jsonl` and `ui-observations.jsonl`" is approximate.** Any `*.json` is loaded (not only `*trace*`/`*session*`), `.jsonl` files are matched by
  `events.jsonl`/`*-events.jsonl` and `ui-observations.jsonl`/`*-ui.jsonl`, there is no explicit file list, and no `walkdir` is used.
- **The red-team mitigation is false.** "Individual trace ingestion errors are caught and logged as warnings ... flagging the unparseable trace in the matrix output": unparsable
  files are skipped silently, with no warning log and no matrix entry (a corrupt file next to three good ones produced `Discovered 3 trace sessions`, nothing more).
- **"Deterministic" and "automated regression gate" were overstated.** Two runs gave a byte-identical matrix on one machine; cross-machine identity is unchecked, and nothing in CI
  or the repository's scripts runs the CLI.
- **Defects found while checking, not claims of this report:** (1) `run_batch` named its scratch directory after the trace's `session_id` and deleted it recursively, so a crafted id
  such as `/../../../victim` made a batch run delete a directory outside the temp base (reproduced in a sandbox with a decoy; fixed on branch
  `maelstrom/batch-session-dir-safety` (commit 84e85cb, not merged), with a test that fails on the old code). (2) The `maelstrom_batch_<uuid>` base directory is left behind after every run. (3) A session whose bridge cannot be
  created counts in `Total Sessions` but in neither the pass nor the fail count (read from the code, not triggered).
- **Verified:** `docs/REPLAY_BATCH_BENCHMARK.md` and the `INDEX.md` entry exist; the exit code is 2 when a session is below 95.0% (run: 1 of 3 failed); the claim release for `W101-REPLAY-BATCH-CORE-02`
  appears in `claims.json`. That file's release notes for both batch tasks (coordinator-owned, not edited) point at `docs/BATCH_REPLAY_VALIDATION.md`, which does not exist, and say
  "parallel" evaluation, which the sequential `run_batch` does not do.
