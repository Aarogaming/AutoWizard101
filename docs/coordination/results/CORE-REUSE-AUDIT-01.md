# Aaroneous contracts and imported-source reuse audit

2026-11-11 · `CORE-REUSE-AUDIT-01` · workstream `CORE-DELIVERY`

Status: source review complete; recommendations are not new runtime integrations.
Owned path: this report only. No game access, desktop control, models, builds,
dependency installation, source refresh or release work was performed.

## Conclusion

Maelstrom already has much of the intended spine: versioned routine/module data,
observations distinct from decisions, a sequential operation scheduler, exclusive
input ownership, cancellation/recovery, and evidence artifacts. The best next
reuse is to consolidate and expose those existing contracts consistently, then
qualify individual adapters. Importing the entire Aaroneous/Guild/Steward runtime
would add competing ownership and dependencies.

“Transformer” has a concrete architectural meaning here: a bounded conversion
between independently testable representations. It need not mean a language model
or machine-learning architecture. Maelstrom should make each conversion inspectable
and replayable, with versions, source identity, freshness and explicit failure.

The imported material has **not been maximized or fully integrated**. The legacy
map accounts for 25 supplied archives, but catalog coverage is not runtime coverage.
Some items should remain references or independent comparison implementations;
using every codebase in production is not the objective. Some catalog entries are
also stale relative to current source.

## Source identity and limits

These are verified local Git HEADs, not a claim of current upstream HEAD:

| Local source | Exact revision | Commit timestamp |
| --- | --- | --- |
| `.local/reference-repos/aaroneous` | `a3383f2feeeee32a47bcd11e34698cfc0d138131` | 2026-10-29 16:25:02 -04:00 |
| `.local/reference-repos/aaroneous-devtools` | `7e7a3d3f45c3d24b8ff8de32d1e1af288840f3ed` | 2026-11-09 05:16:21 UTC |
| `.local/reference-repos/Guild` | `b3749df871403488532a950c51476805dabd77ab` | 2026-04-09 15:35:28 -04:00 |

The Devtools checkout matches the refresh documented in
[`GUILD_STEWARD_LATEST_REUSE_20261110.md`](../../GUILD_STEWARD_LATEST_REUSE_20261110.md).
Aaroneous and standalone Guild are older snapshots. No fetch/pull was performed;
new upstream work may exist. Existing review documents sometimes cite later
individually inspected online files; those do not update these local checkouts.

Primary code was inspected selectively for architecture and existing bindings.
The prior 25-archive reviews supplied historical test evidence; this task did not
execute all archived programs or recertify those results. The shared checkout
contains ongoing work, so source presence alone establishes neither installation
nor live qualification.

## What Aaroneous actually supplies

| Source seam | Concrete implementation | Reuse decision |
| --- | --- | --- |
| `aaroneous/crates/studio_hud/src/transformer_bridge.rs` | Serializable `FrontendCommandSignal`, immutable `BackendTelemetryFrame`, channel bridge; UI emits intent and reads telemetry. | Adopt separation for Maelstrom UI/CLI/MCP. Do not copy its unbounded queues, ignored send failures or nominal default metrics as real observations. |
| `aaroneous/crates/core-contracts/src/lib.rs` | `no_std` contracts, capability flags, fixed-size IPC header and `EngineSnapshotPod`. | Use documented data contracts and explicit capabilities. Fixed-memory Rust ABI is optional, not required for the C#/Python app. Its default FPS/integrity values are defaults, not measurements. |
| `aaroneous/crates/platform_bridge/src/traits.rs` | `VisualObservation`, `HidCommand` sequence/time and `PlatformHost` abstraction. | Preserve observation/action/host boundaries; enrich Maelstrom contracts with target generation, geometry, freshness and evidence. A generic HID command is not permission to act. |
| `aaroneous/crates/platform_bridge/src/sensory_motor_loop.rs` | Gating → latent projection → guardrail → action decode → HID translation; host is explicitly `MockPlatformHost`. | Useful pipeline decomposition and test seam. Not proof of a production game player, isolated desktop or learned survival policy. |
| Devtools `foundation/crates/steward/src/verification.rs` | Request identity binds repository/head/base/platform/suite/digest. | Adapt identity semantics for game pack, policy, target context and experiment version; no need to import repository/merge-specific fields into gameplay. |
| Devtools `foundation/crates/steward/src/local_verification.rs` | Immutable request spool, exclusive claim, monotonically numbered attempts, validated receipts, explicit abandoned-attempt recovery. Running claims are not automatically expired. | Valuable pattern already reflected in Maelstrom journals/recovery. Extend experiment/run identity without adding a second scheduler. |
| Devtools `FOUNDATION_BOUNDARY.md` | Narrow, independently verified capabilities graduate; product must not depend on Devtools. | Follow the same dependency direction. Devtools can validate/export artifacts, while Maelstrom remains runnable independently. |

The earlier `.si`, delta-vision and model-hub findings in
[`AARONEOUS_MAELSTROM_REUSE_REVIEW.md`](../../AARONEOUS_MAELSTROM_REUSE_REVIEW.md)
remain relevant research leads, not newly rerun results: `.si` execution/permission
paths require hardening, mean-difference gating does not measure compute savings,
and model-fit arithmetic is admission estimation rather than enforcement. Do not
promote them based on names or comments.

