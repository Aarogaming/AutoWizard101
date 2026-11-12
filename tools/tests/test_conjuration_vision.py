"""Procedural pixel fixtures, not Wizard101 artwork or live qualification."""

from copy import deepcopy
from dataclasses import asdict
import hashlib
import io
import json
from pathlib import Path
import random
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from conjuration_memory import MemoryMatch
from conjuration_vision import (
    ConjurationVision,
    VisionProfile,
    decode_image,
    load_image,
)

try:
    from PIL import Image, ImageDraw
    import numpy as np
except ImportError:
    Image = ImageDraw = np = None

ROOT = Path(__file__).resolve().parents[2]
PROFILE = ROOT / "tools/fixtures/conjuration_vision/synthetic-profile.json"


def synthetic_gallery():
    """Four distinctive positive patterns; inactive is never an empty background."""
    images = {}
    colors = {
        "back": (30, 50, 170),
        "face-a": (180, 40, 30),
        "face-b": (30, 140, 50),
        "inactive": (90, 30, 100),
    }
    for key, color in colors.items():
        image = Image.new("RGB", (72, 96), color)
        draw = ImageDraw.Draw(image)
        draw.rectangle((1, 1, 70, 94), outline=(230, 200, 90), width=4)
        if key == "back":
            for row in range(3):
                draw.rectangle(
                    (16, 15 + row * 23, 55, 25 + row * 23), fill=(90, 140, 230)
                )
        elif key == "face-a":
            draw.polygon([(36, 15), (60, 75), (12, 75)], fill=(240, 190, 40))
        elif key == "face-b":
            draw.ellipse((14, 24, 58, 68), fill=(100, 210, 240))
        else:
            draw.line((15, 25, 57, 71), fill=(230, 170, 230), width=12)
            draw.line((57, 25, 15, 71), fill=(230, 170, 230), width=12)
        images[key] = image
    return images


def synthetic_board(profile, gallery, keys, scale=1):
    """Render native test pixels then scale the complete frame, including margins."""
    image = Image.new("RGB", profile.reference_size, (17, 22, 27))
    bx, by, bw, bh = profile.board_roi
    for slot, key in zip(profile.slots, keys, strict=True):
        x, y, w, h = slot.roi
        left, top = round((bx + x * bw) * image.width), round(
            (by + y * bh) * image.height
        )
        right, bottom = round((bx + (x + w) * bw) * image.width), round(
            (by + (y + h) * bh) * image.height
        )
        image.paste(gallery[key].resize((right - left, bottom - top)), (left, top))
    return image.resize(
        tuple(round(value * scale) for value in image.size), Image.Resampling.LANCZOS
    )


