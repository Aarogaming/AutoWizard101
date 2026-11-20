import asyncio
import configparser
import copy
import json
from pathlib import Path
import sys
import tempfile
import types
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from activity_runtime import ActivityRun, ActivityStopped, combat_config, prepare_deimos_config, run_dance, validate_profile


QUEST = {"version": 1, "activity": "questing", "maxMinutes": 1, "combatStyle": "aoe",
         "minHealthPercent": 20, "minManaPercent": 10, "usePotions": True, "buyPotions": False, "teamUp": False}
DANCE = {"version": 1, "activity": "pet_dance", "maxMinutes": 5, "games": 2, "resolution": "1280x800",
         "locations": [True, False, False, False, False], "snacks": [False] * 5}


class ActivityTests(unittest.TestCase):
    def test_profile_rejects_unsupported_games_and_snack_coordinates(self):
        for changes in ({"activity": "potion"}, {"resolution": "1280x720"}, {"version": True},
                        {"games": 0}, {"maxMinutes": 0}, {"locations": [False] * 5},
                        {"resolution": "800x600", "snacks": [True] * 5}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                validate_profile(dict(DANCE, **changes))

    def test_private_config_preserves_original_and_respects_spending(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            original = "[settings]\nspeed_multiplier=5\n[hotkeys]\nkill_tool=F9\n"
            (root / "Deimos-config.ini").write_text(original)
            output = root / "run.ini"
            prepare_deimos_config(QUEST, root, output)
            parser = configparser.ConfigParser()
            parser.read(output)
            self.assertFalse(parser.getboolean("settings", "buy_potions"))
            self.assertEqual(parser.getfloat("settings", "speed_multiplier"), 1)
            self.assertEqual(parser.get("hotkeys", "kill_tool"), "F9")
            self.assertEqual((root / "Deimos-config.ini").read_text(), original)

    def test_time_limit_and_stop_request_are_real_stop_conditions(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            now = [0.0]
            run = ActivityRun(QUEST, root / "events.json", root / "stop", clock=lambda: now[0])
            run.started = 0
            now[0] = 61
            self.assertIn("time limit", run.requested_stop())
            now[0] = 1
            run.control.touch()
            with self.assertRaisesRegex(ActivityStopped, "Stopped by you"):
                run.check_stop()

    def test_policy_revocation_stops_an_existing_run(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            policy = root / "policy.conf"
            policy.write_text("ALLOW_LIVE_AUTOMATION=true\nEXECUTION_PROFILE=ExperimentalSimulation")
            now = [0.0]
            run = ActivityRun(QUEST, root / "events.json", root / "stop", clock=lambda: now[0], policy=policy)
            self.assertIsNone(run.requested_stop())
            policy.write_text("ALLOW_LIVE_AUTOMATION=false")
            now[0] = 2
            self.assertIn("Game controls are off", run.requested_stop())

    def test_deimos_uses_real_toggle_callbacks_in_order(self):
        async def check():
            with tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                run = ActivityRun(QUEST, root / "events.json", root / "stop")
                async def value(result):
                    return result
                client = types.SimpleNamespace(stats=types.SimpleNamespace(max_hitpoints=lambda: value(100), current_hitpoints=lambda: value(100), current_mana=lambda: value(100), max_mana=lambda: value(100)), zone_name=lambda: value("Area"), in_battle=lambda: value(False))
                calls = []
                def toggle(name):
                    async def call():
                        calls.append(name)
                    return call
                await run.start_deimos([client], {name: toggle(name) for name in ("combat", "dialogue", "questing")})
                self.assertEqual(calls, ["combat", "dialogue", "questing"])
                self.assertEqual(client.combat_config, combat_config(QUEST))
                self.assertFalse(client.buy_potions)
                self.assertEqual(json.loads(run.events.read_text())["phase"], "running")
                with self.assertRaisesRegex(ValueError, "exactly one"):
                    await run.start_deimos([client, client], {})
        asyncio.run(check())

    def test_low_health_stops_without_claiming_a_completed_quest(self):
        async def check():
            async def value(result):
                return result
            client = types.SimpleNamespace(
                zone_name=lambda: value("Area"), in_battle=lambda: value(False),
                stats=types.SimpleNamespace(current_hitpoints=lambda: value(10), max_hitpoints=lambda: value(100),
                                            current_mana=lambda: value(100), max_mana=lambda: value(100)))
            with tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                run = ActivityRun(QUEST, root / "events.json", root / "stop")
                with self.assertRaisesRegex(ActivityStopped, "Health fell below"):
                    await run.observe_deimos(client, {})
        asyncio.run(check())

    def test_low_resources_prevent_starting_any_gameplay_toggle(self):
        async def check():
            async def value(result):
                return result
            client = types.SimpleNamespace(
                stats=types.SimpleNamespace(max_hitpoints=lambda: value(100), current_hitpoints=lambda: value(10), current_mana=lambda: value(100), max_mana=lambda: value(100)),
                zone_name=lambda: value("Area"), in_battle=lambda: value(False))
            with tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                run = ActivityRun(QUEST, root / "events.json", root / "stop")
                with self.assertRaisesRegex(ValueError, "Refill your resources"):
                    await run.start_deimos([client], {})
                self.assertIsNone(run.started)
        asyncio.run(check())

    def test_stop_cancels_backend_and_waits_for_its_cleanup(self):
        async def check():
            with tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                run = ActivityRun(QUEST, root / "events.json", root / "stop")
                cleaned = []
                closed = []
                async def close():
                    closed.append(True)
                namespace = {"asyncio": asyncio, "run": run, "cleaned": cleaned,
                             "walker": types.SimpleNamespace(clients=[types.SimpleNamespace(close=close)])}
                exec("async def backend():\n run.started = run.clock()\n run.control.touch()\n try:\n  await asyncio.Event().wait()\n finally:\n  cleaned.append(True)", namespace)
                await run.run_deimos(namespace["backend"])
                self.assertEqual(cleaned, [True])
                self.assertEqual(closed, [True])
                self.assertEqual(run.last_status["phase"], "stopped")
                self.assertNotEqual(run.last_status["phase"], "completed")
        asyncio.run(check())

    def test_dance_reuses_backend_methods_and_counts_finished_games(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            run = ActivityRun(DANCE, root / "events.json", root / "stop")
            calls = []
            globals_ = types.SimpleNamespace(game_finished=False, q_pressed=False)
            def update():
                globals_.game_finished = True
            inputs = types.SimpleNamespace(press=lambda *a: calls.append("press"), moveTo=lambda *a: None, click=lambda: None, FAILSAFE=False)
            backend = types.SimpleNamespace(
                setup=lambda resolution: 0, Globals=globals_, pyautogui=inputs,
                DG=types.SimpleNamespace(load_application=lambda resolution: None, update_search=update, turn=0, refresh_rate=7),
                setup_game=lambda *args: calls.append("setup"), finish_game=lambda *args: calls.append("finish"))
            run_dance(run, backend, lambda: True, sleep=lambda _: None)
            self.assertEqual(calls, ["setup", "finish", "setup", "finish"])
            self.assertEqual(run.last_status["phase"], "completed")
            self.assertEqual(run.last_status["gamesPlayed"], 2)
            run2 = ActivityRun(copy.deepcopy(DANCE), root / "events2.json", root / "stop2")
            with self.assertRaisesRegex(ActivityStopped, "lost focus"):
                run_dance(run2, backend, lambda: False, sleep=lambda _: None)
            self.assertEqual(calls, ["setup", "finish", "setup", "finish"])


if __name__ == "__main__":
    unittest.main()
