"""Offline fake clients only: never import wizwalker, ctypes or pymem."""

import asyncio
import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import types
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import wizwalker_adapter as adapter


def request():
    return dict(
        schemaVersion=1,
        runId="a" * 32,
        operation=adapter.VERSION,
        enabled=True,
        networkAllowed=False,
        sourceRoot="unused",
        sourceSha256="b" * 64,
        timeoutSeconds=1,
        target=dict(
            handle=42,
            processId=123,
            startedUtcTicks=1,
            processName="WizardGraphicalClient",
            imagePath="unused",
            imageSha256="c" * 64,
        ),
    )


class Client:
    def __init__(self, value=False, fail=False, fail_close=False, block=False):
        self.value, self.fail, self.fail_close, self.block = (
            value,
            fail,
            fail_close,
            block,
        )
        self.closed = False

    async def loading(self):
        if self.block:
            await asyncio.sleep(100)
        if self.fail:
            raise OSError("private diagnostic must not escape")
        return self.value

    async def close(self):
        self.closed = True
        if self.fail_close:
            raise OSError("disconnect")


class AdapterTests(unittest.IsolatedAsyncioTestCase):
    async def test_real_binding_uses_only_loading_read_and_handle_close(self):
        calls = []

        class Backend:
            process_id = 123

            def __init__(self, hwnd):
                calls.append(("open", hwnd))
                self._pymem = types.SimpleNamespace(
                    close_process=lambda: calls.append("close")
                )

            async def is_loading(self):
                calls.append("loading")
                return True

            async def activate_hooks(self):
                raise AssertionError("Hooks must not be activated")

        with patch.dict(
            sys.modules, {"wizwalker": types.SimpleNamespace(Client=Backend)}
        ):
            result = await adapter.observe(
                request(), adapter.LoadingClient, lambda _: True, lambda: False
            )
        self.assertEqual("observed", result["status"])
        self.assertEqual([("open", 42), "loading", "close"], calls)

    async def run_client(self, client, **kwargs):
        return await adapter.observe(
            request(),
            lambda _: client,
            kwargs.get("identity", lambda _: True),
            kwargs.get("stop", lambda: False),
        )

    async def test_true_and_false_are_observations_not_unknown(self):
        for value in (True, False):
            with self.subTest(value=value):
                client = Client(value)
                result = await self.run_client(client)
                self.assertEqual("observed", result["status"])
                self.assertIs(value, result["loading"])
                self.assertTrue(result["cleanupConfirmed"])
                self.assertTrue(client.closed)
                self.assertGreater(result["observedUtcTicks"], adapter.EPOCH_TICKS)

    async def test_unknown_value_and_failure_do_not_become_false(self):
        for client in (Client(None), Client(0), Client(fail=True)):
            with self.subTest(client=client):
                result = await self.run_client(client)
                self.assertEqual("unavailable", result["status"])
                self.assertIsNone(result["loading"])
                self.assertTrue(client.closed)
                self.assertNotIn("private", json.dumps(result))

    async def test_preflight_rejects_before_prepare_or_open(self):
        def unexpected(*_):
            self.fail("Game dependency reached")

        result = await adapter.observe(
            request(), unexpected, lambda _: False, lambda: False, unexpected
        )
        self.assertEqual("unavailable", result["status"])
        self.assertTrue(result["cleanupConfirmed"])

    async def test_missing_backend_dependency_does_not_open_or_require_recovery(self):
        original = __import__

        def missing(name, *args, **kwargs):
            if name == "wizwalker":
                raise ModuleNotFoundError("private dependency details")
            return original(name, *args, **kwargs)

        with patch.object(adapter, "prepare_source"), patch(
            "builtins.__import__", side_effect=missing
        ):
            result = await adapter.observe(
                request(),
                lambda _: self.fail("opened"),
                lambda _: True,
                lambda: False,
                lambda: adapter.prepare_backend(request(), "unused"),
            )
        self.assertEqual("dependency-unavailable", result["code"])
        self.assertIsNone(result["loading"])
        self.assertTrue(result["cleanupConfirmed"])
        self.assertNotIn("private", json.dumps(result))

    async def test_disabled_and_network_grants_never_open(self):
        for change in (
            {"enabled": False},
            {"networkAllowed": True},
            {"operation": "teleport"},
            {"command": "packet"},
        ):
            with self.subTest(change=change), self.assertRaises(ValueError):
                await adapter.observe(
                    request() | change,
                    lambda _: self.fail("opened"),
                    lambda _: True,
                    lambda: False,
                )

    async def test_replaced_target_after_read_discards_snapshot_and_closes(self):
        calls = 0

        def identity(_):
            nonlocal calls
            calls += 1
            return calls <= 3

        result = await self.run_client(Client(), identity=identity)
        self.assertEqual("unavailable", result["status"])
        self.assertIsNone(result["loading"])
        self.assertTrue(result["cleanupConfirmed"])

    async def test_stop_before_open_does_not_prepare(self):
        result = await adapter.observe(
            request(),
            lambda _: self.fail("opened"),
            lambda _: True,
            lambda: True,
            lambda: self.fail("prepared"),
        )
        self.assertEqual("cancelled", result["status"])
        self.assertTrue(result["cleanupConfirmed"])

    async def test_stop_during_read_waits_for_disconnect(self):
        client = Client(block=True)
        stops = 0

        def stop():
            nonlocal stops
            stops += 1
            return stops >= 4

        result = await self.run_client(client, stop=stop)
        self.assertEqual("cancelled", result["status"])
        self.assertTrue(client.closed)

    async def test_timeout_disconnects_and_has_no_observation(self):
        client = Client(block=True)
        result = await self.run_client(client)
        self.assertEqual("timeout", result["code"])
        self.assertIsNone(result["loading"])
        self.assertTrue(client.closed)

    async def test_partial_construction_never_claims_disconnect(self):
        def failed(_):
            raise OSError("partly opened")

        result = await adapter.observe(request(), failed, lambda _: True, lambda: False)
        self.assertFalse(result["cleanupConfirmed"])
        self.assertEqual("unavailable", result["status"])

    async def test_failed_disconnect_requires_recovery(self):
        result = await self.run_client(Client(fail_close=True))
        self.assertFalse(result["cleanupConfirmed"])
        self.assertEqual("cleanup_failed", result["status"])
        self.assertIsNone(result["loading"])

    async def test_tests_use_no_real_backend_imports(self):
        original = __import__

        def guarded(name, *args, **kwargs):
            if name.split(".")[0] in {"wizwalker", "pymem", "ctypes"}:
                self.fail("Offline test imported game access")
            return original(name, *args, **kwargs)

        with patch("builtins.__import__", side_effect=guarded):
            self.assertEqual("observed", (await self.run_client(Client()))["status"])


