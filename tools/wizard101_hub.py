"""Coordinate isolated Wizard101 tools and exchange offline data (stdlib only)."""

from __future__ import annotations

import argparse
from contextlib import contextmanager
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import sys
import threading

from w101_interaction_policy import require_tool_execution

ROOT = Path(__file__).resolve().parents[1]
LIBRARY = Path("ProjectMaelstrom/ProjectMaelstrom/Scripts/Library")
TOOLS = {
    "deimos": (
        LIBRARY / "Deimos-Wizard101",
        "deimos",
        "Deimos.py",
        True,
        ("questing", "farming", "pet-training", "navigation", "game-input"),
        ("wizwalker", "wizsprinter", "PySimpleGUI", "thefuzz", "pypresence"),
    ),
    "wizwalker": (
        LIBRARY / "wizwalker/wizwalker-master",
        "legacy",
        None,
        False,
        ("navigation", "client-state", "game-input"),
        ("wizwalker",),
    ),
    "wizsdk": (
        LIBRARY / "wizSDK/wizSDK-master",
        "legacy",
        None,
        False,
        ("questing", "farming", "pet-training", "game-input"),
        ("wizSDK",),
    ),
    "companion": (
        Path(".local/upstream/wizard101-companion"),
        "companion",
        "boss_wiki.py",
        False,
        ("ocr", "boss-guides", "quest-tracking", "gear-planning"),
        ("PyQt5", "requests", "Pillow"),
    ),
    "dance": (
        Path(".local/upstream/Wizard101_DanceBot"),
        "dance",
        "main.py",
        True,
        ("ocr", "pet-training", "game-input"),
        ("numpy", "opencv-python", "PyAutoGUI", "pynput"),
    ),
    "moonlight": (
        Path(".local/upstream/moonlight"),
        "moonlight",
        None,
        False,
        ("packet-decoding", "type-data"),
        ("moonlight", "printrospector", "katsuba"),
    ),
    "wiztype": (
        Path(".local/upstream/wiztype"),
        "wiztype",
        None,
        False,
        ("type-data", "patch-diagnostics"),
        ("wiztype", "memobj"),
    ),
    "wizdiff": (
        Path(".local/upstream/wizdiff"),
        "wizdiff",
        None,
        False,
        ("patch-diagnostics",),
        ("wizdiff", "aiohttp", "aiosqlite"),
    ),
}


def tool_paths(name, root=ROOT):
    source, environment, *_ = TOOLS[name]
    overrides_path = root / ".local/wizard101-tools.json"
    overrides = (
        json.loads(overrides_path.read_text(encoding="utf-8"))
        if overrides_path.exists()
        else {}
    )
    config = overrides.get(name, {})
    source = (root / config.get("source", str(source))).resolve()
    executable = "Scripts/python.exe" if os.name == "nt" else "bin/python"
    python = (
        root / config.get("python", str(Path(".local/envs") / environment / executable))
    ).resolve()
    return source, python


def overlaps(names):
    capabilities = {}
    for name in names:
        for capability in TOOLS[name][4]:
            capabilities.setdefault(capability, []).append(name)
    return {key: value for key, value in sorted(capabilities.items()) if len(value) > 1}


def inspect_tool(name, root=ROOT):
    source, python = tool_paths(name, root)
    versions = {}
    error = None
    if python.is_file():
        probe = (
            "import importlib.metadata as m,json,sys; result={}; "
            "exec('for name in sys.argv[1:]:\\n try: result[name]=m.version(name)"
            "\\n except m.PackageNotFoundError: result[name]=None'); print(json.dumps(result))"
        )
        try:
            result = subprocess.run(
                [str(python), "-I", "-c", probe, *TOOLS[name][5]],
                capture_output=True,
                text=True,
                timeout=20,
                check=True,
            )
            versions = json.loads(result.stdout)
        except (subprocess.SubprocessError, ValueError, OSError) as exc:
            error = str(exc)
    else:
        error = "Isolated Python environment missing"
    entry = TOOLS[name][2]
    missing = [package for package in TOOLS[name][5] if not versions.get(package)]
    return {
        "tool": name,
        "source": str(source),
        "source_present": source.is_dir(),
        "python": str(python),
        "entry_present": (source / entry).is_file() if entry else None,
        "packages": versions,
        "missing_packages": missing,
        "probe_error": error,
        "capabilities": TOOLS[name][4],
        "game_compatibility": "unverified",
        "kind": (
            "library"
            if name in ("wizwalker", "wizsdk")
            else "application-or-diagnostic"
        ),
    }


