"""Input-free recovery protocol checks; all ownership files are temporary."""

import argparse
from contextlib import redirect_stdout
import io
import os
from pathlib import Path
import sys
import subprocess
import tempfile
import threading
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import wizard101_hub as hub


class InputRecoveryTests(unittest.TestCase):
    def assert_blocked(self, folder):
        with self.assertRaisesRegex(RuntimeError, "needs review"):
            with hub.automation_lease(folder):
                self.fail("An unresolved input owner was admitted")

    def test_legacy_empty_and_explicit_clean_are_armed_before_yield(self):
        for initial in (b"", b"0"):
            with self.subTest(initial=initial), tempfile.TemporaryDirectory() as folder:
                marker = Path(folder) / "game-input.lock"
                marker.write_bytes(initial)
                writes = []
                persist = hub._persist_input_state

                def record(stream, value):
                    persist(stream, value)
                    writes.append(value)

                with patch.object(hub, "_persist_input_state", side_effect=record):
                    with hub.automation_lease(folder) as lease:
                        self.assertEqual(writes, [b"1"])
                        self.assertFalse(lease.recovery_required)
                self.assertEqual(writes, [b"1", b"0"])
                self.assertEqual(marker.read_bytes(), b"0")
                with hub.automation_lease(folder):
                    pass
                self.assertEqual(marker.read_bytes(), b"0")

    def test_dirty_and_malformed_states_are_rejected_without_mutation(self):
        for initial in (b"1", b"\x00", b"x", b"00", b"0\n", b"10", b"1" * 1024):
            with (
                self.subTest(initial=initial[:8]),
                tempfile.TemporaryDirectory() as folder,
            ):
                marker = Path(folder) / "game-input.lock"
                marker.write_bytes(initial)
                self.assert_blocked(folder)
                self.assertEqual(marker.read_bytes(), initial)

    def test_exceptions_including_interrupts_keep_dirty_without_notice(self):
        for error in (ValueError("failure"), KeyboardInterrupt(), SystemExit(2)):
            with (
                self.subTest(error=type(error).__name__),
                tempfile.TemporaryDirectory() as folder,
            ):
                with self.assertRaises(type(error)):
                    with hub.automation_lease(folder):
                        raise error
                self.assertEqual((Path(folder) / "game-input.lock").read_bytes(), b"1")
                self.assertFalse(
                    (Path(folder) / "input-recovery-required.txt").exists()
                )
                self.assert_blocked(folder)

    def test_explicit_recovery_latch_survives_ordinary_exit(self):
        with tempfile.TemporaryDirectory() as folder:
            with hub.automation_lease(folder) as lease:
                lease.require_recovery()
                lease.require_recovery()
                self.assertTrue(lease.recovery_required)
            self.assertEqual((Path(folder) / "game-input.lock").read_bytes(), b"1")
            self.assert_blocked(folder)

    def test_late_recovery_report_is_rejected_after_owner_ends(self):
        with tempfile.TemporaryDirectory() as folder:
            with hub.automation_lease(folder) as lease:
                pass
            with self.assertRaisesRegex(RuntimeError, "ownership already ended"):
                lease.require_recovery()
            self.assertEqual((Path(folder) / "game-input.lock").read_bytes(), b"0")

    def test_concurrent_recovery_report_is_serialized_with_clean_release(self):
        with tempfile.TemporaryDirectory() as folder:
            releasing = threading.Event()
            attempted = threading.Event()
            failures = []
            persist = hub._persist_input_state

            def report():
                if not releasing.wait(5):
                    failures.append("release never started")
                    return
                attempted.set()
                try:
                    lease.require_recovery()
                except RuntimeError as error:
                    failures.append(str(error))

            def pause_clean(stream, state):
                if state == b"0":
                    releasing.set()
                    self.assertTrue(attempted.wait(5))
                persist(stream, state)

            with patch.object(hub, "_persist_input_state", side_effect=pause_clean):
                with hub.automation_lease(folder) as lease:
                    worker = threading.Thread(target=report)
                    worker.start()
            worker.join(5)
            self.assertFalse(worker.is_alive())
            self.assertEqual(failures, ["Input ownership already ended"])
            self.assertEqual((Path(folder) / "game-input.lock").read_bytes(), b"0")

    def test_reentrant_recovery_report_during_clean_write_restores_dirty(self):
        with tempfile.TemporaryDirectory() as folder:
            persist = hub._persist_input_state

            def report_during_write(stream, state):
                persist(stream, state)
                if state == b"0":
                    lease.require_recovery()

            with patch.object(
                hub, "_persist_input_state", side_effect=report_during_write
            ):
                with hub.automation_lease(folder) as lease:
                    pass
            self.assertEqual((Path(folder) / "game-input.lock").read_bytes(), b"1")
            self.assert_blocked(folder)

    def test_existing_warning_file_or_directory_blocks_legacy_clean_state(self):
        for directory in (False, True):
            with (
                self.subTest(directory=directory),
                tempfile.TemporaryDirectory() as folder,
            ):
                marker = Path(folder) / "game-input.lock"
                marker.write_bytes(b"0")
                warning = Path(folder) / "input-recovery-required.txt"
                if directory:
                    warning.mkdir()
                else:
                    warning.write_text("Recovery still required")
                self.assert_blocked(folder)
                self.assertEqual(marker.read_bytes(), b"0")

    def test_notice_appearing_during_ownership_prevents_clean_release(self):
        with tempfile.TemporaryDirectory() as folder:
            with hub.automation_lease(folder):
                (Path(folder) / "input-recovery-required.txt").write_text(
                    "release unconfirmed"
                )
            self.assertEqual((Path(folder) / "game-input.lock").read_bytes(), b"1")
            self.assert_blocked(folder)

    def test_observed_dirty_state_remains_locally_latched_after_external_edit(self):
        with tempfile.TemporaryDirectory() as folder:
            marker = Path(folder) / "game-input.lock"
            marker.write_bytes(b"1")
            self.assert_blocked(folder)
            marker.write_bytes(b"0")
            self.assert_blocked(folder)

    def test_failed_initial_sync_does_not_grant_ownership(self):
        with tempfile.TemporaryDirectory() as folder:
            with patch.object(hub.os, "fsync", side_effect=OSError("sync failed")):
                with self.assertRaisesRegex(OSError, "sync failed"):
                    with hub.automation_lease(folder):
                        self.fail("An uncommitted input owner was admitted")
            self.assertEqual((Path(folder) / "game-input.lock").read_bytes(), b"1")
            self.assert_blocked(folder)

    def test_warning_stat_error_denies_before_arming_or_yielding(self):
        with tempfile.TemporaryDirectory() as folder:
            marker = Path(folder) / "game-input.lock"
            marker.write_bytes(b"0")
            with patch.object(
                Path, "lstat", side_effect=PermissionError("notice inaccessible")
            ):
                with self.assertRaisesRegex(PermissionError, "notice inaccessible"):
                    with hub.automation_lease(folder):
                        self.fail("Unreadable recovery notice was ignored")
            self.assertEqual(marker.read_bytes(), b"0")

    def test_failed_clean_sync_restores_dirty_and_propagates_failure(self):
        with tempfile.TemporaryDirectory() as folder:
            sync = os.fsync
            calls = 0

            def fail_clean(descriptor):
                nonlocal calls
                calls += 1
                if calls == 2:
                    raise OSError("clean sync failed")
                sync(descriptor)

            with patch.object(hub.os, "fsync", side_effect=fail_clean):
                with self.assertRaisesRegex(OSError, "clean sync failed"):
                    with hub.automation_lease(folder):
                        pass
            self.assertEqual(calls, 3)
            self.assertEqual((Path(folder) / "game-input.lock").read_bytes(), b"1")
            self.assert_blocked(folder)

    def test_failed_clean_and_dirty_restore_quarantines_owner_in_child(self):
        script = r"""
import os, subprocess, sys
from pathlib import Path
from unittest.mock import patch
sys.path.insert(0, sys.argv[1])
import wizard101_hub as hub
folder = Path(sys.argv[2])
persist = hub._persist_input_state
calls = 0
def fail_release(stream, state):
    global calls
    calls += 1
    if calls == 1:
        return persist(stream, state)
    if calls == 2:
        persist(stream, state)
        raise OSError("clean flush failure")
    raise OSError("dirty restore failure")
with patch.object(hub, "_persist_input_state", side_effect=fail_release):
    try:
        with hub.automation_lease(folder):
            pass
    except OSError as error:
        assert str(error) == "clean flush failure"
    else:
        raise AssertionError("Failure disappeared")
key = os.path.normcase(os.path.abspath(folder))
stream = hub._INPUT_QUARANTINED_STREAMS[key]
assert not stream.closed
try:
    with hub.automation_lease(folder):
        raise AssertionError("Local latch bypassed")
except RuntimeError as error:
    assert "needs review" in str(error)
probe = "import sys; sys.path.insert(0, sys.argv[1]); import wizard101_hub as h;\ntry:\n with h.automation_lease(sys.argv[2]): pass\nexcept RuntimeError as e:\n assert 'owns game input' in str(e); sys.exit(9)"
result = subprocess.run([sys.executable, "-c", probe, sys.argv[1], str(folder)],
    capture_output=True, timeout=5,
    creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
assert result.returncode == 9, result.stderr.decode()
# This test alone repairs its temporary marker before the subprocess closes it.
# Production never clears a quarantined owner automatically.
persist(stream, b"1")
stream.close()
print("quarantined-local-and-foreign")
"""
        with tempfile.TemporaryDirectory() as folder:
            result = subprocess.run(
                [sys.executable, "-c", script, str(Path(hub.__file__).parent), folder],
                capture_output=True,
                text=True,
                timeout=10,
                creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn("quarantined-local-and-foreign", result.stdout)
            self.assertEqual((Path(folder) / "game-input.lock").read_bytes(), b"1")

    def test_delegated_exit_code_controls_clean_release(self):
        for code in (0, 1, 130):
            with self.subTest(code=code), tempfile.TemporaryDirectory() as folder:
                args = argparse.Namespace(
                    tool="dance", execute=True, snapshot=None, policy="unused"
                )
                with (
                    patch.dict(os.environ, {"LOCALAPPDATA": folder}),
                    patch.object(hub, "require_tool_execution"),
                    patch.object(
                        hub, "launch_command", return_value=(Path(folder), ["unused"])
                    ),
                    patch.object(
                        hub,
                        "inspect_tool",
                        return_value={
                            "probe_error": None,
                            "missing_packages": [],
                            "entry_present": True,
                        },
                    ),
                    patch.object(hub, "validate_policy") as policy,
                    patch.object(hub.subprocess, "Popen") as popen,
                    redirect_stdout(io.StringIO()),
                ):
                    popen.return_value.wait.return_value = code
                    self.assertEqual(hub.run_tool(args), code)
                    policy.assert_called_once_with("unused")
                lock_folder = Path(folder) / "Maelstrom" / "session-locks"
                self.assertEqual(
                    (lock_folder / "game-input.lock").read_bytes(),
                    b"0" if code == 0 else b"1",
                )
                if code:
                    self.assert_blocked(lock_folder)

    def test_delegated_probe_nonzero_is_not_treated_as_clean_release(self):
        import activity_runtime

        for code in (0, 3):
            with self.subTest(code=code), tempfile.TemporaryDirectory() as folder:
                profile = Path(folder) / "profile.json"
                profile.write_text("{}")
                with (
                    patch.dict(os.environ, {"LOCALAPPDATA": folder}),
                    patch.object(
                        activity_runtime,
                        "load_profile",
                        return_value={"activity": "pet_dance"},
                    ),
                    patch.object(hub, "require_tool_execution"),
                    patch.object(
                        hub, "tool_paths", return_value=(Path(folder), Path("unused"))
                    ),
                    patch.object(hub.subprocess, "run") as run,
                ):
                    run.return_value.returncode = code
                    self.assertEqual(
                        hub.main(
                            ["activity-check", "--profile", str(profile), "--probe"]
                        ),
                        code,
                    )
                marker = (
                    Path(folder) / "Maelstrom" / "session-locks" / "game-input.lock"
                )
                self.assertEqual(marker.read_bytes(), b"0" if code == 0 else b"1")

    def test_legacy_execution_policy_still_rejects_before_launch_or_lease(self):
        args = argparse.Namespace(tool="dance", execute=True)
        with (
            patch.object(hub, "automation_lease") as lease,
            patch.object(hub.subprocess, "Popen") as popen,
        ):
            with self.assertRaisesRegex(ValueError, "unreviewed legacy"):
                hub.run_tool(args)
            lease.assert_not_called()
            popen.assert_not_called()


if __name__ == "__main__":
    unittest.main()
