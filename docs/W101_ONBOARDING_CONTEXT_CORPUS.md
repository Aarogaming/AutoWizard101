# WIZARD101 ONBOARDING CONTEXT CORPUS

> **Status: DRAFT, partly contradicted by the official guide (audit 2026-11-20, Claude).** This page was committed as an "exact", "deterministic" specification, but it cites no sources and the repo holds no
> measurement behind its figures. It was checked against the official Wizard101 player guide (read-only requests; the pages are named in the notes), the repo's own audits and code, and the Internet Archive.
> Each "**Today:**" note below says what that check found; the original text is left unchanged. In short: the keyboard shortcuts are mostly confirmed (two rows are not), the Dance Game's five rounds and the pet
> stat names are confirmed (the stat-to-talent lists are not), and the energy costs, the XP cap, the 70-snack threshold, the chest rules and the whole combat section have no source. The Rust draft
> `crates/maelstrom-core/src/minigame_domain.rs` transcribes this page, and nothing outside its tests calls it.

## Purpose & Overview
This corpus provides a structured, deterministic onboarding specification for autonomous agent runtimes (such as Maelstrom and the Aaroneous runtime) operating within Wizard101. Prose guides and video walkthroughs introduce ambiguity; this document specifies exact numeric thresholds, finite state transitions, mathematical formulas, keyboard input vectors, and exit criteria across all primary game subsystems.

---

## 1. Pet Pavilion Mini-Games (Rules, Thresholds & Optimal Exit Logic)

Every Pet Pavilion mini-game costs wizard Energy scaling monotonically with the pet's current age bracket. Completing a game awards a hard cap of **4 Pet XP** (or **8 Pet XP** during active Double Pet XP member benefit events) upon satisfying the game's completion threshold.

> [!IMPORTANT]
> **Zero-Waste Invariant**: Never play beyond the 4 XP threshold. Accumulating additional points beyond the cap awards zero additional XP, wastes wall-clock runtime, and introduces unnecessary failure opportunities.

> **Today (audit 2026-11-20):** the official guide (`pet-games2`, `pet-games7`) says only that training games need Energy and that pets gain experience from them; it gives no per-age cost and no XP cap, so the
> 2-to-14 Energy progression and "4 / 8 Pet XP" are unsourced. The live dance-entry code reads the cost the game displays (`txtEnergyCost` in `W101DanceEntryContext.cs`) instead of using a table. Double Pet XP
> appears in [W101_PET_SNACK_RESEARCH_20261108.md](W101_PET_SNACK_RESEARCH_20261108.md) only as a past member event (February 2015), not a standing mode. The "Zero-Waste Invariant" is a design preference, not a game rule.

### Pet Age Energy Scaling
| Pet Age Bracket | Energy Cost per Mini-Game Run | Maximum Base XP Awarded |
| :--- | :---: | :---: |
| **Baby** | 2 Energy | 4 XP |
| **Teen** | 4 Energy | 4 XP |
| **Adult** | 6 Energy | 4 XP |
| **Ancient** | 8 Energy | 4 XP |
| **Epic** | 10 Energy | 4 XP |
| **Mega** | 12 Energy | 4 XP |
| **Ultra** | 14 Energy | 4 XP |

> **Today:** the official guide lists the ranks Baby, Teen, Adult, Ancient, Epic and Mega (`pet-games2`); `Ultra` and every Energy and XP figure in this table are unsourced.

### Mini-Game Operational Specs & Automation Priorities

| Mini-Game | Input Mechanics | Win / Max-XP Threshold | Optimal Agent Strategy (Zero-Waste) | Automation Tier |
| :--- | :--- | :--- | :--- | :---: |
| **Dance Game** | Directional arrow keys (`Up`, `Down`, `Left`, `Right`) matching 5 sequential rounds of increasing sequence length and speed. | Complete 3 of 5 rounds for win count; 5 of 5 rounds for max 4 XP. | Capture the prompt lane; emit discrete directional pulses with human jitter (50–120ms dwell). For Pet Promenade daily event counts, winning 3 rounds is sufficient; for XP training, complete all 5 rounds (~20–30s total). | **Tier 1 (Recommended)** |
| **Gobbler Drop** | `Left` / `Right` kinematic tracking catching falling snacks while dodging bombs and heavy anvils. | Score threshold met in ~30–40s of play. | Track sprite X-coordinates on screen. Intentionally avoid clock/time pickups once the score target is reached so the timer expires quickly. | **Tier 2 (Viable Fallback)** |
| **Maze Game** | 2D top-down grid navigation collecting snacks while avoiding patrolling gobblers. | Exactly 70 snacks collected = maximum 4 XP. | Compute shortest Hamiltonian path covering 70 snacks. Never collect +10s clock pickups; stop movement immediately at 70 snacks to allow timer expiry. | **Tier 2 (Viable Fallback)** |
| **Cannon Game** | Aim angle + power meter firing elemental shots (Fire, Ice, Storm) at matching target structures. | Clear fort target blocks within allocated shot budget. | Match shot element to target keystone blocks to collapse structures in 2–3 shots. | **Tier 3 (Constrained)** |
| **Grumpy Gobblers** | Trajectory slingshot aiming to knock down wooden/stone structures. | Clear all target gobblers on stage. | Low priority; high physics engine variance and slower throughput than Dance/Drop. | **Tier 4 (Deprioritized)** |
| **Way of the Ninja Pig** | 3D platformer obstacle course with moving hammers, narrow beams, and pits. | Reach end portal without falling. | Exclude from routine rotation entirely due to high fall risk and poor XP-per-minute efficiency. | **Excluded** |

