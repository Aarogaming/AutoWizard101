# Observation status contract audit — 155

Date: 2026-11-15  
Scope: source/documentation audit only; no host or client was run.

## Finding

The new MCP observation workflow dispatches through the existing native owner, but it cannot yet return a durable correlated terminal result.

Source trace in `JobDeckHost.cs`:

1. `observe` submits a `W101UiTreeOperation`, stores its `OperationTicket`, and returns `{ operationId, status: "Queued", completed: false }`.
2. `state` exposes the active observer's request ID while `_observer` is non-null. `snapshot` exposes current facts and aliases.
3. `CollectObservationAsync` publishes facts only when the scheduler receipt is `Completed`, the result file parses, and `ObservationReady` accepts the identity, freshness, loading, hook-restoration and cleanup evidence.
4. The `finally` block clears `_observer`. It does not persist a terminal observer receipt keyed by that request ID. A later global state/snapshot therefore cannot prove which queued observation produced its facts, or distinguish all failure/cancellation/invalid-result outcomes.

## Consequence and next contract

Do not interpret `observerOperation == null` as success, and do not associate non-empty `snapshot.facts` with a prior request without a matching operation receipt. MCP supports explicit connection, dispatch and coarse current-state inspection; it does not yet support progress and result retrieval for an individual observation.

The next implementation should put a bounded terminal receipt in the native owner, keyed by operation ID, with at least: request ID, target binding, lifecycle status, completion timestamp, cleanup/hook-restoration outcome, and whether a validated result is available. A result payload should remain the existing structured UI-tree result/facts contract, not a second interpretation. Expire or cap retained receipts. Then expose the same read contract through the local pipe, CLI and MCP. Keep admission separate from completion and completion separate from game-action qualification.

## Verification and limits

This report is a static source trace based on the current checkout. No code or tests were changed/run for this slice, no live host or client was queried, and no gameplay, desktop, package or installation qualification is claimed. The preceding MCP implementation's separate test report is `BACKEND-MCP-SESSION-154.md`.
