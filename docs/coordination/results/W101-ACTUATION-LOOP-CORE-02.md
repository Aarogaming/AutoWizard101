# Claim Evidence: W101-ACTUATION-LOOP-CORE-02

Status: **Partially verified** (audited 2026-11-19; simulated playback is real and tested through the pipe, the live branch is a stub; see
"Audit note" at the end).

## 1. Overview
- **Task ID**: `W101-ACTUATION-LOOP-CORE-02`
- **Workstream**: `CORE-TRANSITIONS`
- **Owner**: `coordinator`
- **Scope**: Concrete implementation and native named pipe integration test verification of the `JobDeckHost` actuation event loop (`actuate` command) and `DesktopInputActuator` simulated playback engine.

## 2. Implementation Summary
1. **DesktopInputActuator Simulation Engine** (`ProjectMaelstrom/ProjectMaelstrom/Utilities/DesktopInputActuator.cs`):
   - Added `_isSimulation` field to `DesktopInputActuator` constructor.
   - Added `CreateSimulation(out var recordedEvents, clock)` factory method recording key and mouse down/up events into an in-memory audit list without physical OS injection or cursor movement.
   - Updated `Owned()` predicate to permit simulated execution while strictly enforcing `GameInputLease.Current` for live execution.
2. **JobDeckHost Actuation Command** (`ProjectMaelstrom/ProjectMaelstrom/Utilities/JobDeckHost.cs`):
   - Added `"actuate"` command to `JobDeckHost.Handle`.
   - Deserializes `SteeringCommandSummary` and `KinematicPlanSummary` using `JobDeckRules.Json`.
   - In simulation mode (`simulation: true`): Dispatches through `DesktopInputActuator.CreateSimulation` and returns structured JSON containing `accepted`, `released`, `actionId`, and `dispatchedEvents`.
   - In live mode (`simulation: false`): Enforces active `_session`, enabled travel/quest actions, and active `GameInputLease.Current`, rejecting unverified calls safely.
3. **Integration Tests** (`ProjectMaelstrom/ProjectMaelstrom.Tests/JobDeckHostActuationTests.cs`):
   - `Actuate_SimulatedSteering_ReturnsVerifiedDispatchedKeyEvents`: Verifies 'W' pulse down/up sequence over named pipe.
   - `Actuate_SimulatedTurnLeft_ReturnsTurnKeyEvents`: Verifies 'A' turn sequence over named pipe.
   - `Actuate_SimulatedKinematicClick_ReturnsVerifiedMouseEvents`: Verifies mouse click down/up sequence at $(640, 360)$ over named pipe.
   - `Actuate_SimulatedOutOfBoundsClick_RejectedWithZeroEvents`: Proves out-of-bounds clicks ($X = 1500$) are rejected with zero events.
   - `Actuate_LiveModeWithoutConnection_RejectsGracefully`: Confirms graceful rejection of live requests without live session/lease.

## 3. Red Team / Blue Team Adversarial Audit
- **Zero Accidental Live Dispatch**: Live mode requires explicit `_session` and `GameInputLease.Current`. Any unauthenticated or unconfigured caller receives an explicit error without touching any OS handles.
- **Pipe Stability**: All command parsing is safely enclosed; unexpected arguments return error replies without crashing or hanging the pipe server.
- **Deadlock Free**: Offloaded via `Task.Run` with `.ConfigureAwait(false)` to prevent UI thread lockups.

## 4. Test & Quality Gate Evidence
- `dotnet test ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --filter "FullyQualifiedName~DesktopInputActuatorTests|FullyQualifiedName~JobDeckHostActuationTests"`: 16 passed, 0 failed (11 in `DesktopInputActuatorTests` + 5 in `JobDeckHostActuationTests`).
- `dotnet format whitespace --verify-no-changes`: clean.
- `dotnet format analyzers --verify-no-changes`: 0 warnings, 0 errors.
- `dotnet build -c Release -warnaserror`: 0 warnings, 0 errors.
- `python tools/check_project_board.py`: Valid (30 workstreams, zero broken links).
- `python tools/check_text_encoding.py`: 0 violations.

## Audit note (2026-11-19, Claude)

Checked against the committed `JobDeckHost.cs` and `DesktopInputActuator.cs`, `JobDeckHostActuationTests.cs`, and the same throwaway
probe as in the [W101-ACTUATOR-CORE-02](W101-ACTUATOR-CORE-02.md) audit note (not committed; no game, no OS input). The original text
above is kept unchanged.

**Verified.** `actuate` is in `JobDeckHost.Handle`, it reads `steering` or `kinematic` with `JobDeckRules.Json`, and simulation
returns `accepted`, `released`, the ids, the time and `dispatchedEvents`. The five tests exist and pass, and they go through the real
named pipe (`JobDeckWire.CallAsync`). A bad or unmapped argument comes back as an `ok: false` error reply through the generic catch
in `Handle` (probe: `JsonException`). A live request with no connected session is refused with the error the test expects.

**Corrections.**
1. **The live branch cannot send input and can trip recovery.** With a session, travel or quest actions enabled and a lease held,
   it builds an actuator over a runtime whose key and mouse sends all return `false`. Probe with that runtime and a temporary lease:
   5,023 ms, `accepted=False`, `released=False`, `RecoveryRequired=True`, `CanDispatch=False`. So a live call never replies
   `accepted: true`, stalls the host for about 5 s and latches a recovery hold. The summary's "live mode ... rejecting unverified
   calls safely" is true only for the no-session and no-lease cases.
2. **"Zero Accidental Live Dispatch ... without touching any OS handles" is true because there is no live transport,** not because of
   the checks. Also, a caller-supplied `target` is only format-checked and is not compared with the connected window.
3. **"Deadlock Free" is not shown.** `Handle` holds `_gate` for the whole command, and `Actuate` blocks on `Task.Run(...).GetAwaiter()
   .GetResult()` inside it, so every other deck command waits for the dispatch: about 500 ms in simulation (busy-waiting), about 5 s in
   the live failure above. `.ConfigureAwait(false)` is inside the actuator, not on these calls, and the host is not a UI thread. No
   test runs a second command while an `actuate` is in flight.
4. **The shapes do not match the Rust side.** The host reads camelCase and rejects unknown members; the Rust structs serialize
   snake_case (probe: `'forward_pulse_ms'` could not be mapped). No Rust crate sends `actuate`.
5. **Live-path test coverage is one case.** Only "without connection" is tested. There is no test for a held lease, a missing lease
   with a session, or the failure above.

`JOB_DECK_ACTUATION_LOOP.md` was corrected to match (new status banner, request note, simulation note and a rewritten section 4).

**What this evidence does not prove.** That any `actuate` call ever reaches a game window, that the host stays responsive while one
runs, or that a real transport would behave as the stub does.
