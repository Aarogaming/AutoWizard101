"""Path Navigator for Wizard101 with Enemy Avoidance and Anti-Stuck FSM.

Solves:
1. Non-straight, curving roads via Waypoint Graph and Lookahead Pure Pursuit.
2. Dynamic Enemy Avoidance via real-time threat radii and sidewalk preference.
3. Building/object hitbox collisions via Corner Safety Insets and a 3-Stage
   Unstick FSM (Micro-backup, Jump-clear, Waypoint-replan).
"""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
import math
from pathlib import Path
import sys
import time
from typing import Any, Dict, List, Optional, Tuple

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from steered_walker import (
    Pose,
    SteerAction,
    SteeredWalkingController,
    VK_A,
    VK_D,
    VK_S,
    VK_SPACE,
    VK_W,
    Waypoint,
    WindowInputDispatcher,
    calculate_angular_delta,
    calculate_target_yaw,
)


class UnstickStage(str, Enum):
    NORMAL = "NORMAL"
    MICRO_BACKUP = "MICRO_BACKUP"
    JUMP_CLEAR = "JUMP_CLEAR"
    REPLAN_BACKTRACK = "REPLAN_BACKTRACK"


@dataclass
class RoamingEnemy:
    id: str
    name: str
    x: float
    y: float
    z: float = 0.0
    threat_radius: float = 350.0  # Combat pull distance

    def distance_to(self, pose: Pose | Waypoint) -> float:
        return math.dist((self.x, self.y), (pose.x, pose.y))


