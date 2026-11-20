# Maelstrom game-player gap analysis

Assessment date: 2026-11-07, with Dodge results updated 2026-11-08. Scope: the
current workspace source, local test reports and project documentation. This is
not a clean-machine audit or a claim that the installed app contains every
current change. Incomplete games are not averaged.

## Objective and current position

Maelstrom is intended to be a lightweight Windows game-player and assistance
system. A user selects an external game as a workspace, records or imports
experience, researches and teaches a versioned game pack, evaluates it, and can
eventually run bounded, reactive specialists. The same shared system should work
across different interaction types, starting with small games and growing toward
longer tasks. The game pack should preserve the useful knowledge and behavior;
a separate exported player is a possible later packaging choice.

Today there are useful components and a working **private Dodge research loop**,
but there is not yet a qualified general-purpose player or a complete installed
user journey. The private loop captures rendered images, chooses actions, sends
keys to an isolated X11 display, records outcomes and can train small local models
offline. It does not establish normal Windows gameplay, real-time performance,
automatic support for unfamiliar games, or a finished product experience.

The latest completed independent ten-game slow Dodge validation of the earlier
guarded policy scored **37, 168, 2, 29, 6, 31, 140, 44, 23, 18**: mean **49.8**,
median **30**, and two games at or above 50. The score in this test is the game's
survival score, not a verified count of creeps avoided. A later action-contrast
candidate screened at **67, 61, 46, 55** (mean **57.25**) while its matched
directional baseline scored **124, 22, 63, 55** (mean **66**). That candidate's
separate ten-game validation stopped on its seventh game when the player was not
recognized; the first six completed at **16, 26, 6, 93, 144, 13**, and the
seventh was censored while alive at score 59. The declared ten-game mean is
unavailable; the four-game screen is not a reliability result. The failure was
reproduced as clipped enemy fragments being mistaken for possible eye landmarks;
the source fix has regression coverage. A later fresh ten-game validation
completed at **24, 130, 34, 32, 28, 27, 8, 14, 35, 218**: mean **55**, median
**30**, and two games at or above 50. Two long games account for much of the
average, so this is not reliable performance. Details and evidence paths are in
[Dodge offline learning](DODGE_OFFLINE_LEARNING.md).

## Most important gaps

### 1. The pieces do not yet make one end-to-end product

**What exists:** a native home and Observe workspace, recording and labeling,
an offline Workshop, pack drafts, replay, a named-action format, some overlay
components and several specialized game-test paths.

**Gap:** these pieces have different contracts and entry points. The Workshop is
launched as a separate helper; research training and repeated Dodge studies are
currently workspace tools, not an ordinary in-app workflow. The desktop package
copies an explicit set of Workshop scripts and does not include the new learning
scripts or the local WSL test environment. The user should not need to know which
process, folder or command owns a step. See [home](../ProjectMaelstrom/ProjectMaelstrom/MaelstromHomeForm.cs),
[workspace](../ProjectMaelstrom/ProjectMaelstrom/GameWorkspaceForm.cs) and
[desktop packaging](../scripts/build_desktop.ps1).

**Close it:** make one navigable flow: game library → attach/observe → record →
review/label → build a draft pack → test on a separate session → inspect results.
Then add Assist and, only when qualified, bounded Play. Show the active game,
pack version, mode, current job, progress, errors and stop control throughout.
Qualify the installed build by repeating the whole flow with a second session.

### 2. A pack is not yet a complete portable player contract

**What exists:** schema 2 supports named bounded key actions and rules; draft
context can carry game identity, states and constraints. Replay is read-only and
hashes its inputs. The private Dodge pack can carry experimental model weights.

**Gap:** the registry is primarily keyboard-action data, not a unified capability
contract for pointer movement, clicks, drag, scroll, holds/releases, combinations,
gamepads and game-specific actions. Preconditions, expected outcomes, recovery,
model/detector versions and qualification evidence are not yet a complete shared
runtime contract. During this review, a schema mismatch was reproduced and
fixed in source: the visual recognizer now accepts both schema 1 and schema 2,
with a regression test. The full installed recording-to-Workshop-to-recognizer
workflow still needs an end-to-end check. The Python pack validator rejects an
explicit `recognition` field, so its relationship to the fixture-based
recognition format also needs a shared contract. See
[pack validator](../tools/game_packs.py), [visual recognizer](../ProjectMaelstrom/ProjectMaelstrom/Utilities/VisualStateRecognizer.cs)
and [Workshop](../tools/game_pack_workshop.py).

