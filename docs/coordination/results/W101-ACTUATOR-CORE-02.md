# Claim Evidence: W101-ACTUATOR-CORE-02

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
