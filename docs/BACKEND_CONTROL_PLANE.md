# Maelstrom backend control plane

Status: architecture direction and source audit, 2026-11-15. This document records the owner's requirement that Maelstrom be operable through backend interfaces before its GUI is treated as a complete client. It does not claim feature parity or authorize live gameplay.

## Rule

The backend owns operation definitions, target/session identity, observation, action ownership, scheduling, cancellation, limits, state, and evidence. The desktop UI presents and configures those operations; it must not invent a second implementation for a button. CLI, local API, MCP, and UI are adapters over the same operation contracts. When an operation has no backend contract, the UI must label it as preview/planning or keep it unavailable instead of implying that it works.

This extends the existing product rule that the shared registry and scheduler own execution and workspace screens route to those services. “Headless” means no Maelstrom GUI is needed; it does not bypass the interactive Windows desktop, foreground, target identity, game focus, opt-ins, or manual takeover required by a particular operation.

## Current source audit

| Surface | Existing backend path | Current limits |
| --- | --- | --- |
| Native local API | `JobDeckHost` exposes a current-user-only named pipe with versioned JSON requests, bounds, deadlines, and a single `OperationScheduler`. It serves deck catalog/state/snapshot/edit/preview/start/step/stop/reset, target windows, connect/disconnect, UI-tree observe, scan profile and routine-graph validation. | It is a job-deck and W101-observation API, not a complete generic operation service. Observation lifecycle is exposed through MCP as explicit connect/queue/disconnect, but persistent per-observation completion receipts are missing. One owner per data root. |
| CLI | `MaelstromCli` has bounded commands for window discovery, saved status/recovery, W101 observation, character and World Gate steps, Idle, navigation probes/visits, and selected quest conversation. `--deck-api` calls the local owner; `--deck-host` runs that owner headlessly. | CLI coverage is an explicit command list, not a general operation registry. Several CLI-only operations are not addressable through the native deck API or MCP. Commands require exact HWND/PID/start-time identity and options; the shell must be in a desktop context that can enumerate the target. |
| MCP | `tools/maelstrom_mcp_server.py` delegates card/catalog/telemetry/alias and validation calls to the native owner through `tools/maelstrom_deck_client.py`; it also exposes `maelstrom_list_target_windows`, explicitly confirmed connect/observe/disconnect, and `maelstrom_check_observation_progress`. Planning tools remain local pure functions. It does not start a second owner or invent missing telemetry. | Observation calls are exact-target and opt-in, with quest/travel/resource capabilities forced off. The progress check reports `active` only for the exact current observer ID; after it clears, it says `outcome_unknown`. There is still no correlated terminal receipt or result poll. W101 navigation/conversation CLI operations remain outside MCP. Capability parity is partial. |
| Card Deck UI | `JobDeckForm` calls `JobDeckHost.Handle` on the same in-process host instance; deck operations and scheduler state use the same native service as the local API. | This is a valid same-process backend client, but it does not prove that other workspace windows use the same backend contracts. |
| Observe / teaching workspace UI | `GameWorkspaceForm` owns a `WindowObserver`, still/video capture, recording, selected-input/audio recording, video sampling, and opening review/teacher forms. | These operations are substantially form-lifecycle-driven. There is no matching headless session service/API for the complete record → segment → annotate → propose cards workflow. A user cannot operate all teaching functions from CLI/MCP/API today. |
| Optional HTTP proxy | `MaelstromBot.Server` proxies selected deck endpoints to the local native pipe with authentication and idempotency on start/step. | It is optional, narrower than the pipe contract, and does not turn the native pipe into an internet listener. |

Sources inspected: `MaelstromCli.cs`, `JobDeckHost.cs`, `JobDeckWire.cs`, `JobDeckForm.cs`, `GameWorkspaceForm.cs`, `tools/maelstrom_mcp_server.py`, `tools/maelstrom_deck_client.py`, `docs/JOB_DECK.md`, and `docs/HEADLESS_CLI.md`. The latter contains a pre-existing active file claim; this audit did not edit it.

## Observation-session status boundary (2026-11-15)

The explicit MCP observation flow is recorded in `BACKEND-MCP-SESSION-154.md` and was verified with mocked offline tests. `connect` binds one exact target, enables only the optional client/UI-tree reader, and disables quest, travel and resource capabilities. `observe` queues one `W101UiTreeOperation` and returns an operation ID with `completed: false`.

While work is active, native `state` exposes `observerOperation`; after completion the observer field clears. `CollectObservationAsync` publishes facts only for a completed operation whose reading passes identity, freshness, loading, and cleanup checks. Invalid, cancelled, or failed reads may leave no facts. The terminal state is not retained by operation ID, and a later snapshot's current facts are not correlated to the request. Therefore the current flow supports explicit dispatch and a coarse view of the owner, not a complete request → progress → correlated-result lifecycle.

The next backend slice should add a bounded owner-side receipt lookup keyed by operation ID, including terminal status, target binding, cleanup confirmation, and result availability, then expose that same contract across local API, MCP, and CLI. Do not infer completion from disappearance of `observerOperation` or from non-empty facts.

## Operation contract every surface must share

Each registered operation needs:

