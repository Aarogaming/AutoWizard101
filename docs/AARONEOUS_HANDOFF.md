> Historical coordination material. The current direction and plan are in [the standalone profile](MAELSTROM_STANDALONE_PROFILE.md). Aaroneous integration and further handoffs are not prerequisites.

# AutoWizard101 → Aaroneous: conceptual integration handoff

Prepared October 30, 2026. This document is intended to be given to the Aaroneous project window. AutoWizard101 is the contributing game-specialist project; Aaroneous is the future host. The user has identified an existing open slot in Aaroneous, but its interfaces and implementation have not been inspected from this workspace.

## Request to the receiving Aaroneous window

Read this as a capability and evidence inventory. Inspect the open slot the user identifies, map these capabilities into Aaroneous's existing architecture, and return a follow-up describing the intended structure. Reuse existing host services wherever suitable. Treat all interfaces proposed below as drafts, not implemented APIs or requirements to replace working Aaroneous abstractions.

The user's intent is to coordinate development now and merge at a later stage. Establish compatible boundaries early while allowing each project to continue independently. No cross-project messages, remote publishing, commits or merges have been performed as part of preparing this handoff.

## Shared concept

AutoWizard101 can become an activity-specialist module within Aaroneous. A coordinator chooses a specialist; the specialist interprets observed state and proposes or performs bounded actions through the host's approved execution path.

This is a mixture of specialists at the application level. It does not currently contain a trained neural mixture-of-experts model. A specialist may use rules, image matching, a small vision-language model, an existing automation backend, or a combination.

Proposed activity families:

| Family | Specialist responsibility |
| --- | --- |
| Fairground mini-games | Separate decision methods for Conjuration, Potion Motion, Sorcery Stones, Choo-Choo Zoo, Dueling Diego, Doodle Doug, Skull Riders and Hot Shots |
| Pet games/training | Recognize game state, manage selected game/snack workflow, respect energy and run limits |
| Questing | Follow objectives through navigation, dialogue and combat |
| Farming | Repeat a supported encounter and verify resource/stop conditions and observed outcomes |
| Planning/knowledge | Boss references, gear/deck planning and user-maintained progress |
| Maintenance | Compatibility reports from patch lists, type dumps and suitable saved packet captures |

**Reactive behavior is essential.** Enemies can appear in random orders. Tutorial timestamps and enemy sequences must never become a replay script. Decisions should use the current scene, recent observations, uncertainty and action outcomes. Different games need different timing and state models.

## What actually exists here

| Capability | Implemented state | Evidence and limits |
| --- | --- | --- |
| Tools interface | Windows launcher, setup/status, process supervision and stop controls | Local build prepared; owned process groups are closed when Tools stops them |
| Guided questing/farming | Adapters call real bundled Deimos routines with private configuration, prerequisite checks and bounded runs | Backend imports/config parsing and automated adapter checks pass. A live connection-only check read zone/health and released hooks. Full gameplay outcomes have not been validated |
| Guided pet dance | Adapter reuses real DanceBot methods, with selected count, locations and optional snacks | Offline checks and test backends pass. Actual dance gameplay has not been exercised. Supports 1280×800 and 800×600; snack feeding only at 1280×800 |
| Diego observer | Offline image loading, example teaching, template matching, short-term association, review prompts and JSON reports | No live capture, game connection or game input implementation in the observer |
| Tutorial starter pack | 19 local labeled examples from eight historical video frames, with source/annotation recipe | Example matching, not neural training. A separate check frame recognized both barrels but missed Diego's changed pose at the default 0.94 threshold. That frame also informed threshold exploration and is not an untouched final test set |
| Companion bridge | Read-only export of 1,851 boss entries and native optional-cache merge | Companion planning UI/core database smoke tested. Automatic quest-progress exchange and Companion OCR are not implemented here |
| Maintenance adapters | Saved patch/type comparison and suitable offline packet decoding | Synthetic/offline checks pass. No live packet capture or current-traffic decryption is claimed |

Other fairground games have a sourced research baseline, not functioning gameplay adapters. No small vision-language model has been installed, fine-tuned or benchmarked for this project. OCR has not been added to the Diego observer. Its likely role is supplemental reading of names, scores, menus and results; sprites, poses, movement and geometry need visual perception.

Latest completed verification in this workspace: **35 Python tests and 63 .NET tests passed**, and the local Tools build was updated. Those tests establish the covered software behavior, not reliable game-playing performance. A final native visual inspection of the observer could not finish because computer-use approval timed out; hidden GUI construction/navigation checks passed.

## Proposed host/module responsibilities — confirm against Aaroneous

