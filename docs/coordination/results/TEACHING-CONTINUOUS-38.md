# TEACHING-CONTINUOUS-38 — continuous observation during recording

2026-11-12 · CORE-DELIVERY · source plus bounded live observation; not installed.

## What changed

--teaching-record <request.json> <new-report.json> now runs the existing chunked
recorder through a hidden, nonactivating message-loop host. It validates exact
PID/start/window/process identity. Opt-in W101 telemetry uses the existing
OperationScheduler and shared input/control owner: one 15-second observation
batch at a time, cleanup/disconnect checked before the next. No network tooling,
input dispatch, focus change, audio capture, or model is needed. Generic recording
remains usable without that adapter. UI-tree/quest context are collected; health,
energy and general combat stats are not invented. Continuous here means repeated
bounded observation during capture, not a seamless permanent hook.

Stop-file polling is 250 ms; recording completion cancels and awaits telemetry.
Reader failures stop capture instead of silently dropping state coverage. Raw
observer traces and scheduler receipts remain in the operations store. A completed
run writes telemetry-batches.json, then merges normalized evidence into the
content-addressed state track. Reconnects are distinct context boundaries. A session
review track remains bounded to 32 MB/20,000 readings; an oversized merge is reported
incomplete and raw batches remain available. Hour-scale sharded review is pending.

The live check exposed frequent negative loading probes masking fresh UI trees.
The importer now skips negative probes without changing the tree acquisition time.
Positive loading or read failure still invalidate prior state; freshness still
expires after 2 seconds. A regression test covers the repair.

## CLI authoring and training preparation

- --teaching-merge-w101 <session> <telemetry-batches.json>: reprocess saved batches.
- --teaching-annotate <session> <activity.json>: add/update a validated editable
  activity by ID; stores commentary separately from measured observations.
- --teaching-review <session> <new-report>: propose scenes from fresh evidence.
- --teaching-export-card <session> <activity-id> <new-routine.json>: export the
  existing bounded routine graph without enabling live dispatch.
- --replay-routine <routine> <evidence-session> <new-report>: existing evidence-only
  replay. Missing action receipts remain incomplete.

Recording request uses PascalCase JSON fields:
Target {ProcessId,StartedUtcTicks,WindowHandle,ProcessName}, absolute DataRoot,
Minutes (1–60), absolute StopFile, RecordInputs (default false),
EnableW101Telemetry (default false), Workspace (required for that adapter).
No live actions are implied by enabling observations. Inputs, video, raw UI trees,
normalized state, human/reviewer commentary, proposed cards and actual action
receipts remain distinct artifacts for later supervised/imitation training. This
is dataset preparation, not a trained bot or proof of causal rules. Demonstrator
identity needs richer explicit metadata before mixing human/model teacher datasets.

## Bounded live evidence

CLI-only W101 check requested stop after 20 seconds. No keyboard/mouse input,
foreground change or volume change. Recording exited 0, session stopped normally,
video chunk complete; two observer batches; state track initially contained 81
readings including native Idle. Local recording result:
.local/teaching-continuous-38/record-result.json

Video: 474 captured/submitted source frames, zero queue rejects, 951 rate-converted
encoded frames over 15.85 seconds. Extra encoded frames are not fresh observations;
this is not proof of 60-fps capture or frame-exact training alignment. Source
resolution 1282×759, encoded 1282×760. Preserve timing/geometry sidecars.

Session:
.local/w101-cycle-06/live-data/teaching-sessions/9c8749f3b1aa46cfb5c1ce9969be7084/teaching-session.json

No action demonstration occurred, so downstream replay must not claim successful
input or gameplay. The check establishes recording plus telemetry capture and
cleanup, not automatic gameplay, long-session stability or trained imitation.

## Remaining work

Sharded long-session telemetry, richer demonstrator identity, input-to-action
alignment, observation-derived activity segmentation, training export/splits,
independent validation and live routine qualification remain. System-tray behavior
is a side task. GUI opt-in telemetry control and API/MCP teaching endpoints are not
yet exposed; the new recording integration is CLI-first.

## Final results

- Full native suite: 2,544 passed, zero failed/skipped; warnings-as-errors compile.
- Scoped whitespace verification and analyzer verification passed.
- Both live observer results had CleanupConfirmed=true and HookRestored=true;
  first status observed (15 trees), second cancelled (2 trees). Both final state
  sets showed nativeIdle=true; no input was sent and no Idle cleanup was needed.
- Reprocessed saved traces with repaired negative-loading handling: 17 actual
  tree samples. The original source traces and prior content-addressed track remain.
- Actual executable CLI merge, annotate, review and export each exited 0. Reviewer
  commentary was explicitly attributed to the assistant, not presented as user
  teaching. Exported card represents only an unqualified Idle observation.
- CLI replay exited 2 as expected: observations have no input ownership/dispatch
  receipts, so they cannot establish action success. No synthetic receipt was
  inserted to turn this into a passing gameplay claim.
- Evidence: .local/teaching-continuous-38/{activity,proposals,routine,session,replay}.json
  and events.jsonl. The replay session is a derived observation-only projection,
  preserving the source-track reference; it is not a raw recorder-generated receipt
  stream. Initial live capture preceded the importer-only negative-loading repair;
  all subsequent re-review was offline.

Next milestone: capture an actual bounded teacher action with input observations,
show its before/after state and cancellation, align action IDs through the game
pack, and compare on held-out evidence. Do not train on invented successful actions.
