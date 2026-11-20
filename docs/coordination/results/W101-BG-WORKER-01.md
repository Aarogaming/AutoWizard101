# W101-BG-WORKER-01 — offline background facts worker contract

Status: **OFFLINE CONTRACT DELIVERED — inert until a separately authorized pin-qualification pass.**
Date: 2026-11-17 · owner: root · workstream: `W101-COMBAT`

## Delivered

- `tools/w101_bg_facts.py` — pure request validation (exact HWND/PID/ticks/image hash, bounded interval/cycles), candidate-only fact mapping with provenance and explicit rejection of unknown IDs, deterministic `self_test()`, and an inert `run()` that admits a target but publishes **no facts** (`reader-not-qualified`).
- **No foreground/focus dependency by construction**: a test proves adding `foreground:false`/`focused:false` does not change validation. No process launch against the game, no memory reads, no capture, no input, no network, no game-file writes.
- `tools/tests/test_w101_bg_facts.py` — 6 tests including a real subprocess self-test.

## Verification

- 6/6 focused worker tests; **897 Python tests total** pass (8 skipped); black clean; encoding gate 0 violations; board 30 workstreams valid.
- Native suite unchanged since `fa00bd8` (3,109 passed / 0 failed); this increment contains no C# change.
- **No SHA-256 pin was promoted.** The worker is deliberately inert in normal `run()` mode.

## Remaining (requires live authorization)

Per `W101-BG-WORKER-DESIGN-01.md`: pin the worker after its self-test, then one bounded live pass (3–5 cycles, exact target, no input, no focus change, at least one deliberately unfocused cycle, verified hook restoration) before wiring it into `W101BackgroundReader.readCycle`. No such pass was performed.
