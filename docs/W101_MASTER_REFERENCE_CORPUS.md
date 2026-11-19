# Wizard101 Master Mechanics & Reference Corpus (Agent Verification Index)

## 1. Operational Rationale & Ingestion Contracts

To guarantee that Maelstrom's autonomous models, planning engines, and decision synthesizers operate on verified domain truths rather than hallucinated or outdated mechanics, this master index establishes the **canonical knowledge corpus** for Wizard101.

### Key Fact-Verification Rules for Autonomous Agents

1. **Strict Trust Hierarchy**:
   - **Primary Authority (Level 1)**: Official KingsIsle Update Notes and Wizard101 Central Wiki structured infoboxes for hard numerical bounds (health, pip costs, school resistances, quest prerequisites, and dialogue triggers).
   - **Analytical Authority (Level 2)**: Final Bastion and Swordroll for empirical post-audit mathematical formulas (Archmastery generation rates, Critical/Block probability curves, and boss interrupt sequences).
   - **Legacy References (Level 3)**: Unofficial forum threads and pre-2020 community discussions should be treated as historical evidence and strictly re-verified against post-audit formulas.
2. **Audit Date Awareness**:
   - KingsIsle conducted major combat overhauls between 2020 and 2025 (e.g. Critical/Block rating formulas, damage caps, Archmastery dual-school pips, and early-world boss cheat adjustments).
   - Any rule or formula referencing pre-2020 mechanics must be flagged and validated against the post-audit standard.
3. **Structured Boss Cheat Extraction Schema**:
   Every boss cheat ingested from community or empirical sources must be normalized into four formal fields:
   - **`TriggerCondition`**: Round number, spell school/type cast, health percentage threshold, or player battle circle count.
   - **`InterruptPhase`**: `StartOfRound`, `ImmediateReaction`, `EndOfRound`, or `PassiveAura`.
   - **`Effect`**: Interrupt spell cast, aura applied, blade/trap stripped, or card discarded.
   - **`CounterConstraint`**: Action restrictions imposed on the combat evaluator (`ForbidFeint`, `ForbidBlades`, `RequireDamageOverTime`, `CapDamagePerHit(u32)`).

---

## 2. Primary Encyclopedic & Structured Databases

