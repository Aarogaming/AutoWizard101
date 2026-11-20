# Maelstrom progress — November 12, 2026

Working automation infrastructure and several live-qualified routines exist;
general unattended W101 gameplay does not. This snapshot reconciles the board
with recent travel evidence. Test counts are not completion percentages.

| Front | Current evidence | Remaining gate |
| --- | --- | --- |
| Runtime | Shared scheduler/input owner, conditions, run limits, target validation, receipts, replay and cleanup. CLI wake/turn/Idle and travel used them live. | Recovery and focus transitions across every routine. |
| Cards / app | Workspaces, card/deck UI, settings, local API/MCP, portable/installer groundwork; source Travel card with separate opt-in. | Installed build trails source; integrated release and full visual authoring remain. |
| Capture / teaching | Recording, input/audio evidence, labeling, templates and experiment tools. | No claim of universally sustained 60 fps or a finished generic video-to-player trainer. |
| W101 adapter | Optional reviewed UI-tree/pose hooks, loading/prompts/dialogue/Idle observations and clean disconnects. | New builds/fields need qualification; no universal automatic offset adaptation. |
| Idle / transitions | Shared finalizer, countdown, takeover checks and live Idle returns; Spiral Map-under-Idle defect corrected. | Focus-resumable job bodies and broader interruption cases. |
| World travel | One configured Zafaria card selected/verified its world, travelled, checked Home availability, returned to house origin and verified Idle. | Live multi-page selection, active cooldown waiting and independent zone identity. Public-gate Home/profile routing pending. |
| Walking / mapping | Pose/heading, movement feedback, waypoints, loading-stop and exact-prompt primitives; some house landmarks/crossings. | Fluent paths, collisions, enemy avoidance, learned maps and arbitrary start/destination routes. |
| Wizard switching | Selection/loaded-wizard evidence and per-wizard profile groundwork. | Full unattended switch, correct house and Idle card. |
| Pet Dance | Native player, audio/visual work, perfect-game evidence and transition components. | Repeatable complete visits across levels/UI scales, entry/results/no-snack exit and recovery. |
| Conjuration / Potion Motion | Recognition/collection groundwork and an offline Potion Motion planner. | Independent game datasets, legal moves and verified outcomes. |
| Other minigames / pets | Research, legacy inventory and some observers/fixtures for Diego, Sorcery Stones, Choo-Choo Zoo, Doodle Doug, Skull Riders, Hot Shots, Cannon, Maze, Drop, Grumpy Gobblers and Ninja Pig. | Qualified full players not present for these tracks. |
| Questing | Generic intent/context and conversation card; Quinn turn-in/follow-up evidence. | Navigation, battle, collection and objective verification joined into one generic loop. |
| Combat / farming | Field/duel candidates, geometry, bounded input/planning source and legacy references. | Real selected-card/cast/victory qualification. Mob/boss farming and auto-team not qualified. |
| Resources | Snack preference/recommendation and energy-gate components; gardening/Bazaar references and partial code. | Verified inventory/resources and completed protected selling, feeding, gardening/restocking workflows. |
| Dodge / Reflex | Local adapters, isolated Dodge experience/evaluation/learning, Reflex timing fixtures. | Dodge's required fresh-game average of 50 is not established. |
| Knowledge / legacy / assets | Inventory, research records, first importer and reusable candidates. | Broader sourced exports, versioned mechanics, bounded WAD validation; imported does not mean integrated. |
| Rust | Five-crate workspace and utility translations; audit complete; codec compatibility and Dance state-machine repairs pass 11 core and 8 Dance tests. | Broader contract parity, resource bounds and an optional host bridge before substitution. No production Rust controller connected. |

## Evidence and cooldown refinement

Latest native checkpoint:2478 passing tests; Python UI-reader checkpoint:64.
Configured travel run:c87172cb2a0f47319f004456c3bd7be0; see W101-TRAVEL-30.
Rust audit is RUST-PORT-AUDIT-30; implementation receipts belong to RUST-PARITY-31.
Native tests do not prove Rust parity, every live routine, or installed contents.

Owner reports Home and the relative-world Commons port usually have a 30-second
cooldown, started only by using those ports. This remains owner knowledge pending
Commons qualification. Do not assume World Gate travel starts it or that both
ports share identical readiness. Home currently observes GoHomeButton and
its txtGoHomeTimer: actual traces show the button stays enabled during cooldown,
so enabled alone is insufficient. Unknown is not ready. The configurable 60-second
wait budget is a limit, not the game's cooldown duration. Commons action/timer
mapping is not implemented by the current Home card.

## Guided next increments

1. Qualify travel across menu pages and active cooldown waits; add zone evidence.
2. Finish one repeatable local route, then join walking to interaction/menu steps.
3. Complete no-snack Dance visits and controlled repeats; continue Conjuration offline.
4. Qualify one battle and objective update, then connect the generic Quest card.
5. Package a coherent checkpoint with installed CLI/API/recovery verification.
6. In parallel, establish Rust codec/Dance parity and a buildable baseline; integrate
   one proven pure function through the existing owner before broader substitution.

Keep independent Rust workers off live input. Preserve current contributor work,
use cached dependencies, serialize builds and measure any proposed performance gain.

Rust increment: scoped formatting passed; Clippy exited successfully with warnings in unowned files. The shared runner now compiles for targeted tests, superseding the earlier audit blocker. No host substitution or installation occurred. See RUST-PARITY-31 for compatibility limits and the preserved concurrent Cargo.lock change.
