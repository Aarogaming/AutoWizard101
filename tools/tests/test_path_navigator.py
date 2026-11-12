"""Unit tests for Path Navigator, Enemy Avoidance, and Anti-Stuck FSM."""

from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))

from path_navigator import PathNavigator, RoamingEnemy, UnstickStage
from steered_walker import (
    Pose,
    SteeredWalkingController,
    VK_D,
    VK_S,
    VK_SPACE,
    VK_W,
    Waypoint,
    WindowInputDispatcher,
)


class TestPathNavigator(unittest.TestCase):

    def setUp(self):
        self.dispatcher = WindowInputDispatcher(dry_run=True)
        self.controller = SteeredWalkingController(
            dispatcher=self.dispatcher,
            stuck_timeout_sec=1.0,
            min_progress_distance=20.0,
        )
        self.nav = PathNavigator(
            controller=self.controller,
            corner_safety_margin=50.0,
            enemy_safe_distance=300.0,
        )

    def test_corner_safety_inset_applies_on_sharp_turns(self):
        # A route turning 90 degrees around a corner: (0,0) -> (100,0) -> (100,100)
        route = [
            Waypoint(x=0.0, y=0.0, name="Start"),
            Waypoint(x=100.0, y=0.0, name="Corner"),
            Waypoint(x=100.0, y=100.0, name="End"),
        ]
        self.nav.load_route(route)
        active = self.nav.active_route
        self.assertEqual(len(active), 3)
        # Corner waypoint should have an outward offset added
        corner_wp = active[1]
        self.assertNotEqual(corner_wp.x, 100.0)
        self.assertTrue(corner_wp.name.endswith("_inset"))

    def test_enemy_proximity_halts_and_yields(self):
        route = [Waypoint(x=500.0, y=0.0, arrival_radius=50.0)]
        self.nav.load_route(route)

        # Enemy close to player (at distance 150 < 300 safe distance)
        mob = RoamingEnemy(
            id="mob_1", name="Lost Soul", x=150.0, y=0.0, threat_radius=200.0
        )
        self.nav.update_enemies([mob])

        pose = Pose(x=0.0, y=0.0, yaw=0.0)
        state, info = self.nav.tick(pose, now=10.0)

        self.assertEqual(state, "PAUSE_WAIT_ENEMY")
        self.assertEqual(info["enemy_name"], "Lost Soul")
        # Ensure all movement keys are released while yielding
        self.assertEqual(len(self.dispatcher.active_keys), 0)

    def test_unstick_fsm_stage_1_micro_backup(self):
        route = [Waypoint(x=500.0, y=0.0, arrival_radius=50.0)]
        self.nav.load_route(route)

        pose = Pose(x=0.0, y=0.0, yaw=0.0)

        # t=0: first tick
        self.nav.tick(pose, now=0.0)

        # t=1.2: stalls without progress (> 1.0s timeout) -> triggers obstacle
        state, info = self.nav.tick(pose, now=1.2)
        self.assertEqual(state, "OBSTACLE_DETECTED_RECOVERING")
        self.assertEqual(self.nav.unstick_stage, UnstickStage.MICRO_BACKUP)

        # t=1.3: in micro backup -> holding S and D
        state, _ = self.nav.tick(pose, now=1.3)
        self.assertEqual(state, "UNSTICK_BACKING_UP")
        self.assertIn(VK_S, self.dispatcher.active_keys)
        self.assertIn(VK_D, self.dispatcher.active_keys)

        # t=1.7: micro backup finishes (> 0.4s) -> keys released and returns to normal
        state, _ = self.nav.tick(pose, now=1.7)
        self.assertEqual(state, "UNSTICK_BACKUP_COMPLETE")
        self.assertEqual(self.nav.unstick_stage, UnstickStage.NORMAL)
        self.assertEqual(len(self.dispatcher.active_keys), 0)

    def test_unstick_fsm_stage_2_jump_clear(self):
        route = [Waypoint(x=500.0, y=0.0, arrival_radius=50.0)]
        self.nav.load_route(route)
        pose = Pose(x=0.0, y=0.0, yaw=0.0)

        # Simulate two consecutive stuck events
        self.nav.consecutive_stucks = 2
        self.nav.unstick_stage = UnstickStage.MICRO_BACKUP
        self.nav.unstick_step_start = 0.0

        # Complete backup at t=0.5
        self.nav.tick(pose, now=0.5)
        # Should escalate to JUMP_CLEAR because consecutive_stucks >= 2
        self.assertEqual(self.nav.unstick_stage, UnstickStage.JUMP_CLEAR)

        # While in jump clear: holding W and Space
        state, _ = self.nav.tick(pose, now=0.7)
        self.assertEqual(state, "UNSTICK_JUMP_CLEAR")
        self.assertIn(VK_W, self.dispatcher.active_keys)
        self.assertIn(VK_SPACE, self.dispatcher.active_keys)

    def test_smooth_arrival_at_destination(self):
        w1 = Waypoint(x=100.0, y=0.0, arrival_radius=50.0, name="Gate")
        self.nav.load_route([w1])

        # Step at gate arrival radius
        pose = Pose(x=90.0, y=0.0, yaw=0.0)
        state, info = self.nav.tick(pose, now=1.0)
        self.assertEqual(state, "DESTINATION_REACHED")
        self.assertEqual(len(self.dispatcher.active_keys), 0)


if __name__ == "__main__":
    unittest.main()
