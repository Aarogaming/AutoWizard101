import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from evaluate_game_pack import evaluate


class PackEvaluationTests(unittest.TestCase):
    def test_both_interaction_types_match_and_wrong_expectation_fails(self):
        root = Path(__file__).resolve().parents[1] / "fixtures/game_packs"
        for name in ("pet-dance", "potion-motion"):
            report = evaluate(
                root / name, root / "session.json", root / name / "expected.json"
            )
            self.assertTrue(report["expectations"]["passed"])
            self.assertEqual(report["unmatched_observations"], 1)
            self.assertEqual(report["dispatches"], 0)
        with tempfile.TemporaryDirectory() as directory:
            expected = Path(directory) / "wrong.json"
            expected.write_text(json.dumps([]), encoding="utf-8")
            report = evaluate(root / "pet-dance", root / "session.json", expected)
            self.assertFalse(report["expectations"]["passed"])


if __name__ == "__main__":
    unittest.main()
