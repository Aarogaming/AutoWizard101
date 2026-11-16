# Olde Town live observation and floor anchor — 130

2026-11-15.24 existing offline reader/probe/settling tests passed before one
authorized observation-only CLI run. No keys, clicks, activation, desktop control,
volume changes, travel or movement. Optional reviewed client hooks were used;
this is not a zero-write process observation. No new dependencies or install.

Run39644d9fc48f4dd59fd18911ce98b3b9, report `.local/oldetown-observe-130.json`.
PID21044, HWND945359296, started639303092887288354. Scheduler receipt Completed;
result CleanupConfirmed=true, HookRestored=true. NativeIdle=true at the UI read;
Loading=false. Observer disconnected. No subsequent Idle action was necessary.

## Actual readings

- Candidate zone: `WizardCity/WC_Streets/WC_OldeTown`.
- XYZ:(1743.090087890625,241.71363830566406,2.200042724609375).
- Client yaw:1.6863594055175781.
-24 streamed samples,120 pose reads,1152 pose bytes (excludes context/UI reads).
- Stream remains contextQualified=false, with actor-parent-unqualified zone
  owner binding and unqualified wizard context. LiveQualified/MovementAuthorized
  remain false. This qualifies successful bounded acquisition/cleanup only.

## Local archive comparison

Read-only installed Olde Town collision member decoded through existing bounded
decoder. At the reported XY, the only mesh vertical intersection found was
proxy279, `Imported collision mesh_HD_WC_Street_Blue_A02B_D.tga`:
Z=2.199951171875; absolute difference0.000091552734375. Category2,
collision mask13379. This is a local supporting-surface candidate corroboration,
not proof of arbitrary walkability, no dynamic obstructions, or body clearance.

All276 nonmesh proxies:203 BOX,73 CYLINDER, category1/mask13379. Mesh names also
include bridge, water, fishing, shadow and wall surfaces. Neither names nor the
shared mask alone prove passability. Treat non-support geometry conservatively;
do not exempt decorations or triggers using naming heuristics.

## Remaining reader/control boundary

The current production CLI observer is connected and producing live pose data.
The autonomous follower is not connected to an independently qualified stream:
target/stream lifetime, authoritative zone/wizard context and loading epoch must
be owned by the producer and validated at the native boundary. Do not turn the
candidate flags true merely because one position matches a street triangle.
Olde Town first, Grizzleheim secondary; no Grizzleheim test occurred.
