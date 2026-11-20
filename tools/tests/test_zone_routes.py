"""Unmeasured route sketches must never supply ordinary macro actions."""

import json
from pathlib import Path
import tempfile
import unittest

from tools.macro_pipeline import MacroExecutor, macro_from_dict
from tools.zone_routes import STANDARD_ROUTES, export_standard_routes


class TestZoneRoutes(unittest.TestCase):
    def test_packaged_legacy_filenames_are_non_executable_previews(self):
        root = (
            Path(__file__).resolve().parents[2]
            / "ProjectMaelstrom/ProjectMaelstrom/Resources/Macros"
        )
        for route_id, route in STANDARD_ROUTES.items():
            with self.subTest(route=route_id):
                data = json.loads(
                    (root / f"{route_id}.macro.json").read_text(encoding="utf-8")
                )
                self.assertEqual(data["schema"], "maelstrom.route-preview.v1")
                self.assertFalse(data["executable"])
                self.assertFalse(data["live_qualified"])
                self.assertNotIn("nodes", data)
                self.assertNotIn("node_count", data)
                self.assertEqual(macro_from_dict(data), [])
                self.assertEqual(
                    data["example_waypoints"], route.to_preview()["example_waypoints"]
                )
                self.assertRegex(data["quarantined_macro_sha256"], r"^[0-9a-f]{64}$")

    def test_every_sketch_rejects_both_runnable_conversion_paths(self):
        self.assertEqual(len(STANDARD_ROUTES), 5)
        for route in STANDARD_ROUTES.values():
            with self.subTest(route=route.route_id):
                with self.assertRaisesRegex(ValueError, "Unqualified"):
                    route.to_macro_nodes()
                with self.assertRaisesRegex(ValueError, "Unqualified"):
                    route.to_macro_graph()

    def test_default_export_fails_before_creating_destination(self):
        with tempfile.TemporaryDirectory() as root:
            target = Path(root) / "not-created"
            with self.assertRaisesRegex(ValueError, "preview_only"):
                export_standard_routes(target)
            self.assertFalse(target.exists())

    def test_explicit_preview_cannot_supply_macro_nodes(self):
        with tempfile.TemporaryDirectory() as root:
            paths = export_standard_routes(Path(root), preview_only=True)
            self.assertEqual(len(paths), 5)
            for path in paths:
                with self.subTest(path=path.name):
                    self.assertTrue(path.name.endswith(".route-preview.json"))
                    data = json.loads(path.read_text(encoding="utf-8"))
                    self.assertEqual(data["schema"], "maelstrom.route-preview.v1")
                    self.assertFalse(data["executable"])
                    self.assertFalse(data["live_qualified"])
                    self.assertIsNone(data["coordinate_source"])
                    self.assertNotIn("nodes", data)
                    self.assertNotIn("payload", data)
                    self.assertIn("example_waypoints", data)
                    # Existing parser does not enforce schema; absence of its
                    # nodes matters, not just the non-executable preview label.
                    nodes = macro_from_dict(data)
                    self.assertEqual(nodes, [])
                    self.assertTrue(MacroExecutor(nodes).is_finished)

    def test_preview_does_not_overwrite_existing_artifacts(self):
        with tempfile.TemporaryDirectory() as root:
            paths = export_standard_routes(Path(root), preview_only=True)
            original = paths[0].read_bytes()
            with self.assertRaises(FileExistsError):
                export_standard_routes(Path(root), preview_only=True)
            self.assertEqual(paths[0].read_bytes(), original)


if __name__ == "__main__":
    unittest.main()
