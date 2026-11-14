# Direct Home cooldown observation and waypoint - 2026-11-12

## Source

Travel card setting captureHomeCooldown (default false) requires returnHome. Home dispatch starts a monotonic clock; first read after loading records availability. Existing Idle finalizer runs before additional read-only samples. Same owner, 45-second-from-dispatch/20-read bound, no additional teleport. Unknown does not imply ready; no inference from enabled button alone. Evidence is home-cooldown.json, including source traces. This is sample timing, not exact expiry or Commons qualification.

## Live evidence

First admission 16c9c0723aaf4314be30db06ee0b6a63 rejected before input on foreground. Owner explicitly restored foreground.

Travel 40302fff61604ef1ace6d7d29cf66710 completed configured Zafaria -> Home -> native Idle. Direct Home samples unavailable at 9.69,21.00,23.89,26.79,29.66 seconds; mixed/unknown at32.56; available at35.39. Both cooldownBlocked and cooldownCleared true. Acquisition/cleanup delays mean this corroborates the owner-reported 30-second rule, not exact expiry precision. No further Home input was sent.

Waypoint 0aeb690ccf2945098580d003bca7a181: start (-17.0093,-20.7278,-329.2208), yaw pi. Frozen 60-unit forward target. Pulses40ms and23ms produced final (-17.0093,36.3922,-329.3108), distance2.88 from target; two independent readings confirmed coordinate arrival. Final Idle diagnostic specifically identified target-not-foreground (activity revision remained0); entire visit Failed. Further control stopped, fresh Idle-only authorization requested. A successful motion stage is not a successful full route.

## Route goal and gates

Owner-selected composed target: Home/world gate -> Wizard City -> Commons/Pet Pavilion -> Dance -> native Idle, no snacks. Each stage needs fresh destination/interaction evidence and verified cleanup. Existing house center-path/front-door landmarks are route-teaching evidence, not a repeatable route. Current relative waypoint is calibration, not arbitrary destination routing. Preserve per-wizard/house coordinates and distinct indoor/outdoor spaces. Do not enable the larger chain from these partial successes.

## Checks

27 focused travel/World Gate tests pass with warnings-as-errors. Full native suite:2481 passed, zero failed/skipped. Scoped whitespace format/verify and analyzers passed. Board validation passed for30workstreams. Rust Dance conformance tracked separately. No installation, model, desktop-control session, snacks, purchases or volume changes.

## Authorized Idle cleanup

Fresh owner authorization: e72bee58e3c04f5ab257729753308957 completed Idle-only cleanup with countdown and no movement. Native Idle independently observed; CLI exited. This repairs the final game state but does not convert the interrupted waypoint visit into an unattended full-route success.
