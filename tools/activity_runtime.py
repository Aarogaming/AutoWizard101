"""Guided activity contracts, observed progress, and cooperative run limits."""

from __future__ import annotations

import asyncio
import configparser
import json
from pathlib import Path
import time
import uuid


ACTIVITIES = {"questing": "deimos", "farming": "deimos", "pet_dance": "dance"}
STRATEGIES = {"damage": "any<damage> @ enemy", "aoe": "any<damage & aoe> | any<damage> @ enemy"}
RESOLUTIONS = ("800x600", "1280x800")


def validate_profile(profile):
    if not isinstance(profile, dict) or type(profile.get("version")) is not int or profile.get("version") != 1:
        raise ValueError("This setup uses an unsupported format. Create a new setup.")
    if profile.get("activity") not in ACTIVITIES:
        raise ValueError("This activity does not have a working adapter yet.")
    def integer(name, minimum, maximum):
        value = profile.get(name)
        if isinstance(value, bool) or not isinstance(value, int) or not minimum <= value <= maximum:
            raise ValueError(f"{name} must be between {minimum} and {maximum}.")
    integer("maxMinutes", 1, 180)
    if profile["activity"] == "pet_dance":
        integer("games", 1, 100)
        if profile.get("resolution") not in RESOLUTIONS:
            raise ValueError("Pet dance supports 800x600 or 1280x800.")
        for name in ("locations", "snacks"):
            values = profile.get(name)
            if not isinstance(values, list) or len(values) != 5 or any(type(value) is not bool for value in values):
                raise ValueError(f"Choose valid {name} options.")
        if not any(profile["locations"]):
            raise ValueError("Choose at least one pet dance location.")
        if profile["resolution"] == "800x600" and any(profile["snacks"]):
            raise ValueError("Snack feeding is available only at 1280x800.")
    else:
        integer("minHealthPercent", 0, 100)
        integer("minManaPercent", 0, 100)
        for name in ("usePotions", "buyPotions", "teamUp"):
            if type(profile.get(name)) is not bool:
                raise ValueError(f"Choose a value for {name}.")
        style = profile.get("combatStyle")
        if style not in (*STRATEGIES, "custom"):
            raise ValueError("Choose a combat preference.")
        if style == "custom" and (not isinstance(profile.get("customCombat"), str)
                                   or not 1 <= len(profile["customCombat"].strip()) <= 16000):
            raise ValueError("Enter a custom combat strategy (up to 16,000 characters).")
    return profile


def load_profile(path):
    return validate_profile(json.loads(Path(path).read_text(encoding="utf-8-sig")))


def combat_config(profile):
    return profile["customCombat"].strip() if profile["combatStyle"] == "custom" else STRATEGIES[profile["combatStyle"]]


def resource_stop(profile, health, max_health, mana, max_mana):
    for name, current, maximum in (("Health", health, max_health), ("Mana", mana, max_mana)):
        threshold = profile["min" + name + "Percent"]
        if threshold and maximum > 0 and current / maximum * 100 < threshold:
            return f"{name} fell below your {threshold}% stop threshold."
    return None


def prepare_deimos_config(profile, source, destination):
    """Write a private per-run INI; keep the original tool's preferences intact."""
    validate_profile(profile)
    config = configparser.ConfigParser()
    if not config.read(Path(source) / "Deimos-config.ini", encoding="utf-8-sig"):
        raise ValueError("Deimos settings are missing. Restore its source folder.")
    overrides = {
        "settings": {"speed_multiplier": "1.0", "use_potions": str(profile["usePotions"]),
                     "buy_potions": str(profile["buyPotions"]), "rich_presence": "False",
                     "drop_logging": "False", "use_anti_afk": "False"},
        "sigil": {"use_team_up": str(profile["teamUp"]), "client_to_follow": ""},
        "questing": {"client_to_boost": "", "hitter_client": "", "friend_teleport": "False",
                     "gear_switching_in_solo_zones": "False"},
        "auto pet": {"ignore_pet_level_up": "False", "only_play_dance_game": "False"},
    }
    for section, settings in overrides.items():
        if not config.has_section(section):
            config.add_section(section)
        for key, value in settings.items():
            config.set(section, key, value)
    with Path(destination).open("x", encoding="utf-8") as stream:
        config.write(stream)


class ActivityStopped(Exception):
    pass