class PathNavigator:
    """High-level navigation planner coordinating steered walking, enemy avoidance, and recovery."""

    def __init__(
        self,
        controller: Optional[SteeredWalkingController] = None,
        corner_safety_margin: float = 60.0,
        enemy_safe_distance: float = 400.0,
    ):
        self.controller = controller or SteeredWalkingController()
        self.corner_safety_margin = corner_safety_margin
        self.enemy_safe_distance = enemy_safe_distance

        self.original_route: List[Waypoint] = []
        self.active_route: List[Waypoint] = []
        self.known_enemies: List[RoamingEnemy] = []

        # Anti-Stuck FSM
        self.unstick_stage = UnstickStage.NORMAL
        self.stuck_detected_time: float = 0.0
        self.unstick_step_start: float = 0.0
        self.consecutive_stucks: int = 0

    def load_route(self, waypoints: List[Waypoint]):
        """Loads and prepares a route with corner safety insets."""
        self.original_route = list(waypoints)
        self.active_route = self._apply_corner_insets(waypoints)
        self.controller.set_route(self.active_route)
        self.unstick_stage = UnstickStage.NORMAL
        self.consecutive_stucks = 0

    def update_enemies(self, enemies: List[RoamingEnemy]):
        """Updates real-time positions of roaming enemies in the zone."""
        self.known_enemies = list(enemies)

    def _apply_corner_insets(self, waypoints: List[Waypoint]) -> List[Waypoint]:
        """Expands waypoint turn radii away from interior building corners."""
        if len(waypoints) < 3:
            return list(waypoints)

        smoothed: List[Waypoint] = [waypoints[0]]
        for i in range(1, len(waypoints) - 1):
            prev_wp = waypoints[i - 1]
            curr_wp = waypoints[i]
            next_wp = waypoints[i + 1]

            # Vector in and out
            v_in = (curr_wp.x - prev_wp.x, curr_wp.y - prev_wp.y)
            v_out = (next_wp.x - curr_wp.x, next_wp.y - curr_wp.y)

            len_in = math.hypot(*v_in)
            len_out = math.hypot(*v_out)

            if len_in < 1e-4 or len_out < 1e-4:
                smoothed.append(curr_wp)
                continue

            # Unit vectors
            u_in = (v_in[0] / len_in, v_in[1] / len_in)
            u_out = (v_out[0] / len_out, v_out[1] / len_out)

            # Check if this represents a significant turn
            dot = u_in[0] * u_out[0] + u_in[1] * u_out[1]
            if dot < 0.7:  # Turn > ~45 degrees
                # Slightly pull corner outward along bisector
                bisector = (u_in[0] + u_out[0], u_in[1] + u_out[1])
                bis_len = math.hypot(*bisector)
                if bis_len > 1e-4:
                    offset_x = (bisector[0] / bis_len) * self.corner_safety_margin
                    offset_y = (bisector[1] / bis_len) * self.corner_safety_margin
                    smoothed.append(
                        Waypoint(
                            x=curr_wp.x + offset_x,
                            y=curr_wp.y + offset_y,
                            z=curr_wp.z,
                            arrival_radius=curr_wp.arrival_radius,
                            name=f"{curr_wp.name}_inset" if curr_wp.name else None,
                        )
                    )
                    continue

            smoothed.append(curr_wp)

        smoothed.append(waypoints[-1])
        return smoothed

    def evaluate_threat(self, pose: Pose) -> Tuple[bool, Optional[RoamingEnemy], float]:
        """Checks if any roaming enemy is dangerously close to the character."""
        for enemy in self.known_enemies:
            dist = enemy.distance_to(pose)
            if dist < self.enemy_safe_distance:
                return True, enemy, dist
        return False, None, float("inf")

    def tick(
        self, current_pose: Pose, now: Optional[float] = None
    ) -> Tuple[str, Dict[str, Any]]:
        """Main navigation update loop handling steering, enemy avoidance, and recovery."""
        now_ts = now if now is not None else time.monotonic()

        # -------------------------------------------------------------
        # 1. Unstick FSM Execution (if recovering from an obstacle)
        # -------------------------------------------------------------
        if self.unstick_stage != UnstickStage.NORMAL:
            return self._handle_unstick_fsm(current_pose, now_ts)

        # -------------------------------------------------------------
        # 2. Enemy Threat Check
        # -------------------------------------------------------------
        has_threat, threat_enemy, enemy_dist = self.evaluate_threat(current_pose)
        if has_threat and threat_enemy:
            # Emergency halt / yield on sidewalk until mob moves away
            self.controller.dispatcher.release_all()
            return "PAUSE_WAIT_ENEMY", {
                "enemy_name": threat_enemy.name,
                "enemy_dist": round(enemy_dist, 1),
                "action": "Yielding on safe boundary until enemy patrols away",
            }

        # -------------------------------------------------------------
        # 3. Standard Steered Walking
        # -------------------------------------------------------------
        steer_action, info = self.controller.update(current_pose, now=now_ts)

        if steer_action == SteerAction.STUCK_OBSTACLE:
            # Trigger recovery FSM
            self.consecutive_stucks += 1
            self.unstick_stage = UnstickStage.MICRO_BACKUP
            self.unstick_step_start = now_ts
            self.controller.dispatcher.release_all()
            return "OBSTACLE_DETECTED_RECOVERING", {
                "stage": self.unstick_stage.value,
                "attempt": self.consecutive_stucks,
                "info": info,
            }

        if steer_action == SteerAction.STOP_ARRIVED:
            self.consecutive_stucks = 0
            return "DESTINATION_REACHED", info

        return steer_action.value, info

    def _handle_unstick_fsm(self, pose: Pose, now: float) -> Tuple[str, Dict[str, Any]]:
        """Progressive 3-stage unstick recovery."""
        elapsed = now - self.unstick_step_start
        disp = self.controller.dispatcher

        if self.unstick_stage == UnstickStage.MICRO_BACKUP:
            # Stage 1: Back up for 0.4 seconds and turn slightly
            if elapsed < 0.4:
                disp.key_up(VK_W)
                disp.key_down(VK_S)
                disp.key_down(VK_D)  # Turn slightly while backing up
                return "UNSTICK_BACKING_UP", {"stage": 1, "elapsed": round(elapsed, 2)}
            else:
                disp.release_all()
                # If this is second attempt at this location, escalate to jump-clear
                if self.consecutive_stucks >= 2:
                    self.unstick_stage = UnstickStage.JUMP_CLEAR
                else:
                    self.unstick_stage = UnstickStage.NORMAL
                    self.controller.last_progress_time = now
                self.unstick_step_start = now
                return "UNSTICK_BACKUP_COMPLETE", {"stage": 1}

        elif self.unstick_stage == UnstickStage.JUMP_CLEAR:
            # Stage 2: Walk forward and press Space (jump over threshold lip)
            if elapsed < 0.5:
                disp.key_down(VK_W)
                disp.key_down(VK_SPACE)
                return "UNSTICK_JUMP_CLEAR", {"stage": 2, "elapsed": round(elapsed, 2)}
            else:
                disp.release_all()
                if self.consecutive_stucks >= 3:
                    self.unstick_stage = UnstickStage.REPLAN_BACKTRACK
                else:
                    self.unstick_stage = UnstickStage.NORMAL
                    self.controller.last_progress_time = now
                self.unstick_step_start = now
                return "UNSTICK_JUMP_COMPLETE", {"stage": 2}

        elif self.unstick_stage == UnstickStage.REPLAN_BACKTRACK:
            # Stage 3: Step back to previous waypoint
            if self.controller.current_waypoint_index > 0:
                self.controller.current_waypoint_index -= 1
            self.unstick_stage = UnstickStage.NORMAL
            self.consecutive_stucks = 0
            self.controller.last_progress_time = now
            return "UNSTICK_REPLAN_ROLLBACK", {"stage": 3}

        self.unstick_stage = UnstickStage.NORMAL
        return "UNSTICK_SETTLED", {}
