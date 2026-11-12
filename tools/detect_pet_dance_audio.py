"""Scan finalized game audio without consulting arrow timestamps, then evaluate.

One worker, no audio devices/playback/game inputs. All existing Dance recordings
are development data; this scanner does not grant live or training qualification.
"""

from __future__ import annotations

import argparse
from collections import Counter
import json
import math
from pathlib import Path
import time

# This module sets numerical worker counts before importing NumPy.
from analyze_pet_dance_audio import (
    DIRECTIONS,
    describe_sound,
    digest,
    extract_events,
    load_audio,
    number,
    read_json,
    sha256,
)
import numpy as np

STEP_MS = 50.0
SOUND_MS = 220.0
BASELINE_START_MS = 200.0
CONFIRM_SCANS = 2
REARM_SCANS = 3
MIN_SEPARATION_MS = 250.0
MATCH_TOLERANCE_MS = 200.0
FEATURE_VERSION = "spectral-novelty-v2-boundaries"
DETECTOR_VERSION = "continuous-grid-v1"


def load_templates(document):
    """Load only frozen descriptors; no fitting and no held-out outcomes used."""
    if document.get("feature_version") != FEATURE_VERSION:
        raise ValueError("Template feature version does not match")
    rows = document.get("evaluation", {}).get("templates")
    if not isinstance(rows, dict) or set(rows) != DIRECTIONS:
        raise ValueError("Exactly four frozen direction templates are required")
    result = {}
    for label, row in rows.items():
        values = row.get("descriptor")
        if (
            not isinstance(values, list)
            or len(values) != 84
            or any(
                isinstance(x, bool) or not isinstance(x, (int, float)) for x in values
            )
        ):
            raise ValueError("Invalid template descriptor")
        vector = np.array(values, dtype=np.float64)
        if not np.all(np.isfinite(vector)) or np.any(vector < 0):
            raise ValueError("Invalid template values")
        if abs(float(np.linalg.norm(vector)) - 1) > 1e-6:
            raise ValueError("Template must already have unit norm")
        result[label] = vector
    return result


def classify(features, templates):
    if features["status"] != "ok":
        return {"label": "Unknown", "reason": features["status"], "scores": []}
    scores = sorted(
        (
            (float(np.dot(features["descriptor"], vector)), label)
            for label, vector in templates.items()
        ),
        reverse=True,
    )
    best, margin = scores[0][0], scores[0][0] - scores[1][0]
    accepted = best >= 0.8 and margin >= 0.08
    return {
        "label": scores[0][1] if accepted else "Unknown",
        "reason": "template_separated" if accepted else "low_similarity_or_margin",
        "similarity": best,
        "margin": margin,
        "scores": [{"label": label, "similarity": score} for score, label in scores],
    }


class Debouncer:
    """Emit once per accepted region; time/focus-independent audio state only."""

    def __init__(self):
        self.reset()

    def reset(self):
        self.armed = True
        self.pending_label = None
        self.pending_count = 0
        self.pending_start = None
        self.nonaccepted = 0
        self.last_emit = -math.inf
        self.previous_time = None

    def observe(self, scan):
        qpc = number(scan.get("qpc_ms"), "scan time")
        if self.previous_time is not None and qpc <= self.previous_time:
            raise ValueError("Scan times must strictly increase within a segment")
        self.previous_time = qpc
        label = scan["classification"]["label"]
        if label == "Unknown":
            self.pending_label, self.pending_count, self.pending_start = None, 0, None
            self.nonaccepted += 1
            if self.nonaccepted >= REARM_SCANS:
                self.armed = True
            return None
        if label not in DIRECTIONS:
            raise ValueError("Invalid classification label")
        self.nonaccepted = 0
        if not self.armed:
            return None
        if label == self.pending_label:
            self.pending_count += 1
        else:
            self.pending_label, self.pending_count, self.pending_start = label, 1, qpc
        if (
            self.pending_count < CONFIRM_SCANS
            or qpc - self.last_emit < MIN_SEPARATION_MS
        ):
            return None
        self.armed, self.last_emit = False, qpc
        return {
            "label": label,
            "qpc_ms": qpc,
            "first_support_qpc_ms": self.pending_start,
            "available_qpc_ms": qpc + SOUND_MS,
            "similarity": scan["classification"]["similarity"],
            "margin": scan["classification"]["margin"],
        }


