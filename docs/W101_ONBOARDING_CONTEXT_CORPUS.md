# WIZARD101 ONBOARDING CONTEXT CORPUS

## Purpose & Overview
This corpus provides a structured, deterministic onboarding specification for autonomous agent runtimes (such as Maelstrom and the Aaroneous runtime) operating within Wizard101. Prose guides and video walkthroughs introduce ambiguity; this document specifies exact numeric thresholds, finite state transitions, mathematical formulas, keyboard input vectors, and exit criteria across all primary game subsystems.

---

## 1. Pet Pavilion Mini-Games (Rules, Thresholds & Optimal Exit Logic)

Every Pet Pavilion mini-game costs wizard Energy scaling monotonically with the pet's current age bracket. Completing a game awards a hard cap of **4 Pet XP** (or **8 Pet XP** during active Double Pet XP member benefit events) upon satisfying the game's completion threshold.

> [!IMPORTANT]
> **Zero-Waste Invariant**: Never play beyond the 4 XP threshold. Accumulating additional points beyond the cap awards zero additional XP, wastes wall-clock runtime, and introduces unnecessary failure opportunities.

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

### Mini-Game Operational Specs & Automation Priorities

| Mini-Game | Input Mechanics | Win / Max-XP Threshold | Optimal Agent Strategy (Zero-Waste) | Automation Tier |
| :--- | :--- | :--- | :--- | :---: |
| **Dance Game** | Directional arrow keys (`Up`, `Down`, `Left`, `Right`) matching 5 sequential rounds of increasing sequence length and speed. | Complete 3 of 5 rounds for win count; 5 of 5 rounds for max 4 XP. | Capture the prompt lane; emit discrete directional pulses with human jitter (50–120ms dwell). For Pet Promenade daily event counts, winning 3 rounds is sufficient; for XP training, complete all 5 rounds (~20–30s total). | **Tier 1 (Recommended)** |
| **Gobbler Drop** | `Left` / `Right` kinematic tracking catching falling snacks while dodging bombs and heavy anvils. | Score threshold met in ~30–40s of play. | Track sprite X-coordinates on screen. Intentionally avoid clock/time pickups once the score target is reached so the timer expires quickly. | **Tier 2 (Viable Fallback)** |
| **Maze Game** | 2D top-down grid navigation collecting snacks while avoiding patrolling gobblers. | Exactly 70 snacks collected = maximum 4 XP. | Compute shortest Hamiltonian path covering 70 snacks. Never collect +10s clock pickups; stop movement immediately at 70 snacks to allow timer expiry. | **Tier 2 (Viable Fallback)** |
| **Cannon Game** | Aim angle + power meter firing elemental shots (Fire, Ice, Storm) at matching target structures. | Clear fort target blocks within allocated shot budget. | Match shot element to target keystone blocks to collapse structures in 2–3 shots. | **Tier 3 (Constrained)** |
| **Grumpy Gobblers** | Trajectory slingshot aiming to knock down wooden/stone structures. | Clear all target gobblers on stage. | Low priority; high physics engine variance and slower throughput than Dance/Drop. | **Tier 4 (Deprioritized)** |
| **Way of the Ninja Pig** | 3D platformer obstacle course with moving hammers, narrow beams, and pits. | Reach end portal without falling. | Exclude from routine rotation entirely due to high fall risk and poor XP-per-minute efficiency. | **Excluded** |

### Post-Game Feeding Invariant
Every mini-game run terminates at a modal **Snack-Feeding Prompt** (even if a mini-game round is failed, although winning unlocks higher-rank snack slots). Feeding a pet snack converts snack attributes into permanent pet stats:
- **Strength** (boosts Damage and Resistance talents)
- **Intellect** (boosts Mana and Accuracy talents)
- **Agility** (boosts Resistance and Armor Piercing talents)
- **Will** (boosts Damage and Pip Chance talents)
- **Power** (contributes universally to all talent pools)
- **Bonus Pet XP** (direct XP increase equal to snack rank/value)

---

## 2. Fairegrounds & World Mini-Games (Mana/Health Refill & Chests)

### Fairegrounds Mana Refill Mechanics
Located in the Wizard City Commons Fairegrounds: **Potion Motion** (match-3 tile swapper), **Doodle Dug**, **Skull Riders**, **Hot Air**, **Sorcery Stones**, and **Concentration**.
- **Agent Utility**: Playing and concluding any Fairegrounds mini-game completely refills the player's blue mana wisps and fills potion bottles on exit.
- **Operational Policy**: Acts as an in-zone, zero-gold mana recovery fallback when street mana wisps are depleted or unmapped.

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

### Cantrip Chests (Field Magic)
- Located across higher-level worlds (Wysteria, Avalon, Azteca, etc.).
- Requires casting an active Field Cantrip (e.g., *Magic Touch*, *Second Sight*).
- **Solo Cantrip Chests**: Opened by a single wizard casting the required cantrip.
- **Three-Lock Cantrip Chests**: Require 3 unique wizards casting simultaneously within a 5-second window. In solo runtime mode, skip three-lock chests unless 2+ nearby player avatars are verified present at the chest radius.

---

## 3. Combat Onboarding Spec (Deterministic Rules & Damage Stack)

Wizard101 combat operates as an atomic, turn-based LIFO stack calculator within a 30-second planning phase.

### Duel Circle Initiative & Street Scaling
- **Initiative Roll**: Entering a combat sigil rolls First vs. Second initiative. Going first allows charms/wards and damage to resolve before enemy actions; going second requires absorbing incoming attacks before spells trigger.
- **Solo Street Scaling**:
  - Early Wizard City (Unicorn Way, Triton Avenue): 1 mob per solo player.
  - Colossus Boulevard and all subsequent worlds: 2 mobs spawn per solo player (`Players + 1` rule).

### Exact Damage Calculation Pipeline
Spells resolve mathematically through an exact multiplicative pipeline where step ordering is critical:

$$\text{Damage} = \left\lfloor \left(\text{Base} + \text{Enchant}\right) \times \left(1.0 + \text{Gear\%}\right) + \text{FlatDmg} \right\rfloor \times \left(1.0 + \text{Aura}\right) \times \prod \left(1.0 + \text{Charm}_{\text{LIFO}}\right) \times \left(1.0 + \text{Global}\right) \times \prod \left(1.0 + \text{Ward}_{\text{LIFO}}\right) \times \text{CritMult} \times \left(1.0 - \text{Resist} + \text{Pierce}\right)$$

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

---

## 5. Curated Onboarding Tutorials & Reference Links

1. **Official Wizard101 Player Onboarding Guide**:
   - [Getting Around & Core Controls](https://www.wizard101.com/w101playersguide/getting-around) — Official control schemas, UI element locations, and movement standards.
2. **Wizard101 Central Pet Pavilion Master Guide**:
   - [Pet Pavilion Mini-Games Mechanics](https://wiki.wizard101central.com/wiki/Basic:Pet_Games) — Exact scoring tables, snack stat multipliers, and arena size classifications.
3. **Final Bastion Combat & Deck Architecture Guide**:
   - [Combat Strategies & 7-Card Deck Templates](https://finalbastion.com/wizard101-guides/w101-spell-guides/) — Mathematical deck-slimming principles for rapid mob farming vs. multi-phase bossing.
4. **Final Bastion Minigame & Silver Chest Reference**:
   - [Mini-Games & Chest Puzzles](https://finalbastion.com/wizard101-guides/w101-minigame-guides/) — Shock-A-Lock solutions, Catch-A-Key timing windows, and fishing mechanics.
