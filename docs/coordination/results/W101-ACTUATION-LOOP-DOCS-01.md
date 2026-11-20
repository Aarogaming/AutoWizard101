# Claim Evidence: W101-ACTUATION-LOOP-DOCS-01

Status: **Corrected** (audited 2026-11-19; the doc described a live mode that does not exist and has been amended; see "Audit note" at the end).

## 1. Overview
- **Task ID**: `W101-ACTUATION-LOOP-DOCS-01`
- **Workstream**: `CORE-TRANSITIONS`
- **Owner**: `coordinator`
- **Scope**: Documentation of the `JobDeckHost` actuation event loop (`actuate` named-pipe command), wiring `SteeringCommandSummary` and `KinematicPlanSummary` to `DesktopInputActuator` under simulated playback and live lease modes.

## 2. Changes Made
1. **Created Documentation**:
   - `docs/JOB_DECK_ACTUATION_LOOP.md`: Complete specification detailing:
     - The `actuate` named pipe command and argument schemas.
     - Sequence diagram tracing the end-to-end perception-decision-actuation event loop.
     - Simulated playback mode (`simulation: true`): In-memory event recording (`dispatchedEvents`), zero OS/UI disturbance, full receipt return.
     - Live lease mode (`simulation: false`): Exclusive `GameInputLease.Current` verification, foreground readiness, mid-pulse monitoring, and recovery escalation.
2. **Updated Cross-References**:
   - `docs/BACKEND_CONTROL_PLANE.md`: Added Section 5.4 linking the `actuate` command to the Rust autonomy bridge.
   - `docs/INDEX.md`: Added indexed entry for `docs/JOB_DECK_ACTUATION_LOOP.md`.

## 3. Verification & Compliance
- **Text Encoding Check**: `python tools/check_text_encoding.py` $\rightarrow$ 0 violations.
- **Project Board Check**: `python tools/check_project_board.py` $\rightarrow$ Valid (30 workstreams, zero broken references).
- **Claim Integrity**: Bounded strictly to declared documentation paths.

## Audit note (2026-11-19, Claude)

The cross-references exist: the `actuate` item in section 5 of `docs/BACKEND_CONTROL_PLANE.md` (item 4 of the bridge list) and the
`docs/INDEX.md` line. The original text above is kept unchanged.

**Corrections.**
1. **"Live lease mode: ... foreground readiness, mid-pulse monitoring, and recovery escalation" is not what the host does.** The live
   branch drives a stub transport that returns `false` for every send: it never sends input, it passes `ready = _session != null`,
   and when it reaches the press it ends, after about 5 s, in a recovery hold (see the
   [W101-ACTUATION-LOOP-CORE-02](W101-ACTUATION-LOOP-CORE-02.md) audit note). The sequence diagram's `accepted: true, released: true`
   reply for a held lease cannot occur.
2. **Simulated playback is described correctly,** except that it is not instant (a wall-clock busy-wait up to 500 ms) and that the
   Rust snake_case shape is rejected by the host's camelCase reader.
3. **Where the doc was amended:** a status banner, a note on the diagram, a request note, a simulation note and a rewritten
   section 4 in `JOB_DECK_ACTUATION_LOOP.md`; item 3's invariant sentence and item 4 in `BACKEND_CONTROL_PLANE.md` (the first now
   says "intended design"); the `INDEX.md` line.
