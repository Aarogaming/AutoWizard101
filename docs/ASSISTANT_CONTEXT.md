# Assistant context — Maelstrom

W101-QUEST-STEP-18 completed the selected-conversation slice. User chose the full
Quinn conversation, not per-page supervision. Optional quest-context observation
21112e2c0f874b69959f8499afb94aa2 passed. First action run
acafc0ab7cad4b0386c0b91e1273cabb turned in the talk objective but stopped because
the objective changed before dialogue closed; user confirmed next offer pending.
Fixed shared menu continuation. Follow-up 9a0c596712694b969822305e1a19afdb:
three accepted/released Space messages, 12.9 seconds, eight trees/zero failures,
dialogue closed, new objective Go To Fireglobe Theatre in Firecat Alley. All
restored/disconnected; guard clean, zero task processes. No focus/global input,
walking/combat or Idle action. Source not installed. 1688 full native/31 Python
tests pass, plus10 conversation tests including hashed saved-live replay. Scoped
format/analyzers pass. Older opening dialogue timeout/reconnect did NOT turn in
the quest; user verified this. Progression and timeout are distinct outcomes.
Full workflow/UI readiness review: W101_WORKFLOW_STATUS_20261110.md. Universal
between-run Idle, resource-driven switching and card editor remain missing;
navigation/combat/Potion Motion have later offline contributions beyond older
board descriptions, not full live players. "Myst" was an accidental keyboard
macro; no future-game entry added. No desktop control reserved. Latest screen
is outside Quinn dialogue with the Fireglobe Theatre objective, not native Idle.

Latest architecture direction: "Treat playing wizard101 like a card game,
playing a card game." User is Dealer; approves job cards/run counts/limits.
Maelstrom normalizes state and registers bounded routines, API serves current
facts/status/capabilities, MCP is a thin executive interface to the same owner.
Cards with unmet conditions are deferred without consuming run quota; attempts
still have finite budgets. Approved maintenance cards can supply resources;
no implicit spending or new card authority. Decks run locally without an LLM
call per action. This design is recorded in W101_WORKFLOW_STATUS_20261110.md;
live state API, condition/deck scheduler and card UI are NOT implemented yet.
Existing MCP only exposes inspection/offline planning and compilation.

Latest UI-tree checkpoint: W101_UI_TREE_20261110.md. Source CLI ui-tree-watch
requires two explicit opt-ins and reuses the existing scheduler/owner and bounded
worker. One connection staggers 250 ms loading / 1 s tree reads; same-source
agreement is not independent validation. Root-hook source has guarded restoration
and fixed compatibility failures. Archived signatures failed; a narrowly reused
Deimos development R13 root hook/current scratch pattern matches the exact image
hash and now passes live restoration. Five-second Idle and two user-coordinated
minute watches plus Quinn's five-second dialogue produced 130 trees, zero tree
failures, clean disconnects and no game inputs/desktop control. Idle, settings,
X prompt and spoken dialogue have live evidence; shopGUI/PayToPlay are menus.
Final screen is Quinn's dialogue, left open because the user clarified that
closing it advances the quest. Do not force Idle cleanup through quest dialogue.
Final guard clean, zero task observers. Pruning reduced Idle reads 39,584 -> 2,238
bytes. Seven saved state cases replay offline; 1629 native/25 Python tests pass.
The hook-free route log toward Quinn
captured 104 readings/four loading; it is not coordinates or a navigation replay.
Do not activate all hooks or describe this as focus-independent walking. The
hook-free loading path below is separate. This UI-tree source is not installed.

