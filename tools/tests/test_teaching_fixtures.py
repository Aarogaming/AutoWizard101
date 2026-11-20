import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from game_packs import new_pack, save_pack, load_pack, PackError
from evaluate_game_pack import evaluate


class TeachingFixtureTests(unittest.TestCase):
    def test_revision_preserves_images_and_replays_human_label(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source"
            examples = source / "examples"
            examples.mkdir(parents=True)
            image = b"test image evidence"
            (examples / "frame-0000.png").write_bytes(image)
            (examples / "events.jsonl").write_text(
                json.dumps(
                    {
                        "event_id": "frame-0",
                        "kind": "observation",
                        "sequence": 0,
                        "time_ms": 1,
                        "payload": {
                            "state": "ready",
                            "label_source": "human",
                            "image": "frame-0000.png",
                            "sha256": hashlib.sha256(image).hexdigest(),
                        },
                    }
                ),
                encoding="utf-8",
            )
            (examples / "session.json").write_text(
                json.dumps(
                    {
                        "schema_version": 1,
                        "session_id": "test",
                        "clock_origin": "monotonic",
                        "events_file": "events.jsonl",
                    }
                ),
                encoding="utf-8",
            )
            pack = new_pack("test", "Teaching", "game")
            pack["fixtures"] = ["examples/session.json"]
            pack["skills"] = [
                {
                    "id": "wait",
                    "when": {"state": "ready"},
                    "propose": {"type": "wait", "parameters": {"duration_ms": 100}},
                }
            ]
            saved = save_pack(pack, root / "library", source)
            self.assertEqual((saved / "examples/frame-0000.png").read_bytes(), image)
            report = evaluate(saved, saved / "examples/session.json")
            self.assertEqual(report["proposals_by_skill"], {"wait": 1})
            self.assertEqual(report["dispatches"], 0)
            pack = load_pack(saved)
            pack["version"] = "0.1.1"
            newer = save_pack(pack, root / "library", saved)
            self.assertTrue((newer / "examples/frame-0000.png").exists())
            from game_pack_workshop import Workshop

            window = Workshop(root / "library")
            window.withdraw()
            try:
                window.open_path(newer / "pack.json", confirm=False)
                self.assertEqual(
                    Path(window.session.get()), newer / "examples/session.json"
                )
                window.fields["version"].set("0.1.3")
                window.save()
                self.assertEqual(
                    Path(window.session.get()).parent.parent, window.saved_path.parent
                )
                window.run()
                window.future.result(timeout=10)
                window.after_cancel(window.poll_id)
                window.poll()
                self.assertIn("1 proposals", window.result.get("1.0", "end"))
            finally:
                window.worker.shutdown(wait=True)
                window.destroy()
            (source / "examples/frame-0000.png").write_bytes(b"tampered")
            pack["version"] = "0.1.2"
            with self.assertRaises(PackError):
                save_pack(pack, root / "library", source)
            self.assertFalse((root / "library/test/0.1.2").exists())
            pack["fixtures"] = ["../outside.json"]
            with self.assertRaises(PackError):
                save_pack(pack, root / "library", source)


if __name__ == "__main__":
    unittest.main()
