# CORE-CARD-AUDIT-01 — callable operations and card contracts

Date: 2026-11-11. Workstream: CORE-DELIVERY. Owner: card_audit.
Status: static audit complete; integration work remains. Owned change: this report only.

## Conclusion

Maelstrom has a useful shared execution spine already: pure decisions, versioned data, trusted adapters, one scheduler/control owner, durable receipts, explicit outcomes, and recovery holds. It does **not** yet expose every implemented specialist through one capability/action catalog. There are 19 entries in the current card collection: saved-session replay and selected quest conversations have deck adapters; the other 17 entries are preparation-only. A card image or setting is not an executable operation.

The next useful work is integration and contract alignment, not another engine or language rewrite. Connect existing primitives through the existing owner, publish their actual availability and evidence, and make the same contracts discoverable to the UI, CLI and MCP. Resource-sensitive switching cannot work until real resource facts exist.

This audit read source and existing documentation. It did not execute tests, build, install, connect to W101, acquire desktop input, or run models. Existing test files are coverage locations, not newly passing results. Installation and live outcomes below are attributed to existing reports, not independently requalified here.

## Callable surfaces today

Paths below are repository-relative. Method names are included because line numbers will move as the shared checkout changes.

| Surface | Actual entry point | What callers receive | Important limit |
|---|---|---|---|
| Native card UI | `ProjectMaelstrom/ProjectMaelstrom/MaelstromHomeForm.cs`, `JobDeckForm`, `JobCardView` | Collection, flip-to-edit card settings, deal/duplicate, limits, conditions, include/exclude filter, import/export, play one/deck, stop, connection | Only registered adapters dispatch. One filter is directly editable; additional imported filters are preserved. Conditions use typed text, not a guided fact picker. |
| Native deck API | `Utilities/JobDeckHost.cs: Handle`, `Program.cs: RunDeckCommand`; `--deck-host`, `--deck-api` | `catalog`, `state`, `snapshot`, `replace`, `deal`, `preview`, `step`, `start`, `pause`, `stop`, `reset`, `windows`, `connect`, `disconnect`, `observe` | Local current-user named pipe; separate explicit client/UI-tree/action opt-ins; exact window/process/start identity. Importing or previewing grants no authority. |
| Main headless CLI | `Utilities/MaelstromCli.cs: Commands`, `Parse`, `RunAsync` | `help`, `windows`, `status`, `wizwalker-check`, `wizwalker-watch`, `ui-tree-watch`, `quest-conversation`, `mcp` | Small supported command family, not an index of every helper in `Program.cs`. No generic raw input or arbitrary script execution. No `--cli update` command in this list. |
| Additional executable switches | `Program.cs` | Saved routine/module replay and review, Dance evidence review/calibration/trials, Dodge replay/training/verification, capture/Observe checks, previews | Useful but distributed, often developer-oriented and positional. Their existence does not put them in the deck or MCP. An invocation needs its own mode/qualification documentation. |
| MCP | `tools/maelstrom_mcp_server.py: MaelstromMcpServer`, `handle_tool_call`; `tools/maelstrom_deck_client.py` | Pack discovery, policy inspection, graph validation, combat arithmetic/planning, Potion Motion solving, macro compilation; deck snapshot/state/deal/step/catalog/preview/replace/start/pause/stop/reset | Deck tools delegate to the native host. MCP deliberately does not expose connect/observe/disconnect or grant opt-ins. Some pure tools have independent schemas; graph validation currently drifts from the native contract. |
| Optional HTTP server | `MaelstromBot.Server/Program.cs: DeckProxy` | `/session/snapshot`, `/deck`, `/deck/catalog`, POST deck commands | Authenticated proxy to native named pipe; mutations require admin role and start/step require native idempotency validation. Owner unavailable returns 503 rather than fabricated success. The server links `JobDeckWire.cs`, not a second deck engine. |
| Shared modules | `Utilities/Modules/ModuleCatalog.cs`, `ModuleOperation.cs` | Compile/read/save/replay navigation, sigil, menu and auto-idle bundles; scheduled saved-screen review | Production source search found a caller of `SubmitOffline`, but no production caller of `SubmitOwned`. The trusted owned path exists as groundwork; it is not a generic playable card API yet. |
| Game-pack action registry | `tools/game_packs.py: validate_pack`, `propose_for_observation`, `save_pack` | Version-2 pack action IDs resolve to bounded keys/durations; state rules propose actions; immutable version saves | Separate contract from native `ModuleAction` and `JobRoutineInfo`; proposals do not authorize dispatch. |
| Workspaces | `GameWorkspaceForm`, `DodgeWorkspaceForm`, `Utilities/GameWorkspaceCatalog.cs`, `GameWorkspaceStore.cs` | Generic selected-window Observe workspace, explicit Dodge workspace, named saved preferences, W101-specific tools/deck | Game-specific routing is partly hard-coded. The deck factory creates a Wizard101 deck. A pack and a workspace are not yet one pluggable game descriptor controlling all surfaces. |

