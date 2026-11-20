# Editable Node-RED card — 2026-11-12

Task43 completed offline editor/runtime qualification. Pinned Node-RED5.0.9 installed only in .local/node-red-43 with --ignore-scripts, no global or production install. Editor bound to loopback18843, Node heap capped256MiB, dynamic installs disabled. Installed Maelstrom host used separate .local/node-red-43/data, disconnected throughout.

Browser verified custom palette node, editable node properties, name edit, Deploy, manual injection and successful Stop API reply. Screenshot: .local/node-red-43/editor-proof.png. Example persisted under integrations/node-red/examples/offline-card.json; setup and limits in adjacent README. Native Stop returns Paused; this is tested owner control, not gameplay success.

One real integration test passed: persisted editor edit; false condition leaves Stopped; true condition calls existing Stop and yields Paused; disconnected before/after. Four bridge tests and JS syntax pass. No C# changed; prior2550 native result remains historical, not rerun. No JS formatter/linter configured. No installer changes in this task.

The visual flow reuses standard Node-RED logic nodes and the existing Maelstrom client. It is not yet native graph/card round-trip authoring. Fixed request ID is restricted to this Stop rehearsal; do not copy it into repeated start/step operations. Poll subscriptions are latest-state, not lossless event journals.

User observation recorded this turn: farming spells have approximately10seconds cooldown. This is user-reported, not live-qualified; spell-specific readiness and configurable timing should govern a future farming card. No farming action implemented or sent.
