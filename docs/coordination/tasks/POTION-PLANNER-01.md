# POTION-PLANNER-01 — explicit board moves and connected-group scoring

| Field | Value |
| --- | --- |
| Parent workstream | `W101-POTION-MOTION` |
| Task status | Proposed; unclaimed |
| Owner | Unassigned |
| Mode | Lightweight offline; Python standard library only |

Follow [the collaboration contract](../EXTERNAL_AGENT_GUIDE.md). Claim the paths
before editing and check the workstream inventory for another planner owner.

## Outcome and inputs

Build a small pure board evaluator using the saved
[minigame research](../../MINIGAME_RESEARCH.md). It describes horizontal row and
vertical column moves and connected groups of at least three, including bent
groups. Wrapping is a community-reported behavior awaiting current-client
qualification; it must be an explicit input, not silently assumed.

Inspect the existing [draft pack](../../../tools/fixtures/game_packs/potion-motion/pack.json)
and [expected fixture](../../../tools/fixtures/game_packs/potion-motion/expected.json).
They are Workshop samples, not a complete board solver or a measured refill
strategy. Search existing code before adding another evaluator.

## Proposed ownership

- `tools/potion_motion_board.py`
- `tools/tests/test_potion_motion_board.py`
- `tools/fixtures/potion_motion/board-schema-v1.json`
- `tools/fixtures/potion_motion/synthetic-cases.json`
- `docs/coordination/results/POTION-PLANNER-01.md`

The existing sample pack, generic replay/action registry, C# routine graph, research
ledger and central docs are read-only.

## Scope

Represent a bounded rectangular board, cell identity or unknown, and explicit
movement semantics. Simulate a supplied row/column displacement only under a
declared rule profile. Return resulting boards, four-neighbor connected groups,
and clearly defined heuristic scores as **proposals**, without moving a pointer.
Keep the source board immutable. Bound dimensions to 8×8 and enumeration to a
documented finite candidate count. Unknown wrapping cannot yield a claimed legal
best live move; report that qualification is missing.

Non-goals: image recognition, drag timing, live legality, timer extensions, cascade
refill behavior, scoring/reward formulas, potion-refill thresholds, purchases or
full game automation. Do not add rules merely because similar puzzle games use them.

## Acceptance

- Bent groups count; diagonal-only contact does not.
- Row moves affect only their row; column moves affect only their column.
- Explicit wrapping tests preserve the multiset of known cells; unqualified wrap
  behavior is not silently promoted to a runtime fact.
- Unknown cells do not become an invented color or a confirmed match.
- Equivalent boards/moves produce reproducible results; ties remain explicit.
- Synthetic examples are marked synthetic and linked to the specific saved
  research claim or explicitly stated implementation hypothesis.

Return focused unittest results and a short rules-qualification checklist for a
later saved-footage review. No live test or new web research belongs to this task.
