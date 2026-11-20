# Logic editor reuse and overlay — 2026-11-12

## Decision
Use an existing visual logic tool as an authoring/client surface over Maelstrom's existing scheduler. Do not add a competing input owner or turn each visual node into a separate process. No production dependency added in this research increment.

| Candidate | Reusable capability | Fit and cost |
| --- | --- | --- |
| [Node-RED](https://github.com/node-red/node-red), Apache-2.0 | Complete event-driven visual editor/runtime, reusable subflows, scoped variables, existing integration nodes | First external-workbench prototype. Requires Node runtime; custom bridge must call the existing local API. Its generic HTTP nodes cannot directly consume our named pipe. |
| [Rete.js](https://github.com/retejs/rete), MIT | Visual node editor plus data/control-flow graph processing | Strong embedded card-editor candidate. Requires web UI integration and explicit translation to our canonical graph; not a drop-in WinForms control. |
| [Elsa Workflows](https://github.com/elsa-workflows/elsa-core) | .NET workflow engine, designer, custom activities | Closest language fit, but adopting its execution/persistence machinery overlaps our scheduler. Evaluate authoring and activity patterns before replacing runtime. |
| [BehaviorTree.CPP](https://github.com/BehaviorTree/BehaviorTree.CPP), MIT | Reactive behavior execution, asynchronous actions, typed data flow | Strong game behavior reference/optional engine. C++ bridge adds complexity. Groot2 has separate licensing; do not assume the editor shares the runtime license. |
| [OpenPLC Editor](https://github.com/Autonomy-Logic/openplc-editor), GPLv3 | Actual visual logic programming editor | Useful industrial editing/debugging reference; direct code reuse needs license review. |
| [Beremiz](https://github.com/beremiz/beremiz) | Editor/CLI/runtime separation | Useful architecture reference; licenses vary by component. Full industrial runtime is unnecessary for game actions. |

Sources reviewed online on this date; upstream descriptions are not Maelstrom integration benchmarks. Node-RED context/subflow documentation: https://nodered.org/docs/user-guide/context and https://nodered.org/docs/user-guide/editor/workspace/subflows .

## Concrete integration contract to implement next
Existing JOB_DECK documents current-user named-pipe API, MCP delegation, catalog/state/snapshot, validation and start/pause/stop. It is request/response, not proof of an existing streaming subscription.

1. Export capability IDs, parameter schemas, availability and condition types into the editor palette. Cards/decks are versioned programs; UI-tree fields become typed observed facts, not writable authority.
2. Add bounded local subscriptions: snapshot then changes, monotonic sequence, target/session identity, observed timestamp, source, expiry, explicit unknown, reconnect/resync. Coalesce ordinary updates; preserve important transitions and receipts; bound queues and avoid streaming every raw tree/frame.
3. Execute through existing owner with request ID, graph revision, arguments, deadline, cancellation and resource limits. Return admission, dispatch receipt and independently verified outcome separately.
4. First reusable blocks: fact condition, wait-until, registered action, verified outcome, bounded retry, branch and subroutine. Loading pauses budgets; focus loss pauses; takeover remains respected.
5. Import/export one canonical graph. Round-trip graph -> editor -> graph, replay original and edited variants, test stale facts/disconnect/duplicate request/cancellation. Begin with saved telemetry and zero live input.
6. Prototype an optional Node-RED bridge first to test third-party control without rebuilding an editor. Evaluate Rete for the embedded deck canvas afterward. Dependency approval and pinned license/revision review precede integration.

## Rust reality and timing
Five crates exist. Documented parity: 56 shared geometry/codec cases; Dance 390 observations/15 scenarios; waypoint 59 observations/27 scenarios; pause clock 51 observations/25 scenarios. Waypoint and pause tests were standalone Rust; these staged modules are not production host replacements. Prior Cargo dependency/lock conflicts remain an integration gate, not a current re-tested failure. No worker is being claimed as continuously running and no new Rust benchmark was performed here.

Rust can reduce allocation/GC jitter in measured hot paths and improve ownership safety. It cannot remove Windows foreground restrictions, game loading latency, stale observations or intentionally coarse polling. First measure capture-to-observation, decision, dispatch, acknowledgement and observed effect (p50/p95/p99), then replace one pure component behind a versioned bridge and compare identical replay fixtures. Do not promise a timing improvement from a language change alone.

## Overlay
W101RunningNotice now applies a union-of-three-circles window region, refreshed on size changes and disposing the previous region. The rectangular area and gaps no longer cover or intercept the game; circles retain active-symbol colors and existing non-activating controls. Offline region tests cover normal and scaled geometry. No live desktop test or new installer for this shape change in this task.

Validation: 28 focused overlay tests pass; warnings-as-errors build, scoped whitespace/verification and analyzers pass. Initial restricted test/formatter attempts could not connect local host pipes; reruns with local-host access passed. Board check passes. Installed prior release replay passed separately.
