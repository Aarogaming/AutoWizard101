"""Archive integrity and source health baseline for every supplied legacy project.

This never imports a project's Python modules or launches its applications.
Static parsing is reported separately from dependency/build/behavior testing.
"""

from __future__ import annotations

import argparse
import ast
import hashlib
import json
from pathlib import Path, PurePosixPath
import stat
import tarfile
import tomllib
import xml.etree.ElementTree as ET
import zipfile

SOURCE = {
    ".py",
    ".pyi",
    ".cs",
    ".csproj",
    ".sln",
    ".resx",
    ".config",
    ".rs",
    ".toml",
    ".lock",
    ".go",
    ".mod",
    ".sum",
    ".js",
    ".jsx",
    ".ts",
    ".tsx",
    ".json",
    ".xml",
    ".html",
    ".css",
    ".md",
    ".txt",
    ".rst",
    ".yaml",
    ".yml",
    ".ini",
    ".cfg",
    ".csv",
    ".c",
    ".cpp",
    ".h",
    ".hpp",
    ".pas",
    ".pp",
    ".lpr",
    ".lpi",
    ".dfm",
    ".lfm",
    ".rc",
    ".lua",
    ".ahk",
    ".sh",
    ".bat",
    ".ps1",
    ".sql",
    ".png",
    ".jpg",
    ".jpeg",
    ".bmp",
    ".ico",
    ".svg",
    ".wad",
}
BINARY_BUNDLES = {"petdance.zip", "ProjectMaelstrom_win-x64_portable.zip"}


def safe_member(name: str) -> str:
    normalized = name.replace("\\", "/").rstrip("/")
    path = PurePosixPath(normalized)
    if not path.parts or path.is_absolute() or ".." in path.parts or ":" in normalized:
        raise ValueError("Unsafe archive path")
    for part in path.parts:
        if part.endswith((".", " ")) or part.split(".")[0].upper() in {
            "CON",
            "PRN",
            "AUX",
            "NUL",
            *(f"COM{i}" for i in range(10)),
            *(f"LPT{i}" for i in range(10)),
        }:
            raise ValueError("Unsupported Windows archive path")
    return path.as_posix()


def source_check(name: str, raw: bytes) -> dict | None:
    suffix = Path(name).suffix.lower()
    kind = {
        ".py": "python_ast",
        ".json": "json",
        ".toml": "toml",
        ".xml": "xml",
        ".csproj": "xml",
        ".resx": "xml",
    }.get(suffix)
    if kind is None:
        return None
    row = {"file": name, "kind": kind, "status": "passed"}
    if Path(name).name.lower() in {"tsconfig.json", "jsconfig.json"}:
        return {
            **row,
            "kind": "jsonc",
            "status": "not_run",
            "message": "Compiler configuration allows comments; requires the TypeScript parser.",
        }
    try:
        if kind == "python_ast":
            tree = ast.parse(raw, filename=name)
            imports = set()
            for node in ast.walk(tree):
                if isinstance(node, ast.Import):
                    imports.update(alias.name.split(".")[0] for alias in node.names)
                elif (
                    isinstance(node, ast.ImportFrom) and node.module and not node.level
                ):
                    imports.add(node.module.split(".")[0])
            row["imports"] = sorted(imports)
            row["definitions"] = sum(
                isinstance(n, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef))
                for n in tree.body
            )
        elif kind == "json":
            json.loads(raw.decode("utf-8-sig"))
        elif kind == "toml":
            tomllib.loads(raw.decode("utf-8-sig"))
        else:
            ET.fromstring(raw)
    except (SyntaxError, ValueError, UnicodeError, ET.ParseError) as error:
        row["status"] = "failed"
        row["error_type"] = type(error).__name__
        row["line"] = getattr(error, "lineno", None)
        # Do not print source lines or config values that might contain secrets.
        row["message"] = getattr(
            error, "msg", "Could not parse with the available toolchain"
        )[:160]
    return row


