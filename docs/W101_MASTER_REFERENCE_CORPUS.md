# Wizard101 Master Mechanics & Reference Corpus (Agent Verification Index)

> **Status: DRAFT, unverified link index (audit 2026-11-20, Claude).** Nothing on this page was ingested. The 54 links below were produced by an AI chat session (Gemini, pasted 2026-11-19)
> and the pages behind them were not read or extracted; the italic description under each link is that session's guess at what the page covers, not a quotation. This is not a "canonical
> knowledge corpus", and the trust hierarchy in section 1 is a proposal that no code enforces.
>
> Each link now carries a status tag from the audit (method, tallies and the real locations found are in section 9):
> **[live]** answered HTTP 200; **[archived]** not reachable directly from the audit machine, but the Internet Archive holds a 200 capture; **[unknown]** no evidence either way;
> **[stale]** archived earlier, but the live path now ends at a 404 page; **[wrong]** the path does not exist; **[replaced]** a wrong path swapped for a real page whose title matches
> (the original path is quoted). Replacement pages are matched by title only; their contents were not compared with the descriptions.

## 1. Operational Rationale & Ingestion Contracts

To guarantee that Maelstrom's autonomous models, planning engines, and decision synthesizers operate on verified domain truths rather than hallucinated or outdated mechanics, this master index establishes the **canonical knowledge corpus** for Wizard101.

### Key Fact-Verification Rules for Autonomous Agents

1. **Strict Trust Hierarchy**:
   - **Primary Authority (Level 1)**: Official KingsIsle Update Notes and Wizard101 Central Wiki structured infoboxes for hard numerical bounds (health, pip costs, school resistances, quest prerequisites, and dialogue triggers).
   - **Analytical Authority (Level 2)**: Final Bastion and Swordroll for empirical post-audit mathematical formulas (Archmastery generation rates, Critical/Block probability curves, and boss interrupt sequences).
   - **Legacy References (Level 3)**: Unofficial forum threads and pre-2020 community discussions should be treated as historical evidence and strictly re-verified against post-audit formulas.
   - **Today (audit 2026-11-20):** a proposal, not a decision, and no code reads it. The registry note for this task describes a different four-tier hierarchy
     (client disassembly > local packet trace > community wiki > YouTube) that is not on this page. Which sources outrank which is an owner decision.
2. **Audit Date Awareness**:
   - KingsIsle conducted major combat overhauls between 2020 and 2025 (e.g. Critical/Block rating formulas, damage caps, Archmastery dual-school pips, and early-world boss cheat adjustments).
   - Any rule or formula referencing pre-2020 mechanics must be flagged and validated against the post-audit standard.
   - **Today (audit 2026-11-20):** the source paste said only that critical/block, damage curves and several early boss cheats changed between 2020 and 2025. The examples
     "damage caps" and "Archmastery dual-school pips" were added when this page was written and are unsourced. The only dated page found is a Final Bastion analysis titled for the
     spring 2021 resist-and-pierce change.
3. **Structured Boss Cheat Extraction Schema**:
   Every boss cheat ingested from community or empirical sources must be normalized into four formal fields:
   - **`TriggerCondition`**: Round number, spell school/type cast, health percentage threshold, or player battle circle count.
   - **`InterruptPhase`**: `StartOfRound`, `ImmediateReaction`, `EndOfRound`, or `PassiveAura`.
   - **`Effect`**: Interrupt spell cast, aura applied, blade/trap stripped, or card discarded.
   - **`CounterConstraint`**: Action restrictions imposed on the combat evaluator (`ForbidFeint`, `ForbidBlades`, `RequireDamageOverTime`, `CapDamagePerHit(u32)`).
   - **Today (audit 2026-11-20):** the Rust schema is `BossCheatRule` in `crates/maelstrom-core/src/combat_turn_evaluator.rs`. Its four fields match the names above, but the examples do not:
     `CounterConstraint` is `ForbidTraps`, `ForbidBlades`, `RequireDamageOverTime`, `CapDamagePerHit(f64)` and `ForbidHealing` (there is no `ForbidFeint`, and the cap is an `f64`);
     `CheatTriggerCondition` is `RoundNumber { modulo, offset }`, `SpellCastType`, `HealthThreshold { ratio }`, `PlayerCount { count }` or `LateJoiner`; `CheatInterruptPhase` has the four phases listed;
     `CheatEffect` is `CastSpell`, `ApplyAura`, `RemoveCharms`, `RemoveWards`, `ForceDiscardHand` or `InterruptStun`. Only `counter_constraint` is read by the evaluator; trigger, phase and effect are
     descriptive data that nothing evaluates (see [W101-CHEAT-SCHEMA-CORE-02](coordination/results/W101-CHEAT-SCHEMA-CORE-02.md)). No cheat data from the sources below has been ingested.

