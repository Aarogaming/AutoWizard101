# W101-PATH-NAVIGATOR-01 — Path Navigation with Enemy Avoidance & Anti-Stuck FSM

Status: implemented and tested offline; live navigation qualification remains separate.

The Path Navigator (`tools/path_navigator.py`) solves the three fundamental challenges of Wizard101 world travel:
1. **Curving / Non-Straight Roads:** Handled via Waypoint Corridors and Corner Safety Insets.
2. **Enemy Avoidance:** Real-time threat evaluation with safe-zone sidewalk yielding.
3. **Hitbox Sticking / Geometry Traps:** A progressive 3-stage Anti-Stuck Finite State Machine.

---

## 1. Curving Roads & Path Smoothing

- **Waypoint Corridor Routing:** Instead of long straight lines, roads are divided into sequences of waypoints matching the zone's natural path geometry (or parsed from `.nav` NavMesh files).
- **Corner Safety Insets (`_apply_corner_insets`):**
  - Tight interior corners (such as building edges, arches, or fences) often have collision meshes that jut out beyond their visible models.
  - The navigator automatically detects turns $> 45^\circ$ between adjacent path vectors and offsets the intermediate waypoint outward along the turn's angle bisector by a safety margin ($+60$ units).
  - This forces the character to take a wide, sweeping turn around building corners rather than cutting tight and snagging on hitboxes.

---

## 2. Dynamic Enemy Avoidance

- **Sidewalk Preference:** Waypoint paths prioritize designated sidewalk edges where mobs cannot walk.
- **Real-Time Threat Detection (`evaluate_threat`):**
  - Tracks roaming mobs via read-only telemetry (`known_enemies`).
  - Mobs have a combat pull radius (typically $300\text{--}350$ units).
  - When an enemy enters the safe threshold distance ($< 400$ units), the navigator immediately issues `PAUSE_WAIT_ENEMY`, halting forward progress on the safe boundary and waiting for the enemy to patrol past before resuming.

---

## 3. The 3-Stage Anti-Stuck FSM

When forward progress stalls for $> 1.0\text{--}1.5$ seconds (detected by lack of positional delta while `W` is down), the navigator triggers a progressive recovery hierarchy:

```
[ Movement Stalled (>1.0s) ]
             │
             ▼
   [ Stage 1: Micro-Backup ]
   • Release 'W'
   • Tap 'S' + 'D' for 0.4s (back up & turn 30°)
   • Resume 'W'
             │
   (Still stuck on attempt 2?)
             ▼
   [ Stage 2: Jump-Clear ]
   • Hold 'W' + press 'Space' for 0.5s
   • Hops over small elevation lips / terrain thresholds
             │
   (Still stuck on attempt 3?)
             ▼
   [ Stage 3: Waypoint Rollback ]
   • Step back to previous waypoint W_{i-1}
   • Re-approach with wider clearance
```

---

## Evidence

- **Unit Tests (`tools/tests/test_path_navigator.py`):** 5 tests passed:
  - `test_corner_safety_inset_applies_on_sharp_turns`: Verifies automatic outward expansion of sharp corner waypoints.
  - `test_enemy_proximity_halts_and_yields`: Verifies immediate safe-zone pause when roaming enemies enter threat range.
  - `test_unstick_fsm_stage_1_micro_backup`: Verifies reverse-and-angle recovery on initial collision.
  - `test_unstick_fsm_stage_2_jump_clear`: Verifies jump-hop escalation on repeated stalls.
  - `test_smooth_arrival_at_destination`: Verifies uninterrupted route completion.

- **Workspace Integration:**
  - Passes full Python test suite (539/539 tests passed).
  - Passes black formatting check.