Policy update: W101_CLIENT_SIDE_EXCEPTION_20261110.md supersedes the narrower
read-only exception. The owner explicitly approves reviewed W101 client-side
functionality, including necessary hooks, injection and memory reads/writes;
network manipulation remains excluded and installed game files remain read-only.
UI-only stays the general default; W101 client-side mode should be optional and
default-off with visual operation retained. Do not re-ask this scope decision.
Generic legacy launchers remain blocked. WIZWALKER_ADAPTER_20261110.md records
one narrow optional client adapter now integrated in source: selected-client
loading observation, pinned worker/source, native scheduler and control owner,
supervised one-shot Python process and verified disconnect/result provenance.
The W101 Observe & teach workspace exposes an opt-in check with Stop. The new
HEADLESS_CLI.md commands run before UI initialization and use the same scheduler
and control lock. WIZWALKER_LIVE_20261110.md records two real nonloading reads
with clean disconnect in about 0.64 seconds, while W101 remained in native Idle.
W101_LOADING_TRANSITION_20261110.md supersedes the loading gap: one visually
observed Dance load plus six user-demonstrated zone loading periods were detected.
The one-minute CLI watch saved105 readings/27 loading/105 clean disconnects;
native Idle was visually confirmed afterward and the shared guard was clean.
ModuleRunner/ModuleScreenSession now accept an optional loading condition, and
the Dance postgame continuation can opt in via --client-loading-workspace.
1601 native tests and scoped format/analyzers pass. New source is not installed;
the combined postgame path and live target replacement remain unqualified.
Prefer energy-free zone crossings for loading tests. Use a question notification
as the user's start signal after the bounded watch is already running. Do not
confuse client reading traces with video recordings. No --update command exists.
1562 native/80 combined Python/14 installer tests pass. Setup-20261110-193143-7250d6
is now installed in versions/11d29e86169b472ab46b3a4f2f64de02. Installed help,
windows and status passed, including default user-data resolution. The installed
W101 read completed in 709 ms with verified disconnect, clean guard and no remaining
worker. 1,163 data files were preserved with 341 small hashes unchanged. W101 remained
native Idle; no game inputs were sent. Evidence is in
.local/wizwalker-installed-20261110 and WIZWALKER_LIVE_20261110.md. The older
static telemetry skeleton is not this adapter and its unresolved gaps remain
recorded separately. Do not re-enable arbitrary legacy programs to test this path.

Earlier recovery qualification: INPUT_RECOVERY_LIVE_20261110.md. After the requested
half-hour pause, installed Settings -> Restore game controls passed one real
Stop -> Check -> Restore check. UI showed Game controls restored; saved samples
were 1,016 ms apart with unchanged activity. Original empty legacy-clean guard
was preserved; the final guard was clean under its OS lock. No routine records
or settings changed. All Maelstrom windows closed by 17:45:03 UTC; no desktop
control remains active. No gameplay, capture, model or telemetry reader ran.
Dirty/held/busy/failure scenarios remain offline-qualified, not live-qualified.
That earlier installed binary was superseded by the CLI package above.
Checkpoint paragraphs below retain their original test-time scope; old statements
about installation are historical. Use the current installation above and ROADMAP.md.

Read PRODUCT.md, ROADMAP.md, DESKTOP_GAME_PLAYER_PLAN.md and GAME_PACK_WORKSHOP.md
before planning changes. MAELSTROM_STANDALONE_PROFILE.md describes implemented scope.

Use PROJECT_ORGANIZATION.md and coordination/workstreams.json for current task
ownership/dependencies. Root AGENTS.md, coordination/claims.json and the external
agent briefs define parallel work. The latest shared checks are recorded in
INPUT_RECOVERY_FLOW_20261110.md. Task claims are released at each integrated
checkpoint, not silently kept running in the background.

Current source adds Settings/Routine runs → Restore game controls with explicit
Stop → Check → Restore, shared non-disposing scheduler drain, a read-only Windows
verifier and exclusive evidence-preserving recovery. A new challenge and two fresh
quiet samples are required at final restoration; preview is not authorization.
No automatic resumption, settings change or reset by deleting a warning.
See INPUT_RECOVERY_FLOW_20261110.md for current validation and limitations. The source task used fake observations, temporary locks and hidden rendering;
the installed real quiet-verification path now passes the bounded check above.
Game qualification and live failure-path qualification remain separate.
Current totals: 1507 native tests (97 new), 62 Python/board checks, scoped format
and analyzers all pass. The board is updated and this increment's four claims are
released; re-claim paths before further edits.

