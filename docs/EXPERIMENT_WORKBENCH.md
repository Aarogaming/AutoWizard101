# Experiments in Maelstrom

Updated 2026-11-08. Open **Experiments · compare players and review footage**
from the home window. This is a local research workflow. Its first executable
adapter is isolated Dodge; Wizard101 control and arbitrary-game training remain
separate development work.

## Compare and preserve

1. In **New experiment**, choose the prepared AutoWizard101 game folder, a
   baseline Dodge pack and a different candidate. State the research question.
2. **Create frozen experiment** copies both packs, records application/game/bridge
   fingerprints and reserves test openings before play. New plans share the
   existing offline-study seed registry. Existing development fixtures are
   excluded from the new draws.
3. In **Results**, run one game or a batch with a 45-minute limit. Dodge runs at
   25% stepped speed in its private WSL/Xvfb display. It uses rendered frames for
   decisions and the separate game audit for score verification. It does not
   acquire the Windows foreground. The prepared optional WSL game is required.
4. Four paired development openings compare both packs, alternating execution
   order. If the candidate's complete-game average improves on the baseline and
   reaches 50, two distinct reserved sets of ten games become available.
5. Review all scores, mean, median, minimum, completion count and number reaching
   50. Both fresh sets must average at least 50 for the displayed two-set target.
   This is empirical support for these slow tests, not a guarantee or real-time
   qualification. A few long games can still hide frequent early failures.
6. **Preserve result and evidence** saves a new decision receipt with your
   rationale, statistics, pack/plan identifiers, trial evidence and attached
   footage hashes. Negative and inconclusive experiments can be preserved too.
   This action leaves the installed player unchanged.

Plans, copied reports, sealed audits, terminal images, footage and decisions live
under the normal Maelstrom data folder's `experiments/`. Portable editions use
their `Data` folder; installed editions use the existing per-user data folder,
which the installer preserves. Original full game traces remain in the prepared
workspace's `.local/wsl-dodge-runs/`; each copied result records its origin.
Keep those traces for deeper trajectory analysis. Application binaries are
fingerprinted, not bundled into each experiment; retain the matching release
when reproducibility requires its executable.

Only one isolated study may own the shared game lock. Stopping a game records an
incomplete attempt, never a successful or zero-score game. The workbench checks
the owner process and its start time to distinguish a running trial from an
interruption; old ownerless records remain explicitly unresolved. Completed
trials resume without replay. An interrupted experiment stays available for
diagnosis and requires a new plan for a new ranked comparison. A changed app,
pack, or game also requires a new plan.

A batch reserves enough remaining time for an entire bounded game before
starting it. It monitors a 2 GiB evidence budget and 2 GiB free-space floor,
including earlier source runs from the same experiment. Each game has the
existing 20-minute/action/request limits. These are bounded research defaults,
not measurements of whole-system CPU or memory performance. No hosted model is
started by this workflow.

## Use gameplay footage

In **Gameplay footage**, select the experiment in Results, describe the source,
and import a local video or reopen a teaching recording. The existing review
window supports state labels, regions and teaching-pack export. After review,
the workbench attaches an independent snapshot and a quality assessment. Later
changes to the original recording do not change that snapshot. **Verify selected
footage** checks its hashes and displays the assessment again.

For Wizard101 or another game, choose **Footage study** in **New experiment** and
enter the game identifier and research question. This needs no Dodge setup or
candidate packs. It supports visual review and preservation; Run is disabled
because no gameplay adapter has been selected for that study.

The default is four samples per second, with a selectable starting point. The
import is limited to 600 frames and three minutes; 30 fps yields at most 20
seconds and 60 fps at most 10 seconds. The decoder and PNG encoder each use one
thread, at below-normal process priority. Images fit within 1280 × 720 while
preserving aspect ratio and avoiding upscaling. The original video is untouched.
Attachments are capped at 128 MiB; select a shorter/lower-rate clip if necessary.

Useful footage shows the game viewport clearly, readable cues and HUD, a known
playback speed, and contiguous interactions. Separate menu demonstrations,
successful play and failures into understandable segments. Review edits,
overlays and camera changes. Retain the original recording for higher-rate or
different-region sampling later.

The assessment reports labeled samples, exact-image diversity, dimensions and
sample spacing. It flags sparse, repeated or inconsistent examples. These are
simple data checks, not a trained quality classifier; they cannot certify
correct perception, infer every cut, or establish adaptability. Imported sample
times are approximate video positions, not original capture timestamps.

Video currently feeds **visual-state teaching and recognition examples**. It
does not supply verified keys, intent, rewards or counterfactual outcomes. All
video attachments are marked ineligible for action training through this path.
Action-learning datasets require aligned input/outcome evidence or a separately
validated inference process. Attaching footage does not retrain frozen packs.

## Wizard101 variety and the home-session prerequisite

Wizard101 remains a destination for the shared workflow: small minigames, pet
games, and bounded home navigation before larger gameplay. Dodge is the current
controlled adapter, not the product's entire scope.

The separate [optional home movement panel](HOME_MOVEMENT.md) now implements
user-confirmed home references, short bounded W/S steps, fresh visual checks,
Start/Stop/Resume and desktop takeover. It is not a route planner or reliable
semantic home/menu detector; live movement and idle-timeout behavior still need
W101 qualification. Background play requires a separately validated isolated
session. The WSL Dodge adapter does not establish that W101 will run there.

Home farming is a later pack with identified resources, routes and measurable
outcomes. W101 footage can already be recorded/imported/labeled for recognition;
the W101 minigame execution adapter remains unimplemented by the experiment screen.

## Developer entry points

The same executable supports bounded, noninteractive verification:

```text
ProjectMaelstrom.exe --create-experiment <data-root> <workspace> <baseline-pack> <candidate-pack> <title>
ProjectMaelstrom.exe --create-footage-study <data-root> <game-id> <title> <question>
ProjectMaelstrom.exe --run-experiment <data-root> <experiment-id> <maximum-games> <minutes>
ProjectMaelstrom.exe --preserve-experiment <data-root> <experiment-id> <note>
ProjectMaelstrom.exe --import-experiment-video <data-root> <experiment-id> <video> <ffmpeg.exe> <sample-fps> <start-seconds>
ProjectMaelstrom.exe --verify-experiments-ui <data-root> <output-folder>
```

The last command renders tab content offscreen. It checks layout without showing
or activating a window; it is not a substitute for a complete manual UI test.
Command-line video imports are explicitly marked unreviewed. Invalid evidence,
changed artifacts and cancelled trials remain unranked.
