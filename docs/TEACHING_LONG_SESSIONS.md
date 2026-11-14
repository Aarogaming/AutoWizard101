# Long mixed-gameplay teaching sessions

Owner requirement,2026-11-12: record ordinary30-60minute mixed W101 play, automatically section by zones/activities and extract reusable evidence. Do not require scripted micro-demonstrations. This is a required workflow, not currently qualified recording functionality.

## Existing pieces and actual limits

W101DemonstrationRecorder records selected-window video, target-focused allowlisted inputs and optional game-process audio, but caps at180seconds. PassiveTeachingSampler caps at60seconds/5fps; TeachingRecording stores at most600frames/1GiB. GameplayInputTimeline caps150000events. GPU recorder streams encoded video but retains timing collections. tools/game_session.py already defines game-neutral timestamped event/session contracts; its snapshot reads an event file capped16MiB. route_landmarks.py supplies bounded offline image alignment, not route authority. Reuse these components; do not create another input owner or treat still sampling as continuous video.

## Required session container

One session ID and monotonic origin; recoverable video chunks and event shards referenced by an atomic session index. Persist frame capture timestamps, input focus intervals, optional audio offset, optional qualified W101 UI/pose readings and missing-data intervals. Each chunk records source hashes, target identity, game/build/profile, geometry, actual frame rates/drop metrics and timestamp range. Preserve originals, do not duplicate video into every derived clip. Crash recovery must preserve completed chunks and mark the partial tail unverified. Stop on changed target, disk limit or resource budget; user controls audio/telemetry opt-ins. Never record background typing or unrelated windows.

## Distillation stages

1. Cheap live indexing: focus/loading/UI transitions and available zone/pose facts. No continuous large model or expensive OCR per frame.
2. Offline scene alignment and masking: separate HUD, world scenery, avatar, effects and moving entities. Camera motion, zoom, UI scale and zone changes invalidate naive background subtraction. Use motion-compensated background references only within compatible views; differences are observations, not semantic labels.
3. Segment by zone/activity with corroborating evidence and hysteresis. Preserve uncertain/unclassified spans, overlapping activities and disconnects. A loading screen alone does not name a destination. Long pauses do not imply game failure.
4. Extract candidate routes/landmarks, interaction sequences, menu decisions, minigame cues, combat actions and explicit success/failure/resource observations. Preserve actual input timing, before/after evidence, confidence, contradictions and exact source timecodes.
5. Distill into proposed pack examples and bounded routine graphs. Keep recorded behavior separate from inferred goals or causal effects. A successful human input sequence is not automatically a universal routine.
6. Review uncertain boundaries in a timeline, compare independent sessions, replay offline, then qualify bounded live runs. Never auto-promote uncertain learned actions into unattended gameplay.

## Resource and acceptance gates

Record compressed video; bounded queues; shard input/timing metadata; incremental checkpoint writes. Analysis resumes from checkpoints and has explicit CPU/GPU/memory/concurrency budgets. Lower-cost sampling/event detection runs live; heavier recognition queued offline. Report coverage and backlog rather than silently dropping evidence.

Acceptance:30minute and60minute mixed sessions with multiple zone/activities, stable memory, recoverable interrupted recording, synchronized actual timestamps, no background inputs, manual boundary ground truth, unknown labels retained, source seeking into all segments, repeatable candidate extraction across fresh recordings. Test capture gaps/restart/loading/camera motion/UI changes and disk exhaustion. Publish measured resource use and segmentation precision/recall; no universal recognition or sustained60fps claim without measurement.

First implementation order: chunk/session index and recovery -> unified timestamped sidecars -> zone/activity segmentation -> searchable review timeline -> evidence-backed pack/routine proposals. User can play naturally; optional bookmarks help but are not mandatory.

## Framework boundary: apprentice workflow

Maelstrom owns capture/timing, session indexing, uncertainty/provenance, experiment replay, capability/action discovery, scheduler ownership, resource limits, correction UI and versioned promotion/rollback. A game pack owns recognized zone/activity vocabulary, control bindings, transition signatures, resources, goal rubrics and qualified adapters. W101-specific UI hooks are optional producers, not requirements of the learning engine.

