# Project Maelstrom

Wizard101 automation toolkit with bots, OCR-driven utilities, and a script runner that can load community scripts from a shared library folder.

## Highlights
- WinForms app (C# / .NET 9) with ready-made bots: Halfang farming, Bazaar reagent buying, Pet Dance launcher.
- Image + OCR pipeline (Emgu CV + Tesseract) for detecting UI elements and reading stats.
- Script Library: drop scripts into `ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/<name>` with a `manifest.json`, then run/stop from the UI.
- Sync indicator shows whether the game window is present, focused, and at the expected resolution.
- Theme toggle: choose between system theme and a Wizard101-inspired palette in Settings.
- Wizard theme palette lives in `ProjectMaelstrom/ProjectMaelstrom/wizard_palette.json` (copied to output). Edit hex colors there to match captured game UI; the app reloads it on launch when the Wizard101 theme is selected.
- Snapshot bridge: the app writes OCR’d snapshots to `Scripts/.cache/snapshot.json` every ~5s so external scripts can read health/mana/gold/sync status without redoing OCR.
- Input bridge: when enabled, the app polls `Scripts/.cache/commands.json` and executes commands (clicks/keys) via the trainer. Format example:
  ```json
  [
    {"type": "click", "x": 500, "y": 400},
    {"type": "key_press", "key": "SPACE", "delayMs": 200}
  ]
  ```
- See `ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/BRIDGES.md` and `commands_example.json` for usage patterns.

## Requirements
- Windows 10/11
- .NET 9.0 Runtime
- Wizard101 installed and running in windowed mode
- OCR.space API key (free tier works)

## Quickstart (Release ZIP)
1) Download the latest release ZIP from the Releases page.
2) Extract and run `ProjectMaelstrom.exe`.
3) Set your game to 1280x720 (or your chosen supported resolution).
4) Open Settings in the app, paste your OCR.space API key, and save.
5) Use the main buttons to launch a bot, or open the Script Library panel to run a custom script.

## Quickstart (Source)
```powershell
git clone https://github.com/yourusername/AutoWizard101.git
cd AutoWizard101/ProjectMaelstrom
dotnet restore
dotnet build --configuration Release
dotnet run --project ProjectMaelstrom
```

## Script Library
- Location: `ProjectMaelstrom/ProjectMaelstrom/Scripts/Library`.
- Each script folder needs a `manifest.json`:
  ```json
  {
    "name": "Sample Echo",
    "description": "Tiny sample script that logs a message.",
    "author": "OriginalAuthorHandle",
    "entryPoint": "run.bat",
    "arguments": "",
    "sourceUrl": "https://github.com/user/repo",
    "requiredResolution": "1280x720",
    "requiredTemplates": []
  }
  ```
- Entry point can be an `.exe`, `.bat`, or other runnable file. It runs with working directory set to the script folder.
- Validation warnings (missing entry point, resolution mismatch, missing assets) are shown in the UI before run.
- Logs per script are written to `logs/<script>.log`.
- Raid utilities are split into individual entries: `raid-az-tool`, `raid-ds-tool`, `raid-lm-tool`, `raid-pl-tool` (they point to their respective `main.py` under `raid-utility-tools-main`).

## Bots
- Halfang Dungeon Bot: Fire wizard (Meteor) at 1280x720; automates dungeon entry/combat loop.
- Bazaar Reagent Bot: Select reagents and auto-purchase in the bazaar at 1280x720.
- Pet Dance: Launches an external PetDance script/exe.

## Sync Indicator
- Green: Window present, focused, and matches the configured resolution.
- Orange: Focus lost or resolution mismatch.
- Red: Window missing or sync check failed.

## Game State Snapshots (OCR)
- Game stats (health/mana/gold) are OCR’d from defined regions per resolution.
- ROI config lives at `ProjectMaelstrom/ProjectMaelstrom/Resources/<resolution>/rois.json` (normalized coordinates). Tweak these if OCR is off.
- Snapshots are consumed by the script runner and future overlays/debug views.

## Troubleshooting
- “Window not found”: ensure Wizard101 is running, windowed, and visible; try running the app as admin.
- OCR errors: re-check API key and network; verify resolution matches configured value.
- Template misses: confirm resolution and assets in `Resources/<res>/...`.
- Script won’t start: ensure `manifest.json` is valid and the entry point exists.

## Development
- Build: `dotnet build`
- Run: `dotnet run --project ProjectMaelstrom`
- Tests: `dotnet run --project ProjectMaelstrom` and click “Run Tests” in the UI (light utility tests).

## TODO
- Capture Wizard101 UI colors/fonts/assets from the installed client and derive an in-app theme that mirrors the game (palette, backgrounds, button styles).
- Add a theme switcher in-app to apply the Wizard101-inspired theme across all forms.
- Automate extraction of reference screenshots from the local Wizard101 install (when available) to keep theme assets in sync.
- Add an explicit exception to allow reading Wizard101 game files/screenshots locally for theme derivation (no modification of game files).
- Add an explicit exception to read/review wizard101.com pages for visual reference when deriving themes (no automation against the site).
- Add a “Launch Wizard101” button in the trainer that starts PlayWizard101 from a detected install path.
- Launch Wizard101 directly from the default install paths (e.g., `C:\ProgramData\KingsIsle Entertainment\Wizard101\Wizard101.exe`) and only prompt for a directory if nothing is found.
- Detect Wizard101 launcher states (login, patcher/updater, and “Play” button) and surface state in the trainer (SmartPlay + Sync) so we know when the game is ready.
- Review in-progress chat tasks to ensure all started items are completed or tracked (input bridge, trainer UI dashboard, external script refactors).

## Safety & Fair Play
- Use responsibly. Respect game terms of service and other players.
- Run in windowed mode; keep an eye on automation and stop immediately if something looks wrong.
- A global stop/pause hotkey and simulation/dry-run mode are recommended next improvements.

## Project Layout
```
ProjectMaelstrom/
  ProjectMaelstrom.sln
  ProjectMaelstrom/           # WinForms app
    Main.cs                   # Main UI + bot launcher + script library panel
    Utilities/                # Logging, storage, sync, library services
    Modules/ImageRecognition/ # Template matching and OCR helpers
    Resources/<res>/...       # Templates per resolution
    Scripts/                  # Bundled scripts and Library for custom scripts
    tessdata/                 # Tesseract data
```
