import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from w101_zone_route import ALIASES, plan


class ZoneRouteTests(unittest.TestCase):
    def setUp(self):
        self.graph = dict(
            nodes=[
                dict(id=1, zone=ALIASES["commons"]),
                dict(id=2, zone=ALIASES["pet pavilion"]),
            ],
            edges=[[1, 2]],
        )

    def test_alias_and_no_automatic_execution(self):
        result = plan(self.graph, "Commons", "Pet Dance")
        self.assertTrue(result["found"])
        self.assertFalse(result["runtimeEnabled"])
        self.assertFalse(result["finalInteractionVerified"])

    def test_direction_preserved(self):
        self.assertFalse(plan(self.graph, "Pet Pavilion", "Commons")["found"])

    def test_ambiguous_name_rejected(self):
        self.graph["nodes"].append(dict(id=3, zone=ALIASES["commons"]))
        with self.assertRaises(ValueError):
            plan(self.graph, "Commons", "Pet Dance")

    def test_invalid_edge_rejected(self):
        self.graph["edges"].append([1, 99])
        with self.assertRaises(ValueError):
            plan(self.graph, "Commons", "Pet Dance")

    def test_conflicting_anchors_withheld(self):
        anchor = dict(fromZone=ALIASES["commons"], toZone=ALIASES["pet pavilion"])
        result = plan(self.graph, "Commons", "Pet Dance", [anchor, anchor])
        self.assertIsNone(result["legs"][0]["anchor"])

    def test_cycle_and_same_zone_terminate(self):
        self.graph["edges"].append([2, 1])
        self.assertEqual(plan(self.graph, "Commons", "Commons")["legs"], [])


if __name__ == "__main__":
    unittest.main()