def scan_audio(audio, templates):
    """Scan each uninterrupted audio segment on its own fixed grid.

    No video/replay/input events are parameters. Warmup and incomplete tails are
    preserved as unavailable coverage, not invented quiet or negative labels.
    """
    scans, events, coverage, unavailable = [], [], [], []
    previous_end = None
    origin = audio.timing.get("video_capture_origin_ms")
    if origin is not None:
        origin = number(origin, "video clock origin")
    for segment_id, segment in enumerate(audio.segments):
        start, end = segment[0]["qpc_ms"], segment[-1]["end_ms"]
        coverage.append(
            {"segment": segment_id, "start_qpc_ms": start, "end_qpc_ms": end}
        )
        if previous_end is not None and start > previous_end:
            unavailable.append(
                {
                    "start_qpc_ms": previous_end,
                    "end_qpc_ms": start,
                    "reason": "packet_gap_or_invalid_timestamp",
                }
            )
        unavailable.append(
            {
                "start_qpc_ms": start,
                "end_qpc_ms": min(end, start + BASELINE_START_MS),
                "reason": "baseline_warmup",
            }
        )
        state, index = Debouncer(), 0
        while (anchor := start + BASELINE_START_MS + index * STEP_MS) < end:
            index += 1
            if len(scans) >= 5000:
                raise ValueError("Scan count exceeds the bounded 5000-window budget")
            sound, error = audio.window(anchor, anchor + SOUND_MS)
            baseline, baseline_error = audio.window(anchor - 200, anchor - 80)
            if error or baseline_error:
                features = {"status": "unknown", "reason": error or baseline_error}
            else:
                features = describe_sound(sound[0], baseline[0], audio.rate)
            classification = classify(features, templates)
            row = {
                "scan": len(scans) + 1,
                "segment": segment_id,
                "qpc_ms": anchor,
                "video_ms": anchor - origin if origin is not None else None,
                "available_qpc_ms": anchor + SOUND_MS if not error else None,
                "feature_status": features["status"],
                "feature_reason": features.get("reason"),
                "dominant_peak_hz": features.get("dominant_peak_hz"),
                "rms_dbfs": features.get("rms_dbfs"),
                "novelty_fraction": features.get("novelty_fraction"),
                "classification": classification,
            }
            scans.append(row)
            emitted = state.observe(row)
            if emitted:
                emitted.update(
                    event=len(events) + 1,
                    scan=row["scan"],
                    segment=segment_id,
                    video_ms=row["video_ms"],
                )
                events.append(emitted)
        if end - SOUND_MS < end:
            unavailable.append(
                {
                    "start_qpc_ms": max(start, end - SOUND_MS),
                    "end_qpc_ms": end,
                    "reason": "incomplete_future_window",
                }
            )
        previous_end = end
    return {
        "scans": scans,
        "events": events,
        "audio_coverage": coverage,
        "unavailable_intervals": unavailable,
        "feature_status_counts": dict(Counter(row["feature_status"] for row in scans)),
        "scan_label_counts": dict(
            Counter(row["classification"]["label"] for row in scans)
        ),
        "event_label_counts": dict(Counter(row["label"] for row in events)),
    }


