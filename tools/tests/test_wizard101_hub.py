import argparse
from datetime import datetime, timedelta, timezone
import importlib.util
import json
import os
from pathlib import Path
import sqlite3
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import wizard101_hub as hub
from moonlight_adapter import adapt_reader

reader_path = hub.ROOT / hub.LIBRARY / "snapshot_reader.py"
spec = importlib.util.spec_from_file_location("snapshot_reader", reader_path)
reader = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = reader
spec.loader.exec_module(reader)


class HubTests(unittest.TestCase):
    def test_moonlight_iterator_shim_keeps_existing_implementations(self):
        class ModernReader:
            def __next__(self):
                return "packet"
        adapt_reader(ModernReader)
        self.assertEqual(ModernReader().next(), "packet")
        class LegacyReader(ModernReader):
            def next(self):
                return "legacy packet"
        adapt_reader(LegacyReader)
        self.assertEqual(LegacyReader().next(), "legacy packet")

    def test_overlaps_identify_competing_automation(self):
        result = hub.overlaps(hub.TOOLS)
        self.assertEqual(result["pet-training"], ["deimos", "wizsdk", "dance"])
        self.assertNotIn("companion", result["game-input"])

    def test_export_reads_active_rows_and_preserves_details_without_writes(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "boss.db"
            with sqlite3.connect(path) as db:
                db.execute("CREATE TABLE bosses(name, location, school, is_active, cheats_json, health)")
                db.execute("INSERT INTO bosses VALUES ('Boss', 'Place', 'Fire', 1, '[\"Shield\"]', '100')")
                db.execute("INSERT INTO bosses VALUES ('Retired', '', '', 0, '[]', '')")
            db.close()
            original = path.read_bytes()
            result = hub.export_companion(path)
            self.assertEqual(len(result), 1)
            self.assertEqual(result[0]["Cheats"], ["Shield"])
            self.assertEqual(result[0]["Health"], "100")
            self.assertEqual(path.read_bytes(), original)

    def test_export_missing_database_is_not_created(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "missing.db"
            with self.assertRaises(FileNotFoundError):
                hub.export_companion(path)
            self.assertFalse(path.exists())

    def test_export_rejects_unknown_schema(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "empty.db"
            sqlite3.connect(path).close()
            with self.assertRaisesRegex(ValueError, "schema"):
                hub.export_companion(path)

    def test_existing_output_is_never_overwritten(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "existing.json"
            path.write_text("original")
            with self.assertRaises(FileExistsError):
                hub.write_json(path, [])
            self.assertEqual(path.read_text(), "original")

    def test_v1_v2_types_normalize_to_same_record(self):
        v1 = {"Boss": {"hash": 123, "properties": {"HP": {"offset": 8}}, "bases": []}}
        v2 = {"version": 2, "classes": {"123": dict(v1["Boss"], name="Boss")}}
        self.assertEqual(hub.normalize_types(v1), hub.normalize_types(v2))
        newer = {"version": 2, "classes": {"123": dict(v2["classes"]["123"], properties={"HP": {"offset": 16}})}}
        result = hub.compare_records(hub.normalize_types(v1), hub.normalize_types(newer), "hash")
        self.assertEqual(len(result["changed"]), 1)

    def test_patch_changes_include_additions_removals_and_crc_changes(self):
        result = hub.compare_records(
            [{"name": "gone", "crc": 1}, {"name": "changed", "crc": 2}],
            [{"name": "new", "crc": 1}, {"name": "changed", "crc": 3}], "name")
        self.assertEqual(result["added"][0]["name"], "new")
        self.assertEqual(result["removed"][0]["name"], "gone")
        self.assertEqual(result["changed"][0]["after"]["crc"], 3)
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            hub.compare_records([{"name": "a"}, {"name": "a"}], [], "name")

    def test_input_lock_excludes_second_owner_and_releases(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            with hub.automation_lease(root):
                with self.assertRaisesRegex(RuntimeError, "owns game input"):
                    with hub.automation_lease(root):
                        self.fail("Second owner acquired lock")
            with hub.automation_lease(root):
                pass

    def test_policy_requires_live_and_experimental(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "policy.conf"
            for text in ("", "ALLOW_LIVE_AUTOMATION=true", "ALLOW_LIVE_AUTOMATION=false\nEXECUTION_PROFILE=ExperimentalSimulation"):
                path.write_text(text)
                with self.assertRaises(ValueError):
                    hub.validate_policy(path)
            path.write_text("ALLOW_LIVE_AUTOMATION=true\nEXECUTION_PROFILE=ExperimentalSimulation")
            hub.validate_policy(path)

    def test_preview_does_not_create_process(self):
        args = argparse.Namespace(tool="companion", snapshot=None, execute=False)
        with patch.object(hub.subprocess, "Popen") as popen:
            self.assertEqual(hub.run_tool(args), 0)
            popen.assert_not_called()

    def test_libraries_do_not_have_fake_entry_points(self):
        for name in ("wizwalker", "wizsdk", "wizdiff"):
            with self.assertRaisesRegex(ValueError, "library"):
                hub.launch_command(name, argparse.Namespace())

    def test_moonlight_rejects_v2_types_before_starting(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            capture = root / "input.pcap"
            capture.write_bytes(b"fixture")
            typedefs = root / "types.json"
            typedefs.write_text('{"version": 2, "classes": {}}')
            args = argparse.Namespace(definitions=str(root), capture=str(capture),
                                      output=str(root / "output.json"), typedefs=str(typedefs))
            with self.assertRaisesRegex(ValueError, "v1 type cache"):
                hub.launch_command("moonlight", args, root)
            self.assertFalse((root / "output.json").exists())

    def test_snapshot_environment_and_resource_fields(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "snapshot.json"
            path.write_text(json.dumps({"CapturedUtc": datetime.now(timezone.utc).isoformat(),
                                       "Energy": {"Current": 20, "Max": 100}, "Potions": {"Value": 2},
                                       "WindowPresent": True, "HasFocus": True, "Resolution": "1280x720"}))
            with patch.dict(os.environ, {"MAELSTROM_SNAPSHOT_PATH": str(path)}):
                snapshot = reader.load_snapshot(max_age_seconds=15)
            self.assertEqual(snapshot.energy.current, 20)
            self.assertEqual(snapshot.potions.value, 2)
            self.assertTrue(snapshot.has_focus)

    def test_stale_missing_and_future_snapshots_are_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "snapshot.json"
            for captured in (None, datetime.now(timezone.utc) - timedelta(minutes=1),
                             datetime.now(timezone.utc) + timedelta(minutes=1)):
                path.write_text(json.dumps({"CapturedUtc": captured.isoformat() if captured else None}))
                with self.assertRaises(ValueError):
                    reader.load_snapshot(str(path), max_age_seconds=15)


if __name__ == "__main__":
    unittest.main()
