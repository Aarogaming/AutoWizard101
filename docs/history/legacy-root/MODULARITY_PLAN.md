# Aaroneous Automation Suite — Core vs Plugin Modularization

## Classification

- **CORE (must ship)**
  - Execution policy / executors: `Utilities/ExecutionPolicyManager.cs`, `Utilities/Execution.cs`, `Utilities/LiveBackendProvider.cs`
  - SmartPlay planning + UI: `Utilities/SmartPlayManager.cs`, `Main.cs` UI wiring
  - Recognition/state: `Modules/ImageRecognition/*`, `Utilities/GameStateService.cs`
  - Script library loading: `Utilities/ScriptLibraryService.cs`
  - UI shell/forms: `Main.*`, `ManageScriptsForm.*`, `SettingsForm.*`
  - Logging/telemetry: `Utilities/Logger.cs`, `Utilities/ReplayLogger.cs`
  - Storage helpers: `Utilities/StorageUtils.cs`

- **PLUGIN CANDIDATE (optional)**
  - Minigame definitions/catalogs
  - Replay analyzers/viewers
  - Overlay widgets
  - Additional script packs / utilities
  - Optional theme packs or design captures

- **DEVTOOLS ONLY (never shipped)**
  - `DevTools/UiAuditRunner`
  - `DevTools/FunctionalTestRunner`
  - Any future tooling under `DevTools/`

## Plan
- Keep CORE minimal in portable builds.
- Use plugin capabilities to gate optional features:
  - `OverlayWidgets`, `MinigameDefinitions`, `ReplayAnalyzers`, `LiveIntegration` (blocked by policy when live disabled).
- Ship optional plugins separately (or `_samples/`), so portable build can exclude them by default.
- DevTools stay out of portable builds.

## Packaging note
- Portable build should include only CORE binaries and safe defaults.
- Optional plugins: keep in `plugins/` alongside policy cache or distributed separately; DevTools excluded.