## Existing Maelstrom counterparts

Paths below are relative to the repository root.

| Maelstrom source | What is concretely present | Limit |
| --- | --- | --- |
| `ProjectMaelstrom/ProjectMaelstrom/Utilities/OperationScheduler.cs` | Capacity 32, one queued worker, versioned requests/receipts, timeouts, cancellation, cleanup-failed state and shared recovery hold. | Durable scheduling is not evidence that a game objective completed. |
| `.../Utilities/Modules/OperationModule.cs`, `ModuleRunner.cs` | Versioned module/bundle data, observations, decisions, receipt handling. | Currently supported versions are explicit; compatibility migrations still need deliberate policy rather than accepting arbitrary future fields. |
| `.../Utilities/Modules/ModuleOperation.cs` | Offline review has no input transport; trusted owned work requires the exact scheduler-held input lease; admitted bundle and digest are saved. | Loading a bundle cannot supply arbitrary executable callbacks. Owned admission explicitly does not qualify a game outcome. |
| `.../Utilities/WizwalkerOperation.cs`, `WizwalkerWorker.cs`; `tools/wizwalker_adapter.py` | Optional selected-client adapter with source/worker/client pins, target instance identity, bounded request, shared owner, explicit network exclusion and disconnect evidence. Cleanup uncertainty latches recovery before report I/O. | This inspected operation is a loading observation, not a generic execution route or an automatic endorsement of all wizwalker capabilities. Other adapters must be qualified separately. |
| `.../Utilities/JobDeckHost.cs` | UI/API-side commands connect to deck service; named-pipe request/reply boundary; preview facts do not update live observations or dispatch. | A card's presence in the library is not proof its full gameplay implementation is runnable. |
| `.../Utilities/CombatGeometry.cs`; `tools/wizsdk_combat_geometry.py` | Native and Python ports of legacy card/enchantment geometry. | Fixed coordinates survive in both. Do not confuse arithmetic correctness with calibrated screen coordinates across resolutions/UI sizes. Preserve one production implementation and an independent fixture/reference if useful. |
| `.../Resources/PetDance/default-v1/PROVENANCE.txt` | Bundled visuals/audio are traced to local demonstrations; hashes and a qualified 1280×720 Large-UI context are recorded. | Other geometry requires its own evidence. This is not blanket all-resolution qualification. |
| `tools/import_w101_knowledge.py` | Bounded, offline, parameterized SQLite import for a specific reviewed snack ledger. | This is not a populated universal game database or a generic importer for all archived datasets. |

These are stronger foundations than a container that simply starts old bots.
Some direct legacy adapters and ports remain; they should be admitted through the
same boundaries instead of retaining private global-input loops.

## Recommended layered contract map

```text
Source adapters / capture / optional W101 client observations
    → normalized observations + target/time/source identity
    → state facts + explicit unknown/stale/conflicting evidence
    → card conditions and specialist proposals
    → scheduler admission + budgets + exclusive control owner
    → bounded action adapter
    → dispatch receipt
    → independent observed outcome
    → experiment record / pack candidate / user-visible explanation
```

UI, CLI and MCP should call the same application service and read the same
capability/status catalog. A pack should bind known action IDs and parameters,
not executable source. A successful dispatch remains separate from a verified
outcome. Automatic retries need outcome-aware idempotence rules, especially for
quest acceptance, purchases, snack consumption and other nonrepeatable effects.

For each boundary record: schema/producer version, input/output kinds, source
hash, target/session generation, clock and age limits, resource budget, supported
capabilities, error/unknown reason, and evidence references. Versioned golden
fixtures should prove old supported packs still decode and obsolete packs fail
with an actionable migration message. This is how updates become local changes
rather than opaque changes to a single large loop.

## Legacy reuse: maximize useful functions, not runtime count

The machine-readable inventory
[`research/w101-legacy-work-packages.json`](../../../research/w101-legacy-work-packages.json)
records `root_inventory_count=25`, `mapped_count=25`, `mismatch=false` and 88
recursive compressed items (including builds/dependencies, not 88 independent
source projects). Its `complete=false` fields describe the planned integration
target, not the absence of all working subcomponents.

