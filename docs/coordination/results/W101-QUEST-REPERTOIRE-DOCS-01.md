# Claim Evidence: W101-QUEST-REPERTOIRE-DOCS-01

## 1. Overview
- **Task ID**: `W101-QUEST-REPERTOIRE-DOCS-01`
- **Workstream**: `W101-QUESTS`
- **Owner**: `coordinator`
- **Status**: Draft (no status was recorded; audited 2026-11-20, see "Audit note")
- **Scope**: Formalization of the Wizard City Starter Questline GOAP action repertoire, world state ontology, zone transitions, NPC interaction geometries, and potion replenishment checkpoints.

## 2. Changes Made
1. **Created Documentation**:
   - `docs/W101_QUEST_REPERTOIRE.md`: Complete domain specification covering:
     - World state atoms (`zone`, `has_quest_*`, `completed_*`, `enrolled_*`, `potions`, `health_pct`, `mana`, `in_combat`).
     - Topological zone transitions between `"HeadmasterOffice"`, `"WizardCity_Commons"`, `"WizardCity_Ravenwood"`, `"WizardCity_GolemCourt"`, `"WizardCity_ShoppingDistrict"`.
     - Quest action specifications for *"The Lore of Magic"*, *"Enrollment"*, and *"A Good Impression"*.
     - Potion fountain checkpoints in Commons and vital recovery actions.
     - Calibrated NPC interaction bounding boxes and Fitts's law kinematic targeting.
2. **Updated Cross-References**:
   - `docs/ROUTINE_EXECUTION_MODEL.md`: Added Section 7 detailing the relationship between the GOAP engine and the concrete starter quest repertoire.
   - `docs/INDEX.md`: Added indexed entry for `docs/W101_QUEST_REPERTOIRE.md`.

## 3. Verification & Compliance
- **Text Encoding Check**: `python tools/check_text_encoding.py` $\rightarrow$ 0 violations.
- **Project Board Check**: `python tools/check_project_board.py` $\rightarrow$ Valid (30 workstreams, zero broken references).
- **Claim Integrity**: Bounded strictly to declared documentation paths.

---

## Audit note (audit 2026-11-20, Claude)
Checked against `crates/maelstrom-core/src/quest_domain.rs`, `tests/quest_domain_tests.rs`, `goap.rs` and a throwaway probe that planned over the repertoire (not committed).
- **Verified:** the quest atoms, the zone-transition costs (5, 8, 4, 6), every quest action's preconditions, effects and costs (Lore of Magic, Enrollment, A Good Impression), the potion-fountain refill, the
  cross-reference in `ROUTINE_EXECUTION_MODEL.md` section 7 and the `INDEX.md` entry.
- **The vitals section ("potion fountain checkpoints and vital recovery actions") described a model the code does not have.** The code uses two booleans (`vitals_low`, `has_potions`). The page's
  `potions` (0..=3), `health_pct`, `mana` and `in_combat` atoms do not exist, drinking a potion does not consume it, and the action is `drink_potion_recovery`.
- **"Calibrated NPC interaction bounding boxes" are placeholders.** The code calls the default rectangles "centered near character feet/overhead in 720p"; nine of thirteen NPCs have the same prompt rectangle;
  the page's third rectangle ("Quest Accept Button") is not in the code; nothing records any calibration; and nothing calls the lookup outside tests. "Fitts's law kinematic targeting" is not part of the module.
- **The page was incomplete about transitions:** the Shopping District edges and eight Olde Town edges are in the code but were not listed, and the 5 s and "longer bridge" glosses on the costs have no source
  (costs are unitless planner weights).
- **Section 7's "verification standards" are criteria, not results,** and the tests do not cover "any valid starting zone" or replanning after a disturbance; the planner has no caller outside tests.
- **The scope line ("Formalization of ... NPC interaction geometries, and potion replenishment checkpoints")** is therefore accurate only for the structure, not for the numbers. The page now carries a
  status banner and "Today" notes.
