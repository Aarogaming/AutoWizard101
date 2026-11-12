"""Bounded offline sound/arrow comparisons, never audio capture or game control.

Uses the selected-process PCM16 WAV and its packet QPC mapping. Visual onsets
remain provisional when decoded video has not been aligned to source frames.
Run with --help; the only nonstandard dependency is the existing offline NumPy.
"""

from __future__ import annotations

import argparse
from bisect import bisect_right
from dataclasses import dataclass
import hashlib
import json
import math
import os
from pathlib import Path
import wave

for _thread_variable in (
    "OMP_NUM_THREADS",
    "OPENBLAS_NUM_THREADS",
    "MKL_NUM_THREADS",
    "NUMEXPR_NUM_THREADS",
):
    os.environ[_thread_variable] = "1"

import numpy as np

DIRECTIONS = {"Up", "Down", "Left", "Right"}
MAX_VIDEO_SECONDS = 180
MAX_AUDIO_SECONDS = 210  # Up to 30 seconds of startup before 180 seconds of video.
MAX_EVENTS = 512
CLOCK_TOLERANCE_MS = 2.0


def number(value, name, minimum=0):
    if (
        isinstance(value, bool)
        or not isinstance(value, (int, float))
        or not math.isfinite(value)
        or value < minimum
    ):
        raise ValueError(f"Invalid {name}")
    return float(value)


def integer(value, name, minimum=0):
    if isinstance(value, bool) or not isinstance(value, int) or value < minimum:
        raise ValueError(f"Invalid {name}")
    return value


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def digest(value):
    if (
        not isinstance(value, str)
        or len(value) != 64
        or any(c not in "0123456789abcdefABCDEF" for c in value)
    ):
        raise ValueError("Invalid SHA256")
    return value.upper()


def read_json(path):
    path = Path(path)
    if path.stat().st_size > 24 * 1024 * 1024:
        raise ValueError("JSON exceeds the 24 MiB review limit")
    result = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(result, dict):
        raise ValueError("Expected a JSON object")
    return result


def timed(rows, field, label, limit=20_000, strict=False):
    if not isinstance(rows, list) or len(rows) > limit:
        raise ValueError(f"Invalid {label} list")
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError(f"Invalid {label} entry")
        if number(row.get(field), label + " time") > MAX_VIDEO_SECONDS * 1000:
            raise ValueError(f"{label} exceeds the 180 second review limit")
    result = sorted(rows, key=lambda row: row[field])
    if strict and any(b[field] <= a[field] for a, b in zip(result, result[1:])):
        raise ValueError(f"Duplicate {label} times")
    return result


@dataclass
class Audio:
    samples: np.ndarray
    rate: int
    packets: list
    segments: list
    timing: dict

    def window(self, start_ms, end_ms):
        """Map an absolute-QPC window only inside one uninterrupted packet run."""
        number(start_ms, "window start")
        number(end_ms, "window end")
        if end_ms <= start_ms or end_ms - start_ms > 500:
            raise ValueError("Audio windows must be positive and at most 500 ms")
        for segment in self.segments:
            if segment[0]["qpc_ms"] <= start_ms and end_ms <= segment[-1]["end_ms"]:
                starts = [packet["qpc_ms"] for packet in segment]
                a = segment[max(0, bisect_right(starts, start_ms) - 1)]
                b = segment[max(0, bisect_right(starts, end_ms) - 1)]
                # Even sub-tolerance gaps must not acquire invented samples.
                if start_ms > a["end_ms"] or end_ms > b["end_ms"]:
                    return None, "clock_gap_or_outside_coverage"
                first = a["frame_offset"] + round(
                    (start_ms - a["qpc_ms"]) * self.rate / 1000
                )
                last = b["frame_offset"] + round(
                    (end_ms - b["qpc_ms"]) * self.rate / 1000
                )
                if not 0 <= first < last <= len(self.samples):
                    return None, "invalid_sample_mapping"
                return (self.samples[first:last], first, last), None
        return None, "clock_gap_discontinuity_or_outside_coverage"


