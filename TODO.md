# Project Maelstrom – Consolidated To-Do (Priority Snapshot)

Last updated: 2025-12-28

## P1 – UX / UI
- Widen left menu buttons and center text; fix clipping in Manage Scripts dialog.
- Apply consistent dark Wizard101 theme (navy + gold) across all forms, including top bar and About/Manager dialogs.
- Give Script Loader more breathing room; move Script Library into Settings when SmartPlay handles loading.
- Add overlay stub: always-on-top toggle showing SmartPlay status/playlist; lightweight HUD.

## P1 – SmartPlay & Safety
- Enable SmartPlay by default (idle until tasks selected); hide Input Bridge toggle from main menu and manage under SmartPlay.
- Dev mode guardrails: structured logging, no password logging, focus guard before dispatching clicks/keys.
- Wire behavior/FSM scaffolding for task orchestration (start with behavior trees + FSM states).

## P1 – GitHub integration & updates
- Surface GitHub connection status (unauth vs token), rate-limit info, and retry time in Settings.
- Use new `tools/github_metadata_refresh.py` to refresh repo metadata post-rate-reset; show banner when rate-limited.
- Add provenance/credits display in Script Manager and About (per imported scripts/packs).

## P2 – Vision/OCR & Templates
- Expand template library (Resources/Templates) for HUD: mana/health/energy, mini-game cues, start/score screens.
- Tune OCR preprocess defaults (whitelist/PSM) per HUD fields; measure accuracy at 1280x720.
- Add region-based template click helpers for common mini-game interactions.

## P2 – Overlay & In-Game Mini-Games
- Model in-game mini-games as library entries (not app-side games); track stats/outcomes and SmartPlay recommendations.
- Overlay: show current mini-game, timers, cues, SmartPlay playlist, and quick pause/end.

## P2 – Telemetry & Data
- Define local telemetry schema (SQLite) for runs: reaction times, accuracy, streaks, energy/mana spending.
- Export/import profile + telemetry; privacy-first (local by default).

## P2 – Installer / Project Manager
- Ensure Project Manager handles version check before launch; portable ZIP option; stable/beta channels.
- Add error-log creation for installer; share diagnostics with SmartPlay (dev mode).
- Add “Create Portable” and “Fresh Install/Update” flows with version compare.

## P3 – Behavior & Pathing
- Path graph for common hubs (bazaar, mini-games, pet pavilion) using public maps/user captures.
- Monster avoidance/seeking toggles; resource routing (reagents/wooden chests) with known spawn points.

## P3 – Data Sources & Credits
- Keep wizard101_data_sources.json current; add attribution pipeline for all sourced bots/scripts/maps.
- Ensure open-source license notices bundled; About shows credits and source URLs.

## P3 – Future (optional)
- YOLOv5 detector prototype for HUD/inventory markers (offline/user captures only).
- Offline RL experiments in simulated environments (no live game interaction).

