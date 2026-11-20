# W101-BG-WORKER-DESIGN-01 — requirements for a pinned background facts worker

Status: **DESIGN / HANDOFF — no implementation, no live qualification.**
Date: 2026-11-17 · owner: root · workstream: `W101-COMBAT`

## 1. Why this is the boundary

`W101BackgroundReader.RunAsync` is implemented and tested with an injected `readCycle`. The real delegate cannot be wired offline because:
- memory readers must be pinned by SHA-256 before `WizwalkerWorker` will start them;
- the only reviewed readers today (`tools/w101_ui_tree.py`, `w101_pose_reader.py`, `w101_zone_reader.py`) are embedded in the **foreground-gated** UI-tree operation path (`W101UiTreeOperation.cs:138,175`);
- a background variant must be exercised against this client build (`d410a246…`) to earn its pin and prove it needs no foreground — which requires a live window.

Everything below is specified so that window is short.

## 2. Worker contract (proposed `tools/w101_bg_facts.py`)

Input (JSON on stdin, bounded): schemaVersion, target triple (HWND/PID/start ticks), client image SHA-256, source roots and pinned reader paths, `intervalMs`, `maximumCycles`.

Output per cycle (JSONL only, no prose to UI):
`{schemaVersion, runId, target, observedUtcTicks, facts[], status, code, cleanupConfirmed, hookRestored, note}`

Hard rules:
- **No foreground check and no activation.** Identity is verified from process state (HWND/PID/ticks/image hash), never from `GetForegroundWindow`.
- Memory-only reads through the pinned readers; no window capture, OCR, clipboard, network or game-file writes.
- No keyboard/mouse hooks beyond the reviewed root-hook path already required by the UI-tree reader; if a cycle cannot confirm restoration, emit `cleanupConfirmed:false` and stop.
- Fact IDs must come from the registered catalog or explicitly proposed additions with `ProducerAvailable=false`; no new fact is born Observed.
- Unknown is an explicit value: expired, identity-changed or failed reads yield no facts plus a reason.
- Bounded: 1–5 s interval, hard cycle cap, per-read timeout, 384 MiB worker cap, cancellation within 2 s.

## 3. Pin qualification sequence (requires owner-authorized live window)

1. Write the worker with a `--self-test` mode that reads nothing live and emits a fixed schema; pin its hash in C# only after its own tests pass.
2. In one bounded live pass on the actual client: launch under `WizwalkerWorker` with exact target identity, run 3–5 cycles, assert clean disconnect/hook restoration and `cleanupConfirmed:true` with the window deliberately **not foreground** for at least one cycle.
3. Record receipt, hashes and observed facts in a result document; only then mark the pin reviewed.
4. If cleanup is uncertain at any point: honor recovery hold, no retry, no pin promotion.

## 4. Integration points after qualification

- `W101BackgroundReader`'s `readCycle` becomes a delegate over `WizwalkerWorker.ExecuteJsonAsync` using the pinned worker; publish results via `W101ActivityFacts`-style candidate-only mapping.
- Feed `W101BackgroundFacts`; existing native pipe `facts` and MCP `maelstrom_get_w101_facts` need no changes.
- Activity flags remain triggers; background facts remain candidates until separately qualified.

## 5. Boundaries

No live work, input dispatch, network manipulation, game-file writes, new dependencies or installation is authorized by this document. It defines a handoff, not an implementation.