def write_json(path, data):
    """Create output exclusively: never overwrite an existing export."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8") as stream:
        json.dump(data, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def export_companion(database):
    """Read Companion's boss database without importing GUI code or writing to it."""
    path = Path(database).resolve(strict=True)
    connection = sqlite3.connect(path.as_uri() + "?mode=ro", uri=True)
    connection.row_factory = sqlite3.Row
    try:
        columns = {row[1] for row in connection.execute("PRAGMA table_info(bosses)")}
        required = {"name", "location", "school", "is_active"}
        if not required.issubset(columns):
            raise ValueError(
                f"Unsupported Companion bosses schema; missing {sorted(required - columns)}"
            )
        rows = connection.execute(
            "SELECT * FROM bosses WHERE is_active = 1 ORDER BY name"
        ).fetchall()
        mobs = []
        for row in rows:
            # The upstream location is free text. Do not invent world/area boundaries.
            item = {
                "Name": row["name"],
                "Zone": row["location"],
                "School": row["school"],
                "Type": "Boss",
                "Source": "wizard101-companion",
            }
            for source, target in (
                ("health", "Health"),
                ("url", "SourceUrl"),
                ("description", "Description"),
                ("last_updated_at", "SourceUpdatedAt"),
            ):
                if source in columns:
                    item[target] = row[source]
            if "cheats_json" in columns:
                try:
                    item["Cheats"] = json.loads(row["cheats_json"] or "[]")
                except (TypeError, ValueError):
                    item["CheatsRaw"] = row["cheats_json"]
            mobs.append(item)
        return mobs
    finally:
        connection.close()


def compare_records(before, after, key):
    def index(records):
        result = {}
        for record in records:
            name = record[key]
            if name in result:
                raise ValueError(f"Duplicate record: {name}")
            result[name] = record
        return result

    old, new = index(before), index(after)
    return {
        "added": [new[name] for name in sorted(new.keys() - old.keys())],
        "removed": [old[name] for name in sorted(old.keys() - new.keys())],
        "changed": [
            {"key": name, "before": old[name], "after": new[name]}
            for name in sorted(old.keys() & new.keys())
            if old[name] != new[name]
        ],
    }


def normalize_types(data):
    """Normalize wiztype v1/v2 for comparison; preserve all properties/offsets."""
    if "version" in data:
        if data["version"] != 2 or not isinstance(data.get("classes"), dict):
            raise ValueError("Only wiztype v1 and v2 dumps are supported")
        records = []
        for class_hash, value in data["classes"].items():
            record = dict(value)
            record["hash"] = str(value.get("hash", class_hash))
            records.append(record)
        return records
    return [
        dict(value, name=name, hash=str(value["hash"])) for name, value in data.items()
    ]


def validate_policy(path):
    if path is None:
        raise ValueError(
            "Automation requires --policy pointing to the app's execution_policy.conf"
        )
    values = {}
    for line in Path(path).read_text(encoding="utf-8-sig").splitlines():
        if line.strip() and not line.lstrip().startswith("#") and "=" in line:
            key, value = line.split("=", 1)
            values[key.strip().upper()] = value.strip()
    if (
        values.get("ALLOW_LIVE_AUTOMATION", "false").lower() != "true"
        or values.get("EXECUTION_PROFILE") != "ExperimentalSimulation"
    ):
        raise ValueError("Automation is disabled by the selected execution policy")


_INPUT_RECOVERY_LATCHES = set()
_INPUT_QUARANTINED_STREAMS = {}


