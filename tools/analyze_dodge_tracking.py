"""Compare recorded visual observations with an isolated game's offline audit.

This diagnostic is not used by the player and must not be read during a run.
It evaluates action frames only; a nearby match is an approximation, not a
verified object identity or a measurement of all rendered frames.
"""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
import statistics


def analyze(report, audit, match_radius=65):
    training = report.get("training", {})
    speed, fps = training.get("speed"), training.get("fixed_fps")
    if training.get("mode") != "stepped" or speed not in (0.25, 0.5, 1) or fps != 30:
        raise ValueError("Expected a supported stepped Dodge report")
    if not math.isfinite(match_radius) or match_radius <= 0:
        raise ValueError("Invalid match radius")
    by_tick = {row["ticks"]: row for row in audit}
    if len(by_tick) != len(audit):
        raise ValueError("Duplicate audit ticks")
    aligned = missing = native = visual = matched = near = near_missing = 0
    player_errors, velocity_errors, near_velocity_errors, forward_distances = (
        [],
        [],
        [],
        [],
    )
    for action in report.get("actions", []):
        tick = round(action["before_capture_ms"] * fps / (1000 * speed))
        row = by_tick.get(tick)
        if row is None:
            missing += 1
            continue
        aligned += 1
        player = row["player"]
        # Check whether the game actually moved in the requested direction;
        # an input receipt alone is not evidence of movement.
        after_ms = action.get("after_capture_ms")
        after = (
            by_tick.get(round(after_ms * fps / (1000 * speed)))
            if after_ms is not None
            else None
        )
        direction = str(action.get("action", "")).removeprefix("move-").split("-")
        dx = ("right" in direction) - ("left" in direction)
        dy = ("down" in direction) - ("up" in direction)
        if after is not None and (dx or dy):
            forward_distances.append(
                (
                    (after["player"]["x"] - player["x"]) * dx
                    + (after["player"]["y"] - player["y"]) * dy
                )
                / math.hypot(dx, dy)
            )
        observed_player = action.get("before_player")
        if observed_player is not None:
            player_errors.append(
                math.hypot(
                    observed_player["X"] * 480 - player["x"],
                    observed_player["Y"] * 720 - player["y"],
                )
            )
        # Only include enemy centers in the viewport. Partial sprites and occluded
        # bodies can still make these approximate correspondence counts optimistic.
        mobs = [m for m in row["mobs"] if 0 <= m["x"] <= 480 and 0 <= m["y"] <= 720]
        threats = action["threats"]
        native += len(mobs)
        visual += len(threats)
        pairs = sorted(
            (
                math.hypot(
                    m["x"] - t["position"]["X"] * 480, m["y"] - t["position"]["Y"] * 720
                ),
                i,
                j,
            )
            for i, m in enumerate(mobs)
            for j, t in enumerate(threats)
        )
        used_mobs, used_threats = set(), set()
        for distance, i, j in pairs:
            if distance > match_radius:
                break
            if i in used_mobs or j in used_threats:
                continue
            used_mobs.add(i)
            used_threats.add(j)
            threat, mob = threats[j], mobs[i]
            if threat["velocity_known"]:
                error = math.hypot(
                    threat["velocity_px_s"]["X"] - mob["vx"],
                    threat["velocity_px_s"]["Y"] - mob["vy"],
                )
                velocity_errors.append(error)
                if math.hypot(mob["x"] - player["x"], mob["y"] - player["y"]) < 170:
                    near_velocity_errors.append(error)
        matched += len(used_mobs)
        near_indices = {
            i
            for i, mob in enumerate(mobs)
            if math.hypot(mob["x"] - player["x"], mob["y"] - player["y"]) < 170
        }
        near += len(near_indices)
        near_missing += len(near_indices - used_mobs)
    return {
        "action_frames_aligned": aligned,
        "action_frames_without_audit": missing,
        "movement_pairs_aligned": len(forward_distances),
        "movements_under_5_px_forward": sum(
            distance < 5 for distance in forward_distances
        ),
        "forward_movement_median_px": (
            statistics.median(forward_distances) if forward_distances else None
        ),
        "visible_native_enemy_samples": native,
        "visual_enemy_samples": visual,
        "approximate_one_to_one_matches": matched,
        "near_player_enemy_samples": near,
        "near_player_without_match": near_missing,
        "player_position_error_median_px": (
            statistics.median(player_errors) if player_errors else None
        ),
        "player_position_error_max_px": max(player_errors) if player_errors else None,
        "known_velocity_matches": len(velocity_errors),
        "velocity_error_median_px_s": (
            statistics.median(velocity_errors) if velocity_errors else None
        ),
        "velocity_errors_over_100_px_s": sum(error > 100 for error in velocity_errors),
        "near_player_known_velocity_matches": len(near_velocity_errors),
        "near_player_velocity_errors_over_100_px_s": sum(
            error > 100 for error in near_velocity_errors
        ),
        "limitations": "Offline audit of action frames only; greedy proximity matches do not prove identity, visibility, or real-time accuracy.",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "run",
        type=Path,
        help="Directory containing report.json and evaluation-audit.jsonl",
    )
    args = parser.parse_args()
    report = json.loads((args.run / "report.json").read_text(encoding="utf-8"))
    audit = [
        json.loads(line)
        for line in (args.run / "evaluation-audit.jsonl")
        .read_text(encoding="utf-8")
        .splitlines()
        if line.strip()
    ]
    print(json.dumps(analyze(report, audit), indent=2))


if __name__ == "__main__":
    main()