1. Stable `operation_id` and schema version, with typed arguments and output.
2. Explicit target/session binding, permissions/opt-ins, preconditions, freshness and capability requirements.
3. Bounded duration, input/action count, resource use and retry policy; idempotency for admitted mutations.
4. One owner for scheduling and input, with cancellation, pause/takeover, release/cleanup, and drain semantics.
5. Progress/state events and a durable receipt distinguishing requested, admitted, dispatched, observed, completed, stopped, and verified outcomes.
6. Evidence links and qualification scope. A backend accepting a request is not proof that the game responded.

The capability catalog is the discovery contract for CLI, API, MCP, cards and UI. Each surface may apply transport-specific security rules, but it must report the same operation ID, arguments, availability and result semantics. Unsupported operations fail explicitly; no frontend-only fallback action or simulated success.

## Delivery order

1. Inventory every visible control and service entry point against the operation registry. Mark each as backend-complete, partial, planning-only, or unavailable, with its owner and evidence.
2. Finish the generic backend operation registry and session owner around existing services before adding more front-end controls. Keep deck/card operations as one client of it.
3. Close transport parity gaps for safe discovery and observation first: current target identity, catalog, state, connect/observe/disconnect, operation-ID progress, and correlated terminal receipts. Mutating calls remain opt-in and target-bound.
4. Move long-session capture, synchronized telemetry, annotation, replay and card-proposal workflows behind backend services. Keep capture bounded and serialized with the game observation owner.
5. Route each UI control through the same contract used by CLI/API/MCP. Test the same requests and receipts through each adapter; hide or mark incomplete any control without a backend implementation.
6. Only then qualify a complete headless teach → build pack → replay → bounded live run lifecycle and ship the UI as its user-friendly client.

Do not try to repair the recent desktop focus incident by sending inputs through a second desktop controller. The backend remains subject to Windows focus and takeover constraints. A missing or mismatched desktop/session identity must pause or reject the operation.

---

## 5. Rust Core / C# Host IPC Integration (`MaelstromAutonomyBridge`)

> **Status (audit 2026-11-19, Claude):** partly aspirational. In code today: a read-only snapshot client over the named pipe
> and an offline decision step (`MaelstromAutonomyBridge::process_state`), used only by the replay harness and tests. Not
> true yet: the host publishes no combat, zone or position fact (the bridge now reads them only as qualified aliases, so
> against today's host they are unknown and every cycle ends `Withheld`), nothing sends a result or an `actuate` request
> from Rust to the host, and the live `step_cycle` path has never run. Details:
> [W101-IPC-CORE-02, Audit note and follow-up](coordination/results/W101-IPC-CORE-02.md).

The integration between the high-performance native Rust core (`maelstrom-core`, `maelstrom-fallback`) and the C# host (`JobDeckHost.cs` / `OperationScheduler.cs`) proceeds through the existing, verified named-pipe protocol:

1. **Named Pipe Transport**:
   - Pipe address: `\\.\pipe\maelstrom-deck-<HASH_HEX_24>`, where `<HASH_HEX_24>` is the SHA-256 hash of the normalized, uppercase ASCII data root path.
   - Wire framing: 4-byte Little-Endian length prefix followed by UTF-8 JSON payload (budget: 2 to 262,144 bytes).
2. **Perception-Decision Synchronization Cycle**:
   - `MaelstromNativeBridge` issues a bounded `snapshot` command to the host named pipe.
   - Host returns qualified, fresh facts (`health`, `mana`, `potions`, `nativeIdle`); the host emits no combat, zone or position fact. The Rust side takes those only from aliases named `combat.active`, `zone.name` and `position.x|y|z`, qualified like the vitals (fresh, typed, identity-matched); until the host publishes them they stay unknown.
   - `NativeHostState::update_blackboard(&self, bb: &mut W101Blackboard)` projects qualified telemetry into the Rust Behavior Tree blackboard. A fact that is not a fresh qualified reading becomes a value that proposes nothing and is reported as `None` in the `AutonomyCycleResult`; a cycle whose combat state or pose is unknown ends `Withheld` without ticking the tree, and a disconnected host ends `Failure`.
   - Reactive Behavior Tree (`build_w101_canonical_tree()`) ticks against the blackboard, evaluating priority conditions (Combat > Potion Recovery > Kinematic Stuck > Quest Nav > Idle).
   - If navigation is active, `PurePursuitController` tracks the path and computes `SteeringCommand` pulses (`W`, `A`, `D`, `S`).
   - If UI interaction is active, `WindMouseGenerator` or `BezierTrajectoryGenerator` generates an immutable `KinematicPlan`.
3. **Action Dispatch Invariant (`Facts ≠ Authority ≠ Actions`)**:
   - The Rust autonomy engine never directly injects OS keystrokes or mouse clicks. It returns an `AutonomyCycleResult` containing the proposed action and kinematic parameters.
   - Intended design: physical actuation runs under the host's single scheduler and input lease holder only when target identity,
     window focus, the pre-resume countdown and the cancellation latches are verified clean. The `actuate` command does not meet
     that today (see item 4).
4. **End-to-End Actuation Event Loop (`"actuate"`)**:
   - `JobDeckHost` exposes an explicit `actuate` command over the named pipe that drives `DesktopInputActuator`. Simulated playback
     (`simulation: true`) works and is tested. The live branch (`simulation: false`) is a stub that sends no input. Nothing in the
     Rust crates calls it, and the Rust summary structs use snake_case, which the host rejects. See
     [JobDeckHost Actuation Loop](JOB_DECK_ACTUATION_LOOP.md).
