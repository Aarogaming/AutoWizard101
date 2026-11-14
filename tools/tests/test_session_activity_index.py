import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from game_session import SessionEvent
from session_activity_index import build_index


def event(i, time, scene, zone=None, activity=None, source="fixture"):
    return SessionEvent(
        str(i),
        "observation",
        i,
        time,
        {
            "activity_context": {
                "source": source,
                "scene": scene,
                "zone": zone,
                "activity": activity,
            }
        },
    )


class ActivityIndexTests(unittest.TestCase):
    def test_hour_of_mixed_activity_preserves_loading_and_idle(self):
        events = [
            event(0, 0, "active", "Commons", "navigation"),
            event(1, 100000, "loading"),
            event(2, 103000, "active", "Pavilion", "navigation"),
            event(3, 200000, "idle", "Pavilion"),
            event(4, 900000, "active", "Pavilion", "dance"),
            event(5, 1800000, "idle", "Pavilion"),
            event(6, 2400000, "active", "Pavilion", "menu"),
        ]
        result = build_index(iter(events), 3600000)
        self.assertEqual(7, len(result["segments"]))
        self.assertEqual(3600000, result["segments"][-1]["end_ms"])
        self.assertEqual(1, len(result["transition_candidates"]))
        self.assertFalse(result["transition_candidates"][0]["executable"])
        self.assertEqual(900000, result["segments"][3]["end_ms"])

    def test_missing_source_does_not_invent_a_zone_or_route(self):
        result = build_index(
            [
                event(0, 100, "active", "A"),
                event(1, 200, "loading"),
                event(2, 300, "active", "B", source=None),
            ],
            1000,
        )
        self.assertEqual("unknown", result["segments"][-1]["scene"])
        self.assertIsNone(result["segments"][-1]["zone"])
        self.assertEqual([], result["transition_candidates"])
        self.assertEqual(100, result["leading_unobserved_ms"])

    def test_repeated_context_keeps_source_event_bounds(self):
        result = build_index([event(0, 0, "idle"), event(1, 200, "idle")], 300)
        self.assertEqual(1, len(result["segments"]))
        self.assertEqual("1", result["segments"][0]["last_event"])

    def test_rejects_invalid_clock(self):
        for end in [-1, float("nan")]:
            with self.assertRaises(ValueError):
                build_index([], end)
        with self.assertRaises(ValueError):
            build_index([event(0, 200, "idle"), event(1, 100, "idle")], 300)
