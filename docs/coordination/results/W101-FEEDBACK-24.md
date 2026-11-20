# Bounded dorm feedback qualification — 2026-11-12

Implemented optional --feedback-waypoint true on navigation-visit. The initial destination is frozen from observed position/heading,20–100units forward. Pure WaypointFeedback consumes fresh poses, estimates speed from actual movement, limits each pulse20–100ms, permits at most3moves and6readings, and confirms arrival with2new readings within10units. Stops for nonfinite/stale data, level changes, implausible displacement, stalls, movement away or heading drift>0.15radians. No uncalibrated turn is dispatched.

Live wrapper reuses the existing scheduler/control scope/countdown, scene checks, reviewed reader and input transport. Movement stage budget60seconds, whole visit125seconds; Idle cleanup obeys takeover/cancellation. No independent input owner, new dependency, model or network operation. Read/report directories are distinct per stage/pulse; step evidence persists before each action.

## Trials

- feedback-24a / d632e94bf82d486786124fc49282c4c4: implementation failure after2bounded pulses: indexes30,31,32 hit the reader's31limit. Automatic native Idle succeeded. Not counted as arrival. Fixed stage indexes0–5 and durable incremental evidence.
- feedback-24b /07243d5098fc4867bed96ea911d63436:60unit target,40ms cap. Pulses40,32ms selected from observed progress; residual3.600024units confirmed twice. Completed and native Idle independently verified.
- feedback-24c /7cddb1628062484988612260a1757168:40unit target,30ms cap. Pulses30,20ms; residual2.800014units confirmed twice. Completed and native Idle independently verified.
- feedback-24d /c64e9b13548d4848b124cc3f72adc7d0:60unit target,30ms cap. Two movements observed; final correction interrupted by target/takeover readiness check. No successful arrival or cleanup claimed. User then explicitly authorized Idle-only cleanup.
- idle-cleanup-24 /0a67ba8a9c79400489962c1c32771912: Completed/idle-state-independently-observed. Final game state native Idle, no controller left running.

Two completed varied local trials establish initial repeatability, not a general route success rate. One integration bug and one interrupted run are preserved. User described countdown as looking great/helpful during this sequence; audible sound remains unverified remotely.

## Toward start/destination routes

Saved .local/w101-cycle-06/dorm-route-candidate-24.json with measured starts, frozen destinations, observed endpoints, residuals and evidence IDs. Explicit executable:false/routeQualified:false. Dorm identity is user-reported, not a validated zone-reading contract. This is route evidence, not a runnable coordinate script.

Next gates: calibrated turn direction/rate with fresh heading feedback; zone-qualified landmark/route graph; stall recovery with bounded alternatives; independent replay of the same route from varied starts. Resolve start/destination to this graph, reject unknown/unqualified edges, and verify loading/zone transitions at gateways. Current collinear experiments cannot establish obstacle avoidance or turn reliability.

Offline coverage includes speed variation0.3/0.6/0.9units per ms, repeated arrival evidence, stalls, stale poses, level changes, moving away, heading wrap and explicit CLI admission. No live turning or zone crossing was attempted. Implementation is in source; not packaged/installed in this task.

Final verification:2405native tests passed; scoped formatting/analyzers passed. Project board checked.
