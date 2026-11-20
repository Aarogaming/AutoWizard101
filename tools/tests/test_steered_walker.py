"""Unit tests for Natural Steered Walking Controller."""

import math
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))

from steered_walker import (
    Pose,
    SteerAction,
    SteeredWalkingController,
    VK_A,
    VK_D,
    VK_W,
    Waypoint,
    WindowInputDispatcher,
    calculate_angular_delta,
    calculate_target_yaw,
    normalize_angle_pi,
)


class TestSteeredWalker(unittest.TestCase):

    def test_angle_normalization(self):
        self.assertAlmostEqual(normalize_angle_pi(0.0), 0.0)
        self.assertAlmostEqual(normalize_angle_pi(math.pi), math.pi)
        self.assertAlmostEqual(normalize_angle_pi(-math.pi), -math.pi)
        self.assertAlmostEqual(normalize_angle_pi(3.0 * math.pi), math.pi)
        self.assertAlmostEqual(normalize_angle_pi(-3.0 * math.pi), -math.pi)

    def test_angular_delta_shortest_path(self):
        # 10 deg to 20 deg -> +10 deg
        delta = calculate_angular_delta(math.radians(10), math.radians(20))
        self.assertAlmostEqual(math.degrees(delta), 10.0, places=4)

        # 350 deg to 10 deg -> +20 deg (crosses 0)
        delta = calculate_angular_delta(math.radians(350), math.radians(10))
        self.assertAlmostEqual(math.degrees(delta), 20.0, places=4)

        # 10 deg to 350 deg -> -20 deg
        delta = calculate_angular_delta(math.radians(10), math.radians(350))
        self.assertAlmostEqual(math.degrees(delta), -20.0, places=4)

    def test_steering_turns_when_misaligned(self):
        dispatcher = WindowInputDispatcher(dry_run=True)
        controller = SteeredWalkingController(
            dispatcher=dispatcher, turn_tolerance_rad=0.1
        )

        # Waypoint directly North (+Y direction) from (0, 0)
        # In our coordinate system, let target be at (100, 0)
        controller.set_route([Waypoint(x=100.0, y=0.0, arrival_radius=20.0)])

        # Current pose at (0, 0) facing angle 0, but target requires turning
        pose = Pose(x=0.0, y=0.0, yaw=0.0)
        action, info = controller.update(pose, now=100.0)

        # Should command a turn and hold either A or D
        self.assertIn(action, (SteerAction.TURN_LEFT, SteerAction.TURN_RIGHT))
        self.assertTrue(
            VK_A in dispatcher.active_keys or VK_D in dispatcher.active_keys
        )
        self.assertNotIn(VK_W, dispatcher.active_keys)

    def test_steering_moves_forward_when_aligned(self):
        dispatcher = WindowInputDispatcher(dry_run=True)
        controller = SteeredWalkingController(
            dispatcher=dispatcher, turn_tolerance_rad=0.2
        )

        target = Waypoint(x=0.0, y=-500.0, arrival_radius=50.0)
        controller.set_route([target])

        # Calculate exact target yaw for facing (0, -500)
        current = Pose(x=0.0, y=0.0, yaw=0.0)
        target_yaw = calculate_target_yaw(current, target)
        aligned_pose = Pose(x=0.0, y=0.0, yaw=target_yaw)

        action, info = controller.update(aligned_pose, now=100.0)
        self.assertEqual(action, SteerAction.MOVE_FORWARD)
        self.assertIn(VK_W, dispatcher.active_keys)
        self.assertNotIn(VK_A, dispatcher.active_keys)
        self.assertNotIn(VK_D, dispatcher.active_keys)

    def test_arrival_advances_and_stops(self):
        dispatcher = WindowInputDispatcher(dry_run=True)
        controller = SteeredWalkingController(dispatcher=dispatcher)

        w1 = Waypoint(x=100.0, y=100.0, arrival_radius=30.0, name="PointA")
        w2 = Waypoint(x=200.0, y=100.0, arrival_radius=30.0, name="PointB")
        controller.set_route([w1, w2])

        # Step 1: Arrive at Point A
        pose_at_a = Pose(x=95.0, y=100.0, yaw=0.0)
        action, info = controller.update(pose_at_a, now=100.0)
        # Should advance to Point B
        self.assertEqual(controller.current_waypoint_index, 1)

        # Step 2: Arrive at Point B (final)
        pose_at_b = Pose(x=195.0, y=100.0, yaw=0.0)
        action, info = controller.update(pose_at_b, now=101.0)
        self.assertEqual(action, SteerAction.STOP_ARRIVED)
        self.assertTrue(controller.is_finished)
        self.assertEqual(len(dispatcher.active_keys), 0)

    def test_stuck_detection(self):
        dispatcher = WindowInputDispatcher(dry_run=True)
        controller = SteeredWalkingController(
            dispatcher=dispatcher,
            stuck_timeout_sec=1.5,
            min_progress_distance=20.0,
        )
        controller.set_route([Waypoint(x=500.0, y=0.0, arrival_radius=20.0)])

        pose = Pose(x=0.0, y=0.0, yaw=0.0)
        # Initial tick at t=0
        controller.update(pose, now=0.0)

        # Same position at t=1.0 (stalled, but within 1.5s timeout)
        action, _ = controller.update(pose, now=1.0)
        self.assertNotEqual(action, SteerAction.STUCK_OBSTACLE)

        # Same position at t=2.0 (exceeded 1.5s without >= 20.0 progress)
        action, info = controller.update(pose, now=2.0)
        self.assertEqual(action, SteerAction.STUCK_OBSTACLE)
        # All keys should be released immediately
        self.assertEqual(len(dispatcher.active_keys), 0)


if __name__ == "__main__":
    unittest.main()
