# Data-Driven Quest Interpreter & Dialogue Engine

## 1. Architectural Motivation

Pre-coding every individual quest into hardcoded domain scripts does not scale across Wizard101's 16+ worlds and hundreds of hours of quest content. Furthermore, it introduces maintenance brittleness whenever quest progression texts or intermediate steps receive minor game updates.

Instead of hardcoding quest IDs, the Maelstrom Autonomy Stack employs a **Data-Driven Quest Interpreter** (`crates/maelstrom-core/src/quest_interpreter.rs`). This engine parses live UI and memory observations into **six canonical objective archetypes**, delegating spatial navigation and entity anchoring to a decoupled **World Topology & Anchor Registry** in `quest_domain.rs`.

```
┌─────────────────────────────────────────────────────────────────────────┐
│               Read-Only Sensory Layer (UI & HUD Facts)                  │
│   - Quest Tracker HUD (`Ctrl+T`)                                        │
│   - Quest Journal (`Q`)                                                 │
│   - Quest Helper Compass Arrow (`Ctrl+Shift+Q`)                         │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │ W101QuestFact
┌────────────────────────────────────▼────────────────────────────────────┐
│                    Data-Driven Quest Interpreter                        │
│   - Regex / Pattern Matcher $\to$ Canonical Objective Archetype         │
│   - Universal Dialogue & Quest-Accept State Machine                     │
│   - Fallback Compass Bearing Steering Vector                            │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │ Query Anchors / Routes
┌────────────────────────────────────▼────────────────────────────────────┐
│              Decoupled World Topology (quest_domain.rs)                 │
│   - Named Entity & Landmark Registry (`(zone_id, X, Y, Z)`)            │
│   - Zone Navigation Graphs (Olde Town, Triton Avenue, Unicorn Way)      │
│   - Symmetric Portal Transitions & Sidewalk / Street Weights            │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 2. The Six Canonical Objective Archetypes

Every mainline and side quest in Wizard101 resolves to one of six primitive execution templates:

| Archetype | HUD / Journal Pattern | Execution Template |
| :--- | :--- | :--- |
| **1. Talk / Turn-In** | `"Talk to <NPC> in <Zone>"` | Route via Web-Walker to `<Zone>` $\to$ query Anchor Registry for `<NPC>` $\to$ `X` interact pulse $\to$ advance dialogue loop. |
| **2. Explore / Approach** | `"Explore <Landmark>"` / `"Go to <Tower/Gate>"` | Query Anchor Registry for `<Landmark>` $\to$ navigate to centroid volume $\to$ monitor for objective clear event. |
| **3. Defeat (Count)** | `"Defeat <N> <Mob> in <Zone>"` | Verify/equip `mob_aoe` deck profile $\to$ navigate to `<Mob>` spawn cluster $\to$ engage patrol until kill counter $= N$. |
| **4. Defeat & Collect** | `"Defeat <Mob> and collect <N> <Item>"` | Identical to **Defeat**, tracking item drop fraction rather than combat kills. |
| **5. Interact / Gather** | `"Collect <N> <Object>"` / `"Use <Device>"` | Query entity / POI list for `<Object>` $\to$ approach outside mob aggro cylinders $\to$ `X` interact $\to$ realm hop if node exhausted. |
| **6. Boss / Sigil Instance** | `"Go to <Tower>"` $\to$ `"Defeat <Boss>"` | Check GOAP preconditions (mana, potions, `boss` deck) $\to$ step onto sigil $\to$ wait 10s countdown $\to$ execute boss combat dueling loop. |

---

## 3. Unified Quest Fact Structure (`W101QuestFact`)

The sensory layer provides an immutable `W101QuestFact`:

```rust
pub struct W101QuestFact {
    pub quest_title: String,
    pub is_mainline: bool,
    pub archetype: QuestArchetype,
    pub target_entity_or_landmark: String,
    pub target_zone: String,
    pub current_count: u32,
    pub required_count: u32,
    pub fallback_bearing_deg: Option<f64>,
}
```

### The Quest Helper Compass Vector (Zero-Hardcoding Bearing)
Wizard101's built-in Quest Helper arrow points directly toward the active objective's zone transition portal or local world coordinates. `Ctrl+Shift+Q` snaps the arrow to the active tracking task. Even when a newly discovered quest or obscure NPC is missing from the static Anchor Registry, reading the arrow's bearing angle gives the Pure Pursuit steering controller a valid directional vector toward the objective.

---

## 4. Universal Dialogue & Quest-Accept State Machine

All NPC conversations, quest offers, and reward turn-ins follow an identical, bounded UI flow:

```
┌─────────────────┐       Interact 'X'       ┌──────────────────┐
│ Prompt Detected │ ───────────────────────> │  Dialogue Page   │
│ ("Press X...")  │                          │  (Next / Space)  │
└─────────────────┘                          └────────┬─────────┘
                                                      │ Advance text
                                                      │ (250-650ms jitter)
                                                      ▼
┌─────────────────┐       Policy Accept      ┌──────────────────┐
│  Quest Accepted │ <─────────────────────── │   Quest Offer    │
│  (Update Goal)  │                          │ (Accept/Decline) │
└─────────────────┘                          └──────────────────┘
```

1. **Prompt Detection**: Detect interact badge (`"Press X to Talk"`).
2. **Dialogue Page**: Advance dialogue pages using `Spacebar` with randomized human cadence (`250–650 ms` log-normal jitter) while persisting conversation text for replay audit.
3. **Quest Offer**: Match quest title against user policy (`accept_mainline_only` vs `accept_all_in_zone`); trigger click on `Accept`.
4. **Turn-In / Reward**: Click `Complete` / `Done`, clear active `QuestGoal` on the blackboard, and schedule an immediate Quest Tracker re-parse.
5. **Stuck / Unreachable Guard**: If interact fails to open the dialogue window after 3 bounded pulses, trigger a 2.5s backoff and re-query player Z-height.

---

## 5. Decoupled World Topology & Anchor Registry

`quest_domain.rs` strictly manages **spatial infrastructure**, not individual quest scripts:
- **`ZONE_OLDE_TOWN`**:
  - Waypoints connecting Commons Tunnel to Triton Avenue, Firecat Alley, and Cyclops Lane gates.
  - Sidewalk vs street penalty zones.
  - NPC Anchors: `Sergeant Muldoon`, `Susie Gryphonbane`.
- **`ZONE_TRITON_AVENUE`**:
  - Main avenue navigation corridor, Haunted Tower entrance, Kraken Gate, Galvanost Tower, Tempest Nexus.
  - NPC Anchors: `Duncan Grimwater`, `Artur Wethersfield`.
  - Mob spawn clusters: Scarlet Screamer, Rotting Fodder, Lumbering Troll.
