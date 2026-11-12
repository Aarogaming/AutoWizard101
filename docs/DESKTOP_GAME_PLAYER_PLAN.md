# Maelstrom: Desktop-Use Game-Player

Scope is consolidated in [PRODUCT.md](PRODUCT.md), execution order in
[ROADMAP.md](ROADMAP.md), and pack authoring in
[GAME_PACK_WORKSHOP.md](GAME_PACK_WORKSHOP.md). This document supplies architecture
and source evidence. The Workshop is a core feature, not a later optional utility.

2026-10-30. This is the controlling product/build plan following the broader
repository re-review. Maelstrom is a standalone desktop game player and assistance
overlay. Minigames are its initial curriculum; larger games are its progression.
The earlier reconstruction plan remains a source/repair appendix, not the scope
of the product. This review changes documentation only.

## Reuse findings under the broader scope

Pinned revisions remain in `repository-reuse-inventory.json`. This pass inspected
selected implementation files in the local clones, with emphasis on Maelstrom and
Guild, and reconsidered the earlier 17-repository review. It is not an exhaustive
audit or runtime certification of every repository.

| Area | Inspected source and evidence | Build decision |
| --- | --- | --- |
| Overlay | Maelstrom `src/ProjectMaelstrom/OverlayHudForm.cs` and `Utilities/Overlay/{IOverlayWidget,OverlayWidgetRegistry,OverlaySnapshotHub}.cs`: real WinForms HUD, stop callback, widget registration and snapshots. These also exist locally. Old snapshots hardcode Live/allowed; local snapshots read ExecutionPolicyManager. | Extend local overlay. Do not overwrite it with the older version. Add actual target, observation age, mode, intended action and observed outcome. |
| Desktop capture | Maelstrom `Utilities/Capture/CaptureProvider.cs`: concrete region CopyFromScreen and form DrawToBitmap. Guild `plugins/game_automation/capture/screenshot.py`: real Pillow capture, optional region, whole screen fallback. | Keep one target-window capture service. These are starting implementations, not proof of occlusion-free capture. Add target identity, client-area geometry, DPI transforms and capture validity. |
| Demonstrations | Maelstrom ImitationCaptureService/InputRecorder plus Guild `scripts/game/analyze_imitation_session.py`. | Recover shared session recording/analysis; keep Diego extraction as one optional processor. Apply the previous race, stop, timestamp and malformed-input repairs. |
| Adapter boundary | Guild `plugins/game_automation/adapters/interfaces.py`: GameAdapter declares goto, send_key, get_position and get_game_state. | Adapt the separation, but avoid requiring world coordinates from every game. Desktop capabilities must include pointer, buttons, keys, holds/releases and scrolling; navigation is optional. |
| Action definitions | Guild `plugins/game_automation/actions/catalog.py`: concrete names/categories/aliases. `control/validation.py`: typed fields and validators. | Good small source-adaptation candidates. Reject duplicate/ambiguous registrations; normalize consistently. Unknown/extra fields must fail execution validation where appropriate. Range validation must reject nonfinite numbers; current comparisons can accept NaN. |
| State and planning | Guild `core/game_automation/environment.py`, `state_analyzer.py`, `decision_engine.py`. State records are useful; invalid timestamps can become now, visual confidence can be constant, decisions select the first action and execute_action returns a status dictionary. | Adapt record concepts only with explicit freshness/unknown states. Do not import these as a working perception model or general player. An execution-shaped response is not evidence of input or game success. |
| Curriculum and replay | Guild `core/phase2/{curriculum,input_replay,window_capture}.py`: each wraps supplied payloads as stored records. | These are scaffolds, not progression, playback or capture implementations. Build measurable progression atop the actual recorder and evaluation harness. |
| Navigation | Guild `plugins/game_automation/control/locomotion.py`: coordinate routes, Wizard speed and key assumptions, derived from Automatus. | Retain as Wizard-specific material. Visual navigation for other games requires independent work; do not make Wizard coordinates part of the shared core. |
| Specialist | Guild Diego trainer, existing DanceBot and Deimos adapters. | Each is an activity implementation behind a common contract. Qualify independently; no one specialist defines the host. |
| Alternative floating UI | Maelstrom `Client/merlin_overlay.py`: Tk floating chat prototype with Merlin service coupling and fixed placement. | Inspiration only. Avoid introducing another overlay runtime or service dependency when WinForms already provides the shell. |
| Evidence and catalog | Workbench digest/UI audit tools, Library catalog, Devtools verification concepts reviewed earlier. | Use existing local audit tools; adapt provenance, attempt identity and artifact indexing. Do not import their host frameworks. |
| Model services | Merlin routing/scoring extensions reviewed earlier. | Optional slow planner/model provider after baselines and evaluation exist. No dependency for capture, overlay or ordinary rules-based play. |

The remaining projects retain their earlier disposition: Aaroneous/AAS supply
coordination ideas, not a mandatory host; AndroidApp could eventually be a remote
status surface; MyFortress a future remote gateway; chatterbox optional speech.
opencode's desktop shell is not a gameplay controller and does not justify a shell
rewrite. Alexa, the circuit simulator and website have no established initial
runtime role. Larger scope alone does not make every repository a dependency.

## Shared player versus game knowledge

The shared loop is **observe → estimate state → choose skill → propose action →
validate/execute → observe outcome**. The overlay and session recorder subscribe
to this loop. Neither becomes a second input controller.

Keep the current C# Windows shell and supervised Python workers. Define a small
versioned boundary before introducing more frameworks. Proposed records:

