# W101 route geometry qualification — 2026-11-11

Status: offline study implemented; live calibration pending user readiness.

## Outcome

The earlier SIFT Commons place candidates do not yet support steering. New
spatial-consensus diagnostics show only 1.133% reference coverage after the turn
and 0.704% in the final Idle frame. Both fail the provisional 2.5% image-area
support threshold. Place recognition is preserved as a separate result.

`tools/route_landmarks.py` now reports inlier hull coverage and an optional
reference-center horizontal displacement in normalized image coordinates. It
rejects localized, reflected, nonfinite and extreme projected alignments.
This displacement is not camera yaw, world bearing, road clearance or arrival.
Thresholds are study settings, not live-qualified safety bounds. The observer
still grants no movement authority. Extreme image aspect ratios are rejected
before resizing to keep allocation bounded.

## Saved evidence

Derived reports (original evidence unchanged):
- `.local/w101-route-study/route-landmarks-13/geometry-live-01.json`: three prior
  saved client frames, no alignment candidates; no new live observation/input.
- `.local/w101-route-study/route-landmarks-13/geometry-demonstration-01.json`:
  84 demonstration frames, 27 place candidates, 22 broad alignment candidates;
  single OpenCV thread, approximately 5.36 seconds for the complete evaluation.

These are coverage counts, not accuracy. Reference frames are included and
there is no independent held-out route traversal. Input/reference hashes are
included in each derived result. No model, installer or desktop control used.

## Checks and next gate

Nine focused Python tests pass; scoped Black and compileall pass. Tests cover
known translation, localized support, reflection, nonfinite transforms, extreme
aspect ratio, ambiguity, featureless input and unchanged no-authority flags.

Before navigation: obtain varied heading observations, establish a route-relative
progress/heading provider and independent arrival evidence, then compose through
the existing scheduler and Idle finalizer. No blind replay of the demonstration
or direct legacy dispatcher. New live test notification sent; no Ready received
at the time of this report. Complete route, interaction and repeat gameplay remain
unqualified.

## Notified live check and diagnostic repair

User Ready authorized a bounded turn-only check. Operation
`4a75bbf366074fbd9314b5320d860e76` stopped in wake before dispatch: zero actions,
no turn. UI reading reported native Idle, loading false, cleanup confirmed and
hook restored. Outer readiness changed after the UI read; the existing generic
error did not retain which guard changed. No repeated live action was sent.

Idle cycle runtime now has an optional bounded diagnostic callback, wired to the
existing control scope. Failed reports preserve up to 512 characters of guard
identities/counters only. A diagnostic failure cannot mask the original outcome
or authorize input. Two synthetic regression cases cover truncation and throwing
callbacks. A fresh notification is pending for the repaired check.

The build exposed unfinished concurrent `ClientOperationsCalibrator.cs` edits.
With explicit user approval to repair/integrate, removed the duplicated malformed
telemetry block and repaired interpolation. Unknown combat is now reported as
Unknown instead of False. Added a regression for absent/unknown telemetry.
Calibration geometry checks remain static diagnostics, not qualification of a
live telemetry source, game control or combat coordinates. No new adapter enabled.
Focused native tests: 19 passed. Complete suite and final scoped checks pending.

## Completed retry and next milestone

Notified retry `a6f5bb400e61434c95b96c83787b17df` completed wake, D300 and native
Idle. User independently confirmed all three. Final saved frame also shows
YOU ARE IDLE. No walking, quest advancement or assistant desktop control.
`geometry-live-02.json` preserves eight derived observations; post-turn and three
Idle-stage frames pass the provisional broad-support check. This is alignment
candidate evidence, not exact pose or route arrival. Stop repeating turn-only
checks as the primary milestone: next is measured forward progress and one
independently verified nearby waypoint, then route composition.

The installed executable was read on disk only. SHA256 remained
544ab726ef7b920a5ccb67aa3d06b245db7e0012317d2fe7856d58c17c31cf7e.
The archived PlayerHook signature occurred once. This does not qualify its hook,
local-player identity or offsets. Legacy hook changes flags during its player
check; a new adapter needs explicit instruction/register preservation review and
verified cleanup, not activation of all upstream hooks.

Added `tools/w101_pose_reader.py`: dependency-free beyond the existing bounded
reader, six reads/72 bytes maximum, archived ActorBody layout only, checks stable
parent/marker/pose and finite plausible fields. Six synthetic tests pass for
stable candidate, torn reads, invalid values, layout/marker rejection, sanitized
transport errors and cancellation. It has no process access, address discovery,
hook, input, zone inference or production connection. No position was read from
the live client. Separate local-player address acquisition and current-build
qualification remain required before coordinates can drive navigation.

Final checks: 2369 native tests passed; scoped native whitespace verification and
analyzers passed; nine landmark and six pose tests passed, Python formatting and
syntax checked. No package/install. No controller or capture remains running.
