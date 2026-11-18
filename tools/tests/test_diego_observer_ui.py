from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
try:
    from diego_observer import Observer
    from diego_vision import demo
except ImportError:
    Observer = demo = None

OBSERVER_UI_REQUIRED = unittest.skipIf(
    Observer is None,
    "Existing offline Pillow/OpenCV/tkinter environment required",
)


@OBSERVER_UI_REQUIRED
class DiegoObserverUiTests(unittest.TestCase):
    def test_demo_navigation_confirmation_and_replay_stop(self):
        with tempfile.TemporaryDirectory() as folder:
            pack, frames = demo(folder)
            window = Observer(pack, frames, demo_mode=True)
            try:
                window.geometry("1120x820+-30000+-30000")
                window.update()
                self.assertIn("SYNTHETIC DEMO", window.status.get())
                self.assertEqual(len(window.inspect()["detections"]), 2)
                window.change_frame(1)
                window.change_frame(1)
                result = window.inspect()
                self.assertIn("approaching", " ".join(result["suggestions"]))
                self.assertFalse(result["inputSent"])
                window.change_frame(-1)
                self.assertTrue(
                    all(
                        track["consecutiveFrames"] == 1
                        for track in window.inspect()["tracks"]
                    )
                )
                window.toggle_replay()
                self.assertEqual(window.index, 1)
                window.stop()
                self.assertFalse(window.replaying)
                self.assertIsNone(window.after_id)
                self.assertGreater(window.canvas.winfo_width(), 800)
            finally:
                window.close()


if __name__ == "__main__":
    unittest.main()
