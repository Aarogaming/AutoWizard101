# TEACHING-STATE-37 — timestamped state and local card proposals

2026-11-12 · CORE-DELIVERY · source/offline increment, not installed/live-qualified.

## Delivered

- Session Timeline: Attach state track, Import W101 observer, Review card proposals.
- Cursor inspector shows latest preceding snapshot, source, context, age, validity,
  optional raw UI tree and scalar facts. Future readings never fill earlier frames;
  readings older than 2 seconds are stale. Unknown/loading snapshots replace older
  values. This contract uses full snapshots, not per-field delta merges.
- Game-neutral JSONL state track bound to exact session ID, PID, process start,
  window and process name. 32 MB / 20,000 samples / 128 facts / 128K tree limits.
  Imported tracks are copied under SHA-256 names and checked before review.
- Separate W101 bridge reads existing ui-request.json + ui-observations.jsonl;
  no process access, hooks, input or new observer is started. It preserves tree
  acquisition time, raw nodes, stable UI states, interaction title and available
  quest/pose scalar context. Missing health/energy producers are not invented.
- New recordings save a UTC-to-QPC anchor. Existing recordings without it cannot
  import UTC-only W101 traces by guessing an offset. UTC-aligned imports identify
  their timing source; wall-clock adjustments/drift are not independently corrected.
- Deterministic review fills before/after scenes from fresh readings, lists fact
  changes, retains human commentary and warns on gaps/context changes. Action IDs
  remain user-supplied. Proposals are editable, needs-review, non-executable and do
  not claim causation, dispatch receipts or live qualification.

## Headless use (source executable)

```text
ProjectMaelstrom.exe --teaching-attach-state <teaching-session.json> <state.jsonl>
ProjectMaelstrom.exe --teaching-import-w101 <teaching-session.json> <ui-request.json>
ProjectMaelstrom.exe --teaching-review <teaching-session.json> <new-report.json>
```

Attach/import explicitly updates the saved session's StateTrack pointer and
preserves the evidence under a content-derived filename. Review creates a new
report, refuses overwrite and does not mutate the card/deck or launch the game.
The GUI calls the same implementation. These commands return before WinForms
startup. Existing deck API/MCP still handle cards/validation/execution; the new
teaching commands are CLI-only, not yet added as API/MCP operations.

Generic state row example (one JSON object per line; timestamps in milliseconds
relative to the matching session's QPC origin):

```json
{"Version":1,"SessionId":"matching-session-id","Target":{"ProcessId":123,"StartedUtcTicks":456,"WindowHandle":789,"ProcessName":"game"},"TimeMs":1000,"Source":"game-adapter-v1","Context":"zone-visit-1","Status":"observed","Scene":"menu","Facts":{"health":100,"ui.menu":true},"UiTree":null}
```

Each row is a full snapshot of that adapter's available readings, acquired at
TimeMs. Do not re-timestamp cached facts as fresh. Unsupported data is omitted or
null. The adapter is responsible for truthful acquisition times/qualification;
import validates structure and binding, not the truth of game claims. Arbitrary
commentary is retained as human interpretation, not parsed as executable commands.

## Remaining gates

Continuous scheduler-owned telemetry alongside video, independent field sampling,
sharded hour-scale telemetry, automatic activity segmentation, action-binding
inference, repeated-evidence/counterexample ranking and held-out live qualification
remain pending. Current proposal generation requires manually marked activities.
The system video player and timeline cursor are separate; no frame-accurate embedded
video viewer is claimed. No tray icon added; normal application minimization exists.
No game/desktop access, installation, volume changes or model use in this task.

## Verification

Focused state/session tests: 14 passed before final hardening. Full suite and CLI
smoke-test results are recorded below after completion.

Final verification: 2,538 native tests passed (zero failures/skips), warnings-as-errors
compilation passed, scoped whitespace and analyzer checks passed. Actual built
executable --teaching-attach-state and --teaching-review both exited 0 against
synthetic files in .local/teaching-state-37-smoke; report preserved commentary,
proposed world → menu.open → menu, and explicitly retained needs-review and
live_qualified=false. No desktop window or game was opened.

Side task requested by owner: minimize Maelstrom to a system-tray icon while
client operations run; retain W101 overlay play/pause/stop, tray restore and stop
controls. Keep owner/cancellation alive while hidden and do not hide blocking
recovery/error state. This is backlog, not implemented by this increment.