def load_audio(wav_path, timing):
    """Validate finalized PCM16 stereo evidence and retain discontinuities."""
    wav_path = Path(wav_path)
    if (
        timing.get("schema_version") != 1
        or timing.get("kind") != "selected-process-audio"
        or timing.get("scope") != "selected-process-tree"
        or timing.get("status") != "complete"
        or timing.get("finalized") is not True
        or timing.get("wav_file") != wav_path.name
    ):
        raise ValueError("A complete finalized selected-process WAV is required")
    if wav_path.stat().st_size > 84 * 1024 * 1024:
        raise ValueError("WAV exceeds the bounded review limit")
    if digest(timing.get("wav_sha256")) != sha256(wav_path):
        raise ValueError("WAV hash does not match audio timing")
    fmt = timing.get("format")
    if not isinstance(fmt, dict):
        raise ValueError("Missing PCM format")
    rate = integer(fmt.get("sample_rate"), "sample rate", 8000)
    if rate > 96000 or fmt.get("channels") != 2 or fmt.get("bits_per_sample") != 16:
        raise ValueError("Expected PCM16 stereo at 8–96 kHz")
    if fmt.get("encoding") != "pcm16":
        raise ValueError("Unsupported audio encoding")
    with wave.open(str(wav_path), "rb") as source:
        if (source.getnchannels(), source.getsampwidth(), source.getframerate()) != (
            2,
            2,
            rate,
        ) or source.getcomptype() != "NONE":
            raise ValueError("WAV header disagrees with audio timing")
        frames = source.getnframes()
        if not 0 < frames <= rate * MAX_AUDIO_SECONDS:
            raise ValueError("WAV duration must be positive and at most 210 seconds")
        raw = source.readframes(frames)
        if len(raw) != frames * 4:
            raise ValueError("Truncated WAV data")
    samples = np.frombuffer(raw, dtype="<i2").reshape(-1, 2)
    rows = timing.get("packets")
    if not isinstance(rows, list) or not 0 < len(rows) <= 40_000:
        raise ValueError("Invalid audio packet list")
    packets, segments, current, expected, previous = [], [], [], 0, None
    integer(timing.get("queue_dropped"), "queue dropped")
    if not isinstance(timing.get("device_position_qualified", False), bool):
        raise ValueError("Invalid device-position qualification flag")
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("Invalid audio packet")
        offset = integer(row.get("frame_offset"), "sample-frame offset")
        count = integer(row.get("frame_count"), "sample-frame count", 1)
        qpc = number(row.get("qpc_ms"), "packet QPC")
        raw_qpc = integer(row.get("qpc_position_100ns"), "packet QPC 100ns")
        integer(row.get("device_position"), "device position")
        # Process-loopback may report zero for every device position. Preserve
        # this raw counter but align through QPC, not inferred device progress.
        flags = integer(row.get("flags"), "packet flags")
        if flags & ~7 or abs(qpc - raw_qpc / 10_000) > 0.001:
            raise ValueError("Inconsistent packet flags or QPC units")
        for field, mask in (
            ("silent", 2),
            ("discontinuity", 1),
            ("timestamp_error", 4),
        ):
            if not isinstance(row.get(field), bool) or row[field] != bool(flags & mask):
                raise ValueError("Packet flag fields disagree")
        if offset != expected or offset + count > frames:
            raise ValueError("Packet offsets must describe the contiguous WAV exactly")
        expected += count
        packet = dict(row, end_ms=qpc + count * 1000 / rate)
        bad = row["discontinuity"] or row["timestamp_error"]
        gap = (
            previous is not None and abs(qpc - previous["end_ms"]) > CLOCK_TOLERANCE_MS
        )
        if previous and not bad and qpc < previous["end_ms"] - CLOCK_TOLERANCE_MS:
            raise ValueError("Audio QPC moved backwards or packet clocks overlap")
        if bad or gap:
            if current:
                segments.append(current)
                current = []
        if not bad:
            current.append(packet)
        if row["silent"] and np.any(samples[offset : offset + count]):
            raise ValueError("Silent packet contains nonzero PCM")
        packets.append(packet)
        previous = None if bad else packet
    if current:
        segments.append(current)
    if expected != frames:
        raise ValueError("Timing metadata does not cover the complete WAV")
    usable = [p for p in packets if not p["timestamp_error"]]
    if usable and usable[-1]["end_ms"] - usable[0]["qpc_ms"] > MAX_AUDIO_SECONDS * 1000:
        raise ValueError("Audio QPC span exceeds 210 seconds")
    return Audio(samples, rate, packets, segments, timing)


