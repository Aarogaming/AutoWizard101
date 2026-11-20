from copy import deepcopy
import json
import hashlib
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from compare_pack_versions import compare, compare_runs

PACK = Path(__file__).resolve().parents[1] / "fixtures/game_packs/dodge/pack.json"


class PackComparisonTests(unittest.TestCase):
    def test_native_comparison_rechecks_engine_response(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "channel").mkdir()
            evidence = {
                "id": 3,
                "kind": "action-result",
                "action": "move-left",
                "before": [200, 300],
                "player": [170, 300],
                "visible": True,
                "score": 1,
            }
            raw = json.dumps(evidence).encode()
            (root / "channel/response-0003.json").write_bytes(raw)
            action = {
                "request_id": 3,
                "action": "move-left",
                "input_status": "acknowledged",
                "outcome": "movement-observed",
                "visible": True,
                "score": 1,
                "evidence": "channel/response-0003.json",
                "evidence_sha256": hashlib.sha256(raw).hexdigest(),
            }
            report = {
                "schema_version": 1,
                "mode": "godot-native-test",
                "run_id": "native",
                "pack": {"Id": "dodge", "Version": "0.1.0", "Sha256": "abc"},
                "action_count": 1,
                "actions": [action],
            }
            (root / "report.json").write_text(json.dumps(report))
            result = compare_runs(root / "report.json", root / "report.json")
            self.assertEqual(result["first"]["outcomes"], {"movement-observed": 1})
            report["actions"][0]["outcome"] = "movement-not-observed"
            (root / "report.json").write_text(json.dumps(report))
            with self.assertRaises(ValueError):
                compare_runs(root / "report.json", root / "report.json")

    def test_test_run_comparison_checks_evidence_and_time(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            before, after = b"before", b"after"
            (root / "before.png").write_bytes(before)
            (root / "after.png").write_bytes(after)
            action = {
                "receipt": "dispatched",
                "source_capture_ms": 100,
                "down_ms": 110,
                "up_ms": 130,
                "after_capture_ms": 140,
                "outcome": "movement-observed",
                "before_file": "before.png",
                "after_file": "after.png",
                "before_sha256": hashlib.sha256(before).hexdigest(),
                "after_sha256": hashlib.sha256(after).hexdigest(),
            }
            report = {
                "schema_version": 1,
                "mode": "bounded_dodge_test",
                "run_id": "one",
                "pack": {"Id": "dodge", "Version": "0.1.0", "Sha256": "abc"},
                "action_count": 1,
                "actions": [action],
            }
            (root / "first.json").write_text(json.dumps(report))
            report["run_id"] = "two"
            report["pack"]["Version"] = "0.1.1"
            (root / "second.json").write_text(json.dumps(report))
            compared = compare_runs(root / "first.json", root / "second.json")
            self.assertEqual(compared["first"]["outcomes"], {"movement-observed": 1})
            (root / "after.png").write_bytes(b"modified")
            with self.assertRaises(ValueError):
                compare_runs(root / "first.json", root / "second.json")

    def test_compare_rebinding_on_same_observations(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            pack = json.loads(PACK.read_text())
            newer = deepcopy(pack)
            newer["version"] = "0.1.1"
            newer["actions"][0]["key"] = "A"
            (root / "pack.json").write_text(json.dumps(newer))
            (root / "session.json").write_text(
                json.dumps(
                    {
                        "schema_version": 1,
                        "session_id": "test",
                        "clock_origin": "test",
                        "events_file": "events.jsonl",
                    }
                )
            )
            (root / "events.jsonl").write_text(
                "\n".join(
                    json.dumps(
                        {
                            "event_id": str(i),
                            "kind": "observation",
                            "sequence": i,
                            "time_ms": i * 100,
                            "payload": {"state": state},
                        }
                    )
                    for i, state in enumerate(
                        ["dodge-advice-left", "unknown", "dodge-advice-right"]
                    )
                )
            )
            report = compare(PACK, root / "pack.json", root / "session.json")
            self.assertEqual(
                (report["observations"], report["different"], report["unchanged"]),
                (3, 1, 2),
            )
            self.assertEqual(
                report["examples"][0]["second"][0]["resolved_action"]["key"], "A"
            )
            self.assertEqual(report["verified_outcomes"], 0)
            newer["game_id"] = "different-game"
            (root / "pack.json").write_text(json.dumps(newer))
            with self.assertRaises(ValueError):
                compare(PACK, root / "pack.json", root / "session.json")
