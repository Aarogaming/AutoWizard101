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

## Pathfinding, overlays and spatial architectures (reviewed 2026-11-18)

Reviewed by the coordinator against this checkout; corrections and full evidence in
[ARCH-REF-REVIEW-84](coordination/results/ARCH-REF-REVIEW-84.md). Licences below are
the GitHub API `spdx_id` read on 2026-11-18 unless noted. Ideas are imported, not
repositories (see [PARTIAL-REUSE-DOCTRINE-01](coordination/results/PARTIAL-REUSE-DOCTRINE-01.md)).

| Project | What to investigate | Fit and limitations | Licence as displayed upstream |
| --- | --- | --- | --- |
| [Baritone](https://github.com/cabaletta/baritone) | Cost-heuristic A*: action penalties beyond distance, dynamic threat weighting. | The transferable part is repulsive cost fields over roaming mobs — `tools/path_navigator.py` already has threat radii and sidewalk-yield logic. The pitch's "sidewalks are safe" premise is falsified by `W101-KNOWLEDGE-AUDIT-01`. Java/Minecraft; no code reuse planned. | LGPL-3.0. |
| [l2r](https://github.com/l2r-dev/l2r) | Compact Rust priority-queue patterns only. | 30-star early-dev MMO *server* emulator; its A* credits the generic `pathfinding` crate. Converting W101's 3D `.NAV` vector graph (32,767 vertices/250,000 edges in `NavigationMap.cs`) to a 2D grid was reviewed and rejected. | Apache-2.0 / MIT. |
| [GW2 Nexus Pathing](https://github.com/YoruDev-Ryland/GW2-Nexus---Pathing) | World-to-screen projection math (view + perspective matrix from camera data). | Note: it is an **in-process addon DLL** drawing via ImGui, not an out-of-process overlay; camera comes from MumbleLink (official IPC). The out-of-process pattern TacO used matches our boundary better; our `OverlayHudForm` has no projection yet. | MIT. |
| [ExileApi-Compiled](https://github.com/exApiTools/ExileApi-Compiled) | Defensive, read-only entity-list traversal patterns for roaming-mob facts. | Repository is a compiled-binaries fork (no source, no licence field) — study the source upstream (`TehCheat/PoEHelper`) instead. Must not blur `Facts != Authority != Actions`: mob positions are facts only. | No licence asserted; source-only study. |
| [BOT-MMORPG-AI](https://github.com/ruslanmv/BOT-MMORPG-AI) | Vision/demonstration-learning pipelines; multi-label combat action schemas. | Optional *offline* vision fallback reference for the workbench tools; Jupyter-notebook research repo, not a runtime. Its "takes over while you sleep" framing is out of scope — Maelstrom stays bounded, leased and takeover-aware. | **Apache-2.0** (pitch claimed MIT). |
| [Polygames](https://github.com/facebookarchive/Polygames) | Self-play training harness interface for a local duel-circle simulator. | URL in the source pitch (`facebookresearch/Polygames`) is **dead**; project is **archived** (last push 2021). Reference only if a Rust combat simulator is ever built; no adoption path now. | MIT, archived. |
| [PyWinAssistant](https://github.com/a-real-ai/pywinassistant) | UIA bounding-box spatial graphs; window move/resize/focus recovery. | The tree uses **no UIA at all**, and window-rect recovery already exists natively (`Main.cs` GetWindowRect usage). A UIA *observation* adapter is a possible future fact source — it would be Facts only, never authority. | **MIT** (pitch claimed Apache-2.0). |
| [TagUI](https://github.com/aisingapore/TagUI) | Visual automation and terminal-driven flow patterns. | Canonical owner is `aisingapore/TagUI` (pitch's `kelaberetiv` URL redirects). RPA tooling for general desktops; no W101-specific value beyond flow syntax ideas. | Apache-2.0. |
| [OpenRPA](https://github.com/open-rpa/openrpa) | Orchestration-layer design (OpenFlow) for scheduling bounded jobs. | C#/.NET; our `OperationScheduler` already serializes jobs with receipts. The pitch's "fleet of coordinated W101 accounts" conflicts with the single-owner input lease and is out of scope. | MPL-2.0. |
| [Nav2 (navigation2)](https://github.com/ros-navigation/navigation2) | Costmap2D layered costmaps: static + inflation + dynamic-obstacle layers. | **Implemented natively in pure Rust** (`crates/maelstrom-core/src/costmap.rs`, claim `W101-COSTMAP-01`): 3 layers (Static geometry zone penalties, Footprint & clearance inflation, and Dynamic mob radar with forward velocity trajectory projection and inverse-square repulsive field). No C++/ROS FFI or external dependency added (`maelstrom-core` depends on serde, uuid and the optional thiserror, flate2 and base64); zero-stub mathematical core. **Library code and offline tests only:** `Nav2Costmap` has no caller outside `costmap.rs` and its tests. Canonical owner `ros-navigation`. | Repo licence field *Other/NOASSERTION*; Apache-2.0 stated in project docs. |
| [RTAB-Map](https://github.com/introlab/rtabmap) | RGB-D SLAM, appearance-based loop closure. | Pitch assumes a DirectX/OpenGL depth-buffer hook — a graphics-hook surface no reviewed integration uses (capture is out-of-process Windows.Graphics.Capture in `WindowObserver.cs`) and would need explicit exception scope. Depth-from-game also needs camera intrinsics/odometry the client does not expose. | Repo licence field *Other/NOASSERTION*; BSD claimed upstream — verify LICENSE file before use. |
| [BehaviorTree.CPP](https://github.com/BehaviorTree/BehaviorTree.CPP) / [bonsai](https://github.com/bonsai-bt/bonsai) / [behave](https://github.com/rust-lang-nursery/behave) | Hierarchical Behavior Tree execution, priority fallback selectors, and reactive branch preemption. | Hardcoded state machines fail under nested interruptions (*Questing -> Pulled into Combat -> Low Mana -> Need Potion -> Stuck Recovery*). Behavior Trees evaluate priority hierarchies on every tick: when a higher-priority condition activates (`in_combat == true`), lower branches (`Navigate`) abort cleanly without ad-hoc flags. **Implemented natively in pure Rust** (`crates/maelstrom-core/src/behavior_tree.rs`, claim `W101-BEHAVIOR-TREE-02`) to avoid heavyweight C++ FFI or unvetted external crates. Evaluates against a typed `Blackboard` of verified facts; emits high-level action proposals adhering to `Facts ≠ Authority ≠ Actions`. | MIT (`BehaviorTree.CPP`, `bonsai`, `behave`). |
| [dogoap](https://github.com/victorb/dogoap) | Goal-Oriented Action Planning (GOAP) with dynamic A* graph search over world state transitions. | Excellent conceptual reference for prerequisite chains (*farm reagents -> wait/switch realm -> clear mob -> turn in*). For fast reactive recovery, Behavior Trees provide cheap priority preemption (about 40-60 ns per tick of the canonical tree in a one-off probe on 2026-11-20; no committed benchmark); GOAP can serve as an offline multi-step quest planner producing goal inputs for the BT blackboard. Not wired today: no code calls the planner outside tests and the blackboard cannot hold a plan (see the status in [GOAP_ACTION_PLANNING.md](GOAP_ACTION_PLANNING.md)). | MIT / Apache-2.0. |
| [PythonRobotics](https://github.com/AtsushiSakai/PythonRobotics) (Nav2 DWB / Regulated Pure Pursuit) | Local steering controllers: lookahead path tracking, curvature-bounded turning, Dynamic Window Approach (DWA). | Translates NavMesh waypoint sequences into smooth, human-like continuous keyboard pulses (`W`, `A`, `D`) rather than jerky tank turns. Coupled with an odometry watchdog ($\Delta \text{position} < \epsilon$ while moving for $>1.5\text{s}$) to trigger stuck recovery. **Implemented natively in pure Rust** (`crates/maelstrom-core/src/steering.rs`, claim `W101-STEERING-01`). Library code: the offline autonomy bridge calls the controller for `StepWaypoint` intents, but `OdometryWatchdog` has no caller, so nothing sets the blackboard's `is_stuck` outside tests. | MIT. |
| [cpal](https://github.com/RustAudio/cpal) + [rustfft](https://github.com/ejmahler/RustFFT) | Zero-memory, zero-OCR event detection via per-process Windows WASAPI loopback audio stream. | Detects distinct game audio cues (fishing bobber splash, combat initiation fanfare, dialogue completion, pet game chimes) immune to lighting, camera angles, and memory offset shifts. **Implemented natively in pure Rust** (`crates/maelstrom-core/src/audio_cue.rs`, claim `W101-AUDIO-CORE-02`): discrete single-bin Goertzel spectral power filters and in-band SNR ratios operating over standard PCM buffers without heavy third-party audio runtime bloat. Feeds boolean event flags into the BT Blackboard. | Apache-2.0 (`cpal`) / MIT (`rustfft`). |

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
