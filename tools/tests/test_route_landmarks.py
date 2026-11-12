import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import route_landmarks as module


class RouteLandmarkTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        module.cv2.setNumThreads(1)
        self.image = self.root / "scene.png"
        random = module.np.random.default_rng(37)
        module.cv2.imwrite(
            str(self.image), random.integers(0, 256, (380, 640), dtype=module.np.uint8)
        )

    def tearDown(self):
        self.temp.cleanup()

    def test_same_pixels_are_only_a_candidate_never_action_authority(self):
        for detector in ("orb", "sift"):
            with self.subTest(detector=detector):
                matcher = module.Landmarks(
                    [{"id": "bridge", "image": str(self.image)}], detector
                )
                result = matcher.recognize(self.image)
                self.assertEqual("bridge", result["landmarkCandidate"])
                self.assertFalse(result["movementAuthorized"])
                self.assertFalse(result["arrivalVerified"])
                self.assertFalse(result["liveQualified"])

    def test_unknown_detector_is_rejected(self):
        with self.assertRaises(ValueError):
            module.Landmarks([{"id": "bridge", "image": str(self.image)}], "unknown")

    def test_ambiguous_references_remain_unknown(self):
        matcher = module.Landmarks(
            [{"id": name, "image": str(self.image)} for name in ("bridge", "tunnel")]
        )
        self.assertEqual("Unknown", matcher.recognize(self.image)["landmarkCandidate"])

    def test_featureless_frame_is_unknown(self):
        blank = self.root / "blank.png"
        module.cv2.imwrite(
            str(blank), module.np.zeros((380, 640), dtype=module.np.uint8)
        )
        matcher = module.Landmarks([{"id": "bridge", "image": str(self.image)}])
        self.assertEqual("Unknown", matcher.recognize(blank)["landmarkCandidate"])

    def test_invalid_reference_sets_are_rejected(self):
        for rows in ([], [{"id": "same", "image": str(self.image)}] * 2):
            with self.assertRaises(ValueError):
                module.Landmarks(rows)

    def test_alignment_reports_image_translation_not_action_authority(self):
        points = module.np.float32(
            [(x, y) for x in (80, 200, 440, 560) for y in (80, 190, 300)]
        )
        transform = module.np.float64([[1, 0, 64], [0, 1, 0], [0, 0, 1]])
        result = module.geometry(
            points, points + (64, 0), module.np.ones(12), transform
        )
        self.assertTrue(result["alignmentCandidate"])
        self.assertAlmostEqual(0.1, result["referenceCenterShiftX"])
        self.assertEqual("image-space-only-not-world-bearing", result["meaning"])

    def test_localized_matches_do_not_establish_alignment(self):
        points = module.np.float32(
            [(x, y) for x in (10, 12, 14, 16) for y in (10, 12, 14)]
        )
        result = module.geometry(points, points, module.np.ones(12), module.np.eye(3))
        self.assertFalse(result["alignmentCandidate"])
        self.assertIsNone(result["referenceCenterShiftX"])

    def test_reflection_and_nonfinite_alignment_rejected(self):
        points = module.np.float32(
            [(x, y) for x in (80, 200, 440, 560) for y in (80, 190, 300)]
        )
        for transform in (
            module.np.float64([[-1, 0, 640], [0, 1, 0], [0, 0, 1]]),
            module.np.full((3, 3), module.np.nan),
        ):
            self.assertFalse(
                module.geometry(points, points, module.np.ones(12), transform)[
                    "alignmentCandidate"
                ]
            )

    def test_extreme_aspect_ratio_rejected_before_resizing(self):
        path = self.root / "portrait.png"
        module.cv2.imwrite(
            str(path), module.np.zeros((3000, 100), dtype=module.np.uint8)
        )
        with self.assertRaises(ValueError):
            module.features(path)


if __name__ == "__main__":
    unittest.main()