Previous guard checkpoint: 1410 native tests and 62 Python/board checks pass, with scoped
native format/analyzers and Python Black/syntax checks. The input-recovery gap is
closed in source: shared native/Python lock state is flushed dirty before input,
poison-before-warning keeps it dirty despite failed warning writes, and same-owner
dispatch rejects further actions while retaining release-only cleanup. New owners
and fresh processes reject dirty state without needing a warning file; offline
jobs still run. CleanupFailed cannot be overwritten by worker completion/cancel.
No game/desktop/model/installer was used. Tests use isolated lock directories.
The verified recovery UI is added in the current checkpoint above. Reporting
must finish within ownership lifetime; fully late reports cannot revoke a later
owner. Older installed builds do not understand this guard. Details/evidence:
INPUT_RECOVERY_GUARD_20261110.md. Prior checkpoint totals below are historical.

Previous scheduler checkpoint: 1379 native tests, scoped format/analyzers and 22 board tests
pass. Devtools main was fast-forwarded 41 commits to
7e7a3d3f45c3d24b8ff8de32d1e1af288840f3ed. New jobs use private frozen input maps,
bounded immutable admission/hash-linked lifecycle records and the existing
receipt.json view. Recovery validates authoritative evidence first, retains
history and never resumes actions. Cancellation waits for callbacks and cleanup,
including inline continuation; malformed outcomes cannot poison restart. The
compiled saved-screen workflow completed in 1.097 s with no input lease or desktop
dispatch and a verified 9464-byte journal. No game/model/installer was launched.
GUILD_STEWARD_LATEST_REUSE_20261110.md records the current review and proposed vs
implemented boundary. Its input-recovery-marker follow-on is now addressed by the
new guard checkpoint above; retain the original report as historical evidence.
No new runtime service, dependencies, automatic retries or distributed scheduler.

Previous saved-screen checkpoint: 1316 native tests passed. Screen/scheduler integration is
implemented: actual saved PNG -> local OCR -> ModuleScreenSession -> shared
modules -> scheduled offline receipts. The CLI --review-module-screens runs in
a new isolated output directory without an input transport. The actual full
saved-screen command completed 2 modules / 4 proposals / 4 supplied synthetic
receipts / 11 images in 1.127 s with zero desktop dispatch. Repeated-frame times
and receipts provide synthetic alignment over real screenshots, not a live trial.
PetDanceNativeIdle's existing source now uses module sessions/ModuleOwnedInput
under the inherited OperationContext and exact process-instance target; LiveTrial
version postgame-modules-v7 distinguishes it. Explicit postgame eligibility,
monotonic capture/observation clocks, two post-release outcome frames, per-action
control checks and one in-flight input are enforced. No update installed or live
test run. Navigation/initial sigil evidence and editor controls remain open.
GUILD_STEWARD_SCHEDULER_REUSE_20261110.md reviews older pinned local references;
use the new LATEST_REUSE review for the requested refresh. Its immediate
job-receipt boundary repair is integrated and tested, preserving the C# queue.

Navigation, sigils, menus and Auto-Idle
are independent versioned modules under Utilities/Modules with ModuleCatalog,
ModuleRunner and --replay-modules. W101SharedOperations now supplies data bundles
instead of constructing graphs. The saved w101-operations bundle completes four
modules and seven proposals in deterministic zero-input CLI replay. An independent
offline-lab profile exercises all four modules with different keys/scenes/controls.
Facts, named controls, exact bound outcomes and cumulative budgets are enforced.
Navigation/sigil recognizers, runtime qualification of the integrated postgame
path and module clip editing remain next gates; no new installed update.
REUSABLE_MODULES.md documents contracts and module-specific docs. The previous
checkpoint records 80 Conjuration tests; unchanged Python game components were
not rerun for this module increment. Four actual
saved Dance frames now pass fresh local OCR through selected-level/Play recognition
and entry classification. Pointer-away/control annotations and same-recording
calibration remain explicit; other arenas/layouts and live visits are unqualified.
The Conjuration collection draft preserves 17 frames but only one unique instructions
image; actual card/phase data is still missing. ModuleRunner composes the existing
RoutineRunner; every module action needs its exact pending proposal's observed
outcome. The existing UI dispatcher is reused; no installed update exists.
Use the framework-dependent TargetPath, not the stale win-x64 sibling output, for
offline commands. BUILD_ARTIFACT_RETENTION.md records a read-only cleanup inventory;
no files were deleted. The board holds next tasks. Earlier totals below are history.

