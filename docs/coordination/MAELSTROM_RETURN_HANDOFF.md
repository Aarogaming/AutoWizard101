# Aaroneous → AutoWizard101: Maelstrom integration return handoff

Prepared 2026-10-30. Read-only structure proposal, not an adopted contract, implemented adapter, new dependency, claim, or gameplay qualification.

## Evidence inspected

Received `F:/Modlists/AutoWizard101/docs/AARONEOUS_HANDOFF.md`, the repository AGENTS.md, `Utilities/SnapshotBridge.cs`, `tools/diego_observer.py`, and the report/synthetic fixture portions of `tools/diego_vision.py`. The incoming handoff reports branch `maelstrom/restore-integrations`, HEAD `5b5c2400809dbcb9a146254036ffb86fce148094`, and substantial uncommitted/untracked work. This review did not independently verify its working-tree revision, rerun the reported 35 Python/63 .NET tests, inspect every backend, or launch a game. The reported counts and runtime outcomes remain attributed to the incoming handoff.

Host source was read from Aaroneous main `a3383f2feeeee32a47bcd11e34698cfc0d138131`. Devtools Guild operation semantics remain proposed. The owner-directed suite map is in open Devtools PR #261 and is not adopted product policy. No cross-project dependencies or messages were created by this review.

## 1. Slot mapping

There is no inspected ready-made, fully governed game-specialist plug-in slot. Existing host extension points can be composed after separate qualification:

| Existing host component | Actual interface inspected | Fit and limit |
| --- | --- | --- |
| `crates/capabilities/src/universal_tool.rs` | `UniversalTool`, `ToolDescriptor`, `ToolRegistry::register`, `call_by_name` | Candidate JSON-facing observer capability. Registry dispatch alone supplies no authenticated session, ownership, schema enforcement or effect authority. Both JSON and latent methods are required by the trait; unsupported latent execution must return an explicit error rather than fabricate a mapping. Names/opcodes need allocation review, not arbitrary registration. |
| `crates/api/src/lib.rs` | `UiCartridge::name`, `UiCartridge::render` | Candidate Rust view of an immutable observer report. Requires compiled Rust/egui code; not a Python or .NET plug-in ABI. |
| `crates/studio_hud/src/plugin_api.rs` | `PluginManager::load_cartridge(Box<dyn UiCartridge>)` | In-process registry, not a qualified dynamic loader. The module's documentation says it has no production call sites; this review did not establish a live mounting route. A dedicated composition-root integration is needed. Never pass an egui object or Rust trait-object pointer across .NET/Python/DLL boundaries. |
| `crates/platform_bridge/src/traits.rs` | `PlatformHost`, `VisualObservation`, `HidCommand`, `HidAction` | Future capture/input adapters. `VisualObservation` is a luminance grid, not the observer's richer labels/boxes/tracks. `HidCommand` does not carry a complete grant, expiry, target or operation association. |
| `crates/platform_bridge/src/game_player.rs` | `AutonomousGameAgent`, `PlaythroughState`, `evaluate_autonomous_step` | Existing game-related source, not an accepted reactive policy. It cycles recorded events by step count or emits exploratory mouse movement. Frame luminance change is used as a reward proxy, not task-success evidence. Do not wire the new observer into this policy by default. |
| Existing Devtools Steward verification/authority services | Existing job/receipt/independent review records | Future tasking/evidence integration; do not replace their records or treat proposed Guild lifecycle APIs as implemented. |

