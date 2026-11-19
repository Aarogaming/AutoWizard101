# Maelstrom — Desktop-Use Game-Player

Maelstrom is being rebuilt as a standalone Windows game player and assistance
overlay, with a built-in **Game Pack Workshop**. We start with shared foundations,
learn from simple minigames, and progress toward larger games and longer tasks.
This is the AutoWizard101 repository; existing code is the starting material.

## Start here

- [Worker manual](WORKER_MANUAL.md): one page for agents and contributors: claim, stay in scope, prove, hand back.
- [Project organizer](docs/PROJECT_ORGANIZATION.md): current workstreams, ownership and evidence rules.
- [Workstream board](docs/GAME_WORKSTREAMS.md): legacy projects, minigames and independent background tasks.
- [Parallel-agent guide](docs/coordination/EXTERNAL_AGENT_GUIDE.md): scoped assignments for Codex, Claude or Gemini; read [AGENTS.md](AGENTS.md) before editing.
- [Product charter](docs/PRODUCT.md): agreed scope, user experience and current status.
- [Roadmap](docs/ROADMAP.md): ordered milestones and acceptance gates.
- [Desktop player architecture](docs/DESKTOP_GAME_PLAYER_PLAN.md): shared core versus game packs.
- [Game Pack Workshop](docs/GAME_PACK_WORKSHOP.md): create, record, label, test, save and exchange packs.
- [Experiment workbench](docs/EXPERIMENT_WORKBENCH.md): compare private game trials, preserve candidates and review gameplay footage.
- [Optional W101 home movement](docs/HOME_MOVEMENT.md): bounded steps with explicit Start/Stop and pause on desktop activity; live W101 qualification pending.
- [Implemented baseline](docs/MAELSTROM_STANDALONE_PROFILE.md): actual runtime and limitations.
- [Documentation index](docs/INDEX.md): implementation guides, reuse evidence and history.

## What exists today

The repository contains a C# WinForms application, overlay components, guided
Python activity adapters, local tool setup and an offline Diego observer with
validated reports and synthetic fixtures. These are reusable components, not yet
a finished generic game player. The Workshop, visual teaching and isolated Dodge
experiment workflow exist; a unified multi-game execution runtime remains in development.
Live connection checks do not establish successful autonomous gameplay.

Existing entry points:

- **`Maelstrom.exe`** in a portable release: normal Windows home window with a supervised Workshop and private runtimes. Build with `scripts/build_desktop.ps1`; see [packaging](PACKAGING.md).
- **`MaelstromSetup.exe`**: per-user installation/update, Windows shortcuts and uninstall registration. Packs and reports are kept during update/uninstall. Build with `scripts/build_installer.ps1 -Payload <portable.zip>`.

- **`Launch Game Pack Workshop.cmd`**: create/edit draft rules, save/reload versions and evaluate offline sessions. [Quick guide](docs/WORKSHOP_USER_GUIDE.md).

- `Launch Tools.cmd`: current tools and guided activity launcher.
- `tools/replay_session.py`: validate and summarize an offline schema-v1 session.
- `tools/create_game_pack.py`: create a data-only draft pack in `.local/game-packs/`.
- `tools/fixtures/game_packs/`: two sample pack definitions and a synthetic session.
- `tools/evaluate_game_pack.py`: hash-bound offline replay evidence, with no dispatch.
- Native `--replay-routine`: [rehearse reusable transitions](docs/ROUTINE_GRAPHS.md) against saved session events, with zero desktop input.
- `tools/check_project_board.py`: validate task dependencies, status and evidence links.
- `Launch Diego Observer.cmd`: offline observer; not an autonomous player.
- [Setup and usage](docs/TOOLS_USER_GUIDE.md), [guided activities](docs/GUIDED_ACTIVITIES.md),
  [observer](docs/DIEGO_OBSERVER.md), [integration setup](docs/WIZARD101_INTEGRATION_GUIDE.md).

## Build direction

Observe → understand state → select a skill → propose a bounded action → execute
through one input owner → verify the result. Recording and the overlay expose this
loop. A game pack supplies controls, detectors, skills, fixtures and outcome checks.

The initial candidates are Pet Dance, Home Navigation and Potion Motion, followed
by Diego's real-time challenges. Exact order depends on readiness; foundations and
pack authoring come first. Later stages cover multi-screen routines, standalone
games and extended questing/combat. Optional models must earn their place through
comparison with simpler policies.

## Development

Keep the current .NET 9 Windows / Python foundation. Do not replace the application
or import a host framework merely to reuse a component. Aaroneous, Merlin and
Devtools are optional sources of reusable ideas, not runtime prerequisites.

```powershell
dotnet build ProjectMaelstrom/ProjectMaelstrom/ProjectMaelstrom.csproj
dotnet test ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj
.local/envs/dance/Scripts/python.exe -m unittest discover -s tools/tests
git diff --check
```

The Python command assumes the existing local evaluation environment; a portable
clean-machine installer remains planned. See the standalone profile for dependencies
and publishing. Live execution stays subject to the existing local policy controls.

Source remains in `ProjectMaelstrom/`, adapters/observer tools in `tools/`, and
plans/evidence in `docs/`. Reference clones under `.local/reference-repos/` are
ignored source material, not production dependencies. Earlier server, tray and
handoff utilities are retained; [historical documentation](docs/history/) explains
them without making them part of the new player architecture.
