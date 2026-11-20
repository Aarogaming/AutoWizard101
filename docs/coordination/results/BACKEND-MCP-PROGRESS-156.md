# MCP observation progress check — 156

Date: 2026-11-15  
Scope: implementation and mocked offline checks only.

## Delivered

Added `maelstrom_check_observation_progress(operation_id)`. It accepts only the 32-hex request IDs returned by the native observer and asks the existing native owner for its current `state`:

- Exact ID still present as `observerOperation` → `active`, `outcome_known: false`.
- ID absent, different, or no longer represented → `outcome_unknown`, `outcome_known: false`.
- Owner error → propagated without conversion to completion.

The tool never uses global facts to claim that a particular operation completed. It does not wait, dispatch, cancel, connect, or read a game window. This is a lightweight progress convenience over existing native state, not the durable terminal receipt identified in report155.

## Verification

- `python -m unittest discover -s tools/tests -p test_maelstrom_mcp_server.py`: **22 passed**.
- Added coverage for exact active-ID matching, missing/different/malformed owner state, invalid arguments, owner errors, and schema/description semantics.
- No live MCP process, native owner, client, game, or desktop was queried. No build, package or install was run.

## Remaining gap

Once the observer clears, the native owner still lacks a terminal receipt correlated by operation ID. This tool deliberately reports unknown in that case. Native receipt retention and transport parity remain the next backend task.
