# MCP observation ID casing correction — 157

Date: 2026-11-15  
Scope: focused validation-only correction; mocked offline tests.

The progress tool schema accepts hexadecimal operation IDs in either letter case. Its active-ID comparison now uses case-insensitive hexadecimal text comparison, so the same UUID copied with upper- or lower-case letters does not become a false `outcome_unknown`. Non-string or different IDs remain non-active/unknown, and no terminal success is inferred.

Verification: `python -m unittest discover -s tools/tests -p test_maelstrom_mcp_server.py` — **22 passed**. `python -m compileall -q` passed for the MCP server and test file. No live owner, game, desktop, or external process was queried.
