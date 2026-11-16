# BACKEND-MCP-WINDOWS-153 — MCP target-window discovery

Date: 2026-11-15  
Workstream: `CORE-DELIVERY`  
Scope: Bounded read-only parity slice; no game or desktop interaction.

## Change

Added `maelstrom_list_target_windows` to the MCP server. It delegates to the existing local native owner with the `windows` command through `call_deck`; it does not enumerate windows independently, start another owner, connect to a process, change foreground, capture frames, or send input. The tool accepts no arguments and passes the native success or unavailable response through unchanged. The native host implementation is in `JobDeckHost`.

## Verification

- `python -m unittest discover -s tools/tests -p test_maelstrom_mcp_server.py`: **16 tests passed**.
- Python AST syntax check for the changed module and test file: **passed**.
- `git diff --check` for the changed implementation/test paths: **passed**.
- `python -m black --check ...`: invocation stalled without output and was cancelled after about20 seconds; formatting check is **unverified**, not passed.
- No native host process was launched, no live window list was queried, and no desktop/game input occurred.

## Boundary

This closes only one safe discovery gap. It does not add MCP connect/observe or input operations and does not make every CLI, API, MCP, and UI operation equivalent. Target selection and observation remain separate, explicitly bounded backend work.
