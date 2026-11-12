param(
    [string]$PythonHome,
    [string]$FFmpegHome,
    [switch]$Zip,
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $PythonHome) {
    $devPython = Join-Path $repo '.local/envs/dance/Scripts/python.exe'
    $PythonHome = & $devPython -c 'import sys; print(sys.base_prefix)'
    if ($LASTEXITCODE -ne 0) { throw 'Cannot locate the existing Python runtime.' }
}
$PythonHome = [IO.Path]::GetFullPath($PythonHome)
foreach ($required in @('python.exe', 'pythonw.exe', 'python312.dll', 'Lib/tkinter', 'tcl', 'LICENSE.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PythonHome $required))) { throw "Python 3.12 with Tk is required: missing $required" }
}
if (-not $FFmpegHome) { $FFmpegHome = Join-Path $env:ProgramData 'chocolatey/lib/ffmpeg/tools/ffmpeg' }
$FFmpegHome = [IO.Path]::GetFullPath($FFmpegHome)
foreach ($required in @('bin/ffmpeg.exe', 'bin/ffprobe.exe', 'LICENSE', 'README.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $FFmpegHome $required) -PathType Leaf)) { throw "A complete FFmpeg build with source and license notices is required: missing $required in $FFmpegHome" }
}
$ffmpegOutput = @(& (Join-Path $FFmpegHome 'bin/ffmpeg.exe') -version)
if ($LASTEXITCODE -ne 0 -or $ffmpegOutput.Count -eq 0) { throw 'FFmpeg did not report its version.' }
$ffmpegVersion = $ffmpegOutput[0]
$ffprobeOutput = @(& (Join-Path $FFmpegHome 'bin/ffprobe.exe') -version)
if ($LASTEXITCODE -ne 0 -or $ffprobeOutput.Count -eq 0) { throw 'ffprobe did not report its version.' }
$ffprobeVersion = $ffprobeOutput[0]
# Each build gets a fresh output. Never clean a folder that could contain user data.
$release = Join-Path $repo ('.local/releases/Maelstrom-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6))
New-Item -ItemType Directory -Path $release | Out-Null
$restoreArgs = @(if ($NoRestore) { '--no-restore' })
& dotnet publish (Join-Path $repo 'ProjectMaelstrom/ProjectMaelstrom/ProjectMaelstrom.csproj') -c Release -r win-x64 --self-contained true -m:1 -p:UseSharedCompilation=false -p:PublishSingleFile=false -p:OfflineWorkshopPackage=true -o $release @restoreArgs
if ($LASTEXITCODE -ne 0) { throw 'Windows application publish failed.' }

$runtime = Join-Path $release 'runtime/python'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$videoRuntime = Join-Path $release 'runtime/ffmpeg'
New-Item -ItemType Directory -Path $videoRuntime -Force | Out-Null
foreach ($name in @('ffmpeg.exe', 'ffprobe.exe')) { Copy-Item -LiteralPath (Join-Path $FFmpegHome "bin/$name") -Destination $videoRuntime }
foreach ($name in @('LICENSE', 'README.txt')) { Copy-Item -LiteralPath (Join-Path $FFmpegHome $name) -Destination $videoRuntime }
@{ ffmpeg = $ffmpegVersion; ffprobe = $ffprobeVersion; files = @(foreach ($name in @('ffmpeg.exe', 'ffprobe.exe')) { @{ name = $name; sha256 = (Get-FileHash -LiteralPath (Join-Path $videoRuntime $name) -Algorithm SHA256).Hash } }); notice = 'See LICENSE and README.txt for license, build configuration, and source information.' } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $videoRuntime 'provenance.json') -Encoding utf8
Get-ChildItem -LiteralPath $PythonHome -File | Where-Object {
    $_.Name -in @('python.exe', 'pythonw.exe', 'python3.dll', 'python312.dll', 'LICENSE.txt') -or $_.Name -like 'vcruntime*.dll'
} | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $runtime }
foreach ($folder in @('DLLs', 'tcl')) {
    Copy-Item -LiteralPath (Join-Path $PythonHome $folder) -Destination $runtime -Recurse
}
$librarySource = Join-Path $PythonHome 'Lib'
Get-ChildItem -LiteralPath $librarySource -Recurse -File | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($librarySource, $_.FullName)
    if ($relative -notmatch '(^|[\\/])(site-packages|__pycache__|test|tests|idlelib)([\\/]|$)' -and $_.Extension -ne '.pyc') {
        $target = Join-Path (Join-Path $runtime 'Lib') $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $target
    }
}
# Restrict module lookup to the package, independent of system Python and PATH.
@('.', 'Lib', 'DLLs', '../../tools') | Set-Content -LiteralPath (Join-Path $runtime 'python312._pth') -Encoding ascii
$tools = Join-Path $release 'tools'
New-Item -ItemType Directory -Path $tools | Out-Null
foreach ($name in @('game_pack_workshop.py', 'game_packs.py', 'game_data.py', 'game_session.py', 'evaluate_game_pack.py', 'compare_pack_versions.py', 'compare_wsl_dodge_runs.py', 'replay_session.py', 'create_game_pack.py', 'maelstrom_mcp_server.py', 'maelstrom_deck_client.py', 'w101_interaction_policy.py', 'combat_turn_planner.py')) {
    Copy-Item -LiteralPath (Join-Path $repo "tools/$name") -Destination $tools
}
New-Item -ItemType Directory -Path (Join-Path $tools 'fixtures') | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'tools/fixtures/game_packs') -Destination (Join-Path $tools 'fixtures') -Recurse
Copy-Item -LiteralPath (Join-Path $repo 'tools/fixtures/godot_dodge_native_probe.gd') -Destination (Join-Path $tools 'fixtures')
Set-Content -LiteralPath (Join-Path $release 'portable.mode') -Value 'Maelstrom portable data mode v1'
Copy-Item -LiteralPath (Join-Path $release 'ProjectMaelstrom.exe') -Destination (Join-Path $release 'Maelstrom.exe')
Copy-Item -LiteralPath (Join-Path $repo 'docs/WORKSHOP_USER_GUIDE.md') -Destination (Join-Path $release 'Workshop guide.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/EXPERIMENT_WORKBENCH.md') -Destination (Join-Path $release 'Experiments guide.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/HOME_MOVEMENT.md') -Destination (Join-Path $release 'Home movement guide.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/W101_ROUTINES.md') -Destination (Join-Path $release 'W101_ROUTINES.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/W101_LEGACY_QUALIFICATION.md') -Destination (Join-Path $release 'W101_LEGACY_QUALIFICATION.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/W101_LEGACY_TEACHERS.md') -Destination (Join-Path $release 'W101_LEGACY_TEACHERS.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/PET_DANCE_TEACHER.md') -Destination (Join-Path $release 'PET_DANCE_TEACHER.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/W101_KEEP_ACTIVE_LIVE_TEST.md') -Destination (Join-Path $release 'W101_KEEP_ACTIVE_LIVE_TEST.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/GAME_WORKSPACES.md') -Destination (Join-Path $release 'GAME_WORKSPACES.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/EXISTING_TEST_GAMES.md') -Destination (Join-Path $release 'EXISTING_TEST_GAMES.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/SHARED_OPERATIONS.md') -Destination (Join-Path $release 'SHARED_OPERATIONS.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/HEADLESS_CLI.md') -Destination (Join-Path $release 'HEADLESS_CLI.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/JOB_DECK.md') -Destination (Join-Path $release 'JOB_DECK.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/ISOLATED_WSL_DODGE.md') -Destination (Join-Path $release 'ISOLATED_WSL_DODGE.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/GAME_CONTEXT.md') -Destination (Join-Path $release 'GAME_CONTEXT.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/DODGE_POLICY_COMPARISON.md') -Destination (Join-Path $release 'DODGE_POLICY_COMPARISON.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/DODGE_ENDURANCE_TRAINING.md') -Destination (Join-Path $release 'DODGE_ENDURANCE_TRAINING.md')
foreach ($name in @('GAME_AUDIO_CUES.md', 'GAME_AUDIO_VALIDATION_20261109.md', 'PROCESS_AUDIO_CAPTURE.md', 'PET_DANCE_AUDIO_ANALYSIS.md')) {
    Copy-Item -LiteralPath (Join-Path $repo "docs/$name") -Destination (Join-Path $release $name)
}
Copy-Item -LiteralPath (Join-Path $repo 'docs/PET_DANCE_PLAYER.md') -Destination (Join-Path $release 'Pet Dance player guide.md')
foreach ($required in @('profile.json', 'audio-comparison.json', 'Neutral.png', 'Up.png', 'Down.png', 'Left.png', 'Right.png', 'Watch.png', 'Go.png', 'Resolving.png', 'Success.png', 'Failure.png', 'Loading.png', 'PROVENANCE.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $release "Resources/PetDance/default-v1/$required") -PathType Leaf)) { throw "Pet Dance preset missing from published app: $required" }
}
@'
Open Maelstrom.exe to start. Keep this folder's runtime and support files together.
Your packs, recordings, workspace preferences and reports are stored in Data. Back up that folder before replacing the app.
Open Game workspace to Observe a window. Record video (preview) saves up to three minutes; choose a start time and sample a local video at 1–60 frames per second for labeling (up to 600 samples).
Local test games can be opened from the workspace when separately prepared in the AutoWizard101 folder; see EXISTING_TEST_GAMES.md.
Video capture is experimental: inspect the reported frame rate, dropped frames and timing error. Video samples can miss fast actions.
Record game audio is an optional experimental video setting, off by default. It saves the selected game's sound and music in a separate game-audio.wav with audio-timing.json; the MP4 has no embedded audio. No microphone or desktop-mix fallback is used.
Audio recording does not enable game control or recognize Dance arrows. See GAME_AUDIO_CUES.md for measured limits. Offline Dance audio analysis remains a development-checkout tool; its NumPy environment is not included in this package.
The separate Pet Dance player uses the bundled visual/audio preset and explicit Start/Stop controls for one supervised game. See Pet Dance player guide.md. Optional automatic native Idle is experimental; its capture-handoff repair needs live validation, and supervised cleanup remains available. It does not feed snacks or change volume.
Use Workshop to edit rules and replay them offline. Game control is not enabled in Observe.
Open Wizard101 Card collection & Routine Deck to configure cards and deal only the ones you want to run. Cards marked Preparation only are not connected players. See JOB_DECK.md for conditions, limits and the local API/MCP bridge; optional development-only MCP tools need their separately reviewed components.
No installation, system Python, or system .NET runtime is required.
'@ | Set-Content -LiteralPath (Join-Path $release 'START HERE.txt')
if ($Zip) { Compress-Archive -Path (Join-Path $release '*') -DestinationPath ($release + '.zip') }
Write-Output "Portable app: $(Join-Path $release 'Maelstrom.exe')"
