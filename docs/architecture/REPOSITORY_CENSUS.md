# Repository census — Phase 1 of ontology normalization

Date: 2026-11-18 · owner: root · claim: `ONTOLOGY-CENSUS-01`
Companion to [ONTOLOGY_NORMALIZATION.md](ONTOLOGY_NORMALIZATION.md) (the six
primitives and the Facts != Authority != Actions invariant).

Every path below was existence-checked before listing (inventory produced by a
read-only explore pass, then verified programmatically against the checkout).
Category: C# core/app, Python tool, Rust crate, Node integration, Docs, Data.
Primitives reference the six defined in the companion document.

| Subsystem | Path (repo-relative) | Purpose (<=12 words) | Category | Primitives | Evidence |
|---|---|---|---|---|---|
| Facts store | ProjectMaelstrom/ProjectMaelstrom/Utilities/W101FactsStore.cs | Bounded fact registry with TTL, observed/candidate/unknown states | C# core | State, Trust | ProjectMaelstrom/ProjectMaelstrom.Tests/W101FactsStoreTests.cs; docs/coordination/results/W101-BACKGROUND-API-02.md |
| Activity flags | ProjectMaelstrom/ProjectMaelstrom/Utilities/W101ActivityFlags.cs | Gates background fact publication on accepted observations | C# core | Constraint, Trust | ProjectMaelstrom/ProjectMaelstrom.Tests/W101ActivityFlagsTests.cs; docs/coordination/results/W101-ACTIVITY-FLAGS-01.md |
| Facts mapper & session | ProjectMaelstrom/ProjectMaelstrom/Utilities/W101FactsMapper.cs | Maps UI-tree results into facts; hosts W101FactsSession orchestrator | C# core | State, Perspective | ProjectMaelstrom/ProjectMaelstrom.Tests/W101FactsMapperTests.cs |
| Facts observation cycle | ProjectMaelstrom/ProjectMaelstrom/Utilities/W101FactsCycle.cs | Pinned-observer five-second cycles through shared scheduler | C# core | Transition, State | ProjectMaelstrom/ProjectMaelstrom.Tests/W101FactsCycleTests.cs; docs/coordination/results/W101-CYCLE-INTEGRATION-06.md |
| Background facts reader | ProjectMaelstrom/ProjectMaelstrom/Utilities/W101BackgroundReader.cs | Identity-validated resident reader; recovery-hold publication block | C# core | State, Constraint | ProjectMaelstrom/ProjectMaelstrom.Tests/W101BackgroundReaderTests.cs |
| Facts CLI watch | ProjectMaelstrom/ProjectMaelstrom/Utilities/MaelstromCli.cs | `facts-watch`: bounded exact-identity candidate-only observation | C# CLI | Trust, Constraint | ProjectMaelstrom/ProjectMaelstrom.Tests/MaelstromCliTests.cs; docs/coordination/results/W101-BACKGROUND-API-02.md |
| Facts pipe/MCP publication | ProjectMaelstrom/ProjectMaelstrom/Utilities/JobDeckHost.cs; tools/maelstrom_mcp_server.py | Read-only facts over native pipe and MCP; no injection surface | Cross (C#/Python) | Trust, Constraint | docs/coordination/results/W101-BACKGROUND-API-02.md; tools/tests/test_maelstrom_mcp_server.py |
| Manual capture host | ProjectMaelstrom/ProjectMaelstrom/Utilities/ManualCaptureHost.cs | Owns bounded manual window-capture lifetime and disposal | C# core | State, Constraint | docs/coordination/tasks/MANUAL-CAPTURE-CONTROLLER-01.md; docs/coordination/results/MANUAL-CAPTURE-CONTROLLER-02.md |
| Manual capture controller | ProjectMaelstrom/ProjectMaelstrom/Utilities/ManualCaptureController.cs | Coordinates capture commands, cancellation and bounded samples | C# core | Transition, Constraint | ProjectMaelstrom/ProjectMaelstrom.Tests/ManualCaptureControllerTests.cs; docs/coordination/results/MANUAL-CAPTURE-CONTROLLER-02.md |
| Capture timeline & session view | ProjectMaelstrom/ProjectMaelstrom/Utilities/ManualCaptureTimeline.cs; ProjectMaelstrom/ProjectMaelstrom/SessionTimelineForm.cs | Fixed-capacity sample ring plus session timeline display | C# core/UI | State, Constraint | ProjectMaelstrom/ProjectMaelstrom.Tests/ManualCaptureTimelineTests.cs |
| Overlay HUD | ProjectMaelstrom/ProjectMaelstrom/OverlayHudForm.cs | On-screen status and controls above the game window | C# UI | Perspective | docs/coordination/results/W101-OVERLAY-CONTROLS-35.md |
| Bounded window capture frames | ProjectMaelstrom/ProjectMaelstrom/Utilities/BoundedWindowCapture.cs; ProjectMaelstrom/ProjectMaelstrom/Utilities/WindowObserver.cs | Frame reads with age acceptance, disposal, cancellation (CapturedWindowFrame) | C# core | State, Constraint | ProjectMaelstrom/ProjectMaelstrom.Tests/BoundedWindowCaptureTests.cs |
| Operation scheduler | ProjectMaelstrom/ProjectMaelstrom/Utilities/OperationScheduler.cs | Single serialized operation queue with admission and receipts | C# core | Transition, Constraint | ProjectMaelstrom/ProjectMaelstrom.Tests/OperationSchedulerTests.cs; docs/SCHEDULER_REUSE_INTEGRATION_20261110.md |
| Scheduler recovery, journal, cancellation | ProjectMaelstrom/ProjectMaelstrom/Utilities/OperationScheduler.cs (journal/recovery paths) | Durable recovery journal, drain and cancellation semantics | C# core | Transition, Constraint | ProjectMaelstrom/ProjectMaelstrom.Tests/OperationSchedulerRecoveryTests.cs; ProjectMaelstrom/ProjectMaelstrom.Tests/OperationJournalTests.cs |
| Input ownership lease | ProjectMaelstrom/ProjectMaelstrom/Utilities/GameInputLease.cs | Single-owner input lease; competing acquisition always fails | C# core | Constraint, Trust | ProjectMaelstrom/ProjectMaelstrom.Tests/GameInputRecoveryTests.cs; ProjectMaelstrom/ProjectMaelstrom.Tests/LegacyInputOwnershipTests.cs |
| Input recovery holds | ProjectMaelstrom/ProjectMaelstrom/Utilities/GameInputRecovery.cs | Dirty-state recovery gate blocking new lease until cleared | C# core | Constraint, Transition | ProjectMaelstrom/ProjectMaelstrom.Tests/GameInputRecoveryTests.cs; docs/INPUT_RECOVERY_FLOW_20261110.md |
| Python shared automation lease | tools/wizard101_hub.py | Cross-process automation lease mirroring native ownership rules | Python tool | Constraint, Trust | tools/tests/test_shared_input_lease.py |
| Focus resume policy | ProjectMaelstrom/ProjectMaelstrom/Utilities/W101FeedbackVisit.cs | Resume-after-focus never bypasses recovery or cleanup latches | C# core | Constraint, Transition | ProjectMaelstrom/ProjectMaelstrom.Tests/W101FocusResumeTests.cs; docs/coordination/results/INPUT-FOCUS-69.md |
| Operation focus budget | ProjectMaelstrom/ProjectMaelstrom/Utilities/OperationFocusBudget.cs | Per-operation focus/time budget with disposal accounting | C# core | Constraint, State | ProjectMaelstrom/ProjectMaelstrom.Tests/OperationFocusBudgetTests.cs; docs/coordination/results/INPUT-BUDGET-70.md |
| Path follower | ProjectMaelstrom/ProjectMaelstrom/Utilities/PathFollower.cs | Follows waypoint paths with bounded per-step input | C# core | Transition, Relationship | ProjectMaelstrom/ProjectMaelstrom.Tests/PathFollowerTests.cs |
| Waypoint feedback | ProjectMaelstrom/ProjectMaelstrom/Utilities/WaypointFeedback.cs | Heading/distance feedback during waypoint traversal | C# core | Perspective, Relationship | ProjectMaelstrom/ProjectMaelstrom.Tests/WaypointFeedbackTests.cs; docs/coordination/results/W101-NAV-FEEDBACK-09.md |
| Navigation atlas & connectors | ProjectMaelstrom/ProjectMaelstrom/Utilities/NavigationAtlas.cs; ProjectMaelstrom/ProjectMaelstrom/Utilities/NavigationConnectorGeometry.cs | Pose-exact route planning over verified connectors and corridors | C# core | Relationship, Perspective | ProjectMaelstrom/ProjectMaelstrom.Tests/NavigationAtlasTests.cs; ProjectMaelstrom/ProjectMaelstrom.Tests/NavigationConnectorGeometryTests.cs |
| Zone topology service | ProjectMaelstrom/ProjectMaelstrom/Utilities/ZoneTopologyService.cs | Zone name-to-path topology with shortest-path resolution | C# core | Relationship | ProjectMaelstrom/ProjectMaelstrom.Tests/NativeSuppliedIntegrationsTests.cs |
| Zone graph / Dijkstra | ProjectMaelstrom/ProjectMaelstrom/Utilities/NavigationMap.cs | Parses zone.nav; bounded Dijkstra (32,767 v / 250,000 e) | C# core | Relationship, Constraint | ProjectMaelstrom/ProjectMaelstrom.Tests/NavigationMapTests.cs |
| Master zone map decode | .local/introspection/master-zone-map-121.json; tools/w101_zone_route.py | Candidate 3,517-node zone topology; offline route solving | Data/Python | Relationship, Trust | docs/coordination/results/CLAIM-RECONCILIATION-20261117.md; tools/tests/test_w101_zone_route.py |
| Route landmarks | tools/route_landmarks.py | Extracts landmark observations from recorded navigation evidence | Python tool | Relationship, Perspective | tools/tests/test_route_landmarks.py; docs/coordination/results/W101-ROUTE-LANDMARKS-13.md |
| Combat field reader | tools/w101_combat_reader.py | Reads battle-circle state from saved UI captures | Python tool | State, Perspective | tools/tests/test_w101_combat_reader.py |
| Duel reader | tools/w101_duel_reader.py | Reads duel/arena state frames for offline analysis | Python tool | State, Perspective | tools/tests/test_w101_duel_reader.py |
| Combat turn planner | tools/combat_turn_planner.py | Proposes bounded rule-based action from explicit known facts | Python tool | Transition, Trust | tools/tests/test_combat_turn_planner.py; tools/fixtures/combat_ui/combat-ui-observations-v1.json |
| Combat runtime & turn operations | ProjectMaelstrom/ProjectMaelstrom/Utilities/W101CombatRuntime.cs; ProjectMaelstrom/ProjectMaelstrom/Utilities/W101CombatTurnOperation.cs | Executes planned turns under scheduler and input lease | C# core | Transition, Constraint | ProjectMaelstrom/ProjectMaelstrom.Tests/W101CombatTurnOperationTests.cs; docs/coordination/results/W101-COMBAT-PLAN-03.md |
| Pet dance primed capture | ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDancePrimedCapture.cs | Arms bounded pre-action frame capture for dance runs | C# core | State, Constraint | ProjectMaelstrom/ProjectMaelstrom.Tests/PetDancePrimedCaptureTests.cs |
| Pet dance production runtime | ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceProductionRuntime.cs | End-to-end dance run with gates, receipts and cleanup | C# core | Transition, Constraint | ProjectMaelstrom/ProjectMaelstrom.Tests/PetDanceProductionRuntimeTests.cs; docs/coordination/tasks/DANCE-PRODUCTION-RUNTIME-01.md |
| Pet dance audio analysis | tools/analyze_pet_dance_audio.py; tools/detect_pet_dance_audio.py | Detects and analyzes dance sound cues offline | Python tool | Perspective, State | tools/tests/test_analyze_pet_dance_audio.py; tools/tests/test_detect_pet_dance_audio.py |
| Character switching | ProjectMaelstrom/ProjectMaelstrom/Utilities/W101CharacterOperation.cs | Identifies, enters and verifies character roster selections | C# core | Transition, Trust | ProjectMaelstrom/ProjectMaelstrom.Tests/CharacterSwitchTests.cs; docs/coordination/results/W101-CHARACTER-27.md |
| Teaching recording | ProjectMaelstrom/ProjectMaelstrom/Utilities/TeachingRecording.cs | Records bounded teaching sessions to replayable event files | C# core | State, Transition | ProjectMaelstrom/ProjectMaelstrom.Tests/TeachingRecordingTests.cs |
| Long teaching sessions (lessons) | ProjectMaelstrom/ProjectMaelstrom/Utilities/LongTeachingSession.cs | Compiles long lessons into observed-receipt routine graphs | C# core | Transition, Trust | ProjectMaelstrom/ProjectMaelstrom.Tests/LongTeachingSessionTests.cs; docs/TEACHING_LONG_SESSIONS.md |
| Routine graphs | ProjectMaelstrom/ProjectMaelstrom/Utilities/RoutineGraph.cs | Typed transition graphs with offline replay rehearsal | C# core | Transition, Relationship | ProjectMaelstrom/ProjectMaelstrom.Tests/RoutineGraphTests.cs; docs/ROUTINE_GRAPHS.md |
| Conditional routine replay | ProjectMaelstrom/ProjectMaelstrom/Utilities/RoutineReplay.cs | Offline conditional replay producing verified reports | C# core | Transition, Trust | ProjectMaelstrom/ProjectMaelstrom.Tests/ConditionalRoutineReplayTests.cs |
| Job deck host | ProjectMaelstrom/ProjectMaelstrom/Utilities/JobDeckHost.cs | Runs card jobs over facts, routines and scheduler | C# core | Transition, Trust | ProjectMaelstrom/ProjectMaelstrom.Tests/JobDeckHostTests.cs; docs/JOB_DECK.md |
| Job cards & quotas | ProjectMaelstrom/ProjectMaelstrom/Utilities/StandardJobCards.cs | Built-in quest/travel/resource card definitions and quotas | C# core | Constraint, Transition | ProjectMaelstrom/ProjectMaelstrom.Tests/ExpandedJobCardTests.cs; ProjectMaelstrom/ProjectMaelstrom.Tests/JobDeckTests.cs |
| MCP server | tools/maelstrom_mcp_server.py | Local MCP tools for facts, combat advice, observation status | Python tool | Trust, Constraint | tools/tests/test_maelstrom_mcp_server.py; docs/coordination/results/BACKEND-MCP-WINDOWS-153.md |
| Headless native CLI | ProjectMaelstrom/ProjectMaelstrom/Utilities/MaelstromCli.cs | Window, capture, facts and character commands without UI | C# CLI | Constraint, Trust | ProjectMaelstrom/ProjectMaelstrom.Tests/MaelstromCliTests.cs; docs/HEADLESS_CLI.md |
| Game pack workshop host | ProjectMaelstrom/ProjectMaelstrom/Utilities/WorkshopHost.cs | Authoring and persistence host for game pack workshops | C# core | State, Trust | ProjectMaelstrom/ProjectMaelstrom.Tests/WorkshopHostTests.cs; docs/GAME_PACK_WORKSHOP.md |
| Pack workshop tooling | tools/game_pack_workshop.py | Creates, versions and evaluates offline game packs | Python tool | State, Trust | tools/tests/test_game_workshop.py; tools/fixtures/game_packs/session.json |
| Diego vision | tools/diego_vision.py | Frame observation schema validation for Diego tutorials | Python tool | Perspective, Trust | tools/tests/test_diego_vision.py; tools/fixtures/diego_observer/v1/manifest.json |
| Diego observer | tools/diego_observer.py | Interactive screen observer with ROI frame tracking | Python tool | Perspective, State | tools/tests/test_diego_observer_ui.py; docs/DIEGO_OBSERVER.md |
| Boss wiki resolver | tools/boss_wiki_resolver.py | Resolves boss data from WizWiki with provenance | Python tool | Trust, Relationship | tools/tests/test_supplied_projects_integration.py |
| Dodge analysis scripts | tools/analyze_dodge_outcomes.py (plus analyze_dodge_timing/tracking/distribution/action_support) | Offline outcome, timing and tracking analysis of dodge runs | Python tool | State, Trust | tools/tests/test_analyze_dodge_outcomes.py; tools/fixtures/dodge-benchmark-validation.json |
| W101 Python perception readers | tools/w101_pose_reader.py; tools/w101_zone_reader.py; tools/w101_stats_profile.py | Extract pose, zone and stats profiles from captures | Python tool | Perspective, State | tools/tests/test_w101_pose_reader.py; tools/tests/test_w101_zone_reader.py |
| UI tree reader | tools/w101_ui_tree.py | Reviewed-build-gated UI tree dump with profile allowlist | Python tool | Trust, Perspective | tools/tests/test_w101_ui_tree.py (83 tests); ProjectMaelstrom/ProjectMaelstrom/Utilities/W101UiTreeOperation.cs |
| maelstrom-core crate | crates/maelstrom-core | no_std domain types, deck model, confidence gate contracts | Rust crate | State, Trust | crates/maelstrom-core/tests/waypoint_parity.rs; docs/coordination/results/RUST-PORT-AUDIT-30.md |
| maelstrom-platform crate | crates/maelstrom-platform | Client attachment honoring client-side exception safety invariants | Rust crate | Constraint, Trust | crates/maelstrom-platform/tests/phase3_platform.rs; docs/W101_CLIENT_SIDE_EXCEPTION_20261110.md |
| maelstrom-ide crate | crates/maelstrom-ide | Dockable egui spellbook IDE preview workspace | Rust crate | Perspective | crates/maelstrom-ide/tests/phase4_ide.rs; docs/coordination/results/RUST-PROMOTION-AUDIT-01.md |
| maelstrom-fallback crate | crates/maelstrom-fallback | Native pipe bridge, runner, pet-dance and bazaar modules | Rust crate | Transition, Relationship | crates/maelstrom-fallback/tests/native_bridge.rs; crates/maelstrom-fallback/tests/dance_parity.rs |
| maelstrom-reflex crate | crates/maelstrom-reflex | Stub policy inference and takeover counter buffer | Rust crate | Trust | docs/coordination/results/RUST-PORT-AUDIT-30.md; docs/coordination/results/UNIVERSAL-CONTROL-LOOP-01.md |
| Node-RED integration | integrations/node-red | Flow bridge, card nodes and editor for Maelstrom | Node integration | Transition, Perspective | integrations/node-red/bridge.test.js; integrations/node-red/README.md |
| Coordination records | docs/coordination | Task claims, workstreams and result evidence registry | Docs | Trust, Constraint | docs/coordination/workstreams.json; docs/coordination/results/CLAIM-RECONCILIATION-20261117.md |

## Notes and caveats

- **`.local/introspection/master-zone-map-121.json`** is gitignored: it is not
  clean-checkout evidence. The census row pairs it with the tracked
  `tools/w101_zone_route.py` tests instead.
- **Neither `docs/architecture/` nor this file existed before the claim**; they are
  new under `ONTOLOGY-CENSUS-01`.
- The inventory was cross-checked against the earlier alignment audit: none of the
  commonly named helpers (`ZoneTopologyService`, `CircuitSnapshot`,
  `CapturedWindowFrame`, `PathFollower`, `WaypointFeedback`, `OperationFocusBudget`,
  `ManualCaptureController`) was missing — all located. Corrections to that audit
  (fabricated field names, wrong fact tier) are recorded in
  [ONTOLOGY_NORMALIZATION.md](ONTOLOGY_NORMALIZATION.md) and
  [../coordination/results/ARCH-REF-REVIEW-84.md](../coordination/results/ARCH-REF-REVIEW-84.md).
- Census scope: inventory and classification only. It authorizes no refactor; each
  leak or restructure needs its own claim and evidence.


