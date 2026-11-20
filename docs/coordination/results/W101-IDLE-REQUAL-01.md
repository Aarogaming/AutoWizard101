# W101-IDLE-REQUAL-01 — bounded live re-qualification of the UI observer

Status: **PASS — one bounded read-only observation. No gameplay, no qualification of a route or visit.**
Date: 2026-11-16 · owner: root · claim: `W101-IDLE-REQUAL-01` · workstream: `CORE-CLEANUP`
Directive: owner-authorized staged re-qualification after fix `7058fb2`; no claim for live work existed before this record.

## 1. Purpose and scope

Close the open gate from `W101-LIVE-DISCOVERY-148.md` — "native Idle is not currently verified" — and verify in the live environment that the shutdown-budget fix `7058fb2` no longer produces `CleanupFailed`. Two stages, both **read-only**, exact target identity, W101 in foreground, **no keyboard or mouse input at any point**:

1. `wizwalker-check` — hook-free, one-shot client read.
2. `ui-tree-watch` — 5s bounded observation with the rebuilt budgets (8s cooperative stop grace, `duration + 30` startup allowance, `duration + 45` operation deadline).

## 2. Target and preconditions

- Source build: `ProjectMaelstrom/ProjectMaelstrom/bin/Release/net9.0-windows10.0.22621.0/ProjectMaelstrom.exe` (the build that includes `7058fb2`).
- Exact target from a fresh `--cli windows` row: handle `642779728`, PID `44468`, started-utc-ticks `639304802391820609`, `Bin\WizardGraphicalClient.exe`, image SHA-256 `d410a246…` (one of the two reviewed/pinned client images).
- Foreground confirmed as that HWND before each stage. Recovery guard clear beforehand (`game-input.lock = 0`, no marker).
- All three identity values were copied from the same row; no first-window selection or retargeting.

## 3. Stage 1 — `wizwalker-check` (hook-free)

Run `.local/agent-runs/live-recheck-20261116-222341/wizwalker-check.json`:

- Exit **0**, receipt `5a5e00ba…` state **Completed**, message *"Client reports not loading. This does not prove the wizard is ready to play."*
- Adapter result: `Status = observed`, `Code = loading-state-observed`, `Loading = false`, `CleanupConfirmed = true`, `DurationMs = 313`.
- No input, no hook, guard clear afterward.

## 4. Stage 2 — bounded `ui-tree-watch` (reviewed root-window hook)

Run `.local/agent-runs/live-recheck-ui-20261116-222432/ui-tree-watch.json`:

- Exit **0**, receipt `9cdc3adf…` state **Completed**, timeoutSeconds **50** (the rebuilt `5 + 45` budget).
- Message: *"Observed 5 UI trees and 20 loading samples; root hook restored and disconnected. Candidate states only; no game inputs sent."*
- Artifacts (durable, local): `ui-request.json`, `ui-result.json`, `ui-observations.jsonl` under `%LOCALAPPDATA%\Maelstrom\operations\9cdc3adff3824b31a4ec3d28d3b9a4a8\`.

### `ui-result.json` (verbatim values)

| Field | Value |
| --- | --- |
| `Status` / `Code` | `observed` / `bounded-ui-watch-finished` |
| `Loading` | `false` |
| `States` | `interactionPrompt: false`, `dialogue: false`, `menu: false`, **`nativeIdle: true`** |
| `StableStates` | identical to `States` — native Idle **stable** across the series |
| `CleanupConfirmed` / `HookRestored` | **`true` / `true`** |
| `LoadingSamples` / `TreeSamples` / `TreeFailures` | 20 / 5 / **0** |
| `DurationMs` | 5906 |
| `PoseCandidate` / `QuestContext` | `null` / `null` — no zone identity collected in this pass |

`ui-observations.jsonl`: 26 records (1 `ready`, 20 `loading`, 5 `tree`); each tree record carries the same candidate states. Guard after: marker absent, `game-input.lock = 0`.

## 5. What this proves and what it does not

**Proves (one bounded pass):**
- Native Idle is currently active for this client session and was stable across five serial UI-tree readings — the gate left open by incident 148 is closed.
- The `7058fb2` shutdown fix works in the environment where it previously failed: 5/5 trees, 0 failures, verified restoration and disconnect, no recovery hold.
- Both stages sent no game input; no focus change, no cleanup bypass, no client restart.

**Does not prove:**
- **Zone identity** — no pose context was requested (`PoseCandidate: null`); the wizard's location is unknown.
- Repeatability across sessions, other client builds/layouts, or heavier system load.
- Anything about the Dance visit, entry, energy, navigation, or combat. States are explicitly **candidate**; agreement from one observer is not independent validation.
- That a future longer or context-rich observation will stay within budget.

## 6. Next gates (not performed)

1. If zone identity is needed, a separate bounded observation with `--include-pose-context` and explicit owner scope; a candidate zone is still not a qualified zone.
2. A second independent observation pass before treating Idle verification as repeatable.
3. No live Dance visit, navigation, or input action is authorized by this report.

## 7. Evidence retention

All reports use create-new semantics under `.local/agent-runs/...`; durable operation artifacts are under `%LOCALAPPDATA%\Maelstrom\operations\<receipt-id>\`. No credentials, clipboard contents, or private recordings are copied into this report.
