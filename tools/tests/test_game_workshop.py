from copy import deepcopy
import gc
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from game_packs import PackError, load_pack, new_pack, save_pack, validate_pack
from game_pack_workshop import context_summary
from game_session import SessionError, read_session
from evaluate_game_pack import evaluate, write_report

SAMPLES = Path(__file__).resolve().parents[1] / "fixtures/game_packs"


class WorkshopStorageTests(unittest.TestCase):
    def test_context_pack_roundtrips_and_rejects_invalid_evidence(self):
        pack = load_pack(SAMPLES / "dodge-context")
        candidate = load_pack(SAMPLES / "dodge-context-v2")
        guarded = load_pack(SAMPLES / "dodge-context-v3")
        recovery = load_pack(SAMPLES / "dodge-context-v4")
        self.assertEqual(candidate["pack_id"], pack["pack_id"])
        self.assertNotEqual(candidate["version"], pack["version"])
        self.assertEqual(candidate["context"]["avoidance_policy"], "resilient-v2")
        self.assertEqual(
            guarded["context"]["constraints"], pack["context"]["constraints"]
        )
        self.assertEqual(guarded["context"]["avoidance_policy"], "guarded-v3")
        self.assertEqual(
            recovery["context"]["constraints"], pack["context"]["constraints"]
        )
        self.assertEqual(recovery["context"]["avoidance_policy"], "guarded-v4")
        self.assertIn("Identification threshold: 5/5", context_summary(pack))
        self.assertEqual(
            "No game context in this pack.",
            context_summary(new_pack("plain", "Plain", "game")),
        )
        with tempfile.TemporaryDirectory() as folder:
            self.assertEqual(load_pack(save_pack(pack, folder)), pack)
        for mutate in (
            lambda p: p["context"]["identity"].update(minimum_score=99),
            lambda p: p["context"]["action_states"].append("absent"),
            lambda p: p["context"]["state_masks"]["data"].update(menu="not-base64"),
            lambda p: p["context"]["constraints"].update(
                player_speed_px_s=float("nan")
            ),
        ):
            invalid = deepcopy(pack)
            mutate(invalid)
            with self.assertRaises(PackError):
                validate_pack(invalid)

    def test_unimplemented_recognition_assets_fail_closed(self):
        pack = new_pack("test", "Test", "game")
        pack["recognition"] = {"templates": ["template.png"]}
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaises(PackError):
                save_pack(pack, folder)
            self.assertFalse((Path(folder) / "test").exists())

    def test_roundtrip_and_existing_version_preserved(self):
        pack = load_pack(SAMPLES / "pet-dance")
        with tempfile.TemporaryDirectory() as folder:
            saved = save_pack(pack, folder)
            self.assertEqual(load_pack(saved), pack)
            pack["title"] = "Changed"
            with self.assertRaises(PackError):
                save_pack(pack, folder)
            self.assertNotEqual(load_pack(saved)["title"], "Changed")
            pack["version"] = "0.1.1"
            self.assertEqual(load_pack(save_pack(pack, folder)), pack)

    def test_invalid_ids_qualification_numbers_and_rules(self):
        original = load_pack(SAMPLES / "pet-dance")
        for field, value in (
            ("pack_id", "../outside"),
            ("pack_id", "con"),
            ("version", "1."),
            ("schema_version", True),
            ("status", "live-tested"),
        ):
            pack = deepcopy(original)
            pack[field] = value
            with self.subTest(field=field, value=value), self.assertRaises(PackError):
                validate_pack(pack)
        pack = deepcopy(original)
        pack["skills"][0]["propose"] = {
            "type": "click",
            "parameters": {"x": float("nan"), "y": 0.5},
        }
        with self.assertRaises(PackError):
            validate_pack(pack)
        pack["skills"][0]["propose"] = {
            "type": "wait",
            "parameters": {"duration_ms": -1},
        }
        with self.assertRaises(PackError):
            validate_pack(pack)

    def test_duplicate_json_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "pack.json"
            path.write_text('{"schema_version":1,"schema_version":1}')
            with self.assertRaises(PackError):
                load_pack(path)

    def test_report_replacement_failure_preserves_previous(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "report.json"
            path.write_text("old")
            with patch(
                "evaluate_game_pack.os.replace", side_effect=PermissionError("locked")
            ):
                with self.assertRaises(PermissionError):
                    write_report(path, {"mode": "offline"})
            self.assertEqual(path.read_text(), "old")
            self.assertEqual(list(Path(folder).iterdir()), [path])

    def test_evaluation_hashes_bytes_used_before_edit(self):
        with tempfile.TemporaryDirectory() as folder:
            pack = Path(folder) / "pack.json"
            raw = (SAMPLES / "pet-dance/pack.json").read_bytes()
            pack.write_bytes(raw)
            from game_packs import propose_for_observation

            def edit_after_load(data, observation):
                pack.write_text("changed on disk")
                return propose_for_observation(data, observation)

            with patch(
                "evaluate_game_pack.propose_for_observation",
                side_effect=edit_after_load,
            ):
                report = evaluate(
                    pack, SAMPLES / "session.json", SAMPLES / "pet-dance/expected.json"
                )
            self.assertTrue(report["expectations"]["passed"])
            self.assertEqual(report["pack"]["sha256"], hashlib.sha256(raw).hexdigest())


class SessionValidationTests(unittest.TestCase):
    def test_duplicate_ids_negative_time_and_nonfinite_payload(self):
        for rows in (
            ['{"event_id":"a","kind":"observation","sequence":0,"time_ms":-1}'],
            [
                '{"event_id":"a","kind":"observation","sequence":0,"time_ms":0,"payload":{"x":NaN}}'
            ],
            [
                '{"event_id":"a","kind":"observation","sequence":0,"time_ms":0}',
                '{"event_id":"a","kind":"observation","sequence":1,"time_ms":1}',
            ],
        ):
            with self.subTest(rows=rows), tempfile.TemporaryDirectory() as folder:
                path = Path(folder)
                (path / "session.json").write_bytes(
                    (SAMPLES / "session.json").read_bytes()
                )
                (path / "events.jsonl").write_text("\n".join(rows))
                with self.assertRaises(SessionError):
                    _, events = read_session(path / "session.json")
                    list(events)

    def test_empty_replay_cannot_pass_expectations(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)
            (path / "session.json").write_bytes((SAMPLES / "session.json").read_bytes())
            (path / "events.jsonl").write_text("")
            (path / "expected.json").write_text("[]")
            report = evaluate(
                SAMPLES / "pet-dance", path / "session.json", path / "expected.json"
            )
            self.assertFalse(report["expectations"]["passed"])


class WorkshopUiTests(unittest.TestCase):
    def test_dodge_registry_can_be_rebound_saved_and_replayed(self):
        from game_pack_workshop import Workshop

        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            window = Workshop(root / "packs")
            window.withdraw()
            try:
                window.sample("dodge")
                window.tree.selection_set("avoid-left")
                window.select_rule()
                self.assertEqual(window.action_fields["key"].get(), "LEFT")
                window.action_fields["key"].set("A")
                window.add_action()
                window.fields["version"].set("0.1.1")
                window.save()
                pack = load_pack(window.saved_path)
                self.assertEqual(
                    next(a for a in pack["actions"] if a["id"] == "move-left")["key"],
                    "A",
                )
                (root / "session.json").write_text(
                    json.dumps(
                        {
                            "schema_version": 1,
                            "session_id": "check",
                            "clock_origin": "fixture",
                            "events_file": "events.jsonl",
                        }
                    )
                )
                (root / "events.jsonl").write_text(
                    json.dumps(
                        {
                            "event_id": "1",
                            "kind": "observation",
                            "sequence": 1,
                            "time_ms": 0,
                            "payload": {"state": "dodge-advice-left"},
                        }
                    )
                )
                window.session.set(str(root / "session.json"))
                window.run()
                window.future.result(timeout=10)
                window.after_cancel(window.poll_id)
                window.poll()
                self.assertIn("1 proposals", window.result.get("1.0", "end"))
                report_path = next((root / "reports").glob("*.json"))
                self.assertEqual(
                    json.loads(report_path.read_text())["proposals_by_skill"],
                    {"avoid-left": 1},
                )
                window.compare_version(SAMPLES / "dodge/pack.json")
                window.future.result(timeout=10)
                window.after_cancel(window.poll_id)
                window.poll()
                self.assertIn("1 differing proposals", window.result.get("1.0", "end"))
            finally:
                window.worker.shutdown(wait=True)
                window.destroy()
                # Dispose Tk reference cycles on their owning thread before the next
                # test starts a replay worker that could trigger cyclic collection.
                del window
                gc.collect()

    def test_open_edit_save_reload_and_replay(self):
        from game_pack_workshop import Workshop

        with tempfile.TemporaryDirectory() as folder:
            window = Workshop(Path(folder) / "packs")
            window.withdraw()
            try:
                window.open_path(SAMPLES / "pet-dance/pack.json", confirm=False)
                window.fields["version"].set("0.1.1")
                window.save()
                self.assertEqual(load_pack(window.saved_path)["version"], "0.1.1")
                window.open_path(window.saved_path, confirm=False)
                window.session.set(str(SAMPLES / "session.json"))
                window.expected.set(str(SAMPLES / "pet-dance/expected.json"))
                window.run()
                window.future.result(timeout=10)
                window.after_cancel(window.poll_id)
                window.poll()
                self.assertIn(
                    "Expected proposals match", window.result.get("1.0", "end")
                )
                self.assertEqual(
                    len(list((Path(folder) / "reports").glob("*.json"))), 1
                )
                window.fields["title"].set("Unsaved change")
                with patch.object(window, "error") as error:
                    window.run()
                self.assertIn("Save the current draft", str(error.call_args.args[0]))
            finally:
                window.worker.shutdown(wait=True)
                window.destroy()
                del window
                gc.collect()


if __name__ == "__main__":
    unittest.main()