**Close it:** define one versioned, migration-aware pack manifest that references
perception assets, action capabilities and bindings, state/skill definitions,
outcome checks, constraints, fixtures, provenance and runtime compatibility.
Use parameterized actions rather than attempting to enumerate every possible game
action. Make save/reopen and export/import preserve assets and exact evidence;
reject unsupported versions visibly instead of silently dropping content.

### 3. The observation-to-action loop is specialized and isolated

**What exists:** Dodge has a visual tracker, a private stepped environment,
bounded key actions, game-state signatures and independent score/game-over
evidence. A separate foreground Windows test verified a limited key response.

**Gap:** the trained context-aware Dodge packs are restricted to the private
25%-speed endurance path. The ordinary Windows live path rejects those packs;
it has not demonstrated continuous Dodge play. The private WSL setup is prepared
for this particular Godot game and is not a general Windows VM for arbitrary
games. Windows foreground input still requires the target to own focus; a normal
virtual desktop does not provide a second active input desktop. See
[isolated Dodge](ISOLATED_WSL_DODGE.md), [input evidence](DODGE_INPUT_VERIFICATION.md)
and [live-run guard](../ProjectMaelstrom/ProjectMaelstrom/Utilities/DodgeLiveRun.cs).

**Close it:** define a common observation → proposal → target-bound dispatch →
observed outcome interface, with adapters for the private lab and the ordinary
Windows workspace. Make target identity, geometry, focus, freshness, held-key
release, cancellation and manual takeover one owned responsibility across native,
macro and external-worker paths. Test the same pack against both a controlled
target and, later, a supervised real Windows session. Keep isolation optional and
honest about which games it can host.

### 4. Capture speed is not reaction speed

**What exists:** Windows Graphics Capture, a GPU recording path, timestamps,
bounded queues, playable video, frame inspection and optional target-scoped input
recording. Controlled animated-window tests have exceeded 60 fps; a user-played
Dodge capture delivered about **52.4 fps** in one non-isolated run. Results vary.

**Gap:** a frame rate alone does not measure the time from an event appearing on
screen to Maelstrom observing it, choosing a move and the game responding. OS-level
misses are not the same as recorder queue drops. Static or covered targets,
resize/DPI changes, encoder contention, timestamp alignment and slow consumers
remain important. The current private lab also has deliberate fixed waits; new
wall-time diagnostics are prepared but have not yet been qualified in a live run.
See [delivery course](DELIVERY_COURSE.md) and [workspace recording](GAME_WORKSPACES.md).

**Close it:** measure capture → perception → decision → input → observed response
with a controlled target, including p50/p95/p99 and worst gaps, then repeat on the
chosen game and installed app. Make the fast path consume the newest useful frame
and discard stale work; recording and slow research should not build an unbounded
reaction backlog. Keep any throughput optimization separate until timestamps,
input delivery and game outcomes remain comparable.

### 5. Perception and tagging do not yet generalize

**What exists:** frame/region examples, fixed-region matching, motion and label
suggestions, OCR-related components and a Dodge-specific visual tracker. Some
model suggestions can be reviewed; independent check sessions reject suggestions.

**Gap:** the generic recognizer is an experimental fixed-grid exemplar matcher,
and Dodge detection is calibrated to one 480 × 720 game and its appearance. It
does not establish robust recognition across other games, scale, animation,
occlusion, changing HUDs, or unknown states. The action learner uses hand-designed
visual features and at most three nearby threats; it does not learn vision from
raw video. Existing tagging/model trials have false positives and are not a
qualified automatic action-labeling system.

**Close it:** build a reviewed, versioned dataset with independent sessions,
negative/unknown cases and annotations for objects, states, transitions, actions
and outcomes. Measure per-class precision/recall, unknowns and latency on held-out
games and display conditions. Add tracking confidence and recovery, and use OCR,
templates or compact models only when measured against a simpler baseline.
Keep AI-generated labels provisional until checked.

### 6. Learning signal and evidence are still weak

**What exists:** recorded randomized actions, complete-game outcomes, separate
seed sets, small CPU-only survival and action-contrast models, paired development
tests, held-out action-support diagnostics and score distributions. The current
action-contrast fit used **2,077** randomized observations; its held-out check
found **33** matching actions across four of eight games. In its four-game screen,
the reference rule supplied 2,340 of 2,495 threat-visible decisions, the model 80,
and fallback 75.

**Gap:** these are correlated observations, not thousands of independent lessons.
The one-second label also reflects later collector actions; it does not reveal what
would have happened after a different move or optimize full-game score directly.
Prediction error is not action quality. The model acts on a minority of decisions,
and the current evidence does not attribute the screen's score to the model.
Repeatedly choosing configurations on small sets can overfit even when seeds are
different. This is actual small-model training, but not a generalist model, a
trained mixture of experts or a qualified long-horizon reinforcement learner.

