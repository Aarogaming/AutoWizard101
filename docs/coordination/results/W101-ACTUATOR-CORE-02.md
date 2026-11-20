# Claim Evidence: W101-ACTUATOR-CORE-02

Status: **Partially verified** (audited 2026-11-19; the actuator and its simulation are real and tested, the scheduler wiring and readiness
checks described below are not; see "Audit note" at the end).

## 1. Overview
- **Task ID**: `W101-ACTUATOR-CORE-02`
- **Workstream**: `W101-NAVIGATION`
- **Owner**: `coordinator`
- **Scope**: Concrete implementation and native test verification of `DesktopInputActuator` (`ProjectMaelstrom/ProjectMaelstrom/Utilities/DesktopInputActuator.cs`), wiring `SteeringCommandSummary` and `KinematicPlanSummary` into the C# scheduler under the single input lease (`GameInputLease`).

## 2. Implementation Summary
1. **Actuator Core** (`ProjectMaelstrom/ProjectMaelstrom/Utilities/DesktopInputActuator.cs`):
   - Contract records:
     - `SteeringCommandSummary`: Forward/turn/reverse pulse durations, curvature, angle error, arrival boolean.
     - `KinematicPlanSummary`: Waypoint count, total duration, dwell duration, target X/Y coordinates.
     - `DesktopInputRuntime`: Pluggable delegates for `SendKey`, `SendMouse`, `Clock`, and `Delay`.
   - Dispatch pipelines:
     - `DispatchSteeringAsync`: Validates `Owned()` predicate (`GameInputLease.Current`, `CanDispatch`, target identity, ready state, cancellation). Clamps pulses ($\le 500\text{ms}$ forward, $\le 300\text{ms}$ turns). Executes monitored 10ms poll loop. Guarantees key release in `finally` via `ReleaseKeyAsync`; trips `RequireRecovery` on unconfirmed release.
     - `DispatchKinematicClickAsync`: Validates 1280x720 viewport bounds, clamps dwell time between 40ms and 200ms, sends mouse down/up with monitored delay, and ensures guaranteed release with recovery escalation.
2. **Unit & Integration Tests** (`ProjectMaelstrom/ProjectMaelstrom.Tests/DesktopInputActuatorTests.cs`):
   - `DispatchSteering_WhenArrived_ReturnsImmediateSuccessWithoutKeyDispatch`: Proves idle arrival emits zero keystrokes.
   - `DispatchSteering_ForwardPulse_EmitsKeyDownAndKeyUp`: Validates 'W' down, hold, and release sequence.
   - `DispatchSteering_TurnLeft_EmitsKeyDownAndKeyUp`: Validates 'A' turn sequence.
   - `DispatchSteering_Rejects_WhenNoLeaseOrWrongTarget`: Confirms rejection when lease is missing or target mismatch.
   - `DispatchSteering_Rejects_WhenReadyIsFalse`: Confirms focus/countdown readiness gating.
   - `DispatchSteering_Cancellation_AbortsHoldAndReleasesKey`: Confirms immediate abort and guaranteed release on cancellation.
   - `DispatchSteering_UnconfirmedRelease_TripsRecoveryHold`: Proves failed key release trips persistent `RecoveryRequired` hold.
   - `DispatchKinematicClick_ValidTarget_DispatchesMouseDownAndUp`: Validates bounded mouse click with dwell.
   - `DispatchKinematicClick_OutOfBounds_Rejected`: Confirms rejection of coordinates outside 1280x720 bounds.
   - `DispatchKinematicClick_CombatPassButton_DispatchesWithinBounds`: Confirms combat pass button click dispatches within bounds.
   - `DispatchKinematicClick_CombatCardAndTarget_DispatchesWithinBounds`: Confirms card click and duel target click sequence dispatches within bounds.

## 3. Red Team / Blue Team Adversarial Audit
- **Stuck Key Protection**: Mid-pulse loop polls every 10ms; any focus loss, user keypress, or cancellation breaks hold and executes guaranteed release in `finally`.
- **Exclusive Lease Guard**: `ReferenceEquals(context.Input, GameInputLease.Current)` verified on every dispatch; zero physical events can emit without holding the OS file lock.
- **Viewport Bounds Defense**: Coordinates strictly verified within $0 \le X \le 1280, 0 \le Y \le 720$.

