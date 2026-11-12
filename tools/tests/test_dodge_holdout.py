import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from dodge_holdout import DEVELOPMENT_SEEDS, LEGACY_VALIDATION_SEEDS, reserve_holdout


class DodgeHoldoutTests(unittest.TestCase):
    def test_new_holdouts_are_predeclared_distinct_and_never_reused(self):
        with tempfile.TemporaryDirectory() as temp:
            workspace = Path(temp)
            root = workspace / ".local/dodge-curricula"
            first, second = root / "first", root / "second"
            first.mkdir(parents=True)
            second.mkdir()
            values = iter([118, 26871, *range(1001, 1021)])
            first_path = reserve_holdout(workspace, first, draw=lambda: next(values))
            second_path = reserve_holdout(workspace, second, draw=lambda: next(values))
            a = json.loads(first_path.read_text())["seeds"]
            b = json.loads(second_path.read_text())["seeds"]
            self.assertEqual(len(a), 10)
            self.assertEqual(len(b), 10)
            self.assertTrue(set(a).isdisjoint(b))
            self.assertTrue(
                (set(a) | set(b)).isdisjoint(
                    DEVELOPMENT_SEEDS | LEGACY_VALIDATION_SEEDS
                )
            )
            with self.assertRaises(ValueError):
                reserve_holdout(workspace, first)

    def test_refuses_invalid_registry_without_replacing_it(self):
        with tempfile.TemporaryDirectory() as temp:
            workspace = Path(temp)
            curriculum = workspace / ".local/dodge-curricula/first"
            curriculum.mkdir(parents=True)
            registry = workspace / ".local/dodge-holdouts/registry.json"
            registry.parent.mkdir(parents=True)
            registry.write_text('{"schema_version": 1, "used_seeds": [false]}')
            with self.assertRaises(ValueError):
                reserve_holdout(workspace, curriculum)
            self.assertFalse((curriculum / "validation-manifest.json").exists())
            self.assertIn("false", registry.read_text())

    def test_larger_reservation_is_bounded_and_disjoint(self):
        with tempfile.TemporaryDirectory() as temp:
            workspace = Path(temp)
            curriculum = workspace / ".local/dodge-curricula/large"
            curriculum.mkdir(parents=True)
            values = iter(range(2001, 2017))
            path = reserve_holdout(
                workspace, curriculum, count=16, draw=lambda: next(values)
            )
            self.assertEqual(len(json.loads(path.read_text())["seeds"]), 16)
            other = workspace / ".local/dodge-curricula/other"
            other.mkdir()
            for invalid in (0, 33, True):
                with self.assertRaises(ValueError):
                    reserve_holdout(workspace, other, count=invalid)


if __name__ == "__main__":
    unittest.main()
