# Documentation map

- [Worker manual](../WORKER_MANUAL.md) — one-page entry point for agents: claim, stay in scope, prove with evidence, hand back.
- [Current roadmap](ROADMAP.md) — ordered integration work, current installed/source distinction and acceptance gates.
- [W101 automation readiness](W101_AUTOMATION_READINESS.md) — task-by-task progress, remaining gates and dependency order; live demonstrations distinguished from complete unattended routines.
- [Cards, routines and decks](ROUTINE_EXECUTION_MODEL.md) — Action Cards, Routine Cards and the Routine Deck; shared constraints, verified results and controlled interruption in game-player terms.
- [Architecture audit](ARCHITECTURE_AUDIT_20261111.md) — callable operations, card constraints, layered contracts, knowledge accuracy and concrete gaps.
- [Ontology normalization](architecture/ONTOLOGY_NORMALIZATION.md) — the Facts != Authority != Actions invariant, the six primitives (State/Transition/Relationship/Constraint/Trust/Perspective) and the leak register; interpretation and audit surface only, no rewrite authorized.
- [Repository census](architecture/REPOSITORY_CENSUS.md) — Phase 1 inventory: every major subsystem mapped to path, purpose, category, primitives and evidence; paths existence-checked.
- [Language allocation and core workflows](architecture/LANGUAGE_ALLOCATION_AND_WORKFLOWS.md) — layer-by-layer language allocation (Rust, C#, Python, C/C++) and the five core engineering workflows governing epistemic reality-interactor runtimes.
- [Ownership, borrowing and typestates](architecture/OWNERSHIP_BORROWING_AND_TYPESTATES.md) — modeling reality runtime capabilities as scarce resources via Rust ownership, shared/mutable borrows, typestates, scoped tokens and linear move semantics.
- [Capability composition gap map](architecture/CAPABILITY_COMPOSITION_GAP_MAP.md) — what three Copilot proposals (capability composition; API/MCP/CLI/LSP/event surfaces; capability genome and intent preservation) add beyond the existing card, routine and control-plane contracts: real gaps, one deferral, and what is not adopted.
- [Capability inventory](../research/maelstrom-capabilities.json) — checked, non-executable map of selected workflow families; not a complete runtime registry.
- [Card collection and Routine Deck](JOB_DECK.md) — configurable cards, front/back editor, quotas, generic quest auto-fill, shared scheduler and local API/MCP; installed checkpoint is recorded separately from later source changes.
- [Installed deck release](coordination/results/CORE-DECK-RELEASE-02.md) and [later source checkpoint](coordination/results/W101-COMBAT-CONNECT-06.md) — distribution and newer implementation evidence; neither is complete auto-questing.
- [Deck integration review](coordination/results/CORE-DECK-01.md) — Gemini prototype review, repairs, tests and remaining delivery/navigation gates.

## Direction and implementation

- [Wizard101 working reference](WIZARD101_GAME_KNOWLEDGE.md) — representative sourced controls/mechanics and explicit unknowns; not exhaustive or automatically qualified for execution.
- [Knowledge accuracy audit](coordination/results/W101-KNOWLEDGE-AUDIT-01.md) — primary-source checks, calculator/geometry assumptions and the actual eight-source/nine-claim database slice.
- [Card and callable-surface audit](coordination/results/CORE-CARD-AUDIT-01.md) and [imported-source/Aaroneous audit](coordination/results/CORE-REUSE-AUDIT-01.md) — current source boundaries and prioritized reuse.
- [Earlier W101 workflow checkpoint](W101_WORKFLOW_STATUS_20261110.md) — November 10 routines/UI and conditional-switching gaps; use the current readiness map above for later progress.
- [Selected quest conversation](W101_QUEST_STEP_20261110.md) — optional quest text, reusable repeated menus, bounded conversation pilot and evidence.

- [Open issues: report and plan (2026-11-19)](OPEN_ISSUES_REPORT_AND_PLAN_20261119.md) — draft answer on patch discovery/recovery and each open issue, written in the who/what/when/where/why/how and before/during/after format.
- [Legacy root files](history/legacy-root/README.md) — Aaroneous Automation Suite and Smart Trainer era documents moved out of the repository root; historical, not current policy.
- [Project organization](PROJECT_ORGANIZATION.md) — current sources of truth, parallel lanes and evidence rules.
- [Backend control plane](BACKEND_CONTROL_PLANE.md) — backend-first operation contract, CLI/API/MCP/UI coverage audit, a read-only Rust-to-C# named pipe snapshot client with offline decision logic (MaelstromAutonomyBridge; not yet wired back to the host), and staged parity plan.
- [Game workstreams](GAME_WORKSTREAMS.md) and [structured board](coordination/workstreams.json) — task inventory, ownership, dependencies and next actions.
- [External-agent guide](coordination/EXTERNAL_AGENT_GUIDE.md) — bounded task briefs and shared-checkout coordination for Codex, Claude or Gemini.
- [Shared routine graphs](ROUTINE_GRAPHS.md) — reusable transition runner, registered actions and offline rehearsal.
- [Reusable modules](REUSABLE_MODULES.md) — versioned navigation, sigil, menu and Auto-Idle contracts, composition and saved bundle replay.
- [Screen/scheduler integration](MODULE_SCREEN_INTEGRATION_20261110.md) — actual saved pixels through OCR, module decisions, ordinary job receipts and the existing Dance cleanup source.
- [Current scheduler reuse checkpoint](SCHEDULER_REUSE_INTEGRATION_20261110.md) — refreshed Devtools, immutable admission/history, cancellation repair, 1,379 passing tests and compiled offline workflow evidence.
- [Verified recovery flow](INPUT_RECOVERY_FLOW_20261110.md) — explicit Stop, Check and Restore UI, shared worker drain, fresh release checks and preserved recovery evidence; source-only, with offline verification recorded separately from live qualification.
- [Installed recovery workflow](INPUT_RECOVERY_LIVE_20261110.md) — real Stop/Check/Restore pass from a clean guard, saved quiet-verification evidence, unchanged settings and no gameplay; desktop control ended.
- [Recovery update installation](INPUT_RECOVERY_INSTALL_20261110.md) — package installed, binary hashes and data preservation checked; records the earlier interrupted UI attempt separately from the completed check above.
- [First wizwalker adapter](WIZWALKER_ADAPTER_20261110.md) — optional one-shot client loading observation, source-pinned Python worker, shared native scheduler/control owner, fake-client tests and remaining live gates.
- [Loading transitions and shared routines](W101_LOADING_TRANSITION_20261110.md) — six demonstrated zone loading periods, bounded CLI watch, optional shared loading conditions, offline integration and remaining live gates.
- [Optional W101 UI tree](W101_UI_TREE_20261110.md) — bounded root-hook observer, staggered loading/UI reads, candidate screen states, restoration checks and qualification status.
- [Headless commands](HEADLESS_CLI.md) — window discovery, saved run status and bounded opt-in checks without desktop input.
- [Rust toolchain pin](RUST_TOOLCHAIN.md) — the pinned compiler in `rust-toolchain.toml`, what was checked under it, what it changes and how to move it.
- [CLI agent operating procedures](CLI_AGENT_OPERATING_PROCEDURES.md) — safe invocation, exact target selection, cancellation, receipt reading and current handoff notes.
- [Wizwalker live qualification](WIZWALKER_LIVE_20261110.md) — dependency repair, two successful idle-state reads and clean disconnect; positive loading-transition qualification remains open.
- [W101 client-side exception](W101_CLIENT_SIDE_EXCEPTION_20261110.md) — owner-approved functional reuse of reviewed client-side hooks, injection and memory reads/writes; network manipulation excluded; source dispositions and next integration gates.
- [Earlier W101 telemetry review](W101_TELEMETRY_REVIEW_20261110.md) — historical read-only interpretation, independently tested skeleton and unresolved provider issues; the expanded decision above supersedes its scope.
- [Input recovery guard](INPUT_RECOVERY_GUARD_20261110.md) — preceding failed-warning-save checkpoint: native/Python gameplay stays blocked; same-owner checks, continued offline work and 1,410 passing native tests.
- [Refreshed Guild and Steward review](GUILD_STEWARD_LATEST_REUSE_20261110.md) — current pinned Devtools contracts, implemented/proposed boundaries, selected native reuse and deferred gaps.
- [Earlier cached Guild and Steward review](GUILD_STEWARD_SCHEDULER_REUSE_20261110.md) — historical local source contracts and initial scheduler recovery repair.
- [Module standardization checkpoint](MODULE_STANDARDIZATION_20261110.md) — W101 migration, independent-game fixtures, tests and remaining live/editor integration.
- [Shared W101 operations](W101_SHARED_OPERATIONS.md) — navigation, sigils, menus and Auto-Idle compiled through the common runner.
- [Macro demonstration pipeline](coordination/results/MACRO-PIPELINE-01.md) — RDP waypoint simplification, dual-stream pairing, deterministic compiler and C# parser; live recording UI and in-game qualification remain open.
- [Dance selection qualification](PET_DANCE_SELECTION_QUALIFICATION.md) — pinned saved-footage comparisons and remaining layout/hover limits.
- [Conjuration card collection](CONJURATION_CARD_COLLECTION.md) — bounded preparation, duplicate-aware curation and the next recording plan.
- [Offline qualification checkpoint](W101_OFFLINE_QUALIFICATION_20261110.md) — saved-image review, shared-operation and collection validation.
- [Build retention inventory](BUILD_ARTIFACT_RETENTION.md) — measured build/package categories, stale target-path finding and preservation rules; no cleanup performed.
- [Routine foundation validation](ROUTINE_FOUNDATION_VALIDATION_20261109.md) — integrated tests, replay receipts, reviewed repairs and remaining live gates.
- [Conjuration offline helper](CONJURATION_OFFLINE.md) — memory-match decisions and shuffled-board tests; live perception remains separate.
- [Dance entry transitions](PET_DANCE_ENTRY_TRANSITIONS.md) — saved OCR, enabled/disabled Play pixels and receipt-aware full-visit rehearsal.
- [Conjuration recognition](CONJURATION_RECOGNITION.md) and [image-replay result](coordination/results/CONJ-PERCEPTION-01.md) — bounded card matching into the memory helper, with explicit evidence limits.
- [Parallel recognition checkpoint](PARALLEL_RECOGNITION_VALIDATION_20261109.md) — integrated tests and the remaining live-readiness gates.
- [Product charter](PRODUCT.md) — authoritative scope.
- [Dodge workspace](DODGE_GAME_WORKSPACE.md) — dedicated game entry and shared Observe/test/pack routes.
- [Pet Dance native Idle](PET_DANCE_POSTGAME_OPERATION.md) — postgame UI operation and offline evidence.
- [Installed Dance/Idle trial](PET_DANCE_IDLE_LIVE_20261109.md) — PERFECT gameplay, interrupted attempt, postgame frame-admission failure, and supervised cleanup.
- [Pet arenas and snacks](PET_TRAINING_LEVELS_AND_SNACKS.md) — sourced research, qualification and preferences.
- [Native resolution plan](PET_DANCE_RESOLUTION_PLAN.md) — grid/client/ROI mapping and qualification gates.
- [Workspace release checkpoint](WORKSPACE_RELEASE_20261109.md) — staged package, tests and remaining live gates.
- [Game workspaces](GAME_WORKSPACES.md) — explicit game selection and session ownership.
- [Roadmap](ROADMAP.md) — implementation order and gates.
- [Earlier delivery course](DELIVERY_COURSE.md) — historical priorities and evidence; the current roadmap supersedes its work order.
- [Earlier game-player gap analysis](GAME_PLAYER_GAP_ANALYSIS.md) — historical findings; use the current architecture audit for reconciled priorities.
- [Language-free player learning](PLAYER_LEARNING_COMPARISON.md) — related systems and the offline-to-frozen-policy design.
- [Aaroneous and Devtools reuse review](AARONEOUS_MAELSTROM_REUSE_REVIEW.md) — candidate components, compatibility gaps and shared testing opportunities.
- [Research protocol and experiment index](RESEARCH_PROTOCOL.md) — study design, evidence standards and current results.
- [Experiment workbench](EXPERIMENT_WORKBENCH.md) — in-app comparisons, frozen evidence, gameplay footage and the W101 home-session prerequisite.
- [Wizard101 routines](W101_ROUTINES.md) — one game workspace, shared actions, categories and event-driven chaining.
- [W101 Auto-Idle](HOME_MOVEMENT.md) — persistent choices, explicit session controls, desktop takeover, limits and live qualification gaps.
- [W101 keep-active reuse check](W101_KEEP_ACTIVE_REUSE.md) — root ZIP references, native Idle, and the compact-HUD correction.
- [Legacy W101 teachers](W101_LEGACY_TEACHERS.md) — archive inventory, source reuse, offline Pet Dance rehearsal and demonstration-learning boundaries.
- [Pet Dance teacher](PET_DANCE_TEACHER.md) — repaired native adapter, local asset import, scale calibration, recording review and proposal packs.
- [Pet Dance fade validation](PET_DANCE_FADE_VALIDATION_20261108.md) — complete recorded sequence review, loading-aware trials and remaining live gates.
- [Pet Dance delivery tracker](PET_DANCE_DELIVERY_TRACKER_20261108.md) — evidence-based work status, live acceptance gates and parallel research tasks.
- [Gameplay scene gates](GAMEPLAY_SCENE_GATES.md) — loading pauses, separate time budgets, fresh sequence resumption and the corrected live-trial interpretation.
- [Pixel geometry diagnostic](PET_DANCE_CALIBRATION_DIAGNOSTIC.md) — exact GDI regression fixtures; encoded dimensions alone do not establish a capture transform.
- [Complete Dance visit](PET_DANCE_LIFECYCLE.md) — tested proposal planner from approach through results and native Idle; detectors and dispatcher remain separate.
- [Fresh full-visit observation](PET_DANCE_LIFECYCLE_OBSERVATION_20261109.md) — supervised menus, user demonstration arrows, frozen-profile evaluation and observed native Idle return; no audio or autonomous play.
- [Session settings](GAME_SESSION_SETTINGS.md) — capture originals, propose changes and verify restoration without overwriting user changes; UI execution pending.
- [W101 UI-only boundary](W101_UI_ONLY_BOUNDARY.md) — application admission checks for legacy launch paths and the separate, disabled memory-observation capability.
- [Memory-observation research](W101_MEMORY_OBSERVATION_RESEARCH.md) and [desktop input options](DESKTOP_INPUT_OPTIONS.md) — sourced limits; no driver or protection changes.
- [Legacy work packages](W101_LEGACY_WORK_PACKAGES.md) — dependencies, owners, reuse alternatives and acceptance gates across all 25 root archives.
- [Automatus route import](AUTOMATUS_ROUTE_IMPORT.md) — how its waypoint files enter Maelstrom as hashed, candidate-only route evidence.
- [Pet snack research](W101_PET_SNACK_RESEARCH_20261108.md) and [configuration proposal](W101_PET_SNACK_CONFIGURATION.md) — separate training/Happiness choices, inventory reserves and proposed transaction rules.
- [Read-only snack planner](PET_SNACK_PLANNER.md) — deterministic suggestions from supplied observations, with separate goals, reserves and budgets; no feeding.
- [Game knowledge database](GAME_KNOWLEDGE_DATABASE.md) — tested local schema, source chronology, conflicting claims and staged domain imports; application integration pending.
- [First knowledge importer](W101_KNOWLEDGE_IMPORT.md) — bounded pet-snack ledger to a new SQLite bundle with provenance and commit/hash receipt; no runtime enablement.
- [Game audio cues](GAME_AUDIO_CUES.md) — selected-process WASAPI loopback, native Goertzel spectral classifier (W101-AUDIO-CORE-02), acoustic signature profiles (splash, fanfare, timer), and Blackboard routing.
- [Kinematic input profiles](KINEMATIC_INPUT_PROFILES.md) — design reference, partly implemented, for humanized input: WindMouse (no caller yet), cubic Bézier minimum-jerk curves, Fitts's law durations and log-normal click dwell; only the click target and dwell reach dispatch.
- [Goal-Oriented Action Planning](GOAP_ACTION_PLANNING.md) — design plus library code: the A* planner in `goap.rs` (heuristic corrected 2026-11-19) with offline tests only; no caller outside tests, nothing feeds a plan to the Behavior Tree, and there is no replanning.
- [W101 Quest Repertoire](W101_QUEST_REPERTOIRE.md) — canonical Wizard City starter questline modeling ("The Lore of Magic", "Enrollment", "A Good Impression"), domain actions, zone transitions, NPC interaction bounds (uncalibrated placeholders), and boolean vitals recovery; planning data only, with no caller outside tests.
- [W101 Unicorn Way Quest Domain](W101_QUEST_UNICORN_WAY.md) — Unicorn Way progression ("Finding Out", Private O'Ryan, Lost Soul & Skeletal Pirate combat encounters, "Grim Tales", Private Stillson, Lady Oriel sanctuary), dialogue geometries (uncalibrated placeholders), and a plan-level pre-combat vitals precondition; planning data only, with no caller outside tests, and a low-vitals start needs more than the default search budget.
- [Data-Driven Quest Interpreter](QUEST_INTERPRETER_ENGINE.md) — generic 6 canonical objective archetypes, universal dialogue and quest-accept state machine, quest helper compass vector, and decoupled world topology in Olde Town and Triton Avenue.
- [Offline Autonomy Replay Harness](OFFLINE_AUTONOMY_REPLAY.md) — offline replay of synthetic or ingested session frames through the autonomy bridge: label agreement, an intent-change counter and two proxy figures; the thresholds are partly vacuous (see its status banner) and no recorded multi-hour trace is committed.
- [Autonomy Replay CLI](AUTONOMY_REPLAY_CLI.md) — the 'maelstrom-replay' CLI: single-trace and batch modes, Markdown and JSON output, exit code 2 when accuracy is below 95%; the printed numbers are the harness's proxies (see its status banner) and the steering heading is the z coordinate.
- [Real Trace Ingestion](REAL_TRACE_INGESTION.md) — what the two `.local` ingesters read and what they invent (synthetic events file, a UI capture with no combat frames, fabricated position and vitals), and what an ingested replay can and cannot show.
- [Replay Batch Benchmark](REPLAY_BATCH_BENCHMARK.md) — the `--batch-dir` mode of maelstrom-replay: what it loads, the one real gate (accuracy below 95% exits 2), a real example matrix, and a scratch-directory path-escape defect (fix on a branch).
- [Desktop Input Actuator](DESKTOP_INPUT_ACTUATOR.md) — the C# actuator that turns steering and click summaries into bounded key and mouse presses under GameInputLease; what it enforces itself and what it leaves to the caller.
- [JobDeckHost Actuation Loop](JOB_DECK_ACTUATION_LOOP.md) — the `actuate` named-pipe command: simulated playback works and is tested; the live branch is a stub that sends no input.
- [Combat Dueling Loop](COMBAT_DUELING_LOOP.md) — design plus unconnected pieces: the Behavior Tree combat branch, 800x600 geometry scaled (not measured) to 1280x720, a click-target plan summary and the actuator's click path; nothing chooses a card or target and nothing sends the plan to the host.
- [Pre-Battle Deck Profile Switching](PRE_BATTLE_DECK_PROFILES.md) — design note, partly aspirational (see its status banner): intended out-of-combat deck verification, safe-zone margin checks, encounter archetype tagging (MobAoE, BossFeint, SchoolPrism, HealSupport), spellbook UI geometry, and GOAP EnsureDeckProfile action integration.
- [Combat Turn Evaluator Engine](COMBAT_TURN_EVALUATOR.md) — the offline turn evaluator in `maelstrom-core` (pip arithmetic, blade/trap damage estimate, a fixed priority list, pedestal choice), corrected in the 2026-11-19 audit; not wired to the behavior tree or the host.
- [Reality Runtime Primitive Ontology](REALITY_RUNTIME_ONTOLOGY.md) — the 7 irreducible grammar primitives of reality (Manifold, Entity, Affordance, Connector, Hazard, Resource, Predicate), epistemic control loops, and a mapping from Wizard101 Gamebryo telemetry into those schemas. Design note, partly aspirational (see its status banner): the primitives exist as Rust types with offline tests and nothing consumes the world model yet.
- [Acoustic Fishing Reflex Engine](ACOUSTIC_FISHING_REFLEX.md) — design plus a tested state machine that nothing calls yet: energy precondition, lead-cast geometry, bobber splash reaction with a 150 ms latency flag, state transitions and catch statistics.
- [Epistemic Reality Runtime Hardening](EPISTEMIC_RUNTIME_HARDENING.md) — design specification plus a small tested data model (TTL states, a negative-evidence counter, fast-travel connector constructors, a sigil status gate, a drift counter, provenance tags) that nothing outside the tests calls; the half-life math, shared bus, countdown lockstep, safe halt and several shortcut figures are not implemented or sourced.
- [Short-Horizon Prediction Rollout Engine](SHORT_HORIZON_PREDICTION_ROLLOUT.md) — Mr. House anticipation loop, analytical reflexes vs learned local predictors, shadow world state snapshots, branching micro-future rollouts, model surprise counterfactuals in maelstrom-replay, and compute budgets.
- [W101 Master Mechanics & Reference Corpus](W101_MASTER_REFERENCE_CORPUS.md) — canonical community databases, official KingsIsle guides, combat math formulas, boss cheat catalogs, NPC threat/AI targeting, world subsystems, Katsuba/NifXML schemas, and fact-verification trust hierarchies.
- [W101 Onboarding Context Corpus](W101_ONBOARDING_CONTEXT_CORPUS.md) — zero-ambiguity onboarding specs: Pet Pavilion mini-games rules, energy scaling and zero-waste exit thresholds, Fairegrounds and Silver Chest puzzle policies, combat stack/damage equations, and zero-mouse navigation shortcuts.
- [Capture cancellation review](WINDOW_CAPTURE_CANCELLATION_REVIEW.md) — pending native-copy lifetime repair and tests needed for bounded caller cancellation.
- [Installed W101 sources](W101_LOCAL_INSTALLATION_SOURCES.md) — bounded archive inventory, candidate pet/spell/minigame/navigation data and read-only import plan.
- [Legacy qualification matrix](W101_LEGACY_QUALIFICATION.md) — all 25 supplied archives, independent alternatives, component tests, reproduced defects and build blockers.
- [Legacy review validation](W101_LEGACY_VALIDATION_20261108.md) — measured results, concrete Maelstrom binding points, installed release and remaining runtime gaps.
- [W101 capability integration map](W101_CAPABILITY_INTEGRATION_MAP.md) — all archive/tool roles, teacher-to-native paths, proposed operations, scheduler repairs and dependency-gated acceptance criteria.
- [Shared operations](SHARED_OPERATIONS.md) — implemented bounded queue, native/Python input ownership, run history, scheduled Pet Dance review and remaining live gates.
- [Shared operations validation](SHARED_OPERATIONS_VALIDATION_20261108.md) — installed release, tests, preserved user data and the brief native Idle return check.
- [Destination navigation](DESTINATION_NAVIGATION.md) — candidate graph to owned execution, Nav2 3-layer costmap engine (W101-COSTMAP-01), ZoneNavMesh parser and edge-cost planner library (W101-ZONE-COSTMAP-02; not wired to a caller), Behavior Tree state orchestration (ROUTINE_EXECUTION_MODEL.md; library code ticked only by the offline bridge, several leaves only emit a string), threat repulsion and native Idle restoration; early requirements in [W101 destination navigation](W101_DESTINATION_NAVIGATION.md).
- [Dynamic 3D Spatial Indexing](SPATIAL_INDEXING_ROBOTICS.md) — draft design note: height-balanced 3D bounding-volume tree in spatial_index.rs (held by the costmap) with insert, remove, update and range, nearest and corridor queries; not on the planner's cost path.
- [Opportunistic Detour Filter](OPPORTUNISTIC_DETOUR_FILTER.md) — design note, partly aspirational (see its status banner): detour budget equation and two-leg corridor check exist as library code with offline tests; nothing calls them yet.
- [W101 route demonstration](W101_ROUTE_DEMONSTRATION.md) — passive video and navigation-key recording, human-ready coordination, measured capture limits and route-learning boundaries.
- [Recorded Commons route and Tiny UI study](W101_DEMONSTRATION_20261108.md) — human route and Dance footage, synchronized input evidence, native arrow calibration and fade-transition gap.
- [W101 operating knowledge and version history](W101_GAME_RESEARCH_20261108.md) — sourced travel mechanics, chronological changes, Tiny UI requirements, current observations and the first Pavilion route.
- [Desktop player architecture](DESKTOP_GAME_PLAYER_PLAN.md) — shared foundation.
- [Game Pack Workshop](GAME_PACK_WORKSHOP.md) — authoring and persistence design.
- [Workshop quick guide](WORKSHOP_USER_GUIDE.md) — current offline window.
- [Standalone profile](MAELSTROM_STANDALONE_PROFILE.md) — implemented baseline.
- [Tools guide](TOOLS_USER_GUIDE.md), [activities](GUIDED_ACTIVITIES.md),
  [observer](DIEGO_OBSERVER.md), [integrations](WIZARD101_INTEGRATION_GUIDE.md).

## Evidence and recovery

- [Gaming model trials](GAMING_MODEL_TRIALS.md) — local NitroGen and Generalist-IDM experiments and limits.
- [MaaFramework and Airtest review](VISUAL_AUTOMATION_REUSE_REVIEW.md) — source-pinned decisions, offline probe and sampler/pack integration.

- [Repository review](REPOSITORY_REUSE_REVIEW.md) and [local sources](LOCAL_REFERENCE_SOURCES.md).
- [Recovery appendix](MAELSTROM_2_RECONSTRUCTION_PLAN.md) — source paths and repairs.
- [Minigame research](MINIGAME_RESEARCH.md).
- [Dodge offline learning research](DODGE_OFFLINE_LEARNING.md) — recorded-data models, evaluation limits, and isolated testing.
- [Dueling Diego standalone reference](DUELING_DIEGO_REFERENCE_GAME.md) — local archive provenance and test limitations.
- [Independence decision](ADR_MAELSTROM_INDEPENDENT.md).

## History

Earlier Aaroneous handoffs and server/toolkit documents remain reference material.
The existing AARONEOUS_COMPATIBILITY.md also documents the current offline report
shape; preserve compatibility despite its historical filename. The old README,
server roadmap and assistant context are preserved in history/. Relative links in
those snapshots retain their original locations and may need resolution from the
repository root or original docs directory. They are not current setup instructions.
