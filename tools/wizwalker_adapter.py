"""One selected-client loading observation. No generic commands or gameplay loop.

The native scheduler owns game-input.lock for the worker's entire lifetime.
This trusted worker accepts one bounded stdin grant only after the parent has
attached its process job. It is not a sandbox for arbitrary Python code.
"""

from __future__ import annotations

import argparse
import asyncio
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
import time

VERSION = "wizwalker-loading-v1"
EPOCH_TICKS = 621355968000000000


class DependencyUnavailable(Exception):
    """The reviewed backend could not import; no client has been created."""


def prepare_backend(request, directory):
    prepare_source(request, directory)
    try:
        from wizwalker import Client  # noqa: F401
    except ImportError as error:
        raise DependencyUnavailable() from error


def utc_ticks():
    return EPOCH_TICKS + time.time_ns() // 100


def source_digest(root):
    root = Path(root)
    files = sorted(root.rglob("*.py"), key=lambda p: p.relative_to(root).as_posix())
    if not 1 <= len(files) <= 200:
        raise ValueError("Unsupported source tree")
    digest = hashlib.sha256()
    total = 0
    for path in files:
        if path.is_symlink() or not path.resolve().is_relative_to(root.resolve()):
            raise ValueError("Linked source is not supported")
        data = path.read_bytes()
        total += len(data)
        if total > 4 * 1024 * 1024:
            raise ValueError("Source exceeds the adapter budget")
        digest.update(path.relative_to(root).as_posix().encode() + b"\0")
        digest.update(hashlib.sha256(data).digest())
    return digest.hexdigest()


def validate_request(request):
    if (
        not isinstance(request, dict)
        or type(request.get("schemaVersion")) is not int
        or request.get("schemaVersion") != 1
    ):
        raise ValueError("Unsupported request")
    if request.get("enabled") is not True or request.get("networkAllowed") is not False:
        raise ValueError("Explicit client observation and network exclusion required")
    if request.get("operation") != VERSION:
        raise ValueError("Only the loading observation is implemented")
    if set(request) != {
        "schemaVersion",
        "runId",
        "operation",
        "enabled",
        "networkAllowed",
        "target",
        "sourceRoot",
        "sourceSha256",
        "timeoutSeconds",
    }:
        raise ValueError("Unsupported adapter request fields")
    run = request.get("runId", "")
    if len(run) != 32 or any(c not in "0123456789abcdef" for c in run):
        raise ValueError("Invalid run identity")
    if (
        type(request.get("timeoutSeconds")) is not int
        or not 1 <= request["timeoutSeconds"] <= 8
    ):
        raise ValueError("Invalid observation budget")
    target = request.get("target", {})
    for key in ("handle", "processId", "startedUtcTicks"):
        if type(target.get(key)) is not int or target[key] <= 0:
            raise ValueError("Select a process instance")
    if target.get("processName") != "WizardGraphicalClient":
        raise ValueError("This adapter only supports Wizard101")
    for key in ("imageSha256",):
        value = target.get(key, "")
        if len(value) != 64 or any(c not in "0123456789abcdef" for c in value):
            raise ValueError("Missing client image identity")
    value = request.get("sourceSha256", "")
    if len(value) != 64 or any(c not in "0123456789abcdef" for c in value):
        raise ValueError("Missing reviewed source identity")
    return request


def current_target(target):
    """Verify HWND, PID, creation time and executable; never activate the window."""
    import ctypes
    from ctypes import wintypes as wt

    if os.name != "nt":
        return False
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    user = ctypes.WinDLL("user32", use_last_error=True)
    user.GetWindowThreadProcessId.argtypes = [wt.HWND, ctypes.POINTER(wt.DWORD)]
    kernel.OpenProcess.argtypes = [wt.DWORD, wt.BOOL, wt.DWORD]
    kernel.OpenProcess.restype = wt.HANDLE
    kernel.CloseHandle.argtypes = [wt.HANDLE]
    kernel.GetProcessTimes.argtypes = [wt.HANDLE] + [ctypes.POINTER(wt.FILETIME)] * 4
    kernel.QueryFullProcessImageNameW.argtypes = [
        wt.HANDLE,
        wt.DWORD,
        wt.LPWSTR,
        ctypes.POINTER(wt.DWORD),
    ]
    pid = wt.DWORD()
    user.GetWindowThreadProcessId(target["handle"], ctypes.byref(pid))
    if pid.value != target["processId"]:
        return False
    handle = kernel.OpenProcess(0x1000, False, pid.value)
    if not handle:
        return False
    try:
        times = [wt.FILETIME() for _ in range(4)]
        if not kernel.GetProcessTimes(handle, *(ctypes.byref(t) for t in times)):
            return False
        started = (times[0].dwHighDateTime << 32) | times[0].dwLowDateTime
        if started + 504911232000000000 != target["startedUtcTicks"]:
            return False
        name = ctypes.create_unicode_buffer(32768)
        size = wt.DWORD(len(name))
        return bool(
            kernel.QueryFullProcessImageNameW(handle, 0, name, ctypes.byref(size))
        ) and os.path.normcase(name.value) == os.path.normcase(target["imagePath"])
    finally:
        kernel.CloseHandle(handle)