> **Today (audit 2026-11-20), against the official guide's Pet Minigames page (`pet-games8`; its extracted text has no game names, so each block is matched to a game by its objective text, and the six game names come from `pet-games7`):**
>
> - **Dance Game:** arrow keys, a longer sequence each round, and the game ends after 5 rounds or 3 failures. That confirms the five rounds and the arrow keys. "3 of 5 for win count", "~20-30s" and "50-120ms dwell" are unsourced, and the 3-failure end is not modelled by the Rust evaluator.
> - **Gobbler Drop:** the guide calls it the Pet Drop Game: pick a food, move the pet side to side with the arrow keys, avoid falling dangers, try for bonus items. "Bombs and heavy anvils", clock pickups and the "30-40s" figure are unsourced.
> - **Maze Game:** arrow keys and the mouse, avoid the ghosts, collect as much food as possible before time runs out. The 70-snack threshold, the "+10s clock" pickups and "patrolling gobblers" are unsourced, and the Rust draft's `WASD` input string was contradicted and is corrected. "Shortest Hamiltonian path" is the wrong term (a route through a fixed set of 70 snacks is a travelling-salesman problem), and no solver exists in the repo.
> - **Cannon Game:** the guide says you fire your pet at the target, adjusting direction, angle and power around obstacles and wind. "Elemental shots (Fire, Ice, Storm)" and "2-3 shots" are not on the page and contradict it.
> - **Grumpy Gobblers:** the guide says only "knock everything down", with arrow keys, a power gauge and Space or Fire. The structures, "wooden/stone" and "target gobblers" are unsourced.
> - **Way of the Ninja Pig:** the guide says to reach the end of the course before time expires, avoiding obstacles that knock the pet off the track, using the arrow keys. Hammers, beams and pits, and the fall-risk and XP-per-minute reasoning, are unsourced.
> - **Automation Tier** is a ranking written with this page; nothing was measured. Only the Dance Game has a production runtime in the repo ([DANCE-PRODUCTION-RUNTIME-01](coordination/results/DANCE-PRODUCTION-RUNTIME-01.md)).

### Post-Game Feeding Invariant
Every mini-game run terminates at a modal **Snack-Feeding Prompt** (even if a mini-game round is failed, although winning unlocks higher-rank snack slots). Feeding a pet snack converts snack attributes into permanent pet stats:
- **Strength** (boosts Damage and Resistance talents)
- **Intellect** (boosts Mana and Accuracy talents)
- **Agility** (boosts Resistance and Armor Piercing talents)
- **Will** (boosts Damage and Pip Chance talents)
- **Power** (contributes universally to all talent pools)
- **Bonus Pet XP** (direct XP increase equal to snack rank/value)

> **Today (audit 2026-11-20):** confirmed by the official guide (`pet-games6`): a snack can be offered at the end of each training game, and the guide names these five statistics (`pet-games3`). Not confirmed: the stat lists above.
> The guide says Strength affects Damage, Power Pip chance and Resistance talents; Intellect affects Mana, accuracy and Power Pip talents; Agility affects Health, Accuracy and Resistance talents; Will affects Health and Mana talents;
> Power affects all six. So "Agility ... Armor Piercing" and "Will ... Damage and Pip Chance" are wrong, and Strength and Intellect are incomplete. "Bonus Pet XP equal to snack rank/value", the prompt appearing after a failed round and
> the "higher-rank snack slots" are unsourced; the repo's snack research records liked and loved snacks as +1 and +2 XP historically. Snack feeding is allowed only in an authorized live session with an exact policy (AGENTS.md runtime boundaries).

