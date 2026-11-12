# Maelstrom project organization

Maelstrom builds reusable game-player infrastructure. Pet Dance is the first full-visit integration exercise, not a prerequisite for every other game's offline work. The immediate objective is to remove the assistant from repeated menu handling, action selection and test execution.

## Where project truth lives

| Question | Authoritative place |
| --- | --- |
| What are we building? | [Product charter](PRODUCT.md) |
| What order and gates govern delivery? | [Roadmap](ROADMAP.md) |
| What is each track doing next? | [Workstream board](coordination/workstreams.json), explained in [Game workstreams](GAME_WORKSTREAMS.md) |
| Who may edit which files? | Exact reservations in [claims.json](coordination/claims.json), maintained by the coordinator under the [agent guide](coordination/EXTERNAL_AGENT_GUIDE.md) |
| How do components work? | Focused component documents linked from [INDEX](INDEX.md) |
| What actually happened in tests? | Tests, frozen reports and dated research records linked by the workstream |
| How does an outside agent contribute? | Root [AGENTS.md](../AGENTS.md), the [agent guide](coordination/EXTERNAL_AGENT_GUIDE.md), and a scoped brief in `coordination/tasks/` |

The board is an organizer, not a background execution service. It cannot launch games, schedule input, or grant permissions. Local reports may contain private recordings and remain local; share sanitized evidence, never credentials or clipboard contents.

## Current parallel lanes

1. **Shared foundation:** bounded capture and cleanup, registered actions, receipt-aware routine graphs, offline replay and target/context checks. These changes reduce duplicated code across games.
2. **First integration:** Pet Dance gameplay, entry/result/snack/Idle transitions and geometry qualification. Entry/navigation still need evidence; an isolated PERFECT game is not a qualified full visit.
3. **Other games:** Conjuration memory decisions first, with perception separate; prepare board-game, timing, continuous movement and navigation fixtures independently. Dodge remains the continuous movement/learning track.
4. **Knowledge and reuse:** legacy component mapping, snacks/energy, navigation landmarks, quest and spell information. Reuse the existing knowledge database and archive audit rather than scattering facts into new code.
5. **Product workflow:** game workspaces, editable routines, pack authoring and clear reports. A graphical routine editor remains future implementation; JSON graphs are not a finished user-facing editor.

We have three concurrent subagent slots plus the coordinator. More independent work can be assigned to Claude/Gemini using prepared briefs, but parallel agents must not edit the same files or run competing build/desktop jobs. Keep the build slot with the coordinator; serialize live tests separately.

The claims registry is advisory, not an operating-system lock or permission grant. Active reservations name exact paths. Release them after integration or a confirmed stop; inspect current work before reassigning an abandoned claim. Proposed brief paths and board `planned_outputs` are not reservations until a coordinator registers them. Workers return status; the coordinator updates central status.

## A task's life cycle

- Select an unclaimed workstream/task and name the exact files it owns. Resolve dependencies needed for that task, not every future live dependency.
- Read its source references and acceptance criteria. Preserve working behavior and existing uncommitted changes.
- Implement one reviewable increment, test it offline, and return changed paths, commands/results, evidence and unresolved limitations.
- The coordinator reviews cross-component contracts, runs shared checks, reconciles the board and updates the current roadmap/checkpoint.
- Installation and live qualification are separate tasks. A source fix stays unqualified until the relevant runtime evidence exists.

Workstream evidence must say whether it is a code inspection, synthetic simulation, saved game pixels, a supervised demonstration, or autonomous live execution. Keep labels explicit. Do not convert user-assisted cleanup or assumed actions into autonomous success.

## Keep the repository navigable

Use stable workstream IDs in briefs and reports. Link a focused explanation instead of copying it into several status documents. The roadmap is the current ordered plan; the delivery course and dated reports retain the audit trail. Avoid appending a new conflicting “next step” to the bottom of an old document.

Run `python tools/check_project_board.py` after changing the board. It catches invalid references and missing evidence; it cannot establish that a claim is true. A human/coordinator review still verifies evidence meaning. Keep third-party source under existing source/library locations, experiments under `.local`, and reusable fixture data/tests in the appropriate tracked test directories.

Ready-to-assign briefs are in [the external-agent guide](coordination/EXTERNAL_AGENT_GUIDE.md). They prepare independent work; they do not hand off ownership of the whole project or require the user to operate the games.