**Close it:** record versioned trajectories with observations, action candidates,
selection probabilities, executed inputs, timing and independently observed
outcomes. Compare each candidate with a frozen rule baseline on predeclared fresh
games; keep development, model-selection and final validation separate. For
controlled games, investigate efficient resets or branching to compare alternative
actions from similar situations, without exposing hidden test state to the player.
Measure model contribution, not just total score. Report mean, median, range,
early-loss frequency and uncertainty; do not advance speed because one run is high.

### 7. The long-term agent and game knowledge are mostly future work

**What exists:** guide discovery/retrieval, saved research notes and heuristic
extraction of possible controls, tips and hidden-item clues. Suggestions are
explicitly unverified. The local vision model is optional and review-oriented.

**Gap:** there is no verified automatic menu explorer, general rule-discovery
system, dependable hidden-item database, task planner, long-term memory, or
specialist router that can take an arbitrary game and learn it unattended.
Research snippets are not confirmed facts. A model's slow or stale answer cannot
be allowed to control the current action. See [research form](../ProjectMaelstrom/ProjectMaelstrom/GameResearchForm.cs)
and [guide extraction](../ProjectMaelstrom/ProjectMaelstrom/Utilities/GuideHints.cs).

**Close it:** use a fast specialist for per-frame control and a separate optional
slow planner/researcher for goals, explanations and proposals. Timestamp and expire
proposals; keep sourced claims, observed facts and user notes distinct. Add bounded
skill composition, checkpoints and recovery before attempting quests, farming or
larger games. Route between specialists only after each has its own measured scope.

### 8. Resource management, distribution and trust need product-level work

**What exists:** one private study at a time, low-priority game execution,
per-study time/disk limits, bounded queues, stop files, local data separation and
an installer/uninstaller designed to retain user work. Local model use is optional.

**Gap:** these limits are not a system-wide CPU/GPU/memory governor across the
app, model server, recorder and external tools. The user cannot yet manage the
research loop, costs and background jobs as a polished in-app experience. Package
signing, clean-machine verification, comprehensive backup/restore and exact
installed-version visibility remain open. Input recording is opt-in and scoped,
but supported keys can still appear in game chat; retained video and research data
need clear privacy, deletion and export controls. The checkout has many existing
uncommitted changes and local evidence under `.local`; tests do not establish a
reproducible published release. The current CI workflow tests the .NET solution
but does not run this Python research suite, installer lifecycle or packaged-app
journey. See [packaging](../PACKAGING.md), [input recording](GAME_WORKSPACES.md)
and [CI](../.github/workflows/ci.yml).

**Close it:** add a shared job/resource manager with a lightweight default,
per-job limits, cancellation, clear status, disk budget and recovery. Provide
first-run checks, data locations, export/backup, update/rollback and installed
version/build identifiers. Expand CI and release checks to include Python,
installer, pack migration and packaged-workflow tests. Audit dependencies, game
assets and model licenses separately before distribution.

## Recommended order and acceptance evidence

1. **Finish the current slow Dodge validation.** Keep complete games, compare the
   baseline and candidate, report the full distribution and uncertainty, and
   remain at slow speed if results are not repeatable. The user target is a mean
   score of at least 50 across complete games; do not redefine it as a creature
   count. Use more fresh games when a small, skewed sample leaves the mean uncertain.
2. **Unify the pack/session/action contracts and the no-input workflow.** A pack
   created from one session must reopen, retain templates and bindings, and be
   evaluated against a separate session from the installed app. Unknown and
   unsupported conditions must be visible and never become actions by default.
3. **Measure the actual control loop and resources.** Qualify frame freshness,
   input-to-observed-response latency, stop/release behavior, CPU/memory and
   recording impact. Test focus loss and manual takeover on an owned target.
4. **Improve the specialist using evidence.** Use varied private games, action
   support and comparable baselines. Add faster training only after verifying that
   it preserves the intended simulation timing and outcomes. Test longer runs and
   fresh seeds before raising speed.
5. **Prove the shared system on a second interaction type.** A pointer/board or
   reaction game should reuse the same workspace, pack, evidence and dispatcher;
   only game-specific knowledge should change. Wizard101 home/minigames follow
   once the relevant observation, timing, focus and outcome checks are ready.
6. **Add longer-horizon planning, research and optional specialist routing.**
   Only promote a learned or model-assisted component when it improves a measured
   task over a simpler baseline within the user's resource budget.

The target is a system that can **build and improve game-specific players**. A
single universal model that instantly understands any game is not demonstrated
and should not be assumed as a prerequisite for useful progress.
