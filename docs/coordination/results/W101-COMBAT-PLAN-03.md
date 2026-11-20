# W101-COMBAT-PLAN-03 — Bounded offline planner repair

Status: implemented and checked offline on 2026-11-11; ready for coordinator
integration review. No game access, input, model, new dependency, build or
installation was performed by this task. This is a proposal generator, not a
combat player or evidence that either remaining Fire Elf was defeated.

## Changed files

- `tools/combat_turn_planner.py`
- `tools/tests/test_combat_turn_planner.py`
- This report.

The existing planner was repaired; no second scheduler, input path or solver was
created. Gemini's observation bridge files were not edited. Writes began only
after root confirmed the previously tested portable payload was frozen, so these
later changes are not implicitly part of that installed card/deck checkpoint.

## Repairs and evidence

- Power-pip doubling requires the player's base school to match the spell.
  Balance is no longer treated as universal mastery. School/mastery/shadow pips
  remain unsupported rather than guessed.
- Direct planning and damage calculations reject nonfinite, negative,
  fractional or malformed resources, invalid health ranges and cards. A defeated
  player cannot receive a cast proposal. Combatant IDs must be unique.
- Input bounds: 8 enemies, 64 cards, 64 effects per list, bounded text/numbers.
  Strict dictionary input rejects more than seven combined ordinary/power pip
  slots. Broad direct-construction limits preserve explicitly synthetic matrix
  fixtures; those are not live pip-layout validation.
- Cards with zero accuracy and attacks with zero predicted damage are not
  selected. Heal estimates are capped at missing health.
- Nonzero effects outside the declared spell type are rejected, even when their
  field names are otherwise recognized: an Attack with healing or a Shield with
  damage is an unsupported hybrid, not a flat-effect spell. Explicit serialized
  zeros in inactive effect fields remain accepted.
- Same-magnitude active blades/traps/shields suppress another identical-magnitude
  setup choice. This is a conservative anti-loop heuristic: effect identities
  and distinct stackable variants are not represented, so useful stacking can be
  skipped. Off-school blades/traps are not assumed useful to the player.
- Current hand and enemy indices survive reordered inputs and defeated entries;
  repeated calls preserve their input data. Serialized alternate proposals no
  longer alias mutable internal lists.
- Resistance over 100% cannot generate negative intermediate damage. Damage is
  a conditional estimate if the spell lands, not observed damage or victory.

Independent regression examples include power-pip counts 3 versus 5 for
cross-school versus matching-school casts; 158 estimated damage for a 100-point
hit with 35% blade, 30% trap, 50% shield, 70% pierce and 30% resist; and an
800-point healing cap with 200/1,000 HP. Fixtures are explicitly synthetic.

## Strict dictionary entry point

`plan_combat_from_dict` now requires explicit known values instead of inventing
missing resources. It raises `ValueError` for missing/unknown/unsupported facts.
Existing dataclass construction remains available for explicitly synthetic
fixtures; that convenience is not permission to fill absent live observations.

Required player fields: `id`, `name`, `current_hp`, `max_hp`, `pips`,
`power_pips`, `school`, `gear_damage_boost`, `pierce`, `active_blades`,
`active_shields`.

Required enemy fields: `id`, `name`, `current_hp`, `max_hp`, `school`, `resist`,
`boost`, `active_traps`, `active_shields`, `is_boss`. Supply 1–8 enemies.

Required card fields: `name`, `school`, `spell_type`, `pip_cost`, `accuracy`,
`is_aoe`, plus `base_damage` and `flat_enchant` for Attack; `heal_amount` for
Heal; `buff_percent` for Blade/Trap; or `shield_percent` for Shield. An empty
explicit hand is accepted. Unknown fields are rejected, including unsupported
mechanics passed under new field names. Metadata/provenance and controls must
remain in the observation envelope rather than being silently discarded here.

For nonempty effect arrays, top-level `effect_order` must explicitly equal
`"application"`. The adapter must establish actual application order before
setting that value. If the field is supplied with any other value, it is rejected
even with empty arrays. Nothing is silently sorted or reversed. Independent
ordered-shield cases use 100 base damage and 30 pierce: shields `[50, 20]` yield
64; `[20, 50]` yield 60. Both supplied orders are exercised through direct damage
evaluation and the strict entry point.

This ordering is not universally equivalent to other reused calculators. Local
Kursova `Spells/BaseSpell/Spell.cs::CheckPierce` chooses the strongest shield first
by its smallest damage multiplier. Local Deimos `src/combat_math.py` describes
incoming wards as LIFO and iterates that effect order. Neither those source
comments nor these synthetic tests qualify current W101 client ordering.

Only one attack school may be supplied to the strict helper because modifiers
are scalar, not school-indexed. The observation bridge must establish that those
modifiers apply to that school. Effect identities, critical/block rules, DOT,
drains, variable-pip costs, mastery/archmastery, shadow pips, school-indexed
effects and complex target modes are not implemented. The future live adapter
must identify these mechanics rather than project them into ordinary flat hits.

Root owns the MCP wrapper: catch `ValueError` from this helper and return an
explicit invalid-input result. The existing successful MCP fixture in the owned
planner tests now supplies known synthetic values. The legacy vision adapter's
unknown-card fallback and hardcoded catalog remain unqualified for live use;
this task did not alter that separate component.

## Checks

- Before changes: 9 planner tests passed.
- `python -B -m unittest discover -s tools/tests -p 'test_*combat*.py'`:
  **45 passed** (36 planner, 5 vision adapter, 4 matrix), 0 failures; 0.013 seconds
  reported for test execution. This does not launch a model or game.
- `python -m black --check --workers 1 tools/combat_turn_planner.py
  tools/tests/test_combat_turn_planner.py`: both unchanged after formatting.
- `python -m compileall -q tools/combat_turn_planner.py
  tools/tests/test_combat_turn_planner.py`: passed.
- No separate Python linter was configured or run.

One restricted-shell formatting command stalled. Its exact Python process and
own shell were identified and stopped; only then was the single-process check
retried successfully outside that restriction. No task-owned process remains.

Next integration gate: Gemini's fresh target/turn-bound observation contract,
followed by root's owned action/outcome loop. A returned CAST_SPELL or PASS is
never authorization to dispatch, confirmation of damage, or quest completion.