Source links: [capability interface](https://github.com/Aarogaming/aaroneous/blob/a3383f2feeeee32a47bcd11e34698cfc0d138131/crates/capabilities/src/universal_tool.rs), [UI interface](https://github.com/Aarogaming/aaroneous/blob/a3383f2feeeee32a47bcd11e34698cfc0d138131/crates/api/src/lib.rs), [UI registry](https://github.com/Aarogaming/aaroneous/blob/a3383f2feeeee32a47bcd11e34698cfc0d138131/crates/studio_hud/src/plugin_api.rs), [platform traits](https://github.com/Aarogaming/aaroneous/blob/a3383f2feeeee32a47bcd11e34698cfc0d138131/crates/platform_bridge/src/traits.rs), [game source](https://github.com/Aarogaming/aaroneous/blob/a3383f2feeeee32a47bcd11e34698cfc0d138131/crates/platform_bridge/src/game_player.rs).

## 2. Ownership map

- **AutoWizard101 retains:** domain recognition, example packs, calibration, supported-game logic, existing .NET Tools UI and Python/backend adapters. It can develop and run independently while these boundaries are qualified.
- **Maelstrom presentation:** consumes reports and renders goals, activity status, limitations and evidence. Wizard101 details remain in the domain adapter rather than shared presentation internals.
- **Merlin:** future explicitly selected inference/retrieval services. No model installation or model-dependent gameplay capability is established by this handoff.
- **Guild/Steward:** eventual operation admission, job association, attempts and independent disposition. Loading an adapter does not start work or grant permission.
- **Marionette/Platform Bridge:** future target-specific capture and input execution. External backends currently own their own input; the host cannot promise exclusive ownership until those paths are explicitly adapted or excluded.
- **Library:** future artifact/provenance catalog, retaining image/report identity and evaluation history. The existing snapshot cache remains a replaceable projection, not global authoritative session state.
- **Chimera:** software development/build/integration operations around this consumer; not a second gameplay decision scheduler.

## 3. Contract mapping

Preserve the observer's existing JSON as a payload. Add a separately versioned integration envelope only after agreement and review; this avoids silently changing existing consumers.

| Incoming concept | Actual existing representation | Necessary adapter work |
| --- | --- | --- |
| Capability | `ToolDescriptor` has name/opcode/description/parameters_schema/category | Define approved activity identity, interface version, prerequisites, supported mode and validation level; prevent duplicate name/opcode registration. |
| Observation | `diego_vision.report` has mode, inputSent, sourceSize, playArea, detections, tracks, suggestions and limitations | Preserve full report and add source digest, session/activity, sequence and documented timing through a reviewed envelope. Do not coerce object detections into a luminance grid and lose the source report. |
| Action proposal | Observer suggestions are report data; `HidAction` describes physical actions | No direct conversion in the first slice. Future proposals bind source observation, target, effect limits, expiry and authority. The executor rechecks them at dispatch. |
| Execution result | No common adopted result linking all these components was established | Retain rejected/executed/unknown separately; reference actual observations and evidence. A successful process exit or upload is not gameplay success. |
| Lifecycle | Existing game enum and proposed Guild operation state are different | Use explicit mapping and preserve separate operation, attempt, UI, backend and effect states. Do not rename an enum and claim semantic compatibility. |
| Artifact | Local frames/packs and JSON output; C# snapshot cache writes a temporary file and renames it | Identify immutable bytes, origin, licenses, dataset split and revisions. Atomic rename alone does not prove power-loss durability or multiwriter ordering. |

`VisualObservation.timestamp_us` is documented as UNIX epoch, while `game_player` produces command timestamps from a process-local elapsed clock. No cross-process ordering or expiry may assume these are the same clock domain. Capture-age checks need an explicitly reviewed receiving/execution-local time protocol.

## 4. Runtime plan

Use a supervised process boundary for the existing Python/.NET components. Keep their dependencies outside the Aaroneous Cargo dependency graph. The first host consumer imports a saved JSON report; a later sidecar runner may invoke the observer through an explicitly supplied executable, script, pack, frame and output location, with bounded runtime, output size and cancellation. No embedded Python runtime or Rust DLL ABI is needed for the first slice.

The inspected observer already supports `--pack`, `--frame` and `--output`. Headless mode still imports tkinter in its module, so the environment must satisfy that import until it is deliberately separated. The .NET UI is Windows-specific; that does not prevent offline JSON fixtures from being tested on other hosts. No Linux input backend, live capture, overlay, global scheduler or sandbox has been qualified here.

## 5. First vertical slice

1. AutoWizard101 produces one saved frame/report pair through its existing CLI and preserves the pack and source hashes. Begin with its synthetic fixture; use a separately reviewed, provenance-retained real example afterward.
2. A standalone Rust fixture importer consumes only that report. Validate mode `offline-observer`, `inputSent == false`, dimensions, ROI/box bounds, numeric finiteness, maximum report size and explicit unknowns; reject malformed data. Preserve similarity as similarity, not probability.
3. A Rust presentation adapter renders the source identity, objects, suggestions and limitations. Test it independently before identifying the actual Studio HUD mount site under a separate exact-path claim.
4. The displayed disposition is "offline observation produced/review required". It must not call `evaluate_autonomous_step`, start a backend or inject input.
5. Retain fixture inputs, expected output, actual commands, exit codes, report digest, source/patch provenance and independent review. This proves a consumer seam, not game-playing reliability.

Acceptance cases: missing/wrong version, unknown fields policy, malformed/oversized JSON, invalid geometry, non-finite similarity, previous-session/sequence data, synthetic labels, negative detections, random enemy order and cancelled import. Tests must exercise existing behavior, not merely mirror a proposed schema. The first fixture importer can remain independent until the host contract is accepted.

## 6. Coordination and next changes

**AutoWizard101 next bounded change:** preserve the real source/patch and required artifact inventory; document the observer report shape and add portable, synthetic JSON fixtures plus negative cases. Retain the independent application and avoid migration of working backends just for naming consistency.

**Aaroneous/Devtools next bounded change:** reconcile this mapping with existing capability, authority, Guild and UI work; claim one non-overlapping fixture/import scope. Confirm registry name/opcode ownership and the presentation composition root. Do not create a second router, executor or model host. Product source changes follow product AGENTS and the full native gate; Devtools changes use its own gates and independent review.

**Shared artifact:** a small versioned compatibility note and fixture set, with an authoritative home and pinned consumer copies generated or transferred by an explicit procedure. Do not independently maintain conflicting specifications. Breaking changes require versioning or an adapter.

## 7. Open decisions and gates

- Agree on the authoritative descriptor/envelope schema and stable activity ID; no numeric opcode is allocated here.
- Locate a qualified host UI composition root and read-only capability dispatch route. Existing registries do not establish end-to-end authorization or a loaded panel.
- Confirm complete dirty-source and ignored-artifact preservation, package licenses, reproducible environments and a portable dependency plan before transfer.
- Independently assess the current backend process lifecycle and who can stop or fence each input sender before any supervised control trial.
- Qualify stale-response rejection, idempotency at the effect boundary, release acknowledgement, clock semantics, focus loss, stuck-input cleanup, cancellation and unknown outcomes. A host permit cannot intercept a backend's direct keys by declaration.
- Keep unsupported activities visibly unavailable. The incoming example match rate, unit-test counts and source existence do not upgrade a capability to gameplay-validated or establish a general model.

This return mapping can be given to the AutoWizard101 project window by the user. No message was sent to another chat, and no repository, runtime, claim or deployment was changed.