- **Wizard101 Central Wiki (Main Canonical Portal)**: [Central Wiki Main Page](https://wiki.wizard101central.com/wiki/Wizard101_Wiki)  
  *Canonical community database for NPCs, Quests, Spells, Cheats, Drops, and Zones.*
- **Wizard101 Central — Category: Cheating Bosses**: [Cheating Bosses Index](https://wiki.wizard101central.com/wiki/Category:Cheating_Bosses)  
  *Master index of all bosses with interrupt and scripted triggers across all worlds.*
- **Wizard101 Central — Category: Quests**: [Quests Index](https://wiki.wizard101central.com/wiki/Category:Quests)  
  *Full quest progression chains, prerequisites, dialogue pages, and turn-in NPC anchors.*
- **Wizard101 Central — Category: Creatures / Enemies**: [Creatures Index](https://wiki.wizard101central.com/wiki/Category:Creatures)  
  *Health values, school affinities, starting pips, boost/resist percentages, and deck compositions.*
- **Wizard101 Central — Category: NPCs**: [NPCs Index](https://wiki.wizard101central.com/wiki/Category:NPCs)  
  *Static and dynamic world anchors, vendors, trainers, and quest givers.*
- **Wizard101 Central — Category: Locations**: [Locations Hierarchy](https://wiki.wizard101central.com/wiki/Category:Locations)  
  *World $\rightarrow$ Zone $\rightarrow$ Sub-Area hierarchy, door portals, and world gates.*
- **Wizard101 Central — Category: Reagents**: [Reagents Index](https://wiki.wizard101central.com/wiki/Category:Reagents)  
  *Ground spawn zones, transmute recipes, and vendor purchase costs.*
- **Wizard101 Central — Category: Housing / Gardening**: [Gardening Index](https://wiki.wizard101central.com/wiki/Category:Gardening)  
  *Seed types, plant needs, pests, soil types, and like/dislike anchors.*
- **Wizard101 Central — Category: Pets & Pet Snacks**: [Pets Index](https://wiki.wizard101central.com/wiki/Category:Pets)  
  *Base stats, talent pools, pet derby, training mini-games, and energy costs.*
- **Wizard101 Central — Category: Fishing**: [Fishing Index](https://wiki.wizard101central.com/wiki/Category:Fishing)  
  *Fish schools, rank requirements, lures, sentinels, and zone water pools.*
- **Wizard101 Central — Category: Cantrips**: [Cantrips Index](https://wiki.wizard101central.com/wiki/Category:Cantrips)  
  *Ritual and field cantrip spells, energy consumption, and cantrip chest requirements.*
- **Wizard101 Central — Category: Silver Chests**: [Silver Chests Index](https://wiki.wizard101central.com/wiki/Category:Silver_Chests)  
  *Shock-A-Lock / puzzle chest mechanics and spawn locations.*
- **Fandom Wizard101 Wiki**: [Fandom Wiki Portal](https://wizard101.fandom.com/wiki/Wizard101_Wiki)  
  *Secondary cross-reference database for lore and mechanic summaries.*

---

## 3. Official KingsIsle Documentation & System Guides

- **Official Wizard101 Player's Guide**: [Player's Guide](https://www.wizard101.com/game/players-guide)  
  *Core controls, UI layout, compass navigation, Quest Helper vector, and hotkeys.*
- **Official Combat & Dueling Guide**: [Combat Guide](https://www.wizard101.com/game/combat-guide)  
  *Dueling circle pedestals, planning phase, turn order, pip generation, and flee rules.*
- **Official Spell Mechanics Guide**: [Spell Mechanics](https://www.wizard101.com/game/spells)  
  *Charms, wards, auras, global bubbles, shadow gauge, and Archmastery/school pips.*
- **Official Pet Training & Pavilion Guide**: [Pet Pavilion Guide](https://www.wizard101.com/game/pets)  
  *Mini-games, snack feeding, happiness metrics, energy scaling, and hatching.*
- **Official Gardening Guide**: [Gardening Guide](https://www.wizard101.com/game/gardening)  
  *Plant growth cycles, needs satisfaction, pest mitigation, and elder harvest.*
- **Official Crafting & Bazaar Guide**: [Crafting & Bazaar](https://www.wizard101.com/game/crafting)  
  *Crafting station slots, cooldown timers, and Bazaar supply/demand dynamic pricing.*
- **Official Fishing Guide**: [Fishing Guide](https://www.wizard101.com/game/fishing)  
  *Energy costs, lure schools, bobber bites, and striking mechanics.*
- **Official Cantrips Guide**: [Cantrips System](https://www.wizard101.com/game/cantrips)  
  *Field cantrip requirements, team ritual coordination, and chest unlocking.*
- **Official Update Notes & Patch Archive**: [KingsIsle Update Notes](https://www.wizard101.com/game/updates)  
  *Chronological record of game client updates, server-side balance patches, and anti-cheat revisions.*

---

## 4. Combat Mathematics, Formulas & Resolution Stacks

- **Final Bastion — Damage Calculation & Modifier Stacking Guide**: [Damage Calculation Guide](https://finalbastion.com/wizard101-guides/w101-spell-guides/wizard101-damage-calculation/)  
  *Multiplicative vs. additive damage stacking, armor piercing, flat damage ratings, and stat caps.*
- **Final Bastion — Archmastery & School Pips Complete Guide**: [Archmastery Guide](https://finalbastion.com/wizard101-guides/w101-spell-guides/wizard101-archmastery-guide/)  
  *Archmastery ratings, school pip priority thresholds, and dual-school pip conversion.*
- **Final Bastion — Shadow Pips & Shadow-Enhanced Spells**: [Shadow Magic Guide](https://finalbastion.com/wizard101-guides/w101-spell-guides/wizard101-shadow-magic-guide/)  
  *Shadow pips, shadow gauge filling rates, backlash penalties, and spell tiers.*
- **Final Bastion — Critical & Block System Guide**: [Critical & Block Guide](https://finalbastion.com/wizard101-guides/w101-spell-guides/wizard101-critical-block-guide/)  
  *Post-audit rating equations, diminishing return curves, and critical damage multipliers.*
- **Wizard101 Central — Charms & Wards Resolution Order**: [Charms & Wards LIFO Order](https://wiki.wizard101central.com/wiki/Basic:Charms_and_Wards)  
  *Last-In First-Out (LIFO) stack resolution for blades, traps, shields, and prisms.*
- **Wizard101 Central — Damage Over Time (DoT) & HoT Mechanics**: [DoT & HoT Guide](https://wiki.wizard101central.com/wiki/Basic:Damage_Over_Time)  
  *Tick timing, initial hit interactions, and trap consumption rules.*
- **Wizard101 Central — Auras & Global Spells**: [Auras & Globals](https://wiki.wizard101central.com/wiki/Basic:Auras)  
  *Aura duration counters, overwrite conflicts, and school damage modifiers.*
- **Wizard101 Central — Power Pips & Conversion**: [Pips Guide](https://wiki.wizard101central.com/wiki/Basic:Pips)  
  *Power pip percentage, pip conversion ratings, and mastery amulet overrides.*
- **Wizard101 Central — Status & Control Effects**: [Status Effects](https://wiki.wizard101central.com/wiki/Basic:Status_Effects)  
  *Stun blocks, pacify, taunt, beguile, and disorient mechanics.*

---

## 5. Boss Cheats & Encounter Interrupt Catalogs

- **Final Bastion — Master Boss Cheat Guides Index**: [Master Boss Cheat Index](https://finalbastion.com/wizard101-guides/w101-boss-guides/)  
  *World-by-world trigger conditions, exact dialogue cue strings, and counter-strategies.*
- **Swordroll — Cheating Boss Guides & Walkthroughs**: [Swordroll Guides](https://www.swordroll.com/p/wizard101-guides.html)  
  *Interrupt trigger phases, deck configurations, and encounter execution checklists.*
- **Wizard101 Central — Bosses with Cheats**: [Boss Cheats Table](https://wiki.wizard101central.com/wiki/Basic:Boss_Cheats)  
  *Structured wiki matrix of all interrupt triggers and scripted boss responses.*
- **Final Bastion — Castle Darkmoor Guide**: [Darkmoor Cheat Guide](https://finalbastion.com/wizard101-guides/w101-dungeons-bosses/castle-darkmoor-guide/)  
  *Multi-tier dungeon benchmark (Sir Blackwater, Shane von Shane, Malistaire the Undying).*
- **Final Bastion — Catacombs Guides**: [Catacombs Cheat Guides](https://finalbastion.com/wizard101-guides/w101-dungeons-bosses/the-catacombs-guide/)  
  *King Detritus, Sir Greggor, and Devourer mechanics.*
- **Final Bastion — Wallaru & Novus Boss Cheat Guides**: [End-Game Boss Guides](https://finalbastion.com/wizard101-guides/w101-dungeons-bosses/)  
  *Modern end-game cheat phases and solo boss constraints.*

---

## 6. NPC Threat, AI Behavior, Targeting & Dueling Circles

- **Official Forums — Threat Management & Aggro Mechanics**: [Threat & Aggro Thread](https://www.wizard101.com/forum/halstons-laboratory/threat-management-or-agro-4613)  
  *Pedestal position priority, damage vs. healing threat generation, and Taunt/Calm mitigation.*
- **Official Forums — Understanding Enemy Attack Priority**: [Attack Priority Thread](https://www.wizard101.com/forum/ravenwood-commons/understanding-attack-priority-54134)  
  *Target selection order and high-damage threat attraction.*
- **Official Forums — Monster Target Selection & Late Joiners**: [Target Selection Thread](https://www.wizard101.com/forum/ravenwood-commons/monster-target-selection-8ad6a42b60bb4c7f0160fd7298c761d2)  
  *Aggro assignment when allies join battle circles mid-duel.*
- **Official Forums — Combat Planning Phase Timer**: [Turn Timer Thread](https://www.wizard101.com/forum/halstons-laboratory/game-timer-8ad6a4245b42afbe015b444388f51970)  
  *30-second decision window and round execution cutoffs.*
- **Wizard101 Central — Dueling Circle Rules**: [Dueling Circle Mechanics](https://wiki.wizard101central.com/wiki/Basic:Dueling)  
  *Coin flip first/second turn precedence and 4v4 slot architecture.*
- **Wizard101 Central — Health & Mana Wisps**: [Wisps & Recovery](https://wiki.wizard101central.com/wiki/Basic:Wisps_and_Potions)  
  *Post-battle replenishments and flee spawn points.*

---

## 7. World Navigation, Mounts, Reagents & Subsystems

- **Final Bastion — Mount Speed & Utility Guide**: [Mounts Guide](https://finalbastion.com/wizard101-guides/w101-gear-guides/wizard101-mounts-guide/)  
  *Mount speeds (+40%, +50%, +55%), multi-passenger mounts, and collision geometry.*
- **Final Bastion — Reagent Farming & Realm-Hopping Guide**: [Reagent Farming Guide](https://finalbastion.com/wizard101-guides/w101-crafting-guides/wizard101-reagent-farming-guide/)  
  *Harvest route maps, spawn coordinates, and realm switching cooldowns.*
- **Final Bastion — Couch Potatoes & EMP Gardening Master Guide**: [Gardening Master Guide](https://finalbastion.com/wizard101-guides/w101-gardening-guides/couch-potatoes-gardening-guide/)  
  *69-plot medium/large spell plots, stacking layouts, and elder drop schedules.*
- **Final Bastion — Pet Training & Mega-Snack Guide**: [Pet Training Guide](https://finalbastion.com/wizard101-guides/w101-pet-guides/wizard101-pet-training-guide/)  
  *Optimal mini-game routing and experience-per-energy ratios.*
- **Final Bastion — Cantrip Chests & Spellwrighting Guide**: [Cantrips & Chests Guide](https://finalbastion.com/wizard101-guides/w101-spell-guides/wizard101-cantrips-guide/)  
  *Multi-wizard cantrip chest coordination and location tables.*
- **Final Bastion — Silver Chests & Mini-Game Guide**: [Silver Chests Guide](https://finalbastion.com/wizard101-guides/w101-minigame-guides/)  
  *Shock-A-Lock puzzle solving and catch timers.*
- **Final Bastion — Monstrology Extraction Guide**: [Monstrology Guide](https://finalbastion.com/wizard101-guides/w101-monstrology-guides/)  
  *Animus extraction rules and house guest summon cards.*

---

## 8. Engine, File Formats & Protocol Specifications

- **Wizard101 Central — Client Patch History**: [Patch Notes Category](https://wiki.wizard101central.com/wiki/Category:Patch_Notes)  
  *Historical client release versions, graphical updates, and UI changes.*
- **NifTools / Gamebryo NIF Format Specification**: [NifXML Spec](https://github.com/niftools/nifxml)  
  *Underlying Gamebryo 3D scene graphs, node hierarchies, and collision structures.*
- **Katsuba Source & Documentation**: [Katsuba Repository](https://github.com/vbe0201/katsuba)  
  *High-performance schemas for KingsIsle WAD, NAV, BCD, POI, and serialized types.*
- **WizWikiAPI**: [WizWikiAPI Repository](https://github.com/wizspoil/WizWikiAPI)  
  *Structured scrapers and programmatic schema wrappers for Central Wiki tables.*