class ContractTests(unittest.TestCase):
    def test_invalid_identity_and_budgets(self):
        for key, value in [
            ("handle", 0),
            ("processId", True),
            ("startedUtcTicks", -1),
            ("processName", "another-game"),
        ]:
            candidate = request()
            candidate["target"][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                adapter.validate_request(candidate)
        for value in (0, 9, True):
            with self.assertRaises(ValueError):
                adapter.validate_request(request() | {"timeoutSeconds": value})

    def test_source_pin_matches_review_and_native_constant(self):
        root = Path(__file__).resolve().parents[2]
        review = json.loads(
            (root / "tools/fixtures/wizwalker_review.json").read_text(encoding="utf-8")
        )
        self.assertEqual(
            review["sourceSha256"], adapter.source_digest(root / review["sourceRoot"])
        )
        native = (
            root / "ProjectMaelstrom/ProjectMaelstrom/Utilities/WizwalkerOperation.cs"
        ).read_text(encoding="utf-8")
        self.assertIn(review["sourceSha256"], native)
        worker_hash = hashlib.sha256(
            (root / "tools/wizwalker_adapter.py").read_bytes()
        ).hexdigest()
        self.assertIn(worker_hash, native)

    def test_source_change_detected_and_bytecode_not_copied(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source = root / "original"
            source.mkdir()
            (source / "__init__.py").write_text("# inert fixture", encoding="utf-8")
            (source / "junk.pyc").write_bytes(b"not trusted")
            image = root / "client.exe"
            image.write_bytes(b"inert image")
            candidate = copy.deepcopy(request())
            candidate.update(
                sourceRoot=str(source), sourceSha256=adapter.source_digest(source)
            )
            candidate["target"].update(
                imagePath=str(image),
                imageSha256=hashlib.sha256(image.read_bytes()).hexdigest(),
            )
            before = list(sys.path)
            bytecode = sys.dont_write_bytecode
            try:
                adapter.prepare_source(candidate, root / "run")
                self.assertFalse((root / "run/source/wizwalker/junk.pyc").exists())
                (source / "__init__.py").write_text("# changed", encoding="utf-8")
                with self.assertRaisesRegex(ValueError, "source changed"):
                    adapter.prepare_source(candidate, root / "second")
            finally:
                sys.path[:] = before
                sys.dont_write_bytecode = bytecode


if __name__ == "__main__":
    unittest.main()
