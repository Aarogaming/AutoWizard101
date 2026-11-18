import copy
import hashlib
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
import wave

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
try:
    import numpy as np
    from analyze_pet_dance_audio import (
        analyze,
        analyze_events,
        describe_sound,
        evaluate_groups,
        extract_events,
        load_audio,
    )
except ImportError:
    np = None
    analyze = analyze_events = describe_sound = None
    evaluate_groups = extract_events = load_audio = None

NUMPY_REQUIRED = unittest.skipIf(
    np is None, "Existing offline NumPy environment required"
)


def tone(frequency, duration=0.22, rate=8000, amplitude=0.5):
    values = amplitude * np.sin(
        2 * np.pi * frequency * np.arange(round(duration * rate)) / rate
    )
    pcm = np.rint(values * 32767).astype("<i2")
    return np.column_stack([pcm, pcm])


def frame(time, cue, committed, phase="Watch"):
    return {
        "time_ms": time,
        "phase": phase,
        "cue": cue,
        "Cues": committed,
        "Rejected": False,
        "fade_of": "Unknown",
    }


def example_round(start, sequence):
    frames, committed = [], []
    for index, direction in enumerate(sequence):
        time = start + index * 500
        frames.append(frame(time, direction, committed.copy()))
        committed.append(direction.lower())
        frames.append(frame(time + 100, direction, committed.copy()))
        frames.append(frame(time + 150, direction, committed.copy()))
        frames.append(frame(time + 200, "Fading", committed.copy()))
        frames.append(frame(time + 300, "Neutral", committed.copy()))
    go = start + len(sequence) * 500
    frames.append(frame(go, "Neutral", committed.copy(), "Go"))
    return frames, {
        "time_ms": go,
        "actions": ["dance-" + d.lower() for d in sequence],
        "dispatched": False,
    }


