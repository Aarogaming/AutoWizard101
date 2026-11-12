# W101 knowledge and mechanics audit — 2026-11-11

Task: **W101-KNOWLEDGE-AUDIT-01**. Workstream: **CORE-KNOWLEDGE**.
Owner: `knowledge_audit`. Scope: repository and primary-source review only.
Only this result file was edited. No game process, desktop, capture, model,
installer or gameplay was used. Research retrieval date: **2026-11-11**;
project timezone: America/New_York. Source publication dates are separate below.

## Conclusion

**We have a useful knowledge architecture and a small, provenance-preserving
import, but not a complete or automatically maintained game database. The
documentation is not uniformly accurate enough to become executable rules.**

The dated [W101 research](../../W101_GAME_RESEARCH_20261108.md) distinguishes
observations, historical documentation and unqualified candidates. In contrast,
[WIZARD101_GAME_KNOWLEDGE.md](../../WIZARD101_GAME_KNOWLEDGE.md) claims to be
exhaustive at line 4 and is promoted as comprehensive by `docs/INDEX.md:8`, but
contains no citations, source dates or current-client applicability records.
It combines plausible mechanics, legacy coordinate assumptions and statements
about implementation that do not match the code. Treat it as an **unverified
draft**, not the source of runtime facts. This audit recommends that change;
it does not edit or silently replace the guide.

This is a representative audit of the rules most likely to affect actions.
It is not a review of every quest, spell, pet, item, dungeon or game patch.
An official undated guide is primary documentation, not proof that every detail
still applies to the owner's current build or bindings.

## Highest-priority corrections

