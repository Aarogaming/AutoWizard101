# Evidence Report: W101-CORPUS-DOCS-01

## 1. Summary
Ingested the comprehensive Wizard101 Master Mechanics & Reference Corpus into `docs/W101_MASTER_REFERENCE_CORPUS.md` and updated `docs/INDEX.md`.

## 2. Ingested Reference Categories & Standards
1. **Primary Encyclopedic Databases**: Central Wiki (Cheating Bosses, Quests, Creatures, NPCs, Locations, Reagents, Housing/Gardening, Pets, Fishing, Cantrips, Silver Chests) and Fandom cross-reference.
2. **Official KingsIsle Player Guides**: Core controls, combat/dueling guide, spell mechanics (charms, wards, auras, shadow, Archmastery), pet training, gardening, crafting/bazaar pricing, and historical patch archives.
3. **Combat Mathematics & Formulas**: Final Bastion post-audit formulas (multiplicative vs additive damage, Archmastery pip priority, shadow gauge/backlash, Critical/Block probability curves), LIFO charm/ward stack resolution, DoT/HoT mechanics, and status effects.
4. **Boss Cheats & Encounter Interrupts**: Final Bastion and Swordroll boss cheat catalogs, Darkmoor/Catacombs benchmarks, and modern Wallaru/Novus solo encounter rules.
5. **NPC Threat, AI Behavior & Targeting**: Official forum threat mechanics, slot position targeting, damage vs heal threat generation, 30s planning phase timers, and dueling circle rules.
6. **World Navigation, Mounts & Subsystems**: Mount speeds (+40%/+50%/+55%), reagent spawn maps/realm-hopping, Couch Potatoes 69-plot gardening layouts, pet mega-snack optimization, and cantrip/silver chests.
7. **Engine, File Formats & Protocol Specs**: Gamebryo NIF XML formats, Katsuba KingsIsle WAD/NAV/BCD/POI serialization schemas, and WizWikiAPI scrapers.
8. **Fact-Verification Trust Hierarchy**: Level 1 (Official KI notes & Central infoboxes), Level 2 (Final Bastion/Swordroll post-audit formulas), Level 3 (Legacy pre-2020 forum threads requiring re-verification).
9. **Structured Cheat Extraction Schema**: `TriggerCondition`, `InterruptPhase`, `Effect`, `CounterConstraint`.

## 3. Verification
- `docs/INDEX.md` updated with reference to `W101_MASTER_REFERENCE_CORPUS.md`.
- `python tools/check_project_board.py` validated (30 workstreams clean).
- `python tools/check_text_encoding.py` verified (0 violations).
