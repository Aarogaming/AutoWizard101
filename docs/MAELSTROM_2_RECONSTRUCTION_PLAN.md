# Maelstrom 2.0: reconstruct the existing pieces

2026-10-30. Source review and proposed build sequence; no application behavior
changed in this pass. This plan takes precedence over the earlier generic reuse
ordering. Maelstrom remains a standalone application in AutoWizard101.

Scope update: [Desktop-Use Game-Player plan](DESKTOP_GAME_PLAYER_PLAN.md) now
controls architecture and sequencing. This document is the detailed recovery
appendix for the recording/Diego components; Diego is one game pack, not the host.

## Finding

The strongest recovery path is split between repositories: **Maelstrom contains
the recording and Diego event-extraction tools; Guild contains a reactive Diego
trainer and an analyzer for those recorded sessions.** This workspace already has
the launcher, supervised activity adapters, and an offline Diego observer.
Recover and qualify that chain before inventing another framework.

The reviewed Maelstrom application subset has 55 files identical to local files,
42 different, and 21 absent locally after line-ending normalization. This covers
tracked C#, project and resource files, excluding Scripts/bin/obj; it is not a
percentage of reusable functionality. MinigameCatalog is already identical.
No common Git ancestor was found for the compared revisions; selective file
recovery is more appropriate than merging entire histories.

All 16 reference repositories are cloned under `.local/reference-repos/`.
Revisions are recorded in `docs/repository-reuse-inventory.json` and the local
download inventory. The important revisions here are Maelstrom `bb6e3608ec0e`,
Guild `b3749df87140`, Workbench `7df1b0ca5ec3`, Library `9ee5442d0544`, and Merlin
`191b9371e20c`. These findings are static source observations, not successful
upstream builds or gameplay tests. The broader review screened all 17 accessible
repositories; it did not audit every file or historical branch.

## Recovery map

Source paths below are relative to each named reference checkout. Proposed local
homes are destinations for future work, not claims that imports have happened.

| Piece | Source | Decision and proposed local home |
| --- | --- | --- |
| Player launcher, guided activities, process cleanup | Current ToolCenterForm, GuidedActivityForm, ToolIntegrationService, OwnedProcessJob and Python activity runner | Keep as foundation. Extend existing services rather than replace them. |
| Session recording and controls | Maelstrom `src/ProjectMaelstrom/Utilities/ImitationCaptureService.cs`, `ImitationCaptureManager.cs`, `InputRecorder.cs`, and `ImitationControlForm.cs` | Recover into corresponding local application directories after recording lifecycle repairs. Reuse existing capture utilities. |
| Multi-object template matches | Maelstrom `src/ProjectMaelstrom/Modules/ImageRecognition/TemplateMatcher.cs`, added `FindMatches` method | Small selective backport candidate into the existing matcher; qualify ROI, template size and suppression behavior. |
| Diego event extraction and profiles | Maelstrom `src/ProjectMaelstrom/Utilities/ImitationEventExtractor.cs` | Recover for offline comparison first. Compare with `tools/diego_vision.py`; select one authoritative live observation path after evaluation. |
| Recorded-session analysis | Guild `scripts/game/analyze_imitation_session.py` | Best first source adaptation, proposed `tools/analyze_imitation_session.py`. Its frames/inputs/events JSONL files match the old recorder's outputs. Harden parsing and timing before adoption. |
| Reactive Diego scheduling | Guild `plugins/game_automation/trainers/dueling_diego.py` and `base.py` | Extract scheduling into a small local Python module with fake clock and adapter first. Retain event-driven decisions; replace unsafe lifecycle assumptions. |
| Existing pet dance implementation | Wizard101_DanceBot, already integrated locally | Keep one backend. Guild's alternative dance trainer is a comparison candidate, not a reason to add a second default player. |
| Catalog reader | Library `plugins/catalog/plugin.py`; Workbench `Tools/Core/DiscoveryEngine/library_catalog.py` | These copies are identical after newline normalization. Adapt one reader into the existing Library feature only when artifact indexing is needed. Validate paths and schema. |
| UI audit comparison | Workbench `Tools/UiAuditDiff/Program.cs` | Already identical to local `DevTools/UiAuditDiff/Program.cs`. Use the local tool; no new import. |
| Attempt receipts and recovery rules | aaroneous-devtools `foundation/crates/steward/src/verification.rs` and `local_verification.rs` | Adapt semantics into local run records. Do not introduce the Rust service or signing infrastructure as prerequisites. |
| Optional model routing and scoring | Merlin `merlin_routing_contract.py`, `merlin_quality_gates.py` | Later, extract small contracts. A scoring hook is an extension point, not a trained model or calibrated game evaluator. |
| Whole async executor and host transport | Maelstrom AsyncBotExecutor, AsyncBotTaskScheduler, gRPC and hub services | Do not transplant wholesale. Existing ownership/process controls are a better foundation; extract transport-neutral ideas only where needed. |

Preserve provenance and applicable upstream notices for copied files, including
third-party material in these repositories. Ownership of a repository does not
erase licenses of incorporated dependencies.

## Repairs that must precede reuse

### Recording and perception

The old capture service can reuse a directory and overwrite session outputs.
Timer callbacks can overlap, while stop disposes writers without awaiting every
callback. Event writes and frame indexing are not consistently protected.
Inputs are buffered until stop, so abrupt termination can lose them.

Use unique session directories, a non-overlapping capture loop, awaited stop,
incremental input records, and a manifest that marks incomplete sessions explicitly.
Keep frame/input/event times on a documented common clock. Do not infer time from
frame number. Test stop during capture and repeated session names before exposing
the recovered recording UI.