| Priority | Repository evidence | Finding and proposed correction |
| --- | --- | --- |
| P1 | `WIZARD101_GAME_KNOWLEDGE.md:144`; `ProjectMaelstrom/ProjectMaelstrom/Utilities/Modules/AutoIdleModule.cs:8` | Guide says AutoIdleModule uses background `PostMessage` A/D. Actual module compiles finite graphs and contains no input API. Native mode delegates to MenuModule at lines 26–41; movement is bounded pulses at lines 44–49. Correct this implementation claim. Do not infer background-input qualification from an idle-mode name. |
| P1 | `WIZARD101_GAME_KNOWLEDGE.md:103`–113; `Utilities/CombatGeometry.cs:18`–27, 61–77, 112–119 | Fixed legacy pixels and target-index conventions are presented beside game mechanics. These are unqualified geometry assumptions, not resolution-independent controls. Keep them as legacy fixtures until associated with a measured client viewport, UI scale and current target rectangles. Mathematical unit tests do not qualify clicks. |
| P1 | `WIZARD101_GAME_KNOWLEDGE.md:108` | Missing combat circles plus an ordinary HUD is not independent proof of a won battle or quest progress. Loading, capture failure, dismissal, fleeing and defeat can remove UI. Require a fresh state transition and the intended result, such as objective progress; distinguish unavailable observation from confirmed absence. This is an engineering inference, not a new game rule. |
| P1 | `WIZARD101_GAME_KNOWLEDGE.md:94` | “Cannot be pulled” while on sidewalks is an unjustified absolute. Official encounter documentation establishes that proximity/front-of-creature movement can initiate combat, not universal sidewalk immunity. Use sidewalks as a route preference with observed encounter margins and recovery, not a guaranteed safe-zone predicate. [Official encounter guide](https://www.wizard101.com/w101playersguide/how-to-play4). |
| P1 | `WIZARD101_GAME_KNOWLEDGE.md:31`–39 | 80/150 capacity may describe historical/base accounts, but cannot be the only possible current capacity. The publisher advertised backpack-expansion elixirs on October 26, 2026. Read the actual capacity/count; preserve protected equipment and require approved sale policy before any sell step. The sale is expired and provides no authorization to purchase anything. [Publisher announcement](https://www.play.wizard101.com/news/expansion-elixir-sale). |
| P2 | `WIZARD101_GAME_KNOWLEDGE.md:85`–89 | Quest guidance is not a collision-free path. Official documentation describes approximate steps, excludes some quests and limits Quest Finder search to the area/world. Ctrl+Shift+Q itself is confirmed, not an error. Keep objective selection separate from route generation and arrival verification. [Quest tracker](https://www.wizard101.com/w101playersguide/questing3), [keyboard defaults](https://www.wizard101.com/w101playersguide/keyboardshortcuts). |
| P2 | `WIZARD101_GAME_KNOWLEDGE.md:142`–144 | A fixed 15-minute timeout and continuous-input guarantee lack current qualification. Native Idle already has explicit local observations in `research/w101-context-20261108.json`; it must not disappear behind older spin advice. The November 5 publisher announcement places Idle in a regional rollout context, not a proof of unlimited uptime on all servers. [Publisher rollout announcement](https://www.play.wizard101.com/news/Ahmarra-Arrives-on-North-American-and-European-Servers). |
| P2 | `WIZARD101_GAME_KNOWLEDGE.md:49`–78, 120–126, 133–135 | Numeric pet progression/Energy costs, talent formulas, Team Up timing/penalties and realm cooldowns need claim-level provenance and current scope. They were not established by this audit's primary sources. Do not promote them to fixed budgets, timers or outcome guarantees. |

## Representative primary-source validation

All pages below were retrieved on **2026-11-11**. Unless a publication date is
stated, the page is undated; crawl freshness is not a release date. “Documented”
does not mean observed on the active client in this audit.

| Area | Verified baseline and qualification boundary | Primary source |
| --- | --- | --- |
| Interaction and menus | X interacts; Q opens the quest journal; I opens pets; M opens map; Esc toggles options. These are default bindings and state-dependent commands, not permission to blindly press them through dialogue/chat/loading. | [Keyboard shortcuts](https://www.wizard101.com/w101playersguide/keyboardshortcuts) |
| Movement | W/Up advances, S/Down backs up, A/Left and D/Right **turn**. The dedicated movement guide resolves the shortcut table's less precise “Move Left/Right” labels. Holding both mouse buttons moves forward with mouse direction. | [Getting around](https://www.wizard101.com/w101playersguide/getting-around1) |
| Transport | Home and End are different: End targets the current world's common area. Page Down creates a mark; Page Up recalls it. Do not interpret End as always Wizard City Commons. Marking spends mana; preserve existing marks. | [Keyboard shortcuts](https://www.wizard101.com/w101playersguide/keyboardshortcuts), [map/mark guide](https://www.wizard101.com/w101playersguide/getting-around3) |
| Easily confused shortcuts | R replies in chat; Ctrl+R and Num Lock autorun. Ctrl+T toggles quest tracking. Ctrl+Shift+Q sets the nearest quest. Ctrl+O uses a potion outside a duel; Ctrl+S opens the shop, so a generic “safe shortcut” list must not treat all keys as harmless. | [Keyboard shortcuts](https://www.wizard101.com/w101playersguide/keyboardshortcuts) |
| Map and quest navigation | Map displays locations and supports a world overview. It is not described as arbitrary click-to-teleport. Quest guidance is partial; approximate distance does not establish straight-line traversability. Quest Finder searches the current area, then world. | [Map guide](https://www.wizard101.com/w101playersguide/getting-around3), [quest tracker](https://www.wizard101.com/w101playersguide/questing3) |
| Pet Dance | Replay progressively longer arrows with arrow keys; documented endpoint is five rounds or three failures. The source supplies no universal five-second timing, confidence threshold, audio-note mapping or resolution recipe. Those belong to measured, versioned recognizer calibration. | [Pet minigames](https://www.wizard101.com/w101playersguide/pet-games8) |
| Pet training | Six training games are listed; training uses Energy, which replenishes over time. This confirms the resource type, not an exact age-cost table or regeneration interval for every account. | [Pet training](https://www.wizard101.com/w101playersguide/pet-games7) |
| Snacks | Training can be followed by a snack; preference messages distinguish ordinary/liked/loved snacks. The guide supports observation of preference, not a complete school/category formula, exact bonus table or permission to consume inventory. | [Pet feeding](https://www.wizard101.com/w101playersguide/pet-games6) |
| Pet attributes | Official guide names Strength, Intellect, Agility, Will and Power and describes talent relationships. It does not publish the exact formula table in the native calculator. Its Epic-focused talent explanation is historical scope, not an exhaustive current progression model. | [Pet statistics](https://www.wizard101.com/w101playersguide/pet-games3) |
| Resources and potions | Health/Mana globes and nearby potion bottles are documented; potion refill via minigames or Hilda Brewer is supported. The page does not establish the guide's exact zero-health reset values, refill-score threshold, or “Potion Motion in 10–20 seconds” performance claim. | [Health/Mana guide](https://www.wizard101.com/w101playersguide/how-to-play2) |
| Combat | Spell availability is displayed; grey cards are not castable yet. Treasure cards are consumable and require a separate draw process. These observations support eligibility/consumption checks, not fixed pixels or an automatic win detector. | [Casting guide](https://www.wizard101.com/w101playersguide/spell-cards3) |
| Native Idle chronology | Publisher announcement dated November 5, 2026 says the EU update **will** include Idle Wizard. This is forward-looking regional information; local Idle observations and later current-client qualification must remain separate. No new Idle hotkey is qualified by this audit. | [Ahmarra rollout announcement](https://www.play.wizard101.com/news/Ahmarra-Arrives-on-North-American-and-European-Servers) |

The existing `docs/W101_GAME_RESEARCH_20261108.md:23`–46 and 54–81 generally
handle these distinctions better than the unsourced overview. Preserve that
dated document as historical evidence; do not rewrite its earlier “not yet
observed” statements to imply they were wrong because later work succeeded.
Likewise, older four-game versus later six-game pet pages can reflect evolution,
not contradictory live facts. This audit found no primary-source basis for
turning every community/forum number into a current invariant.

## Knowledge store and native consumption

The existing architecture is a strong foundation for explainable layers:

`source revision → scoped claim → observation/qualification → disabled runtime
candidate → game-pack action/routine admission`.

`docs/GAME_KNOWLEDGE_DATABASE.md:19`–28 distinguishes these records;
`research/game_knowledge_schema.sql` implements the corresponding tables.
The latest claim leaf is explicitly **not** a truth-selection view. Dates,
region, platform, build and Live/Test context remain separate, and contradictory
claims can coexist. Keep this structure rather than replacing it with a single
large unversioned fact file or model prompt.

Actual implementation is much narrower than the proposed full-game catalog:

- `docs/W101_KNOWLEDGE_IMPORT.md:51`–73 and
  `tools/import_w101_knowledge.py` accept one explicitly mapped pet-snack ledger:
  eight source IDs and nine claim IDs. Arbitrary new sources/fields are rejected.
- `research/w101-context-20261108.json` contains navigation, shortcuts, Idle,
  minigames and other scoped claims, but is not the second implemented importer.
  `docs/GAME_KNOWLEDGE_DATABASE.md:103` still lists this mapping as planned.
- There is no native knowledge UI/provider, scheduled retrieval, automatic
  refresh or runtime import in this slice (`W101_KNOWLEDGE_IMPORT.md:99`–100).
  JSON export, new mappings and append-only cross-bundle merge remain next work
  (`W101_KNOWLEDGE_IMPORT.md:125`). This does not deny the separate research UI;
  it means the shared SQLite knowledge service is not connected to it.
- Webpage content hashes remain null because the importer only receives a
  ledger, not source-page bytes. Ledger hashes must never be represented as page
  hashes. Publication/modified dates, scope and evidence classifications are
  retained, with unknowns left unknown.
- Reimporting a changed ledger creates a different immutable bundle. This is
  not an implemented history merge between bundles; each starts at revision 1.

Read-only inspection of the existing local bundle
`.local/w101-knowledge-import-check/pet-snacks-v1-20261109/knowledge.sqlite`
on this audit date confirmed:

| Check | Result |
| --- | --- |
| Size | 323,584 bytes |
| SHA-256 matches import receipt | Yes |
| SQLite integrity check | `ok` |
| Sources / source revisions | 8 / 8 |
| Claims / evidence edges | 9 / 12 |
| Observations / qualifications / runtime candidates | 0 / 0 / 0 |
| Source revisions with webpage-content hashes | 0 |

The local bundle exists here but is not guaranteed in a clean checkout. Its
integrity is not evidence that every imported statement is current or true.

## Existing safeguards worth retaining

The snack recommendation implementation is more disciplined than the broad
knowledge overview: `Utilities/PetSnackRecommendations.cs:7` defines benefits as
observed totals for the pet/context; lines 18–19 separate feed cost from a
previously paid game entry. The one-attempt Energy gate at lines 166–175 needs
fresh verified Energy/cost and a reserve. The planner has item protection,
allowlists, stock reserves and bounded feed allowances. These are useful shared
patterns for potions, selling, crafting and quest item purchase cards. They do
not establish that inventory recognition or feeding is live qualified.

`Utilities/PetTalentCalculator.cs:21`–24 identifies a legacy formula port, but
the current file has no source revision/hash or calibration evidence. Its seven
named talent definitions are not an exhaustive talent database. Keep formula
calculation separate from observed pet identity, actual attributes and confirmed
displayed outcome; unknown talent names must not silently become game knowledge.

Two further reuse findings need the same boundary:

- `tools/wizsdk_combat_geometry.py:3`–6 and 25–26 advertise exact coordinates
  and no desktop-mouse interference. Its constants at lines 29–41 and target
  calculations at 74–99 simply reproduce a fixed layout. This pure helper
  neither sends inputs nor proves how an input adapter behaves. Safe replacement
  description: **legacy layout arithmetic, requiring a measured geometry
  profile and independently qualified input adapter**. The primary casting
  guide describes visible card availability, not these coordinates.
- `Utilities/ModernCombatCalculator.cs:25`–44 labels a hardcoded 150% intersection
  and rational curve as modern/KingsIsle behavior without a source or game-mode
  parameter. The publisher's [January 2023 EU update notes](https://eu.wizard101.com/game/community/update-notes/january2023)
  document historical PvE/PvP curves with different intersections; their PvE
  150 intersection approaches 200, whereas this code's curve mathematically
  approaches 250. This does **not** establish today's replacement coefficients;
  it shows that even the cited historical concept is not enough to label this
  formula authoritative. Keep it as an experimental estimate until a scoped
  current formula and fixtures are supplied. Critical scaling at lines 146–155
  also lacks authority/qualification. Pip checks at 69–73 count all power/school
  pips as two without checking school eligibility in that method; a comment
  about matching schools is not an implemented guard. Confirm observation units
  before applying any curve, so already-effective displayed damage is not
  transformed a second time. Do not present `IsLethal` estimates as verified
  combat outcomes.

## Bounded follow-up queue for the coordinator

1. **Documentation status repair:** mark the unsourced overview as unverified;
   correct the AutoIdle implementation description and index endorsement. Keep
   numbered legacy layouts explicitly historical, with links to qualification.
2. **Read-only knowledge export:** implement bounded versioned JSON exports of
   claims with their source revisions, scope, contradictions and qualifications.
   No new database provider is needed for the initial C# consumer design.
3. **Context-ledger mapping:** import the existing navigation/key/Idle research
   with stable IDs. Add source revision metadata and precise evidence locators;
   preserve November 8 observations as November 8, not current live evidence.
4. **Observed capability profile:** record current build/region, actual bindings,
   viewport/UI scale, resource capacity, pet cost and scene before a card uses a
   rule. Unknown prerequisites should defer the card with an explanation.
5. **Maintenance and coverage:** maintain per-domain missing/verified/stale
   counts, source revision differences and requalification triggers. Prioritize
   quest progress, battle outcomes, inventory capacity, potion behavior and
   snack costs over attempting to scrape every quest at once.
6. **Runtime provenance:** expose which observation and scoped claim allowed an
   action, what version of transformer produced it, and what outcome evidence
   resolved it. No source page, research text or copied legacy comment should
   directly become executable behavior.

## Verification and status

Review complete; **CORE-KNOWLEDGE remains incomplete**, consistent with its
current first-import/backlog state. Evidence: primary pages above, direct source
inspection and a read-only SQLite/hash check. No production behavior changed.
No native build or gameplay test was appropriate or run. No claim of current
client mechanics qualification is added. Central board/claims updates remain
with the coordinator.
