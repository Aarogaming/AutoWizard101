# Wizard101 as one game: reusable routines

Decision recorded 2026-11-08 from the user's workspace design. Maelstrom is a
desktop game-player authoring and experiment system. W101 is a single game
workspace, not a collection of unrelated launch buttons.

The [capability integration map](W101_CAPABILITY_INTEGRATION_MAP.md) assigns all
reviewed archives and connected tools to shared components, optional teachers
and proposed operations. It defines scheduler prerequisites and the path from
recorded teacher behavior to native student packs. These are planned extensions;
the motion editor described below remains the implemented routine subset.

## Product structure

- Auto-Idle is opt-in, with Spin, Step, Routine and Native Idle methods.
- Farming, Pets, Potions and Quests organize saved routines and future players.
  Potions includes mana minigames such as Conjuration; Pets includes pet activities.
  They share the W101 identity and controls.
- Observe & teach opens the existing recording, labeling and pack workflow.

The first builder edits ordered motion clips: add, remove, reorder, name,
categorize, save/reload, select for Auto-Idle, and export a draft pack. The shared
registry defines turn-left/A, turn-right/D, step-forward/W and step-back/S.
These are default bindings, not automatically discovered controls.

Routines live in `routines/wizard101/<id>.json`. They contain action identifiers,
not executable code or arbitrary key strings. Auto-Idle takes a copy of the
selected sequence. Editing another draft cannot mutate a running session.
No selection or load starts input.

Exports use the existing schema-v2 action registry (`id`, `title`, `key`,
`duration_ms`), `game_id: wizard101`, empty skills and `status: draft`.
`routine_draft` preserves category, order and intended outcome gate. The Workshop
can inspect the pack/registry; it does not execute that extension or qualify a
full activity player. Only the explicitly started Auto-Idle runner executes these
motion sequences today. Category names do not implement farming or quests.

## Control model

The editing metaphor is a timeline; live execution is event-driven:

1. Observe the game and evaluate the current step's preconditions.
2. Resolve an allowed action through the shared registry.
3. Dispatch within its duration and resource budget.
4. Observe independently, verify the outcome, then advance.
5. Unknown state, takeover, focus loss, cancellation or timeout pauses/releases.

Current motion guards check fresh view, retained HUD, bounded scene difference
and visible change. They do not yet recognize semantic states or obstacles.
Next additions should extend this model with recognized-state conditions,
bounded waits, branches, bounded repeats and subroutine calls. A minigame player
chooses from the current board/enemies, not a fixed order of positions/events.
Auto-Idle must yield to active minigame/quest controllers; future controllers
must share a single input owner.

## Where Markov models fit

A state machine or behavior tree handles explicit event-conditioned macros.
A Markov model estimates next states from a current state; an MDP adds actions
and rewards. Visual observations are incomplete, so a learned policy may need
history or a belief state rather than treating one frame as the whole state.
Conjuration specifically needs memory of revealed card identities and positions.

These are optional policy layers, not requirements for chaining. Prediction
should propose actions/confidence through the same bounded registry and verified
outcome interface. It must not assume enemies, cards or events arrive in a fixed
order. Keep models out of the fast path unless measurements justify their cost.

## Remaining qualification

- Live one-direction Auto-Idle and takeover checks; an uninterrupted
  keep-connected duration test has not completed.
- Conjuration preview, reveal, matching and result examples and recognition.
- A reusable condition/branch editor and transition schema validated by both
  runtime and replay.
- Activity-specific farming, pets, potions and quest players, tested separately.
- Pack comparison across fresh randomized sessions before unattended play.

This consolidates the original game-pack/action-registry direction rather than
introducing a separate automation framework.
