# W101 Idle fail-safe and quiet recovery

Status: source implemented, offline verified; live qualification deferred by user.

The reusable `W101IdleFailsafe.RunAsync` finalizes an already drained routine under
its existing scheduler lease. It is integrated into `navigation-visit` by default.
It never submits a second controller, reacquires focus or replays gameplay.
Other existing quest/Dance/card entry points still require deliberate integration;
this is not blanket live qualification of every routine.

- Two additional Idle attempts by default, with 250/500 ms backoff and a shared
  40-second finalization deadline. Each attempt begins with fresh observations;
  already-Idle can complete without input.
- Optional quiet cleanup resumption: 2–10 quiet seconds, at most 12 seconds of
  waiting per recovery, within the same finalization budget. Polling is 200 ms.
- Same target, foreground, geometry, input owner, functioning activity monitor,
  released keys and clean recovery state are required before resuming. Escape and
  cancellation are not cleared. No model, driver or new dependency.
- The composed visit has an 85-second outer budget. Failed gameplay remains
  failed even when its Idle finalization succeeds. Reports preserve attempts,
  quiet-resume decisions and skipped reasons.

CLI options: `--idle-retries 0..2`, `--resume-after-quiet true|false`,
`--quiet-seconds 2..10`, available only on `navigation-visit`. Quiet resume defaults
false. This resumes the fail-safe only, not an interrupted gameplay sequence.

Verification: 2,341 full native tests passed; focused final CLI tests 43 passed.
Scoped whitespace/analyzer checks and warnings-as-errors build passed. Synthetic
coverage includes exhausted retries, current ownership, cancellation, uncertain
cleanup, denied quiet resume, activity resetting the quiet timer, Escape, held
keys, focus/geometry/target changes and no gameplay replay. Previous simple live
left/right visits passed, but do not qualify this new recovery behavior.

The user declined the proposed mouse-bump live test; no game input or assistant
desktop control occurred in this finishing task. No package/install performed.
Next live gate: notification, Ready, focus-switch delay, one deliberate mouse
bump after wake, then verify quiet recovery and native Idle against app evidence.