def evaluate_events(detections, references, origin, review_start, review_end, scans):
    """Match by time only, never by direction, after detection has completed."""
    unmatched = set(range(len(detections)))
    rows, unscorable = [], []
    for reference in sorted(references, key=lambda r: r["time_ms"]):
        expected = origin + reference["time_ms"]
        nearby_coverage = [
            s
            for s in scans
            if abs(s["qpc_ms"] - expected) <= STEP_MS
            and s["feature_status"] != "unknown"
        ]
        if not nearby_coverage:
            unscorable.append(
                dict(reference, reason="no_valid_audio_window_near_reference")
            )
            continue
        candidates = [
            i
            for i in unmatched
            if abs(detections[i]["qpc_ms"] - expected) <= MATCH_TOLERANCE_MS
        ]
        chosen = (
            min(
                candidates,
                key=lambda i: (
                    abs(detections[i]["qpc_ms"] - expected),
                    detections[i]["qpc_ms"],
                ),
            )
            if candidates
            else None
        )
        event = detections[chosen] if chosen is not None else None
        if chosen is not None:
            unmatched.remove(chosen)
        rows.append(
            {
                "round": reference.get("round"),
                "visual_label": reference["label"],
                "visual_video_ms": reference["time_ms"],
                "detected_label": event["label"] if event else "Missing",
                "event": event["event"] if event else None,
                "correct_direction": (
                    event["label"] == reference["label"] if event else False
                ),
                "anchor_delta_ms": event["qpc_ms"] - expected if event else None,
                "available_delta_ms": (
                    event["available_qpc_ms"] - expected if event else None
                ),
            }
        )
    extras = [
        dict(
            detections[i],
            inside_native_review=review_start
            <= detections[i]["qpc_ms"] - origin
            <= review_end,
        )
        for i in sorted(unmatched)
    ]
    correct = sum(r["correct_direction"] for r in rows)
    reviewed_detections = sum(
        review_start <= e["qpc_ms"] - origin <= review_end for e in detections
    )
    return {
        "matching_tolerance_ms": MATCH_TOLERANCE_MS,
        "matching_rule": "nearest unused event by time, independent of label",
        "reference_cues": len(references),
        "scorable_reference_cues": len(rows),
        "correct_direction_matches": correct,
        "missed_reference_cues": sum(r["detected_label"] == "Missing" for r in rows),
        "wrong_direction_matches": sum(
            r["detected_label"] != "Missing" and not r["correct_direction"]
            for r in rows
        ),
        "total_detected_events": len(detections),
        "unmatched_detections": extras,
        "unmatched_inside_native_review": sum(
            e["inside_native_review"] for e in extras
        ),
        "unmatched_outside_native_review": sum(
            not e["inside_native_review"] for e in extras
        ),
        "reference_precision_full_stream": (
            correct / len(detections) if detections else None
        ),
        "precision_inside_native_review": (
            correct / reviewed_detections if reviewed_detections else None
        ),
        "recall_scorable_references": correct / len(rows) if rows else None,
        "matches": rows,
        "unscorable_references": unscorable,
        "precision_scope": "Conditional on supplied preview reference set. Outside native review, unmatched events are retained explicitly; sparse scene screenshots do not certify every frame.",
        "timing_scope": "Anchor may precede sound because it starts a220ms window. Availability includes lookahead and confirmation; visual/source alignment remains provisional.",
        "development_data": True,
        "untouched_test_claimed": False,
    }


def detect(wav_path, timing_path, template_path):
    started = time.perf_counter()
    timing, template_doc = read_json(timing_path), read_json(template_path)
    audio = load_audio(wav_path, timing)
    templates = load_templates(template_doc)
    scanned = scan_audio(audio, templates)
    return {
        "schema_version": 1,
        "kind": "continuous-pet-dance-audio-development-scan",
        "detector_version": DETECTOR_VERSION,
        "feature_version": FEATURE_VERSION,
        "wav_sha256": digest(timing["wav_sha256"]),
        "audio_timing_sha256": sha256(timing_path),
        "source_video_sha256": (
            digest(timing["video_sha256"]) if timing.get("video_sha256") else None
        ),
        "template_document_sha256": sha256(template_path),
        "video_capture_origin_ms": timing.get("video_capture_origin_ms"),
        "config": {
            "step_ms": STEP_MS,
            "baseline_ms": [-200, -80],
            "sound_ms": [0, SOUND_MS],
            "confirm_scans": CONFIRM_SCANS,
            "rearm_nonaccepted_scans": REARM_SCANS,
            "minimum_separation_ms": MIN_SEPARATION_MS,
            "minimum_similarity": 0.8,
            "minimum_margin": 0.08,
        },
        "input_scope": "Audio and frozen descriptors only; no arrow, input or scene timestamps supplied to scanning.",
        "duration_seconds": len(audio.samples) / audio.rate,
        "scan_elapsed_seconds": time.perf_counter() - started,
        "device_position_qualified": timing.get("device_position_qualified", False),
        **scanned,
        "development_data": True,
        "exact_decoded_video_alignment_verified": False,
        "live_qualified": False,
        "automatic_training_eligible": False,
        "inputs_sent": 0,
        "audio_played": False,
    }


