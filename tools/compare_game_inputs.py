"""Compare predicted button presses with an opt-in Maelstrom input recording.

Prediction JSON: video_sha256, coverage_start_ms, coverage_end_ms, and events
with time_ms, type (key_down or mouse_down), and control. Suggestions are
untrusted predictions; this program never sends or replays inputs.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from collections import defaultdict
from pathlib import Path


def _number(value, name):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError(f"Invalid {name}")
    return float(value)


def _hash_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def compare(input_path, predictions_path, tolerance_ms=150, include_injected=False):
    input_path, predictions_path = Path(input_path), Path(predictions_path)
    truth = json.loads(input_path.read_text(encoding="utf-8-sig"))
    guesses = json.loads(predictions_path.read_text(encoding="utf-8-sig"))
    tolerance_ms = _number(tolerance_ms, "tolerance")
    if not 0 <= tolerance_ms <= 1000:
        raise ValueError("Tolerance must be between 0 and 1000 ms")
    if truth.get("Version") != 1 or truth.get("Kind") != "observed-game-input" or not truth.get("VideoVerified") or truth.get("Truncated"):
        raise ValueError("A complete, verified version-1 input log is required")
    video_name = truth.get("VideoFile")
    if video_name != "capture.mp4":
        raise ValueError("Unexpected video file name")
    digest = truth.get("VideoSha256", "").upper()
    if len(digest) != 64 or digest != _hash_file(input_path.parent / video_name):
        raise ValueError("The video does not match its input log")
    if guesses.get("video_sha256", "").upper() != digest:
        raise ValueError("Predictions must be bound to the same video SHA-256")
    if "model_output_status" in guesses and guesses["model_output_status"] != "complete":
        raise ValueError("Model output is incomplete or invalid; it cannot be scored as accuracy")
    start = _number(guesses.get("coverage_start_ms"), "prediction start")
    end = _number(guesses.get("coverage_end_ms"), "prediction end")
    duration = _number(truth.get("CaptureEndMs"), "capture end") - _number(truth.get("VideoCaptureOriginMs"), "capture origin")
    if start < 0 or end <= start or end > duration:
        raise ValueError("Prediction coverage must lie inside the captured video interval")

    allowed = {("key_down", k) for k in truth.get("KeyAllowlist", [])}
    allowed.update(("mouse_down", k) for k in ("Left", "Right", "Middle"))
    events = sorted(truth.get("Events", []), key=lambda e: _number(e.get("TimeMs"), "input time"))
    focused = False
    focus_start = None
    focus_ranges = []
    actual, injected = defaultdict(list), []
    for event in events:
        time = _number(event.get("TimeMs"), "input time")
        kind, control = event.get("Type"), event.get("Control")
        if kind == "focus_gained" and not focused:
            focus_start, focused = time, True
        elif kind == "focus_lost" and focused:
            focus_ranges.append((focus_start, time))
            focused = False
        elif focused and start <= time < end and (kind, control) in allowed:
            if event.get("Injected", False) and not include_injected:
                injected.append((time, kind, control))
            else:
                actual[(kind, control)].append(time)
    if focused:
        focus_ranges.append((focus_start, duration))

    def in_focus(time):
        return any(left <= time < right for left, right in focus_ranges)

    predicted, excluded = defaultdict(list), 0
    for event in guesses.get("events", []):
        time = _number(event.get("time_ms"), "prediction time")
        key = (event.get("type"), event.get("control"))
        # An injected input is not human ground truth; don't score guesses near it.
        near_injected = any(k == key[0] and c == key[1] and abs(t - time) <= tolerance_ms for t, k, c in injected)
        if not start <= time < end or not in_focus(time) or key not in allowed or near_injected:
            excluded += 1
            continue
        predicted[key].append(time)

    matched, false_positive, missed = 0, 0, 0
    errors = []
    for key in actual.keys() | predicted.keys():
        observed, proposed = sorted(actual[key]), sorted(predicted[key])
        i = j = 0
        while i < len(observed) and j < len(proposed):
            delta = proposed[j] - observed[i]
            if abs(delta) <= tolerance_ms:
                matched += 1
                errors.append(abs(delta))
                i += 1
                j += 1
            elif delta < -tolerance_ms:
                false_positive += 1
                j += 1
            else:
                missed += 1
                i += 1
        missed += len(observed) - i
        false_positive += len(proposed) - j
    return {
        "video_sha256": digest, "coverage_ms": [start, end], "tolerance_ms": tolerance_ms,
        "matched_presses": matched, "unmatched_predicted_presses": false_positive,
        "missed_observed_presses": missed, "excluded_predictions": excluded,
        "precision": matched / (matched + false_positive) if matched + false_positive else None,
        "recall": matched / (matched + missed) if matched + missed else None,
        "mean_absolute_timing_error_ms": sum(errors) / len(errors) if errors else None,
        "includes_automated_inputs": bool(include_injected),
        "scope": ("Focused key-down and mouse-button-down events, including explicitly selected automated inputs; not motion, releases or gameplay success."
                  if include_injected else "Focused, non-injected key-down and mouse-button-down events only; not motion, releases or gameplay success."),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input_log", type=Path)
    parser.add_argument("predictions", type=Path)
    parser.add_argument("--tolerance-ms", type=float, default=150)
    parser.add_argument("--include-injected", action="store_true", help="Include automated test inputs; results are not human-demonstration accuracy")
    args = parser.parse_args()
    print(json.dumps(compare(args.input_log, args.predictions, args.tolerance_ms, args.include_injected), indent=2))


if __name__ == "__main__":
    main()