The user wants a generic Desktop-Use Game-Player, overlay and built-in pack authoring.
Wizard101 minigames are initial exercises, not the architecture. Foundation first,
then progressively harder games. Preserve and reuse existing source across owned
repositories before creating substitutes. Do not restart the handoff routine.

The user describes the game as the workspace, like an IDE selecting a project or
a desktop tool selecting a target process. Use explicit target selection and
verified reconnection, not arbitrary foreground-window attachment or mandatory
embedding. GAME_WORKSPACES.md records this decision.

## Latest implementation checkpoint — 2026-11-09 local / 2026-11-10 UTC

The user clarified dark IDE-like workspaces, Cheat Engine-style target selection,
and iCue/video-editor-style routine composition. Dedicated Dodge routing and dark
Pet Dance visit setup exist; a draggable routine timeline does not. Product
direction is recorded in PRODUCT.md. The new workspace skin is shared.

Pet Dance now has an optional native Idle player operation (local OCR, no feeding,
same input owner and activity baseline), persisted arena/snack/goal preferences,
read-only attribute/school-aware recommendations and a one-attempt Energy gate.
Live inventory/Energy reading, feeding and Bazaar restocking are not connected.
Crown Shop actions are excluded from this Dance operation. NeverFeed is default.
See PET_DANCE_POSTGAME_OPERATION.md and PET_TRAINING_LEVELS_AND_SNACKS.md.

The historical 2026-11-09 installed version was `08d8c3434d4b41ec8d39a7632435e06f`, replacing
`b5c5cdf4de554492986e529d4af5a1dd`. The user approved and installed
`.local/releases/Setup-20261109-220719-c0e02b/MaelstromSetup.exe` through its UI.
Assembly SHA-256 is `961EAAE4B7CA061B1EA8DEEDEAFA2C7E51E56CFC701D67C7DF274DD346B8AA84`;
all 750 baseline data files remained present (presence, not full content integrity).
Evidence: `.local/dance-native-idle-live-20261109/repair-installation-verified.json`.
There is no outstanding approval for that already-installed repair package.

Latest live report: `%LOCALAPPDATA%/Maelstrom/reports/pet-dance/20261110-031213-a787bbc5d0244862a8350e68e863e891/`.
At 1280—720 with Large UI it recorded five successes and 25 dispatched keys;
PERFECT was independently observed on the reward screen. The report deliberately
retains `game_victory_verified: false`. Automatic Idle admitted zero frames:
80 reads, one missing frame, 79 stale frames, zero aligned frames. The 31 retained
stale samples were 495.742–511.382 ms old, exceeding the unchanged 300-ms limit.
It stopped on desktop activity/target change without any postgame menu action.
This establishes stale delivery as the admission failure in this run; slightly
future timestamps were not its cause. It does not establish every underlying
Windows capture behavior. See PET_DANCE_IDLE_LIVE_20261109.md.

The user subsequently reported finishing the menus and returning W101 to native
Idle. The saved receipt is Completed for **supervised** cleanup and says
`autonomous_idle_recognition: false`; this is not a new autonomous or assistant-
observed Idle success. Current work is offline, with no new live operation launched.
Game controls were enabled for the last trial; no later switch-off is verified.
Do not change desktop state or claim the controls are off from this checkpoint.
No feeding or volume change was recorded. Preserve the earlier interrupted and
zero-admission reports as historical evidence rather than overwriting them.