## 4. Test & Quality Gate Evidence
- `dotnet test ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --filter FullyQualifiedName~DesktopInputActuatorTests`: 11 passed, 0 failed.
- `dotnet format whitespace --verify-no-changes`: clean.
- `dotnet format analyzers --verify-no-changes`: 0 warnings, 0 errors.
- `dotnet build -c Release -warnaserror`: 0 warnings, 0 errors.
- `python tools/check_project_board.py`: Valid (30 workstreams, zero broken links).
- `python tools/check_text_encoding.py`: 0 violations.

## Audit note (2026-11-19, Claude)

Checked against `DesktopInputActuator.cs`, the committed `JobDeckHost.cs`, both test files, the two docs this task owns and a
throwaway xunit probe in a separate worktree (not committed). The probe used the in-memory simulation transport and, for one case,
a lease in a temporary directory. No game window and no OS input were involved. The original text above is kept unchanged.

**Verified.** The three records, the constants (500 ms forward, 300 ms turns and reverse, 40-200 ms dwell, 5,000 ms release wait),
the `Owned()` predicate, the 10 ms re-check loop, release in `finally` with a retry every 100 ms, `RequireRecovery` on a failed
release and the 1280 x 720 rejection are in the code. The 11 listed tests exist and pass (re-run today together with the 5 host
tests: 16 passed). The clamps hold: a 9,000 ms request held 500 ms for forward, 300 ms for reverse and for a turn; a click dwell of
9,000 held 200 ms and a dwell of 5 held 40 ms. A failed release does trip recovery (probe: 5,023 ms, `released=False`,
`RecoveryRequired=True`, `CanDispatch=False`).

**Corrections.**
1. **Not wired into the scheduler.** The only non-test user of the actuator is `JobDeckHost.Actuate`, which builds its own
   `OperationContext` by hand: no `OperationScheduler`, no focus budget. Nothing on the Rust side sends the summaries, and the Rust
   structs serialize snake_case, which the host's reader rejects (probe: `JsonException`, `'forward_pulse_ms'` could not be mapped).
2. **Readiness and takeover are delegated.** The actuator only calls the `ready()` delegate it is given. Foreground focus, the
   3,000 ms quiet period, the countdown and loading screens are not checked by it; the host passes `() => true` in simulation and
   `() => _session != null` live. So "any focus loss, user keypress, or cancellation breaks hold" is true only for cancellation as
   implemented. The `ready()` test is false at the start; no test flips it during a hold.
3. **Target identity is a format check.** The actuator requires `pid-<n>-start-<n>` with positive integers and equality with the
   request's target. It does not check the process name or any window, and the host uses a caller-supplied target as given.
4. **"Zero physical events without the OS file lock" is about the predicate, not the transport.** The lease test is skipped in
   simulation by design and the transport is injected. The lock file belongs to `GameInputLease` and was not re-audited.
5. **One key per call, and several fields are unused.** Order of choice: turn-left, turn-right, reverse, forward. Probe: forward 250
   plus turn-left 80 sent only `A` down and up. `Curvature`, `AngleErrorRad`, `PointCount` and `TotalDurationMs` are never read, and
   no cursor path is produced: a click is one down and one up at the target.
6. **The viewport defense is a fixed box and lets NaN through.** The comparison uses constants, not the client's viewport. Probe: a
   NaN target was accepted with events at `NaN,NaN`, a NaN dwell was accepted and held 0 ms, infinity was rejected. A NaN needs an
   in-process caller; I did not test whether the pipe's JSON can carry one.
7. **Simulation is not instant.** Its delay is a no-op, so the hold loop busy-waits on the wall clock: a 500 ms forward pulse took 500
   ms wall and about 600 ms of process CPU (process-wide, so approximate).
8. **Gaps in the tests.** None covers NaN, `S` or `D`, the clamps, a transport that throws, or `ready()` changing mid-hold.

The docs this task owns were corrected to match (`DESKTOP_INPUT_ACTUATOR.md`, plus the references in `ROUTINE_EXECUTION_MODEL.md`,
`BACKEND_CONTROL_PLANE.md` and `INDEX.md`).

**What this evidence does not prove.** Nothing here shows input reaching a game window. The simulation and the injected transports
show the actuator's own logic only; the 15 ms timing target in the doc was never measured on a real transport.
