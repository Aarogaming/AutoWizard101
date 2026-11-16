import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from w101_shape_clearance import overlaps


class ShapeClearanceTests(unittest.TestCase):
    def hit(
        self,
        start,
        end,
        center=(0, 0, 0),
        rotation=(1, 0, 0, 0, 1, 0, 0, 0, 1),
        scale=1,
        extent=(1, 1, 1),
        radius=0,
        height=0,
    ):
        return overlaps(start, end, center, rotation, scale, extent, radius, height)

    def test_crossing_and_outside(self):
        self.assertTrue(self.hit((-3, 0, 0), (3, 0, 0)))
        self.assertFalse(self.hit((-3, 3, 0), (3, 3, 0)))

    def test_headroom_changes_result(self):
        self.assertFalse(self.hit((-3, 0, 0), (3, 0, 0), center=(0, 0, 10), height=8))
        self.assertTrue(self.hit((-3, 0, 0), (3, 0, 0), center=(0, 0, 10), height=10))

    def test_rotated_thin_box_rejects_aabb_corner_false_positive(self):
        q = 2**-0.5
        self.assertFalse(
            self.hit(
                (2, -2, 0),
                (2.1, -2, 0),
                rotation=(q, -q, 0, q, q, 0, 0, 0, 1),
                extent=(4, 0.1, 1),
            )
        )

    def test_footprint_expansion(self):
        self.assertFalse(self.hit((-3, 2, 0), (3, 2, 0), radius=0.5))
        self.assertTrue(self.hit((-3, 2, 0), (3, 2, 0), radius=1))

    def test_singular_rejected(self):
        with self.assertRaises(ValueError):
            self.hit((0, 0, 0), (1, 1, 1), rotation=(0,) * 9)

    def test_negative_scale_is_supported(self):
        self.assertTrue(self.hit((-3, 0, 0), (3, 0, 0), scale=-2))

    def test_invalid_body_rejected(self):
        with self.assertRaises(ValueError):
            self.hit((0, 0, 0), (1, 1, 1), height=-1)


if __name__ == "__main__":
    unittest.main()
