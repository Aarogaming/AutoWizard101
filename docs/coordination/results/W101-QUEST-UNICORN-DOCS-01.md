# Claim Evidence: W101-QUEST-UNICORN-DOCS-01

## 1. Overview
- **Task ID**: `W101-QUEST-UNICORN-DOCS-01`
- **Workstream**: `W101-QUESTS`
- **Owner**: `coordinator`
- **Status**: Draft (no status was recorded; audited 2026-11-20, see "Audit note")
- **Scope**: Documentation for the canonical Unicorn Way questline action repertoire (*"Finding Out"*, Private O'Ryan, Lost Soul and Skeletal Pirate combat encounters, *"Grim Tales"*, Private Stillson, Lady Oriel sanctuary, Ambrose report), dialogue geometries, and pre-combat health replenishment invariants.

## 2. Key Deliverables
1. **Unicorn Way Quest Domain Specification** (`docs/W101_QUEST_UNICORN_WAY.md`):
   - World state ontology extension: `zone == "WizardCity_UnicornWay"`, `talked_to_oryan`, `has_quest_defeat_lost_souls`, `lost_souls_defeated`, `has_quest_grim_tales`, `talked_to_stillson`, `has_quest_defeat_skeletal_pirates`, `skeletal_pirates_defeated`, `has_quest_lady_oriel`, `talked_to_lady_oriel`, `completed_unicorn_way`.
   - Topological zone transitions: Commons $\longleftrightarrow$ Unicorn Way gate.
   - Combat actions and safety preconditions: `engage_and_defeat_lost_soul` and `engage_and_defeat_skeletal_pirate` require $\neg \text{vitals\_low}$.
   - Pre-combat recovery sequence: Potion drink or return to Commons fountain if vitals are depleted prior to entering combat.
   - Calibrated NPC interaction target anchors for Private O'Ryan, Private Stillson, and Lady Oriel in 1280x720 base viewport.
2. **Updated Architecture Docs**:
   - `docs/W101_QUEST_REPERTOIRE.md`: Extended topological zone transition diagram and sequence summary to integrate Unicorn Way.
   - `docs/ROUTINE_EXECUTION_MODEL.md`: Updated Section 7 to link Unicorn Way domain and combat encounter integration.
   - `docs/INDEX.md`: Added `W101_QUEST_UNICORN_WAY.md` to core architecture documentation index.

## 3. Red Team / Blue Team Adversarial Audit
- **Combat Precondition Safety**:
  - *Red Team Attack*: If the macro GOAP planner selects combat without verifying vitals, an agent could charge into a mob duel ring while on low HP, risking defeat and forced defeat-penalty respawns.
  - *Blue Team Mitigation*: All hostile combat actions (`engage_and_defeat_lost_soul`, `engage_and_defeat_skeletal_pirate`) mandate `vitals_low == false` as an explicit precondition. When vitals are depleted, $A^*$ state-space search is forced to plan `drink_potion_recovery` or route back through the Commons archway to `refill_potions_at_fountain` before returning to combat.
- **Topological Ingress/Egress Consistency**:
  - *Red Team Attack*: Asymmetric zone transition costs or missing reverse edges could trap the planner in Unicorn Way or cause infinite loop oscillations.
  - *Blue Team Mitigation*: Bidirectional transition actions (`travel_commons_to_unicorn_way`, `travel_unicorn_way_to_commons`) are symmetric ($Cost = 6.0$), preserving admissible heuristics in $A^*$ search.

## 4. Verification Evidence
- `python tools/check_project_board.py`: Valid (30 workstreams, zero broken links).
- `python tools/check_text_encoding.py`: 0 violations.

---

## Audit note (audit 2026-11-20, Claude)
Checked against `crates/maelstrom-core/src/quest_domain.rs`, `goap.rs`, `tests/quest_domain_tests.rs` and a throwaway probe that planned over the domain (not committed).
- **Verified:** the state atoms, the Commons to Unicorn Way transition pair (cost 6.0 each), every Unicorn Way action's preconditions, effects and costs, the two combat preconditions
  (`vitals_low == false`), the potion-drink and fountain-trip recovery paths, and the cross-references in `ROUTINE_EXECUTION_MODEL.md` section 7 and `INDEX.md`.
- **"Calibrated NPC interaction target anchors" are placeholders.** Nothing records a calibration, Private Stillson's rectangle is a copy of the shared default, and nothing calls the lookup outside tests.
- **The cost glosses were invented** (6 as about 6 s, 12 as duel-ring entry and card draw, 15 as higher mob HP, 5 as hedge-maze navigation); costs are unitless planner weights, and a fight is one atomic action.
- **The vitals threshold ("health below 40% or depleted mana") does not exist.** `vitals_low` is a boolean that no action sets and nothing feeds; "zero risk of entering hostile combat with critical low
  health" is withdrawn: the precondition protects the plan model only.
- **Red-team "symmetric costs preserve admissible heuristics" is a non sequitur.** The heuristic in `goap.rs` is the unmet goal atoms divided by the most atoms one action fixes (rounded up), times the minimum action cost; it does not depend on
  transition-cost symmetry. The reverse edge matters because the planner cannot leave Unicorn Way without it.
- **Section 7 ("from any starting zone")** held only partly: the committed tests start in the Office; a probe found plans from all ten zones, but a low-vitals start with a potion needs 2,546 search
  iterations and the default budget of 2,000 returns `None`. The page now has the table. The "enemy encounter parameters" and "recovery checkpoints" named in the `claims.json` release note do
  not exist in the page (there are no mob parameters), and that note also says "4 unit tests" where five were added; `claims.json` is coordinator-owned and was not edited.
- **Evidence section:** the project-board and encoding checks pass today (30 workstreams, 0 violations). The page was marked as a draft with "Today" notes.
