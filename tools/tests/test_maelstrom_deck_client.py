"""Offline deck bridge checks; every subprocess call is mocked."""

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))

import maelstrom_deck_client


class MaelstromDeckClientTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="deck-client-test-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.executable = self.root / "Maelstrom & test.exe"
        self.executable.write_bytes(b"not an executable; subprocess is mocked")
        self.data_root = self.root / "data & literal; $(nothing)"
        environment = mock.patch.dict(
            os.environ,
            {
                "MAELSTROM_EXE": str(self.executable),
                "MAELSTROM_DATA_ROOT": str(self.data_root),
            },
        )
        environment.start()
        self.addCleanup(environment.stop)
        runner = mock.patch.object(maelstrom_deck_client.subprocess, "run")
        self.run = runner.start()
        self.addCleanup(runner.stop)
        self.run.return_value = subprocess.CompletedProcess([], 0)

    def respond(self, contents, returncode=0):
        def write_response(arguments, **kwargs):
            Path(arguments[-1]).write_bytes(contents)
            return subprocess.CompletedProcess(arguments, returncode)

        self.run.side_effect = write_response

    def assert_unconfirmed(self, result):
        self.assertIs(result["ok"], False)
        self.assertTrue(result["error"])
        self.assertNotIn("result", result)
        self.assertNotIn("completed", result)
        self.assertNotIn("facts", result)

    def test_missing_configuration_never_launches_or_invents_game_state(self):
        for name in ("MAELSTROM_EXE", "MAELSTROM_DATA_ROOT"):
            with self.subTest(name=name), mock.patch.dict(os.environ, {name: ""}):
                result = maelstrom_deck_client.call_deck("snapshot")
                self.assert_unconfirmed(result)
                self.assertIn("No game state is available", result["error"])
        self.run.assert_not_called()

    def test_invalid_paths_never_launch(self):
        cases = (
            {"MAELSTROM_EXE": "relative.exe"},
            {"MAELSTROM_EXE": str(self.root / "missing.exe")},
            {"MAELSTROM_EXE": str(self.root)},
            {"MAELSTROM_DATA_ROOT": "relative-data"},
        )
        for environment in cases:
            with self.subTest(environment=environment), mock.patch.dict(
                os.environ, environment
            ):
                self.assert_unconfirmed(maelstrom_deck_client.call_deck("state"))
        self.run.assert_not_called()

    def test_nonfinite_or_non_json_arguments_do_not_launch(self):
        for arguments in ({"bad": float("nan")}, {"bad": object()}, ["not an object"]):
            with self.subTest(arguments=type(arguments).__name__):
                self.assert_unconfirmed(
                    maelstrom_deck_client.call_deck("preview", arguments)
                )
        self.run.assert_not_called()

    def test_response_envelope_is_passed_through_without_fabricated_completion(self):
        payload = {
            "ok": True,
            "schemaVersion": 1,
            "result": {
                "operationId": "synthetic-operation",
                "status": "Queued",
                "completed": False,
                "facts": None,
            },
            "error": None,
        }
        self.respond(json.dumps(payload).encode("utf-8"))
        self.assertEqual(payload, maelstrom_deck_client.call_deck("step"))
        self.run.assert_called_once()

    def test_native_error_is_preserved_even_when_exit_code_is_nonzero(self):
        payload = {
            "ok": False,
            "schemaVersion": 1,
            "result": None,
            "error": "Reload the current revision before editing.",
        }
        self.respond(json.dumps(payload).encode("utf-8"), returncode=4)
        self.assertEqual(payload, maelstrom_deck_client.call_deck("deal"))
        self.run.assert_called_once()

    def test_bom_prefixed_native_json_is_accepted(self):
        payload = {"ok": True, "result": {"status": "Stopped"}}
        self.respond(json.dumps(payload).encode("utf-8-sig"))
        self.assertEqual(payload, maelstrom_deck_client.call_deck("state"))

    def test_request_values_and_paths_remain_separate_literal_arguments(self):
        command = "unknown; echo literal"
        arguments = {"title": 'A "quoted" card & $(literal)\nsecond line', "energy": 0}
        request_id = "f" * 32
        observed = {}

        def capture_request(argv, **kwargs):
            observed["argv"] = argv
            observed["kwargs"] = kwargs
            observed["request"] = json.loads(Path(argv[3]).read_text(encoding="utf-8"))
            self.assertFalse(Path(argv[4]).exists())
            Path(argv[4]).write_text('{"ok": false, "error": "unknown command"}')
            return subprocess.CompletedProcess(argv, 2)

        self.run.side_effect = capture_request
        result = maelstrom_deck_client.call_deck(command, arguments, request_id)
        self.assertFalse(result["ok"])
        argv = observed["argv"]
        self.assertEqual(5, len(argv))
        self.assertEqual(
            [str(self.executable), "--deck-api", str(self.data_root)], argv[:3]
        )
        self.assertEqual(
            {"command": command, "arguments": arguments, "requestId": request_id},
            observed["request"],
        )
        options = observed["kwargs"]
        self.assertIs(options["shell"], False)
        self.assertEqual(12, options["timeout"])
        for channel in ("stdin", "stdout", "stderr"):
            self.assertEqual(subprocess.DEVNULL, options[channel])
        self.assertEqual(
            getattr(subprocess, "CREATE_NO_WINDOW", 0), options["creationflags"]
        )
        self.assertFalse(Path(argv[3]).parent.exists())
        self.assertFalse(self.data_root.exists())

    def test_omitted_arguments_are_an_empty_object_and_request_id_stays_null(self):
        def capture_request(argv, **kwargs):
            request = json.loads(Path(argv[3]).read_text(encoding="utf-8"))
            self.assertEqual(
                {"command": "state", "arguments": {}, "requestId": None}, request
            )
            Path(argv[4]).write_text('{"ok": true, "result": {}}')
            return subprocess.CompletedProcess(argv, 0)

        self.run.side_effect = capture_request
        self.assertTrue(maelstrom_deck_client.call_deck("state")["ok"])

    def test_request_size_budget_is_checked_before_launch(self):
        for value in ("x" * 262144, "\u00e9" * 131072):
            with self.subTest(character=value[0]):
                result = maelstrom_deck_client.call_deck("deal", {"title": value})
                self.assert_unconfirmed(result)
                self.assertIn("size budget", result["error"])
        self.run.assert_not_called()

    def test_exit_zero_without_response_does_not_imply_completion(self):
        result = maelstrom_deck_client.call_deck("step", request_id="a" * 32)
        self.assert_unconfirmed(result)
        self.assertIn("exit 0", result["error"])
        self.assertIn("No completion is inferred", result["error"])
        self.run.assert_called_once()

    def test_empty_invalid_or_wrongly_typed_result_is_unconfirmed(self):
        invalid = (
            b"",
            b"not json",
            b"[]",
            b"null",
            b"true",
            b"{}",
            b'{"ok": "true"}',
            b'{"ok": 1}',
            b'{"ok": null}',
            b"\xff\xfe",
        )
        for contents in invalid:
            with self.subTest(contents=contents):
                self.run.reset_mock()
                self.respond(contents)
                self.assert_unconfirmed(maelstrom_deck_client.call_deck("state"))
                self.run.assert_called_once()

    def test_oversized_response_is_rejected_before_decoding(self):
        self.respond(b"x" * 262145)
        with mock.patch.object(
            maelstrom_deck_client.json,
            "loads",
            side_effect=AssertionError("must not decode"),
        ):
            result = maelstrom_deck_client.call_deck("state")
        self.assert_unconfirmed(result)
        self.run.assert_called_once()

    def test_valid_response_at_exact_size_limit_is_preserved(self):
        prefix = b'{"ok":true,"result":"'
        suffix = b'"}'
        contents = prefix + b"x" * (262144 - len(prefix) - len(suffix)) + suffix
        self.respond(contents)
        result = maelstrom_deck_client.call_deck("state")
        self.assertTrue(result["ok"])
        self.assertEqual(262144 - len(prefix) - len(suffix), len(result["result"]))

    def test_timeout_is_not_retried_or_replaced_with_a_late_success(self):
        observed = {}

        def time_out(argv, **kwargs):
            observed["folder"] = Path(argv[3]).parent
            Path(argv[4]).write_text('{"ok": true, "result": {"completed": true}}')
            raise subprocess.TimeoutExpired(argv, kwargs["timeout"])

        self.run.side_effect = time_out
        result = maelstrom_deck_client.call_deck("step", request_id="b" * 32)
        self.assert_unconfirmed(result)
        self.assertIn("TimeoutExpired", result["error"])
        self.assertIn("Inspect state before retrying", result["error"])
        self.run.assert_called_once()
        self.assertFalse(observed["folder"].exists())

    def test_launch_failure_is_unconfirmed_and_not_retried(self):
        self.run.side_effect = OSError("synthetic unavailable owner")
        result = maelstrom_deck_client.call_deck("step", request_id="c" * 32)
        self.assert_unconfirmed(result)
        self.assertIn("OSError", result["error"])
        self.run.assert_called_once()


if __name__ == "__main__":
    unittest.main()
