# Desktop Input Actuator Architecture & Lease Wiring

> **Status (audited 2026-11-19):** the actuator and its simulated playback are implemented and tested. Live physical input through the
> `actuate` pipe command is not: the host's live branch is a stub that sends nothing (see
> [JobDeckHost Actuation Loop](JOB_DECK_ACTUATION_LOOP.md), section 4). Sections 2-4 below state what the code enforces itself and
> what it leaves to the caller. Evidence: [W101-ACTUATOR-CORE-02](coordination/results/W101-ACTUATOR-CORE-02.md), audit note.

## 1. Overview & Architectural Role

The Maelstrom automation suite enforces a strict invariant:
$$\mathbf{Facts \neq Authority \neq Actions}$$

- **Facts**: Perceptual observations from the UI tree, memory, or audio cues (`NativeHostState`).
- **Authority**: Macro quest synthesis (GOAP) and reactive arbitration (Behavior Trees) selecting parameterized *intents* (`SteeringCommandSummary`, `KinematicPlanSummary`).
- **Actions**: Physical OS keystrokes or mouse clicks dispatched to the game window under the exclusive single input lease (`GameInputLease`).

`DesktopInputActuator` (`ProjectMaelstrom/ProjectMaelstrom/Utilities/DesktopInputActuator.cs`) is the C# side that turns a
steering summary or a click summary into bounded key or mouse presses. Today its only caller outside tests is `JobDeckHost.Actuate`.
Nothing on the Rust side sends to it (the Rust summary structs serialize snake_case and the host rejects that shape), and the
actuator is not driven by the `OperationScheduler`.

The diagram shows the intended flow. In the code the "window foreground, countdown, user quiet" box is the caller-supplied
`ready()` delegate, and a `false` result returns a rejected receipt at once: nothing is paused or awaited.

```mermaid
flowchart TD
    subgraph Rust Autonomy Stack [Intent Generation]
        Bridge[MaelstromAutonomyBridge] --> SteerCmd[SteeringCommandSummary]
        Bridge --> KinPlan[KinematicPlanSummary]
    end

    subgraph Named Pipe Transport [IPC Bridge]
        SteerCmd --> Pipe[\\.\pipe\maelstrom-deck-HASH]
        KinPlan --> Pipe
    end

    subgraph C# Desktop Input Actuator [Action Dispatch]
        Pipe --> Actuator[DesktopInputActuator]
        
        subgraph Safety Invariants & Guard Checks
            Actuator --> LeaseCheck{GameInputLease.Current Held & CanDispatch?}
            LeaseCheck -- No --> Reject[Reject Dispatch & Return Invalid Receipt]
            LeaseCheck -- Yes --> ReadyCheck{Window Foreground & Countdown Done & User Quiet?}
            ReadyCheck -- No --> Hold[Pause & Await Countdown]
            ReadyCheck -- Yes --> TokenCheck{Cancellation Token Active?}
            TokenCheck -- Yes --> Abort[Abort Dispatch]
            TokenCheck -- No --> Dispatch[Emit Bounded Hardware / SendMessage Event]
        end
        
        Dispatch --> KeyDown[Key/Mouse Down]
        KeyDown --> MonitoredHold[Monitored Delay Loop]
        MonitoredHold --> KeyUp[Guaranteed Key/Mouse Up Release]
        KeyUp --> ReleaseCheck{Release Confirmed?}
        ReleaseCheck -- No --> TripHold[Trip Recovery Hold RequireRecovery]
        ReleaseCheck -- Yes --> Receipt[Return Verified RoutineReceipt]
    end
```

---

## 2. Invariant Specifications

Each call to `DesktopInputActuator` is meant to satisfy six invariants. The code enforces some itself and delegates others to the
caller; each item says which.

### 2.1. Exclusive Ownership (`GameInputLease`)
- Must hold the active session lock file (`game-input.lock`).
- `context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current)`.
- If an unconfirmed release or power loss occurred previously, the directory is held in quarantine until explicit user-supervised recovery.
- Enforced by the actuator through its `Owned()` predicate, except in simulation, where the lease test is skipped by design. The lock
  file and the quarantine belong to `GameInputLease` and were not re-audited here.

### 2.2. Target Identity Verification
- The actuator checks that the request's target equals the supplied target id and that the id has the form
  `pid-<PID>-start-<TICKS>` with positive integers. It does not check the process name and does not look for a window.