def extract_events(replay, inputs):
    """Retain committed Watch cues, response presses, phases and results separately.

    Native Cues list growth confirms stability. An onset is backtracked only
    through contiguous full-arrow frames; fades and held repeats add no events.
    """
    if replay.get("schema_version") != 1:
        raise ValueError("Unsupported replay")
    frames = timed(replay.get("events"), "time_ms", "visual", strict=True)
    proposals = timed(replay.get("proposals"), "time_ms", "proposal", strict=True)
    outcomes = timed(replay.get("outcomes"), "time_ms", "outcome", strict=True)
    keys = timed(inputs.get("Events"), "TimeMs", "input")
    events, rounds, warnings = [], [], []
    previous_go = -1
    for index, proposal in enumerate(proposals):
        actions = proposal.get("actions")
        if (
            not isinstance(actions, list)
            or not 3 <= len(actions) <= 7
            or any(a not in {"dance-" + d.lower() for d in DIRECTIONS} for a in actions)
            or not isinstance(proposal.get("dispatched"), bool)
        ):
            raise ValueError("Invalid directional proposal")
        sequence = [a.removeprefix("dance-").title() for a in actions]
        go, round_id = proposal["time_ms"], index + 1
        boundary = (
            proposals[index + 1]["time_ms"]
            if index + 1 < len(proposals)
            else MAX_VIDEO_SECONDS * 1000
        )
        found = [o for o in outcomes if go < o["time_ms"] < boundary]
        result = found[0] if len(found) == 1 else None
        if any(o.get("outcome") not in ("Success", "Failure") for o in found):
            raise ValueError("Unknown outcome label")
        rounds.append(
            {
                "round": round_id,
                "go_ms": go,
                "result_ms": result["time_ms"] if result else None,
                "outcome": result["outcome"] if result else "Unknown",
                "status": "complete" if result else "incomplete_or_ambiguous",
            }
        )
        selected = [f for f in frames if previous_go < f["time_ms"] <= go]
        counted, run, last_time, accepted_run_start = 0, [], None, None
        for frame in selected:
            cue, phase, time = frame.get("cue"), frame.get("phase"), frame["time_ms"]
            if (
                phase != "Watch"
                or cue not in DIRECTIONS
                or frame.get("Rejected") is not False
                or (last_time is not None and time - last_time > 100)
                or (run and cue != run[-1]["cue"])
            ):
                run = []
            if (
                phase == "Watch"
                and cue in DIRECTIONS
                and frame.get("Rejected") is False
            ):
                run.append(frame)
                committed = frame.get("Cues")
                if not isinstance(committed, list) or any(
                    not isinstance(c, str) for c in committed
                ):
                    raise ValueError("Missing native committed cue evidence")
                normalized = [c.title() for c in committed]
                if len(normalized) > counted:
                    valid = (
                        len(normalized) == counted + 1
                        and normalized == sequence[: len(normalized)]
                        and normalized[-1] == cue
                        and len(run) >= 2
                        and time - run[0]["time_ms"] >= 50
                        and run[0]["time_ms"] != accepted_run_start
                    )
                    if valid:
                        events.append(
                            {
                                "kind": "watch_direction",
                                "label": cue,
                                "round": round_id,
                                "time_ms": run[0]["time_ms"],
                                "confirmed_ms": time,
                                "clock": "provisional_decoded_video",
                                "evidence": "full arrow run confirmed by native Cues growth",
                            }
                        )
                        accepted_run_start = run[0]["time_ms"]
                    else:
                        warnings.append(
                            f"Round {round_id}: cue {len(normalized)} lacks an unambiguous stable onset"
                        )
                    counted = len(normalized)
            last_time = time
        if counted != len(sequence):
            warnings.append(f"Round {round_id}: incomplete Watch onset evidence")
        events.append(
            {
                "kind": "phase",
                "label": "Go",
                "round": round_id,
                "time_ms": go,
                "clock": "provisional_decoded_video",
            }
        )
        if result:
            events.append(
                {
                    "kind": "result",
                    "label": result["outcome"],
                    "round": round_id,
                    "time_ms": result["time_ms"],
                    "clock": "provisional_decoded_video",
                }
            )
        previous_go = go
    focused, held = False, set()
    for key in keys:
        kind, control, time = key.get("Type"), key.get("Control"), key["TimeMs"]
        if kind == "focus_gained":
            focused = True
        elif kind == "focus_lost":
            focused = False
            held.clear()
        elif kind in ("key_down", "key_up") and control in DIRECTIONS:
            if not isinstance(key.get("Injected"), bool):
                raise ValueError("Missing explicit injected-input provenance")
            if kind == "key_up":
                held.discard(control)
                continue
            if control in held:
                continue
            held.add(control)
            matching = [
                r for r in rounds if r["go_ms"] <= time < (r["result_ms"] or r["go_ms"])
            ]
            if not focused or key["Injected"] or len(matching) != 1:
                warnings.append(
                    "Directional key excluded: focus, injection or round boundary is uncertain"
                )
                continue
            events.append(
                {
                    "kind": "response_key",
                    "label": control,
                    "round": matching[0]["round"],
                    "time_ms": time,
                    "clock": "input_qpc",
                    "evidence": "focused non-injected key-down; actor requires session annotation",
                }
            )
    previous_phase = None
    for frame in frames:
        phase = frame.get("phase")
        if phase != previous_phase and phase in (
            "Watch",
            "Loading",
            "Unknown",
            "Resolving",
        ):
            events.append(
                {
                    "kind": "scene_phase",
                    "label": phase,
                    "round": None,
                    "time_ms": frame["time_ms"],
                    "clock": "provisional_decoded_video",
                }
            )
        previous_phase = phase
    if len(events) > MAX_EVENTS:
        raise ValueError("Event count exceeds the 512-event offline budget")
    return sorted(events, key=lambda e: (e["time_ms"], e["kind"])), rounds, warnings


