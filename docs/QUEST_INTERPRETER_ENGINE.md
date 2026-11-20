# Data-Driven Quest Interpreter & Dialogue Engine

> **Status (audit 2026-11-20, Claude):** this page is a design. The module it describes (`crates/maelstrom-core/src/quest_interpreter.rs`) is real library code with offline unit tests: a case-sensitive English
> prefix classifier into six archetypes, and a dialogue state machine that returns proposal strings. Nothing calls it outside its tests, nothing consumes its strings, nothing reads the HUD, the journal
> or the compass arrow, and none of the execution templates below exist. Corrections are inline; notes marked **Today** say what the code does. Quest wording, NPC, zone and mob names are taken from the
> code and were not checked against the game. Evidence: [W101-QUEST-INTERPRETER-CORE-02](coordination/results/W101-QUEST-INTERPRETER-CORE-02.md) and
> [W101-QUEST-INTERPRETER-DOCS-01](coordination/results/W101-QUEST-INTERPRETER-DOCS-01.md), audit notes (2026-11-19 and 2026-11-20).

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

**Today:** the diagram is the intended architecture. The "Regex / Pattern Matcher" is `strip_prefix` and `find` on one string handed to `parse_from_text` (no regex dependency); nothing hands this module tracker, journal or
compass data (it is only called with literal strings in its own tests), so the top box does not exist. There is no "Fallback Compass Bearing Steering Vector": the bearing is a caller-supplied number that is carried inside a parsed fact and used by nothing.
The registry is a table of eight anchors plus GOAP travel actions in `quest_domain.rs`; it has no zone navigation graphs, portal geometry or sidewalk and street weights (section 5).

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

**Today:** the right-hand column is intended behaviour and does not exist: nothing routes, queries a registry from a fact, equips a deck, counts kills or drops, hops realms, or waits for a sigil countdown.
The module only classifies a line. What `parse_from_text` does, checked with a throwaway probe and now pinned in part by unit tests:
- It matches only the case-sensitive English prefixes `Talk to`, `Explore`, `Go to`, `Defeat`, `Collect` and `Use`. `talk to Susie Gryphonbane` and `Visit Mr Lincoln` return `None`. `Go to <Tower>` is
  classified Explore, not Boss/Sigil, so row 6's `"Go to <Tower>"` wording does not match the code.
- The ` in <Zone>` suffix is split off for Talk, Explore/Go to and Defeat lines. For `Defeat X in <Zone> and collect N Y` the zone stays inside the mob name (`Rotting Fodder in Triton Avenue / Luminescent
  Fungus`, zone empty), and for `Collect N X in <Zone>` it stays inside the object name. `... and Collect ...` with a capital C falls through to Defeat (Count).
- A `Defeat` line is Boss/Sigil when the quest title contains "tower" or the target contains "boss" or "kraken" (substring, any case). `Defeat 4 Scarlet Screamers in Triton Avenue` under the title
  `Haunted Towers` becomes Boss/Sigil, and so does `Defeat Bosswick`. With no leading number the count is 1; a number that does not fit `u32` (`99999999999 Foo`) stays in the name with a count of 1.
- No progress fraction is parsed: `(2/4)` stays inside the target or zone text.

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

**Today:** the struct matches the code field for field. `current_count` is always 0 (no progress fraction is parsed), `required_count` is 1 unless a leading number is found, and
`fallback_bearing_deg` is whatever the caller passes (the [0, 360) range is a doc comment; 370, -10 and NaN are carried unchanged). A line that does not parse yields no fact, so its bearing is dropped.

### The Quest Helper Compass Vector (Zero-Hardcoding Bearing)
Wizard101's built-in Quest Helper arrow points directly toward the active objective's zone transition portal or local world coordinates. `Ctrl+Shift+Q` snaps the arrow to the active tracking task. Even when a newly discovered quest or obscure NPC is missing from the static Anchor Registry, reading the arrow's bearing angle gives the Pure Pursuit steering controller a valid directional vector toward the objective.

**Today:** `Ctrl+Shift+Q` is a documented default whose documented purpose is "select nearest quest" ([WIZARD101_GAME_KNOWLEDGE.md](WIZARD101_GAME_KNOWLEDGE.md), citing the official shortcuts page; this was
not re-checked against the page here). An earlier version of the 2026-11-19 audit note called the key unverified, which was wrong. What is not supported is this section's description: "snaps the arrow to the active
tracking task" and "a valid directional vector toward the objective". The sourced docs say quest guidance is approximate and not a collision-free path, and
[EPISTEMIC_RUNTIME_HARDENING.md](EPISTEMIC_RUNTIME_HARDENING.md) calls it an orientation hint with "no code reads the arrow". Nothing here produces a bearing or feeds one to steering.

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

