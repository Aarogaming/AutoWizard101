# W101-STEERED-WALKING-01 — Natural Steered Walking Controller

Status: implemented and tested offline; live navigation qualification remains separate.

The Steered Walking Controller (`tools/steered_walker.py`) provides closed-loop, natural movement along waypoint paths without desktop mouse/keyboard hijacking, and without code injection, coordinate overwriting, or collision NOPing.

---

## Architecture and Design

1. **Read-Only Telemetry (The Sensor):**
   - Reads current spatial pose `(x, y, z, yaw)` from process memory via `W101TelemetryService`.
   - Never writes to position coordinates or patches movement instructions.

2. **Mathematical Closed-Loop Steering:**
   - Evaluates Euclidean distance to current waypoint `(x, y)`.
   - Computes target heading angle via `calculate_target_yaw`.
   - Normalizes angular error via `calculate_angular_delta` into $[-\pi, \pi]$.
   - Emits steering state transitions:
     - `TURN_LEFT` (holding 'A') or `TURN_RIGHT` (holding 'D') when $|\Delta \text{yaw}| > \text{turn\_tolerance}$ ($0.15$ rad $\approx 8.6^\circ$).
     - `MOVE_FORWARD` (holding 'W') when facing within alignment tolerance.
     - `STOP_ARRIVED` when within `arrival_radius` of the final waypoint.

3. **Background Window Message Delivery (The Actuator):**
   - Sends standard Win32 window messages (`WM_KEYDOWN = 0x0100`, `WM_KEYUP = 0x0101`) for virtual keys (`VK_W`, `VK_A`, `VK_D`) directly to the target `HWND`.
   - Leaves the user's desktop mouse cursor and physical keyboard completely unhindered.
   - Leverages the game's internal physics engine for natural walking/mount speed, terrain following, and collision boundaries.

4. **Stuck / Obstacle Detection:**
   - Monitors distance delta while forward key is engaged.
   - If progress is stalled for $> 1.5$ seconds (e.g. running into a fence or wall), the controller immediately releases all keys and emits `STUCK_OBSTACLE` for path replanning or jumping.

---

## Evidence

- **Unit Tests (`tools/tests/test_steered_walker.py`):** 6 tests passed:
  - `test_angle_normalization`: Validates angle wrapping into $[-\pi, \pi]$.
  - `test_angular_delta_shortest_path`: Validates signed angular delta across circular 0/$\pi$ boundaries.
  - `test_steering_turns_when_misaligned`: Verifies that heading misalignment dispatches turn keys ('A'/'D') without moving forward.
  - `test_steering_moves_forward_when_aligned`: Verifies straight-line movement ('W') when aligned.
  - `test_arrival_advances_and_stops`: Verifies multi-waypoint advancement and clean arrival stop.
  - `test_stuck_detection`: Verifies automatic key release and obstacle warning when forward motion is blocked.

- **Offline Boundary:**
  - `live_qualified: false`.
  - Zero desktop input hijacking; zero memory writes; zero teleportation.
