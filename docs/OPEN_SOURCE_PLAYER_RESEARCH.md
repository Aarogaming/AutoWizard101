# Open-source research: building the player-building harness

Reviewed 2026-11-04. This is a source/documentation review, not a qualification
of any project on this machine. No repository here is a drop-in universal player.
Maelstrom remains a Windows app with its own window selection, capture, evidence,
game packs, modes and input authority. Do not add production dependencies without
the project's normal approval process.

## Closest matches

| Project | What to investigate | Fit and limitations | License as displayed upstream |
| --- | --- | --- | --- |
| [Game2Player](https://github.com/game2player/Game2Player) | Scout, builder, player, feedback, verified rollout and data-export contracts. | Closest to a harness that improves a player: its builder adapts web games and exposes game state; its coding player uses that state, while its VLM player uses screenshots. Those advantages do not automatically exist in a Windows game. Treat game-state-based scores as test oracles only when explicitly available; never claim screen-only generalization from them. | Apache-2.0 for original code, stated models and task specs; third-party games/assets have separate terms. |
| [ScreenQuest](https://github.com/DanielTea/screenquest) | Latest-frame handling, fresh-result guards, evidence, play/judge/tune/retest. | A concrete fast-path/slow-planner split with bounded rules and independent post-run checking. Mac/Core ML and one calibrated game, so source is architectural reference rather than a Windows runtime. Its own reports include failures. | MIT code; models and game assets separate. |
| [GamingAgent / LMGame-Bench](https://github.com/lmgame-org/GamingAgent) | Agent interface, episode logs, benchmark tasks and evaluation. | Useful evaluation and desktop-agent reference; LLM/VLM workflow is not evidence of low-latency Dodge control. Inspect action and frame boundaries before reuse. | MIT repository; check each asset/model separately. |
| [GameWorld](https://github.com/gameworld-project/gameworld) | Task definitions, progress, termination and action protocols. | Browser games and internal game state support reproducible outcomes; use ideas for a test adapter, not as the Windows capture path. A repository license was not found in the reviewed root page; verify rights before copying. | Unverified. |
| [Gymnasium](https://github.com/Farama-Foundation/Gymnasium) | Environment reset/step, observation, action, reward, termination and truncation. | A strong interface reference for controlled experiments; real Windows games may not reset or expose deterministic state. Model-free rules can implement the same concepts. | MIT. |

## Perception, annotation and automation

| Project | What to investigate | Fit and limitations | License as displayed upstream |
| --- | --- | --- | --- |
| [MaaFramework](https://github.com/MaaXYZ/MaaFramework) | Recognition-driven pipelines, ROIs, masks, branching and recovery. | Already reviewed in [our detailed comparison](VISUAL_AUTOMATION_REUSE_REVIEW.md). Consider a supplied-image adapter; do not add a second owner of capture or input. | LGPL-3.0. |
| [Airtest](https://github.com/AirtestProject/Airtest) | Template matching, reference resolution and assertion reports. | Our previous image-only probe exposed both useful matching and false-positive cases. Existing Emgu/OpenCV may cover the required primitives without the entire Python stack. | Apache-2.0. |
| [Game-Vision](https://github.com/Amit-Singh-P/Game-Vision) | Detection records, temporal association, motion smoothing, event logs and benchmark methodology. | Potential offline tracking inspiration. The reviewed page contains unresolved merge-conflict markers and shows a very short history; inspect and independently test before relying on it. Its object detectors and weights have separate requirements and terms. | MIT repository. |
| [Supervision](https://github.com/roboflow/supervision) | Detection data structures, video annotation, tracking and evaluation helpers. | Optional offline experiment; importing its full Python stack into the live Windows app is not justified without measurement. | [MIT](https://supervision.roboflow.com/latest/faq/). |
| [CVAT Community](https://github.com/cvat-ai/cvat) | Object/video annotation formats, tracks, review and quality assurance. | Good reference or optional external dataset tool; a browser/server-oriented application would be heavy as Maelstrom's default UI. | MIT community core; inspect extensions separately. |
| [FiftyOne](https://github.com/voxel51/fiftyone) | Dataset curation, hard examples and model evaluation. | Useful for larger offline datasets; not an always-on gameplay requirement. | Apache-2.0 repository; inspect optional components separately. |
| [GameCLI Agent](https://github.com/BjornBrorsson/GameCLI-Agent) | OCR/visual click verification, phase detection, recovery and run logs. | Windows-focused, but designed around LLM-led turn-based play; useful for the slow path, not evidence of fast play. | MIT. |

## Capture, execution and optional training

| Project | What to investigate | Fit and limitations |
| --- | --- | --- |
| [Microsoft's HWND capture sample](https://github.com/microsoft/Windows.UI.Composition-Win32-Samples/tree/master/cpp/ScreenCaptureforHWND) | Compare frame-pool lifecycle, device handling and resizing against Maelstrom's existing Windows Graphics Capture path. Confirm the sample's license before copying. |
| [DXcam](https://github.com/ra1nty/DXcam) | Compare Desktop Duplication behavior in a bounded test; it is a different capture approach with display/occlusion/privacy tradeoffs. No claim it fixes our game-specific throughput. Verify current license and window isolation. |
| [Stateless](https://github.com/dotnet-state-machine/stateless) | Guarded transitions and inspectable state machines for the harness; compare with existing data-only packs first. Verify license and dependency footprint before adoption. |
| [ONNX Runtime](https://github.com/microsoft/onnxruntime) | Optional compact model inference after an independently measured rule baseline. CPU/GPU provider and quantization performance are workload-dependent; model licenses are separate. |
| [imitation](https://github.com/HumanCompatibleAI/imitation) and [Stable-Baselines3](https://github.com/DLR-RM/stable-baselines3) | Optional offline behavior cloning or reinforcement-learning experiments after reliable trajectory and evaluation contracts exist. This is actual model training, unlike tuning rules. Inspect licenses, workload and Windows compatibility before installing. |

## Proposed integration order

1. Extend the existing `tools/game_packs.py`, `tools/game_session.py`, and
   `tools/evaluate_game_pack.py` rather than inventing a parallel pack/replay
   system. They already validate data-only state rules and provide read-only
   replay, but do not yet provide a complete measured live control loop.
   Define a versioned game-pack contract for observations and uncertainty,
   conditions, bounded action proposals, expectations, and evidence-linked outcomes.
   Separate action dispatch from actual observed outcome; stale or unknown input
   must not silently become a successful result.
2. Use the existing sampler and recordings to replay candidate rules without game
   inputs. Compare a baseline and a candidate on held-out sessions, including
   missed detections, latency, resource use and negative cases.
3. Evaluate a Dodge specialist against saved frames and then a short, bounded test
   only when the system is healthy. A stock Godot test can optionally expose game
   state as a test oracle; the player itself should use screen-derived observations.
4. Add a slow model-assisted authoring provider later. The model proposes pack
   changes; deterministic validation, offline checks and separate confirmation
   decide whether to retain them. Tuning rules is not the same as training weights.
5. Only after a measured need, compare compact vision or policy models and
   specialized runtimes. Enforce explicit task budgets and avoid any model being
   loaded merely because Observe is open.

All recommendations are candidates. Their upstream claims, licensing, performance,
and integration behavior have not been verified by running them in Maelstrom.
