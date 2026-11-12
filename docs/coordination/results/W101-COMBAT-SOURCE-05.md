# W101-COMBAT-SOURCE-05 — Combat source and observation audit

Date: 2026-11-11. Status: source review and optional structural observation
implemented offline; native integration and current-client qualification remain
pending. Coordinator expanded the claim to `tools/w101_ui_tree.py` and its test
module. No game process, memory, desktop, network, build or installed game file
was accessed. Tests use synthetic byte arrays and fake clients only.

## Finding

The existing pieces can support a bounded combat loop, but they do not yet supply
a qualified combat observation or a qualified addressed mouse click. The current
UI reader proves loading, four UI states and optional quest context. Reusing the
old combat handler wholesale would quietly add other hooks, unrestricted reads,
unbounded waits and a separate mouse lock outside Maelstrom's owner.

The user-reported Fire Elf objective is context, not a newly observed battle or
proof of how many item drops remain. A battle win and quest-count increment must
be separate outcomes; the collection count need not increase once per defeat.

## Qualified boundary before this increment

- `tools/w101_ui_tree.py`: one reviewed, image-pinned root-window hook; serialized
  loading reads every 250 ms and tree reads every 1,000 ms; missed intervals drop.
  Tree bounds are 384 nodes, depth 16, 128 KiB read bytes and a 4 MiB trace.
- `snapshot()` emits snapshot-local index/parent, name, visible/enabled and a
  narrow text allowlist. It skips decorative/HUD branches except exact opted-in
  quest paths. It emits no rectangle, control type, card identity, participant
  identity, health, pips, school, turn, battle phase or combat outcome.
- `W101UiTreeOperation` validates exactly four named states and optional quest
  context. `ReadOwnedAsync` reuses a parent's lease/pins, records a two-second
  read in a new folder and disconnects before returning. Combat needs its own
  explicit projection/result contract; adding dictionary keys alone violates the
  current native validator.
- `W101DialogueInput` qualifies an addressed, bounded Space pulse only. It does
  not establish that W101 accepts mouse coordinates through ordinary messages.

## Reuse map

Paths in this table refer to the archived wizwalker source below
`ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/wizwalker/wizwalker-master/wizwalker`.
These are **source definitions**, not current-client readings.

| Needed fact | Existing source and exact candidate | Qualification / missing evidence |
| --- | --- | --- |
| Planning opportunity | `combat/handler.py` reads visible `Hand`; Deimos `src/paths.py:55` records `WorldView/PlanningPhase/Alignment/PlanningPhaseSubWindow/SpellSelection/Hand` ancestry. | Root-tree structural candidate can reuse the existing root hook. Visible planning/hand is not a round number. Its disappearance may be animation, loading, a modal or battle end. |
| Actual phase and round | `combat/handler.py` uses `client.duel.duel_phase()` and `round_num()`. `memory/memory_objects/duel.py:337` obtains the current duel through `read_current_duel_base`; phase is the hook export `read_current_duel_phase`. | Requires separately reviewed `DuelHook`, not the active root hook. `Client.in_battle()` converts some read errors to false; that fallback must not become victory evidence. |
| Hand controls | `CombatHandler._get_card_windows()` selects control **type** `SpellCheckBox`, reverses its list and filters visible controls. | Current observer emits names, not types. Type validation, current ancestry and ordering need evidence. Do not cache those controls across turns as the legacy handler does. |
| Card identity/effect | `CombatCard.get_graphical_spell()` follows `Window.maybe_graphical_spell`; card name/type come through `Spell.spell_template`; rank, school, accuracy, enchantment, treasure status, spell ID and effects have explicit readers. | Archived pointer/field offsets are unqualified. Resolve only a bounded supported subset; retain metadata provenance/version. Unknown card is not a generic Balance attack. Current dynamic/enchantment effect must not be replaced by a catalog average. |
| Castability | `CombatCard.is_castable()` negates `Window.maybe_spell_grayed()`. | A game-provided affordance could avoid duplicating every pip rule, but its offset and visual meaning require current evidence. Generic window `enabled` is not equivalent to spell castability. |
| Target/name/health label | `CombatHandler.get_members()` finds `CombatantControl`; `CombatMember` locates child `Name` and `Health`, with Health used for targeting. | Read only exact game-owned branches with bounded text. Duplicate NPC names are normal; a display name alone cannot bind a target. Hidden/invalid health must remain unknown, not zero. |
| Local player versus enemy | `CombatMember.is_client()` compares participant `owner_id_full` with `client_object.global_id_full`; Deimos `Fighter.update_member_caches()` uses participant `team_id`. | Both sides of identity/team comparison need qualified fields; `ClientHook` may be needed for the current client-object root. Screen position or Fire Elf name alone does not establish ownership/team. |
| Resources | `CombatMember` reads participant `num_pips`, `num_power_pips`, `num_shadow_pips`, `player_health`; `game_stats()` provides max HP/current and max mana. | Root-reachable participant objects may avoid a new participant hook, but their layout/pointer/type validation is still new. Archived data predates later pip mechanics. Do not silently map school/shadow pips into normal pips. |
| School, modifiers, resistances | Participant `primary_magic_school_id`, `game_stats`, hanging/aura/shadow effects; Deimos `combat_objects.py` and `combat_utils.py` map these. | Potentially broad, expensive reads. Avoid `class_snapshot` of entire objects. Required modifier coverage must match the planner's supported mechanics; absence of an observed modifier is not proof of zero. |
| Pass and defeat controls | `CombatHandler.pass_button()` uses `Focus`, except visible `DoneWindow/DefeatedPassButton`; flee similarly uses `Flee` or `DefeatedFleeButton`. | `Focus` is the legacy pass control's name, not a request to activate the desktop window. Exact full ancestry must be obtained from a current observation before dispatch. `Draw` spends treasure-card resources and is outside the first adapter. |
| Victory / defeat / progress | Legacy `handle_combat()` ends when `in_combat()` becomes false; existing quest projection can independently read the objective. | No qualified win signal was found in saved UI fixtures. Require an independently observed supported result state and separately compare the same quest's count; a missing hand, timer change or zero targets is insufficient. |