---

## 2. Fairegrounds & World Mini-Games (Mana/Health Refill & Chests)

### Fairegrounds Mana Refill Mechanics
Located in the Wizard City Commons Fairegrounds: **Potion Motion** (match-3 tile swapper), **Doodle Dug**, **Skull Riders**, **Hot Air**, **Sorcery Stones**, and **Concentration**.
- **Agent Utility**: Playing and concluding any Fairegrounds mini-game completely refills the player's blue mana wisps and fills potion bottles on exit.
- **Operational Policy**: Acts as an in-zone, zero-gold mana recovery fallback when street mana wisps are depleted or unmapped.

> **Today (audit 2026-11-20):** the official Mini Games page (`mini-games1`) says mini-games recharge the mana globe and earn gold and items, that every world has a Mini Games area, and that after more than 20 mini-games in 24 hours gold and items stop while mana still refills.
> That supports the mana refill as a fallback (the page I could read names only Skull Riders), but says nothing about potion bottles, the Wizard City Commons as the location, or the six names listed here. The 20-per-day limit is missing from this page.
> `FairegroundsMiniGame::refills_mana_and_potions_on_exit` returns `true` for all six without evidence.

### World Silver Chests (Puzzle Locks)
Silver chests spawn in place of normal wooden chests throughout all worlds and require solving a micro-puzzle:
1. **Shock-A-Lock**:
   - **Mechanism**: Rotate circuit tiles on a 2D grid to create an unbroken conduit connecting the power source node to the target sink node before the fuse countdown expires.
   - **Resolution**: Grid traversal (BFS/A*) to determine tile rotation counts.
2. **Catch-A-Key / Lockpick**:
   - **Mechanism**: Timing-based oscillator; press `Spacebar` when the moving key indicator aligns within the highlighted target zone on the sweep bar.
   - **Resolution**: Intercept timing calculation based on indicator velocity and target bar width.
3. **Safety & Failure Policy**:
   - Cap attempts at a hard maximum of **2 retries** per chest instance.
   - If a chest fails twice or times out, write its `(X, Y, Z, zone)` coordinates to an ephemeral cooldown table (TTL: 300s) to prevent recursive interaction loops on contested or desynced chests.

> **Today (audit 2026-11-20):** none of the official guide pages checked describes Shock-A-Lock or Catch-A-Key; Final Bastion has a Shock-A-Lock silver chest page in the Internet Archive (contents not read). The grid mechanics, the fuse, BFS/A* and the "press Spacebar in the target zone" rule are unsourced.
> "2 retries" is ambiguous: `SilverChestPolicy` allows two attempts in total (its cap is exhausted by the second failure, which matches "fails twice"), not one attempt plus two retries. The cooldown table of `(X, Y, Z, zone)` entries with a 300s TTL does not exist;
> the policy only carries `cooldown_ttl_seconds` and returns `true` when the cap is reached.

### Cantrip Chests (Field Magic)
- Located across higher-level worlds (Wysteria, Avalon, Azteca, etc.).
- Requires casting an active Field Cantrip (e.g., *Magic Touch*, *Second Sight*).
- **Solo Cantrip Chests**: Opened by a single wizard casting the required cantrip.
- **Three-Lock Cantrip Chests**: Require 3 unique wizards casting simultaneously within a 5-second window. In solo runtime mode, skip three-lock chests unless 2+ nearby player avatars are verified present at the chest radius.

> **Today (audit 2026-11-20):** the official Cantrips page (`cantrips`) describes cantrip area effects such as healing, mana and fishing luck; the chest rules (solo versus three-lock, the 5-second window, the example cantrips and worlds) are unsourced.
> `CantripChestType::can_attempt` models only a head count; the 5-second simultaneity is not modelled.

---

## 3. Combat Onboarding Spec (Deterministic Rules & Damage Stack)

Wizard101 combat operates as an atomic, turn-based LIFO stack calculator within a 30-second planning phase.

> **Today (audit 2026-11-20):** none of the official guide pages checked (`spell-cards`, `magic-schools`, `pvp-help`) states a LIFO resolution order, an initiative roll, a 30-second planning timer, street scaling or a flee rule.
> [COMBAT_DUELING_LOOP.md](COMBAT_DUELING_LOOP.md) already records that its 30-second timer has no source, and the code has no `Flee` action. The Rust damage draft multiplies charm and ward factors, so list order has no effect.
> Treat everything in this section as an unverified hypothesis.

