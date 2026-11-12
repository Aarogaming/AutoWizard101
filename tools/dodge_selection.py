"""Resilience-oriented screening without changing final game scores."""

from __future__ import annotations

import math


def screening_score(scores, target=50):
    """Average scores capped at twice target; return None for incomplete input.

    This is a training selection heuristic only. Qualification always uses the
    uncapped mean of every completed game.
    """
    if (
        not isinstance(scores, list)
        or not scores
        or type(target) not in (int, float)
        or not math.isfinite(target)
        or target <= 0
        or any(type(score) is not int or score < 0 for score in scores)
    ):
        return None
    cap = 2 * target
    return sum(min(score, cap) for score in scores) / len(scores)


def rank_screened(rows, target=50):
    """Choose only when every predeclared candidate has complete scores."""
    if not rows:
        return None
    ranked = []
    for row in rows:
        scores = row.get("scores")
        score = screening_score(scores, target)
        if (
            score is None
            or type(row.get("expected_games")) is not int
            or row["expected_games"] != len(scores)
        ):
            return None
        ranked.append((score, min(scores), sum(scores) / len(scores), row))
    # max() would try to compare dictionaries for an exact tie; prefer the
    # first declared candidate instead, preserving reproducibility.
    return max(ranked, key=lambda item: item[:3])[3]