## Geometry and input: the hidden dependencies

`Window.window_rectangle()` reads four integers at archived offset 160. These
bytes already fall inside the current snapshot's 96-byte header read beginning at
offset 80. A new projection can preserve bounded **raw layout rectangles** without
adding read traffic, after checking them against current captures. That does not
make them client-pixel coordinates.

The legacy `Window.scale_to_client()` sums ancestor rectangle origins and applies
`client.render_context.ui_scale()`. The latter uses `RenderContextHook` through
`CurrentRenderContext`, an additional hook not activated by Maelstrom. Do not
assume a 1024x768 baseline, derive UI scale from window width alone, or treat the
fixed constants in native `CombatGeometry.cs` as newly observed coordinates.
Reuse the shared normalized geometry/calibration boundary with a verified transform
or qualify a narrow current scale read. Bind geometry to client size, UI scale,
observation and selected process; resize invalidates it.

`mouse_handler.click_window()` then relies on more than those rectangles:

1. `set_mouse_position()` converts client to screen coordinates and calls
   `hook_handler.write_mouse_position`.
2. That cursor value belongs to `MouselessCursorMoveHook`, which redirects the
   client's `GetCursorPos` and also modifies two game toggle bytes.
3. It sends mouse move/down/up messages with position `lParam = 0`, then moves
   the synthetic cursor out of the client.

Directly importing this helper is not a harmless geometry helper and would not
inherit `RootScope`'s restoration proof. Its `SendMessageW` calls are also not
timeout-bounded. The two valid implementation candidates are a separately tested
native addressed click with current client-pixel geometry, or a narrowly reviewed
mouseless adapter with complete restoration evidence. Neither is currently
qualified; the existing Space result cannot choose between them.

Every action must retain the shared lease, exact PID/start time/HWND/image binding,
fresh observation/plan identity, human takeover and cancellation checks. Selection
and targeting are separate proposals/receipts: re-observe selected-card state and
rebind the target before the second click. An accepted OS message proves dispatch,
not card selection, a cast, damage, victory or quest progress.

## Smallest next implementation

1. Add an explicit opt-in combat structural projection to the existing bounded
   root reader, without expanding its four-state semantics or reading arbitrary
   HUD text. Preserve loading/privacy/bounds and emit missing values as unknown.
   Begin with observed planning/hand ancestry, named target controls, selected
   text and raw rectangles. Give controls per-observation opaque bindings;
   current tree indices are local to one snapshot, not stable target IDs.
2. Qualify card/participant types and only the minimum supported fields from the
   exact current build. The archived text offset was 584 while the already
   repaired current reader uses 712; this is concrete evidence that neighboring
   archived offsets must not simply be trusted. Pin and review every added field.
3. Feed those observations into Gemini's strict snapshot bridge and the existing
   planner. A first limited policy may use **verified castability plus known
   spell effect/target metadata** instead of exact damage forecasts. Such a
   policy must explicitly lack lethal/damage predictions until modifiers are
   covered; it must not fill missing resistance or pips with convenient defaults.
4. Run the native one-turn contract against recorded/synthetic reordered hands,
   same-name enemies, loading mid-turn, resize, absent resources, timeout, failed
   button release and fizzle/round transitions. No input on stale/unknown binding.
