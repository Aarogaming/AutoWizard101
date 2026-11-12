# Audio recording release and installation — 2026-11-09

The experimental audio-recording update is **packaged, installed and checked**.
Controlled sound isolation passed in both selected/excluded directions. W101
remained on loading, so Dance sounds/arrows could not be qualified. Native Idle
was restored through the game menu and confirmed again after installation.
This release ships opt-in recording; audio-driven play remains unqualified.

Later verification after the user's restart is recorded separately in the
[preview-sound mapping study](PET_DANCE_SOUND_MAPPING_20261109.md): two visits
reached gameplay, supplied all four arrow/sound patterns and returned to
courtyard native Idle. That study used this installed build; no replacement
package was required. The release-time loading observations below remain intact.

## Delivered artifacts and checks

| Item | Result |
| --- | --- |
| Portable archive | [Maelstrom-20261109-122555-562ec5-release.zip](../.local/releases/Maelstrom-20261109-122555-562ec5-release.zip), 194,702,496 bytes; SHA256 `5E7B85C619E694162620BF323691EC8408DFB9E0252C6948342069D4E404787D`. |
| Standalone installer | [MaelstromSetup.exe](../.local/releases/Setup-20261109-123226-e68dac/MaelstromSetup.exe); SHA256 `F520B3BFEBEFB0212794AC38834A25F63A455EFF1D37B50DF5264DFA0A181494`. |
| Active installed directory | `%LOCALAPPDATA%/Programs/Maelstrom/versions/582d427cedc0418b87ba7e3a93ee48b5`; registration retains `0.2.0-preview`. The directory and hashes identify this update. |
| Installed application identity | `ProjectMaelstrom.dll` SHA256 `2F62AB1FF0F5912322D2DEDC08F0213955E162E4E17A3ABB35C2C306BE995C58`, identical to the tested portable build. |
| Installer lifecycle | [Actual embedded-payload test](../.local/w101-audio-study/installer-lifecycle.json) passed install/update/uninstall with audio, timing, packs, reports, recordings, experiments and settings preserved. |
| Actual user data | [Installed verification](../.local/w101-audio-study/installed-verification.json): all 319 prior files retained their size and modification time. This audit is metadata comparison, not a full byte-hash audit. |
| Installed recording | [Three-second requested capture](../.local/w101-audio-study/installed-idle-01/finished.json) verified video; audio finalized 163,611 frames with zero queue drops/errors and confirmed cleanup. No game inputs sent; hooks released. |
| Final game state | [Recorded native Idle banner](../.local/w101-audio-study/installed-idle-01/final-idle.png) remains visible over loading. This confirms Idle, not successful loading recovery. |

The final archive contains 2,411 entries and excludes user `Data` and legacy
`Scripts`. A hidden workspace rendering check created a policy cache after the
initial publish; a candidate archive containing that generated cache was rejected
and renamed `*-rejected-smoke-cache.zip`. Only the `*-release.zip` above was
embedded and installed. The existing update helper invoked the tested installer
engine while Maelstrom was closed, retaining shortcut preferences. No volume,
microphone, system audio route or game-installation files were changed.

Validation: 507 application tests passed in Debug and Release, 14 installer tests
passed, and 25 focused Python audio-analysis tests passed; scoped formatting and
lint checks passed. Packaged guides preserve their pre-installation snapshot;
these workspace receipts record the subsequent installation and final check.

## Delivered behavior

- Video recording has an optional **Record game audio** choice, off by default.
  It captures the selected game process and its children, including music, into
  a separate `game-audio.wav` with packet timing metadata. The MP4 itself has no
  embedded sound. No microphone or system-mix fallback is used.
- Audio startup, cancellation, target loss and recording failures are explicit.
  Reports preserve source timestamps, gaps, uncertain device positions and
  cleanup status. Requesting audio is distinguished from successfully saving it.