Use existing SessionEvent -> observation/fact catalog -> ModuleDecision/action proposals -> RoutineReceipt -> outcome evidence contracts. Do not create an independent action executor inside a learner. Separate demonstrated inputs, inferred intent, and verified effects. Proposed routines can be replayed against held-out sessions; recorded-video replay cannot counterfactually simulate the game engine. Alternative actions require an environment model or bounded live experiments. Promotion is per capability/context/version, never a blanket "game learned" flag. Keep corrections, failures, source spans, confidence and qualification limits with every learned artifact.

Idle is an optional human task delimiter, not deletion of evidence. Retain continuous timing and low-rate state evidence while reducing video sampling/encoding work only where recording supports it. Loading is a retained transition segment; connect predecessor/successor only with observed context. Do not infer zone identity from a loading screen. A camera/background model is local to a view/zone and must not absorb temporary objects or erase interaction cues.

## First implemented index, scope

`tools/session_activity_index.py` consumes the existing SessionEvent stream and builds sourced active/idle/loading/unknown intervals and nonexecutable loading adjacency candidates. Observations opt into payload.activity_context with source/scene/zone/activity fields. Unknown/missing source remains unknown. Events and segments have budgets, timestamps must be ordered and within declared duration, and source event IDs remain attached. Four tests include a synthetic hour with mixed activities, Idle boundaries, loading links, missing evidence and invalid clocks. This is NOT an automatic recognizer, recorder integration, full session UI or hour-long capture qualification. The existing snapshot reader's16MiB limit remains; a sharded streaming session index is next.


## Source increment: session timeline (2026-11-12)

Game workspace → **Long session / timeline…** pauses Observe before opening the
new editor. Select 1–60 minutes; recording remains opt-in. The existing GPU
recorder runs sequential two-minute chunks, with fresh input hooks per chunk,
optional game-only audio, and no input dispatch. The session index is replaced
atomically before/after chunks. Completed chunks survive a later failure; the
active chunk may be incomplete. Chunk boundaries can have gaps and are not
claimed seamless. The manifest stores the monotonic origin, exact process
identity/start time/window, chunk timing and actual video origins. Video/input/
audio timing sidecars remain authoritative for frame alignment.

Recording checks for 2 GB free before each chunk and stops at the next checkpoint
when session files reach 20 GB (one chunk can overshoot that soft limit). It does
not retain an hour of frames in memory. Close requests stop capture and wait for
finalization; the user can close again afterward. No model or new dependency.

The timeline has a time cursor, chunk ranges, explicit gap indication, activity
start/end markers, editable conditions/action IDs/outcomes and up to 4,000
characters of commentary per activity. Video opens in the system player; this is
not yet an embedded frame-accurate video editor. Export uses the existing
RoutineGraph contract plus a source sidecar with the annotation and session hash.
An activity currently compiles to one bounded action/outcome transition, not an
automatically learned multi-step routine. Action bindings are still required.

Replay invokes the existing offline RoutineReplay against a separately selected
session.json containing real observation/receipt evidence. Raw video, mouse/key
observations and human notes do not manufacture those receipts. This first UI
connection does not automatically derive scene evidence from the video or promote
a routine into a live game pack. Automatic segmentation, re-review proposal
mining, embedded video seeking, adapter telemetry capture, crash recovery UI
polish, and a real 30–60-minute capture qualification remain work.

### Synchronized game-state track (next adapter increment)

Treat telemetry like a timestamped instrument log alongside video. The PID is an
identity field, not the UI tree. Each adapter reading must include session-relative
monotonic time, adapter/profile version, target process/start identity, zone/visit,
source, validity and individual field observation times. Position/heading,
health/mana/energy, objective, UI controls, loading and Idle are optional facts;
unsupported/missing/stale values remain unknown. A frame lookup may associate the
latest preceding sample but must expose its age and never borrow a future sample.
Use change records plus bounded periodic full snapshots; capture faster motion
facts separately from slow resource/menu facts. No fresh UI-tree read on every
60-fps frame by default. Preserve loading/zone transitions and observation gaps.
Human commentary is a distinct annotation track, not measured state. Re-review
should propose rules with supporting spans, contradictions and holdout replay;
it must not silently convert repeated correlation into verified causation.
