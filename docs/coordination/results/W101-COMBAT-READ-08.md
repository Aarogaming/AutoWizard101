# W101-COMBAT-READ-08 — Bounded rooted combat fields

Date: 2026-11-11. Owner: `combat_reader`. Status: needs integration review;
offline implementation verified, current-client fields **not qualified**.

## Implemented contract

`tools/w101_combat_reader.py` exports `ControlBinding(index, address, kind)` and
`await read_combat_fields(read_bytes, controls, *, layout_id)`. The coordinator's
root traversal supplies the private addresses, snapshot-local indices and kind
(`card` or `participant`). This module does not open processes, install hooks,
scan memory, write memory, dispatch input or read text outside spell templates.
The coordinator retains target validation, ancestry/privacy filtering, ownership,
loading/cancellation, the hard worker deadline and clean disconnect.

Supported layout IDs are explicitly selected: `archived-wizwalker` and
`deimos-development-df2283f`. No offset probing/fallback exists. Both produce
`qualification: "source-layout-only"` and `metadataQualified: false`. The root
hook's executable pin does not qualify these additional object fields.

Output includes bounded `cards`, `participants`, fixed-code `rejected`, and read
budgets. Pointer addresses and external exception text never enter output.
Cards include template/instance IDs, internal template name/type, school,
accuracy, enchantment/treasure flags, dynamic effects and gray/checked candidates.
Participant IDs are decimal strings preserving uint64 precision; identity/team,
player flag, school, HP/max HP and pip categories are separate observations.
Hidden HP is null. No player/hostility relation is inferred from screen position,
team alone or NPC name. Legacy school pips remain unknown.

`phase`, `round`, `selectedCard`, per-card `selected`, and participant `relation`
are null. A checked checkbox has not yet been proved to mean selected spell;
the visible Hand is not an observed planning phase or round. Missing controls
and failed reads never become victory. Damage/resistance/modifier estimates and
pip costs are deliberately absent. An eventual narrow policy can require actual
castability and supported dynamic effect/target metadata; this reader does not
silently fill the strict planner's missing statistics with zero.

## Source comparison

Archived source is under
`ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/wizwalker/wizwalker-master/wizwalker`.
The separately installed development source is under
`.local/envs/deimos/Lib/site-packages/wizwalker`; its `direct_url.json` pins
Deimos-Wizard101/wizwalker commit
`df2283f146748d74b2229d32c08233c20ce8bb3d`, requested revision `development`.
This is local source inspection, not a claim about the running client.

| Field | Archived offset | Development offset / interpretation |
| --- | ---: | --- |
| SpellCheckBox graphical spell | 952 | 1104 |
| SpellCheckBox grayed flag | 1024 | 1208 |
| SpellCheckBox checked | absent | 1028; meaning still needs live evidence |
| CombatantDataControl participant | 1656 | 1888 |
| Spell template pointer | 120 | 120 |
| Spell template ID / school / accuracy | 128 / 136 / 132 | unchanged |
| Spell instance ID / treasure | 272 / 265 | 204 / 197 |
| Spell dynamic effects vector | 88 | 88; derived effect classes now explicit |
| SpellTemplate internal name / type | 96 / 312 | 96 / 352 |
| Participant owner / team / school | 112 / 144 / 148 | unchanged |
| Participant current / maximum / hidden HP | 236 / 240 / 244 | 244 / 248 / 252 |
| Participant pips | inline bytes 152–154 | pointer at 152 to PipCount; bytes 80–89 preserve generic, power, seven schools and shadow |

The control's name `CombatantControl` is not its class: the source requires
`CombatantDataControl`. Each dereference validates exact PropertyClass type and
the type-name stub's LEA instruction, including only the documented single JMP.
Expected graph classes are `SpellCheckBox` → `GraphicalSpell` → `SpellTemplate`
and `CombatantDataControl` → `CombatParticipant` → `PipCount` (development).
Only exact base `SpellEffect` is read; conditional/random/variable/nested variants
are rejected instead of misrepresented as a simple effect. These exact classes
must themselves be verified against current game observations.

Development source SHA-256 evidence:

- window.py: `bafb546624681f64348cf36fed5ada2f5d8ce68cee8fd645df993c7ad4e15052`
- spell.py: `aed13a85e53f79a971ab2a5b9ee01f864e884c618476b80c68fdd3ffb34cce5f`
- combat_participant.py: `dcb8e08188c11dff1351121faf65c3bf9a43639c86d396885e9b30779d7a627d`
- spell_template.py: `839b40abeece348accb92a579f44c99ec5232434a1ca622f17e5270b2b81cd65`
- spell_effect.py: `7a91a7749ca47d0eb12e43e8c508877356c442d24030b924e38b49c12be3e0d9`
- pip_count.py: `5ada15050d89c976430541a4c104212774d6605cbccdba6e0041689126341ed0`

## Bounds and verification

At most 16 cards, eight participants, eight effects per card, 128 UTF-8 bytes per
template string, 2,048 bytes per read, 65,536 total bytes and 768 reads; the scan
has a two-second cooperative deadline and individual reads at most 250 ms.
Pointers are validated, vectors aligned/size-limited, repeated pointers rejected,
source types exact, booleans strict and mutable bindings/identities/vector data
read again. No cache survives a scan. Cooperative deadlines do not replace the
owner's process timeout if an injected reader ignores cancellation.

Fifteen synthetic tests pass, including both layouts, single-JMP types, wrong
layout/type/instruction, changing bindings/vectors, hidden HP, school pips,
unsupported effect variants, grayed/disabled/hidden controls, budgets/deadlines,
short reads, sanitized failures, cancellation, malformed bindings and absence of
false victory. No recorded or live battle fixture was used. The ordinary
one-card/one-participant fixture uses under 4 KiB and under 100 reads.

Commands run, one process at a time:

- `python -m unittest discover -s tools/tests -p test_w101_combat_reader.py` — 15 passed, latest 0.415 seconds.
- `python -m black --workers 1 tools/w101_combat_reader.py tools/tests/test_w101_combat_reader.py` — completed.
- `python -m black --check --workers 1 tools/w101_combat_reader.py tools/tests/test_w101_combat_reader.py` — passed.
- `python -m compileall -q tools/w101_combat_reader.py tools/tests/test_w101_combat_reader.py` — passed.

No separate Python lint tool was invoked; formatting, syntax and focused tests
are the reported checks. Reader SHA-256 at freeze:
`a405acdadd9b983a512e853155d832e6a1126439dcd783cffe393992b2b97c74`.

Remaining: coordinator import/pin/package integration; current image/layout/type
and castability qualification; selected-card semantics; self/hostile identity;
actual phase/round; geometry/input qualification; verified whole-battle and
independent quest-count outcomes. No installed app or game was touched here.