- The development tools can compare recorded sound with visual Dance cues and
  observed keys. This is an offline research workflow; it does not yet provide
  a qualified in-app Dance-note recognizer or automatically train a live player.
- Packs, reports and recordings remain in the existing user-data folder during
  update and uninstall. Keep audio WAV and timing sidecars together with their
  video and input evidence when copying a recording.

The completed evidence is in
[Game audio validation](GAME_AUDIO_VALIDATION_20261109.md): owned-process startup
and cleanup, and a passive W101 native-Idle capture. The latter recorded 572
audio packets/5.72 s but only one true video frame in a static scene. It is not a
60 fps gameplay benchmark. Two owned sound hosts subsequently passed both
swapped selected/excluded 697/1209 Hz checks with volume unchanged. The Dance
attempt saved 108.12 seconds of audio with zero writer queue drops, but the
game remained on loading. The supervisor returned it to native Idle and a
follow-up passive capture finalized. This does not qualify Dance notes or prove
gameplay resumed. Exact decoded audio/video synchronization,
held-out direction accuracy and autonomous live play remain separate gates.

## Runtime contents and boundaries

| Component | Required for this candidate | Inclusion/review |
| --- | --- | --- |
| Native app | `Maelstrom.exe`, matching `ProjectMaelstrom.dll`, deps/runtime files and the complete self-contained Windows x64 publish output | The new capture, writer, UI and controlled-verification C# types compile into the existing app assembly. Do not copy only the EXE over an older version. |
| Audio dependencies | Existing NAudio 2.2.1 dependency closure, including Core/WASAPI assemblies | SDK publish must retain the existing transitive assemblies. No newly installed audio package, model or driver is required. |
| Windows interfaces | System `Mmdevapi.dll` and `Ole32.dll` | Use the OS-provided interfaces; do not bundle replacement system DLLs. Unsupported/failed activation must stay explicit. See [backend requirements](PROCESS_AUDIO_CAPTURE.md). |
| Video tools | Existing private FFmpeg/ffprobe and their provenance/license files | Still needed for video inspection/sampling; PCM audio capture itself does not depend on FFmpeg. |
| Workshop | Existing private Python 3.12/Tk and explicitly selected stdlib scripts | Preserve the current independent Workshop runtime and module path restrictions. |
| Offline audio analyzer | `tools/analyze_pet_dance_audio.py` plus its existing NumPy environment | **Not included by the current desktop packaging script.** It remains a checkout research tool. Do not claim it is available in the installed UI or add NumPy implicitly to production. |
| Controlled tone fixture | `Maelstrom.TestProcess` with explicit start/stop markers | Development verification only; not a runtime dependency and not required in the end-user payload. |
| Profiles/evidence/assets | Local Dance calibration, `.local` studies and installed-game asset hypotheses | Keep them out of the generic application payload and user-data removal ledger. Asset names do not qualify a runtime behavior. |
| New audio guides | [Capture/learning overview](GAME_AUDIO_CUES.md), [validation](GAME_AUDIO_VALIDATION_20261109.md), [backend](PROCESS_AUDIO_CAPTURE.md), [offline analysis](PET_DANCE_AUDIO_ANALYSIS.md) | `build_desktop.ps1` now copies these four guides. START HERE describes experimental opt-in capture and checkout-only analysis. This release-preparation checklist is not part of that copied set. |

## Packaging review and repairs

1. In [ProjectMaelstrom.csproj](../ProjectMaelstrom/ProjectMaelstrom/ProjectMaelstrom.csproj),
   the reviewed exclusion condition was
   `Condition="'OfflineWorkshopPackage' == 'true'"`, comparing literal strings.
   It now evaluates the property as
   `Condition="'$(OfflineWorkshopPackage)' == 'true'"` so the desktop script's
   flag excludes `Scripts/**`. The coordinating session confirmed `Scripts` was
   absent from the new build. The portable app also passed hidden workspace
   rendering with its audio checkbox visible and off by default. Inspect the
   final refreshed ZIP as well.
