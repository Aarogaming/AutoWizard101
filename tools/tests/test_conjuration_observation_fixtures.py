import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from conjuration_observation_fixtures import load_and_replay, main, replay_fixture

FIXTURES = Path(__file__).resolve().parents[1] / "fixtures/conjuration_observations"


def fixture():
    return json.loads((FIXTURES / "synthetic-pairs.json").read_text())


class ConjurationObservationFixtureTests(unittest.TestCase):
    def test_maximum_board_identifier_has_a_schema_compatible_receipt(self):
        data = fixture()
        original = data["board_id"]
        data["board_id"] = "b" * 128
        for event in data["events"]:
            if event["kind"] == "observation":
                event["board"]["board_id"] = data["board_id"]
            elif event["kind"] == "action_receipt":
                event["proposal_id"] = event["proposal_id"].replace(
                    original, data["board_id"]
                )
        self.assertTrue(replay_fixture(data)["last_board_cleared"])
        schema = json.loads((FIXTURES / "schema-v1.json").read_text())
        maximum = schema["properties"]["events"]["items"]["oneOf"][1]["properties"][
            "proposal_id"
        ]["maxLength"]
        self.assertTrue(
            all(
                len(e["proposal_id"]) <= maximum
                for e in data["events"]
                if e["kind"] == "action_receipt"
            )
        )

    def test_pairs_and_triples_keep_provenance_and_verify_observed_groups(self):
        for name in ("synthetic-pairs", "synthetic-triples"):
            with self.subTest(name=name):
                report = load_and_replay(FIXTURES / (name + ".json"))
                self.assertTrue(report["last_board_cleared"])
                self.assertEqual(2, report["matched_groups_observed"])
                self.assertEqual(0, report["desktop_dispatch_count"])
                self.assertFalse(report["game_win_verified"])
                self.assertFalse(report["live_qualified"])
                self.assertEqual(64, len(report["sha256_fixture"]))
                self.assertEqual(report, load_and_replay(FIXTURES / (name + ".json")))

    def test_no_receipts_are_manufactured(self):
        data = fixture()
        data["events"] = [e for e in data["events"] if e["kind"] != "action_receipt"]
        for event in data["events"]:
            event.pop("expected", None)
        report = replay_fixture(data)
        self.assertEqual(0, report["supplied_receipt_count"])
        self.assertEqual(1, report["proposal_count"])
        self.assertFalse(report["last_board_cleared"])

    def test_stale_revision_and_duplicate_slots_rejected(self):
        for mutation in ("revision", "slots", "positions", "hidden-identity"):
            data = fixture()
            cards = data["events"][0]["board"]["cards"]
            if mutation == "revision":
                data["events"].insert(1, copy.deepcopy(data["events"][0]))
            elif mutation == "slots":
                cards[1]["slot"] = cards[0]["slot"]
            elif mutation == "positions":
                cards[1]["x"], cards[1]["y"] = cards[0]["x"], cards[0]["y"]
            else:
                cards[0]["identity"] = "future-label"
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                replay_fixture(data)

    def test_reset_requires_fresh_board_identity_and_invalidates_old_receipt(self):
        data = fixture()
        data["events"] = [
            data["events"][0],
            {"kind": "reset", "time_ms": 110, "board_id": "new-board", "group_size": 2},
            data["events"][1],
        ]
        with self.assertRaises(ValueError):
            replay_fixture(data)
        data["events"] = data["events"][:2]
        data["events"].append(
            {
                "kind": "reset",
                "time_ms": 120,
                "board_id": data["board_id"],
                "group_size": 2,
            }
        )
        with self.assertRaises(ValueError):
            replay_fixture(data)

    def test_time_is_monotonic_finite_and_not_boolean(self):
        for value in (True, -1, float("inf"), float("nan"), 10**1000):
            data = fixture()
            data["events"][1]["time_ms"] = value
            with self.subTest(value=str(value)[:30]), self.assertRaises(ValueError):
                replay_fixture(data)
        data = fixture()
        data["events"][1]["time_ms"] = 0
        with self.assertRaises(ValueError):
            replay_fixture(data)

    def test_recorded_label_sources_are_hashed_but_not_called_pixel_recognition(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "evidence.bin"
            path.write_bytes(b"opaque-source-bytes-only-the-label-loader-hashes")
            data = fixture()
            data["synthetic"] = False
            for event in data["events"]:
                if event["kind"] == "observation":
                    event["source"] = {
                        "annotation_origin": "manual",
                        "image": path.name,
                        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                    }
            report = replay_fixture(data, Path(directory))
            self.assertEqual("recorded-label-replay", report["evidence_mode"])
            self.assertEqual(1, len(report["images"]))
            data["events"][0]["source"]["sha256"] = "0" * 64
            with self.assertRaises(ValueError):
                replay_fixture(data, Path(directory))

    def test_synthetic_provenance_cannot_claim_recorded_annotations(self):
        data = fixture()
        data["events"][0]["source"]["annotation_origin"] = "manual"
        with self.assertRaises(ValueError):
            replay_fixture(data)
        data = fixture()
        data["synthetic"] = False
        with self.assertRaises(ValueError):
            replay_fixture(data)

    def test_escaping_images_and_nonlocal_urls_rejected(self):
        for image in (
            "../outside.png",
            "/outside.png",
            "C:/outside.png",
            "https://example.invalid/image.png",
            "folder\\image.png",
        ):
            data = fixture()
            data["events"][0]["source"].update(image=image, sha256="a" * 64)
            with self.subTest(image=image), self.assertRaises(ValueError):
                replay_fixture(data)

    def test_wrong_expectation_fails_before_output_and_existing_report_is_preserved(
        self,
    ):
        with tempfile.TemporaryDirectory() as directory:
            source, output = (
                Path(directory) / "fixture.json",
                Path(directory) / "report.json",
            )
            data = fixture()
            data["events"][0]["expected"]["proposal_slot"] = "not-a-card"
            source.write_text(json.dumps(data))
            self.assertEqual(1, main([str(source), str(output)]))
            self.assertFalse(output.exists())
            source.write_text(json.dumps(fixture()))
            output.write_text("keep-existing")
            self.assertEqual(1, main([str(source), str(output)]))
            self.assertEqual("keep-existing", output.read_text())

    def test_duplicate_json_keys_oversize_and_unknown_fields_rejected(self):
        data = fixture()
        data["notes"] = {"unexpected": "object"}
        with self.assertRaises(ValueError):
            replay_fixture(data)
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "fixture.json"
            source.write_text('{"schema_version":1,"schema_version":1}')
            with self.assertRaises(ValueError):
                load_and_replay(source)
            source.write_bytes(b" " * (1048576 + 1))
            with self.assertRaises(ValueError):
                load_and_replay(source)
        data = fixture()
        data["script"] = "not executable"
        with self.assertRaises(ValueError):
            replay_fixture(data)
        data = fixture()
        data["events"] = [data["events"][0]] * 2001
        with self.assertRaises(ValueError):
            replay_fixture(data)


if __name__ == "__main__":
    unittest.main()
