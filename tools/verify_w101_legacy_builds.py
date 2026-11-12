"""Repeatable offline build/data checks with one worker and finite time limits.

No project installation scripts, network services, bot entry points or game
clients are run. C and C# executions are the reviewed local probe programs.
"""

from __future__ import annotations

import argparse
import ast
import json
import os
from pathlib import Path
import shutil
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]


def run(output: Path):
    report = json.loads((output / "audit.json").read_text(encoding="utf8"))
    roots = {p["archive"]: Path(p["source_copy"]) for p in report["projects"]}
    build = output / "build"
    build.mkdir(exist_ok=True)
    rows = []

    def save():
        (output / "builds.json").write_text(
            json.dumps(
                {
                    "schema_version": 1,
                    "checks": rows,
                    "network_allowed": False,
                    "game_input": False,
                },
                indent=2,
            )
            + "\n",
            encoding="utf8",
        )

    def command(
        archive,
        name,
        args,
        cwd=ROOT,
        environment=None,
        seconds=45,
        blocked_on_failure=False,
    ):
        row = {"archive": archive, "test": name, "status": "passed"}
        start = time.perf_counter()
        try:
            env = {**os.environ, **(environment or {})}
            process = subprocess.Popen(
                args,
                cwd=cwd,
                env=env,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                encoding="utf8",
                errors="replace",
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
            try:
                text, _ = process.communicate(timeout=seconds)
            except subprocess.TimeoutExpired:
                # Only the process tree started by this bounded check is stopped.
                if os.name == "nt":
                    subprocess.run(
                        ["taskkill", "/PID", str(process.pid), "/T", "/F"],
                        capture_output=True,
                        timeout=10,
                    )
                else:
                    process.kill()
                process.communicate(timeout=10)
                raise TimeoutError("Check exceeded its resource budget")
            row["exit_code"] = process.returncode
            row["status"] = (
                "passed"
                if process.returncode == 0
                else ("blocked" if blocked_on_failure else "failed")
            )
            log = output / (name + ".log")
            log.write_text(text, encoding="utf8")
            row["log"] = log.name
        except (OSError, TimeoutError, subprocess.SubprocessError) as error:
            row.update(status="blocked", detail=type(error).__name__)
        row["elapsed_ms"] = round((time.perf_counter() - start) * 1000, 2)
        rows.append(row)
        save()
        print(json.dumps(row), flush=True)
        return row

    aurorium = roots["Aurorium-main.zip"] / "Aurorium-main"
    command(
        "Aurorium-main.zip",
        "aurorium-offline-build",
        [shutil.which("cargo") or "cargo", "check", "--offline", "-j", "1"],
        cwd=aurorium,
        environment={
            "CARGO_NET_OFFLINE": "true",
            "CARGO_TARGET_DIR": str(build / "aurorium"),
        },
        blocked_on_failure=True,
    )
    go_root = roots["w101-proto-go-master.zip"] / "w101-proto-go-master"
    command(
        "w101-proto-go-master.zip",
        "protocol-go-offline-build",
        [shutil.which("go") or "go", "build", "-p", "1", "./..."],
        cwd=go_root,
        environment={
            "GOPROXY": "off",
            "GOSUMDB": "off",
            "GOTOOLCHAIN": "local",
            "GOCACHE": str(build / "go-cache"),
            "GOMODCACHE": str(build / "go-modules"),
        },
        blocked_on_failure=True,
    )

    c_root = roots["libkiwad-master.zip"] / "libkiwad-master"
    gcc = shutil.which("gcc") or "gcc"
    c_binary = build / "kiwad-probe.exe"
    row = command(
        "libkiwad-master.zip",
        "kiwad-compile",
        [
            gcc,
            "-std=c11",
            "-Wall",
            "-Wextra",
            "-I",
            str(c_root / "include"),
            str(ROOT / "tools/legacy_kiwad_probe.c"),
            str(c_root / "src/kiwad.c"),
            "-o",
            str(c_binary),
        ],
    )
    if row["status"] == "passed":
        command("libkiwad-master.zip", "kiwad-probe", [str(c_binary)], seconds=5)

    damage_root = (
        roots["Wizard101-Damage-Calculator-master.zip"]
        / "Wizard101-Damage-Calculator-master/KursovaLibrary"
    )
    damage_build = build / "damage"
    damage_build.mkdir(exist_ok=True)
    # Only source files from the pure library are linked. Its original net5.0
    # desktop application is not being qualified by this net9.0 console probe.
    import xml.etree.ElementTree as ET

    project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
    group = ET.SubElement(project, "PropertyGroup")
    for key, value in {
        "OutputType": "Exe",
        "TargetFramework": "net9.0",
        "EnableDefaultCompileItems": "false",
        "ImplicitUsings": "enable",
    }.items():
        ET.SubElement(group, key).text = value
    group = ET.SubElement(project, "ItemGroup")
    ET.SubElement(group, "Compile", Include=str(ROOT / "tools/legacy_damage_probe.cs"))
    ET.SubElement(group, "Compile", Include=str(damage_root / "**/*.cs"))
    ET.ElementTree(project).write(damage_build / "Probe.csproj", encoding="utf-8")
    (damage_build / "NuGet.Config").write_text(
        "<configuration><packageSources><clear /></packageSources></configuration>",
        encoding="utf8",
    )
    command(
        "Wizard101-Damage-Calculator-master.zip",
        "damage-library-probe",
        [
            shutil.which("dotnet") or "dotnet",
            "run",
            "--project",
            str(damage_build / "Probe.csproj"),
            "--no-launch-profile",
            "-p:UseSharedCompilation=false",
            "-m:1",
        ],
    )

    for archive, reason in {
        "cheat-engine-master.zip": "Lazarus/FPC toolchain unavailable; Pascal application and drivers not built or launched.",
        "wiz-packet-map-main.zip": "Legacy x86 injection project requires its MSVC/Crypto++ build setup and client offsets; not attached to W101.",
        "GrubNinja-master.zip": "Electron/React dependencies absent; lifecycle includes a global npm link. Reward endpoint calls are not gameplay evidence.",
        "WizGallery-dev.zip": "Next.js dependencies and database credentials/services absent; server/migration scripts not started.",
        "WizWikiAPI-main.zip": "TypeScript dependencies absent; bundled README reports upstream API no longer works. Current service not queried.",
        "raid-utility-tools-main.zip": "Matching Qt, memobj and forked wizwalker environment and live client compatibility unqualified.",
        "petdance.zip": "Binary teacher not launched; source-equivalent images and reviewed source components tested separately.",
        "ProjectMaelstrom_win-x64_portable.zip": "Historical binary preserved; current app is tested separately. Historical application not launched.",
        "On-Wiz-master.zip": "Documentation project; no application entry point to test.",
    }.items():
        rows.append(
            {
                "archive": archive,
                "test": "application_readiness",
                "status": (
                    "not_applicable"
                    if archive == "On-Wiz-master.zip"
                    else "not_qualified"
                ),
                "detail": reason,
            }
        )

    # Validate bounded, local data without contacting any service or executing
    # the upstream modules (some connect to databases at import time).
    wiki = roots["WizWikiAPI-main.zip"] / "WizWikiAPI-main/app/src/Data/Worlds.json"
    worlds = json.loads(wiki.read_text(encoding="utf8"))
    valid = all(
        isinstance(row, dict)
        and isinstance(row.get("Name"), str)
        and isinstance(row.get("Areas"), list)
        for row in worlds.values()
    )
    rows.append(
        {
            "archive": "WizWikiAPI-main.zip",
            "test": "world_context_schema",
            "status": "passed" if valid else "failed",
            "detail": f"{len(worlds)} historical world records; freshness not established",
        }
    )

    # Raid themes are literal data: extract literals, never import their GUI.
    count = 0
    for file in roots["raid-utility-tools-main.zip"].rglob("themes.py"):
        tree = ast.parse(file.read_bytes())
        for node in tree.body:
            if isinstance(node, ast.Assign) and isinstance(node.value, ast.Dict):
                value = ast.literal_eval(node.value)
                if isinstance(value, dict):
                    count += 1
    rows.append(
        {
            "archive": "raid-utility-tools-main.zip",
            "test": "literal_theme_data",
            "status": "passed" if count else "failed",
            "detail": f"{count} dictionaries; no raid gameplay tested",
        }
    )

    # Parse JavaScript without evaluating it; JSX belongs to a different parser.
    for archive in (
        "GrubNinja-master.zip",
        "Wizard101-Utilities-master.zip",
        "WizGallery-dev.zip",
    ):
        passed, needs_jsx, failed = 0, [], []
        for source in roots[archive].rglob("*.js"):
            if source.stat().st_size > 512_000:
                continue
            result = subprocess.run(
                [shutil.which("node") or "node", "--input-type=module", "--check"],
                input=source.read_text(encoding="utf8"),
                text=True,
                capture_output=True,
                timeout=5,
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
            if result.returncode == 0:
                passed += 1
            elif "Unexpected token '<'" in result.stderr:
                needs_jsx.append(source.relative_to(roots[archive]).as_posix())
            else:
                failed.append(source.relative_to(roots[archive]).as_posix())
        rows.append(
            {
                "archive": archive,
                "test": "javascript_parse",
                "status": "failed" if failed else "partial" if needs_jsx else "passed",
                "passed_files": passed,
                "needs_jsx_parser": needs_jsx,
                "failed_files": failed,
            }
        )
    save()
    return rows


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output", type=Path, default=ROOT / ".local/legacy-validation"
    )
    run(parser.parse_args().output.resolve())
