# Build outputs and retention

Read-only inventory: 2026-11-09 local / 2026-11-10 UTC. No files were removed.
These are logical file sizes from a bounded metadata inventory, not measured
reclaimable disk space or an assertion that every artifact is disposable.

| Inspected category | Observed size/count | Treatment |
| --- | --- | --- |
| Known app/test/helper/installer `bin` and `obj` | About 1.68 GiB; 6,165 files | Regenerable compiler outputs; clean only with no related build/test/review running. |
| `.local/releases` ZIPs | 57 files; about 11.15 GiB | Retain selected current/known-good rollback packages before removing superseded duplicates. |
| Generated release folders | 60 app folders and 51 installer folders | Full sizes not measured. Newest two app folders were about 462.3 MiB each; newest two setup folders about 319.6 MiB each. Do not extrapolate those figures as measured totals. |
| Release folders with `Data` | 26 folders | Inspect/preserve user data before any whole-folder removal. |

Newest named generated artifacts at this inventory were
`Maelstrom-20261109-220552-9b4956` and `Setup-20261109-220719-c0e02b`.
This did **not** independently establish which version is installed or which
release is the known-good rollback. There was no current/retention manifest at
the release root; build scripts create unique folders and do not prune them.

## Avoiding the wrong build

The latest tested framework-dependent application output is currently:

```text
ProjectMaelstrom/ProjectMaelstrom/bin/Release/net9.0-windows10.0.22621.0/ProjectMaelstrom.dll
```

The sibling `win-x64/ProjectMaelstrom.dll` came from an earlier publish/build and
did not contain the new entry-image review command. Three bounded review launch
attempts mistakenly used that stale output and reached their process deadlines;
they provide no recognition evidence. Their task-owned processes were terminated.
The corrected command uses the freshly tested assembly and records its hash.

Do not infer the active target from a folder name. Resolve the project's current
`TargetPath` for the chosen build configuration, verify the build result, and
record the assembly/package hash in the experiment receipt. A useful next delivery
increment is an explicit tested-build pointer and version-aware retention tool;
neither should promote an untested build or delete user data automatically.

## Preservation and later cleanup

Preserve game packs, profiles, recordings, studies, reports, source archives at
repository root, and every release-local `Data` directory. The user-supplied
legacy ZIPs are source material, not disposable build products.

Before a cleanup task, identify installed-current and known-good rollback
versions, reserve their packages, list exact candidate paths/sizes, and exclude
any active build or process. Preserve and verify data independently before
removing obsolete release binaries. Then verify the retained build and its data
paths. No recursive deletion should be based only on age or a name glob.

The existing `scripts/cleanup_artifacts.ps1` handles zero-byte ZIPs, verification
folders and UI scratch files; it is not a version-aware release retention tool.
It was not run during this inventory.
