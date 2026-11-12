import io
import importlib.util
from pathlib import Path
import sys
import tarfile
import tempfile
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from inventory_w101_legacy import inspect
from rehearse_w101_dance_teacher import CueGate, definitions
from legacy_dance_scale_check import ScaleMatcher


class LegacyArchiveTests(unittest.TestCase):
    def test_inspects_zip_and_mislabeled_tar_without_extracting(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            zipped = root / "bot.zip"
            with zipfile.ZipFile(zipped, "w") as archive:
                archive.writestr("bot/README.md", "Legacy example")
                archive.writestr("bot/main.py", "pyautogui.press('up')\n")
            report = inspect(zipped)
            self.assertEqual(report["format"], "zip")
            self.assertEqual(report["signals"][0]["kind"], "input")
            disguised = root / "petdance.zip"
            with tarfile.open(disguised, "w") as archive:
                member = tarfile.TarInfo("../do-not-extract.py")
                raw = b"pyautogui.press('up')\n"
                member.size = len(raw)
                archive.addfile(member, io.BytesIO(raw))
            self.assertEqual(inspect(disguised)["format"], "tar")
            self.assertFalse((root.parent / "do-not-extract.py").exists())
            self.assertFalse((root / "bot").exists())


class TeacherCueTests(unittest.TestCase):
    def test_scale_search_is_bounded(self):
        for scales in ([], [0.1], [float("nan")], [1] * 42):
            with self.assertRaises(ValueError):
                ScaleMatcher({}, scales=scales)

    @unittest.skipUnless(
        all(
            importlib.util.find_spec(name) is not None
            for name in ("PIL", "numpy", "cv2")
        ),
        "Image experiment uses the isolated dance environment",
    )
    def test_scaled_arrow_and_blank_are_distinguished(self):
        from PIL import Image, ImageDraw

        up = Image.new("RGBA", (32, 32))
        ImageDraw.Draw(up).ellipse(
            (0, 0, 31, 31), fill=(12, 80, 40, 255), outline=(230, 170, 30, 255), width=2
        )
        ImageDraw.Draw(up).polygon(
            [(16, 2), (30, 16), (22, 16), (22, 30), (10, 30), (10, 16), (2, 16)],
            fill=(230, 220, 30, 255),
        )
        down = up.rotate(180)
        matcher = ScaleMatcher({"up": up, "down": down}, scales=[0.75, 1, 1.25])
        blank = Image.new("RGB", (80, 80), (20, 30, 40))
        frame = blank.copy()
        scaled = up.resize((40, 40), Image.Resampling.BILINEAR)
        frame.paste(scaled, (18, 21), scaled)
        self.assertEqual(matcher.predict(frame)["label"], "up")
        self.assertIsNone(matcher.predict(blank)["label"])

    def test_held_arrow_is_one_event_but_same_arrow_after_gap_is_new(self):
        gate = CueGate()
        self.assertTrue(gate.accept(["UP"]))
        self.assertFalse(gate.accept(["UP"]))
        self.assertFalse(gate.accept([]))
        self.assertFalse(gate.accept(["UP"]))
        self.assertFalse(gate.accept([]))
        self.assertFalse(gate.accept([]))
        self.assertTrue(gate.accept(["UP"]))

    def test_ambiguity_never_produces_an_action(self):
        gate = CueGate()
        self.assertFalse(gate.accept(["UP", "LEFT"]))
        self.assertTrue(gate.accept(["LEFT"]))

    def test_changed_source_cannot_execute(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory)
            (source / "dance_game.py").write_text(
                "raise RuntimeError('must not execute')"
            )
            with self.assertRaisesRegex(ValueError, "source changed"):
                definitions(source, "dance_game.py", {"update_search"}, {})


if __name__ == "__main__":
    unittest.main()