5. Only after separate current-game observation and addressed-click qualification
   should Quest cards consume a combat capability. Combat completion, collection
   progress and quest completion remain independent evidence. Navigation to the
   next enemy is another registered capability, not an implied combat side effect.

## Evidence reviewed and limits

- `tools/fixtures/w101_ui_tree_live_20261110.json` contains seven derived structural
  cases for Idle, leaving Idle, settings, X prompt, shop, pay-to-play and spoken
  dialogue. It explicitly is not screenshots, retains source trace hashes and
  contains no combat field/phase/hand fixture. SHA-256:
  `70d5e54160ab6c47997632012e7168aaeee9bdc581234d41e427ba593450a0a7`.
- `tools/fixtures/combat/combat_lookup_matrix.json` is a parameterized planner
  matrix (1,120 entries), not current-client observations or a field-layout test.
- Reviewed current UI worker SHA-256:
  `2d94fa263db106448afdd658ffd485d9fc958999d9b9855cdf6c4054008691e1`;
  loading helper:
  `70f38c0f875bce43d78f2b94e7933cf27a9051eaadad2a73f7743a3dd23f2d19`.
  Current supported executable identity is recorded by the existing review
  fixture; no installed executable was read for this audit.
- Archived `combat/handler.py` SHA-256:
  `e42b6fb06d813732c05511be0ab494965a1dac0decd8becb223ed1566a6c73d5`;
  `combat/card.py`:
  `c77ecc5062fded13be98d788fc8f60ea5699d8fedf5e57fd6a83c72d64435ee0`;
  `combat/member.py`:
  `ac4f518358861c2f387fead5ba2c5ee51def964a6c2021ed3c2ae987b9f51f97`.

The hashes above identify the reviewed baseline, before the following increment.

## Implemented optional structural projection

The worker now accepts `includeCombatContext: true` independently of the quest
context option. Default remains off. `UiClient` and `snapshot()` propagate that
boolean without adding a hook. Existing header bytes provide raw rectangles only
for the structural allowlist; no extra rectangle reads, type/spell/participant
dereferences or combat text reads were added. The existing privacy exclusions,
node/depth/byte/time/trace budgets, target checks and restoration are unchanged.

`combatContext` in result/tree trace has:

- `schemaVersion: 1`, `qualification: "structural-unqualified"`,
  `geometrySpace: "raw-layout-unqualified"`, `metadataQualified: false` and
  `castabilityKnown: false`.
- Candidate `planningVisible` / `handVisible` booleans under exact expected
  ancestry; absence does not establish battle end or a turn.
- At most 64 controls, each carrying snapshot-local `index` / `parent`, `name`,
  finite `kind`, effective visible/enabled booleans and nullable `rawRectangle`.
  Coordinates are four integer layout values bounded to -65,536 through 65,536
  with positive width/height. Invalid geometry becomes null, never guessed.
- Kinds: `planning-container`, `hand-container`, `hand-descendant`,
  `combatant-container`, `combatant-label`, `done-container`, `button-candidate`.
  An enabled control is not a castable spell and a hand descendant is not
  necessarily a card.

The context is null without opt-in, the reviewed layout, a complete tree and a
unique Root/WorldView. Duplicate planning/hand containers, incomplete relevant
ancestors, invalid/duplicate/orphan/cyclic indices and control-budget overflow
also yield null. Loading and failed tree/cleanup reads clear it. The outer
`statesObservedUtcTicks` / tree-event timestamp identifies the observation;
no second fabricated timestamp or stable-combat claim is introduced.

New worker SHA-256 after this increment:
`58fbee909a67145d400b848b037acde1db843013d2662384321d7cc2c3129b19`.
Root owns the native DTO/CLI and source-review pin updates; the existing pin
consistency test must pass after those are integrated. No input adapter or new
Quest-card capability is enabled by this projection.

Validation run here:

- Six `CombatTreeTests` passed: opt-in/read-byte parity/no combat text, privacy
  and ancestry, hidden/disabled parents, malformed and incomplete projections,
  control budget, geometry bounds, snapshot-local IDs and absent WorldView.
- Two `ContractTests` passed, including rejecting nonboolean combat opt-in.
- Three `-k combat` tests passed, including two new observer cases covering
  loading/error/cleanup clearing and trace propagation (the third overlaps a
  structural test; these counts are not additive unique coverage).
- Eight new test methods total. Final scoped Black formatting and syntax checks
  passed after the last malformed-input repair; report whitespace is clean.

No build, live hook or game input ran. Full observer/native integration checks
and central board/claim updates remain with the coordinator.
