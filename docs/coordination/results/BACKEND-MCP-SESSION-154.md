# Backend MCP observation session — 154

Date: 2026-11-15  
Scope: offline implementation and mocked contract checks only.

## Delivered

Added three MCP operations that reuse the existing native JobDeckHost owner:

- `maelstrom_connect_observation_target` requires an exact HWND, PID, and process start-tick identity, plus explicit client-observation and UI-tree opt-ins. It always disables quest actions, travel actions, and resource observation.
- `maelstrom_observe_connected_target` requires an explicit read-only confirmation and forwards only the native `observe` command. A queued response remains queued; it is not represented as a completed reading.
- `maelstrom_disconnect_observation_target` forwards the native disconnect command and preserves the host's refusal while work or cleanup is active. It cannot stop an operation.

All three schemas reject undeclared arguments. The tools do not launch the host, enumerate a live target, attach to a client, change focus, capture frames, or send input unless separately invoked by an MCP caller. No live call was made for this task.

## Verification

- `python -m unittest discover -s tools/tests -p test_maelstrom_mcp_server.py`: **20 passed**.
- Tests cover exact identity mapping, forced-off capabilities, required opt-ins and confirmation, malformed-argument rejection, queued-result fidelity, disconnect behavior, and strict schemas.
- Live-host, end-to-end MCP transport, desktop, game, packaging, and installation checks were not run.
- Formatting was not run; the formatter had stalled in the prior checkpoint. Syntax is exercised by importing the module during the passing tests.

## Remaining integration gap

This is a narrow MCP session surface, not full CLI/API/MCP parity. Observation results are asynchronous and these tools do not yet expose a dedicated operation-status poll/wait contract. The caller must use the existing owner state/telemetry tools and handle native queue/cleanup states truthfully. A future status operation should be added only after matching the native owner’s actual operation receipt and cancellation semantics.