| Record | Required meaning |
| --- | --- |
| Target | Session-bound process/window identity, client bounds, DPI/coordinate transform and supported capture/input capabilities. A title alone is insufficient. |
| Observation | Unique ID, sequence, monotonic capture time and clock origin, target/geometry revision, frame reference, detector provenance, text/entities and explicit unknowns. |
| Skill request | Game/profile version, task, preconditions, supported mode, resource/time limits and relevant recent observations. |
| Action proposal | Unique attempt/action IDs, source observation, target, primitive or bounded skill, coordinates in a named space, duration, expiry and expected outcome. |
| Action receipt | Accepted/rejected/dispatched/interrupted status, timing and released inputs. Dispatch is separate from verified outcome. |
| Outcome | Observed success/failure/unknown, evidence references and reason. A score increase, screen transition or completion signal must come from observation. |

A game pack supplies identification hints, screen states, bindings, detectors,
rules/models, skill definitions, success/failure checks, fixtures and supported
versions. Game names, enemy types, world positions and score rules stay there.
Core code must work with a fake pack and a second genuinely different activity.

Use two decision timescales: fast bounded controllers handle timing-sensitive
actions; slower planners choose goals and skills. Both share one action arbiter.
A planner's delayed response cannot execute against an expired frame. SLMs are
optional providers; multiple specialists do not by themselves constitute a trained
mixture-of-experts model.

Screen/OCR plus ordinary desktop input is the portable baseline. Existing
game-specific structured-state integrations remain explicitly labeled optional
capabilities. Their success must not be reported as proof of screen-only play.

## Overlay and desktop behavior are first-class work

Start with select window → choose activity → Observe / Assist / Play → Stop.
Observe records without controlling; Assist shows proposals; Play executes only
supported, qualified skills. Present the current mode and uncertainty plainly.
Keep detailed debugging in an expandable view.

The initial overlay needs window anchoring, DPI-aware positioning, deliberate
interactive versus click-through regions, UI-thread updates, and clear pause/stop.
Do not let clicks intended for the overlay become game input. Focus loss, target
closure, geometry changes and stale frames must invalidate pending actions and
release held buttons/keys. Manual takeover must stop the controller reliably.

Capture must not mistake the overlay for game content. Evaluate window-only capture
or exclusion/masking behavior; CopyFromScreen alone does not establish this.
Qualify borderless/windowed behavior first. Exclusive fullscreen, minimized and
occluded windows remain unsupported until demonstrated. Replays must never silently
enable desktop input. No new graphics-capture dependency is selected by this plan.

## Progression: gain capabilities, then increase game size

These are proposed stages, not claims that named games are already supported.
Exact second-game selection follows rules/footage and capture evaluation.

| Stage | Initial workload | What it proves before advancing |
| --- | --- | --- |
| 0: Observe and explain | Offline recordings, fake target, then user-selected live window | Generic session format, overlay state, geometry, replay and no accidental input. |
| 1: Bounded minigame | Pet dance is a candidate because a backend exists; compare it with existing Diego material | One complete task: identify start, react to observations, stop, recognize result. Treat existing backend behavior and new generic-path behavior separately. |
| 2: Different interaction type | A pointer/board-style minigame, selected after inspection | Same core works with pointer actions and board state, not just keyboard timing. Adding it primarily adds a pack. |
| 3: Reactive real-time game | Diego is the strongest recovered-source candidate | Random enemy order, tracking, deadlines, deduplication, interruption and reliable input release. It can be tested earlier in parallel with offline foundations. |
| 4: Multi-screen activities | Menu → minigame → result → retry; bounded farming activity | Skill composition, screen transitions, recovery, resource limits and user takeover. |
| 5: Small standalone game | A supported 2D game with movement, goals and clear checkpoints | Cross-game transfer of the host, visual navigation, camera/scene changes and longer memory. No Wizard client assumptions in the core. |
| 6: Larger games/tasks | Quest chains, inventory, combat and extended exploration | Hierarchical plans, persistent task state, verified subgoals, resumability and bounded recovery across sessions. Qualify game by game. |

For each stage, retain a fixed regression corpus and held-out sessions. Measure
task success, false/missed detections, observation-to-action latency, stale/repeated
actions, intervention frequency, stop latency and outcome-verification accuracy.
Set task-specific thresholds before trials; do not invent universal latency or
success targets without measurements. Compare learned policies against simple
rules/manual demonstrations. Randomize order, timing and display conditions.
Synthetic replay tests logic; it cannot establish real desktop latency or live wins.

## Revised implementation order

1. Define the minimal target/observation/action/outcome contracts and pack boundary.
   Prove them using a fake target and two different fixture types. Extend existing
   records where possible rather than adding a second competing framework.
2. Recover the recorder/session reader as game-neutral infrastructure. Make Diego
   events an extension, preserving the existing single-frame report consumer.
3. Connect Observe mode to the existing overlay and target-window service. Qualify
   capture geometry, overlay contamination, focus and stop before enabling inputs.
4. Consolidate bounded input ownership and held-input cleanup across workers and
   native paths. Exercise a fake executor, then controlled desktop trials.
5. Qualify a small activity pack end to end, followed by a different interaction
   type. Recover Diego logic behind the same boundary; do not expose its event
   names or timing assumptions as mandatory core fields.
6. Add skill composition, searchable recordings/run evidence and progressively
   larger-game packs. Add learned planners only where evaluation shows a benefit.

The first deliverable is therefore a **game-neutral Observe/Assist foundation with
replay and an overlay**, exercised by minigame fixtures. The recorder/reader remains
valuable first recovery work, but sits inside that broader milestone.

## Limits of this review

No runtime source was imported or changed in this pass. No desktop/gameplay trial
or upstream test suite was run to validate the newly reviewed components. Existing
local tests check the current implementation, not this proposed general player.
The detailed capture/executor/Diego defects and source paths in
`MAELSTROM_2_RECONSTRUCTION_PLAN.md` remain prerequisites for recovering those files.
