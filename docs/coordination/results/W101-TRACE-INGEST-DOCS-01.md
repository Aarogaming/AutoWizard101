# Claim Evidence: W101-TRACE-INGEST-DOCS-01

## 1. Overview
- **Task ID**: `W101-TRACE-INGEST-DOCS-01`
- **Workstream**: `CORE-TRANSITIONS`
- **Owner**: `coordinator`
- **Status**: Draft (no status was recorded; audited 2026-11-20, see "Audit note")
- **Scope**: Documentation for real gameplay trace ingestion (.local session and UI event streams), conversion to `AutonomyReplaySession` format, and batch offline replay verification reporting.

## 2. Key Deliverables
1. **Real Trace Ingestion Specification** (`docs/REAL_TRACE_INGESTION.md`):
   - Catalogued real gameplay artifacts in `.local`:
     - `.local/quest-step-check/TestData/Routines/w101-operations/events.jsonl` (19 events, 900ms routine navigation/interaction).
     - `.local/deck-release-20261111/combat-structure-live-data/.../ui-observations.jsonl` (16 live UI tree observations from PID 27396).
     - `.local/clean-capture-baseline-20261103/recording/timing.json` (3,205 verified frame intervals at 1280x720).
   - Specified schema normalization mapping raw JSONL observation events to `ReplayFrame` and `NativeHostState`.
   - Defined verification invariants (decision accuracy $\ge 99.5\%$, $\kappa < 5.0$, $J < 10.0$, 100% offline).
2. **Updated Documentation**:
   - `docs/AUTONOMY_REPLAY_CLI.md`: Added Section 5 linking real gameplay trace ingestion and batch validation.
   - `docs/INDEX.md`: Indexed `docs/REAL_TRACE_INGESTION.md`.

## 3. Red Team / Blue Team Adversarial Audit
- **Telemetry Inconsistency Across Game Updates**:
  - *Red Team Attack*: Real recorded gameplay traces may have missing or evolving UI node names across client versions, leading to deserialization failures in the offline replayer.
  - *Blue Team Mitigation*: `NativeHostState` uses flexible `serde_json::Value` payload wrappers with fallback default aliasing, ensuring that missing nodes do not panic and instead cleanly project safe fallback states.
- **Timestamp Drift & Ordering**:
  - *Red Team Attack*: Asynchronous capture files might contain disordered timestamps or clock jumps.
  - *Blue Team Mitigation*: Replay frames are validated strictly for monotonic non-decreasing `timestamp_ms` prior to evaluation.

## 4. Verification Evidence
- `python tools/check_project_board.py`: Valid (30 workstreams, zero broken links).
- `python tools/check_text_encoding.py`: 0 violations.

---

## Audit note (2026-11-20, Claude)
Checked against `from_w101_events_jsonl`, `from_ui_observations_jsonl`, `replay_session`, `NativeHostState` and the three `.local` files (read-only), and by replaying the two
JSONL files offline through `maelstrom-replay --batch-dir`. The 2026-11-19 corrections in the CORE-02 file stand.
- **"Real gameplay" and "empirical recordings" are overstated.** `events.jsonl` is a synthetic rehearsal (its first event says so). `ui-observations.jsonl` is a live UI watch, but none of its
  16 lines has `planningVisible` true, so the ingested session has 0 labelled ticks and its 100.0% accuracy covers nothing. Position, vitals, potions and zone are constants or formulas.
- **The catalogue counts are right but incomplete.** 19 events (11 observations, 7 receipts, 1 marker; only the observations replay, 800 ms), 16 UI lines (15 replay, 2,715 ms), 3,205 capture frames.
  The PID and HWND cited are not in the UI JSONL; they are in the sibling request, result and receipt files of the same run.
- **`timing.json` is not ingested.** The deliverable describes it as a trace source, but no code reads it, and it is a capture-timing report (53.39 fps delivered against a 60 fps
  target, largest gap 55.6 ms, queue rejected 0), not a gameplay trace.
- **"Schema normalization" mapping is partly invented.** No ingester reads combat tree nodes, zone annotations, odometry, an `idleText` node or filled bottles, and nothing is labelled
  `DrinkPotion`. The real mapping is now a table on the page.
- **Blue-team claim 1 is wrong in its mechanism.** `NativeHostState` is a typed struct, not a `serde_json::Value` wrapper. The ingesters read raw JSON with `.get(..).unwrap_or(default)`, so a
  missing node does not panic, but it also silently removes the label (accuracy then reads 100.0% on fewer or zero labelled ticks).
- **Blue-team claim 2 is false.** No code validates that `timestamp_ms` is monotonic: the events ingester copies `time_ms` unchanged, the UI ingester only clamps below the first line's ticks with
  `saturating_sub`, and `from_file` accepts any order (`duration_ms` is `last - first`, clamped at 0).
- **"Verification invariants (accuracy >= 99.5%, kappa < 5.0, J < 10.0)"** are not enforced as stated: the exit gate is 95.0%, the curvature gate cannot fail (|kappa| <= 2.0, heading is the z
  coordinate), and jerk never changes the exit code.
- **Verified:** `docs/REAL_TRACE_INGESTION.md` and the `INDEX.md` entry exist; `AUTONOMY_REPLAY_CLI.md` has the Section 5 link; the `.local` paths named in the page exist in this checkout
  (git-ignored, so they will not exist in a fresh clone). The project-board and encoding checks named under "Verification Evidence" pass on 2026-11-20.