**Today:** `DialogueStateMachine` has six states and returns the strings `PROPOSAL:Interact:X`, `PROPOSAL:Dialogue:AdvanceSpace`, `...:Accept`, `...:Complete` and `...:Dismiss`; no Rust, C# or Python code outside
its tests reads them. Step by step:
1. "Press X to Talk" is text the caller passes to `on_prompt_detected`; nothing detects it.
2. There is no jitter, log-normal or 250-650 ms timing anywhere in the module, and the conversation text is not persisted: each page overwrites the previous one in the state.
3. `QuestAcceptPolicy` has no effect (both variants propose Accept, and the machine never receives `is_mainline`); nothing clicks.
4. A reward page yields `PROPOSAL:Dialogue:Complete` and the `TurnInReward` state (tested). Nothing clears a `QuestGoal` or schedules a tracker re-parse; `finish_interaction` only resets the machine.
5. Three failed interacts give `StuckBackoff { failed_attempts: 3, cooldown_ms: 2500 }` (tested). Nothing re-queries height, and no timer leaves the state: only `finish_interaction` does, and a 4th failure keeps counting.
   `on_interact_failed` enters the same state from `Idle` too. A second path exists that the page does not mention: after 25 non-final pages the next page call returns `Dismiss` and enters
   `StuckBackoff { 25, 3000 }` (tested), which also discards a final page arriving at that point.

---

## 5. Decoupled World Topology & Anchor Registry

`quest_domain.rs` holds an anchor table and GOAP travel actions (it also holds the starter questlines: see [W101_QUEST_REPERTOIRE.md](W101_QUEST_REPERTOIRE.md)):
- **`ZONE_OLDE_TOWN`**:
  - Travel actions, cost 5 each way: Commons, Triton Avenue, Firecat Alley and Cyclops Lane. These are zone-to-zone planning edges, not waypoints or gate geometry.
  - NPC anchor: `Sergeant Muldoon`.
- **`ZONE_TRITON_AVENUE`**:
  - NPC anchors: `Susie Gryphonbane`, `Duncan Grimwater`, `Artur Wethersfield`.
  - Landmark anchors: `Haunted Towers`, `Kraken Gate`, `Galvanost Tower`, `Tempest Nexus`.
- **`ZONE_FIRECAT_ALLEY`** and **`ZONE_CYCLOPS_LANE`** have zone constants and travel actions but no anchors.

**Today:**
- An earlier version of this section put Susie Gryphonbane in Olde Town (the code and `test_quest_interpreter_anchor_resolution_pipeline` put her in Triton Avenue) and listed waypoints, sidewalk and street
  penalty zones, a main-avenue corridor and mob spawn clusters (Scarlet Screamer, Rotting Fodder, Lumbering Troll). None of those exists in `quest_domain.rs`; in the Rust and C# sources the first two mob names appear only as parse-test strings.
- The eight (x, y, z) coordinates are round numbers with no source in the repository. The tests pin the values, which freezes them; it does not validate them.
- `find_spatial_anchor` takes a name only: ASCII case is ignored, whitespace and articles are not normalised, and the zone in the quest text is never cross-checked. `Go to the Kraken Gate in Triton Avenue` parses to
  the target `the Kraken Gate`, which finds no anchor (the entry is `Kraken Gate`); `Talk to Susie Gryphonbane in Olde Town` still resolves to her Triton Avenue anchor.

---

## Audit note (2026-11-19, Claude)

This document describes **intended** quest-interpreter behaviour. The implementation in
`crates/maelstrom-core/src/quest_interpreter.rs` covers text classification into the six archetypes and a dialogue state
machine only. There is no jitter or 250-650 ms timing, no deck equipping, kill or drop tracking, realm hopping or sigil
countdown handling, `QuestAcceptPolicy` currently has no effect, and the module is not called by any runtime path. The
`Ctrl+Shift+Q` key is a documented default ("select nearest quest"; this note previously called it unverified, which was wrong: see section 3). Details: `docs/coordination/results/W101-QUEST-INTERPRETER-CORE-02.md`.
