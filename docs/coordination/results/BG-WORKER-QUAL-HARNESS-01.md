# BG-WORKER-QUAL-HARNESS-01 — offline qualification plan generator

Status: **DRY-RUN ONLY — no live execution, no pin promotion, no client attachment.**
Date: 2026-11-17 · owner: root · workstream: `W101-COMBAT`

## Delivered

- `tools/qualify_bg_worker.py`:
  - `plan(cycles)` produces the deterministic 7-step qualification sequence: self-test → pin worker SHA → exact-target attach → 3–5 bounded cycles → **one deliberately unfocused cycle** → cleanup verification → receipt record.
  - Bounds: 3–5 cycles, 64-hex image pin, explicit gates for identity, no-input, unfocused publication and hook restoration.
  - `--live` **always refuses** (`live-execution-not-implemented`) even with owner token and reviewed pin flag; `refuse_live()` rejects missing token and unreviewed pin. No process launch or game access exists in this tool.
  - `--dry-run` output is byte-stable.
- `tools/tests/test_qualify_bg_worker.py` — 5 tests covering bounds, unfocused/cleanup gates, live refusal and deterministic dry-run.

## Verification

904 Python tests pass (8 skipped), black clean, encoding gate 0 violations, board 30 workstreams valid. No native C# change in this task (last C# checkpoint: 3,109 passed / 0 failed at `fa00bd8`).

## Limits

This is a plan, not an execution. No live qualification occurred; no SHA pin was promoted; no game process was contacted. A future owner-authorized window must implement the live steps under the shared owner with target fence, cancellation, cleanup verification and durable receipts. The owner token constant is a gate marker, not authorization to execute.