`JobDeckRoutines.Create` is the authoritative current deck catalog. The two dispatch paths are:

1. `review.routine` → `PrepareReplay` → `RoutineReplay.Run`. It pins and copies bounded saved graph/session/events inputs. Completion proves saved replay completion only.
2. `w101.questing` → `JobDeckHost.PrepareQuest` → `W101QuestConversationOperation.Prepare`. It can autofill a selection from a fresh open conversation, apply scope filters, advance bounded pages and verify progress. Generic objective parsing is broader than executable adapters: walking, combat, collection and full quest lifecycle remain unconnected here.

The 17 preparation cards are four pet games, eight fairgrounds minigames, Auto-Idle, Potions, Inventory, Farming and Battle. `Planned` sets `Available=false` and its prepare delegate throws. Standalone Dance teacher evidence therefore must not be described as a working Dance deck visit. `W101CombatTurnOperation.Prepare` and `W101CombatBattleOperation.Prepare` are composable prepared-job building blocks, but the current deck factory does not register them.

## Restrictions that are implemented

| Contract | Source enforcement | Effect |
|---|---|---|
| Configuration bounds | `JobDeckRules.Validate` in `Utilities/JobDeck.cs` | Schema 1; at most 64 cards; deck attempts 1–1000; duration 1–3600 seconds; card copies 1–100, attempts at least copies and at most 100, cooldown 1–3600 seconds, bounded settings/conditions/filters. |
| Trusted catalog and game identity | `JobDeckRules.Validate`, `JobDeckRoutines.Create` | Unknown routines/settings/options rejected; routine must match game or be game-neutral. Imported files cannot add executable delegates. |
| Conditions and scope | `JobDeckRules.FactBlock` | `eq/gte/lte`, finite numeric thresholds, exact include/exclude lists with exclude precedence; absent facts wait. For live work, target/game match and facts must be at most two seconds old and not future-dated. |
| Admission revalidation | `JobDeckService.Advance` | Reserve attempt durably before prepare, record card/deck revision/facts, clamp operation budget to deck deadline; recheck facts, eligibility and selected target at actual execution. |
| Run lifecycle | `ReserveStart`, `CompleteReservedStart`, `Advance`, `FinishAsync`, `Reset` | Durable request deduplication, bounded history, fixed deadline, explicit counter reset, successful receipts alone count completed copies. Editing/removing/readding cards does not erase attempt history. Non-success stops continuous play. |
| Single owner | `OperationScheduler.Submit`, `PumpAsync`; `GameInputLease` | One queue/worker per data root; capacity 32; source pins verified at execution; trusted callback receives the shared input lease for ordinary live work. Cancellation retains ownership until worker cleanup finishes. |
| Durable evidence and recovery | `OperationJournal`, `OperationScheduler`, `JobDeckService.Save`, `FinishAsync` | Interrupted work is not automatically resumed. Failed state persistence blocks dispatch; unknown receipt outcome remains unknown; cleanup uncertainty cannot become success or clear the recovery latch. |
| UI/client selection | `JobDeckHost.Connect`, `PrepareQuest`; `MaelstromCli.Parse` | Exact HWND/PID/start identity, optional reviewed reader, separate action opt-in. Fresh game context is revalidated by the operation. |
| Module contracts | `OperationModules.ValidateSpec/Define`, `ModuleCatalog.Compile`, `RoutineGraphs.Validate`, `RoutineRunner` | Known module kinds, bounded actions, required named controls, target/geometry/context bindings, fresh observations, receipt plus newer observed outcome. Saved files cannot assert live qualification. |
| Transport | `JobDeckWire`, `JobDeckHost.ServeAsync` | Current-user named pipe, 256 KiB envelope, bounded connections/read times. HTTP is a proxy; MCP delegates to this owner. |

The scheduler validates lifecycle and evidence references, not gameplay semantics by itself. Each trusted specialist still must establish that its own outcome was observed. This is the correct separation; a generic `Completed` receipt should not be treated as proof of every possible downstream goal.

## Gaps and prioritized follow-up

### 1. Align MCP routine validation with the native runtime