| Imported family | Best reuse | Remaining work / disposition |
| --- | --- | --- |
| Pet Dance / Wizard101_DanceBot | Sequence recognition, teacher data, independent comparison with native visuals/audio. | Keep native phased player, shared transitions and current evidence; qualify complete visits and size changes. Do not add another independent loop. |
| wizwalker | Reviewed optional observations and carefully scoped future operations. | Existing adapter demonstrates proper ownership/disconnect. Grow one capability at a time with compatibility probes and fixtures. |
| Deimos main/master, Automatus, raid utilities | Route/subgoal formats, conditions, recovery strategies, combat policies. | Main/master are distinct providers/revisions. Extract finite state/policy data; native route arrival and enemy avoidance remain separate qualification targets. |
| wizSDK, wizAPI, Farming, afk-wizard | Card geometry, visual-state hypotheses, turn policies and utility algorithms. | Consolidate normalization and tests; old fixed resolution, “missing means battle,” global input and endless loops must not become default runtime behavior. |
| Damage Calculator | Independent arithmetic reference and differential tests. | Repair known mutation/enchant/resist/boost issues. Current `ModernCombatCalculator` claims and hardcoded formulas require current mechanics evidence, not merely passing self-consistency tests. |
| Bazaar TC farming, gardening | Search/transaction and resource-aware routine states. | Require fresh inventory/stock/energy facts, clear spending rules and observed transaction outcomes before scheduling them. Category labels are not completed bindings. |
| wizwad, libkiwad, Aurorium | Read-only format parsers, version/manifest provenance, differential parser fixtures. | Consolidate to one hardened production importer. Preserve other parsers for comparison; validate lengths, bounds, decompression and source versions. Installed game files stay read-only. |
| Utilities, WizWikiAPI, WizGallery, On-Wiz, raid/GrubNinja reference data | Sourced knowledge, screenshots, mechanics, chronology and provenance. | Import claims with dates/build/scope and contradictions; never treat historical data as fresh live facts. Avoid duplicating a web/database stack. |
| w101-proto-go, packet maps, moonlight-type protocol research | Offline saved-file schemas and research references. | No live network manipulation. Protocol tools are not required player runtime dependencies. Missing build dependencies and old offsets remain unqualified. |
| Cheat Engine, historical Maelstrom portable archive | Workspace, inspection and UX patterns; historical comparison. | Preserve as source/history. No second engine, driver, injector or nested old executable is needed merely to copy the UX. |

Other owner repositories: WorkBench/Library contribute artifact verification and
provenance patterns; Guild contributes lifecycle separation; Merlin contributes
optional offline teacher-routing explanations. The existing peer review documents
pattern adaptation, not wholesale source imports. Keep MyFortress/mobile/voice/
coding-agent stacks outside the default player until a concrete measured need.
MaaFramework and Airtest should remain comparison references unless saved-frame
tests demonstrate a gap that the current native recognition cannot fill. The
existing Airtest probe is explicitly synthetic and does not qualify game accuracy.

## Priority tasks to place on the central roadmap

1. **Reconcile current capability truth.** Derive callable surfaces, constraints,
   implementation/installation/live status and evidence links from one catalog.
   Repair historical archive claims; preserve their original evidence dates.
2. **Freeze the existing transformation contracts.** Document observation → facts
   → proposal → admission → dispatch → verified outcome. Add cross-boundary
   fixtures for stale sources, conflicting observations, cancellation and version
   mismatch; do not invent a parallel runtime.
3. **Complete one native combat connection.** Bind fresh client/UI observations to
   policy and target-bound action; validate mechanics and outcome evidence. Move
   duplicate legacy coordinates behind one calibrated geometry adapter.
4. **Make optional adapters replaceable.** Source/version probes, bounded worker
   outputs, source hashes, capability-specific fixtures and explicit disconnect.
   Never give the entire imported package implicit execution privileges.
5. **Strengthen offline experimentation.** Stable experiment ID, separate immutable
   attempt IDs, pinned pack/policy/context, conflicting-result rejection and
   comparison on independent games. Reuse Steward semantics, not its whole service.
6. **Populate knowledge incrementally.** Extend the reviewed claim/import pipeline
   with chronological mechanics/shortcuts/route assertions and current evidence.
   Connect source changes to invalidation of dependent pack assumptions.
7. **Measure efficiency before swapping engines.** Bound capture/recognition queues,
   prioritize freshest frames, run expensive teachers offline, and measure latency,
   memory and missed-event rate. Adopt delta gating/model admission only where an
   actual benchmark supports it.

## Documentation drift requiring coordinator reconciliation

- Legacy map entries still say Deimos/wizSDK/wizwalker are blocked by UI-only
  policy. The settled optional W101 exception supersedes that reason, and a
  reviewed wizwalker adapter now exists. Authorization still does not qualify
  every operation.
- The Pet Dance map describes an unqualified user workflow while the current
  bundled preset records specific live qualification. Preserve scope and context
  instead of erasing either historical limitations or newer evidence.
- Geometry helpers advertise “exact” coordinates and calculator comments
  advertise “modern” mechanics without constituting current validation. The
  `150.0` soft-cap threshold and asserted formula in `ModernCombatCalculator.cs`
  specifically need sourced mechanics review.
- Old reuse proposals and `W101_CAPABILITY_INTEGRATION_MAP.md` describe the
  scheduler-unification stage as “next” although scheduler/module bindings now
  exist. Historical reports must link to current board truth, not become competing
  roadmaps.

## Verification and handback

Verified local HEAD identities and commit timestamps; inspected primary source
contracts and current adapter bindings; compared structured legacy coverage and
historical reuse reports. No runtime/test/build/desktop result is claimed by this
read-only audit. Root owns central board updates and implementation selection.

No new runtime dependency is recommended for the immediate work. This report
selects bounded reuse tasks; it does not authorize a source replacement, deletion,
live run or packaging change.
