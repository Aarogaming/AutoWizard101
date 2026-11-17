import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import build_combat_ui_fixture as fixture_tool  # noqa: E402


class CombatUiFixtureTests(unittest.TestCase):
    def test_extract_is_deterministic_and_counts_visibility(self):
        with tempfile.TemporaryDirectory() as tmp:
            source = Path(tmp) / "obs.jsonl"
            source.write_text(
                "\n".join(
                    [
                        json.dumps({"kind": "ready"}),
                        json.dumps(
                            {
                                "kind": "tree",
                                "nodes": [
                                    {"name": "Health", "visible": True},
                                    {"name": "Pips", "visible": False},
                                ],
                            }
                        ),
                        json.dumps(
                            {
                                "kind": "tree",
                                "nodes": [
                                    {"name": "Health", "visible": True},
                                    {"name": "Health", "visible": False},
                                ],
                            }
                        ),
                    ]
                ),
                encoding="utf-8",
            )
            first = fixture_tool.extract([source])
            second = fixture_tool.extract([source])
            self.assertEqual(first, second)
            self.assertEqual(2, first["treeSamples"])
            by_name = {n["name"]: n for n in first["nodes"]}
            self.assertEqual(3, by_name["Health"]["observations"])
            self.assertEqual(2, by_name["Health"]["visible"])
            self.assertEqual(1, by_name["Health"]["hidden"])
            self.assertEqual(1, by_name["Pips"]["hidden"])
            self.assertEqual(64, len(first["sources"][0]["sha256"]))

    def test_cli_writes_fixture(self):
        with tempfile.TemporaryDirectory() as tmp:
            source = Path(tmp) / "obs.jsonl"
            source.write_text(
                json.dumps(
                    {"kind": "tree", "nodes": [{"name": "Hand", "visible": True}]}
                )
                + "\n",
                encoding="utf-8",
            )
            out = Path(tmp) / "fixture.json"
            proc = subprocess.run(
                [
                    sys.executable,
                    str(
                        Path(__file__).resolve().parents[1]
                        / "build_combat_ui_fixture.py"
                    ),
                    str(out),
                    str(source),
                ],
                capture_output=True,
                text=True,
                timeout=15,
            )
            self.assertEqual(0, proc.returncode, proc.stderr)
            data = json.loads(out.read_text(encoding="utf-8"))
            self.assertEqual("maelstrom-combat-ui-vocabulary-v1", data["kind"])
            self.assertEqual(["Hand"], [n["name"] for n in data["nodes"]])


if __name__ == "__main__":
    unittest.main()
