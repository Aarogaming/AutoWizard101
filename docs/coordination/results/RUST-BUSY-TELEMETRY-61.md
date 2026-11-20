# Busy-pipe recovery and live Rust telemetry

Status: complete for bounded connection recovery and passive live projection. Source-only; not packaged or installed. Native scheduler/control ownership remains authoritative.

## Changes and diagnosis

Rust retries only ERROR_PIPE_BUSY before connection, with a 1.5-second deadline, 25/50/100 ms capped backoff and cancellation. Missing/denied/broken pipes fail immediately. No sent request is replayed. Both preview poll paths and the diagnostic CLI share recovery. Snapshot reports expose the actual qualified Rust projection alongside the host response.

The first live observer completed and restored its hook, but the native host discarded its facts: publication incorrectly used quest-action readiness, which rejects native Idle. ObservationReady now validates target, schema/source, freshness, loading, stable state consistency and cleanup independently of action readiness. Quest/action guards remain unchanged.

## Live evidence

Initial diagnostic root: `.local/rust-w101-61-318d0453ed474cb88c03540caebcb782`.
Successful source-host root: `.local/rust-w101-fixed-61-a671644291f44b088fffdefa94cc5975`.
Operation: `2df6df22f05445abbccfe5f2af8ffc64`; successful Rust report `snapshot-13.json`.

The native scheduler ran one bounded optional observer. Rust projected health 1022/1022, mana 76, energy 55, gold 1136, level 10 and nativeIdle=true with qualified wizard identity. Raw/native gold matched Rust gold. Observation completed with hook restored and cleanup confirmed; inputsDelegated=false. No desktop inputs, capture, focus changes or volume changes occurred. W101 remained in native Idle.

After three seconds, `expired.json` cleared health. After disconnect, `disconnected.json` showed connected=false and health unknown. The source host exited 0 through its stop-file protocol. Diagnostic polling was bounded at 250 ms for this short verification; production preview polling remains at most 1 Hz and non-overlapping.

## Validation

- 2,627 native tests passed, including 11 new observation gate cases (83 focused tests passed first).
- 76 Rust workspace tests passed, including five busy-recovery tests and an actual isolated Windows busy-pipe test.
- Rust Clippy with warnings denied and scoped rustfmt passed.
- Scoped native formatting/analyzer verification and project-board checks recorded with final integration.

## Limits and follow-up

This verifies read-only telemetry transport and qualification, not gameplay or the Rust GUI. The installed release still predates this source repair. Non-ASCII path parity, empty-root usability and independently qualified identity metadata remain follow-ups; freshness alone must not bypass identity safeguards. Gemini coordination is recorded through LIA-0022; external recurrence remains wake-up only, with no source/build/game grant.
