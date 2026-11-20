# COMBAT-UI-FIXTURE-01 — saved boss-battle UI vocabulary fixture

Status: **OFFLINE FIXTURE — candidate names only, no live reads or input.**
Date: 2026-11-17 · owner: root · workstream: `W101-COMBAT`

## Delivered

- `tools/build_combat_ui_fixture.py` — deterministic extractor over saved `ui-observations.jsonl` files; emits node names, observation/visibility counts and SHA-256 of each source. No pixels, text, memory addresses or input records.
- `tools/fixtures/combat_ui/combat-ui-observations-v1.json` — generated from 3 owner-authorized boss-battle passes (receipts `a26b8b8…`, `1d26e3a4…`, `f1fd715f…`): **75 tree samples, 143 distinct node names**, sources hash-pinned.
- 2 focused tests; source files remain in ignored `.local` artifacts.

## Verification

899 Python tests pass (8 skipped), black clean, encoding gate 0 violations, board valid. Native C# unchanged since `fa00bd8` (3,109 passed / 0 failed).

## Limits / next use

These are candidate UI names from an archived layout reader on one client build (`d410a246…`). They are not screen coordinates, combat facts, or action authority. They are the deterministic fixture material for mapping a reviewed combat profile later (see `W101-BUILD-PROFILE-01.md`), and for testing activity flags without live sessions. No live capture or client attachment occurred in this task.