**Concrete drift:** `tools/maelstrom_mcp_server.py:_validate_routine_graph` accepts schema 1 or 2, requires `routine_id` or `name`, and treats missing target nodes as warnings. Native `Utilities/RoutineGraph.cs: Read/Validate` requires schema 1 and fields `id`, `game_id`, `start_node`, finite time/action limits, defined completion and reachable nodes; unknown and duplicate JSON fields are rejected. The two validators do not validate the same artifact.

Impact: MCP can say a graph is valid that the actual runtime cannot read, and can reject a native-shaped graph. This does not bypass native dispatch checks, but it misleads builders and breaks the intended transformer boundary.

Small fix: route authoritative validation to a bounded native offline command, or replace the Python validator with explicit parity and common positive/negative fixtures. Keep input validation separate from permission to run. Add conformance fixtures for unknown schema/fields, dangling edges, missing completion, invalid budgets, duplicate properties and valid current native graphs.

### 2. Publish typed capabilities and facts, not just card labels

`JobRoutineInfo` is a useful beginning, but it describes a small deck catalog. `JobCondition` names are checked syntactically by `CheckFact`; there is no authoritative fact definition containing type, units, producer, freshness, supported game/adapter and evidence. A misspelling such as an unproduced resource fact can be saved and wait forever. The UI cannot explain unsupported versus temporarily unknown using a typed source.

Current `JobDeckHost.Handle("snapshot")` correctly lists unavailable readings: health, mana, energy, potions, inventory, area, quest.type and quest.id. `UpdateFacts` supplies stable UI booleans, objective text and parsed `quest.action/area/target`. `quest.area` means the objective's destination, not the wizard's current zone.

Small fix: one versioned capability/fact manifest with stable IDs, input/output contracts, constraints, producer, supported surfaces, qualification and evidence links. Validate configured conditions against that manifest, retaining an explicit unknown state at runtime. Populate dropdowns and API help from it. Keep current-area and objective-area distinct. Do not invent HP, energy, funds or bag capacity defaults to enable cards.

Also distinguish catalog availability from connection/readiness. Currently Quest is registered when the host supplies its callback; the separate quest-action opt-in can still be missing when prepare is attempted. Eligibility should explain that prerequisite before consuming a run attempt.

### 3. Connect bounded observation refresh to waiting decks

`JobDeckService.PumpAsync` scans cached observations once per second. `JobDeckHost.Observe` runs on explicit request or a start needing fresh quest context; `UpdateFacts` also runs within a quest scope check. The ordinary waiting loop does not schedule a fresh observation itself. After two seconds, cached facts become unusable. Conditions becoming true externally therefore do not automatically wake a waiting deck unless something updates facts.

Small fix: a lightweight, bounded observation refresh service coordinated by the same owner. Run only requested producers/frequencies, avoid overlap with active operations/hooks, stop/drain on cancellation, and publish observation timestamps and causes. Test a condition that becomes true after initial observation, target loss, stale data, stop during refresh, and a refresh unable to finish before the deck deadline. Do not add a second scheduler or bypass hook cleanup for polling.

### 4. Finish one module-to-card vertical integration

`ModuleCatalog` compiles navigation, sigil, menu and Auto-Idle into `RoutineGraph`. `W101SharedOperations` supplies vocabulary/data bindings and native-Idle menu transitions. `ModuleOwnedInput` contains target/geometry/ownership/freshness/loading/control checks. These are reusable parts, but `ModuleOperation.SubmitOwned` has no production call site in the inspected native source. The generic path is not yet an automatic between-card Idle system.

Small fix: adapt one narrow, already recognized operation—interaction or native Idle—through a `PreparedDeckJob` under the existing deck/scheduler. Do not nest a second submission inside an executing card. Give it observation and outcome requirements; qualify entry, exit, loading, user takeover and cleanup. Then reuse that integration for sigils/menus and route steps. Native Idle entry and safe resume are distinct capabilities.

### 5. Make transforms explicit across action vocabularies

The Python pack registry uses key/duration actions (schema 2, up to 128 entries, pulses 1–1000 ms); native `ModuleAction` adds kind/control ID and limits pulses to 150 ms; native `RoutineGraph` refers to trusted action IDs and separate receipts. The deck catalog points to trusted whole operations. Different limits serve different layers; silently assuming these records are interchangeable would be a bug.

Small fix: documented adapters for pack action → qualified binding, observation → normalized fact, card → prepared operation, and receipt → verified game outcome. Each adapter declares supported input/output schema versions and any narrowing (for example a 500 ms pack action must be rejected or deliberately transformed before admission to a 150 ms module contract). Preserve the existing internal contracts until a consumer actually needs an adapter.

### 6. Broaden resource and task switching only after producers exist

