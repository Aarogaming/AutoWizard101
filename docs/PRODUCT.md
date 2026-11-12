# Maelstrom product charter

Maelstrom is a standalone Windows Desktop-Use Game-Player and assistance overlay,
with a built-in Game Pack Workshop. This repository remains AutoWizard101; the
existing C# shell and Python components are the recovery foundation.

## Intent consolidated from the project conversation

- Reuse the owner's fragmented repositories before writing replacements. Keep
  provenance and qualify actual source behavior rather than trusting feature names.
- Build the shared foundation first, then progress from simple games to difficult
  activities. Wizard101 is the initial test environment, not the limit of the system.
- Observe the actual screen and react to current state. Enemy order and timing are
  variable; demonstration playback is not an autonomous policy.
- Make ordinary use approachable through window selection, clear modes, a visible
  overlay and reliable pause/stop/manual takeover.
- Include authoring: record, label, define, test, save, version and exchange packs.
  Build our own initial packs through the same tools users will use.
- Use rules and existing specialists first. Evaluate optional SLMs and specialist
  routing as evidence accumulates; no trained generic player or MoE exists today.
- Remain independent of Aaroneous, Merlin and Devtools services. Their reusable
  ideas/components are optional; the handoff cycle is finished.

## Product surfaces

Game independence is an architectural requirement: cards, scheduling, ownership,
fresh observations, budgets, verified outcomes and learning/evidence storage
belong to Maelstrom. Wizard101 supplies its first extensive game pack and skin;
future minigames, Minecraft, Combat Arms or offline games provide game-specific
adapters and knowledge. The theme must never turn the shared engine into a
quest-specific or W101-only controller. Naming a future game is not a claim of
current support or permission to enable its online automation.

The owner's November 11 refinement gives the W101 workspace a themed card
interface: a collection grouped into Questing, Pet Games, Minigames and other
activity decks, with a separate Routine Deck containing only selected cards.
Fronts show recognizable artwork, intent, requirements, limits and status;
cards flip to configuration. Generic Quest cards auto-fill from observed
objectives and select registered capabilities rather than hardcoded quest
scripts. Nested routines remain contracts over the same scheduler, not nested
unbounded controllers. Other games can use their own skin over the same engine.
See [the current card implementation](JOB_DECK.md) for its actual limits.

The agreed interaction direction is a dark, IDE-like application: choose a game
as the workspace (the useful process-selection metaphor from Cheat Engine), then
compose visible routine steps with the clarity of iCue's macro tools and a video
editor. General game interaction defaults to UI-only. The owner's 2026-11-10
W101 exception authorizes reviewed client-side functionality, including necessary
hooks, injection and process-memory reads/writes, as optional integrations.
Independent visual operation remains available. Network manipulation is excluded
and installed game files remain read-only. See
[the expanded client-side decision](W101_CLIENT_SIDE_EXCEPTION_20261110.md).
Each game, including Dodge the Creeps,
needs an explicit home for its connection, routines, packs, recordings and runs.

The target layout is a game/workspace sidebar, a central routine canvas, a
selected-step inspector, a live preview and a run/evidence panel. Routine clips
represent named operations with entry conditions, limits, actions, observed
outcomes and cleanup. Branches and waits react to events; time on a timeline does
not imply a fixed sequence of gameplay. Unknown/loading states are explicit.
The shared action registry and scheduler own execution; workspace screens route
to those services rather than implement separate players.

Current delivery: dedicated Dodge workspace routes, and a dark Pet Dance visit
setup with Play → Reward → Snack policy → Native Idle. A draggable timeline,
per-game asset index and integrated docking shell are **not implemented** yet.
First complete the minigame workflow before character switching, home farming,
battles, HP/MP management, navigation, quests and Bazaar restocking. Snack
preferences default to no consumption; Crown Shop purchases are excluded from
the Pet Dance operation. Native-resolution grid mapping is a requirement, not
yet a qualification claim; see [resolution plan](PET_DANCE_RESOLUTION_PLAN.md).

The game is the workspace: choose or launch a specific game, attach its pack and
observations, then Observe/Assist/Play. See [Game workspaces](GAME_WORKSPACES.md).
The default is an external game window with a managed connection and overlay.

| Surface | Purpose |
| --- | --- |
| Play | Select a target, pack and bounded task; Observe, Assist or Play. |
| Overlay | Explain current state, intended action, uncertainty and results; pause/stop. |
| Workshop | Build and test packs from demonstrations and definitions. |
| Library | Find packs, versions, profiles, templates and recordings. |
| Runs | Inspect attempts, evidence, interventions and actual outcomes. |
| Setup | Check target/display/input readiness and optional backend availability. |

These surfaces are planned product organization, not a claim that all screens exist.

## Initial curriculum

Foundation and offline fixtures come first. Then qualify Pet Dance for sequences,
Home Navigation for spatial movement, and Potion Motion for board/pointer actions.
These are candidate first packs; readiness and measured difficulty determine their
exact order. Diego supplies a later real-time challenge and useful early offline
fixtures. Continue to multi-screen routines, small standalone games and longer
questing/combat tasks. Larger tasks require verified smaller skills.

Home trials begin with a user-selected, unoccupied home and short movement between
landmarks. Initially exclude housing edits, item use and leaving the home. This is
controlled live testing, not an offline simulator or proof of zero side effects.

## Current versus planned

Existing: Windows application, overlay components, local tool launcher, guided
Wizard101 adapters, offline Diego observer/reports and synthetic regression tests.
An initial offline Workshop window now edits draft rules, saves/reloads versions
and evaluates labeled sessions with exact-input hashes and expected proposals.
A native Windows home window launches and supervises it. The portable desktop
build bundles private .NET/Python runtimes and keeps Workshop data in `Data/`.
The per-user installer stages updates, registers Windows shortcuts/uninstall, and
keeps installed user data outside the application folder. A portable-data copy
option preserves both originals and existing installed versions.
Connection-only evidence exists; complete autonomous gameplay is not established.

Planned: unified target/action ownership across all paths, Workshop capture/labeling,
portable pack validation/import/export, qualified
generic overlay flow, multi-game progression and optional learned specialists.

Success means a second game can be added principally through a pack, with shared
capture, input, overlay, recording and verification retained. A saved pack, a
successful key dispatch and a completed game are three different outcomes.

## Documentation authority

Use this charter for scope, ROADMAP.md for order, DESKTOP_GAME_PLAYER_PLAN.md for
architecture, GAME_PACK_WORKSHOP.md for authoring and MAELSTROM_STANDALONE_PROFILE.md
for the implemented baseline. Source reviews and reconstruction notes are evidence
appendices. Historical server/handoff material does not set product prerequisites.
