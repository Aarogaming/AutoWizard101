# Claim Evidence: W101-TRACE-INGEST-DOCS-01

## 1. Overview
- **Task ID**: `W101-TRACE-INGEST-DOCS-01`
- **Workstream**: `CORE-TRANSITIONS`
- **Owner**: `coordinator`
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
