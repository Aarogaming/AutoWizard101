import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from w101_bcd_evidence import classify, classify_triangle, primitive_bounds


class BcdEvidenceTests(unittest.TestCase):
    def test_long_thin_box_does_not_expand_all_axes_to_length(self):
        b = primitive_bounds(
            dict(
                type="BOX",
                params=[1000, 2, 3],
                rotation=[1, 0, 0, 0, 1, 0, 0, 0, 1],
                translation=[0, 0, 0],
                scale=1,
            )
        )
        self.assertEqual([1000, 2, 3], b["max"])
        self.assertEqual([-1000, -2, -3], b["min"])

    def test_rotated_box_is_enclosed(self):
        p = dict(
            type="BOX",
            params=[2, 3, 4],
            rotation=[0, -1, 0, 1, 0, 0, 0, 0, 1],
            translation=[10, 20, 30],
            scale=2,
        )
        b = primitive_bounds(p)
        for x in [-2, 2]:
            for y in [-3, 3]:
                for z in [-4, 4]:
                    point = [10 - 2 * y, 20 + 2 * x, 30 + 2 * z]
                    self.assertTrue(
                        all(a <= v <= c for a, v, c in zip(b["min"], point, b["max"]))
                    )

    def test_capsule_includes_endcaps(self):
        b = primitive_bounds(
            dict(
                type="CYLINDER",
                params=[10, 2],
                rotation=[1, 0, 0, 0, 1, 0, 0, 0, 1],
                translation=[0, 0, 0],
                scale=1,
            )
        )
        self.assertEqual(b["max"][2], 12)
        self.assertEqual(b["max"][0], 2)
        self.assertEqual(b["max"][1], 2)

    def test_capsule_rotates_long_axis_without_swapping_length_radius(self):
        b = primitive_bounds(
            dict(
                type="CYLINDER",
                params=[100, 3],
                rotation=[0, 0, 1, 0, 1, 0, -1, 0, 0],
                translation=[0, 0, 0],
                scale=1,
            )
        )
        self.assertEqual([103, 3, 3], b["max"])

    def test_plane_remains_unresolved(self):
        self.assertIsNone(primitive_bounds(dict(type="PLANE")))

    def test_bad_dimensions_rejected(self):
        with self.assertRaises(ValueError):
            primitive_bounds(
                dict(
                    type="SPHERE",
                    params=[-1],
                    rotation=[1] * 9,
                    translation=[0] * 3,
                    scale=1,
                )
            )

    def test_ramp(self):
        r = classify_triangle([[0, 0, 0], [10, 0, 5], [0, 10, 0]], 0.6)
        self.assertEqual(r["role"], "support-candidate")
        self.assertEqual(r["grade"], 0.5)

    def test_steep(self):
        self.assertEqual(
            classify_triangle([[0, 0, 0], [10, 0, 20], [0, 10, 0]], 0.6)["role"],
            "steep-barrier-candidate",
        )

    def test_vertical(self):
        self.assertEqual(
            classify_triangle([[0, 0, 0], [0, 0, 10], [0, 10, 0]], 0.6)["role"],
            "vertical-barrier-candidate",
        )

    def test_degenerate(self):
        self.assertEqual(
            classify_triangle([[0, 0, 0]] * 3, 0.6)["role"], "degenerate-unresolved"
        )

    def test_invalid(self):
        with self.assertRaises(ValueError):
            classify_triangle([[float("nan"), 0, 0]] * 3, 0.6)

    def test_no_implicit_clearance(self):
        r = classify(
            dict(
                zones=[
                    dict(
                        archive="test",
                        memberSha256="hash",
                        meshes=[],
                        unsupportedPrimitives=[],
                    )
                ]
            ),
            0.6,
        )
        self.assertFalse(r["zones"][0]["coverageComplete"])
        self.assertFalse(r["runtimeEnabled"])


if __name__ == "__main__":
    unittest.main()
