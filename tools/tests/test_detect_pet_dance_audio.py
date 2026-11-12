import copy
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from analyze_pet_dance_audio import Audio, describe_sound
from detect_pet_dance_audio import (
    Debouncer,
    classify,
    evaluate_events,
    load_templates,
    scan_audio,
)
import numpy as np

RATE = 44100
ORIGIN = 10000.0


def tone(hz, seconds=0.22):
    mono = np.rint(
        0.1 * np.sin(2 * np.pi * hz * np.arange(round(RATE * seconds)) / RATE) * 32767
    ).astype("<i2")
    return np.column_stack((mono, mono))


def templates_document():
    result = {}
    for label, hz in {"Up": 220, "Down": 390, "Left": 697, "Right": 1100}.items():
        feature = describe_sound(tone(hz), np.zeros((5292, 2), dtype="<i2"), RATE)
        result[label] = {"descriptor": feature["descriptor"]}
    return {
        "feature_version": "spectral-novelty-v2-boundaries",
        "evaluation": {"templates": result},
    }


def synthetic_audio(seconds=4, pulses=(), gap_at=None, gap_ms=500):
    samples = np.zeros((round(seconds * RATE), 2), dtype="<i2")
    for start, hz in pulses:
        clip = tone(hz)
        index = round(start * RATE)
        samples[index : index + len(clip)] = clip
    packets, segments, current = [], [], []
    for offset in range(0, len(samples), 441):
        second = offset / RATE
        if gap_at is not None and second == gap_at:
            segments.append(current)
            current = []
        time = (
            ORIGIN
            + second * 1000
            + (gap_ms if gap_at is not None and second >= gap_at else 0)
        )
        row = {
            "qpc_ms": time,
            "frame_offset": offset,
            "frame_count": min(441, len(samples) - offset),
            "end_ms": time + min(441, len(samples) - offset) * 1000 / RATE,
        }
        packets.append(row)
        current.append(row)
    if current:
        segments.append(current)
    return Audio(samples, RATE, packets, segments, {"video_capture_origin_ms": ORIGIN})


def scan(time, label):
    return {
        "qpc_ms": time,
        "classification": {"label": label, "similarity": 0.99, "margin": 0.5},
    }


class FrozenTemplateTests(unittest.TestCase):
    def test_four_normalized_templates_are_required(self):
        document = templates_document()
        self.assertEqual(set(load_templates(document)), {"Up", "Down", "Left", "Right"})
        for mutation in ("missing", "nan", "unnormalized", "wrong-version"):
            with self.subTest(mutation=mutation):
                bad = copy.deepcopy(document)
                if mutation == "missing":
                    bad["evaluation"]["templates"].pop("Right")
                elif mutation == "nan":
                    bad["evaluation"]["templates"]["Up"]["descriptor"][0] = float("nan")
                elif mutation == "unnormalized":
                    bad["evaluation"]["templates"]["Up"]["descriptor"] = [1.0] * 84
                else:
                    bad["feature_version"] = "different-features"
                with self.assertRaises(ValueError):
                    load_templates(bad)

    def test_unknown_quality_and_ambiguous_templates_never_classify(self):
        document = templates_document()
        templates = load_templates(document)
        self.assertEqual(classify({"status": "silence"}, templates)["label"], "Unknown")
        features = describe_sound(tone(697), np.zeros((5292, 2), dtype="<i2"), RATE)
        self.assertEqual(classify(features, templates)["label"], "Left")
        templates["Right"] = templates["Left"].copy()
        self.assertEqual(classify(features, templates)["label"], "Unknown")


class DebounceTests(unittest.TestCase):
    def test_confirmation_anchor_and_availability_include_lookahead(self):
        state = Debouncer()
        self.assertIsNone(state.observe(scan(1000, "Left")))
        result = state.observe(scan(1050, "Left"))
        self.assertEqual(result["qpc_ms"], 1050)
        self.assertEqual(result["first_support_qpc_ms"], 1000)
        self.assertEqual(result["available_qpc_ms"], 1270)

    def test_held_tone_emits_once(self):
        state = Debouncer()
        events = [state.observe(scan(t, "Up")) for t in range(1000, 2000, 50)]
        self.assertEqual(sum(e is not None for e in events), 1)

    def test_repeated_direction_requires_rearm(self):
        state = Debouncer()
        labels = [
            "Right",
            "Right",
            "Unknown",
            "Unknown",
            "Right",
            "Right",
            "Unknown",
            "Unknown",
            "Unknown",
            "Right",
            "Right",
        ]
        results = [
            state.observe(scan(1000 + n * 50, label)) for n, label in enumerate(labels)
        ]
        self.assertEqual([e["qpc_ms"] for e in results if e], [1050, 1500])

    def test_short_or_conflicting_acceptance_is_not_an_event(self):
        state = Debouncer()
        results = [
            state.observe(scan(1000 + n * 50, label))
            for n, label in enumerate(["Up", "Down", "Unknown", "Left", "Unknown"])
        ]
        self.assertFalse(any(results))

    def test_reset_breaks_cross_gap_confirmation(self):
        state = Debouncer()
        state.observe(scan(1000, "Up"))
        state.reset()
        self.assertIsNone(state.observe(scan(1050, "Up")))
        self.assertIsNotNone(state.observe(scan(1100, "Up")))

    def test_nonmonotonic_times_are_rejected(self):
        state = Debouncer()
        state.observe(scan(1000, "Unknown"))
        with self.assertRaises(ValueError):
            state.observe(scan(1000, "Up"))


class StreamingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.templates = load_templates(templates_document())

    def test_audio_only_pulses_are_detected_without_reference_times(self):
        audio = synthetic_audio(pulses=[(0.8, 697), (1.6, 220), (2.4, 390)])
        result = scan_audio(audio, self.templates)
        self.assertEqual([e["label"] for e in result["events"]], ["Left", "Up", "Down"])
        for event, actual in zip(result["events"], [800, 1600, 2400]):
            self.assertLessEqual(abs(event["video_ms"] - actual), 200)
            self.assertGreaterEqual(event["available_qpc_ms"], ORIGIN + actual)
        self.assertTrue(
            all(abs((s["qpc_ms"] - ORIGIN - 200) % 50) < 1e-6 for s in result["scans"])
        )
        self.assertTrue(any(s["feature_status"] == "unknown" for s in result["scans"]))

    def test_repeated_pulses_are_not_collapsed_into_one(self):
        result = scan_audio(
            synthetic_audio(pulses=[(0.8, 697), (1.6, 697), (2.4, 697)]), self.templates
        )
        self.assertEqual([e["label"] for e in result["events"]], ["Left"] * 3)

    def test_silence_and_gaps_are_distinct(self):
        result = scan_audio(synthetic_audio(seconds=2, gap_at=1), self.templates)
        self.assertEqual(result["events"], [])
        self.assertEqual(len(result["audio_coverage"]), 2)
        self.assertGreater(result["feature_status_counts"]["silence"], 0)
        self.assertTrue(
            any(
                row["reason"] == "packet_gap_or_invalid_timestamp"
                for row in result["unavailable_intervals"]
            )
        )
        self.assertEqual(
            result["audio_coverage"][1]["start_qpc_ms"]
            - result["audio_coverage"][0]["end_qpc_ms"],
            500,
        )


class EvaluationTests(unittest.TestCase):
    def event(self, index, time, label):
        return {
            "event": index,
            "qpc_ms": ORIGIN + time,
            "available_qpc_ms": ORIGIN + time + 220,
            "label": label,
        }

    def evaluate(self, events, refs, coverage=True):
        scans = [
            {
                "qpc_ms": ORIGIN + ref["time_ms"],
                "feature_status": "silence" if coverage else "unknown",
            }
            for ref in refs
        ]
        return evaluate_events(events, refs, ORIGIN, 500, 2500, scans)

    def test_nearest_time_matching_does_not_use_label_to_hide_wrong_prediction(self):
        events = [self.event(1, 1010, "Down"), self.event(2, 1100, "Up")]
        refs = [{"time_ms": 1000, "label": "Up", "round": 1}]
        result = self.evaluate(events, refs)
        self.assertEqual(result["wrong_direction_matches"], 1)
        self.assertEqual(result["correct_direction_matches"], 0)
        self.assertEqual(result["unmatched_inside_native_review"], 1)

    def test_full_stream_extras_remain_visible_outside_review(self):
        events = [
            self.event(1, 100, "Left"),
            self.event(2, 1000, "Up"),
            self.event(3, 3000, "Right"),
        ]
        refs = [{"time_ms": 1000, "label": "Up", "round": 1}]
        result = self.evaluate(events, refs)
        self.assertEqual(result["unmatched_outside_native_review"], 2)
        self.assertAlmostEqual(result["reference_precision_full_stream"], 1 / 3)
        self.assertEqual(result["precision_inside_native_review"], 1)
        self.assertTrue(result["development_data"])

    def test_missing_in_silence_counts_as_miss_but_missing_coverage_is_unscorable(self):
        refs = [{"time_ms": 1000, "label": "Up", "round": 1}]
        silent = self.evaluate([], refs)
        gap = self.evaluate([], refs, coverage=False)
        self.assertEqual(silent["missed_reference_cues"], 1)
        self.assertEqual(silent["recall_scorable_references"], 0)
        self.assertEqual(gap["scorable_reference_cues"], 0)
        self.assertEqual(len(gap["unscorable_references"]), 1)
        self.assertIsNone(gap["recall_scorable_references"])

    def test_evaluation_cannot_mutate_existing_detection_results(self):
        events = [self.event(1, 1000, "Left")]
        original = copy.deepcopy(events)
        for label in ("Left", "Right"):
            self.evaluate(events, [{"time_ms": 1000, "label": label, "round": 1}])
        self.assertEqual(events, original)


if __name__ == "__main__":
    unittest.main()
