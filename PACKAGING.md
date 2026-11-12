# Maelstrom Windows distribution

## Current portable desktop build

Run `scripts/build_desktop.ps1` from PowerShell. Add `-Zip` to create a ZIP alongside
its output. Each build creates a fresh folder under `.local/releases/`; it never
cleans an existing folder that might contain user packs.

Open **Maelstrom.exe**. It is the normal Windows application entry point, with an
icon and home window. Choose **Open Game Pack Workshop**. The app starts and
supervises the helper without a command window. Users do not run Python scripts.

The same executable supports the [headless commands](docs/HEADLESS_CLI.md).
The package includes `HEADLESS_CLI.md`; optional wizwalker observations still use
an explicitly selected local adapter repository/environment, not a bundled backend.

Keep the whole release folder together. The EXE has support libraries and private
runtimes beside it; this is a portable folder distribution, not a single-file EXE.
The build includes .NET 9 for Windows x64 and a copy of the existing Python 3.12
runtime, standard library and Tk, with its license. Python module lookup is limited
to packaged paths. It does not install Python or change PATH.

The portable build also includes private `ffmpeg.exe` and `ffprobe.exe` files for
video sampling and independent recording inspection. By default the build uses
the existing Chocolatey FFmpeg distribution; pass `-FFmpegHome` for another
complete build with `bin/ffmpeg.exe`, `bin/ffprobe.exe`, `LICENSE` and `README.txt`.
The copied runtime includes its license, upstream build/source notice, reported
versions and SHA-256 hashes. The current local Gyan build reports GPL v3; check its
notices and source obligations before distributing an installer. Windows Media
Transcoder writes the experimental video itself. Without a usable Windows
encoder or the bundled inspection tools, the preview reports an error.

`portable.mode` selects `<app>/Data/game-packs` and `<app>/Data/reports` for Workshop
content. Use a writable location. Back up Data before replacing the app. A packaged
build without that marker uses `%LOCALAPPDATA%/Maelstrom` for Workshop data. Source
builds continue to use the checkout's `.local` development library.

The package uses an explicit list of Workshop scripts and sample fixtures. It
excludes the repository's large Scripts library and optional game backends. The
portable build currently qualifies the offline Workshop; other legacy features and
external tool environments need separate packaging and compatibility checks.

Build prerequisites: Windows, .NET SDK with package restore access, and the existing
Python 3.12 installation with Tk. Pass `-PythonHome` to select another compatible
local Python installation. No new Python production package is installed.

## Installation and uninstallation

Build the standalone installer with:

```powershell
./scripts/build_installer.ps1 -Payload .local/releases/<portable-build>.zip
```

Open the resulting **MaelstromSetup.exe**. It embeds the application payload and
requires no download or administrator access. It installs versioned application
files under `%LOCALAPPDATA%/Programs/Maelstrom`, creates a Start Menu shortcut,
offers a desktop shortcut, and registers Maelstrom in Windows Installed apps.
Run a newer setup to update; there is no automatic update feed in this preview.

Installed packs/reports live separately under `%LOCALAPPDATA%/Maelstrom`.
The experiment workbench stores plans, copied evidence, decisions and footage
under that data folder's `experiments/`; update and uninstall preserve them too.
The lifecycle verification now includes experiment, decision and footage
sentinels. Portable migration still copies only the existing pack/report types;
keep the complete portable Data folder when moving experiment history.
Setup offers to copy from a portable Data folder. Existing pack versions and
report filenames are skipped, never overwritten; source files stay intact. Packs
are copied as complete version folders with pack.json, while reports are top-level
JSON files. Imports do not execute or qualify packs. Earlier successfully copied
items remain if an import later fails, so retrying safely skips them.

Updates extract to a new unique version directory and switch Windows registration
only after extraction completes. Registration/receipt failure rolls back to the
previous version. Unchanged tracked files from the previous version are then
removed. Modified or unknown files are kept; old locked files can be cleaned on a
later update. A small installation receipt records ownership and hashes.

To uninstall, use **Windows Settings → Apps → Installed apps → Maelstrom → Uninstall**,
or run setup with `--uninstall`. The review screen explains that your data stays.
Only matching recorded application files and owned shortcuts/registration are
removed. Modified/unknown files and packs/reports are retained. A small tombstone
receipt remains in the application folder to support reinstall around retained
files. A locked removal is reported as a retryable error, not silently ignored.
The uninstaller runs a temporary copy of itself outside the app directory so
Windows can remove installed binaries; that temporary copy may remain for normal
Windows temporary-file cleanup.

The old InstallerForm source is preserved but excluded from the build. Its update
feed and recursive-uninstall routines are no longer used. No real installation or
registry changes are performed merely by building a package.

Validation: `dotnet test Installer.Tests/Installer.Tests.csproj -c Release` covers
preservation, registration, rollback, migration and path rejection. Published setup
also supports `--verify-lifecycle <report.json>`: it uses a unique temporary app/data
directory and isolated registry/shortcut locations with the real embedded payload.
This diagnostic does not install into the user's actual Maelstrom directories.

No release signing or clean-machine/full-game qualification has been completed.
The older packaging instructions remain in `docs/history/LEGACY_PACKAGING.md`.

The native Pet Dance player now ships its bounded visual/audio preset under
`Resources/PetDance/default-v1`, with provenance and reference hashes. The
Wizard101 workspace exposes a visible setup and explicit per-game Start/Stop;
installed launches also honor the Tools game-control setting. No development
recordings, Python analysis environment or model service is needed for this player.
See [the player guide](docs/PET_DANCE_PLAYER.md) for qualified settings and the
supervised native Idle return. Package checks must verify the preset survives
publish and run from the installed application directory.

## Explicit CLI installation

`MaelstromSetup.exe --install-cli <new-report.json>` runs the same per-user transaction as the setup window, preserving the existing desktop-shortcut preference. It reserves a new JSON report before mutation, refuses an open app, and reports success/failure with exit 0/1. It does not launch Maelstrom or gameplay. Use only for an explicitly requested installation/update. Existing reports are never overwritten.