The extractor's template score is not a calibrated confidence probability. Some
metric confidence values are constants, and adaptive thresholds need evaluation.
Keep raw scores and provenance; validate on held-out footage, unknown objects,
different window sizes, occlusion and reordered enemies. The existing Python
observer remains useful as a comparison implementation and offline baseline.

### Scheduling and control

Guild's Diego trainer schedules observations by timing and deduplicates events;
it is a genuine starting point for random enemy ordering. However:

- Its success counters can count acknowledged key dispatch, not game success.
- Missing timing can cause immediate dispatch; clock origins need explicit rules.
- Queued actions need fresh scene validation before execution.
- The per-round event limit is reset per processing call in the inspected logic.
- The base trainer's start/stop conditions mishandle paused states, and calibration
  returns success after a delay without proving connection readiness.

Recover the decision logic behind a fake input adapter first. Test shuffled and
duplicate enemies, stale observations, clock discontinuities, unknown states,
pause/resume/stop, and bounded queues. Validate key mappings against the tutorial
and current client; source configuration alone is not proof of correct controls.

Maelstrom's old AsyncBotExecutor changes status on pause without gating the loop.
Stop can report stopped after a five-second wait while the task is still alive.
Retries can continue indefinitely. Its reviewed tests do not establish these
guarantees: the pause test lacks an assertion, and the throttle assertion permits
more work than the configured limit. Do not replace current supervision with it.

RuntimeOwnershipPlan is migration configuration, not a universal input lock.
GameCommandReadbackGuard checks a request fingerprint, not observed game results.
The current hub lease also does not cover every native legacy bot. Unifying those
input paths remains necessary before multiple specialists can share one client.

### Apparent functionality that is still scaffolding

Maelstrom's Python minigame modules and imitation pipeline contain placeholders.
Guild's `minigames/dueling_diego.py` is a re-export of its trainer, not a second
implementation. Guild's inspected learning pipeline returns training metadata and
a fixed evaluation score rather than learned player weights. Fishing/gardening
trainer methods also contain placeholders. None establishes pretrained gameplay.

## Build sequence and acceptance gates

1. **Recover the offline session reader.** Adapt Guild's analyzer to bounded,
   validated inputs. Report missing/malformed data explicitly instead of silently
   treating it as an empty successful session. Define a versioned session envelope
   with session ID, clock origin, source revision, frame references and completion
   status. Preserve the current single-frame report shape for existing consumers.
   Gate: synthetic valid, truncated, malformed, out-of-order and missing-file
   sessions produce explicit, reproducible results without a game or input hooks.

2. **Restore recording as a coherent feature.** Port the old capture cluster and
   focused UI after the lifecycle fixes above. Stay on the current .NET target and
   dependencies; do not copy the old csproj or pull in gRPC to record a window.
   Gate: complete or explicitly incomplete sessions remain readable through stop,
   capture failure and repeated starts; user-initiated capture is bounded.

3. **Qualify perception on the same recordings.** Run the recovered extractor and
   current observer against a shared labeled corpus. Measure missed/false objects,
   timing errors and latency; establish thresholds from evidence. OCR reads text
   such as instructions and scores; visual detection/tracking handles moving
   enemies. Gate: select an authoritative observation path using held-out results,
   with explicit abstention on unknown scenes.

4. **Recover Diego decisions in recommendation mode.** Adapt Guild's scheduler
   to the session contract and a fake clock/input adapter. Feed actual observations,
   not a memorized enemy sequence. Gate: randomized replay and lifecycle tests pass,
   stale actions are rejected, and dispatch acknowledgments remain distinct from
   observed outcomes. No model training is required for this milestone.

5. **Connect one supervised live controller.** Route qualified decisions through
   existing policy, focus, process ownership and stop controls. Extend ownership to
   native paths before allowing competing controllers. Add per-attempt receipts
   and observed outcome checks. Gate: controlled live trials demonstrate stop,
   focus-loss handling and results; a connection probe is not gameplay validation.

6. **Compose the friendly interface, then expand.** Activities selects a task;
   Training records/reviews sessions; Library holds profiles/examples; Runs shows
   evidence and outcomes; Setup checks readiness. Build on existing forms and
   services. Bring questing, farming and pet dance under the same run contract,
   qualifying each backend independently. Add another minigame only after its
   observations, decisions and controls have equivalent evidence.

7. **Add optional learned specialists when data justifies them.** Use curated
   sessions and measured baselines to evaluate an SLM for interpretation or
   planning. Merlin can explain model selection and fallback. A router among
   specialized controllers is useful before a trained mixture-of-experts exists;
   do not describe it as a pretrained MoE. Keep timing-critical actions bounded
   and verified regardless of which planner proposes them.

## What we actually need to create

Most new work should be the connections: a versioned recorded-session contract,
clock/freshness handling, a narrow input adapter, repaired lifecycle behavior,
attempt evidence, and UI composition. Existing forms, capture algorithms,
event scheduling, game backends and audit tools supply substantial source material.
No credible reuse percentage or completion estimate follows from file counts alone.

Merlin, Library, Workbench and Devtools contribute focused pieces; Aaroneous is not
a mandatory host. AndroidApp, MyFortress, opencode, chatterbox, Alexa, the circuit
simulator and the website do not need to enter the initial gameplay build.

The next implementation should therefore be **the recorded-session reader and
contract**, recovered from Guild and checked against Maelstrom's recorder. That
gives the fragmented components a testable connection before live behavior changes.
