# Destination navigation: candidate graph to owned execution

Current checkpoint: 2026-11-14. Source implementation and offline evidence only.
No live Commons-to-Dance route is qualified or enabled by this change.

## Reused boundaries

`NavigationMap` parses the inspected nav format and searches directed edges.
`W101NavigationArchive` reads only zone.nav from bounded read-only WAD streams.
`NavigationAtlas` resolves names in a specific zone/instance/map/profile context;
it rejects unknown connectors, missing prompt qualification, conflicting edge
evidence, unknown clearance and mismatched member hashes. Qualification records
are trusted host evidence, not user-authored permission flags.

`PathFollower` is a pure canonical-coordinate state machine. It emits expiring
forward/signed-turn intentions, consumes separate dispatch/release receipts,
stops at corners and invalidates on loading/context changes. Its defaults are
prototype limits, not measured W101 safety bounds. No reverse recovery exists.

`W101PathSession.RunOwnedAsync` connects injected observations and actuation to
the existing OperationScheduler/GameInputLease. It does not create another input
owner or install hooks. The Python moving-reader wire format is consumed by
`DecodeCandidate`, which explicitly converts W101 yaw into canonical heading.
Source monotonic timestamps are not compared with host counters: the request
start is a conservative observation-time lower bound, with a bounded round trip.
This loses freshness rather than inventing clock synchronization.

The scheduler test uses that Python wire shape, the real follower and the real
scheduler with synthetic input/observation delegates. It is not game physics or
live operation. A production moving-reader binding is deliberately absent until
current-zone identity, timing and reader behavior qualify. A candidate window
alone cannot set MovingReaderQualified.

Named arrival requires post-release prompt verification. The parent W101 visit
still owns native Idle finalization, focus-pause recovery, countdown and overlay.
This nested seam does not claim Idle or start a Dance game.

## Actual installed-archive binding results

Read-only production parser analysis wrote reports to
`.local/nav-binding-108/commons-candidate-binding.json` and
`.local/nav-binding-108/pavilion-candidate-binding.json`.

| Graph | Nearest start / goal | XY connector gaps | Directed path length | Largest edge abs(dZ) | Stored reverse-edge fraction |
| --- | --- | --- | --- | --- | --- |
| Commons | 89 / 209 | 219.776 / 269.149 | 7471.859 | 140.176 | 0.999128 |
| Pavilion | 50 / 5 | 262.999 / 197.979 | 3195.979 | 30.479 | 1.0 |

Commons chain: `89,96,105,112,121,127,263,264,165,174,183,192,199,209`.
Pavilion chain: `50,43,34,23,14,5`.
Largest planar turn: Commons 0.909280 radians; Pavilion 0.785398 radians.

These are nearest-XY structural bindings only. In particular the Commons chain
contains a 140-unit vertical edge difference that needs terrain/level inspection;
neither proximity nor connectivity proves a usable player path. Reciprocal
records describe storage, not physically bidirectional passage.

Member SHA-256 values and five candidates per endpoint are in the JSON reports.
The near-tent endpoint remains unverified as a Dance interaction anchor. The
pre-crossing approach heading was about4.376, but the actual subsequent crossing
used about5.507. Neither is a certified portal trigger center or universal entry
heading. No collision.bcd semantics were adopted.

## Remaining live gates

1. Qualify current zone/instance and a persistent bounded moving-reader session;
   retain serialized hook/scratch ownership. Plausibility does not prove atomicity.
2. Measure acquisition/dispatch/release latency and speed-dependent expiry limits.
   Software watchdogs and message acknowledgements are not physical stop guarantees.
3. Validate start/end connectors and actual corridor/terrain evidence. Successful
   centerline traversal does not prove surrounding clearance.
4. Verify the portal destination and fresh Dance prompt after release; handle
   loading as a new route context, including same-zone transitions.
5. Bind the session to the parent visit's focus/countdown/Idle lifecycle and qualify
   repeated complete runs from varied starts. Until then, do not expose this as
   an operational navigation card or silently promote candidate graph edges.

Cross-language parity should require exact discrete states and documented numeric
tolerances, not bit-identical trigonometry or incidental graph visit counts. Rust
replacement must pass the same contracts and separate live qualification.

## Elevation and live observer follow-up

Production `NavigationMap.FindPath` alternatives in `.local/nav-binding-109`:
excluding199->209 gives a22vertex route of12228.161units. Rejecting every edge
with abs(dZ)>40 gives no Commons89->209 path. The40unit threshold is an experiment,
not a qualified terrain rule. The asymmetric stored edge is95->85. Original
route rises137.786units over565.550XY on183->192, stays raised on192->199, then
descends140.176over513.608XY on199->209. This suggests inspecting raised terrain;
it does not establish player walkability. Vertex-number jumps through263/264
do not locate the largest elevation change.

The pose opt-in now includes pinned bounded zone and moving-window candidates.
Zone decoding uses ActorBody parent as a candidate ClientObject and archived
offsets304/88, with pointer/header/string fences. These remain explicit layout
hypotheses. No zone fact is automatically published. The moving window has three
samples and experimental rejection bounds, not a calibrated control feed.

Live observation recovered from an isolated-worker import failure; a regression
test now verifies pinned helper loading without the tools search path. A subsequent
probe read `WizardCity/Interiors/WC_Housing_Dorm_Interior`, position approximately
(93.16,4.14,0), and three stationary samples. Independent game-window observation
d8d4b844ab41461386b9f059e956d639 confirms dorm/native Idle. Hook restoration and
disconnect were verified. This is one-location corroboration, not moving-feed or
cross-zone qualification. No movement occurred in those probes.

Task110 live calibration checkpoint: right100ms operation9d137e96875e4661a5705ce91afed023, left100ms01f67afdbc7840bcb39813988321892c and forward100msd9f249d8c80042dd88761feb3304f0cc all completed with nativeIdle verified. Eight samples per window captured right yaw+0.241275rad, left-0.291540rad, forward deltaXY(48.907349,-36.790131), approximately61.20units. Sampling intervals roughly47-63ms; nominal key duration is NOT a measured latency/velocity profile. Each moving window used40reads/384bytes. Context/atomicity remain explicitly unqualified; this proves changing readings during bounded action, not continuous route control or clearance.75 focused native checks pass. Next: persistent qualified sampling, coordinate/heading convention calibration and typed portal arrival before Commons->Dance route promotion. No controller remains active; no installation performed.
