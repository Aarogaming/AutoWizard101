import json
import subprocess
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import w101_bg_facts as worker  # noqa: E402


def target(
    image: str = "d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a",
):
    return {"handle": 42, "pid": 123, "started_utc_ticks": 456, "image_sha256": image}


class BackgroundFactsContractTests(unittest.TestCase):
    def test_request_requires_exact_target_and_bounded_budget(self):
        ok, reason = worker.validate_request({"schemaVersion": 1, "target": target()})
        self.assertTrue(ok, reason)
        self.assertFalse(worker.validate_request({"schemaVersion": 1})[0])
        self.assertFalse(worker.validate_request({"schemaVersion": 1, "target": {}})[0])
        bad = {"schemaVersion": 1, "target": target("short")}
        self.assertEqual("invalid-image-pin", worker.validate_request(bad)[1])
        self.assertEqual(
            "invalid-interval",
            worker.validate_request({**bad, "target": target(), "intervalMs": 6000})[1],
        )

    def test_no_foreground_field_is_required_or_consumed(self):
        base = {"schemaVersion": 1, "target": target()}
        with_focus = {**base, "foreground": False, "focused": False}
        self.assertEqual(
            worker.validate_request(base), worker.validate_request(with_focus)
        )

    def test_mapping_is_candidate_only_and_rejects_unknown_ids(self):
        facts = worker.map_observations(
            [
                {"id": "health", "value": "4920"},
                {"id": "made.up", "value": "1"},
                {
                    "id": "zone.candidate",
                    "value": "Khrysalis/Interiors/KR_Z10_I05_AlluviumCave",
                },
            ],
            639000000000000000,
            "r15-archived",
        )
        self.assertEqual(["health", "zone.candidate"], [f["id"] for f in facts])
        for fact in facts:
            self.assertEqual("candidate", fact["qualification"])
            self.assertIn("not live-qualified", fact["note"])

    def test_inert_run_never_publishes_facts(self):
        result = worker.run({"schemaVersion": 1, "target": target()})
        self.assertEqual("reader-not-qualified", result["code"])
        self.assertEqual([], result["facts"])
        self.assertFalse(result["readerQualified"])

    def test_self_test_is_offline_and_schema_stable(self):
        result = worker.self_test()
        self.assertEqual("self-test", result["status"])
        self.assertFalse(result["readerQualified"])
        self.assertEqual([], result["facts"])

    def test_cli_self_test_process_is_deterministic(self):
        proc = subprocess.run(
            [
                sys.executable,
                str(Path(__file__).resolve().parents[1] / "w101_bg_facts.py"),
            ],
            input=json.dumps({"selfTest": True}),
            capture_output=True,
            text=True,
            timeout=15,
        )
        self.assertEqual(0, proc.returncode)
        self.assertEqual("self-test", json.loads(proc.stdout)["status"])


if __name__ == "__main__":
    unittest.main()