@unittest.skipIf(
    Image is None or np is None, "Existing offline Pillow/NumPy environment required"
)
class ConjurationVisionTests(unittest.TestCase):
    def setUp(self):
        self.data = json.loads(PROFILE.read_text(encoding="utf-8"))
        self.profile = VisionProfile.from_dict(self.data)
        self.gallery = synthetic_gallery()
        self.player = ConjurationVision(self.profile, self.gallery)

    def frame(self, keys=("back", "face-a", "face-b", "inactive"), scale=1):
        return synthetic_board(self.profile, self.gallery, keys, scale)

    def observe(
        self,
        image,
        revision=1,
        time_ms=0,
        phase="active",
        player=None,
        board_id="board-1",
    ):
        return (player or self.player).observe(
            image, board_id=board_id, revision=revision, time_ms=time_ms, phase=phase
        )

    def stable(self, image, player=None, phase="active"):
        first = self.observe(image, player=player, phase=phase)
        self.assertTrue(all(card.state == "unknown" for card in first.board.cards))
        return self.observe(image, 2, 60, player=player, phase=phase)

    def test_positive_states_require_stable_pixels_and_serialize_with_evidence(self):
        result = self.stable(self.frame())
        self.assertEqual(
            [card.state for card in result.board.cards],
            ["hidden", "face_up", "face_up", "removed"],
        )
        self.assertEqual(
            [card.identity for card in result.board.cards],
            [None, "synthetic-a", "synthetic-b", None],
        )
        self.assertEqual(result.phase_source, "caller-supplied-not-detected")
        self.assertTrue(all(slot.reason == "accepted" for slot in result.slots))
        encoded = json.loads(json.dumps(asdict(result)))
        self.assertEqual(encoded["board"]["board_id"], "board-1")
        self.assertEqual(len(encoded["reference_hashes"]), 4)

    def test_whole_frame_scaling_preserves_labels_and_native_positions(self):
        for scale in (0.5, 0.75, 1, 1.6, 2, 3):
            with self.subTest(scale=scale):
                player = ConjurationVision(self.profile, self.gallery)
                image = self.frame(scale=scale)
                result = self.stable(image, player)
                self.assertEqual(
                    [card.state for card in result.board.cards],
                    ["hidden", "face_up", "face_up", "removed"],
                    result.slots,
                )
                self.assertEqual(result.source_size, image.size)
                self.assertAlmostEqual(result.board.cards[0].x, 74 * scale, delta=1)

    def test_randomized_layouts_recognize_pixels_instead_of_fixed_identity_order(self):
        rng = random.Random(73)
        for index in range(24):
            keys = ["face-a", "face-a", "face-b", "back"]
            rng.shuffle(keys)
            player = ConjurationVision(self.profile, self.gallery)
            result = self.stable(self.frame(keys), player)
            actual = [(card.state, card.identity) for card in result.board.cards]
            expected = [
                (
                    ("hidden", None)
                    if key == "back"
                    else ("face_up", "synthetic-" + key[-1])
                )
                for key in keys
            ]
            self.assertEqual(actual, expected, (index, result.slots))

    def test_preview_and_hidden_board_feed_existing_memory_without_dispatch(self):
        memory = MemoryMatch("board-1", group_size=2)
        preview = self.frame(("face-a", "face-b", "face-b", "face-a"))
        memory.observe(self.observe(preview, phase="preview").board)
        memory.observe(self.observe(preview, 2, 60, phase="preview").board)
        self.assertIsNone(memory.propose())
        hidden = self.frame(("back",) * 4)
        memory.observe(self.observe(hidden, 3, 120).board)
        self.assertIsNone(memory.propose())
        memory.observe(self.observe(hidden, 4, 180).board)
        proposal = memory.propose()
        self.assertIsNotNone(proposal)
        self.assertEqual(proposal.reason, "complete-known-group")
        self.assertEqual(
            memory.memory,
            {
                "s0": "synthetic-a",
                "s1": "synthetic-b",
                "s2": "synthetic-b",
                "s3": "synthetic-a",
            },
        )
        self.assertIsNone(memory.propose())

    def test_blank_missing_or_half_covered_cards_never_mean_removed(self):
        for corruption in ("missing", "half-covered", "shifted"):
            with self.subTest(corruption=corruption):
                image = self.frame(("face-a",) * 4)
                draw = ImageDraw.Draw(image)
                if corruption == "missing":
                    draw.rectangle((38, 72, 109, 167), fill=(17, 22, 27))
                elif corruption == "half-covered":
                    draw.rectangle((38, 72, 73, 167), fill=(17, 22, 27))
                else:
                    crop = image.crop((38, 72, 110, 168))
                    draw.rectangle((38, 72, 109, 167), fill=(17, 22, 27))
                    image.paste(crop, (52, 72))
                result = self.stable(
                    image, ConjurationVision(self.profile, self.gallery)
                )
                self.assertEqual(
                    result.board.cards[0].state, "unknown", result.slots[0]
                )
                self.assertEqual(result.board.cards[0].identity, None)

    def test_similar_reference_labels_withhold_identity(self):
        data = deepcopy(self.data)
        data["references"].append(
            {"key": "near-a", "state": "face_up", "identity": "different-local-label"}
        )
        gallery = dict(self.gallery)
        gallery["near-a"] = gallery["face-a"].copy()
        gallery["near-a"].putpixel((35, 35), (241, 190, 40))
        player = ConjurationVision(VisionProfile.from_dict(data), gallery)
        result = self.stable(self.frame(("face-a",) * 4), player)
        self.assertTrue(all(card.state == "unknown" for card in result.board.cards))
        self.assertTrue(
            all(slot.reason == "ambiguous-reference" for slot in result.slots)
        )

    def test_inactive_state_needs_an_explicit_distinctive_positive_template(self):
        data = deepcopy(self.data)
        data["references"] = data["references"][:-1]
        gallery = {
            key: value for key, value in self.gallery.items() if key != "inactive"
        }
        result = self.stable(
            self.frame(("inactive",) * 4),
            ConjurationVision(VisionProfile.from_dict(data), gallery),
        )
        self.assertTrue(all(card.state == "unknown" for card in result.board.cards))
        gallery = dict(self.gallery, inactive=Image.new("RGB", (72, 96), (17, 22, 27)))
        with self.assertRaisesRegex(ValueError, "Plain backgrounds"):
            ConjurationVision(self.profile, gallery)

    def test_unknown_pixels_clear_previous_confidence_instead_of_carrying_it(self):
        self.stable(self.frame())
        unclear = Image.new("RGB", (400, 240), (0, 0, 0))
        result = self.observe(unclear, 3, 120)
        self.assertTrue(all(card.state == "unknown" for card in result.board.cards))
        result = self.observe(self.frame(), 4, 180)
        self.assertTrue(all(card.state == "unknown" for card in result.board.cards))
        result = self.observe(self.frame(), 5, 240)
        self.assertEqual(result.board.cards[0].state, "hidden")

    def test_animation_unknown_and_result_phase_cannot_authorize_even_clear_pixels(
        self,
    ):
        for phase in ("animation", "unknown", "result"):
            with self.subTest(phase=phase):
                result = self.stable(
                    self.frame(), ConjurationVision(self.profile, self.gallery), phase
                )
                self.assertTrue(
                    all(card.state == "unknown" for card in result.board.cards)
                )
                self.assertTrue(
                    all(
                        slot.reason == "supplied-phase-not-stable"
                        for slot in result.slots
                    )
                )

    def test_short_interval_or_long_gap_cannot_satisfy_stability(self):
        self.observe(self.frame())
        early = self.observe(self.frame(), 2, 10)
        self.assertTrue(all(card.state == "unknown" for card in early.board.cards))
        gap = self.observe(self.frame(), 3, 400)
        self.assertTrue(all(slot.stable_samples == 1 for slot in gap.slots))
        self.assertTrue(all(card.state == "unknown" for card in gap.board.cards))
        self.assertEqual(
            self.observe(self.frame(), 4, 460).board.cards[0].state, "hidden"
        )

    def test_wrong_aspect_does_not_stretch_an_unqualified_board_into_a_match(self):
        image = self.frame().resize((500, 240))
        result = self.stable(image)
        self.assertTrue(all(card.state == "unknown" for card in result.board.cards))
        self.assertTrue(
            all(slot.reason == "source-aspect-mismatch" for slot in result.slots)
        )

    def test_geometry_change_requires_new_board_identity_and_new_stability(self):
        self.stable(self.frame())
        with self.assertRaisesRegex(ValueError, "new board identifier"):
            self.observe(self.frame(scale=2), 3, 120)
        reset = self.observe(self.frame(scale=2), 0, 120, board_id="board-2")
        self.assertTrue(all(card.state == "unknown" for card in reset.board.cards))
        ready = self.observe(self.frame(scale=2), 1, 180, board_id="board-2")
        self.assertEqual(ready.board.cards[0].state, "hidden")

    def test_invalid_revisions_and_timestamps_do_not_mutate_stability(self):
        self.observe(self.frame())
        for revision, stamp in (
            (1, 60),
            (True, 60),
            (2, 0),
            (2, -1),
            (2, float("nan")),
            (2, float("inf")),
        ):
            with self.subTest(revision=revision, stamp=stamp):
                with self.assertRaises(ValueError):
                    self.observe(self.frame(), revision, stamp)
        self.assertEqual(
            self.observe(self.frame(), 2, 60).board.cards[0].state, "hidden"
        )

    def test_retired_board_identity_cannot_restart_revision_or_geometry_history(self):
        self.observe(self.frame(), 7, 0, board_id="visit-a")
        self.observe(self.frame(), 0, 60, board_id="visit-b")
        for image in (self.frame(), self.frame(scale=2)):
            with self.assertRaisesRegex(ValueError, "retired board"):
                self.observe(image, 0, 120, board_id="visit-a")
        current = self.observe(self.frame(), 1, 120, board_id="visit-b")
        self.assertEqual(current.board.cards[0].state, "hidden")

    def test_board_identity_history_has_a_finite_session_budget(self):
        image = self.frame()
        for index in range(128):
            self.observe(image, 0, index * 60, board_id=f"visit-{index}")
        with self.assertRaisesRegex(ValueError, "128 distinct boards"):
            self.observe(image, 0, 128 * 60, board_id="visit-128")

    def test_bad_roi_bounds_overlap_duplicates_and_reference_scope_are_rejected(self):
        variants = []
        for roi in ((0.9, 0, 0.2, 1), (0, 0, 0, 1), (float("nan"), 0, 1, 1)):
            data = deepcopy(self.data)
            data["board_roi"] = roi
            variants.append(data)
        for change in (
            "overlap",
            "same-slot",
            "hidden-label",
            "unknown-reference",
            "size",
            "plain",
        ):
            data = deepcopy(self.data)
            if change == "overlap":
                data["slots"][1]["roi"] = data["slots"][0]["roi"]
            elif change == "same-slot":
                data["slots"][1]["slot"] = "s0"
            elif change == "hidden-label":
                data["references"][0]["identity"] = "future-answer"
            elif change == "unknown-reference":
                data["references"][0]["state"] = "unknown"
            elif change == "size":
                data["reference_size"] = [8192, 8192]
            else:
                data["slots"][1]["roi"] = [0.28, 0.25, 0.1, 0.5]
            variants.append(data)
        for data in variants:
            with self.subTest(data=data):
                with self.assertRaises(ValueError):
                    VisionProfile.from_dict(data)

    def test_profile_typos_boolean_versions_and_overflowing_numbers_are_rejected(self):
        variants = []
        for field, value in (("schema_version", True), ("profile_typo", 1)):
            data = deepcopy(self.data)
            data[field] = value
            variants.append(data)
        for field, value in (("minimum_simlarity", 0.9), ("stable_ms", 10**400)):
            data = deepcopy(self.data)
            data["matching"][field] = value
            variants.append(data)
        for data in variants:
            with self.subTest(data=data):
                with self.assertRaises(ValueError):
                    VisionProfile.from_dict(data)
        with self.assertRaises(ValueError):
            self.observe(self.frame(), time_ms=10**400)

    def test_profile_file_rejects_duplicate_json_keys(self):
        raw = PROFILE.read_text(encoding="utf-8")
        duplicated = raw.replace(
            '"schema_version": 1,', '"schema_version": 1, "schema_version": 1,', 1
        )
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "profile.json"
            path.write_text(duplicated, encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Duplicate JSON field"):
                VisionProfile.load(path)

    def test_gallery_is_defensively_copied_and_missing_keys_are_rejected(self):
        image = self.frame()
        self.gallery["face-a"].paste((0, 0, 0), (0, 0, 72, 96))
        self.assertEqual(self.stable(image).board.cards[1].identity, "synthetic-a")
        with self.assertRaisesRegex(ValueError, "gallery keys"):
            ConjurationVision(self.profile, {})

    def test_corrupt_partial_and_oversized_images_are_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "frame.png"
            path.write_bytes(b"not a PNG")
            with self.assertRaisesRegex(ValueError, "corrupt"):
                load_image(path)
            self.frame().save(path)
            payload = path.read_bytes()
            path.write_bytes(payload[: len(payload) // 2])
            with self.assertRaisesRegex(ValueError, "corrupt"):
                load_image(path)
            self.frame().save(path)
            self.assertEqual(load_image(path).size, (400, 240))
        with self.assertRaisesRegex(ValueError, "Transparent"):
            self.observe(Image.new("RGBA", (400, 240), (0, 0, 0, 0)))
        with self.assertRaisesRegex(ValueError, "geometry"):
            self.observe(Image.new("L", (2049, 2048)))

    def test_exact_byte_decoder_matches_loader_and_rejects_bad_payloads(self):
        stream = io.BytesIO()
        image = self.frame()
        image.save(stream, format="PNG")
        payload = stream.getvalue()
        decoded = decode_image(payload)
        self.assertEqual(decoded.size, image.size)
        self.assertEqual(decoded.tobytes(), image.tobytes())
        for invalid in (
            None,
            "not bytes",
            bytearray(payload),
            b"not a PNG",
            payload[:30],
        ):
            with self.subTest(payload_type=type(invalid).__name__):
                with self.assertRaises(ValueError):
                    decode_image(invalid)
        with patch("conjuration_vision.MAX_IMAGE_BYTES", 32):
            with self.assertRaisesRegex(ValueError, "exceeds"):
                decode_image(b"x" * 33)
        transparent = io.BytesIO()
        Image.new("RGBA", (20, 20), (10, 20, 30, 0)).save(transparent, format="PNG")
        with self.assertRaisesRegex(ValueError, "Transparent"):
            decode_image(transparent.getvalue())

    def test_real_instructions_negative_is_hash_pinned_if_locally_available(self):
        provenance = json.loads(
            (PROFILE.parent / "provenance.json").read_text(encoding="utf-8")
        )
        source = provenance["real_negative_candidate"]
        path = ROOT / source["path"]
        if not path.is_file():
            self.skipTest("Local research-only instructions image is not distributed")
        self.assertEqual(
            hashlib.sha256(path.read_bytes()).hexdigest(), source["sha256"]
        )
        with self.assertRaisesRegex(ValueError, "Transparent"):
            load_image(path)
        with Image.open(path) as image:
            self.assertEqual(list(image.getextrema()[3]), source["raw_alpha_range"])
            diagnostic_rgb = image.convert("RGB")
        self.assertEqual(
            hashlib.sha256(diagnostic_rgb.tobytes()).hexdigest(),
            source["diagnostic_rgb_pixel_sha256"],
        )
        result = self.stable(diagnostic_rgb)
        self.assertTrue(
            all(card.state == "unknown" for card in result.board.cards), result.slots
        )


if __name__ == "__main__":
    unittest.main()
