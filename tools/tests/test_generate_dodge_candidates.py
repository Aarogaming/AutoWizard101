from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from game_packs import PackError, load_pack, validate_context
from generate_dodge_candidates import generate

SOURCE = (
    Path(__file__).resolve().parents[1]
    / "fixtures"
    / "game_packs"
    / "dodge-context-v12"
    / "pack.json"
)


class CandidateGenerationTests(unittest.TestCase):
    def test_two_phase_planner_has_a_distinct_version(self):
        with tempfile.TemporaryDirectory() as temp:
            candidate = load_pack(
                generate(SOURCE, temp, [700], [50], "directional-two-phase-v31")[0]
            )
            self.assertEqual(candidate["version"], "0.9.0-h700-r50")
            self.assertEqual(
                candidate["context"]["avoidance_policy"], "directional-two-phase-v31"
            )

    def test_pulse_timing_planner_has_a_distinct_version(self):
        with tempfile.TemporaryDirectory() as temp:
            candidate = load_pack(
                generate(
                    SOURCE, temp, [1000], [50], "directional-pulse-v32", margins=[60]
                )[0]
            )
            self.assertEqual(candidate["version"], "0.10.0-h1000-r50-m60")
            self.assertEqual(
                candidate["context"]["avoidance_policy"], "directional-pulse-v32"
            )

    def test_explicit_edge_margins_are_bounded_and_versioned(self):
        with tempfile.TemporaryDirectory() as temp:
            paths = generate(
                SOURCE, temp, [700], [50], "directional-v26", margins=[60, 100]
            )
            packs = [load_pack(path) for path in paths]
            self.assertEqual(
                [p["context"]["constraints"]["edge_margin_px"] for p in packs],
                [60, 100],
            )
            self.assertTrue(packs[0]["version"].endswith("-m60"))
            with self.assertRaises(ValueError):
                generate(SOURCE, temp, [700], [50], "directional-v26", margins=[101])

    def test_configurable_planning_generates_distinct_bounded_weights(self):
        with tempfile.TemporaryDirectory() as temp:
            paths = generate(
                SOURCE,
                temp,
                [700],
                [50],
                "directional-configurable-v30",
                [50, 200],
                [50, 200],
            )
            self.assertEqual(len(paths), 4)
            packs = [load_pack(path) for path in paths]
            self.assertEqual(
                {p["context"]["constraints"]["risk_weight_percent"] for p in packs},
                {50, 200},
            )
            self.assertEqual(
                {p["context"]["constraints"]["edge_weight_percent"] for p in packs},
                {50, 200},
            )
            self.assertEqual(len({p["version"] for p in packs}), 4)
            with self.assertRaises(ValueError):
                generate(
                    SOURCE,
                    temp,
                    [700],
                    [50],
                    "directional-configurable-v30",
                    [401],
                    [100],
                )
            with self.assertRaises(ValueError):
                generate(SOURCE, temp, [700], [50], "directional-v26", [200], [100])
            for invalid in (True, 401):
                packs[0]["context"]["constraints"]["risk_weight_percent"] = invalid
                with self.assertRaises(PackError):
                    validate_context(packs[0]["context"])

    def test_generates_directional_planning_variant(self):
        with tempfile.TemporaryDirectory() as temp:
            path = generate(SOURCE, temp, [700], [50], "directional-v26")[0]
            candidate = load_pack(path)
            self.assertEqual(
                candidate["context"]["avoidance_policy"], "directional-v26"
            )
            self.assertEqual(candidate["version"], "0.4.0-h700-r50")
            with self.assertRaises(ValueError):
                generate(SOURCE, temp, [700], [50], "unknown")

    def test_guarded_directional_variant_has_its_own_pack_version(self):
        with tempfile.TemporaryDirectory() as temp:
            path = generate(SOURCE, temp, [700], [50], "directional-guard-v27")[0]
            candidate = load_pack(path)
            self.assertEqual(
                candidate["context"]["avoidance_policy"], "directional-guard-v27"
            )
            self.assertEqual(candidate["version"], "0.5.0-h700-r50")

    def test_uncertain_motion_carry_is_an_explicit_pack_variant(self):
        with tempfile.TemporaryDirectory() as temp:
            path = generate(SOURCE, temp, [700], [50], "directional-carry-v28")[0]
            candidate = load_pack(path)
            self.assertEqual(
                candidate["context"]["avoidance_policy"], "directional-carry-v28"
            )
            self.assertEqual(candidate["version"], "0.6.0-h700-r50")

    def test_smoothed_motion_is_an_explicit_pack_variant(self):
        with tempfile.TemporaryDirectory() as temp:
            path = generate(SOURCE, temp, [700], [50], "directional-smooth-v29")[0]
            candidate = load_pack(path)
            self.assertEqual(
                candidate["context"]["avoidance_policy"], "directional-smooth-v29"
            )
            self.assertEqual(candidate["version"], "0.7.0-h700-r50")

    def test_versions_vary_only_declared_policy_parameters(self):
        with tempfile.TemporaryDirectory() as temp:
            paths = generate(SOURCE, temp, [350, 700], [50])
            self.assertEqual(len(paths), 2)
            first, second = [load_pack(path) for path in paths]
            self.assertEqual(
                first["context"]["constraints"]["prediction_horizon_ms"], 350
            )
            self.assertEqual(
                second["context"]["constraints"]["prediction_horizon_ms"], 700
            )
            self.assertEqual(first["context"]["constraints"]["threat_radius_px"], 50)
            self.assertEqual(
                first["context"]["avoidance_policy"], "center-configurable-v20"
            )
            self.assertEqual(first["actions"], second["actions"])
            self.assertEqual(first["skills"], second["skills"])
            with self.assertRaises(ValueError):
                generate(SOURCE, temp, [350, 900], [50])
            self.assertFalse(
                (Path(temp) / first["pack_id"] / "0.3.0-h900-r50").exists()
            )

    def test_rejects_invalid_or_excessive_candidate_sets(self):
        with tempfile.TemporaryDirectory() as temp:
            for horizons, radii in [
                ([99], [50]),
                ([350, 350], [50]),
                (list(range(100, 117)), [50]),
                ([350], [101]),
            ]:
                with self.assertRaises(ValueError):
                    generate(SOURCE, temp, horizons, radii)
            self.assertEqual(list(Path(temp).iterdir()), [])


if __name__ == "__main__":
    unittest.main()