class _AutomationLeaseState:
    """A delegated failure prevents a normal context exit from clearing recovery."""

    def __init__(self, directory_key):
        self._directory_key = directory_key
        self._recovery_required = False
        self._held = True
        self._gate = threading.RLock()

    @property
    def recovery_required(self):
        with self._gate:
            return self._recovery_required

    def require_recovery(self):
        with self._gate:
            if not self._held:
                raise RuntimeError("Input ownership already ended")
            self._recovery_required = True
            _INPUT_RECOVERY_LATCHES.add(self._directory_key)


def _persist_input_state(stream, state):
    stream.seek(0)
    if stream.write(state) != 1:
        raise OSError("Could not persist the shared input recovery state")
    stream.flush()
    os.fsync(stream.fileno())


def _input_recovery_notice_exists(path):
    try:
        path.lstat()
    except FileNotFoundError:
        return False
    return True


@contextmanager
def automation_lease(directory=None):
    """Byte-zero lock shared with GameInputLease.cs; never delete the lock file.

    A directory override is for isolated tests, not the selected source workspace.
    All installed/portable workspaces use the same per-user lock by default.
    Empty legacy files and ASCII '0' are clean; ASCII '1' requires recovery.
    Ownership is durably marked dirty before yielding. Only an ordinary exit
    without require_recovery() clears it; exceptions and process death do not.
    Delegated workers must call require_recovery() after a nonzero exit status
    before leaving the context; late recovery reports are rejected.
    """
    folder = (
        Path(directory)
        if directory is not None
        else Path(os.environ["LOCALAPPDATA"]) / "Maelstrom" / "session-locks"
    )
    directory_key = os.path.normcase(os.path.abspath(folder))
    if directory_key in _INPUT_RECOVERY_LATCHES:
        raise RuntimeError(
            "Previous input release needs review before starting live controls"
        )
    folder.mkdir(parents=True, exist_ok=True)
    # Append mode would append a second byte even after seek(0). Never truncate
    # or replace this file: both runtimes must lock the same persistent inode.
    descriptor = os.open(
        folder / "game-input.lock",
        os.O_RDWR | os.O_CREAT | getattr(os, "O_BINARY", 0),
        0o600,
    )
    stream = os.fdopen(descriptor, "r+b", buffering=0)
    locked = False
    quarantined = False
    try:
        stream.seek(0)
        try:
            if os.name == "nt":
                import msvcrt

                msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl

                fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
            locked = True
        except OSError as exc:
            raise RuntimeError("Another Maelstrom routine owns game input") from exc
        stream.seek(0)
        state = stream.read(2)
        if state not in (b"", b"0"):
            _INPUT_RECOVERY_LATCHES.add(directory_key)
            raise RuntimeError(
                "Previous input release needs review before starting live controls "
                "(dirty or invalid shared recovery state)"
            )
        if directory_key in _INPUT_RECOVERY_LATCHES or _input_recovery_notice_exists(
            folder / "input-recovery-required.txt"
        ):
            _INPUT_RECOVERY_LATCHES.add(directory_key)
            raise RuntimeError(
                "Previous input release needs review before starting live controls"
            )
        try:
            _persist_input_state(stream, b"1")
        except BaseException:
            _INPUT_RECOVERY_LATCHES.add(directory_key)
            raise
        lease = _AutomationLeaseState(directory_key)
        try:
            yield lease
        except BaseException:
            lease.require_recovery()
            raise
        finally:
            with lease._gate:
                try:
                    if not lease.recovery_required:
                        try:
                            if _input_recovery_notice_exists(
                                folder / "input-recovery-required.txt"
                            ):
                                lease.require_recovery()
                            else:
                                _persist_input_state(stream, b"0")
                                if lease.recovery_required:
                                    _persist_input_state(stream, b"1")
                        except BaseException:
                            lease.require_recovery()
                            # Retain the physical lock if even restoring dirty fails.
                            # No later worker may trust a possibly written clean byte.
                            try:
                                _persist_input_state(stream, b"1")
                            except BaseException:
                                quarantined = True
                                _INPUT_QUARANTINED_STREAMS[directory_key] = stream
                            raise
                finally:
                    lease._held = False
    finally:
        if not quarantined:
            try:
                if locked:
                    stream.seek(0)
                    if os.name == "nt":
                        msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
                    else:
                        fcntl.flock(stream, fcntl.LOCK_UN)
            finally:
                stream.close()


