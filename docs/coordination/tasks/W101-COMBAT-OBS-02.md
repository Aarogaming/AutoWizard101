# W101-COMBAT-OBS-02 — Combat observations for the shared player

Assigned to Gemini. Read root AGENTS.md, the external-agent guide,
docs/JOB_DECK.md and the active claim registry. This is the next bounded source
contribution; do not create another deck, scheduler, UI or action dispatcher.

## Objective

Bridge observed combat information to the existing combat planner with a strict,
game-independent observation contract and a W101 interpretation layer. The
immediate user example is `Defeat Any Fire Elf and Collect Arrows in Firecat
Alley. (1 of 3)`, leaving two drops/defeats according to the user's progress.
That text is test data, not a quest-specific branch or live authorization. Loot
progress is not necessarily one drop per defeated enemy.

The deliverable must make the next native combat-adapter task concrete. A
planner proposal or a successful dispatch is never a completed battle.

## Exact owned paths

- `tools/w101_combat_snapshot.py`
- `tools/tests/test_w101_combat_snapshot.py`
- `tools/fixtures/combat/observation-contract-v1.json`
- `docs/coordination/results/W101-COMBAT-OBS-02.md`

Read other files only. Root owns the native runtime, current UI reader, hashes,
deck UI, scheduler, MCP/API, project files, board and claims. No new dependencies.
No builds, installers, model jobs, live capture, process/memory access or game
input. Pure Python tests for your component may run single-process with a short
time bound after reporting the proposed command to the coordinator.

## Reuse first

Inspect `tools/combat_turn_planner.py`, `tools/vision_combat_adapter.py`,
`tools/wizsdk_combat_geometry.py`, their tests, `tools/w101_ui_tree.py`,
`tools/fixtures/combat`, the legacy Deimos/wizSDK combat sources already present,
and `docs/coordination/results/W101-COMBAT-01.md`. Distinguish actual captured
current-client evidence from synthetic fixtures and historical UI names/layouts.
Do not modify existing evidence or production hashes.

## Required result

### Integration update — 2026-11-11

Read the accepted [planner repairs](../results/W101-COMBAT-PLAN-03.md),
[owned one-turn contract](../results/W101-COMBAT-LOOP-04.md), and
[structural reader](../results/W101-COMBAT-SOURCE-05.md) before implementing the
bridge. The existing planner now requires explicit known facts, rejects hybrid
effects and unsupported mechanics, enforces seven ordinary/power pip slots for
strict input, and requires `effect_order="application"` for nonempty effect
arrays. Do not fill missing facts with synthetic defaults to satisfy this gate.

The optional reader can return `combatContext`, but that is deliberately
`structural-unqualified`, with raw layout rectangles and no spell metadata,
castability or combatant/round identity. It must remain `ready=false` for a live
turn until those fields and geometry are independently qualified. The native
turn contract binds a snapshot, encounter, round, geometry and semantic card/
combatant identities; it replans before the first input and requires a target
to be explicitly alive before targeting. Map your contract to those existing
types and list missing observations rather than inventing a second executor.

Your four-file reservation remains unchanged. These updates do not grant live
access, native edits, builds or installer ownership.

1. A bounded, JSON-serializable snapshot with exact target/session identity,
   observation time/source, battle phase/turn, player resources, available hand
   controls, enemies/targets, loading/disconnect/takeover flags, and explicit
   unknown values. Support only fields actually supplied by the observation.
2. A pure validator/projector from saved observation data to the existing
   planner's required inputs. Expose a `ready` decision with precise missing or
   contradictory facts. Do not silently default unknown HP, pips, school,
   resistances, spell availability, geometry or targeting to plausible numbers.
3. Keep spell effect metadata separate from live observations. Preserve source
   and version for any reused spell/enemy data. Unsupported enchantments,
   target modes, power-pip rules or geometry must remain explicit.
4. Normalized, target-bound control references; no fixed 1024x768 click points
   treated as current pixels. A future native dispatcher must bind the control
   to the current client geometry and verified screen state.
5. Outcome contract for selected-card/target receipts, round resolution,
   victory/defeat and independently observed quest progress. Missing outcome
   is unknown; a damage estimate or finished animation is not victory.
6. Focused tests for fresh/old/wrong-target snapshots, unknown resources,
   loading/turn transition, reordered hands/enemies, missing targets, contradictory
   UI state, normalized geometry limits, and before/after quest counts.
7. A source map of the exact legacy/current fields needed for the next optional
   UI-tree or visual-reader extension. Name unqualified hooks/layouts clearly.

Do not implement treasure-card spending, purchases, potions, flee, player teaming,
network manipulation, arbitrary hooks or a fallback that presses keys when data
is absent. Do not kill/restart W101. Do not call live input from this module.

## Completion report

Report changed files, commands/results, fixture provenance, available versus
unknown facts, and the smallest native follow-up needed to play one observed
turn through OperationScheduler/GameInputLease. Leave final integration, build,
live qualification and board changes to root. No claim of an operational battle
bot or finished quest without real outcome evidence.