class ActivityRun:
    def __init__(self, profile, events, control, clock=time.monotonic, policy=None):
        self.profile = validate_profile(profile)
        self.events = Path(events)
        self.control = Path(control)
        self.clock = clock
        self.created = clock()
        self.started = None
        self.phase = "starting"
        self.stop_reason = None
        self.last_status = {}
        self.policy = policy
        self.last_policy_check = -1000.0

    def emit(self, phase, message, **values):
        self.phase = phase
        status = {"phase": phase, "message": message, "activity": self.profile["activity"],
                  "elapsedSeconds": int(self.clock() - (self.started if self.started is not None else self.created)),
                  "updatedUtc": time.time(), **values}
        self.last_status = status
        self.events.parent.mkdir(parents=True, exist_ok=True)
        temporary = self.events.with_name(self.events.name + "." + uuid.uuid4().hex + ".tmp")
        try:
            temporary.write_text(json.dumps(status, ensure_ascii=False), encoding="utf-8")
            for attempt in range(4):
                try:
                    temporary.replace(self.events)
                    break
                except PermissionError:
                    if attempt == 3:
                        break
                    time.sleep(0.01)
        finally:
            temporary.unlink(missing_ok=True)
        print("ACTIVITY " + json.dumps(status, ensure_ascii=False), flush=True)

    def requested_stop(self):
        if self.control.exists():
            return "Stopped by you."
        now = self.clock()
        if self.policy and now - self.last_policy_check >= 1:
            self.last_policy_check = now
            from wizard101_hub import validate_policy
            try:
                validate_policy(self.policy)
            except (OSError, ValueError):
                return "Game controls are off or unavailable. The activity was stopped."
        if self.started is None and now - self.created >= 120:
            return "The game connection did not become ready within two minutes."
        if self.started is not None and now - self.started >= self.profile["maxMinutes"] * 60:
            return "Your time limit was reached."
        return None

    def check_stop(self):
        reason = self.requested_stop()
        if reason:
            self.stop_reason = reason
            raise ActivityStopped(reason)

    async def start_deimos(self, clients, toggles):
        if len(clients) != 1:
            raise ValueError("Guided activities require exactly one signed-in Wizard101 client.")
        self.check_stop()
        client = clients[0]
        # Establish readable state before enabling any gameplay routine.
        max_health = await client.stats.max_hitpoints()
        if max_health <= 0 or not await client.zone_name():
            raise ValueError("Sign in and enter the game world before starting this activity.")
        in_battle = await client.in_battle()
        if self.profile["activity"] == "farming" and in_battle:
            raise ValueError("Finish the current battle and stand at the dungeon entrance first.")
        if not in_battle:
            reason = resource_stop(self.profile, await client.stats.current_hitpoints(), max_health,
                                   await client.stats.current_mana(), await client.stats.max_mana())
            if reason:
                raise ValueError("Refill your resources before starting. " + reason)
        client.combat_config = combat_config(self.profile)
        client.use_potions = self.profile["usePotions"]
        client.buy_potions = self.profile["buyPotions"]
        client.use_team_up = self.profile["teamUp"]
        await toggles["combat"]()
        await toggles["dialogue"]()
        await toggles[self.profile["activity"]]()
        self.started = self.clock()
        self.emit("running", "Following your tracked quest." if self.profile["activity"] == "questing"
                  else "Farming from the dungeon entrance you selected.")

    async def observe_deimos(self, client, namespace):
        battles = 0
        in_battle_before = False
        while True:
            self.check_stop()
            zone = await client.zone_name()
            in_battle = await client.in_battle()
            if in_battle_before and not in_battle:
                battles += 1
            in_battle_before = in_battle
            health = await client.stats.current_hitpoints()
            max_health = await client.stats.max_hitpoints()
            mana = await client.stats.current_mana()
            max_mana = await client.stats.max_mana()
            if not in_battle:
                reason = resource_stop(self.profile, health, max_health, mana, max_mana)
                if reason:
                    self.stop_reason = reason
                    raise ActivityStopped(reason)
            task = namespace.get("questing_task" if self.profile["activity"] == "questing" else "sigil_task")
            if task is None or task.done():
                self.stop_reason = "The underlying activity routine ended."
                raise ActivityStopped(self.stop_reason)
            self.emit("running", "In battle." if in_battle else "Travelling or waiting for the next action.",
                      zone=zone, health=health, maxHealth=max_health, mana=mana, maxMana=max_mana,
                      battlesEnded=battles)
            await asyncio.sleep(1)

    async def run_deimos(self, callback):
        """Bound connection/runtime and let Deimos run its own cancellation cleanup."""
        self.emit("connecting", "Connecting to your wizard. Waiting for the backend to become ready.")
        main_task = asyncio.create_task(callback())
        observer = None
        namespace = callback.__wrapped__.__globals__ if hasattr(callback, "__wrapped__") else callback.__globals__
        try:
            while not main_task.done():
                self.check_stop()
                if self.started is not None and observer is None:
                    observer = asyncio.create_task(self.observe_deimos(namespace["walker"].clients[0], namespace))
                if observer is not None and observer.done():
                    await observer
                await asyncio.sleep(0.2)
            await main_task
            self.check_stop()
            if self.started is None:
                raise RuntimeError("Deimos stopped before it could start your activity. See the tool output.")
            self.emit("stopped", "The backend closed. No completion count is assumed.")
        except ActivityStopped as exc:
            self.emit("stopping", str(exc))
            main_task.cancel()
            await asyncio.gather(main_task, return_exceptions=True)
            self.emit("stopped", str(exc))
        except BaseException as exc:
            self.emit("failed", str(exc))
            raise
        finally:
            if observer is not None:
                observer.cancel()
                await asyncio.gather(observer, return_exceptions=True)
            main_task.cancel()
            await asyncio.gather(main_task, return_exceptions=True)
            cleanup_errors = []
            walker = namespace.get("walker")
            for client in getattr(walker, "clients", []):
                try:
                    await asyncio.wait_for(client.close(), timeout=5)
                except Exception as exc:
                    cleanup_errors.append(str(exc))
            listener = namespace.get("listener")
            if getattr(listener, "is_running", False):
                await asyncio.wait_for(listener.stop(), timeout=5)
            if cleanup_errors:
                raise RuntimeError("The backend could not fully release its connection: " + "; ".join(cleanup_errors))