def launch_command(name, args, root=ROOT):
    source, python = tool_paths(name, root)
    if getattr(args, "profile", None):
        from activity_runtime import ACTIVITIES, load_profile

        profile = load_profile(args.profile)
        if name != ACTIVITIES[profile["activity"]]:
            raise ValueError("The selected tool does not match this activity setup.")
        if not all((args.events, args.control, args.policy)):
            raise ValueError(
                "Guided activities require progress, stop control and policy paths."
            )
        return source, [
            str(python),
            str(root / "tools/activity_runner.py"),
            "--source",
            str(source),
            "--profile",
            str(Path(args.profile).resolve(strict=True)),
            "--events",
            str(Path(args.events).resolve()),
            "--control",
            str(Path(args.control).resolve()),
            "--policy",
            str(Path(args.policy).resolve(strict=True)),
        ]
    if name in ("wizwalker", "wizsdk", "wizdiff"):
        raise ValueError(
            f"{name} is a library; use its adapter rather than a fabricated main.py"
        )
    if name == "wiztype":
        if not args.output:
            raise ValueError("wiztype requires --output")
        output = Path(args.output).resolve()
        if output.exists():
            raise FileExistsError(output)
        command = [
            str(python),
            "-m",
            "wiztype",
            str(output),
            "--version",
            "1",
            "--indent",
            "2",
        ]
    elif name == "moonlight":
        if not all((args.definitions, args.capture, args.output)):
            raise ValueError(
                "moonlight requires --definitions, --capture, and --output"
            )
        definitions = Path(args.definitions).resolve(strict=True)
        capture = Path(args.capture).resolve(strict=True)
        output = Path(args.output).resolve()
        if not definitions.is_dir() or not capture.is_file():
            raise ValueError("Expected a DML definitions directory and capture file")
        if output.exists():
            raise FileExistsError(output)
        command = [
            str(python),
            str(root / "tools/moonlight_adapter.py"),
            "decode",
            "pcap",
            str(definitions),
            str(capture),
            str(output),
        ]
        if args.typedefs:
            typedefs = Path(args.typedefs).resolve(strict=True)
            type_data = json.loads(typedefs.read_text(encoding="utf-8"))
            if not isinstance(type_data, dict) or "version" in type_data:
                raise ValueError(
                    "Moonlight expects a v1 type cache; generate it with wiztype --version 1"
                )
            command += ["--typedefs", str(typedefs)]
    else:
        command = [str(python), str(source / TOOLS[name][2])]
    return source, command


