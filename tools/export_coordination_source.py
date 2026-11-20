"""Preserve local working-tree changes and hash required artifacts for a handoff."""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def git(*arguments):
    return subprocess.run(
        ["git", *arguments], cwd=ROOT, check=True, capture_output=True
    ).stdout


def export(destination):
    destination = Path(destination).resolve()
    # The export is intentionally a local ignored artifact, not an external upload.
    local = (ROOT / ".local").resolve()
    if not destination.is_relative_to(local):
        raise ValueError(
            "Choose a new destination inside this project's .local folder."
        )
    if destination.exists():
        raise ValueError("Export destination already exists; choose a new folder.")
    status = git("status", "--porcelain=v1", "-z")
    changed = git("diff", "--name-only", "-z", "HEAD").split(b"\0")
    untracked = git("ls-files", "--others", "--exclude-standard", "-z").split(b"\0")
    names = sorted({raw.decode("utf-8") for raw in changed + untracked if raw})
    sources = {}
    content = {}
    for name in names:
        path = (ROOT / name).resolve()
        if not path.is_relative_to(ROOT):
            raise ValueError("Source path escapes the workspace.")
        if path.is_file():
            content[name] = path.read_bytes()
            sources[name] = {
                "sha256": hashlib.sha256(content[name]).hexdigest(),
                "bytes": len(content[name]),
            }
        else:
            sources[name] = {"state": "deleted-or-unavailable"}
    patch = git("diff", "--binary", "HEAD")
    if git("status", "--porcelain=v1", "-z") != status:
        raise ValueError(
            "Working-tree paths changed during export; retry after edits settle."
        )
    for name, raw in content.items():
        if (ROOT / name).read_bytes() != raw:
            raise ValueError(
                "A source file changed during export; retry after edits settle."
            )
    destination.mkdir(parents=True)
    (destination / "tracked-changes.patch").write_bytes(patch)
    archive = destination / "working-tree-files.zip"
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as bundle:
        for name, raw in content.items():
            bundle.writestr(name, raw)
    artifact_inventory = []
    for relative in (
        ".local/artifacts/environment-snapshot.json",
        ".local/diego-observer/tutorial-pack/provenance.json",
        "tools/fixtures/diego_observer/v1/manifest.json",
    ):
        path = ROOT / relative
        if path.is_file():
            raw = path.read_bytes()
            artifact_inventory.append(
                {
                    "path": relative,
                    "sha256": hashlib.sha256(raw).hexdigest(),
                    "bytes": len(raw),
                    "includedInSourceArchive": relative in content,
                }
            )
        else:
            artifact_inventory.append({"path": relative, "state": "missing"})
    manifest = {
        "inventoryVersion": 1,
        "repositoryHead": git("rev-parse", "HEAD").decode().strip(),
        "branch": git("branch", "--show-current").decode().strip(),
        "dirty": bool(status),
        "trackedPatchSha256": hashlib.sha256(patch).hexdigest(),
        "sourceArchiveSha256": hashlib.sha256(archive.read_bytes()).hexdigest(),
        "sources": sources,
        "artifactInventory": artifact_inventory,
        "notes": "Patch covers tracked HEAD differences. ZIP preserves present changed/untracked source bytes. Deleted paths are recorded. Ignored environments, builds and tutorial media are not bundled. Base commit/history and licenses remain required; this is not an installer or a host contract.",
    }
    (destination / "inventory.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    return manifest


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    result = export(parser.parse_args().output)
    print(
        json.dumps(
            {
                "sourceFiles": len(result["sources"]),
                "repositoryHead": result["repositoryHead"],
                "dirty": result["dirty"],
            }
        )
    )
