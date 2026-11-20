import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from build_dodge_exploration_pack import build
from game_packs import load_pack

SOURCE = (
    Path(__file__).resolve().parents[2]
    / "tools/fixtures/game_packs/dodge-context-v12/pack.json"
)


class ExplorationPackTests(unittest.TestCase):
    def test_builds_a_draft_and_refuses_model_sources(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            result = load_pack(build(root, SOURCE, root / ".local/packs"))
            self.assertEqual(
                result["context"]["avoidance_policy"], "directional-explore-v35"
            )
            self.assertEqual(result["exploration_provenance"]["exploration_rate"], 0.2)
            source = root / "modeled.json"
            source.write_text(
                json.dumps({**load_pack(SOURCE), "experimental_model": {}})
            )
            with self.assertRaisesRegex(ValueError, "non-model"):
                build(root, source, root / ".local/packs")


if __name__ == "__main__":
    unittest.main()