def add_visual_evaluation(report, replay_path, inputs_path):
    """Called only after complete audio detection; cannot choose scan windows."""
    replay, inputs = read_json(replay_path), read_json(inputs_path)
    source = report["source_video_sha256"]
    if (
        digest(replay.get("source_video_sha256")) != source
        or digest(inputs.get("VideoSha256")) != source
        or inputs.get("VideoVerified") is not True
        or inputs.get("Truncated") is not False
    ):
        raise ValueError("Evaluation source identities or input completeness differ")
    origin = number(inputs.get("VideoCaptureOriginMs"), "video origin")
    if (
        abs(origin - number(report.get("video_capture_origin_ms"), "audio origin"))
        > 0.001
    ):
        raise ValueError("Audio and visual evaluation origins differ")
    video = Path(inputs_path).parent / "capture.mp4"
    if video.stat().st_size > 512 * 1024 * 1024 or sha256(video) != source:
        raise ValueError("Evaluation video identity failed")
    observations, rounds, warnings = extract_events(replay, inputs)
    references = [e for e in observations if e["kind"] == "watch_direction"]
    frames = replay["events"]
    if not frames:
        raise ValueError("Visual review has no coverage")
    result = evaluate_events(
        report["events"],
        references,
        origin,
        min(f["time_ms"] for f in frames),
        max(f["time_ms"] for f in frames),
        report["scans"],
    )
    result.update(
        replay_sha256=sha256(replay_path),
        input_log_sha256=sha256(inputs_path),
        reference_warnings=warnings,
        observed_rounds=rounds,
    )
    report["evaluation"] = result
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("wav", type=Path)
    parser.add_argument("timing", type=Path)
    parser.add_argument("templates", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--replay", type=Path)
    parser.add_argument("--inputs", type=Path)
    args = parser.parse_args()
    if bool(args.replay) != bool(args.inputs):
        parser.error("Supply both --replay and --inputs for post-detection evaluation")
    if args.output.exists():
        parser.error("Choose a new output path; previous evidence is never overwritten")
    try:
        report = detect(args.wav, args.timing, args.templates)
        if args.replay:
            add_visual_evaluation(report, args.replay, args.inputs)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        with args.output.open("x", encoding="utf8") as stream:
            json.dump(report, stream, indent=2, allow_nan=False)
            stream.write("\n")
    except (ValueError, OSError) as error:
        parser.exit(2, f"Continuous scan rejected: {error}\n")
    print(
        json.dumps(
            {
                "output": str(args.output),
                "windows": len(report["scans"]),
                "detections": len(report["events"]),
                "elapsed_seconds": report["scan_elapsed_seconds"],
                "evaluation": {
                    k: v
                    for k, v in report.get("evaluation", {}).items()
                    if k
                    in (
                        "correct_direction_matches",
                        "missed_reference_cues",
                        "wrong_direction_matches",
                        "unmatched_inside_native_review",
                        "unmatched_outside_native_review",
                        "reference_precision_full_stream",
                        "recall_scorable_references",
                    )
                },
            }
        )
    )


if __name__ == "__main__":
    main()
