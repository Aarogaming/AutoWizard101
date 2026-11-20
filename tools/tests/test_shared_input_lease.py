"""Cross-language contract check against the compiled, input-free .NET helper."""

import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from wizard101_hub import automation_lease


@unittest.skipUnless(
    os.name == "nt" and os.environ.get("MAELSTROM_LOCK_TEST_HELPER"),
    "Requires Windows and the compiled Maelstrom.TestProcess helper",
)
class SharedInputLeaseTests(unittest.TestCase):
    def native(self, directory, *extra):
        return subprocess.Popen(
            [
                "dotnet",
                os.environ["MAELSTROM_LOCK_TEST_HELPER"],
                "--inputLock",
                str(directory),
                *extra,
            ],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            creationflags=subprocess.CREATE_NO_WINDOW,
        )

    def test_python_owner_blocks_native_and_release_allows_it(self):
        with tempfile.TemporaryDirectory() as folder:
            with automation_lease(Path(folder)):
                with self.native(folder) as process:
                    process.communicate(timeout=5)
                    self.assertEqual(process.returncode, 9)
            with self.native(folder) as process:
                process.communicate(timeout=5)
                self.assertEqual(process.returncode, 0)

    def test_native_owner_blocks_python_and_process_death_requires_recovery(self):
        with tempfile.TemporaryDirectory() as folder:
            ready = Path(folder) / "ready.txt"
            process = self.native(
                folder, "--lockReady", str(ready), "--holdLockMs", "10000"
            )
            try:
                deadline = time.monotonic() + 5
                while (
                    not ready.exists()
                    and time.monotonic() < deadline
                    and process.poll() is None
                ):
                    time.sleep(0.02)
                self.assertTrue(
                    ready.exists(), "Native helper failed to acquire the lock"
                )
                with self.assertRaisesRegex(RuntimeError, "owns game input"):
                    with automation_lease(Path(folder)):
                        self.fail("Python bypassed native ownership")
            finally:
                if process.poll() is None:
                    process.kill()
                process.communicate(timeout=5)
            self.assertEqual((Path(folder) / "game-input.lock").read_bytes(), b"1")
            with self.assertRaisesRegex(RuntimeError, "needs review"):
                with automation_lease(Path(folder)):
                    self.fail("Process death was treated as a confirmed input release")
            with self.native(folder) as successor:
                successor.communicate(timeout=5)
                self.assertEqual(successor.returncode, 9)

    def test_native_normal_release_allows_python(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.native(folder) as process:
                process.communicate(timeout=5)
                self.assertEqual(process.returncode, 0)
            self.assertEqual((Path(folder) / "game-input.lock").read_bytes(), b"0")
            with automation_lease(Path(folder)):
                pass

    def test_python_exception_blocks_new_native_owner_without_notice(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaisesRegex(ValueError, "failed worker"):
                with automation_lease(Path(folder)):
                    raise ValueError("failed worker")
            self.assertFalse((Path(folder) / "input-recovery-required.txt").exists())
            with self.native(folder) as process:
                process.communicate(timeout=5)
                self.assertEqual(process.returncode, 9)

    def test_dirty_and_invalid_bytes_block_both_runtimes_without_notice(self):
        for marker in (b"1", b"\x00", b"x", b"00", b"0\n"):
            with self.subTest(marker=marker), tempfile.TemporaryDirectory() as folder:
                state = Path(folder) / "game-input.lock"
                state.write_bytes(marker)
                with self.assertRaisesRegex(RuntimeError, "needs review"):
                    with automation_lease(Path(folder)):
                        self.fail("Invalid recovery state was ignored")
                with self.native(folder) as process:
                    process.communicate(timeout=5)
                    self.assertEqual(process.returncode, 9)
                self.assertEqual(state.read_bytes(), marker)

    def test_native_warning_failure_still_blocks_fresh_owners(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.native(folder, "--failRecoveryWarning") as process:
                stdout, stderr = process.communicate(timeout=5)
                self.assertEqual(process.returncode, 0, stderr.decode())
                self.assertIn(b"recovery-latched-without-notice", stdout)
            self.assertFalse((Path(folder) / "input-recovery-required.txt").exists())
            self.assertEqual((Path(folder) / "game-input.lock").read_bytes(), b"1")
            with self.assertRaisesRegex(RuntimeError, "needs review"):
                with automation_lease(Path(folder)):
                    self.fail("A failed warning save unblocked Python input")
            with self.native(folder) as successor:
                successor.communicate(timeout=5)
                self.assertEqual(successor.returncode, 9)

    def test_default_lock_is_shared_across_source_workspaces(self):
        from unittest.mock import patch

        with tempfile.TemporaryDirectory() as folder, patch.dict(
            os.environ, {"LOCALAPPDATA": folder}
        ):
            with automation_lease():
                expected = Path(folder) / "Maelstrom" / "session-locks"
                with self.native(expected) as process:
                    process.communicate(timeout=5)
                    self.assertEqual(process.returncode, 9)

    def test_unconfirmed_release_blocks_both_runtimes(self):
        with tempfile.TemporaryDirectory() as folder:
            (Path(folder) / "input-recovery-required.txt").write_text(
                "test recovery notice"
            )
            with self.assertRaisesRegex(RuntimeError, "needs review"):
                with automation_lease(Path(folder)):
                    self.fail("Unconfirmed release was ignored")
            with self.native(folder) as process:
                process.communicate(timeout=5)
                self.assertEqual(process.returncode, 9)


if __name__ == "__main__":
    unittest.main()
