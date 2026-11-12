# Wizard101 working reference

Reviewed 2026-11-11. This is a sourced starting point, **not an exhaustive game
database or an executable specification**. The previous draft mixed game rules,
legacy coordinates and proposed Maelstrom behavior. See the [accuracy audit](coordination/results/W101-KNOWLEDGE-AUDIT-01.md)
for the original findings, source dates and unresolved details.

Most official guides below are undated. Retrieval date is not publication date
or proof of compatibility with every region, client, UI scale or user binding.
Actions still need a fresh selected-game observation and a qualified adapter.

## Default controls

| Control | Documented purpose | Application constraint |
| --- | --- | --- |
| X | Interact | Verify the intended prompt; dialogue can progress a quest. |
| Q / I / M / Esc | Quest journal / pets / map / options | Check the screen and any focused text field. |
| W or Up / S or Down | Forward / backward | Bound movement and verify progress. |
| A or Left / D or Right | Turn left / right | Turning is not lateral movement. |
| Home / End | Home / current world's common area | End is not always Wizard City Commons. |
| Page Down / Page Up | Mark / return to mark | Preserve the user's mark; marking costs mana. |
| Ctrl+T / Ctrl+Shift+Q | Toggle tracking / select nearest quest | Selection is separate from routing. |
| R / Ctrl+R or Num Lock | Chat reply / autorun | Do not confuse chat with movement. |
| Ctrl+O / Ctrl+S | Potion outside duel / shop | Consumption requires policy; Crown Shop stays excluded. |

Sources: [official shortcuts](https://www.wizard101.com/w101playersguide/keyboardshortcuts),
[movement](https://www.wizard101.com/w101playersguide/getting-around1),
[map and marking](https://www.wizard101.com/w101playersguide/getting-around3).
These defaults do not qualify Maelstrom sending every shortcut.

## Navigation, quests and encounters

The map and tracker supply navigation information, not general click-to-teleport.
Quest guidance is partial and distance approximate. Quest Finder searches the
area and then the world. A heading or decreasing distance does not establish a
traversable path. [Quest tracking](https://www.wizard101.com/w101playersguide/questing3),
[map guide](https://www.wizard101.com/w101playersguide/getting-around3).

Nearby creatures can initiate encounters. Sidewalks are useful route preferences,
not universal immunity guarantees. Maelstrom needs localization, obstacles,
encounter margins and arrival evidence. A recorded demonstration is not
autonomous-route qualification. [Encounters](https://www.wizard101.com/w101playersguide/how-to-play4),
[navigation evidence](W101_DESTINATION_NAVIGATION.md).

## Resources, pets and snacks

Health and Mana globes have nearby potion bottles; minigames and Hilda Brewer can
refill potions. Exact refill thresholds, defeat reset values and a 10–20 second
refill claim are not established here. [Official resources guide](https://www.wizard101.com/w101playersguide/how-to-play2).

Read actual inventory count/capacity. Historical 80/150 figures cannot be universal:
the publisher advertised expansion elixirs on October 26, 2026. This is capacity
evidence, not permission to purchase. [Publisher announcement](https://www.play.wizard101.com/news/expansion-elixir-sale).
Full inventory → Bazaar → sell → resume is a proposed Maelstrom workflow, not a
finished capability. Protected items, limits, navigation and sale outcomes need checks.

Six pet games are documented; training uses Energy. Dance replays longer arrow
sequences, ending after five rounds or three failures. Fixed windows and audio
notes belong to measured calibration, not the guide's rules.
[Pet training](https://www.wizard101.com/w101playersguide/pet-games7),
[Dance instructions](https://www.wizard101.com/w101playersguide/pet-games8).

Snack messages distinguish ordinary, liked and loved snacks. Exact bonuses,
stage costs and talent formulas need scoped sources and observed outcomes.
The native planner suggests snacks with reserves and protected items; feeding
stays off by default. [Feeding guide](https://www.wizard101.com/w101playersguide/pet-games6),
[snack planner](PET_SNACK_PLANNER.md), [training preferences](PET_TRAINING_LEVELS_AND_SNACKS.md).

## Combat and geometry

Visible eligibility matters; grey cards are unavailable. Treasure cards are
consumable and have a separate draw interaction.
[Official casting guide](https://www.wizard101.com/w101playersguide/spell-cards3).

Legacy coordinates and target indices in `CombatGeometry.cs` and
`tools/wizsdk_combat_geometry.py` are layout arithmetic, not portable control
bindings. Use measured client geometry, current UI scale and fresh semantic
controls. Missing battle panels can also mean loading, failure, defeat or fleeing;
they are not proof of victory. Verify the intended outcome independently.

`ModernCombatCalculator.cs` and `PetTalentCalculator.cs` are calculation models,
not authoritative current rules. The former's claimed curve differs even from
the historical concept: official January 2023 EU notes distinguish PvE/PvP
curves. This audit does not establish today's replacement coefficients.
Keep estimates separate from observed damage and confirmed victories.
[Historical update notes](https://eu.wizard101.com/game/community/update-notes/january2023),
[source analysis](coordination/results/W101-KNOWLEDGE-AUDIT-01.md).

## Native Idle and actual implementation

Native Idle has local observation evidence. Regional announcements and older
spin advice describe different points in game history; neither a universal
timeout nor unlimited uptime is qualified here.
[Recorded operating context](W101_GAME_RESEARCH_20261108.md),
[publisher rollout announcement](https://www.play.wizard101.com/news/Ahmarra-Arrives-on-North-American-and-European-Servers).

`Utilities/Modules/AutoIdleModule.cs` compiles finite routine graphs; it has no
input API. Native Idle delegates to the menu module; movement methods use bounded
primitives. Execution belongs to the scheduler and qualified input adapter.
This does not prove background A/D delivery or an automatic between-run deck
policy. [Shared modules](REUSABLE_MODULES.md), [current roadmap](ROADMAP.md).

## Still requiring claim-level verification

- Pet XP, per-age Energy, mastery/skip requirements and talent formulas.
- Team Up variants, countdowns, penalties and sigil timing.
- Realm cooldowns, spawn rules and inventory overflow behavior.
- Current damage, critical and pip rules by mode, school and client build.
- Other minigame controls, menu variants and additional UI scales.

The [knowledge contract](GAME_KNOWLEDGE_DATABASE.md) preserves source revision,
dates, region/build, uncertainty, contradictions and qualification. Imported
text and passing arithmetic tests alone never grant actions.