2. [build_desktop.ps1](../scripts/build_desktop.ps1) is the current packaging path.
   The older [build_portable.ps1](../scripts/build_portable.ps1) rebuilds a shared
   `artifacts/portable` folder and produces a different framework-dependent
   layout. It is not the candidate release path described by
   [PACKAGING.md](../PACKAGING.md).
3. [SetupForm](../installer/SetupForm.cs) currently passes the fixed
   `0.2.0-preview` version to installation, and the installer project uses the
   same version. A distinct release identifier or accompanying artifact hash
   is needed to tell this update from older previews. Do not invent an already
   installed version or derive release qualification from the executable name.
4. Installer lifecycle verification now includes binary `game-audio.wav` and
   `audio-timing.json` preservation sentinels alongside video and experiment
   sentinels. Its receipt reports audio/timing preservation. Unit tests exercise
   data retention and reject archive entries under user `Data/`; **14 installer
   tests passed**. Run the published installer lifecycle check on the final
   payload too; source tests alone do not qualify that artifact.

The exclusion, guide-copy and preservation-sentinel fixes are implemented.
The fixed preview version still requires an artifact path/hash to distinguish
releases. Final payload inspection, published installer verification and the
actual update remain release gates.

## Commands for the coordinating session

Run checks serially after source changes. The coordinating session reports
**507 application C# tests, 14 installer tests and 25 Python audio tests passing**
for the current source. None substitutes for checking the final packaged
artifact. Use existing dependencies;
`--no-restore`/`-NoRestore` assumes the appropriate restore assets are present.

```powershell
dotnet test ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test Installer.Tests/Installer.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false
& '.local/envs/dance/Scripts/python.exe' -m unittest discover -s tools/tests -p test_analyze_pet_dance_audio.py -v
```

The current portable candidate was built after the exclusion fix. For another
source rebuild, use:

```powershell
& './scripts/build_desktop.ps1' -NoRestore -Zip
```

Use the exact newly emitted ZIP path; do not guess the latest directory while
another build could be running. Inspect it for the runtime files above and for
the absence of user `Data/`, development `.local/` evidence and excluded legacy
`Scripts/`. Keep the fresh payload intact and preserve build logs/hashes.

```powershell
& './scripts/build_installer.ps1' -NoRestore -Payload '<exact-new-portable.zip>'
& '<exact-new-MaelstromSetup.exe>' --verify-lifecycle '<new-workspace-report.json>'
```

The published `--verify-lifecycle` command uses temporary application/data and
isolated registration/shortcut locations; it does not update the user's real
installation. Check its report and process exit, then inspect the packaged
app's recording option and sidecar behavior with bounded tests. The standard
installer has no silent-install switch in the reviewed CLI; actual update uses
its existing setup workflow after Maelstrom/Workshop and their owned recording
workers have closed. Do not terminate the game or unrelated processes to update.

## Data preservation and installation acceptance

[InstallEngine](../installer/InstallEngine.cs) installs versioned app files under
`%LOCALAPPDATA%/Programs/Maelstrom`; user data lives separately in
`%LOCALAPPDATA%/Maelstrom`. The installer rejects a payload containing `Data/`,
skips `portable.mode`, and removes only recorded application files whose hashes
still match. It preserves modified/unknown files and does not delete user data
on uninstall. A portable copy instead uses its own `Data/` folder.

Before the real update, preserve a hash inventory of existing packs, reports,
recordings and audio/timing sidecars. After update, verify those files remain
unchanged, the install receipt points to the newly checked version, and the
installed executable's audio controls match the tested build. Report actual
installer/portable paths, hashes, test results and any remaining live limitations.
Building alone does not establish installation success.

The optional portable-data importer copies pack versions and top-level reports
only. It does **not** migrate recordings, audio or experiment history. Retain the
entire portable Data folder when moving those histories. Update/uninstall of an
existing installed copy is a separate operation and should preserve all of them.