def spectrum(samples, rate):
    """Channel-power average, never a phase-cancelling stereo downmix."""
    values = samples.astype(np.float64) / 32768.0
    values -= values.mean(axis=0, keepdims=True)
    # Compare baseline and event with the same spectral resolution. Otherwise a
    # shorter baseline broadens a steady note and fabricates spectral novelty.
    width = min(2048, round(rate * 0.08), len(values))
    if width < 128:
        raise ValueError("Insufficient samples for spectral analysis")
    window = np.hanning(width)
    power = np.zeros(4097)
    starts = list(range(0, len(values) - width + 1, max(1, width // 2)))
    for start in starts:
        transformed = np.fft.rfft(
            values[start : start + width] * window[:, None], n=8192, axis=0
        )
        power += np.mean(np.abs(transformed) ** 2, axis=1) / np.sum(window**2)
    power /= len(starts)
    frequencies = np.fft.rfftfreq(8192, 1 / rate)
    return frequencies, power


def describe_sound(samples, baseline, rate):
    """Descriptive pitch/spectrum only; multiple peaks may be music or harmonics."""
    values = samples.astype(np.float64) / 32768
    rms = float(np.sqrt(np.mean(values**2)))
    base_rms = float(np.sqrt(np.mean((baseline.astype(np.float64) / 32768) ** 2)))
    frequencies, power = spectrum(samples, rate)
    _, base_power = spectrum(baseline, rate)
    band = (frequencies >= 80) & (frequencies <= min(8000, rate / 2))
    unfiltered_peak = int(np.argmax(power))
    outside_band = not bool(band[unfiltered_peak])
    power = np.where(band, power, 0)
    base_power = np.where(band, base_power, 0)
    total = float(power.sum())
    novelty = np.maximum(power - base_power, 0)
    novelty_fraction = min(1.0, float(novelty.sum()) / max(total, 1e-20))
    norm = float(np.linalg.norm(power) * np.linalg.norm(base_power))
    background_similarity = float(np.dot(power, base_power) / norm) if norm else 0.0
    peak = int(np.argmax(power))
    peak_hz = float(frequencies[peak]) if total > 1e-15 else None
    band_bins = np.flatnonzero(band)
    boundary_peak = peak in (int(band_bins[0]), int(band_bins[-1]))
    local = [
        i
        for i in range(1, len(power) - 1)
        if power[i] > power[i - 1] and power[i] >= power[i + 1]
    ]
    local.sort(key=lambda i: power[i], reverse=True)
    peaks = []
    for i in local:
        hz = float(frequencies[i])
        if power[i] < power[peak] * 0.25:
            break
        if all(abs(hz - p["hz"]) >= 40 for p in peaks):
            peaks.append({"hz": hz, "relative_power": float(power[i] / power[peak])})
        if len(peaks) == 4:
            break
    ambiguous = False
    if peak_hz:
        for other in peaks[1:]:
            ratio = max(peak_hz, other["hz"]) / min(peak_hz, other["hz"])
            if other["relative_power"] >= 0.45 and abs(ratio - round(ratio)) > 0.04:
                ambiguous = True
    edges = np.geomspace(80, min(8000, rate / 2), 85)
    descriptor = np.array(
        [
            np.sum(novelty[(frequencies >= a) & (frequencies < b)])
            for a, b in zip(edges, edges[1:])
        ]
    )
    descriptor = np.sqrt(descriptor)
    length = float(np.linalg.norm(descriptor))
    if length:
        descriptor /= length
    status = "ok"
    if rms < 1e-4:
        status = "silence"
    elif outside_band or boundary_peak:
        # Removing low bass can leave a spectral skirt peaking at the cutoff;
        # that edge is not evidence of a new note at the cutoff frequency.
        status = "out_of_band_or_boundary_peak"
    elif ambiguous:
        status = "mixed_or_harmonic_ambiguity"
    elif novelty_fraction < 0.15 and background_similarity > 0.9:
        status = "no_local_change_music_or_held_sound"
    return {
        "status": status,
        "rms_dbfs": 20 * math.log10(max(rms, 1e-10)),
        "baseline_rms_dbfs": 20 * math.log10(max(base_rms, 1e-10)),
        "dominant_peak_hz": peak_hz,
        "unfiltered_dominant_peak_hz": float(frequencies[unfiltered_peak]),
        "dominant_energy_outside_analysis_band": outside_band,
        "peak_at_analysis_boundary": boundary_peak,
        "spectral_peaks": peaks,
        "novelty_fraction": novelty_fraction,
        "background_similarity": background_similarity,
        "descriptor": descriptor.tolist(),
        "pitch_is_fundamental_verified": False,
    }


def analyze_events(audio, events, video_origin_ms):
    analyzed = []
    for index, event in enumerate(events):
        row = dict(event, event_id=index + 1)
        qpc = video_origin_ms + event["time_ms"]
        row["qpc_ms"] = qpc
        post, error = audio.window(qpc, qpc + 220)
        baseline, base_error = (
            audio.window(qpc - 200, qpc - 80)
            if qpc >= 200
            else (None, "outside_coverage")
        )
        if error or base_error:
            row.update(status="unknown", reason=error or base_error)
        else:
            row["features"] = describe_sound(post[0], baseline[0], audio.rate)
            row["status"] = row["features"]["status"]
            row["snippet"] = {
                "first_sample_frame": post[1],
                "end_sample_frame": post[2],
                "sample_rate": audio.rate,
                "channels": 2,
                "duration_ms": (post[2] - post[1]) * 1000 / audio.rate,
            }
        analyzed.append(row)
    return analyzed


def evaluate_groups(events, calibration_rounds, heldout_rounds, known_rounds):
    """Fit Watch templates only; split whole rounds before fitting any feature."""
    for round_id in [*calibration_rounds, *heldout_rounds]:
        integer(round_id, "round number", 1)
    calibration, heldout = set(calibration_rounds), set(heldout_rounds)
    if calibration & heldout:
        raise ValueError("Calibration and held-out rounds must be disjoint")
    if not (calibration | heldout) <= set(known_rounds):
        raise ValueError("Requested split contains an unknown round")
    result = {
        "calibration_rounds": sorted(calibration),
        "heldout_rounds": sorted(heldout),
        "split_unit": "whole round",
        "scope": "Within-session exploratory evaluation; not cross-session accuracy",
        "general_accuracy_claimed": False,
        "templates": {},
        "evaluations": [],
    }
    if not calibration or not heldout:
        result["status"] = "descriptive_only_explicit_round_split_required"
        return result
    templates = {}
    for direction in sorted(DIRECTIONS):
        rows = [
            e
            for e in events
            if e["round"] in calibration
            and e["kind"] == "watch_direction"
            and e["label"] == direction
            and e["status"] == "ok"
        ]
        if rows:
            vector = np.mean([e["features"]["descriptor"] for e in rows], axis=0)
            length = float(np.linalg.norm(vector))
            if length:
                templates[direction] = vector / length
                result["templates"][direction] = {
                    "events": [e["event_id"] for e in rows],
                    "rounds": sorted({e["round"] for e in rows}),
                    "count": len(rows),
                    "descriptor": templates[direction].tolist(),
                }
    result["missing_direction_templates"] = sorted(DIRECTIONS - set(templates))
    result["status"] = (
        "evaluated" if len(templates) == 4 else "insufficient_direction_coverage"
    )
    for row in events:
        if row["round"] not in heldout or row["kind"] not in (
            "watch_direction",
            "response_key",
        ):
            continue
        scores = []
        prediction, reason = "Unknown", row["status"]
        if row["status"] == "ok" and len(templates) == 4:
            scores = sorted(
                (
                    (float(np.dot(row["features"]["descriptor"], v)), label)
                    for label, v in templates.items()
                ),
                reverse=True,
            )
            if scores[0][0] >= 0.8 and scores[0][0] - scores[1][0] >= 0.08:
                prediction, reason = scores[0][1], "template_separated"
            else:
                reason = "low_similarity_or_ambiguous_templates"
        elif row["status"] == "ok":
            reason = "incomplete_direction_templates"
        result["evaluations"].append(
            {
                "event_id": row["event_id"],
                "round": row["round"],
                "kind": row["kind"],
                "observed_label": row["label"],
                "audio_prediction": prediction,
                "reason": reason,
                "agrees": (
                    prediction == row["label"] if prediction != "Unknown" else None
                ),
                "scores": [
                    {"label": label, "similarity": score} for score, label in scores
                ],
            }
        )
    result["counts_by_kind"] = {
        kind: {
            "total": sum(e["kind"] == kind for e in result["evaluations"]),
            "agrees": sum(
                e["kind"] == kind and e["agrees"] is True for e in result["evaluations"]
            ),
            "differs": sum(
                e["kind"] == kind and e["agrees"] is False
                for e in result["evaluations"]
            ),
            "unknown": sum(
                e["kind"] == kind and e["agrees"] is None for e in result["evaluations"]
            ),
        }
        for kind in ("watch_direction", "response_key")
    }
    return result


def analyze(wav_path, timing_path, replay_path, input_path, calibration=(), heldout=()):
    timing, replay, inputs = (
        read_json(p) for p in (timing_path, replay_path, input_path)
    )
    audio = load_audio(wav_path, timing)
    if (
        inputs.get("Version") != 1
        or inputs.get("Kind") != "observed-game-input"
        or inputs.get("VideoVerified") is not True
        or inputs.get("Truncated") is not False
    ):
        raise ValueError("A verified, nontruncated game input log is required")
    if timing.get("video_verified") is not True:
        raise ValueError("Audio must be finalized against a verified video")
    video_hash = digest(inputs.get("VideoSha256"))
    if (
        digest(timing.get("video_sha256")) != video_hash
        or digest(replay.get("source_video_sha256")) != video_hash
    ):
        raise ValueError("Audio, replay and input log must reference the same video")
    origin = number(inputs.get("VideoCaptureOriginMs"), "video origin")
    end = number(inputs.get("CaptureEndMs"), "video end")
    if not 0 < end - origin <= MAX_VIDEO_SECONDS * 1000:
        raise ValueError("Video input coverage must be at most 180 seconds")
    if (
        abs(
            number(timing.get("video_capture_origin_ms"), "audio video origin") - origin
        )
        > 0.001
        or abs(number(timing.get("video_capture_end_ms"), "audio video end") - end)
        > 0.001
    ):
        raise ValueError("Audio and input clocks do not share the recorded origin/end")
    for a, b in (
        ("target_process_id", "TargetProcessId"),
        ("target_process_start_utc_ticks", "TargetProcessStartUtcTicks"),
    ):
        if integer(timing.get(a), a, 1) != integer(inputs.get(b), b, 1):
            raise ValueError("Audio and input target identities differ")
    if inputs.get("VideoFile") != "capture.mp4":
        raise ValueError("Unexpected source video filename")
    source_video = Path(input_path).parent / "capture.mp4"
    if (
        source_video.stat().st_size > 512 * 1024 * 1024
        or sha256(source_video) != video_hash
    ):
        raise ValueError("Source video hash or bounded size check failed")
    allowlist = inputs.get("KeyAllowlist")
    if not isinstance(allowlist, list) or any(
        not isinstance(key, str) for key in allowlist
    ):
        raise ValueError("Missing game input allowlist")
    for key in timed(inputs.get("Events"), "TimeMs", "input"):
        if key["TimeMs"] > end - origin:
            raise ValueError("Input event is outside recorded coverage")
        if key.get("Control") in DIRECTIONS and key.get("Control") not in allowlist:
            raise ValueError("Directional input is not in the recorded allowlist")
    if any(
        frame["time_ms"] > end - origin
        for frame in timed(replay.get("events"), "time_ms", "visual")
    ):
        raise ValueError("Replay frame is outside recorded coverage")
    events, rounds, warnings = extract_events(replay, inputs)
    if any(e["time_ms"] > end - origin for e in events):
        raise ValueError("Event is outside recorded video/input coverage")
    rows = analyze_events(audio, events, origin)
    evaluation = evaluate_groups(
        rows, calibration, heldout, [r["round"] for r in rounds]
    )
    return {
        "schema_version": 1,
        "kind": "offline-pet-dance-audio-comparison",
        "source_video_sha256": video_hash,
        "wav_sha256": digest(timing["wav_sha256"]),
        "audio_timing_sha256": sha256(timing_path),
        "replay_sha256": sha256(replay_path),
        "input_log_sha256": sha256(input_path),
        "audio_format": timing["format"],
        "audio_duration_seconds": len(audio.samples) / audio.rate,
        "packet_count": len(audio.packets),
        "device_position_qualified": timing.get("device_position_qualified", False),
        "audio_alignment_clock": "packet QPC; raw device positions are not used for alignment",
        "usable_clock_segments": len(audio.segments),
        "queue_dropped": timing["queue_dropped"],
        "video_capture_origin_ms": origin,
        "exact_decoded_video_alignment_verified": False,
        "clock_scope": "Packet QPC maps PCM to input QPC. Visual labels use shared origin plus decoded time; source-frame alignment and audio latency remain unqualified.",
        "feature_version": "spectral-novelty-v2-boundaries",
        "feature_windows_ms": {"baseline": [-200, -80], "sound": [0, 220]},
        "clock_join_tolerance_ms": CLOCK_TOLERANCE_MS,
        "rounds": rounds,
        "events": rows,
        "warnings": warnings,
        "evaluation": evaluation,
        "automatic_training_eligible": False,
        "live_qualified": False,
        "inputs_sent": 0,
    }


def round_list(value):
    try:
        values = [int(item) for item in value.split(",") if item]
    except ValueError as exc:
        raise argparse.ArgumentTypeError(
            "Use comma-separated positive round numbers"
        ) from exc
    if any(i < 1 for i in values) or len(set(values)) != len(values):
        raise argparse.ArgumentTypeError("Round numbers must be unique and positive")
    return values


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("wav", type=Path)
    parser.add_argument("timing", type=Path)
    parser.add_argument("replay", type=Path)
    parser.add_argument("inputs", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--calibration-rounds", type=round_list, default=[])
    parser.add_argument("--heldout-rounds", type=round_list, default=[])
    args = parser.parse_args()
    if args.output.exists():
        parser.error(
            "Output already exists; preserve previous evidence with a new filename"
        )
    try:
        report = analyze(
            args.wav,
            args.timing,
            args.replay,
            args.inputs,
            args.calibration_rounds,
            args.heldout_rounds,
        )
        args.output.parent.mkdir(parents=True, exist_ok=True)
        with args.output.open("x", encoding="utf8") as stream:
            json.dump(report, stream, indent=2, allow_nan=False)
            stream.write("\n")
    except (ValueError, OSError, wave.Error) as exc:
        parser.exit(2, f"Audio analysis rejected: {exc}\n")
    print(
        json.dumps(
            {
                "output": str(args.output),
                "events": len(report["events"]),
                "evaluation": report["evaluation"]["status"],
                "live_qualified": False,
            }
        )
    )


if __name__ == "__main__":
    main()