| Concern | Proposed Aaroneous responsibility | Proposed AutoWizard101 responsibility |
| --- | --- | --- |
| Activity routing | Select specialist and coordinate task transitions | Declare supported activities, prerequisites and backend readiness |
| Shared state | Provide canonical sessions, observation references and resource facts | Adapt game-specific readings into the agreed representation |
| Screen/desktop services | Reuse host capture, overlay, OCR and input infrastructure where available | Supply game-specific recognition and calibration, avoiding a second desktop framework |
| Model services | Reuse host inference, model registry and training/evaluation facilities where available | Supply labeled examples, domain rules and task-specific evaluation cases |
| Execution ownership | Authoritative permission, lease, focus, cancellation and action dispatch | Propose bounded actions or wrap a backend that owns input explicitly |
| Player experience | Consistent lifecycle, status and stop behavior | Activity setup details, explanations and observed outcome reporting |
| Artifacts | Artifact references, provenance, retention and transfer rules | Calibration/example packs, reports and compatible backend manifests |

These are suggested seams. The receiving project should identify which services already exist and which pieces fit the open slot. Avoid creating a second router, input scheduler, model host or overlay if Aaroneous already provides one.

## Contract concepts to align before the late merge

Prefer Aaroneous's existing names and serialization conventions. The minimum information needed across the boundary is:

| Concept | Information to preserve |
| --- | --- |
| Capability declaration | Stable activity ID, contract version, supported modes, prerequisites, backend/version, calibration requirements, validation level and known limitations |
| Observation | Session/activity ID, source frame/reference, ordered sequence, capture age, coordinate space and dimensions, recognized objects, resource facts and explicit unknowns |
| Action proposal | Proposal ID, originating observation, intended bounded action, parameters, required focus/ownership, expiry and explanation |
| Execution result | Proposal ID, accepted/rejected/executed state, actual execution record, resulting observation reference and stop/failure reason |
| Specialist state | Lifecycle, current objective, resource requirements, pending handoff and terminal outcome supported by evidence |
| Learning example | Recent frames, source/time references, labels, recorded human or controller actions, resulting state, provenance and dataset split |

Key rules:

- Start with offline review, observation-only and suggestion-only modes. Actual control remains an explicit separate mode; loading a profile or model never starts play.
- An action proposal is data, not permission to execute. The executor checks the current session, observation age, focus, policy, allowed action set and exclusive ownership.
- Reject stale proposals and proposals from a previous session or specialist. A late model response must not control a different activity.
- Use an execution-local clock for expiry checks. Cross-process sequence IDs and documented clock semantics are needed; do not compare unrelated monotonic clocks.
- Make proposal execution idempotent. Transport retries must not duplicate a jump, click, purchase or activity start.
- Keep unknown/missing readings distinct from zero and from a negative detection. Image similarity is not a calibrated probability.
- Do not infer real-time velocity from the observer's comfortable offline replay interval. Preserve capture timing when it exists.
- Measure end-to-end latency before allocating timing-critical decisions to a model. Keep immediate control in a suitable fast loop if model latency is inadequate.
- Record outcomes conservatively: battle ended is not victory, score increased is not potion refilled, and stopped is not completed.
- Declare backend input ownership honestly. The current external routines send input themselves; routing those routines through a single host executor needs deliberate adaptation, not a claim that their keys are already intercepted.

The existing hub lease coordinates hub-managed automation only. It is not a global scheduler for native bots or separately launched applications. Tools pauses the trainer's task pump/input bridge while its modal interface is open. A host-wide ownership design remains integration work.

## Example future handoff: farming → refill → farming

This sequence illustrates coordination; it is **not implemented**:

1. Farming observes a resource condition and requests a refill objective.
2. The coordinator stops/suspends farming and receives confirmation that its input ownership is released.
3. A refill specialist validates game state, setup, permission and available method, then obtains ownership.
4. It performs the supported bounded workflow and verifies actual resource change.
5. It releases ownership and returns observed results. The coordinator revalidates the farming context before any resume.

Cancellation, low confidence, focus loss or failed verification should produce an explicit stopped/needs-attention result, not silently resume another specialist.

## How to stay aligned while development remains separate

1. **Map the slot first.** Return Aaroneous's existing component names, interfaces, process model and ownership rules. Identify which current files can become adapters and which remain standalone tools.
2. **Agree on a small versioned boundary.** Stabilize capability IDs, observation/action/result concepts and lifecycle behavior. Keep a revisioned interface note with both projects.
3. **Exchange contract fixtures early.** Use synthetic frames/state and mocked execution. Test schema evolution, unknown states, cancellation, duplicate proposals, stale observations and reversed/random enemy orders without touching the game.
4. **Continue independent work.** AutoWizard101 can improve game perception/calibration and backend adapters. Aaroneous can supply the host slot, routing, inference and execution services according to the agreed ownership map.
5. **Record interface changes explicitly.** Each handoff should state interface revision, source commit plus uncommitted patch status, added/removed capabilities, required artifacts, tests and known gaps. Breaking changes need a new version or an adapter.
6. **Merge by capability.** First integrate the offline Diego observer and artifact flow; then suggestion-only inference; then a supervised controller after validation. Integrate existing quest/farm/pet backends with their own capability gates rather than enabling every routine at once.

