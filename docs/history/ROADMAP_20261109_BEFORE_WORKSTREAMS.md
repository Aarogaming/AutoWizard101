# Historical roadmap — preserved before workstream consolidation

This is an archived status snapshot, not the current execution plan. See ../ROADMAP.md.

# Maelstrom roadmap

**Execution course and current audit:** [DELIVERY_COURSE.md](../DELIVERY_COURSE.md).
That document sets the active dependency gates and supersedes older status prose
below where it conflicts with current code or evidence.

Scope: [product charter](../PRODUCT.md). All milestones below are planned unless
explicitly noted. Existing components are source material, not completed gates.

The native [experiment workbench](../EXPERIMENT_WORKBENCH.md) now connects frozen
packs, private Dodge comparisons, evidence preservation and gameplay-video
review. The optional [W101 home movement routine](../HOME_MOVEMENT.md) now has
bounded Start/Stop/Resume controls and pauses on desktop activity. Its real W101
movement and idle-timeout behavior still need qualification before prolonged live minigame sessions, then
qualify individual minigame packs. Home farming follows identified resources and
verified routes. W101 minigame execution and home farming remain planned.

Distribution milestone: the native home window and self-contained portable build
exist. The per-user installer/update/uninstaller now preserve a separate data folder,
use owned-file receipts, and offer non-overwriting portable-data migration. Actual
payload lifecycle checks run in isolated locations; clean-machine and signing work
remain. See the root PACKAGING.md.

| Order | Deliverable | Acceptance gate |
| --- | --- | --- |
| 0 | Consolidate intent and source evidence | Current charter, architecture, Workshop and baseline distinguish implemented from planned. Documentation consolidation completed; runtime unchanged. |
| 1 | Shared contracts and offline harness | Target, observation, action, receipt and outcome records work with a fake target, clock and two distinct fixture types; no desktop input. |
| 2 | Recording and initial Workshop | Create a draft, import/record examples, label, define one skill, replay-test, save and reload without losing assets; failed saves preserve prior versions. |
| 3 | Observe/Assist overlay | Select a window; show current state and proposed actions; qualify geometry, DPI, capture contamination, stale frames and target closure. |
| 4 | Unified bounded input | One owner across native and worker paths; release held inputs on stop/focus loss; reject stale proposals; controlled trials verify manual takeover. |
| 5 | First small pack | Pet Dance is the leading reuse candidate; complete a bounded task with observed outcome and reproducible evidence. Existing backend success alone does not qualify the generic path. |
| 6 | Different interaction types | Home Navigation and Potion Motion candidates test spatial movement and pointer/board logic through the same core. Pack save/export/import and version-specific qualification work. |
| 7 | Real-time play | Diego tests unpredictable ordering, moving targets, deadlines and interruption. Recovered logic passes replay before live play. |
| 8 | Composed tasks and larger games | Multi-screen routines, then small standalone games, then longer questing/combat; checkpoints, recovery and verified subgoals remain bounded. |
| 9 | Optional learned specialists | Curated data, held-out evaluations and measurable improvement over rules justify model integration; preserve fallback and action ownership. |

These are dependency gates, not a calendar promise. Offline work on later packs can
supply fixtures early. Promote gameplay complexity only after preceding capabilities
are reliable; actual measured difficulty determines ordering within a stage.

The native Observe workspace now records bounded sampled image sessions, supports
manual per-frame state labels, and exports self-contained teaching draft packs.
Workshop opens their included session and preserves image evidence across edits.
An experimental whole-frame exemplar matcher now learns from exported image
examples, shows state/similarity/unknown in Observe, and evaluates separate labeled
recordings with exact training duplicates excluded. Existing-pack merging,
region/mask annotation and calibrated visual recognition remain future work.

The native Observe workspace now selects a running window, saves the most recent
workspace name, and previews Windows Graphics Capture with pause/reconnect and
target identity checks. Controlled-window tests cover occlusion, resize and target
loss. It can suggest visual states but does not bind packs for live play or send input.
Wizard101 live capture remains to be qualified. See [workspace instructions](../GAME_WORKSPACES.md).

Next implementation: region/mask labeling, difference-based region suggestions and
broader held-out image evaluation, then recognized observations and overlay
integration. The record/label/export and offline rule editing loops now exist;
robust visual perception and simulated action arbitration remain open.

Before further custom matching, follow the [MaaFramework/Airtest reuse review](../VISUAL_AUTOMATION_REUSE_REVIEW.md):
region/template authoring, existing native OpenCV baseline, then an isolated Maa
recognition-only probe. Preserve capture/input ownership and compare independent
game recordings before choosing a production backend.

Initial implementation now exists in `tools/game_session.py` and
`tools/replay_session.py`: schema-v1 manifests, bounded JSONL reading, increasing
sequence/nondecreasing monotonic time validation, and a read-only event summary.
Data-only pack drafts now have a schema check and immutable local save/load in
`tools/game_packs.py`, with `tools/create_game_pack.py` as a starter workflow.
The replay command can match labeled observation states to validated key or
normalized pointer proposals; two sample packs use different input types. Replay
prints simulated proposals without invoking input. These remain early data-only
building blocks, not image detectors, a fake executor, full pack contract or real
multi-game gameplay. `game_pack_workshop.py` now supplies an offline window for
create/open, key/click/wait rule editing, immutable version saves and asynchronous
evaluation. Focused checks cover save/reload, unchanged old versions, validation,
input-bound hashes and the window's evaluation workflow. This completes part of
milestone 2 together with the new Observe recording/labeling flow; richer
annotation and import/export workflows remain open gates.

For each live milestone record task success, intervention rate, false/missed
observations, stale actions, action latency and stop behavior. Set thresholds before
trials based on the task; synthetic tests alone cannot prove live performance.

The previous server/webhook roadmap is retained in [history](../history/SERVER_ROADMAP.md).
