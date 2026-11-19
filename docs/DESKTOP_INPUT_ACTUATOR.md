# Desktop Input Actuator Architecture & Lease Wiring

## 1. Overview & Architectural Role

The Maelstrom automation suite enforces a strict invariant:
$$\mathbf{Facts \neq Authority \neq Actions}$$

- **Facts**: Perceptual observations from the UI tree, memory, or audio cues (`NativeHostState`).
- **Authority**: Macro quest synthesis (GOAP) and reactive arbitration (Behavior Trees) selecting parameterized *intents* (`SteeringCommandSummary`, `KinematicPlanSummary`).
- **Actions**: Physical OS keystrokes or mouse clicks dispatched to the game window under the exclusive single input lease (`GameInputLease`).

`DesktopInputActuator` (`ProjectMaelstrom/ProjectMaelstrom/Utilities/DesktopInputActuator.cs`) is the definitive bridge connecting the Rust autonomy engine (`MaelstromAutonomyBridge`) to physical Windows input dispatch.

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

Every call to `DesktopInputActuator` must satisfy six mandatory invariants:

### 2.1. Exclusive Ownership (`GameInputLease`)
- Must hold the active session lock file (`game-input.lock`).
- `context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current)`.
- If an unconfirmed release or power loss occurred previously, the directory is held in quarantine until explicit user-supervised recovery.

### 2.2. Target Identity Verification
- The target window must match the exact bound PID and start timestamp: `pid-<PID>-start-<TICKS>`.
- The target process name must strictly equal `"WizardGraphicalClient"`.

### 2.3. Readiness & User Takeover Guard
- `ready()` callback must return `true`:
  - Target window has foreground focus.
  - No mouse motion or user keypress detected within the last 3,000 ms.
  - Pre-resume countdown (if transitioning from paused state) has reached zero.
  - Game is not in a loading screen or zone transition.

### 2.4. Continuous Mid-Pulse Monitoring
- Keyboard pulses (`W`, `A`, `S`, `D`) are held for at most 500 ms per tick.
- During the pulse hold, ownership and user takeover are polled every 10 ms.
- If focus is lost or user input is detected, the key is released immediately.

### 2.5. Guaranteed Release & Recovery Escalation
- All key and mouse releases execute within a `finally` block via `ReleaseAsync`.
- If the game thread fails to acknowledge key-up within 5,000 ms, `RequireRecovery` trips a persistent lock, preventing any further automation until manually verified.

---

## 3. Data Structure Mappings

### 3.1. Steering Command Dispatch (`SteeringCommandSummary`)
Maps parameterized Pure Pursuit steering outputs to virtual keypresses:
- `ForwardPulseMs`: Dispatches `'W'` (forward motion) for up to 500 ms.
- `TurnLeftPulseMs`: Dispatches `'A'` (left turn) for up to 300 ms.
- `TurnRightPulseMs`: Dispatches `'D'` (right turn) for up to 300 ms.
- `ReversePulseMs`: Dispatches `'S'` (reverse recovery) for up to 300 ms.
- When `IsArrived == true`, dispatches zero keystrokes and reports completion.

### 3.2. Kinematic Plan Dispatch (`KinematicPlanSummary`)
Maps humanized Bézier trajectory waypoints to mouse interactions:
- `TargetX`, `TargetY`: Validated against client viewport bounds ($0 \le X \le 1280, 0 \le Y \le 720$).
- `DwellMs`: Down-press hold duration modulated by log-normal distribution ($40\text{ ms} \le \text{dwell} \le 200\text{ ms}$).
- Dispatches left mouse click under the verified lease.

---

## 4. Verification Standards

1. **Unit Testing**: Isolated tests verifying rejection when lease is missing, cancellation is tripped, or window is non-foreground.
2. **Timing Benchmarks**: Pulse timing bounded within $\pm 15\text{ms}$ of target duration.
3. **Safety Assertions**: Zero unreleased keys under any cancellation or exception scenario.