### Duel Circle Initiative & Street Scaling
- **Initiative Roll**: Entering a combat sigil rolls First vs. Second initiative. Going first allows charms/wards and damage to resolve before enemy actions; going second requires absorbing incoming attacks before spells trigger.
- **Solo Street Scaling**:
  - Early Wizard City (Unicorn Way, Triton Avenue): 1 mob per solo player.
  - Colossus Boulevard and all subsequent worlds: 2 mobs spawn per solo player (`Players + 1` rule).

### Exact Damage Calculation Pipeline
Spells resolve mathematically through an exact multiplicative pipeline where step ordering is critical:

$$\text{Damage} = \left\lfloor \left(\text{Base} + \text{Enchant}\right) \times \left(1.0 + \text{Gear\%}\right) + \text{FlatDmg} \right\rfloor \times \left(1.0 + \text{Aura}\right) \times \prod \left(1.0 + \text{Charm}_{\text{LIFO}}\right) \times \left(1.0 + \text{Global}\right) \times \prod \left(1.0 + \text{Ward}_{\text{LIFO}}\right) \times \text{CritMult} \times \left(1.0 - \text{Resist} + \text{Pierce}\right)$$

> **Today:** the final factor `(1.0 - Resist + Pierce)` does not match the code, which uses `1 - R_eff` with `R_eff = max(Resist - Pierce, 0)` for a positive resist and `R_eff = Resist` for a negative one
> (details in [W101-ONBOARDING-CORPUS-01](coordination/results/W101-ONBOARDING-CORPUS-01.md)). The stage order and "exact" are unverified, and the fixture values in the Rust tests (an Efreet base of 895, an enchant of 300) are invented.

### LIFO Resolution & Stacking Rules
1. **LIFO Stack Rule**: Charms (Blades, Weaknesses) and Wards (Traps, Shields) resolve **Last-In, First-Out** (the most recently applied buff or debuff consumes first).
2. **Duplicate Stacking Rule**: Spells of identical percentage and identical item source do *not* stack. To stack multiple blades or traps, they must originate from distinct sources:
   - Regular Trained Spell (e.g., Fireblade +35%)
   - Sharpened / Potent Enchanted Spell (e.g., Fireblade +45%)
   - Gear / Pet Item Card (e.g., Fireblade +35% Item Card)
   - Treasure Card (TC) (e.g., Fireblade +40% TC)
3. **Weakness & Shield Mitigation**:
   - Incoming negative charms (Weakness, Black Mantle) sit on top of the player's blade stack.
   - Clear high debuffs (-50% to -90%) using a 0-pip off-school wand attack or *Cleanse Charm* prior to unleashing primary AoE damage.

### Targeting & Threat Arbitration
- **Enemy AI Default**: Enemies default their attacks to **Slot 1** (the leftmost duel circle pedestal).
- **Threat Weighting**: Healing spells generate higher aggregate threat per point than raw damage. Taunt and Pacify shift threat weights directly.
- **Mob-Clearing Heuristic**: In encounters with 2+ enemies, stacking 1–2 blades into a high-yield school AoE spell (*Meteor Strike*, *Tempest*, *Scarecrow*, *Sandstorm*, *Humongofrog*, *Frost Giant*, *Forest Lord*) is mathematically superior to sequential single-target hits.

### Combat Flee & Recovery Invariant
- Executing a Flee action instantly drops wizard Mana to **0** and teleports the character to the current world's Commons hub.
- Following any flee, verify potion count or collect blue wisps before navigating back to combat.

> **Today:** unsourced in the three subsections above: the LIFO consumption order, the duplicate-stacking rule and its four source categories, the example percentages (35%, 45%, 40%), the "-50% to -90%" debuff range and the cleanse advice,
> Slot 1 as the default target, healing out-threatening damage, the AoE spell list, and the flee rule (mana to 0, teleport to the Commons).

---

## 4. Navigation & Keyboard Input Data (Zero-Mouse Shortcuts)

Exposing built-in keyboard shortcuts eliminates fragile pixel-hunting and UI coordinate drift:

