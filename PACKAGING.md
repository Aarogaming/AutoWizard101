# Project Maelstrom — Portable Packaging

## Goals
- Ship a minimal, safe Core build.
- Exclude DevTools (UiAuditRunner, FunctionalTestRunner) from portable output.
- Plugins are optional; by default no sample plugins are included.
- Execution policy remains safe by default (ALLOW_LIVE_AUTOMATION=false).

## Build commands

### Debug (dev) build
```pwsh
dotnet build ProjectMaelstrom/ProjectMaelstrom.sln -c Debug
```

### Release portable build (default: no sample plugins)
```pwsh
./scripts/build_portable.ps1
```
Outputs to: `artifacts/portable/`

### Release portable build with sample plugins
```pwsh
./scripts/build_portable.ps1 -IncludeSamples
```
Outputs to: `artifacts/portable/` (includes `plugins/_samples`)

## What’s included (portable)
- Core app binaries from `ProjectMaelstrom` (Release).
- Empty `plugins/` folder (for optional installs).
- Default safe `execution_policy.conf` (ALLOW_LIVE_AUTOMATION=false).

## What’s excluded (portable)
- DevTools/** (UiAuditRunner, FunctionalTestRunner).
- Any *TestRunner* outputs.
- `plugins/_samples` unless `-IncludeSamples` is used.
- Other sample/demo assets.

## Runtime notes
- The app runs directly from `artifacts/portable/ProjectMaelstrom.exe` (or published exe if self-contained).
- Plugin loader tolerates missing/empty `plugins/`.
- To add plugins: drop manifests/DLLs into `plugins/` or install via GitHub Release (in-app installer).
- Default policy: `ALLOW_LIVE_AUTOMATION=false`, `EXECUTION_PROFILE=AcademicSimulation` (Public).

## Validation (recommended)
- Launch `artifacts/portable/ProjectMaelstrom.exe` on a clean machine context.
- Confirm it starts with no plugins present (plugins/ empty or missing).
- Confirm plugin loader shows no errors when plugins/ is empty.
- Optionally add a plugin (copy into plugins/) and verify it appears in Plugin Manager.

## Optional: self-contained publish
If you want a self-contained portable:
```pwsh
dotnet publish ProjectMaelstrom/ProjectMaelstrom/ProjectMaelstrom.csproj -c Release -r win-x64 --self-contained true -o artifacts/portable
```
Adjust `build_portable.ps1` as needed if you prefer publish over build+copy.***