def run_tool(args, root=ROOT):
    if args.execute:
        require_tool_execution(args.tool)
    source, command = launch_command(args.tool, args, root)
    snapshot = Path(args.snapshot).resolve() if args.snapshot else None
    plan = {
        "tool": args.tool,
        "command": command,
        "working_directory": str(source),
        "snapshot": str(snapshot) if snapshot else None,
        "execute": args.execute,
    }
    print(json.dumps(plan, indent=2), flush=True)
    if not args.execute:
        return 0
    readiness = inspect_tool(args.tool, root)
    if (
        readiness["probe_error"]
        or readiness["missing_packages"]
        or not source.is_dir()
        or readiness["entry_present"] is False
    ):
        raise ValueError(f"Tool is not ready: {json.dumps(readiness)}")
    if args.tool in ("wiztype", "moonlight"):
        Path(args.output).resolve().parent.mkdir(parents=True, exist_ok=True)
    environment = os.environ.copy()
    environment["PYTHONNOUSERSITE"] = "1"
    environment["PYTHONUTF8"] = "1"
    environment.pop("PYTHONPATH", None)
    if snapshot:
        environment["MAELSTROM_SNAPSHOT_PATH"] = str(snapshot)

    def execute():
        process = subprocess.Popen(
            command, cwd=source, env=environment, start_new_session=os.name != "nt"
        )
        try:
            return process.wait()
        except KeyboardInterrupt:
            if process.poll() is None:
                if os.name == "nt":
                    subprocess.run(
                        ["taskkill", "/PID", str(process.pid), "/T", "/F"],
                        capture_output=True,
                        check=False,
                    )
                else:
                    import signal

                    os.killpg(process.pid, signal.SIGTERM)
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
            return 130

    if TOOLS[args.tool][3]:
        validate_policy(args.policy)
        with automation_lease() as lease:
            exit_code = execute()
            if exit_code != 0:
                lease.require_recovery()
            return exit_code
    return execute()


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="action", required=True)
    commands.add_parser(
        "status", help="Inspect isolated package metadata without importing game tools"
    )
    commands.add_parser(
        "overlaps", help="Show shared capabilities and competing input owners"
    )
    activity_check = commands.add_parser(
        "activity-check",
        help="Validate a setup and backend without attaching to the game",
    )
    activity_check.add_argument("--profile", required=True)
    activity_check.add_argument("--probe", action="store_true")
    export = commands.add_parser(
        "export-companion", help="Export active bosses as a Maelstrom-compatible cache"
    )
    export.add_argument("database")
    export.add_argument("--output", required=True)
    diff = commands.add_parser(
        "compare-types", help="Compare saved wiztype v1/v2 dumps offline"
    )
    diff.add_argument("before")
    diff.add_argument("after")
    diff.add_argument("--output", required=True)
    launch = commands.add_parser(
        "launch", help="Preview by default; --execute starts the selected isolated tool"
    )
    launch.add_argument("tool", choices=TOOLS)
    for option in (
        "output",
        "definitions",
        "capture",
        "typedefs",
        "policy",
        "snapshot",
        "profile",
        "events",
        "control",
    ):
        launch.add_argument("--" + option)
    launch.add_argument("--execute", action="store_true")
    args = parser.parse_args(argv)
    try:
        if args.action == "status":
            print(json.dumps([inspect_tool(name) for name in TOOLS], indent=2))
        elif args.action == "overlaps":
            print(json.dumps(overlaps(TOOLS), indent=2))
        elif args.action == "activity-check":
            from activity_runtime import ACTIVITIES, load_profile

            profile = load_profile(args.profile)
            tool = ACTIVITIES[profile["activity"]]
            if args.probe:
                require_tool_execution(tool)
            source, python = tool_paths(tool)
            command = [
                str(python),
                str(ROOT / "tools/activity_runner.py"),
                "--profile",
                str(Path(args.profile).resolve(strict=True)),
                "--source",
                str(source),
                "--probe" if args.probe else "--check",
            ]
            if args.probe:
                with automation_lease() as lease:
                    result = subprocess.run(command, cwd=source, check=False)
                    if result.returncode != 0:
                        lease.require_recovery()
            else:
                result = subprocess.run(command, cwd=source, check=False)
            return result.returncode
        elif args.action == "export-companion":
            records = export_companion(args.database)
            write_json(args.output, records)
            print(
                f"Exported {len(records)} bosses. Source database was opened read-only."
            )
        elif args.action == "compare-types":
            old = normalize_types(
                json.loads(Path(args.before).read_text(encoding="utf-8"))
            )
            new = normalize_types(
                json.loads(Path(args.after).read_text(encoding="utf-8"))
            )
            write_json(args.output, compare_records(old, new, "hash"))
        elif args.action == "launch":
            return run_tool(args)
        return 0
    except (OSError, ValueError, RuntimeError, sqlite3.Error) as exc:
        parser.exit(1, f"Integration error: {exc}\n")


if __name__ == "__main__":
    sys.exit(main())