---

## 2. Primary Encyclopedic & Structured Databases

- **Wizard101 Central Wiki (Main Canonical Portal)**: [Central Wiki Main Page](https://wiki.wizard101central.com/wiki/Wizard101_Wiki) **[archived]**  
  *Canonical community database for NPCs, Quests, Spells, Cheats, Drops, and Zones.*
- **Wizard101 Central — Category: Cheating Bosses**: [Cheating Bosses Index](https://wiki.wizard101central.com/wiki/Category:Cheating_Bosses) **[unknown]**  
  *Master index of all bosses with interrupt and scripted triggers across all worlds.*
- **Wizard101 Central — Category: Quests**: [Quests Index](https://wiki.wizard101central.com/wiki/Category:Quests) **[archived]**  
  *Full quest progression chains, prerequisites, dialogue pages, and turn-in NPC anchors.*
- **Wizard101 Central — Category: Creatures / Enemies**: [Creatures Index](https://wiki.wizard101central.com/wiki/Category:Creatures) **[archived]**  
  *Health values, school affinities, starting pips, boost/resist percentages, and deck compositions.*
- **Wizard101 Central — Category: NPCs**: [NPCs Index](https://wiki.wizard101central.com/wiki/Category:NPCs) **[unknown]**  
  *Static and dynamic world anchors, vendors, trainers, and quest givers.*
- **Wizard101 Central — Category: Locations**: [Locations Hierarchy](https://wiki.wizard101central.com/wiki/Category:Locations) **[archived]**  
  *World $\rightarrow$ Zone $\rightarrow$ Sub-Area hierarchy, door portals, and world gates.*
- **Wizard101 Central — Category: Reagents**: [Reagents Index](https://wiki.wizard101central.com/wiki/Category:Reagents) **[archived]**  
  *Ground spawn zones, transmute recipes, and vendor purchase costs.*
- **Wizard101 Central — Category: Housing / Gardening**: [Gardening Index](https://wiki.wizard101central.com/wiki/Category:Gardening) **[archived]**  
  *Seed types, plant needs, pests, soil types, and like/dislike anchors.*
- **Wizard101 Central — Category: Pets & Pet Snacks**: [Pets Index](https://wiki.wizard101central.com/wiki/Category:Pets) **[archived]**  
  *Base stats, talent pools, pet derby, training mini-games, and energy costs.*
- **Wizard101 Central — Category: Fishing**: [Fishing Index](https://wiki.wizard101central.com/wiki/Category:Fishing) **[archived]**  
  *Fish schools, rank requirements, lures, sentinels, and zone water pools.*
- **Wizard101 Central — Category: Cantrips**: [Cantrips Index](https://wiki.wizard101central.com/wiki/Category:Cantrips) **[unknown]**  
  *Ritual and field cantrip spells, energy consumption, and cantrip chest requirements.*
- **Wizard101 Central — Category: Silver Chests**: [Silver Chests Index](https://wiki.wizard101central.com/wiki/Category:Silver_Chests) **[unknown]**  
  *Shock-A-Lock / puzzle chest mechanics and spawn locations.*
- **Fandom Wizard101 Wiki**: [Fandom Wiki Portal](https://wizard101.fandom.com/wiki/Wizard101_Wiki) **[live 2026-11-19]**  
  *Secondary cross-reference database for lore and mechanic summaries.*

---

## 3. Official KingsIsle Documentation & System Guides

- **Official Wizard101 Player's Guide**: [Player's Guide](https://www.wizard101.com/w101playersguide/how-to-play) **[replaced]** (was `/game/players-guide`; page title "How to Play Wizard101")  
  *Core controls, UI layout, compass navigation, Quest Helper vector, and hotkeys.*
- **Official Combat & Dueling Guide**: [Combat Guide](https://www.wizard101.com/game/combat-guide) **[wrong]** (the path redirects to a 404 page; no real equivalent found)  
  *Dueling circle pedestals, planning phase, turn order, pip generation, and flee rules.*
- **Official Spell Mechanics Guide**: [Spell Mechanics](https://www.wizard101.com/w101playersguide/spell-cards) **[replaced]** (was `/game/spells`; page title "Spell Cards")  
  *Charms, wards, auras, global bubbles, shadow gauge, and Archmastery/school pips.*
- **Official Pet Training & Pavilion Guide**: [Pet Pavilion Guide](https://www.wizard101.com/w101playersguide/pet-games1) **[replaced]** (was `/game/pets`; page title "Game Pets")  
  *Mini-games, snack feeding, happiness metrics, energy scaling, and hatching.*
- **Official Gardening Guide**: [Gardening Guide](https://www.wizard101.com/w101playersguide/gardening) **[replaced]** (was `/game/gardening`; page title "Game Gardening")  
  *Plant growth cycles, needs satisfaction, pest mitigation, and elder harvest.*
- **Official Crafting & Bazaar Guide**: [Crafting & Bazaar](https://www.wizard101.com/w101playersguide/crafting) **[replaced]** (was `/game/crafting`; page title "Wizard101 Crafting")  
  *Crafting station slots, cooldown timers, and Bazaar supply/demand dynamic pricing.*
- **Official Fishing Guide**: [Fishing Guide](https://www.wizard101.com/w101playersguide/fishing) **[replaced]** (was `/game/fishing`; page title "Wizard101 Player Guide | Fishing")  
  *Energy costs, lure schools, bobber bites, and striking mechanics.*
- **Official Cantrips Guide**: [Cantrips System](https://www.wizard101.com/w101playersguide/cantrips) **[replaced]** (was `/game/cantrips`; page title "Cantrips Introduction")  
  *Field cantrip requirements, team ritual coordination, and chest unlocking.*
- **Official Update Notes & Patch Archive**: [KingsIsle Update Notes](https://www.wizard101.com/game/updates) **[stale]** (archived earlier; today the path ends at a 404 page)  
  *Chronological record of game client updates, server-side balance patches, and anti-cheat revisions.*

---

## 4. Combat Mathematics, Formulas & Resolution Stacks

- **Final Bastion — Damage Calculation & Modifier Stacking Guide**: [Damage Calculation Guide](https://finalbastion.com/wizard101-guides/w101-spell-guides/wizard101-damage-calculation/) **[wrong]**  
  *Multiplicative vs. additive damage stacking, armor piercing, flat damage ratings, and stat caps.*
- **Final Bastion — Archmastery & School Pips Complete Guide**: [Archmastery Guide](https://finalbastion.com/wizard101-guides/w101-spell-guides/wizard101-archmastery-guide/) **[wrong]**  
  *Archmastery ratings, school pip priority thresholds, and dual-school pip conversion.*
- **Final Bastion — Shadow Pips & Shadow-Enhanced Spells**: [Shadow Magic Guide](https://finalbastion.com/wizard101-guides/w101-spell-guides/wizard101-shadow-magic-guide/) **[wrong]**  
  *Shadow pips, shadow gauge filling rates, backlash penalties, and spell tiers.*
- **Final Bastion — Critical & Block System Guide**: [Critical & Block Guide](https://finalbastion.com/wizard101-guides/w101-spell-guides/wizard101-critical-block-guide/) **[wrong]**  
  *Post-audit rating equations, diminishing return curves, and critical damage multipliers.*
- **Wizard101 Central — Charms & Wards Resolution Order**: [Charms & Wards LIFO Order](https://wiki.wizard101central.com/wiki/Basic:Charms_and_Wards) **[wrong]**  
  *Last-In First-Out (LIFO) stack resolution for blades, traps, shields, and prisms.*
- **Wizard101 Central — Damage Over Time (DoT) & HoT Mechanics**: [DoT & HoT Guide](https://wiki.wizard101central.com/wiki/Basic:Damage_Over_Time) **[wrong]**  
  *Tick timing, initial hit interactions, and trap consumption rules.*
- **Wizard101 Central — Auras & Global Spells**: [Auras & Globals](https://wiki.wizard101central.com/wiki/Basic:Auras) **[wrong]**  
  *Aura duration counters, overwrite conflicts, and school damage modifiers.*
- **Wizard101 Central — Power Pips & Conversion**: [Pips Guide](https://wiki.wizard101central.com/wiki/Basic:Pips) **[wrong]**  
  *Power pip percentage, pip conversion ratings, and mastery amulet overrides.*
- **Wizard101 Central — Status & Control Effects**: [Status Effects](https://wiki.wizard101central.com/wiki/Basic:Status_Effects) **[wrong]**  
  *Stun blocks, pacify, taunt, beguile, and disorient mechanics.*

---

## 5. Boss Cheats & Encounter Interrupt Catalogs

- **Final Bastion — Master Boss Cheat Guides Index**: [Master Boss Cheat Index](https://finalbastion.com/wizard101-guides/w101-boss-guides/) **[wrong]**  
  *World-by-world trigger conditions, exact dialogue cue strings, and counter-strategies.*
- **Swordroll — Cheating Boss Guides & Walkthroughs**: [Swordroll Guides](https://www.swordroll.com/p/wizard101-guides.html) **[live]**  
  *Interrupt trigger phases, deck configurations, and encounter execution checklists.*
- **Wizard101 Central — Bosses with Cheats**: [Boss Cheats Table](https://wiki.wizard101central.com/wiki/Basic:Boss_Cheats) **[wrong]**  
  *Structured wiki matrix of all interrupt triggers and scripted boss responses.*
- **Final Bastion — Castle Darkmoor Guide**: [Darkmoor Cheat Guide](https://finalbastion.com/wizard101-guides/w101-dungeons-bosses/castle-darkmoor-guide/) **[wrong]**  
  *Multi-tier dungeon benchmark (Sir Blackwater, Shane von Shane, Malistaire the Undying).*
- **Final Bastion — Catacombs Guides**: [Catacombs Cheat Guides](https://finalbastion.com/wizard101-guides/w101-dungeons-bosses/the-catacombs-guide/) **[wrong]**  
  *King Detritus, Sir Greggor, and Devourer mechanics.*
- **Final Bastion — Wallaru & Novus Boss Cheat Guides**: [End-Game Boss Guides](https://finalbastion.com/wizard101-guides/w101-dungeons-bosses/) **[wrong]**  
  *Modern end-game cheat phases and solo boss constraints.*

---

## 6. NPC Threat, AI Behavior, Targeting & Dueling Circles

- **Official Forums — Threat Management & Aggro Mechanics**: [Threat & Aggro Thread](https://www.wizard101.com/forum/halstons-laboratory/threat-management-or-agro-4613) **[unknown]**  
  *Pedestal position priority, damage vs. healing threat generation, and Taunt/Calm mitigation.*
- **Official Forums — Understanding Enemy Attack Priority**: [Attack Priority Thread](https://www.wizard101.com/forum/ravenwood-commons/understanding-attack-priority-54134) **[unknown]**  
  *Target selection order and high-damage threat attraction.*
- **Official Forums — Monster Target Selection & Late Joiners**: [Target Selection Thread](https://www.wizard101.com/forum/ravenwood-commons/monster-target-selection-8ad6a42b60bb4c7f0160fd7298c761d2) **[unknown]**  
  *Aggro assignment when allies join battle circles mid-duel.*
- **Official Forums — Combat Planning Phase Timer**: [Turn Timer Thread](https://www.wizard101.com/forum/halstons-laboratory/game-timer-8ad6a4245b42afbe015b444388f51970) **[unknown]**  
  *30-second decision window and round execution cutoffs.*
- **Wizard101 Central — Dueling Circle Rules**: [Dueling Circle Mechanics](https://wiki.wizard101central.com/wiki/Basic:Duels) **[replaced]** (was `Basic:Dueling`; the page is `Basic:Duels`, archived 2025-10-10)  
  *Coin flip first/second turn precedence and 4v4 slot architecture.*
- **Wizard101 Central — Health & Mana Wisps**: [Wisps & Recovery](https://wiki.wizard101central.com/wiki/Basic:Wisps_and_Potions) **[wrong]**  
  *Post-battle replenishments and flee spawn points.*

---

## 7. World Navigation, Mounts, Reagents & Subsystems

- **Final Bastion — Mount Speed & Utility Guide**: [Mounts Guide](https://finalbastion.com/wizard101-guides/w101-gear-guides/wizard101-mounts-guide/) **[wrong]**  
  *Mount speeds (+40%, +50%, +55%), multi-passenger mounts, and collision geometry.*
- **Final Bastion — Reagent Farming & Realm-Hopping Guide**: [Reagent Farming Guide](https://finalbastion.com/wizard101-guides/w101-crafting-guides/wizard101-reagent-farming-guide/) **[wrong]**  
  *Harvest route maps, spawn coordinates, and realm switching cooldowns.*
- **Final Bastion — Couch Potatoes & EMP Gardening Master Guide**: [Gardening Master Guide](https://finalbastion.com/wizard101-guides/w101-gardening-guides/couch-potatoes-gardening-guide/) **[wrong]**  
  *69-plot medium/large spell plots, stacking layouts, and elder drop schedules.*
- **Final Bastion — Pet Training & Mega-Snack Guide**: [Pet Training Guide](https://finalbastion.com/wizard101-guides/w101-pet-guides/wizard101-pet-training-guide/) **[wrong]**  
  *Optimal mini-game routing and experience-per-energy ratios.*
- **Final Bastion — Cantrip Chests & Spellwrighting Guide**: [Cantrips & Chests Guide](https://finalbastion.com/wizard101-guides/w101-spell-guides/wizard101-cantrips-guide/) **[wrong]**  
  *Multi-wizard cantrip chest coordination and location tables.*
- **Final Bastion — Silver Chests & Mini-Game Guide**: [Silver Chests Guide](https://finalbastion.com/wizard101-guides/w101-crafting-guides/how-to-play-shock-a-lock-silver-chest-game/) **[replaced]** (was `/w101-minigame-guides/`; archived 200 capture of the Shock-A-Lock silver chest page)  
  *Shock-A-Lock puzzle solving and catch timers.*
- **Final Bastion — Monstrology Extraction Guide**: [Monstrology Guide](https://finalbastion.com/wizard101-guides/w101-monstrology-guides/) **[wrong]**  
  *Animus extraction rules and house guest summon cards.*

---

## 8. Engine, File Formats & Protocol Specifications

- **Wizard101 Central — Client Patch History**: [Patch Notes Category](https://wiki.wizard101central.com/wiki/Category:Patch_Notes) **[unknown]**  
  *Historical client release versions, graphical updates, and UI changes.*
- **NifTools / Gamebryo NIF Format Specification**: [NifXML Spec](https://github.com/niftools/nifxml) **[live]**  
  *Underlying Gamebryo 3D scene graphs, node hierarchies, and collision structures.*
- **Katsuba Source & Documentation**: [Katsuba Repository](https://github.com/vbe0201/katsuba) **[live]**  
  *High-performance schemas for KingsIsle WAD, NAV, BCD, POI, and serialized types.*
- **WizWikiAPI**: [WizWikiAPI Repository](https://github.com/R-unic/WizWikiAPI) **[replaced]** (was `wizspoil/WizWikiAPI`, a 404; the repository is `R-unic/WizWikiAPI`)  
  *Structured scrapers and programmatic schema wrappers for Central Wiki tables.*

---

## 9. Audit note (audit 2026-11-20, Claude)
Checked against the web with read-only requests (default client, no login; only the `<title>` of the seven replacement guide pages was read), the Internet Archive's availability API and CDX listings,
GitHub's public repository descriptions, the `BossCheatRule` types in the repo, and the git-ignored 2026-11-19 verification notes in `private/`.

- **Provenance.** Every link and description came from an AI chat session and was pasted on 2026-11-19 under a header that marked it UNVERIFIED. The committed page dropped that header, and the repo holds no
  content extracted from any of these sources.
- **Method and limits.** Wizard101 Central refuses connections from the audit machine and Final Bastion shows a bot check, so those two sites were judged from the archive only; "wrong" for them means
  the path is absent from the archive's listing of the site's real pages (strong evidence, not proof). The four forum threads answer 403 to scripted requests and the archive has no capture of them,
  so they stay unknown. An unusual client name also drew a 403 from wizard101.com, so the checks quoted here used the default client; Fandom's 200 is from 2026-11-19 and was not reconfirmed.
- **Tally of the 54 links before replacements:** 4 live (Swordroll, `nifxml` and `katsuba` on 2026-11-20; Fandom on 2026-11-19), 8 archived (the Central main page and 7 category pages), 9 unknown
  (5 Central categories, 4 forum threads), 1 stale (`/game/updates`) and 32 wrong: 9 by direct request (the 8 `/game/*` guide paths redirect to a 404 page, and `wizspoil/WizWikiAPI` is a 404) and 23 by the archive
  listing (15 Final Bastion paths, 8 Central `Basic:` pages). 10 of the 32 now point at real pages (tagged **[replaced]**); 22 are still wrong. The 2026-11-19 note counted 34 wrong among 55 links in the paste;
  recounting its own list gives 33, and the committed page has 54 links because one Final Bastion event link was dropped.
- **Real locations found.** The official guides live under `https://www.wizard101.com/w101playersguide/` (`how-to-play`, `spell-cards`, `pet-games1`, `gardening`, `crafting`, `fishing` and `cantrips` answer 200;
  the combat guide has no equivalent). Central has `Basic:Duels` but the archive holds no capture of `Basic:Charms_and_Wards`, `Damage_Over_Time`, `Auras`, `Pips`, `Status_Effects`, `Boss_Cheats` or
  `Wisps_and_Potions`. Final Bastion's real categories include `w101-dungeons-boss-guides`, `w101-crafting-guides` and `w101-analysis`; the nearest real pages to the wrong damage and shadow rows are
  `w101-analysis/damage-resist-and-pierce-spring-2021/` and `w101-analysis/explaining-the-shadow-spell-nerfs/` (archived 200; narrower than the descriptions), and the wrong boss rows should be looked up under
  `w101-dungeons-boss-guides/`. `niftools/nifxml` (the nif.xml format description), `vbe0201/katsuba` (tools for KingsIsle file formats) and `R-unic/WizWikiAPI` (an API over the public wiki) match their descriptions
  at the level of the repository summary; the specific format lists in the descriptions (WAD, NAV, BCD, POI) were not checked.
- **Unsourced specifics.** No checked page backs these figures and rules in the descriptions: "Mount speeds (+40%, +50%, +55%)", "69-plot" gardening layouts, the "30-second decision window",
  "coin flip first/second turn precedence", "4v4 slot architecture", and "Last-In First-Out (LIFO) stack resolution" for charms and wards (the Rust damage pipeline multiplies the charm and ward factors,
  so list order has no effect; see [CORE-SELF-AUDIT-REPAIRS-01](coordination/results/CORE-SELF-AUDIT-REPAIRS-01.md)). Treat all of them as unverified.
- **Other pages with the same dead paths.** [W101_ONBOARDING_CONTEXT_CORPUS.md](W101_ONBOARDING_CONTEXT_CORPUS.md) links `finalbastion.com/wizard101-guides/w101-spell-guides/` and `.../w101-minigame-guides/`,
  neither of which is among the real categories found on 2026-11-19 (that list may be partial). That page was not audited here.
