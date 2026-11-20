"""UI-only admission checks with no real backend imports, process or game access."""

import argparse
from contextlib import redirect_stderr
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import activity_runner
import wizard101_hub as hub
from w101_interaction_policy import execution_block_reason, require_tool_execution

QUEST = {
    "version": 1,
    "activity": "questing",
    "maxMinutes": 1,
    "combatStyle": "damage",
    "minHealthPercent": 20,
    "minManaPercent": 10,
    "usePotions": True,
    "buyPotions": False,
    "teamUp": False,
}


class W101InteractionPolicyTests(unittest.TestCase):
    def test_memory_observation_has_a_distinct_not_enabled_reason(self):
        from w101_interaction_policy import set_process_observation_allowed

        set_process_observation_allowed(False)
        self.assertIn(
            "process-memory observation is not enabled",
            execution_block_reason("wiztype"),
        )
        self.assertIn("hooks", execution_block_reason("deimos"))
        for tool in ("companion", "moonlight", "wizdiff"):
            self.assertIsNone(execution_block_reason(tool))

    def test_process_observation_can_be_permitted_by_exception(self):
        from w101_interaction_policy import set_process_observation_allowed

        try:
            set_process_observation_allowed(True)
            self.assertIsNone(execution_block_reason("wiztype"))
            # client internals and unreviewed code remain blocked
            self.assertIn("hooks", execution_block_reason("deimos"))
            self.assertIn("unreviewed", execution_block_reason("unknown-tool"))
        finally:
            set_process_observation_allowed(False)

    def test_live_launch_rejects_before_readiness_resolution_or_subprocess(self):
        for tool in ("deimos", "wiztype", "dance", "unknown-ui-claim"):
            with self.subTest(tool=tool), patch.object(
                hub, "launch_command"
            ) as command, patch.object(hub.subprocess, "Popen") as process:
                with self.assertRaisesRegex(ValueError, "UI-only mode"):
                    hub.run_tool(argparse.Namespace(tool=tool, execute=True))
                command.assert_not_called()
                process.assert_not_called()

    def test_direct_probe_denies_before_any_wizwalker_import(self):
        original_import = __import__

        def no_game_import(name, *args, **kwargs):
            if name.startswith("wizwalker"):
                self.fail("UI-only probe attempted to import a client backend")
            return original_import(name, *args, **kwargs)

        with patch("builtins.__import__", side_effect=no_game_import):
            with self.assertRaisesRegex(ValueError, "UI-only mode"):
                # The direct guard must reject before its first await; no event loop needed.
                activity_runner.probe_deimos(QUEST).send(None)

    def test_hub_probe_rejects_before_backend_path_or_subprocess(self):
        with tempfile.TemporaryDirectory() as folder:
            profile = Path(folder) / "quest.json"
            profile.write_text(json.dumps(QUEST), encoding="utf-8")
            with patch.object(hub, "tool_paths") as paths, patch.object(
                hub.subprocess, "run"
            ) as process, redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit) as exit_code:
                    hub.main(["activity-check", "--profile", str(profile), "--probe"])
                self.assertEqual(1, exit_code.exception.code)
                paths.assert_not_called()
                process.assert_not_called()

    def test_direct_runner_rejects_live_and_probe_before_backend_validation(self):
        with tempfile.TemporaryDirectory() as folder:
            profile = Path(folder) / "quest.json"
            profile.write_text(json.dumps(QUEST), encoding="utf-8")
            for flags in ([], ["--probe"], ["--check", "--probe"]):
                args = [
                    "activity_runner",
                    "--profile",
                    str(profile),
                    "--source",
                    "missing-source",
                    *flags,
                ]
                with self.subTest(flags=flags), patch.object(
                    sys, "argv", args
                ), patch.object(activity_runner, "validate_backend") as backend:
                    with self.assertRaisesRegex(ValueError, "UI-only mode"):
                        activity_runner.main()
                    backend.assert_not_called()

    def test_offline_backend_check_reads_source_without_importing_it(self):
        with tempfile.TemporaryDirectory() as folder:
            source = Path(folder)
            (source / "Deimos.py").write_text(
                "# maelstrom_run\nraise RuntimeError('must not execute')",
                encoding="utf-8",
            )
            original_import = __import__

            def no_backend_import(name, *args, **kwargs):
                if (
                    name == "src"
                    or name.startswith("src.")
                    or name.startswith("wizwalker")
                ):
                    self.fail("Offline setup check imported backend code")
                return original_import(name, *args, **kwargs)

            with patch("builtins.__import__", side_effect=no_backend_import):
                self.assertIsNone(activity_runner.validate_backend(QUEST, source))

    def test_unknown_capability_claim_fails_closed(self):
        with self.assertRaisesRegex(ValueError, "unreviewed"):
            require_tool_execution("native")


if __name__ == "__main__":
    unittest.main()
