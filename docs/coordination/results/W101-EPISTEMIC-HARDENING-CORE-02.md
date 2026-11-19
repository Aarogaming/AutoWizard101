# Evidence Report: W101-EPISTEMIC-HARDENING-CORE-02

## 1. Summary
Implemented the concrete systems-level Epistemic Reality Runtime Hardening extensions in `crates/maelstrom-core/src/ontology.rs` and verified all behavior with 14 unit and integration tests in `crates/maelstrom-core/tests/ontology_tests.rs`.

The module strictly enforces the **Facts != Authority != Actions** architecture:
- **Provenance & Versioning Tagging**: Added `ProvenanceTag` with `ProvenanceOrigin` (`StaticWadExtract`, `EmpiricalLiveObservation`, `ManualSeed`, `SyntheticTestFixture`), `build_hash`, verification counters, and epoch timestamps on `Manifold` and `Entity`.
- **Entity Epistemic Belief States & Anti-Hallucination Decay**: Added `EntityEpistemicState` (`Active`, `Depleted { cooldown_until_ms }`, `Stale`, `Unknown`). Dynamic roaming mobs decay to `Unknown` upon exceeding `ttl_ms` ($\le 500\text{ms}$), while harvestable entities support explicit `mark_depleted(cooldown_until_ms)` states rather than silent removal.
- **Bayesian Negative Evidence Recording**: Implemented `record_negative_evidence(now_ms)` on `Entity` and `RealityWorldModel`. Repeated negative observations drop entity confidence to `Unknown`, preventing path planner oscillation between empty anchors.
- **Native Client Fast-Travel Connectors & Shortcuts**: Added `Connector::fast_travel_home`, `Connector::fast_travel_commons`, and `Connector::marker_recall`, along with `AffordanceKind::HotbarShortcut`, `SetMarker`, `RecallMarker`, and `EmergencyPotion`, turning multi-zone walking into deterministic $O(1)$ topological graph jumps.
- **Multi-Client Sigil Lockstep Barrier Synchronization**: Implemented `MultiClientSigilBarrier` managing multi-client states (`Preparing`, `ReadyForSigil`, `SteppedOnSigil`, `InCombat`), guaranteeing synchronized 10s countdown ring entry across dual-boxing/hivemind sessions.
- **Phased Zone Instance Identity**: Added `ZoneInstanceKey` (`zone_template_id`, `phase_tag`) to `Manifold`, decoupling learned spatial geometry and anchors from transient process memory addresses.
- **Tiered Graceful Degradation Ladder**: Implemented `record_drift_observation` managing transitions across `Tier0FullOntology`, `Tier1VisualAcoustic`, and `Tier2SafeHalt` under consecutive residual drift faults.

## 2. Verification Evidence
- **Integration Test Suite**: `cargo test -p maelstrom-core --test ontology_tests`
  - `test_manifold_and_entity_kinematics`: PASSED
  - `test_affordance_accessibility_and_gating`: PASSED
  - `test_connector_topology_jump`: PASSED
  - `test_hazard_dynamic_threat_and_vertical_separation`: PASSED
  - `test_resource_consumption_and_replenishment`: PASSED
  - `test_predicate_confidence_and_freshness`: PASSED
  - `test_reality_world_model_integration_and_goap_conversion`: PASSED
  - `test_provenance_tagging_and_versioning`: PASSED
  - `test_entity_epistemic_decay_and_negative_evidence`: PASSED
  - `test_reagent_harvest_depletion_and_cooldown`: PASSED
  - `test_fast_travel_connectors_and_shortcuts`: PASSED
  - `test_multi_client_sigil_barrier_synchronization`: PASSED
  - `test_tiered_graceful_degradation_ladder`: PASSED
  - `test_zone_instance_key_preservation`: PASSED
  - Result: 14 passed; 0 failed.
- **Full Workspace Tests**: `cargo test --workspace` passed 100% across all crates (65 unit tests, 14 ontology, 8 fishing, 6 combat evaluator, 19 fallback, 7 replay CLI, 9 IDE, platform, and reflex).
- **Linter**: `cargo clippy -p maelstrom-core --all-targets -- -D warnings` passed with 0 warnings.
- **Formatting**: `rustfmt --edition 2021` applied cleanly.
- **C# Native Tests**: `dotnet test ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj -c Release` passed 100% (3,135 passed; 0 failed).
- **Quality Gates**: `python tools/check_project_board.py` and `python tools/check_text_encoding.py` both passed cleanly with 0 violations.