@NUMPY_REQUIRED
class AudioFixture(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.rate = 8000
        self.origin = 1_000_000.0
        self.samples = np.zeros((self.rate * 8, 2), dtype="<i2")
        self.video = self.root / "capture.mp4"
        self.video.write_bytes(b"bounded synthetic video identity; not actual media")
        self.video_hash = hashlib.sha256(self.video.read_bytes()).hexdigest().upper()
        self.timing = {
            "schema_version": 1,
            "kind": "selected-process-audio",
            "scope": "selected-process-tree",
            "status": "complete",
            "finalized": True,
            "wav_file": "game-audio.wav",
            "format": {
                "sample_rate": self.rate,
                "channels": 2,
                "bits_per_sample": 16,
                "encoding": "pcm16",
            },
            "queue_dropped": 0,
            "target_process_id": 123,
            "target_process_start_utc_ticks": 456,
            "video_verified": True,
            "video_sha256": self.video_hash,
            "video_capture_origin_ms": self.origin,
            "video_capture_end_ms": self.origin + 7800,
            "exact_decoded_video_alignment_verified": False,
        }
        self.inputs = {
            "Version": 1,
            "Kind": "observed-game-input",
            "VideoVerified": True,
            "Truncated": False,
            "VideoFile": "capture.mp4",
            "VideoSha256": self.video_hash,
            "VideoCaptureOriginMs": self.origin,
            "CaptureEndMs": self.origin + 7800,
            "TargetProcessId": 123,
            "TargetProcessStartUtcTicks": 456,
            "KeyAllowlist": ["Up", "Down", "Left", "Right"],
            "Events": [{"TimeMs": 0, "Type": "focus_gained"}],
        }
        self.replay = {
            "schema_version": 1,
            "source_video_sha256": self.video_hash,
            "events": [],
            "proposals": [],
            "outcomes": [],
        }
        self.make_packets()

    def make_packets(self):
        # Audio starts 200ms before video; concatenated WAV index zero is NOT video zero.
        start = self.origin - 200
        count = self.rate // 10
        self.timing["packets"] = [
            {
                "frame_offset": offset,
                "frame_count": count,
                "qpc_position_100ns": round(
                    (start + offset * 1000 / self.rate) * 10_000
                ),
                "qpc_ms": start + offset * 1000 / self.rate,
                "device_position": offset,
                "flags": 0,
                "silent": False,
                "discontinuity": False,
                "timestamp_error": False,
            }
            for offset in range(0, len(self.samples), count)
        ]

    def save(self):
        wav_path = self.root / "game-audio.wav"
        with wave.open(str(wav_path), "wb") as out:
            out.setnchannels(2)
            out.setsampwidth(2)
            out.setframerate(self.rate)
            out.writeframes(self.samples.tobytes())
        self.timing["wav_sha256"] = (
            hashlib.sha256(wav_path.read_bytes()).hexdigest().upper()
        )
        for name, value in (
            ("audio-timing.json", self.timing),
            ("replay.json", self.replay),
            ("input-events.json", self.inputs),
        ):
            (self.root / name).write_text(json.dumps(value), encoding="utf8")
        return wav_path

    def load(self):
        return load_audio(self.save(), self.timing)

    def run_analysis(self, calibration=(), heldout=()):
        self.save()
        return analyze(
            self.root / "game-audio.wav",
            self.root / "audio-timing.json",
            self.root / "replay.json",
            self.root / "input-events.json",
            calibration,
            heldout,
        )

    def add_round(self, start=500, sequence=("Up", "Down", "Right"), outcome="Success"):
        frames, proposal = example_round(start, sequence)
        self.replay["events"].extend(frames)
        self.replay["proposals"].append(proposal)
        self.replay["outcomes"].append(
            {"time_ms": proposal["time_ms"] + 1000, "outcome": outcome}
        )
        return proposal["time_ms"]

    def press(self, time, direction, injected=False):
        self.inputs["Events"].extend(
            [
                {
                    "TimeMs": time,
                    "Type": "key_down",
                    "Control": direction,
                    "Injected": injected,
                },
                {
                    "TimeMs": time + 50,
                    "Type": "key_up",
                    "Control": direction,
                    "Injected": injected,
                },
            ]
        )

    def test_absolute_qpc_offset_selects_correct_audio(self):
        first = round((500 + 200) * self.rate / 1000)
        self.samples[first : first + 1760] = tone(440)
        audio = self.load()
        result = analyze_events(
            audio,
            [
                {
                    "kind": "watch_direction",
                    "label": "Up",
                    "round": 1,
                    "time_ms": 500,
                    "clock": "provisional_decoded_video",
                }
            ],
            self.origin,
        )[0]
        self.assertEqual(result["snippet"]["first_sample_frame"], first)
        self.assertAlmostEqual(result["features"]["dominant_peak_hz"], 440, delta=5)
        self.assertEqual(result["status"], "ok")

    def test_zero_device_positions_with_advancing_qpc_are_usable(self):
        self.timing["device_position_qualified"] = False
        for packet in self.timing["packets"]:
            packet["device_position"] = 0
        audio = self.load()
        window, error = audio.window(self.origin + 500, self.origin + 720)
        self.assertIsNone(error)
        self.assertEqual(window[1], round(700 * self.rate / 1000))
        report = self.run_analysis()
        self.assertFalse(report["device_position_qualified"])
        self.assertIn("packet QPC", report["audio_alignment_clock"])

    def test_210_second_audio_accepts_30_second_startup_preroll(self):
        self.samples = np.zeros((self.rate * 210, 2), dtype="<i2")
        self.make_packets()
        for packet in self.timing["packets"]:
            packet["qpc_ms"] -= 29_800
            packet["qpc_position_100ns"] -= 298_000_000
        self.inputs["CaptureEndMs"] = self.origin + 180_000
        self.timing["video_capture_end_ms"] = self.origin + 180_000
        result = self.run_analysis()
        self.assertEqual(result["audio_duration_seconds"], 210)
        self.assertEqual(result["usable_clock_segments"], 1)
        self.assertEqual(self.timing["packets"][0]["qpc_ms"], self.origin - 30_000)
        self.assertEqual(result["events"], [])

    def test_video_domain_stays_limited_to_180_seconds(self):
        self.inputs["CaptureEndMs"] = self.origin + 181_000
        self.timing["video_capture_end_ms"] = self.origin + 181_000
        with self.assertRaisesRegex(ValueError, "Video input coverage.*180"):
            self.run_analysis()
        self.replay["events"] = [frame(181_000, "Up", [])]
        with self.assertRaisesRegex(ValueError, "180 second review limit"):
            extract_events(self.replay, self.inputs)

    def test_qpc_gap_is_not_repaired_using_wav_positions(self):
        for packet in self.timing["packets"][10:]:
            packet["qpc_ms"] += 500
            packet["qpc_position_100ns"] += 5_000_000
        audio = self.load()
        window, reason = audio.window(self.origin + 750, self.origin + 950)
        self.assertIsNone(window)
        self.assertIn("gap", reason)
        later, reason = audio.window(self.origin + 1400, self.origin + 1500)
        self.assertIsNone(reason)
        self.assertEqual(later[1], round(1100 * self.rate / 1000))

    def test_discontinuity_and_timestamp_error_windows_are_unknown(self):
        for flag, field in ((1, "discontinuity"), (4, "timestamp_error")):
            with self.subTest(flag=flag):
                self.make_packets()
                self.timing["packets"][10]["flags"] = flag
                self.timing["packets"][10][field] = True
                audio = self.load()
                window, reason = audio.window(self.origin + 750, self.origin + 950)
                self.assertIsNone(window)
                self.assertIn("discontinuity", reason)

    def test_silent_audio_is_distinct_from_missing_coverage(self):
        for packet in self.timing["packets"]:
            packet.update(flags=2, silent=True)
        audio = self.load()
        rows = analyze_events(audio, [{"time_ms": 500}, {"time_ms": 9000}], self.origin)
        self.assertEqual(rows[0]["status"], "silence")
        self.assertEqual(rows[1]["status"], "unknown")

    def test_malformed_packet_metadata_is_rejected(self):
        changes = [
            ("frame_offset", 1),
            ("frame_count", 0),
            ("qpc_ms", float("nan")),
            ("qpc_position_100ns", 1),
            ("flags", 8),
            ("silent", True),
        ]
        for field, value in changes:
            with self.subTest(field=field):
                self.make_packets()
                self.timing["packets"][0][field] = value
                with self.assertRaises(ValueError):
                    self.load()

    def test_overlapping_packet_clock_is_rejected(self):
        packet = self.timing["packets"][1]
        packet["qpc_ms"] -= 50
        packet["qpc_position_100ns"] -= 500_000
        with self.assertRaisesRegex(ValueError, "overlap"):
            self.load()

    def test_unfinished_source_and_bad_hash_rejected(self):
        wav = self.save()
        for field, value in (
            ("status", "incomplete"),
            ("finalized", False),
            ("scope", "system-mix"),
            ("wav_sha256", "0" * 64),
        ):
            with self.subTest(field=field):
                bad = copy.deepcopy(self.timing)
                bad[field] = value
                with self.assertRaises(ValueError):
                    load_audio(wav, bad)

    def test_duration_limit_and_truncated_pcm_rejected_before_analysis(self):
        wav = self.save()
        original = wav.read_bytes()
        oversized = bytearray(original)
        struct.pack_into("<I", oversized, 40, self.rate * 211 * 4)
        for content, reason in (
            (oversized, "210 seconds"),
            (original[:-8], "Truncated"),
        ):
            with self.subTest(reason=reason):
                wav.write_bytes(content)
                self.timing["wav_sha256"] = hashlib.sha256(content).hexdigest().upper()
                with self.assertRaisesRegex(ValueError, reason):
                    load_audio(wav, self.timing)

    def test_missing_packet_tail_and_false_silent_claim_rejected(self):
        self.timing["packets"].pop()
        with self.assertRaisesRegex(ValueError, "complete WAV"):
            self.load()
        self.make_packets()
        self.timing["packets"][0].update(flags=2, silent=True)
        self.samples[0] = 500
        with self.assertRaisesRegex(ValueError, "nonzero"):
            self.load()

    def test_shared_source_identity_and_origin_are_required(self):
        self.add_round()
        self.timing["video_capture_origin_ms"] += 10
        with self.assertRaisesRegex(ValueError, "origin"):
            self.run_analysis()
        self.timing["video_capture_origin_ms"] -= 10
        self.timing["target_process_id"] = 456
        with self.assertRaisesRegex(ValueError, "identities"):
            self.run_analysis()
        self.timing["target_process_id"] = 123
        self.replay["source_video_sha256"] = "F" * 64
        with self.assertRaisesRegex(ValueError, "same video"):
            self.run_analysis()

    def test_end_to_end_keeps_unknown_audio_and_false_qualification(self):
        self.add_round(outcome="Failure")
        result = self.run_analysis()
        self.assertEqual(result["rounds"][0]["outcome"], "Failure")
        self.assertTrue(any(e["status"] == "silence" for e in result["events"]))
        self.assertFalse(result["live_qualified"])
        self.assertFalse(result["exact_decoded_video_alignment_verified"])
        self.assertFalse(result["automatic_training_eligible"])
        self.assertEqual(result["inputs_sent"], 0)
        self.assertEqual(
            result["evaluation"]["status"],
            "descriptive_only_explicit_round_split_required",
        )

    def test_empty_replay_produces_no_invented_negative_labels(self):
        result = self.run_analysis()
        self.assertEqual(result["events"], [])
        self.assertEqual(result["rounds"], [])

    def test_repeated_cues_are_separate_only_after_visual_gap(self):
        self.add_round(sequence=("Right", "Right", "Up"))
        events, _, _ = extract_events(self.replay, self.inputs)
        cues = [e for e in events if e["kind"] == "watch_direction"]
        self.assertEqual([e["label"] for e in cues], ["Right", "Right", "Up"])
        self.assertEqual([e["time_ms"] for e in cues], [500, 1000, 1500])
        self.assertEqual([e["confirmed_ms"] for e in cues], [600, 1100, 1600])
        # Even an inconsistent native list cannot turn a held arrow into two onsets.
        for row in self.replay["events"]:
            if row["time_ms"] == 650:
                row["Cues"] = ["right", "right"]
        events, _, warnings = extract_events(self.replay, self.inputs)
        self.assertEqual(sum(e["kind"] == "watch_direction" for e in events), 2)
        self.assertTrue(warnings)

    def test_input_repeats_focus_and_injection_are_separate_from_watch(self):
        go = self.add_round()
        self.press(go + 100, "Up")
        self.inputs["Events"].append(
            {"TimeMs": go + 120, "Type": "key_down", "Control": "Up", "Injected": False}
        )
        self.press(go + 200, "Up")
        self.press(go + 300, "Right", injected=True)
        self.inputs["Events"].append({"TimeMs": go + 400, "Type": "focus_lost"})
        self.press(go + 500, "Down")
        events, _, warnings = extract_events(self.replay, self.inputs)
        keys = [e for e in events if e["kind"] == "response_key"]
        self.assertEqual([e["label"] for e in keys], ["Up", "Up"])
        self.assertEqual(len([e for e in events if e["kind"] == "watch_direction"]), 3)
        self.assertTrue(warnings)

    def test_ambiguous_result_never_assigns_keys_to_a_round(self):
        go = self.add_round()
        self.replay["outcomes"].append({"time_ms": go + 800, "outcome": "Failure"})
        self.press(go + 100, "Up")
        events, rounds, _ = extract_events(self.replay, self.inputs)
        self.assertEqual(rounds[0]["outcome"], "Unknown")
        self.assertFalse(any(e["kind"] == "response_key" for e in events))


@NUMPY_REQUIRED
class SpectralTests(unittest.TestCase):
    def test_bass_or_cutoff_peak_does_not_become_an_in_band_note(self):
        for frequency in (30, 40, 60, 80):
            with self.subTest(frequency=frequency):
                result = describe_sound(
                    tone(frequency, rate=44100),
                    np.zeros((5292, 2), dtype="<i2"),
                    44100,
                )
                self.assertEqual(result["status"], "out_of_band_or_boundary_peak")
                self.assertTrue(
                    result["dominant_energy_outside_analysis_band"]
                    or result["peak_at_analysis_boundary"]
                )

    def test_expected_capture_sample_rates_preserve_tone_and_background(self):
        for rate in (44100, 48000):
            with self.subTest(rate=rate):
                samples = tone(440, rate=rate)
                result = describe_sound(
                    samples, np.zeros((round(rate * 0.12), 2), dtype="<i2"), rate
                )
                self.assertEqual(result["status"], "ok")
                self.assertAlmostEqual(result["dominant_peak_hz"], 440, delta=8)
                steady = describe_sound(
                    samples, tone(440, duration=0.12, rate=rate), rate
                )
                self.assertEqual(
                    steady["status"], "no_local_change_music_or_held_sound"
                )

    def test_pure_tone_peak_and_antiphase_stereo(self):
        samples = tone(440)
        samples[:, 1] *= -1
        result = describe_sound(samples, np.zeros((960, 2), dtype="<i2"), 8000)
        self.assertEqual(result["status"], "ok")
        self.assertAlmostEqual(result["dominant_peak_hz"], 440, delta=5)
        self.assertGreater(result["rms_dbfs"], -20)
        self.assertFalse(result["pitch_is_fundamental_verified"])

    def test_two_nonharmonic_tones_remain_ambiguous(self):
        samples = tone(440, amplitude=0.3) + tone(690, amplitude=0.3)
        result = describe_sound(samples, np.zeros((960, 2), dtype="<i2"), 8000)
        self.assertEqual(result["status"], "mixed_or_harmonic_ambiguity")
        self.assertGreaterEqual(len(result["spectral_peaks"]), 2)

    def test_unchanged_music_or_held_note_is_not_a_new_direction(self):
        result = describe_sound(tone(440), tone(440, duration=0.12), 8000)
        self.assertEqual(result["status"], "no_local_change_music_or_held_sound")


@NUMPY_REQUIRED
class RoundSplitTests(unittest.TestCase):
    def events(self):
        rows = []
        for round_id in (1, 2):
            for direction, freq in zip(
                ("Up", "Down", "Left", "Right"), (220, 390, 640, 1050)
            ):
                rows.append(
                    {
                        "event_id": len(rows) + 1,
                        "round": round_id,
                        "kind": "watch_direction",
                        "label": direction,
                        "status": "ok",
                        "features": describe_sound(
                            tone(freq), np.zeros((960, 2), dtype="<i2"), 8000
                        ),
                    }
                )
        return rows

    def test_whole_round_split_has_no_fit_leakage(self):
        rows = self.events()
        result = evaluate_groups(rows, [1], [2], [1, 2])
        self.assertEqual(result["status"], "evaluated")
        self.assertTrue(all(t["rounds"] == [1] for t in result["templates"].values()))
        self.assertTrue(all(e["round"] == 2 for e in result["evaluations"]))
        self.assertEqual(result["counts_by_kind"]["watch_direction"]["agrees"], 4)
        self.assertFalse(result["general_accuracy_claimed"])
        modified = copy.deepcopy(rows)
        for row in modified[4:]:
            row["features"]["descriptor"] = [0.0] * 84
        changed = evaluate_groups(modified, [1], [2], [1, 2])
        self.assertEqual(result["templates"], changed["templates"])

    def test_overlap_unknown_round_and_incomplete_labels_rejected_or_unknown(self):
        rows = self.events()
        for calibration, heldout in (([1], [1]), ([1], [3]), ([True], [2])):
            with self.subTest(split=(calibration, heldout)), self.assertRaises(
                ValueError
            ):
                evaluate_groups(rows, calibration, heldout, [1, 2])
        rows[0]["status"] = "silence"
        result = evaluate_groups(rows, [1], [2], [1, 2])
        self.assertEqual(result["status"], "insufficient_direction_coverage")
        self.assertTrue(
            all(e["audio_prediction"] == "Unknown" for e in result["evaluations"])
        )


if __name__ == "__main__":
    unittest.main()