Current exact filters and limits can constrain known facts. They do not yet implement inventory-full → bazaar → protected-item sale → snack purchase → pets → quest resume, potion policy, CrownShop lockout throughout such a workflow, battle farming, team coordination or autonomous navigation. Preparation settings are not evidence of these operations.

Build each as a bounded specialist with prerequisite facts, configuration, explicit forbidden actions, actual resource accounting and an observed postcondition. Keep expensive/consumable decisions opt-in. Add resource facts and outcome schemas before making those cards available. A generic quest parser should choose among qualified capabilities; it must not turn text recognition into unrestricted quest execution.

### 7. Add compatibility policy before independent module upgrades

There is useful version discipline now: deck schema 1; routine schema 1; module schema 1/version `1.0.0`; pack schemas 1/2 with immutable saved versions; operation IDs/versions, hashed inputs, and journal/receipt schema checks. This is fail-closed parsing, not a general compatibility/migration service. `QuestMode` preserves one older selection-file convention, but there is no universal migrate/rollback registry across all these artifacts. The API response has schemaVersion; requests do not negotiate a protocol version/capabilities.

Small fix: document a compatibility table, explicit supported version pairs, immutable original preservation, dry-run migration and failure behavior. Let readers report unsupported versus corrupt files clearly. Add contract tests for prior supported artifacts and separately promote new adapter versions after replay/live gates. Do not promise arbitrary hot-swapping merely because a `Version` string exists.

## Architecture to preserve

Use the current layers as the foundation:

`capture/client adapter → normalized observations → game-pack facts/context → condition evaluation → card/graph decision → prepared operation → scheduler/control owner → target input adapter → observed outcome → receipt/experiment evidence`

The UI, CLI, MCP and HTTP interface should call this spine. Source libraries supply readers, recognizers, planners or transforms behind those interfaces; they should not each own their own live loop. The fast path remains rules and small recognizers. Models/research/training belong outside it unless a bounded specialist explicitly needs them.

Useful existing separation to retain:

- `RoutineRunner` is pure and cannot capture, sleep or send input; an accepted receipt alone does not advance its graph.
- `ModuleCatalog` loads data and compiles known modules, never arbitrary plugins or scripts.
- `OperationRequestSnapshot.Freeze` protects admission pins from caller mutation.
- `GenericMacroDeck.cs` is excluded from the shipped native project and linked only into tests; it is not a competing live controller.
- Legacy `StandardJobCards` proposals explicitly defer dispatch to the shared JobDeck registry.
- `GameWorkspaceStore` saves preferences, not a stale HWND or permission to control a process.

## Evidence and validation plan

Existing source suites to extend, rather than adding a second test framework:

- Deck/rules/owner: `ProjectMaelstrom/ProjectMaelstrom.Tests/JobDeckTests.cs`, `JobDeckHostTests.cs`, `JobDeckRecoveryTests.cs`.
- Scheduler/cancellation/journal/recovery: `OperationSchedulerTests.cs`, `OperationSchedulerCancellationTests.cs`, `OperationSchedulerJournalTests.cs`, `OperationSchedulerRecoveryTests.cs`.
- Pure transition and shared modules: `RoutineGraphTests.cs`, `RoutineReplayTests.cs`, `ModuleRunnerTests.cs`, `ModuleOperationTests.cs`, `ModuleOwnedInputTests.cs`, `ModuleLoadingGateTests.cs`, and navigation/sigil/menu/AutoIdle tests.
- Contract bridges: `tools/tests/test_maelstrom_mcp_server.py`, `test_maelstrom_deck_client.py`, `test_action_registry.py`, `test_macro_pipeline.py`.
- Combat prepared-operation contracts: `W101CombatTurnOperationTests.cs`, `W101CombatBattleOperationTests.cs`, `W101CombatRuntimeTests.cs`, `W101CombatInputTests.cs`.

Qualification must remain separate:

1. **Static/source:** this report establishes code paths and missing connections.
2. **Synthetic/offline:** native/Python tests and replay can establish contracts and behavior against saved inputs; no gameplay success follows automatically.
3. **Installed:** `docs/JOB_DECK.md` references `CORE-DECK-RELEASE-02.md` for the installed card/deck checkpoint. That does not establish that all current working-tree combat additions are installed.
4. **Live:** selected-conversation and standalone Dance historical outcomes remain narrowly scoped. This audit did not independently reproduce them or establish autonomous combat, navigation, resource management or a full Dance deck visit.

The coordinator owns the canonical roadmap, capability inventory and board update. This report provides the source audit inputs; it does not mark W101, Dodge or Maelstrom complete.
