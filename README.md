# Project Maelstrom

Game automation toolkit, bots, and supporting services for Wizard101 / Maelstrom.

Contents:
- `ProjectMaelstrom/`: WinForms automation client, bots, OCR/vision modules, and script library.
- `MaelstromBot.Server/`: Server/job submission & OpenAI reconciler.
- `MaelstromToolkit/`: planning/packs/policies used by the automation stack.
- `tmp_master/`: vendor/playground workspaces (ingra UI, MCP Windows automation, n8n workflow builder).
- `tools/`: Wizard101 data fetchers and metadata helpers.
- `docs/`: game automation roadmap & workspace notes.

History was preserved from the AAS monorepo via `git subtree split`; this repo is meant to evolve independently and can be re-bridged to AAS via plugins/APIs later.
