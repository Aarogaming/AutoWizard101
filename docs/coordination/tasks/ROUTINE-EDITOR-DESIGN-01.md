# ROUTINE-EDITOR-DESIGN-01 — user interactions for an event-driven routine editor

| Field | Value |
| --- | --- |
| Parent workstream | `CORE-DELIVERY` |
| Task status | Proposed; unclaimed |
| Owner | Unassigned |
| Mode | Documentation and nonexecutable JSON examples only |

Follow [the collaboration contract](../EXTERNAL_AGENT_GUIDE.md). This is an
interaction-design task, not authorization to replace the UI or routine runtime.

## Outcome and inputs

Describe a coherent Windows editing workflow for the existing routine contracts.
Read [W101 routine requirements](../../W101_ROUTINES.md),
[shared operations](../../SHARED_OPERATIONS.md),
[game workspace](../../GAME_WORKSPACES.md),
[delivery course](../../DELIVERY_COURSE.md), and current source:

- `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101Routine.cs`
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/RoutineGraph.cs`
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/RoutineReplay.cs`
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceLifecycle.cs`

Root owns the graph/replay implementation. Check its current API and tests before
describing a control as supported. Existing docs contain historical milestones;
resolve conflicts with the coordinator rather than assuming the latest plan is shipped.

## Proposed ownership

- `docs/design/ROUTINE_EDITOR_INTERACTIONS.md`
- `docs/design/routine-editor-state-examples.json`
- `docs/coordination/results/ROUTINE-EDITOR-DESIGN-01.md`

All app code, designers, routine schemas, tests, game packs, central docs and
release/status files are read-only. No executable prototype, browser app or dependency.

## Scope

Design the user journey: select one game workspace; choose a saved routine or
draft; add allowed actions/observed-state conditions; reorder or branch; set
finite repeat/resource limits; validate; replay offline; inspect outcomes; save a
version; request live start only through the existing explicit control boundary.

Cover action versus wait versus branch, unknown-state handling, per-game context,
Auto-Idle choice, cancellation, takeover, unsaved changes, report history and
read-only review of a failed run. Keep snack consumption a separate disabled
choice. The editor must not imply that naming “Pets”, “Farming” or “Quests” creates
an implemented player. Preserve the user's timeline/macro-chaining metaphor while
showing that gameplay branches on observations rather than a fixed script.

For each proposed control, state whether the current runtime supports it, which
field/API it maps to, and what remains future work. Include keyboard navigation,
clear status text, focus order and error recovery. Use Markdown diagrams/tables
and nonexecutable JSON examples; do not open or operate the user's desktop.

## Acceptance

- A new user can distinguish editing, replay, a proposed action, actual dispatch,
  and independently verified game outcome.
- Loading a draft, importing research, changing a setting or opening a pack never
  starts input or consumes a snack in the proposed flow.
- Repeat counts and budgets are finite, cancellation has visible completion, and
  unknown state has a documented bounded response instead of silent success.
- Examples map to real current contract names; unavailable capabilities are
  visibly planned and not presented as working controls.
- Design covers a small Conjuration routine and an existing Pet Dance session
  without inventing recognition or whole-game navigation functionality.

Return the interaction document, valid JSON examples, source API references and
an integration checklist. No build or live test is necessary for this document task.
