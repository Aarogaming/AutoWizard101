# HandoffTray (Tray + Watcher)

- Windows-only tray helper (net9.0 WinForms). Run from repo root.
- Modes: Off (default), Watch To-Codex, Watch From-Codex, Both.
- Actions: Copy latest HANDOFF_TO_CODEX, run handoff import (toolkit CLI), send latest prompt via API (toggle), open reports folder.
- API send: default OFF, requires env `OPENAI_API_KEY`, per-send confirmation, redacts obvious secrets, writes responses to `artifacts/handoff/reports/OPENAI_RESPONSE_*.md`.
- Artifacts/logs: `artifacts/handoff/**` (ignored).
- Safety: no auto-posting; toggle + confirmation required. No secrets stored.
- CLI dependency: `dotnet` available; runs `dotnet run --project MaelstromToolkit/MaelstromToolkit.csproj -- handoff import --out artifacts/handoff/reports`.