def audit(path: Path, output_root: Path) -> dict:
    with path.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    destination = output_root / (path.stem + "-" + digest[:10])
    destination.mkdir(parents=True, exist_ok=True)
    result = {
        "archive": path.name,
        "sha256": digest,
        "source_copy": str(destination.resolve()),
        "integrity": "passed",
        "checks": [],
        "members": 0,
        "source_files": 0,
        "extracted_bytes": 0,
        "omitted_large_files": [],
        "links_skipped": [],
        "build_manifests": [],
        "test_files": [],
        "live_test": "not_run",
        "application_launched": False,
    }
    seen = set()

    def consume(name, size, stream):
        name = safe_member(name)
        result["members"] += 1
        folded = name.casefold()
        if folded in seen:
            raise ValueError("Case-colliding or duplicate archive member")
        seen.add(folded)
        if result["members"] > 40000:
            raise ValueError("Archive file-count limit")
        base, suffix = Path(name).name, Path(name).suffix.lower()
        selected = suffix in SOURCE or base.lower().startswith(
            ("license", "copying", "makefile")
        )
        if path.name in BINARY_BUNDLES:
            selected = name.startswith("assets/") and suffix in {".png", ".jpg", ".ico"}
        if not selected:
            # Consume every stream for CRC/decompression verification, without
            # writing bundled executables or importing dependency runtimes.
            while stream.read(1024 * 1024):
                pass
            return
        if size > 32 * 1024 * 1024:
            result["omitted_large_files"].append(name)
            while stream.read(1024 * 1024):
                pass
            return
        raw = stream.read(32 * 1024 * 1024 + 1)
        if len(raw) != size or len(raw) > 32 * 1024 * 1024:
            raise ValueError("Invalid archive member length")
        result["extracted_bytes"] += len(raw)
        if result["extracted_bytes"] > 512 * 1024 * 1024:
            raise ValueError("Source-copy size limit")
        target = (destination / name).resolve()
        if not target.is_relative_to(destination.resolve()):
            raise ValueError("Source path escaped destination")
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists():
            if target.read_bytes() != raw:
                raise ValueError(
                    "Existing reviewed source differs; preserve it and choose a new output root"
                )
        else:
            with target.open("xb") as file:
                file.write(raw)
        result["source_files"] += 1
        if base.lower() in {
            "pyproject.toml",
            "requirements.txt",
            "package.json",
            "cargo.toml",
            "go.mod",
            "makefile",
        } or suffix in {".csproj", ".lpi", ".lpr"}:
            result["build_manifests"].append(name)
        if (
            base.lower().startswith("test_")
            or base.lower().endswith(("_test.go", ".test.js", ".test.ts"))
        ) and suffix in SOURCE:
            result["test_files"].append(name)
        check = source_check(name, raw)
        if check:
            result["checks"].append(check)

    try:
        if zipfile.is_zipfile(path):
            result["format"] = "zip"
            with zipfile.ZipFile(path) as archive:
                if sum(m.file_size for m in archive.infolist()) > 1024 * 1024 * 1024:
                    raise ValueError("Expanded archive exceeds 1 GiB review budget")
                for member in archive.infolist():
                    if member.is_dir():
                        continue
                    if stat.S_ISLNK(member.external_attr >> 16):
                        result["links_skipped"].append(member.filename)
                        continue
                    with archive.open(member) as stream:
                        consume(member.filename, member.file_size, stream)
        else:
            result["format"] = "tar"
            with tarfile.open(path) as archive:
                expanded = 0
                for member in archive:
                    if member.issym() or member.islnk():
                        result["links_skipped"].append(member.name)
                    if not member.isfile():
                        continue
                    expanded += member.size
                    if expanded > 1024 * 1024 * 1024:
                        raise ValueError("Expanded archive exceeds 1 GiB review budget")
                    with archive.extractfile(member) as stream:
                        consume(member.name, member.size, stream)
    except (OSError, ValueError, zipfile.BadZipFile, tarfile.TarError) as error:
        result["integrity"] = "review_failed"
        result["error"] = str(error)[:240]
    result["static_passed"] = sum(c["status"] == "passed" for c in result["checks"])
    result["static_failed"] = sum(c["status"] == "failed" for c in result["checks"])
    (destination / "maelstrom-audit.json").write_text(
        json.dumps(result, indent=2) + "\n", encoding="utf-8"
    )
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--root", type=Path, default=Path(__file__).resolve().parents[1]
    )
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    results = []
    for path in sorted(args.root.glob("*.zip")):
        result = audit(path, args.output / "sources")
        results.append(result)
        print(
            json.dumps(
                {
                    k: result[k]
                    for k in (
                        "archive",
                        "integrity",
                        "source_files",
                        "static_passed",
                        "static_failed",
                    )
                }
            ),
            flush=True,
        )
    report = {
        "schema_version": 1,
        "projects": results,
        "note": "Integrity and static parsing are not application, behavioral, security or live-game qualification.",
    }
    (args.output / "audit.json").write_text(
        json.dumps(report, indent=2) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
