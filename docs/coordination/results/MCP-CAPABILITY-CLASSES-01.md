# MCP-CAPABILITY-CLASSES-01 — capability classes for the MCP server

Date: 2026-11-19 · owner: root · claim: `MCP-CAPABILITY-CLASSES-01`
Ported policy: `D:\Aaroneous\docs\architecture\MCP_SERVICE_CONTRACT.md` (MIT;
policy ported, no code copied).

## What changed

- `tools/maelstrom_mcp_server.py`: an explicit capability class for all 24
  registered tools (`ReadOnlyInspection` / `BoundedProposal` / `Verification`;
  see `TOOL_CLASSES`), with the contract's fail-closed default: an unregistered
  tool name is treated as `Mutation` and denied (`capability-denied`) unless the
  session grants it (`allow_mutation=True` or `MAELSTROM_MCP_ALLOW_MUTATION=1`).
- No current tool is classed `Mutation`: input dispatch remains behind the
  native CLI and the single-owner lease. Existing tool behaviour is unchanged.
- `tools/tests/test_maelstrom_mcp_server.py`: 7 new tests (full class coverage,
  four-class validity, no-mutation current state, default denial, session and
  environment grants, inspection not denied).

## Also in this change

`tools/check_claim_evidence.py` gained an exemption found while gating this
work: a released claim's path that is uncommitted is exempt when an ACTIVE
claim owns it (claimed in-flight work), so historical ownership does not flag
legal current edits.

## Evidence

- `python -m unittest tools.tests.test_check_claim_evidence tools.tests.test_maelstrom_mcp_server`
  → **54 tests, OK** (18 + 36; 18 = 17 prior + 1 new exemption test).
- `python tools/check_claim_evidence.py --strict` → 0 warnings, 0 new registry
  violations, 0 stale baseline entries, exit 0.
- `black --check` on the four changed files → unchanged; `compileall` clean.

## Limits

- The grant knob is session-scoped configuration, not a cryptographic token;
  the real authority boundary stays the native input lease.
- Classification is a static map: a tool added without updating `TOOL_CLASSES`
  becomes default-denied (fail-closed), which the tests assert indirectly.
- Offline tooling only; no live or game component.
