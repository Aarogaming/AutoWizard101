"""Build bounded, visual-only Dodge transitions from a verified run in memory.

The caller must verify the report, audit, environment and whole-game split before
using this research helper. Audit data supplies only alive/dead times: policy
features are extracted exclusively from recorded visual observations. This
module does not load files, train a model, or issue input.
"""

from __future__ import annotations

import math

from train_dodge_imitation import ACTIONS_WITH_HOLD, feature_vector


def _milliseconds(ticks, speed, fps):
    return ticks * 1000 * speed / fps


def build_transitions(
    report, audit, *, player_speed=400, death_penalty=5.0, max_gap_ms=500
):
    """Return observed transitions with per-second survival reward.

    A completed, independently bracketed game-over gets one negative terminal
    reward. Incomplete runs and gaps remain truncated, never a fabricated death.
    The next observation and behavior action are recorded; no unobserved
    counterfactual outcome is claimed. Returns are not causal action values.
    """
    if not isinstance(report, dict) or not isinstance(report.get("training"), dict):
        raise ValueError("Invalid episode report")
    training = report["training"]
    speed, fps = training.get("speed"), training.get("fixed_fps")
    if (
        training.get("mode") != "stepped"
        or type(speed) not in (int, float)
        or speed not in (0.25, 0.5, 1.0)
        or fps != 30
        or type(death_penalty) not in (int, float)
        or not math.isfinite(death_penalty)
        or not 0 < death_penalty <= 100
        or type(max_gap_ms) is not int
        or not 1 <= max_gap_ms <= 5000
        or not isinstance(audit, list)
        or not 1 <= len(audit) <= 10000
    ):
        raise ValueError("Invalid bounded episode configuration")

    alive_at, first_dead, last_alive, last_tick = {}, None, None, -1
    for row in audit:
        if not isinstance(row, dict):
            raise ValueError("Invalid audit row")
        tick, alive = row.get("ticks"), row.get("alive")
        if type(tick) is not int or tick <= last_tick or type(alive) is not bool:
            raise ValueError("Invalid audit sequence")
        if first_dead is not None and alive:
            raise ValueError("Audit unexpectedly returned to alive")
        alive_at[tick] = alive
        if alive:
            last_alive = tick
        elif first_dead is None:
            first_dead = tick
        last_tick = tick
    if last_alive is None:
        raise ValueError("No observed living game state")
    terminal = report.get("stop_reason") == "game-over"
    if terminal != (first_dead is not None):
        raise ValueError("Game-over and audit terminal evidence disagree")
    end_tick = first_dead if terminal else last_alive
    end_ms = _milliseconds(end_tick, speed, fps)

    observations = []
    raw_actions = report.get("actions", [])
    raw_decisions = report.get("decisions", [])
    if (
        not isinstance(raw_actions, list)
        or not isinstance(raw_decisions, list)
        or len(raw_actions) + len(raw_decisions) > 20000
        or any(not isinstance(row, dict) for row in raw_actions + raw_decisions)
    ):
        raise ValueError("Invalid bounded episode observations")

    def action_key(row, time_key):
        timestamp, action = row.get(time_key), row.get("action")
        if (
            type(timestamp) not in (int, float)
            or not math.isfinite(timestamp)
            or not isinstance(action, str)
        ):
            raise ValueError("Invalid action identity")
        return timestamp, action

    action_records = {action_key(row, "before_capture_ms") for row in raw_actions}
    action_rows = [row for row in raw_decisions if row.get("action") is not None]
    action_decisions = {action_key(row, "capture_ms") for row in action_rows}
    if (
        len(action_records) != len(raw_actions)
        or len(action_decisions) != len(action_rows)
        or action_records != action_decisions
    ):
        raise ValueError("Action decisions and receipts do not match")
    if any(
        row.get("action") is None
        and (row.get("advice") != "Hold" or row.get("observation_fresh") is not True)
        for row in raw_decisions
    ):
        raise ValueError("Unobserved decision interrupts the episode")
    candidates = list(raw_actions)
    candidates.extend(
        {
            **row,
            "action": "hold",
            "before_capture_ms": row.get("capture_ms"),
            "outcome": "hold-observed",
        }
        for row in raw_decisions
        if row.get("advice") == "Hold"
        and row.get("action") is None
        and row.get("observation_fresh") is True
    )
    for row in candidates:
        name, timestamp = row.get("action"), row.get("before_capture_ms")
        if (
            name not in ACTIONS_WITH_HOLD
            or row.get("state") != "playing"
            or type(timestamp) not in (int, float)
            or not math.isfinite(timestamp)
            or timestamp < 0
            or timestamp > end_ms
        ):
            raise ValueError("Invalid visual decision in episode")
        tick = round(timestamp * fps / (1000 * speed))
        if (
            alive_at.get(tick) is not True
            or abs(timestamp - _milliseconds(tick, speed, fps)) > 0.01
        ):
            raise ValueError("Decision does not match a living audit time")
        outcome = row.get("outcome")
        if name == "hold":
            if outcome != "hold-observed":
                raise ValueError("Invalid hold observation")
        elif outcome != "movement-observed" and not (
            terminal and row.get("after_state") == "game_over"
        ):
            raise ValueError("Unverified action interrupts the episode")
        features = feature_vector(row, player_speed=player_speed)
        if features is None:
            raise ValueError("Invalid visual features interrupt the episode")
        exploration = row.get("exploration")
        if exploration is not None:
            if (
                not isinstance(exploration, dict)
                or type(exploration.get("Explored")) is not bool
                or type(exploration.get("EligibleCount")) is not int
                or not 1 <= exploration["EligibleCount"] <= 9
                or type(exploration.get("Probability")) not in (int, float)
                or not math.isfinite(exploration["Probability"])
                or not 0 < exploration["Probability"] <= 1
                or not isinstance(exploration.get("Advice"), str)
                or (
                    "hold"
                    if exploration["Advice"].lower() == "hold"
                    else "move-" + exploration["Advice"].lower()
                )
                != name
            ):
                raise ValueError("Invalid exploration evidence")
            exploration = {
                key: exploration[key]
                for key in ("Explored", "EligibleCount", "Probability", "Advice")
            }
        observations.append((float(timestamp), name, features, exploration))
    observations.sort(key=lambda item: item[0])
    if not observations or any(
        a[0] >= b[0] for a, b in zip(observations, observations[1:])
    ):
        raise ValueError("Missing or duplicate observed decision times")

    transitions = []
    for index, (timestamp, action, features, exploration) in enumerate(observations):
        last = index == len(observations) - 1
        next_ms = end_ms if last else observations[index + 1][0]
        elapsed = next_ms - timestamp
        if elapsed < 0:
            raise ValueError("Episode ends before an observation")
        gap = elapsed > max_gap_ms
        terminated = last and terminal and not gap
        truncated = gap or (last and not terminated)
        # No reward is invented across an unobserved gap.
        reward = 0.0 if gap else (-death_penalty if terminated else elapsed / 1000)
        transitions.append(
            {
                "time_ms": timestamp,
                "state": features,
                "action": action,
                "exploration": exploration,
                "next_state": None if last or gap else observations[index + 1][2],
                "next_action": None if last or gap else observations[index + 1][1],
                "elapsed_ms": elapsed,
                "reward": reward,
                "terminated": terminated,
                "truncated": truncated,
            }
        )
    return transitions


