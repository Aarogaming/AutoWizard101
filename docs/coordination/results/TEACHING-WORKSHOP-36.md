# TEACHING-WORKSHOP-36 — recording to editable activities

2026-11-12 · CORE-DELIVERY · source/offline increment; not installed or live-qualified.

Added a model-free Session Timeline entry in the selected game workspace. Observe
is stopped first; active recording/import/Dodge runs block entry. Sequential
2-minute GPU recording chunks support a requested 1–60 minute session. Per-chunk
input hooks/audio are opt-in; atomic indexes retain completed chunks after later
failures. Target PID/start/window and QPC timestamps identify the recording.

Timeline cursor and activity ranges support manual labels, conditions, action IDs,
expected outcomes and commentary. Export creates the existing bounded RoutineGraph
and provenance sidecar; replay uses the existing evidence-only runner, never a
new executor or fabricated receipt. No model/dependency/game-specific logic added.

Verification: full native suite passed 2,530 tests before the final commentary and
target metadata addition. Final focused rerun is recorded below. Warnings-as-errors
compilation, scoped whitespace verification and analyzer verification passed.

Limits: no live/hour-long recording performed, no installer produced. Video opens
in the system player; timeline does not yet provide embedded frame scrubbing.
Automatic activity discovery, UI-tree/stat telemetry capture, long-session replay
sharding and automated candidate generation remain pending. Existing raw input
recordings are not automatically converted into verified routine receipts. Chunk
boundaries contain measurable gaps; do not call recording seamless or 60 fps
qualified. The 20 GB cap is checked between chunks, allowing one-chunk overshoot.

See docs/TEACHING_LONG_SESSIONS.md for the synchronized game-state track design:
source/age/validity per reading; human commentary remains separate from evidence.

Final focused verification: 12/12 session/card/replay tests passed after commentary and target metadata changes. Project-board check passed (30 workstreams).