No recurring synchronization job is configured. The user is carrying this handoff between project windows. A follow-up should reconcile the design before creating cross-repository dependencies.

## Late-stage merge readiness

- The receiving project can load the capability declaration and consume a real offline observation/report through its existing slot.
- Both projects pass shared contract fixtures; ownership, expiry, cancellation and duplicate-action behavior are exercised.
- Required packages/backends have a reproducible installation plan and retained attribution/licenses.
- Model/example artifacts have provenance and a deliberate transfer plan. Private recordings and generated environments are not silently committed.
- Current-client perception evaluation covers independent recordings, random enemy order, unseen/negative examples, scale/pose changes and uncertainty behavior.
- A supervised control trial measures action timing and verifies outcomes, with an effective stop/focus mechanism.
- Unsupported activities remain unavailable in the host UI. Installing a library, loading an example pack or passing unit tests does not upgrade a capability to gameplay-validated.

## Source and artifact map

Workspace: `F:\Modlists\AutoWizard101`.

Branch: `maelstrom/restore-integrations`. Current repository HEAD when this handoff was prepared: `5b5c2400809dbcb9a146254036ffb86fce148094`. **The implementation described here includes substantial uncommitted modifications and untracked files. Checking out that commit alone will not reproduce it.** Preserve the relevant working-tree patch and new files, or create a reviewed integration commit at handoff time. No commit has been created by this handoff task.

| Area | Files |
| --- | --- |
| Native Tools/process supervision | `ProjectMaelstrom/ProjectMaelstrom/ToolCenterForm.cs`, `Utilities/ToolIntegrationService.cs`, `Utilities/OwnedProcessJob.cs`, `Main.Tools.cs` |
| Guided activities | `GuidedActivityForm.cs`, `Models/ActivityProfile.cs`, `tools/activity_runner.py`, `tools/activity_runtime.py`, bundled Deimos adapter hooks |
| Existing snapshot bridge | `Utilities/SnapshotBridge.cs`, `Scripts/Library/snapshot_reader.py`; inspect these actual formats before choosing how to map them to the host |
| Offline Diego perception/review | `tools/diego_vision.py`, `tools/diego_observer.py` |
| Tutorial annotations/build recipe | `tools/diego_tutorial_annotations.json`, `tools/build_diego_tutorial_pack.py` |
| Backend hub/maintenance | `tools/wizard101_hub.py`, `tools/moonlight_adapter.py`, `tools/wizdiff_adapter.py` |
| Tests | `tools/tests/`, `ProjectMaelstrom/ProjectMaelstrom.Tests/` |
| Documentation | `docs/DIEGO_OBSERVER.md`, `docs/GUIDED_ACTIVITIES.md`, `docs/MINIGAME_RESEARCH.md`, `docs/WIZARD101_INTEGRATION_GUIDE.md` |

Native file paths above are relative to `ProjectMaelstrom/ProjectMaelstrom/` unless a `tools/`, `docs/` or complete project prefix is shown.

`.local/` is ignored by Git. It contains isolated Python environments, upstream source checkouts, prepared executables, reference frames/packs and evaluation artifacts. The 1,851-entry generated Companion cache is also ignored. These are local artifacts, not automatically present in another checkout. The environment snapshot is an evaluation record, not a portable lockfile. Do not copy environments or assume local absolute paths are a deployment contract.

Some older restoration/integration notes describe earlier milestones and counts. For current activity limits, use the guided-activities and observer guides plus the evidence distinctions in this handoff.

## Requested return follow-up from Aaroneous

Please return a concise structure proposal containing:

1. **Slot mapping:** the exact existing Aaroneous slot/component and why AutoWizard101 fits it.
2. **Ownership map:** what stays in this module and which host services replace or wrap its standalone infrastructure.
3. **Contract mapping:** actual host interface names and a draft mapping of capabilities, observations, proposals, execution results, lifecycle and artifacts.
4. **Runtime plan:** in-process versus supervised sidecar boundaries, Windows desktop requirements, dependency/model handling and backend isolation.
5. **First vertical slice:** one offline tutorial frame/example pack → specialist observation → host display or suggestion, with no game control.
6. **Coordination decisions:** stable IDs/versioning, shared fixtures, source/artifact transfer and the next narrowly scoped changes for each project.
7. **Open questions and merge gates:** uncertainties that the identified slot cannot resolve, and the evidence required before supervised control.

Return that mapping to this window so the two sides can align around Aaroneous's actual structure rather than independently invent incompatible frameworks.
