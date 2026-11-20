# W101-UI-OBSERVER-SHUTDOWN-01 — bounded shutdown-budget repair

Status: **COMPLETE — offline fix and re-qualification. NO LIVE RETRY.**
Date: 2026-11-16 · owner: root · claim: `W101-UI-OBSERVER-SHUTDOWN-01` · workstream: `CORE-CLEANUP`

## 1. Trigger and evidence

A read-only `ui-tree-watch` (5s, exact target triple, both opt-ins) against the live W101 client at 21:45 on 2026-11-16 produced **no observations** and ended in `CleanupFailed`:

- Receipt `3657311ef2fc4551ad2c5ec6d0e0c682`: state `CleanupFailed`, message *"Input recovery required; gameplay remains blocked. Client observer exceeded its shutdown budget."*
- Operation directory timeline: `ui-request.json` written 21:45:28; `ui-observations.jsonl` created **empty** at 21:45:46; `stop.request` written 21:45:51; terminal receipt 21:45:54.
- Recovery marker: *"UI observer ended without verified hook restoration and disconnect."* `game-input.lock` went `0` → `1` (gameplay blocked).
- The guard worked as designed. The verified `recover-input` flow restored control on the second attempt (first attempt exit 2, *"Desktop activity detected; recovery was not started"*; second exit 0, evidence `b194eabb656d4df78ad8e67cd69dca4f`). No guard was deleted, rewritten, or bypassed.

## 2. Root cause — three budgets collided

| Layer | Budget before fix | Source |
| --- | --- | --- |
| Native worker maximum | `duration + 18` = **23s** for a 5s watch | `W101UiTreeOperation.cs:57,63` (startup allowance) |
| Native cooperative stop → forced kill | **3s** | `WizwalkerWorker.cs:78-82` |
| Python hook restoration | `asyncio.wait_for(client.close(), 3)` plus a 0.5s settle and read-back verification | `tools/w101_ui_tree.py:411-423,2173` |

Timeline arithmetic: worker launched ~21:45:28, `stop.request` at 21:45:51 = **23s**, exactly the worker maximum. Startup/import/hook setup consumed the entire allowance before the observation loop began, so the observation never produced data. The native kill at 3s raced the Python side's own 3s `client.close()` verification; the worker was killed before it could write `result.json` with `CleanupConfirmed`, forcing `RequireRecovery`.

This is a **budget/timing defect**, not a hook-restoration bug: no evidence that restoration itself failed.

## 3. Fix (bounded, safety-preserving)

- `WizwalkerWorker.ExecuteJsonAsync` gains an optional `cooperativeStopSeconds` (default **3**, validated 1–10 via `ValidateLimits`). All pre-existing callers keep the 3s default — no behavior change outside UI-tree observations. The forced kill still calls `RequireRecovery`; nothing can bypass the recovery guard.
- `W101UiTreeOperation` now uses:
  - startup allowance `duration + 30` (was +18),
  - cooperative stop grace `8s` for both observation paths (plain and moving-pose),
  - operation deadline `duration + 45` (was +30), so the scheduler cannot cancel before the forced-stop grace expires.
- Invariant asserted by tests: for every duration 1–60, `duration + 30 + 8 < duration + 45`.

Deliberately **not** changed: the Python worker, hook profile, pin hashes, memory cap (384 MiB), ownership/cancellation checks, `RequireRecovery` semantics, or any live/action opt-in.

## 4. Offline verification

| Check | Result |
| --- | --- |
| Focused tests (`WizwalkerWorkerTests`, `W101UiTreeOperationTests`) | **63 passed / 0 failed**, `-warnaserror` |
| Full native suite | see coordinator verification note in the claim release |
| Scoped whitespace + analyzers | exit 0 / exit 0 (coordinator) |
| Text encoding gate | 0 violations (coordinator) |
| Project board | 30 workstreams valid (coordinator) |

New tests cover: admission of the new budget, bounded grace (1–10), rejection of out-of-range grace with the correct parameter name, deadline-outlasts-worst-case ordering, and preservation of the existing limit tests.

## 5. Limits and next gate

**No live retry was performed.** The fix is re-qualified offline only, per the task scope. A future live `ui-tree-watch` needs fresh owner authorization; it must be bounded, use the exact current target triple, and be prepared to honor the recovery guard again. If a second live attempt still ends in `CleanupFailed`, the next investigation should target the Python `client.close()` path itself (e.g. slow read-back/settle under load), not further budget increases.

The original observation goal remains open: zone and native-Idle state are still unverified for this client session.
