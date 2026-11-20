# W101-ZONE-CANDIDATE-REQUAL-01 — bounded live pose-context observation

Status: **PASS — one bounded read-only observation. Candidate zone only; NOT a qualified zone and NOT movement authorization.**
Date: 2026-11-16 · owner: root · claim: `W101-ZONE-CANDIDATE-REQUAL-01` · workstream: `W101-NAVIGATION`

## 1. Scope

Second stage of the owner-authorized re-qualification (after `W101-IDLE-REQUAL-01`): close the zone-identity gap by adding `--include-pose-context` to one bounded `ui-tree-watch`. Same exact target (handle `642779728`, PID `44468`, ticks `639304802391820609`, image `d410a246…`), W101 foreground, recovery guard clear, **no keyboard or mouse input, no focus change**, fresh output path.

## 2. Result

Run `.local/agent-runs/live-recheck-pose-20261116-222618/ui-tree-pose.json`:

- Exit **0**, receipt `a255da69…` state **Completed**, timeoutSeconds **50** (rebuilt budget), message *"Observed 5 UI trees and 20 loading samples; root hook restored and disconnected. Candidate states only; no game inputs sent."*
- `Status: observed`, `Code: bounded-ui-watch-finished`, `Loading: false`
- `States`/`StableStates`: `nativeIdle: true` (stable), `menu/dialogue/interactionPrompt: false`
- `CleanupConfirmed: true`, `HookRestored: true`, 5 trees / 20 loading samples / **0 failures**, `DurationMs: 7782`
- Pins verified for `w101_ui_tree.py`, `w101_pose_reader.py`, `w101_combat_reader.py`, `w101_zone_reader.py`, `w101_moving_pose.py`, the adapter and client image.
- Guard after: marker absent, `game-input.lock = 0`.
- Durable artifacts: `%LOCALAPPDATA%\Maelstrom\operations\a255da69ffc24563a469a05280e96bea\{ui-request.json,ui-result.json,ui-observations.jsonl}`; 26 records (1 ready, 20 loading, 5 tree, 1 pose-window-ready).

## 3. Candidate readings (verbatim values; all explicitly `Status: candidate`, `LiveQualified: false`, `MovementAuthorized: false`)

| Reading | Value |
| --- | --- |
| Zone candidate | `WizardCity/Interiors/WC_Housing_Dorm_Interior` (`layoutId: archived-wizwalker-client-zone`, 8 reads / 170 bytes) |
| `ownerBinding` | `registry-owner-endpoints-matched` |
| `wizardOwnership` | `status: observed`, `ownerMatched: true`, `statsMatched: true`, `source: client-object-registry-v1`, 58 reads / 596 bytes |
| Position | X `29.560529708862305`, Y `4.139770984649658`, Z `0` |
| Yaw | `4.7123894691467285` rad (= 3π/2) |
| `bodyDimensionsCandidate` | height field `164.0888671875`, scale `1.0`, `collisionQualified: false` |
| Moving-window candidate | `atomicityVerified: false`, stationary samples at the same pose |

These values corroborate the earlier dorm observation recorded in `docs/coordination/CODEX_WORK.md` (XYZ 29.56, 4.14, 0 matches the dorm). They do **not** establish a qualified zone, a calibrated collision profile, or any movement authorization.

## 4. Limits

- Single bounded pass; no independent zone confirmation from a second source, no traversal, no portal transition.
- Zone identity comes from an archived layout reader and is explicitly `liveQualified: false`.
- Ownership matching at the observed endpoints is `observed`, not a continuous epoch guarantee.
- No game input was dispatched, so no response, arrival, or route behavior was tested.
- This does not qualify the Commons↔Pavilion route, Dance entry, or any gameplay routine.

## 5. Next gates (not performed)

1. Repeat sampling to establish stability of the candidate zone across passes and a loading epoch.
2. Independent corroboration of zone identity (e.g. a visual/game-state source) before treating it as qualified context.
3. Only after that, a separately authorized and bounded movement experiment with countdown, takeover and Idle finalization.

**Live work stops here** under the current authorization; any further live action requires fresh owner scope.
