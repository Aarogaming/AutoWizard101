# W101 automation readiness

Updated 2026-11-11 after W101-MODULE-INTEGRATION-05. This is a task-level snapshot
of the roadmap and board, not permission to execute cards or a completion estimate.
No percentage or calendar estimate is justified by the present qualification data.

The framework is ahead of the individual players: scheduler, single input owner,
cards, conditions, reports, recording, packs and replay exist. Many selectable
cards are still preparation-only. Imported legacy code is material to adapt and
test, not evidence that its operation works in today's integrated application.

Evidence levels below deliberately distinguish a narrow live demonstration from
a reliable, unattended, complete workflow. New source is not automatically installed.

| Auto-task | Current position | Gate to a usable bounded routine |
| --- | --- | --- |
| Identify selected game/window | Working CLI/workspace foundation | Keep exact target identity through every session |
| Observe loading, Idle, menus/dialogue | Narrow live evidence | Bind specific controls and outcomes, qualify more states/layouts |
| Combined UI-tree + visual observation | Live Idle probe completed | Sustained latency/freshness checks; action qualification remains separate |
| Record/review/label and replay | Existing shared tools | Per-task training examples and held-out outcome checks |
| Open options / interact with X | Shared bindings, concrete bounded key transport, offline tests | Real selected context, observed input response, host registration |
| Enter native Idle | Existing Dance cleanup and shared bindings | Fresh complete-path qualification, shared card connection |
| Wake from Idle | Not a qualified reusable operation | Explicit wake action, observed exit and restored context |
| Idle automatically between cards | Not connected as a complete policy | Enter/wake pair, paused deck state, preserve dialogue and takeover |
| Optional spin/step/routine Auto-Idle | Prior optional maintenance work | Reconcile modes with shared deck owner and qualified context |
| Walk one known route | Navigation primitives and recorded route material | Current localization, movement feedback, stuck recovery and arrival |
| Cross zones / resume after loading | Real loading observations, shared gates | Connect movement and destination identity across transition |
| Navigate arbitrary objectives | Framework/research stage | Route graph, current location, barriers, landmark/arrival evidence |
| Avoid enemies while travelling | Pure Rust Nav2 3-layer costmap implemented as library code with no caller outside its tests (`crates/maelstrom-core/src/costmap.rs`, `W101-COSTMAP-01`); not a qualified W101 routine | Live threat tracking, zone asset ingestion, reachable paths and battle interruption |
| Use Home/Commons/marked travel | Research/configuration material | Explicit allowed action, destination verification, resource constraints |
| Complete one selected conversation | Narrow live quest-progression evidence | Broader conversation/offer variants and generic card qualification |
| Accept/turn in quests routinely | Selected conversation machinery | Distinguish offers, continuation, completion and objective changes |
| Select/change/seek quests | Partial objective/intent reading | Quest-list identity, filters, reliable selection and acceptance policy |
| Destination/interaction quest | Quest parser + navigation/interaction pieces | Integrated route → exact target → verified objective progress |
| Defeat/collect quest | Not autonomous | Qualified battle + navigation + collection/progress verification |
| General multi-quest autopilot | Not ready | Above quest types, interruptions, resources and recovery |
| One basic battle | Reader/planner/turn/battle/input code, offline tests | Real card/target geometry, castability, selected state and victory evidence |
| Mob farming | Not a qualified integrated loop | Battle, approach/re-entry, health/mana, loot and limits |
| Boss/dungeon farming | Further than normal combat | Instance navigation, boss-specific behavior, resets and recovery |
| Team assistance / multi-client coordination | No qualified complete routine | Per-client ownership, identities, synchronization and shared outcome policy |
| Pet Dance gameplay | Real PERFECT game evidence | Repeatability across levels/layouts and bounded run policy |
| Complete repeat Pet Dance visits | Integration pending | Entry, level, play, result, no-feed exit, Idle and Energy gates |
| Conjuration Concentration | Recognition/memory work and collection tooling | Actual board/preview/flip/outcome corpus and live clicking |
| Potion Motion | Offline planner | Live board recognition, rule/animation qualification and moves |
| Dueling Diego | Recorded material and prior experiments | Qualified action cues/timing, reactive policy and live outcomes |
| Sorcery Stones | Backlog | Grid/special-state recognition and swap/cascade policy |
| Choo-Choo Zoo | Backlog | Tile connectivity, moving-state recognition and rotations |
| Doodle Doug | Backlog | Maze/player/enemy/bomb perception and hazard routing |
| Skull Riders | Backlog | Flight/motion perception and collision avoidance |
| Hot Shots | Backlog | Aim/charge/target calibration and outcome evaluation |
| Pet Cannon | Backlog | Aim/power/target recognition and feedback |
| Pet Maze | Backlog | Maze localization and goal/obstacle routing |
| Pet Drop | Backlog | Board/state data, verified rules and solver |
| Grumpy Gobblers | Backlog | Aim/projectile/target observations and policy |
| Way of the Ninja Pig | Backlog | Obstacle/phase recognition and timing policy |
| Snack suggestions | Preferences/recommendation foundations | Current pet attributes and inventory observations |
| Feed selected snacks | No qualified feeding loop | Allow/deny rules, exact item/quantity, outcome and consumption checks |
| Energy-aware pet training | Pure gate exists | Verified current Energy, costs, reserves and completion accounting |
| Reagent collection | Legacy material, interaction/navigation pieces | Locate/approach/collect/disappearance or inventory evidence |
| Gardening/home farming | Legacy material and proposed one-plant slice | Needs recognition, spell/resource checks and verified plant outcome |
| Bazaar search | Legacy search material | Bounded current item/price reading and no-match correctness |
| Sell/restock | Not a qualified purchasing/selling loop | Protected items, quantities, gold, prices and transactional confirmation |
| Use potions at low health/mana | Planned maintenance card | Reliable vitals/stock, thresholds and observed replenishment |
| Refill potions through minigames | Composite future routine | Qualified game completion/reward plus potion inventory evidence |
| Gear/deck changes | No qualified integrated routine | Exact inventory/deck identity, configuration and verification |
| Bank/shared-bank management | No qualified integrated routine | Protected-item policy, capacity and verified transfers |
| Crafting/fishing/pet hatching/racing | Future candidates, not claimed implemented | Dedicated rules, observations, resource restrictions and tests |
| Sign-in/reconnect/character switching | Credential setup source; complete automation unqualified | Launcher/login/character states, explicit policy and clean recovery |
| Quest → sell → restock → pets → resume | Scheduler conditions exist; gameplay loop does not | Reliable resource producers and every constituent routine above |
| Long unattended operation | Not ready | Repeatable components, interruption/recovery tests, resource budgets and honest stop reports |

## Build order

1. Qualify a reusable active/Idle/menu/interaction cycle with observed outcomes.
2. Qualify one short route with arrival and stuck handling; the remote user must
   not be required to provide movement for each test.
3. Connect a complete Pet Dance visit, including no-feed exit and Energy policy;
   qualify repeated visits. Collect Conjuration examples in a separate lane.
4. Qualify one ordinary battle, then controlled defeat/collect quests.
5. Add vitals, stock and inventory producers; then protected selling/restocking.
6. Compose varied quests and farming loops, expanding geography and game-specific
   exceptions only after each smaller routine passes repeated tests.

Navigation is the largest shared gameplay dependency for a remote operator.
Resource observations are the largest shared dependency for routine switching.
Neither is replaced by adding more cards or an additional general model.

Sources: [roadmap](ROADMAP.md), [board](coordination/workstreams.json),
[latest source/live probe](coordination/results/W101-MODULE-INTEGRATION-05.md),
[combat checkpoint](coordination/results/W101-COMBAT-CONNECT-06.md),
[card documentation](JOB_DECK.md). Dated older audits retain their original counts
and should not be read as current catalog totals.