def run_dance(run, backend, focus_check, sleep=time.sleep):
    """Use the real dance workflow without reopening its separate configuration UI."""
    profile = run.profile
    run.emit("connecting", "Checking the pet game window and loading arrow templates.")
    run.check_stop()
    if backend.setup(profile["resolution"]) != 0:
        raise ValueError("The pet game window does not match your selected resolution.")
    backend.DG.load_application(profile["resolution"])
    backend.pyautogui.FAILSAFE = True
    backend.Globals.q_pressed = False
    originals = {}
    def guarded(original):
        def call(*args, **kwargs):
            run.check_stop()
            if not focus_check():
                raise ActivityStopped("Wizard101 lost focus. Return to the game and start again.")
            return original(*args, **kwargs)
        return call
    for method in ("press", "moveTo", "click"):
        originals[method] = getattr(backend.pyautogui, method)
        setattr(backend.pyautogui, method, guarded(originals[method]))
    run.started = run.clock()
    completed = 0
    try:
        for index in range(profile["games"]):
            run.check_stop()
            if not focus_check():
                raise ActivityStopped("Wizard101 lost focus. Return to the game and start again.")
            backend.Globals.game_finished = False
            run.emit("running", f"Starting dance game {index + 1} of {profile['games']}.", gamesPlayed=completed, gamesRequested=profile["games"])
            mover = backend.setup_game([int(value) for value in profile["locations"]],
                                       [int(value) for value in profile["snacks"]], profile["resolution"])
            last_turn = -1
            last_change = run.clock()
            while not backend.Globals.game_finished:
                run.check_stop()
                if not focus_check():
                    raise ActivityStopped("Wizard101 lost focus. Return to the game and start again.")
                backend.DG.update_search()
                turn = backend.DG.turn
                if turn != last_turn:
                    last_turn, last_change = turn, run.clock()
                if run.clock() - last_change > 90:
                    raise ActivityStopped("No new dance round was detected for 90 seconds. Check the game and your pet's level.")
                run.emit("running", f"Playing dance game {index + 1} of {profile['games']}.",
                         gamesPlayed=completed, gamesRequested=profile["games"], danceRound=turn)
                sleep(1 / backend.DG.refresh_rate)
            run.check_stop()
            backend.finish_game(mover, profile["resolution"])
            completed += 1
            run.emit("running", "Dance game finished; checking the next action.", gamesPlayed=completed, gamesRequested=profile["games"])
            sleep(2)
        run.emit("completed", "Your requested dance games have finished.", gamesPlayed=completed, gamesRequested=profile["games"])
    finally:
        for method, original in originals.items():
            setattr(backend.pyautogui, method, original)
