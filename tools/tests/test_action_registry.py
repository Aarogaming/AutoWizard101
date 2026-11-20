from copy import deepcopy
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from game_packs import PackError, load_pack, propose_for_observation, validate_pack
from evaluate_game_pack import evaluate

PACK = Path(__file__).resolve().parents[1] / "fixtures/game_packs/dodge/pack.json"


class ActionRegistryTests(unittest.TestCase):
    def test_registry_resolves_game_action_to_bounded_binding(self):
        pack = load_pack(PACK)
        result = propose_for_observation(pack, {"state": "dodge-advice-left"})
        self.assertEqual(result[0]["proposal"]["parameters"]["action_id"], "move-left")
        self.assertEqual(result[0]["resolved_action"]["key"], "LEFT")
        self.assertEqual(result[0]["resolved_action"]["duration_ms"], 120)
        self.assertEqual(propose_for_observation(pack, {"state": "unknown"}), [])
        self.assertEqual(
            propose_for_observation(
                pack, {"state": "dodge-advice-left", "stale": True}
            ),
            [],
        )
        self.assertEqual(
            propose_for_observation(pack, {"state": "dodge-advice-hold"}), []
        )
        pack["skills"][0]["when"]["state"] = "unknown"
        self.assertEqual(propose_for_observation(pack, {"state": "unknown"}), [])

    def test_rejects_unsafe_or_ambiguous_registry(self):
        original = load_pack(PACK)
        for change in ("duplicate", "missing", "duration", "key", "unknown_field"):
            pack = deepcopy(original)
            if change == "duplicate":
                pack["actions"].append(deepcopy(pack["actions"][0]))
            elif change == "missing":
                pack["actions"].pop(0)
            elif change == "duration":
                pack["actions"][0]["duration_ms"] = 1001
            elif change == "key":
                pack["actions"][0]["key"] = "LEFT; do something"
            else:
                pack["actions"][0]["command"] = "bad"
            with self.subTest(change=change), self.assertRaises(PackError):
                validate_pack(pack)

    def test_replay_resolves_actions_without_claiming_outcomes(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "session.json").write_text(
                json.dumps(
                    {
                        "schema_version": 1,
                        "session_id": "dodge-test",
                        "clock_origin": "fixture",
                        "events_file": "events.jsonl",
                    }
                )
            )
            (root / "events.jsonl").write_text(
                "\n".join(
                    json.dumps(
                        {
                            "event_id": f"obs-{i}",
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
                + "\n"
            )
            result = evaluate(PACK, root / "session.json")
            self.assertEqual(result["observations"], 3)
            self.assertEqual(result["unmatched_observations"], 1)
            self.assertEqual(result["unmatched_states"], {"unknown": 1})
            self.assertEqual(
                result["proposals_by_skill"], {"avoid-left": 1, "avoid-right": 1}
            )
            self.assertEqual(result["dispatches"], 0)
            self.assertEqual(result["verified_outcomes"], 0)
            expected = [
                {
                    "event_id": "obs-0",
                    **propose_for_observation(
                        load_pack(PACK), {"state": "dodge-advice-left"}
                    )[0],
                },
                {
                    "event_id": "obs-2",
                    **propose_for_observation(
                        load_pack(PACK), {"state": "dodge-advice-right"}
                    )[0],
                },
            ]
            (root / "expected.json").write_text(json.dumps(expected))
            self.assertTrue(
                evaluate(PACK, root / "session.json", root / "expected.json")[
                    "expectations"
                ]["passed"]
            )
            expected[0]["resolved_action"]["key"] = "A"
            (root / "expected.json").write_text(json.dumps(expected))
            with self.assertRaises(ValueError):
                evaluate(PACK, root / "session.json", root / "expected.json")


if __name__ == "__main__":
    unittest.main()
