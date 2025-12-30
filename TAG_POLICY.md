# Tag Policy (Quick Reference)

- v1.0.0-gold: canonical baseline; immutable forever; no retagging.
- UX tags (e.g., v1.1.x-ux): cosmetic-only checkpoints; no behavior/tab-order/policy/runtime changes.
- Tooling tags (e.g., v1.0.x-tools): DevTools-only (handoff/automation); must pass selftest/CI gate.
- Before creating any new tag:
  - Run: `dotnet build ProjectMaelstrom/ProjectMaelstrom.sln -c Debug` (PASS)
  - Spot-check UI (if UX tag) at 175% DPI and narrow window width on touched screens (manual)
  - Keep artifacts under artifacts/ux/** ignored
  - Do not refresh baselines; do not touch gold