def prepare_source(request, directory):
    source = Path(request["sourceRoot"])
    if source_digest(source) != request["sourceSha256"]:
        raise ValueError("Reviewed source changed")
    image = Path(request["target"]["imagePath"])
    if image.stat().st_size > 512 * 1024 * 1024:
        raise ValueError("Client image exceeds verification budget")
    with image.open("rb") as stream:
        if (
            hashlib.file_digest(stream, "sha256").hexdigest()
            != request["target"]["imageSha256"]
        ):
            raise ValueError("Selected client image changed")
    # Import only a fresh verified source copy, never an inherited __pycache__.
    frozen = Path(directory) / "source" / "wizwalker"
    frozen.mkdir(parents=True, exist_ok=False)
    for path in source.rglob("*.py"):
        dest = frozen / path.relative_to(source)
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, dest)
    if source_digest(frozen) != request["sourceSha256"]:
        raise ValueError("Source changed while snapshotting")
    sys.dont_write_bytecode = True
    sys.path.insert(0, str(frozen.parent))


class LoadingClient:
    """Pinned wizwalker Client; no activate_hooks, input, teleport or network API."""

    def __init__(self, target):
        from wizwalker import Client

        self.client = Client(target["handle"])
        if self.client.process_id != target["processId"]:
            self.client._pymem.close_process()
            raise ValueError("Client target changed")

    async def loading(self):
        return await self.client.is_loading()

    async def close(self):
        # No hooks or gameplay methods are activated by this adapter. Closing
        # the process handle also avoids Client.close's broader hook lifecycle.
        self.client._pymem.close_process()


async def observe(request, factory, identity, stop, prepare=lambda: None):
    validate_request(request)
    started = time.monotonic()
    result = dict(
        schemaVersion=1,
        runId=request["runId"],
        operation=VERSION,
        sourceSha256=request["sourceSha256"],
        target=request["target"],
        status="unavailable",
        code="not-started",
        loading=None,
        observedUtcTicks=0,
        durationMs=0,
        cleanupConfirmed=True,
    )
    client = None
    try:
        if stop():
            result.update(status="cancelled", code="cancelled-before-open")
            return result
        if not identity(request["target"]):
            raise ValueError("Selected target changed")
        prepare()
        if stop() or not identity(request["target"]):
            raise ValueError("Target or session changed before open")
        if time.monotonic() - started >= request["timeoutSeconds"]:
            raise TimeoutError("Preparation exceeded budget")
        result["cleanupConfirmed"] = False
        client = factory(request["target"])
        task = asyncio.create_task(client.loading())
        try:
            while not task.done():
                if stop():
                    result.update(status="cancelled", code="cancelled")
                    break
                if not identity(request["target"]):
                    raise ValueError("Selected target changed")
                if time.monotonic() - started >= request["timeoutSeconds"]:
                    raise TimeoutError("Observation timed out")
                await asyncio.wait([task], timeout=0.05)
            else:
                value = task.result()
                if type(value) is not bool:
                    raise ValueError("Unknown loading state")
                if stop() or not identity(request["target"]):
                    raise ValueError("Target or session changed after read")
                if time.monotonic() - started >= request["timeoutSeconds"]:
                    raise TimeoutError("Observation exceeded budget")
                result.update(
                    status="observed",
                    code="loading-state-observed",
                    loading=value,
                    observedUtcTicks=utc_ticks(),
                )
        finally:
            if not task.done():
                task.cancel()
            try:
                await task
            except (Exception, asyncio.CancelledError):
                pass
    except Exception as error:
        # Fixed codes; no memory bytes, private state, paths or traceback in reports.
        result.update(
            status="unavailable",
            code=(
                "timeout"
                if isinstance(error, TimeoutError)
                else (
                    "dependency-unavailable"
                    if isinstance(error, DependencyUnavailable)
                    else "observation-unavailable"
                )
            ),
            loading=None,
        )
    finally:
        if client is not None:
            try:
                await asyncio.wait_for(client.close(), timeout=2)
                result["cleanupConfirmed"] = True
            except Exception:
                result.update(
                    status="cleanup_failed", code="disconnect-unconfirmed", loading=None
                )
        result["durationMs"] = int((time.monotonic() - started) * 1000)
    return result


def main(factory=LoadingClient, identity=current_target, prepare=prepare_backend):
    parser = argparse.ArgumentParser()
    parser.add_argument("--directory", required=True)
    args = parser.parse_args()
    directory = Path(args.directory).resolve()
    # EOF/missing opt-in returns before importing any game dependency.
    raw = sys.stdin.buffer.readline(32769)
    if len(raw) > 32768:
        raise ValueError("Request exceeds budget")
    request = validate_request(json.loads(raw))

    async def run():
        asyncio.get_running_loop().set_default_executor(
            ThreadPoolExecutor(max_workers=1)
        )
        return await observe(
            request,
            factory,
            identity,
            lambda: (directory / "stop.request").exists(),
            lambda: prepare(request, directory),
        )

    result = asyncio.run(run())
    with (directory / "result.json").open("x", encoding="utf-8") as stream:
        json.dump(result, stream, allow_nan=False)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
