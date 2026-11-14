# W101 data supply audit

Latest continuation: [UI-correlated resource qualification](coordination/results/W101-STATS-54.md). Health/mana/energy/potion current, maximum and percentage now reach the optional shared registry/card path. The [132-property named layout inventory](../research/w101-current-stats-layout.json) and76 collected scalar candidates remain broader research evidence. The original audit below is historical scope, not latest qualification status.

2026-11-12. This is a source inventory plus one bounded current-client observation, not exhaustive introspection of every possible game field.

## Searchable inventory

[Machine-readable declarations](../research/w101-data-supply.json): 975 imported public declarations; 553 reader/helper candidates and 422 action-excluded methods. Each records source path, line, source hash, declared return type and dependencies. Static classification is advisory; reader/helper candidates still require review and current-build qualification. No imported methods were executed during catalog generation. Regenerate with `python tools/audit_w101_data.py`.

| Imported domain | Reader/helper candidates |
| --- | ---: |
| actor_body | 10 |
| behavior_instance | 4 |
| behavior_template | 2 |
| camera_controller | 14 |
| character_registry | 3 |
| client | 25 |
| client_object | 23 |
| client_zone | 3 |
| combat_action | 28 |
| combat_participant | 81 |
| combat_resolver | 4 |
| duel | 50 |
| game_client | 12 |
| game_object_template | 19 |
| game_stats | 119 |
| play_deck | 6 |
| quest_position | 2 |
| render_context | 3 |
| spell | 27 |
| spell_effect | 22 |
| spell_template | 47 |
| teleport_helper | 4 |
| window | 45 |

## What can supply useful data

- UI: visibility, enabled state, tree ancestry, named text controls, menu/dialogue/interaction/Idle recognition. Existing allowlists exclude private branches; do not expose arbitrary chat, account or login text.
- World: player position/heading, zone and quest positions, actor/body state, camera and render geometry. Object coordinates alone do not establish traversable routes.
- Character/stats: health, mana, energy, potion charge, level, currencies, base/bonus stats and school-specific modifiers. Imported health/mana maximum methods sum base and bonus. Current energy uses visible textEnergy; maximum/bonus semantics need verification. Potion charge is floating point and must not be rounded into full bottles.
- Combat: duel phase, participants, spells/effects, hand/deck and combat actions. Structural candidates are distinct from qualified castability and verified outcomes.
- Knowledge: spell/item templates, behavior templates, quests, minigame instructions and zone assets. Reuse [installed archive inventory](W101_LOCAL_INSTALLATION_SOURCES.md): 2,599 archive names, 178,576 indexed entries in 14 selected archives at the earlier snapshot. Those historical counts were not refreshed today; asset names do not prove current server rules or player ownership.

## Running-client evidence

Current process identity and executable SHA256 recorded in `.local/resource-audit-51/identity.json`; hash d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a matches an existing reviewed adapter profile. A three-second UI/quest/pose observer completed with three UI trees and twelve loading samples. Receipt: `.local/resource-audit-51/observation.json`. Root hook restored and disconnect verified. No keyboard/mouse, focus changes, movement, menus or volume changes. This was scoped existing-hook observation, not a passive OS-only inspection. The new stats resource collector was NOT activated.

## Registry qualification and priority

1. Preserve existing qualified UI-state producers and source timestamps.
2. Qualify stats pointer/hook for this exact image, then independently compare health/mana/energy/potions current/max with visible values. New offline resource contracts are in [resource result](coordination/results/W101-RESOURCES-50.md).
3. Add identity, level, zone and cooldown candidates where exact semantics and transitions can be verified.
4. Then inventory, quest details, combat, pets and gardening. Never expose unverified legacy offsets as current facts.

## Requested scan-cycle design (not yet implemented)

One scheduler-owned observer combines active consumers' requested canonical fields. Live mode enables only needed groups; learning mode expands to all qualified gameplay groups within budgets. Fast/medium/slow groups have independent due times but no overlapping reads. Publish an atomic snapshot with sequence, acquisition times, context/wizard identity and per-field quality. Loading/disconnect/character changes invalidate affected groups. Record deltas plus periodic full snapshots for replay. API/MCP/cards consume the cached snapshot; discovery/subscription does not authorize hooks or gameplay. Slow resources must retain their original timestamps, not borrow a fresh UI timestamp. Exclude private data and unqualified layouts even in learning mode.

Completeness limit: neither a source audit nor one UI sample proves every server-side, hidden or dynamically allocated field is available. No memory sweep, network inspection, game-file mutation or new hook was used.