def discounted_returns(transitions, *, discount_per_second=0.99):
    """Compute complete-segment returns without bootstrapping through gaps.

    Values describe the recorded behavior following an action, not the causal
    result of that action. Censored segments return None until a verified
    terminal transition is available; no reward is invented after a stop.
    """
    if (
        not isinstance(transitions, list)
        or not 1 <= len(transitions) <= 20000
        or type(discount_per_second) not in (int, float)
        or not math.isfinite(discount_per_second)
        or not 0 < discount_per_second <= 1
    ):
        raise ValueError("Invalid return calculation")
    values = [None] * len(transitions)
    future = None
    for index in range(len(transitions) - 1, -1, -1):
        row = transitions[index]
        if not isinstance(row, dict):
            raise ValueError("Invalid transition")
        reward, elapsed = row.get("reward"), row.get("elapsed_ms")
        terminated, truncated = row.get("terminated"), row.get("truncated")
        if (
            type(reward) not in (int, float)
            or not math.isfinite(reward)
            or type(elapsed) not in (int, float)
            or not math.isfinite(elapsed)
            or elapsed < 0
            or type(terminated) is not bool
            or type(truncated) is not bool
            or (terminated and truncated)
        ):
            raise ValueError("Invalid transition outcome")
        if terminated or truncated:
            if row.get("next_state") is not None or row.get("next_action") is not None:
                raise ValueError("Ended transition cannot have a next action")
        elif (
            index + 1 >= len(transitions)
            or not isinstance(transitions[index + 1], dict)
            or row.get("next_state") is None
            or row.get("next_state") != transitions[index + 1].get("state")
            or row.get("next_action") != transitions[index + 1].get("action")
        ):
            raise ValueError("Transition does not match the next observed decision")
        if truncated:
            future = None
        elif terminated:
            future = float(reward)
        elif future is not None:
            future = reward + discount_per_second ** (elapsed / 1000) * future
        values[index] = future
    return values
