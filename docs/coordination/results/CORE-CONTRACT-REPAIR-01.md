# CORE-CONTRACT-REPAIR-01 — authoritative routine validation

Date: 2026-11-11. Workstream: CORE-DELIVERY. Owner: card_audit.
Status: source repair and Python checks complete; coordinator owns native formatting/build/test verification. Not packaged, installed or live-qualified by this task.

## Problem and change

The advertised MCP `validate_routine_graph` tool used a separate Python validator that accepted a schema-2 `routine_id` shape which the native schema-1 replay reader rejected. Its test incorrectly called that shape valid. Validation now uses the actual native contract instead of maintaining two definitions of a routine.

- `ProjectMaelstrom/ProjectMaelstrom/Utilities/JobDeckHost.cs` adds read-only `validate-routine` with `{graph: <object>}`. It calls `RoutineGraphs.Read`, returns structural acceptance/rejection and the authority `native-routine-graphs-v1`, and always reports `liveQualified=false`. It is allowed with the other read-only calls during draining. It does not connect a game, submit scheduler work, resolve trusted live actions, change the deck or acquire input.
- `tools/maelstrom_mcp_server.py` removes the divergent validator. Its existing tool performs bounded JSON preflight and delegates through `call_deck`. Confirmed native acceptance/rejection is distinct from unavailable or malformed replies. An older/missing owner, invalid response, oversized input or non-JSON data produces `valid=false, confirmed=false`; there is no local success fallback and no automatic host launch.
- Existing transport limits and timeouts remain unchanged. No new runtime dependency, API host, controller or generic execution path was added. The HTTP server need not expose this new command; the dedicated MCP validation tool calls it directly through the existing bridge.
- The MCP `get_w101_policy_state` description and result now explicitly identify their scope as the Python generic legacy launcher. Its existing restrictions remain in force. `native_opt_in_state` is `not_observed`; the reviewed native exception is linked as a separate policy document. Inspecting this report does not enable a policy, call the native bridge or infer any current session permission.
- `docs/JOB_DECK.md` documents the native graph shape, command, bounded structural scope and the need for an updated host. An older installed executable cannot serve the new command.

`confirmed=true` means the canonical native structure validator answered, not that a routine has passed replay, has permitted action bindings, or can control a live game.

## Tests and validation

Python checks performed:

- `python -m unittest discover -s tools/tests -p test_maelstrom_mcp_server.py`: **14 passed**.
- `python -m black --check tools/maelstrom_mcp_server.py`: passed.
- `python -m black --check tools/tests/test_maelstrom_mcp_server.py`: passed.
- `python -m compileall -q tools/maelstrom_mcp_server.py tools/tests/test_maelstrom_mcp_server.py`: passed.

The Python tests mock the native bridge. They establish routing, result interpretation, rejection of invalid confirmations, explicit unavailability and no bridge call for malformed/nonfinite/oversized inputs. They do not pretend to exercise the actual native graph parser.

Native tests added to `ProjectMaelstrom/ProjectMaelstrom.Tests/JobDeckHostTests.cs` exercise the actual host/parser with a built-in native schema-1 graph and reject the prior MCP-only shape, schema 2, missing start/edge targets, invalid budget, unknown/duplicate properties, invalid roots and an oversized graph. They assert unchanged deck state, no connection, no scheduler history/pending work, no input lease and no live qualification. The coordinator runs these tests and all required native formatting/analyzer/build checks in the shared build slot.

## Limits and follow-up

This closes the identified graph-validation contract drift at the source level. It does not implement typed fact capabilities, observation refresh while a deck waits, module-to-card integration, combat dispatch, general migrations or new gameplay. Those remain separate findings in `CORE-CARD-AUDIT-01.md` and the coordinator's roadmap.

No desktop use, W101 connection, model, installer or gameplay was started. No installed/live result is claimed.