| Key / Shortcut | In-Game Action | Agent Runtime Utility |
| :--- | :--- | :--- |
| `W` / `A` / `S` / `D` | Move Forward, Turn Left, Move Backward, Turn Right | Core kinematic locomotion (`W` pulse duration + `A`/`D` heading alignment). |
| `Q` / `E` | Strafe Left / Strafe Right | Micro-lateral obstacle clearance along street curbs without altering heading yaw. |
| `X` | Context Interact (Talk, Collect, Enter Sigil, Use) | Primary affordance trigger whenever within 3D interaction radius ($R \le 15.0$). |
| `Spacebar` | Jump / Advance Dialogue / Hook Fish | Clears low elevation lips, advances dialogue window pages, and catches fish on audio splash. |
| `Ctrl + T` | Toggle Quest Tracker HUD | Guarantees active quest banner, objective text, and progress counters are visible for OCR. |
| `Ctrl + Shift + Q` | Snap Quest Helper Arrow to Nearest Quest | Deterministically points the 3D HUD compass arrow to the nearest active goal/portal. |
| `N` | Toggle Visual Navigation Trail | Toggles breadcrumb trail on the ground leading directly to active quest objective. |
| `Home` | Teleport to Equipped Castle / Dorm | Instant zero-pathing jump to player house for gardening plots, bank, or pet training. |
| `End` | Teleport to Current World Commons Hub | Instant escape/reset to world center (Wizard City Commons, Krokotopia Oasis, etc.). |
| `Page Down` | Drop Location Marker | Places an anchor marker at current 3D coordinates (requires 100% mana). |
| `Page Up` | Recall to Location Marker | Instant teleport back to dropped marker (consumes mana; bypasses long return trips). |
| `Ctrl + O` | Consume Potion (Out of Combat) | Refills 100% health and mana instantly from bottle stock without opening menus. |
| `Ctrl + K` | Out-of-Combat Deck Profile Switcher | Instant swap between Mob Deck and Boss Deck profiles. |
| `Ctrl + M` | Open Realm & Area Switcher | Deterministically opens realm list for hopping to low-population realms for reagents. |

> **Today (audit 2026-11-20), against the official Keyboard Shortcuts page (`keyboardshortcuts`) and Getting Around page (`getting-around1`):**
>
> - **Confirmed (12 of 14 rows):** `W`/`A`/`S`/`D` (Getting Around says `A` and `D` turn left and right; the shortcuts page says Move Left and Move Right), `X` Interact, `Space` Jump, `Ctrl+T` quest tracker, `Ctrl+Shift+Q` quest helper to the nearest quest, `Home` go to your home,
>   `End` go to the Common Area of the current world, `Page Down` Set Marker, `Page Up` Recall to Marker, `Ctrl+O` use a potion outside a duel, `Ctrl+K` swap card decks outside a duel, `Ctrl+M` open the Realms section.
> - **Contradicted:** `Q` is Open/Close Quest Journal on the official page, and no page lists `E` or any strafe key, so the `Q`/`E` strafe row is unsupported. `N` is Turn On/Off Navigation Bubbles; "visual navigation trail ... breadcrumb" is a different description.
> - **Unsourced additions:** dialogue and fishing for `Space`, "requires 100% mana" for `Page Down`, a mana cost for `Page Up`, "refills 100% health and mana" for `Ctrl+O`, the interaction radius `R <= 15.0`, the Mob/Boss deck naming, and the low-population-realm use of `Ctrl+M`.

---

## 5. Curated Onboarding Tutorials & Reference Links

> **Today (audit 2026-11-20):** status tags as in [the reference corpus](W101_MASTER_REFERENCE_CORPUS.md) (its section 9 has the method). The descriptions are unverified guesses.

1. **Official Wizard101 Player Onboarding Guide**:
   - [Getting Around & Core Controls](https://www.wizard101.com/w101playersguide/getting-around1) — Official control schemas, UI element locations, and movement standards. **[replaced]** (was `/w101playersguide/getting-around`, which redirects to a 404 page; `getting-around1` and `getting-around2` answer 200)
2. **Wizard101 Central Pet Pavilion Master Guide**:
   - [Pet Pavilion Mini-Games Mechanics](https://wiki.wizard101central.com/wiki/Basic:Training_Pets) — Exact scoring tables, snack stat multipliers, and arena size classifications. **[replaced]** (was `Basic:Pet_Games`, which the archive has never captured; `Basic:Training_Pets` and `Basic:Minigames` are archived; nearest page, contents not compared)
3. **Final Bastion Combat & Deck Architecture Guide**:
   - [Combat Strategies & 7-Card Deck Templates](https://finalbastion.com/wizard101-guides/w101-spell-guides/) — Mathematical deck-slimming principles for rapid mob farming vs. multi-phase bossing. **[wrong]** (path absent from the archive; no equivalent found)
4. **Final Bastion Minigame & Silver Chest Reference**:
   - [Mini-Games & Chest Puzzles](https://finalbastion.com/wizard101-guides/w101-crafting-guides/how-to-play-shock-a-lock-silver-chest-game/) — Shock-A-Lock solutions, Catch-A-Key timing windows, and fishing mechanics. **[replaced]** (was `/w101-minigame-guides/`, absent from the archive; archived 200 capture of the Shock-A-Lock silver chest page)
