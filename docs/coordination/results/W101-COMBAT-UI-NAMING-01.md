# W101-COMBAT-UI-NAMING-01 — combat UI nodes observed during a boss battle

Status: **candidate node names from one read-only pass; reader mapping gap identified. No combat decisions.**
Date: 2026-11-16 · owner: root · claim: `W101-COMBAT-UI-NAMING-01` · workstream: `W101-COMBAT`

## 1. Provenance

Owner reported "boss battle" and the queued foreground waiter fired a bounded `ui-tree-watch` automatically (no clicking, no input, no focus change by the agent). Target: handle `642779728`, PID `44468`, ticks `639304802391820609`, image `d410a246…`. Flags: combat + pose + quest context, **no resource context** (avoids the 2-tree cap). Receipt `a26b8b8ecc314bac8503ba981bb15df8`, exit 0, `Completed`, 25 trees / 100 loading samples / 0 failures, `CleanupConfirmed: true`, `HookRestored: true`, 27,453 ms. Artifacts: `.local/agent-runs/auto-boss-20261116-233030/` and `%LOCALAPPDATA%\Maelstrom\operations\a26b8b8ecc314bac8503ba981bb15df8\`.

Zone candidate: `Khrysalis/Interiors/KR_Z10_I05_DryGulchCave` (`status: candidate`, `liveQualified: false`).

## 2. The decisive finding

`combat-readiness.json` again reported:

```json
{"Ready": false, "CardCandidates": 0, "ParticipantCandidates": 0, "Missing": ["fresh-combat-observation"]}
```

But the same pass's UI trees contain **visible combat nodes** (below). So this is **not** an absence of combat — it is a **reader mapping gap**: `tools/w101_combat_reader.py` did not recognize the client's current node names, and therefore published no candidates even though the combat UI was present. This corrects the earlier reports' implication that "no combat was observed."

## 3. Combat node names actually observed (136 distinct names total)

Visible in 25/25 trees: `spriteHealthBkg`, `spriteManaBkg`.
Visible in 18/25 trees: `CombatantControl`, `Health`, `Pips`, `ShadowPips`.
Visible in 9/25 trees (a full combat phase): `SchoolPipPanel`, `Hand`, `Card1`, `Card2`, `Card3`, `Card4`, `Card5`, `Card6`, `Card7`, `SpellSelection`, `Target`, `Target_0`, `Target_1`, `Target_2`, `Target_3`.
Present but hidden: `SpellCast` (18), `ConfirmTargetsWindow` (9), `PetCard` (9), `DuelTimerWindow` (25), `btnSpellbook` (25).

These are **candidate structural names from the archived layout reader**. There are no `RawRectangle` values in this report; raw layout rectangles are not client-pixel click targets. No card identity, pip count, castability or target selection is claimed.

## 4. What changes

1. The earlier `W101-COMBAT-API-MAP-01` and `W101-UI-NAMING-01` records should be read with this caveat: `Ready:false` meant "reader found nothing," not "the owner was not in combat." The owner explicitly reported combat during those sessions too.
2. Concrete next task (not performed here): update or replace the combat-reader node-name mapping using these observed names, with deterministic fixtures built from these 25 saved trees, then re-run the same capture to see `CardCandidates > 0`. This is the "stub into building the system from what's provided" step — the game gave us the vocabulary.
3. The Dueling Diego flag idea remains separate: `DuelTimerWindow` was hidden in all 25 samples even during this boss fight, confirming hidden nodes are not activity evidence.

## 4b. Second observation — reproducible reader gap

A second owner-authorized boss battle produced an independent pass (receipt `1d26e3a4819e4cbf97750e3cad7435b5`, exit 0, `Completed`, 25 trees / 100 loading samples / 0 failures, hook restored, guard clear, no input), this time in zone candidate `Khrysalis/Interiors/KR_Z10_I05_AlluviumCave`.

- **139 distinct node names**, 26 combat-related.
- Visible combat nodes again: `CombatantControl`, `Health`, `Pips`, `ShadowPips`, `ShadowPipBackground` (21/25), `SpellCast` (21), `Target` (14), `SpellSelection`, `ConfirmTargetsWindow`, `Target_0…Target_3`, `SchoolPipPanel` (7), `Hand` (6).
- `combat-readiness.json` again: `Ready:false`, 0 card/participant candidates, `Missing:["fresh-combat-observation"]`.

Two independent boss battles in different zones reproduce the same mismatch. The reader mapping gap is therefore **repeatable**, not a one-off. Both passes' saved trees are available as fixture material for the mapping fix.

## 5. Limits and boundaries

One pass, one client build/layout; names may change with patches. No input, no dispatch, no gameplay qualification, no new fact IDs marked available. The waiter only watched the foreground handle; it did not capture, focus or control the window. No further live pass is authorized by this report.