Native-resolution geometry and bounded panel extraction exist as an offline
overload in the installed repair package. Saved game pixels pass declared synthetic
720p/1080p/1440p transforms, with UI scale independent of client size. Menu OCR has
a bounded raster and inverse word mapping. Its pre-package native suite passed
981/981; scoped formatting and analyzers passed. Keep the 1280—720 live gate until
actual native-resolution capture/replay qualification in PET_DANCE_RESOLUTION_PLAN.md
is complete. Installed offline capability is not native-resolution live evidence.

Newer source repairs are **not installed or live-qualified**: bounded 25-ms frame
reacquisition breaks the simulated 500-ms buffered-frame starvation loop while
OCR remains at most 2 Hz; freshness, input ownership and cancellation are unchanged.
Its 23-test focused run passed. Final supervised confirmation now propagates
cancellation, bounds capture to 2 seconds plus a separate 3-second disposal budget,
disposes late frames and preserves unconfirmed cleanup as failure. A subsequent
32-test focused run passed (overlapping coverage, not 55 distinct tests). These
repairs require a new package and live qualification; reinstalling the unchanged
22:07 installer would not deliver them.

The reusable routine graph/transition runner, graph sidecars and offline replay
workflow are integrated in source: PetDancePostGame consumes the shared runner,
and Dance/Dodge synthetic session rehearsals have zero desktop dispatch. The
integrated native suite passed 1017/1017 with format/analyzer checks. Cross-run
receipt identity and completion preservation have regression tests. This is not
an editable timeline or a qualified full live visit; no new package is installed.
The parallel Conjuration helper passes 21 offline tests including 60 shuffled
pair/triple simulations and reviewed conflict/reset regressions; it is not yet
integrated into the live player/UI. Continue shared primitives and evidence-backed
integration, not a new assistant-dependent handoff or another live trial by default.
No memory/client/network manipulation or unbounded desktop control.
Older dated sections below describe historical stages, not today's complete scope.

Keep C# WinForms and supervised Python workers. Existing code and uncommitted work
must be preserved. Do not claim a trained SLM/MoE, universal input ownership, working
Workshop or successful live gameplay without evidence. Recording, suggestion,
dispatch and verified success are distinct. Unknown state must remain unknown.

Reference repositories are ignored local clones in .local/reference-repos, pinned
by docs/repository-reuse-inventory.json. Source defects and recovery decisions are
recorded in REPOSITORY_REUSE_REVIEW.md and MAELSTROM_2_RECONSTRUCTION_PLAN.md.
Aaroneous/Devtools/Merlin are optional sources, not required hosts.

Initial pack candidates: Pet Dance, Home Navigation, Potion Motion; Diego follows
as a real-time challenge. Home trials begin with bounded movement only. Each new
pack should use the Workshop, shared session contract and outcome verification.

Historical server endpoints and tray details remain in
history/SERVER_ASSISTANT_CONTEXT.md; they do not define product requirements.
# Observe workspace implementation (2026-10-30)

The native home now opens GameWorkspaceForm. GameWindows discovers explicit HWND +
PID + process-start identities; WindowObserver uses Windows Graphics Capture with
no input dispatch and no desktop-copy fallback. Pause/close clears frames; resize,
minimize/hide, target loss and five seconds without frames stop capture. Most recent
workspace metadata lives beside packs under workspaces/last-workspace.json, with no
persisted live handles. Raw preview only: no OCR/recording/pack binding yet. Native
tests plus --verify-observe exercise identity/persistence and controlled-window
capture/occlusion/reconnect. Wizard101 was not running during verification.

# Observe teaching (2026-10-30)

TeachingRecording now saves bounded 1fps PNG sessions (120 frames/128 MiB), atomic
recording.json metadata and per-frame human state labels. TeachingReviewForm
reviews/reopens recordings and exports new self-contained draft versions with
examples/session.json, events.jsonl and hashed PNGs. No automatic detector/rules or
existing-pack merge. Home opens the exported pack in Workshop through --pack;
game_packs.save_pack accepts source_directory to preserve referenced session/image
assets on revisions. Updates/uninstall preserve the data root; portable importer
still copies packs/reports only. Controlled-window diagnostic also exercises
record-label-export and produces a review screenshot.
