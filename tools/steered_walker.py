"""Natural Steered Walking Controller for Wizard101.

Provides closed-loop, non-glitchy character movement via:
1. Read-only spatial telemetry (current X, Y, Z coordinates and yaw heading).
2. Pure mathematical steering (bearing calculation, shortest angular delta).
3. Background Win32 window message delivery (WM_KEYDOWN / WM_KEYUP) directly
   to the Wizard101 window handle without taking over desktop mouse or keyboard.
4. Stuck/obstacle detection to prevent walking into walls.
Zero code caves, zero memory writes, zero coordinate teleportation.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
import math
from pathlib import Path
import sys
import time
from typing import Any, Callable, Dict, List, Optional, Tuple

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))


# Standard Win32 Virtual Key Codes
VK_W = 0x57
VK_A = 0x41
VK_S = 0x53
VK_D = 0x44
VK_SPACE = 0x20

WM_KEYDOWN = 0x0100
WM_KEYUP = 0x0101


class SteerAction(str, Enum):
    IDLE = "IDLE"
    TURN_LEFT = "TURN_LEFT"
    TURN_RIGHT = "TURN_RIGHT"
    MOVE_FORWARD = "MOVE_FORWARD"
    STOP_ARRIVED = "STOP_ARRIVED"
    STUCK_OBSTACLE = "STUCK_OBSTACLE"


@dataclass
class Pose:
    x: float
    y: float
    z: float = 0.0
    yaw: float = 0.0  # radians

    def distance_to(self, target: Pose | Waypoint) -> float:
        return math.dist((self.x, self.y), (target.x, target.y))


@dataclass
class Waypoint:
    x: float
    y: float
    z: float = 0.0
    arrival_radius: float = 80.0
    name: Optional[str] = None


def normalize_angle_pi(angle: float) -> float:
    """Normalizes an angle to the range [-pi, pi]."""
    while angle > math.pi:
        angle -= 2.0 * math.pi
    while angle < -math.pi:
        angle += 2.0 * math.pi
    return angle


def calculate_target_yaw(current: Pose, target: Waypoint | Pose) -> float:
    """Calculates the target yaw angle in radians to face the waypoint.

    Conforms to Wizard101's coordinate system (Y-down/Y-up orientation).
    """
    dx = target.x - current.x
    dy = target.y - current.y
    # In W101: yaw 0 faces along -Y axis, increasing clockwise
    # Using standard atan2(dx, -dy) or atan2(dx, dy)
    dist = math.hypot(dx, dy)
    if dist < 1e-5:
        return current.yaw

    origin_line = 1.0  # reference unit along -Y
    target_line = dist
    target_to_origin = math.hypot(dx, dy + 1.0)

    # Cosine rule for angle from south
    cos_val = (target_line**2 + origin_line**2 - target_to_origin**2) / (
        2.0 * origin_line * target_line
    )
    cos_val = max(-1.0, min(1.0, cos_val))
    target_angle = math.acos(cos_val)

    if target.x > current.x:
        return 2.0 * math.pi - target_angle
    return target_angle


def calculate_angular_delta(current_yaw: float, target_yaw: float) -> float:
    """Calculates the shortest signed angular difference (target - current)."""
    diff = target_yaw - current_yaw
    return normalize_angle_pi(diff)


class WindowInputDispatcher:
    """Dispatches background window messages to game client HWND."""

    def __init__(self, hwnd: Optional[int] = None, dry_run: bool = False):
        self.hwnd = hwnd
        self.dry_run = dry_run or (hwnd is None)
        self.active_keys: set[int] = set()
        self.dispatch_log: List[Tuple[float, int, str]] = []

    def key_down(self, vk_code: int):
        if vk_code not in self.active_keys:
            self.active_keys.add(vk_code)
            self._send_message(WM_KEYDOWN, vk_code)

    def key_up(self, vk_code: int):
        if vk_code in self.active_keys:
            self.active_keys.remove(vk_code)
            self._send_message(WM_KEYUP, vk_code)

    def release_all(self):
        for vk in list(self.active_keys):
            self.key_up(vk)

    def _send_message(self, msg: int, vk_code: int):
        self.dispatch_log.append(
            (
                time.monotonic(),
                vk_code,
                "KEYDOWN" if msg == WM_KEYDOWN else "KEYUP",
            )
        )
        if not self.dry_run and self.hwnd:
            try:
                import ctypes

                ctypes.windll.user32.PostMessageW(
                    self.hwnd, msg, vk_code, 0
                )  # type: ignore
            except Exception:
                pass


class SteeredWalkingController:
    """Controls character walking smoothly across waypoints without teleportation."""

    def __init__(
        self,
        dispatcher: Optional[WindowInputDispatcher] = None,
        turn_tolerance_rad: float = 0.15,  # ~8.6 degrees
        stuck_timeout_sec: float = 1.5,
        min_progress_distance: float = 20.0,
    ):
        self.dispatcher = dispatcher or WindowInputDispatcher(dry_run=True)
        self.turn_tolerance = turn_tolerance_rad
        self.stuck_timeout = stuck_timeout_sec
        self.min_progress = min_progress_distance

        self.waypoints: List[Waypoint] = []
        self.current_waypoint_index: int = 0

        # Progress tracking for stuck detection
        self.last_progress_time: float = time.monotonic()
        self.last_progress_distance: float = float("inf")

    def set_route(self, waypoints: List[Waypoint]):
        """Initializes a new waypoint route."""
        self.waypoints = list(waypoints)
        self.current_waypoint_index = 0
        self.last_progress_time = time.monotonic()
        self.last_progress_distance = float("inf")
        self.dispatcher.release_all()

    @property
    def current_waypoint(self) -> Optional[Waypoint]:
        if 0 <= self.current_waypoint_index < len(self.waypoints):
            return self.waypoints[self.current_waypoint_index]
        return None

    @property
    def is_finished(self) -> bool:
        return self.current_waypoint_index >= len(self.waypoints)

    def update(
        self, current_pose: Pose, now: Optional[float] = None
    ) -> Tuple[SteerAction, Dict[str, Any]]:
        """Processes one steering tick given the latest observed pose."""
        now_ts = now if now is not None else time.monotonic()

        target = self.current_waypoint
        if target is None:
            self.dispatcher.release_all()
            return SteerAction.STOP_ARRIVED, {"message": "All waypoints completed"}

        dist = current_pose.distance_to(target)

        # 1. Arrival Check
        if dist <= target.arrival_radius:
            self.current_waypoint_index += 1
            self.last_progress_time = now_ts
            self.last_progress_distance = float("inf")

            if self.is_finished:
                self.dispatcher.release_all()
                return SteerAction.STOP_ARRIVED, {
                    "arrived_at": target.name or f"wp_{self.current_waypoint_index}",
                    "distance": dist,
                    "remaining": 0,
                }
            # Advance to next waypoint
            target = self.current_waypoint
            if target:
                dist = current_pose.distance_to(target)

        # 2. Stuck / Obstacle Detection
        if dist < (self.last_progress_distance - self.min_progress):
            self.last_progress_distance = dist
            self.last_progress_time = now_ts
        elif (now_ts - self.last_progress_time) > self.stuck_timeout:
            self.dispatcher.release_all()
            return SteerAction.STUCK_OBSTACLE, {
                "message": "Movement stalled; obstacle or wall detected",
                "distance": dist,
                "elapsed_stalled": now_ts - self.last_progress_time,
            }

        # 3. Heading & Bearing Calculation
        target_yaw = calculate_target_yaw(current_pose, target)  # type: ignore
        angular_delta = calculate_angular_delta(current_pose.yaw, target_yaw)

        info = {
            "waypoint_index": self.current_waypoint_index,
            "target_name": target.name if target else None,
            "distance": round(dist, 2),
            "angular_delta_deg": round(math.degrees(angular_delta), 2),
        }

        # 4. Steering Decisions
        if abs(angular_delta) > self.turn_tolerance:
            # Need to turn to face waypoint
            self.dispatcher.key_up(VK_W)
            if angular_delta > 0:
                # Turn left
                self.dispatcher.key_up(VK_D)
                self.dispatcher.key_down(VK_A)
                return SteerAction.TURN_LEFT, info
            else:
                # Turn right
                self.dispatcher.key_up(VK_A)
                self.dispatcher.key_down(VK_D)
                return SteerAction.TURN_RIGHT, info
        else:
            # Aligned: walk straight forward
            self.dispatcher.key_up(VK_A)
            self.dispatcher.key_up(VK_D)
            self.dispatcher.key_down(VK_W)
            return SteerAction.MOVE_FORWARD, info

    def stop(self):
        """Immediately halts movement and releases all held keys."""
        self.dispatcher.release_all()