- The `WizardGraphicalClient` process-name check and the PID and start-time comparison against a real window are done by the
  callers that build the target (for example `HomeMovement.IsWizard101`). `JobDeckHost.Actuate` uses a caller-supplied `target`
  as given and does not compare it with the connected window.

### 2.3. Readiness & User Takeover Guard
- The actuator calls the caller-supplied `ready()` before the press and on every poll, and stops when it returns `false`.
  What `ready()` checks is up to the caller: foreground focus, a 3,000 ms quiet period after user input, the pre-resume countdown
  and loading screens are not tested by the actuator.
- `JobDeckHost.Actuate` passes `() => true` in simulation and `() => _session != null` in its live branch.

### 2.4. Continuous Mid-Pulse Monitoring
- A forward pulse (`W`) is held for at most 500 ms. Turn pulses (`A`, `D`) and the reverse pulse (`S`) are held for at most 300 ms.
  Measured in simulation: a 9,000 ms request held 500 ms for `W`, 300 ms for `S` and `D`.
- During the hold, `Owned()` (lease identity, target, cancellation and `ready()`) is re-evaluated about every 10 ms.
- If `Owned()` turns false or the token is cancelled, the hold ends and the key is released. Only cancellation is covered by a
  test; no test flips `ready()` during a hold.

### 2.5. Guaranteed Release & Recovery Escalation
- Every press that was attempted is released in a `finally` block by `ReleaseKeyAsync` or `ReleaseMouseAsync`, which retry every
  100 ms for up to 5,000 ms.
- If the release is not confirmed, `RequireRecovery` is called on the lease in the context (it is skipped when the context has no
  lease), which blocks further dispatch until recovery. Measured with a failing transport and a temporary lease: 5,023 ms, then
  `released=false`, `RecoveryRequired=true`, `CanDispatch=false`.

---

## 3. Data Structure Mappings

### 3.1. Steering Command Dispatch (`SteeringCommandSummary`)
Maps parameterized Pure Pursuit steering outputs to virtual keypresses:
- `ForwardPulseMs`: Dispatches `'W'` (forward motion) for up to 500 ms.
- `TurnLeftPulseMs`: Dispatches `'A'` (left turn) for up to 300 ms.
- `TurnRightPulseMs`: Dispatches `'D'` (right turn) for up to 300 ms.
- `ReversePulseMs`: Dispatches `'S'` (reverse recovery) for up to 300 ms.
- When `IsArrived == true`, dispatches zero keystrokes and reports completion.
- Only one key is sent per call, chosen in the order turn-left, turn-right, reverse, forward. A command that carries both a forward
  pulse and a turn pulse sends only the turn key (measured: forward 250 plus turn-left 80 sent `A` down and up, nothing else).
- `Curvature` and `AngleErrorRad` are carried but never read.

### 3.2. Kinematic Plan Dispatch (`KinematicPlanSummary`)
Reduces a planned mouse movement to one click. The waypoints are not forwarded, and nothing moves the cursor along a path:
- `TargetX`, `TargetY`: compared with a fixed 1280 x 720 box (0 <= X <= 1280, 0 <= Y <= 720), not with the actual client viewport.
  The comparison does not reject NaN (measured: a NaN target was accepted and the down and up events carried `NaN,NaN`); infinity
  is rejected.
- `DwellMs`: clamped to 40-200 ms by the actuator (measured: 9,000 held 200 ms, 5 held 40 ms). A NaN dwell passes the clamp and
  holds 0 ms. The log-normal sampling of the dwell happens on the Rust side (clamped to 40-180 ms there), not here.
- `PointCount` and `TotalDurationMs` are carried but never read.
- Sends one left-button down and one up at the target, under the lease check described above.

---

## 4. Verification Standards

1. **Unit Testing**: 11 tests in `DesktopInputActuatorTests` cover arrival, forward and turn pulses, rejection without a lease or
   with the wrong target, rejection when `ready()` is false at the start, cancellation during the hold, an unconfirmed release
   tripping recovery, and four click cases (valid, out of bounds, combat pass button, card and target). They do not cover NaN
   values, the `S` and `D` pulses, the clamps, or `ready()` changing during a hold.
2. **Timing Benchmarks**: a target of pulse timing within 15 ms. No test measures it. The hold is a loop on the wall clock with a
   10 ms delay step, so it is bounded by the delay granularity of the transport, which was not measured on a real transport.
3. **Safety Assertions**: the tests assert a release after cancellation and a recovery hold after a failed release. "Zero
   unreleased keys under any exception" is a design intent: exceptions inside the hold are caught and the release runs, but no
   test throws from the transport.
