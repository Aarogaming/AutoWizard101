import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from w101_connector import select
from w101_zone_route import ALIASES, plan


class ConnectorTests(unittest.TestCase):
    def request(self, **changes):
        result = dict(
            points=[(1, 0, 0), (3, 0, 0), (5, 0, 0)],
            edges=[(0, 1), (1, 2)],
            goal=2,
            pose=(0, 0, 0),
            settled=True,
            fresh=True,
            heading=0,
            max_distance=4,
            max_elevation=1,
            max_grade=1,
            margin=0.2,
            turn_cost=1,
        )
        result.update(changes)
        return result

    def test_unknown_geometry_never_qualifies(self):
        self.assertIsNone(select(**self.request(qualified_vertices=[0]))["selected"])

    def test_obstacle_blocks_swept_connector(self):
        box = dict(name="post", min=(0.5, 0.1, -0.1), max=(0.6, 0.2, 0.1))
        result = select(
            **self.request(
                obstacles=[box], geometry_complete=True, qualified_vertices=[0, 1]
            )
        )
        self.assertIsNone(result["selected"])
        self.assertEqual(result["candidates"][0]["obstacleHits"], ["post"])

    def test_changed_landing_changes_selection(self):
        first = select(
            **self.request(geometry_complete=True, qualified_vertices=[0, 1])
        )
        second = select(
            **self.request(
                pose=(3, 0, 0), geometry_complete=True, qualified_vertices=[0, 1]
            )
        )
        self.assertEqual(first["selected"], 0)
        self.assertEqual(second["selected"], 1)

    def test_elevation_and_reach(self):
        self.assertEqual(select(**self.request(pose=(0, 0, 10)))["candidates"], [])
        self.assertEqual(select(**self.request(max_distance=0.1))["candidates"], [])

    def test_stale_or_unsettled(self):
        for flag in ["fresh", "settled"]:
            self.assertIsNone(select(**self.request(**{flag: False}))["selected"])

    def test_unreachable(self):
        self.assertEqual(select(**self.request(edges=[]))["candidates"], [])

    def test_route_connection(self):
        graph = dict(
            nodes=[
                dict(id=0, zone=ALIASES["commons"]),
                dict(id=1, zone=ALIASES["pet pavilion"]),
            ],
            edges=[[0, 1]],
        )
        result = plan(
            graph,
            "Commons",
            "Pet Dance",
            connector_request=dict(zone=ALIASES["commons"], **self.request()),
        )
        self.assertEqual(
            result["entryConnector"]["status"], "arrived-in-zone-connector-unqualified"
        )
        self.assertFalse(result["runtimeEnabled"])

    def test_invalid_calibration(self):
        with self.assertRaises(ValueError):
            select(**self.request(margin=float("nan")))


if __name__ == "__main__":
    unittest.main()
