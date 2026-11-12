# The game is the workspace

Product decision, 2026-10-30. The user's model is an IDE opening a workspace or a
desktop tool selecting its target process. Maelstrom opens a game workspace; the
game is the input. The first native Observe workspace is now implemented; the
broader workflow below remains the design contract.

## Available now: Observe a running game

1. Start Maelstrom and choose **Open game workspace · Observe**.
2. Open Wizard101 in windowed or borderless mode. Select its titled window from
   the list (the executable and process ID distinguish multiple clients).
3. Give the workspace a name and select **Observe selected window**.
4. Check the live preview and its update time. **Pause** clears the image and
   releases capture. Select Observe again to reconnect. Refresh the list after a
   game restart and explicitly select the replacement window.

Windows Graphics Capture reads the selected window rather than a desktop screenshot
and sends no game inputs. A covered target can still be captured while it keeps
rendering, but some windows stop producing fresh frames when fully covered. Keep
the game visible if the preview stops updating.
Windows may display its capture border. Capture stops and clears the preview on
minimize/hide, process replacement, resize, errors, or five seconds without a new
frame. An entirely static game may also trigger the no-new-frames pause. Restore
the game and reconnect. Protected content, exclusive fullscreen, HDR color
accuracy, and device-specific capture behavior are not yet qualified; try SDR
windowed mode if the preview is blank or incorrect. There is no desktop fallback.

The most recent workspace name and executable hint are saved in
`workspaces/last-workspace.json` beneath the normal data folder. Reopening only
preselects a matching executable; it never begins capture automatically. No live
window handles are saved. This first version has one current workspace, not a
workspace library. Frames stay in memory unless you explicitly start recording.

OCR, Assist/Play, and an attached overlay are still future work. An experimental
whole-frame recognizer can now display state predictions beside the raw preview.
The native capture diagnostic verifies pixels, occlusion, resize invalidation,
reconnect and hidden-target rejection using its own test window; Wizard101 still
needs a separate live qualification run.

## Record and teach

For the locally prepared test targets, select **Local test games…**, open Reflex
or Dodge the Creeps, return to the workspace and select **Refresh windows**. Pick
the game window, then choose **Observe selected window**. The optional game files
live in the AutoWizard101 development folder, not in the installed app; choose that
folder in the test-games dialog if it is not found. Reflex uses an app-style local
HTML window. See [Existing test games](EXISTING_TEST_GAMES.md) for setup and limits.

### Experimental video preview

After attaching a game, choose **Record video (preview)** and then **Stop video**.
The preview stops automatically after three minutes. It records the selected
window only, with no audio and, by default, no input events, and writes `capture.mp4`
and `timing.json` under `video-recordings` in the data folder. The status reports
measured capture fps, queue rejections, gaps between delivered frames, and the
95th-percentile difference between capture and saved playback timestamps. A long
gap can also mean the screen was unchanged. Queue rejections do not include
frames that Windows never delivered. Inspect the actual results before relying
on this preview; 60 fps and real-game performance are not guaranteed. A static
screen may produce no new frames. Observe stays attached and marks old recognition
as stale. If recording cannot receive its first frame, it stops with an explanation.
When the window stops changing before recording stops, the recorder holds the last
observed image through the end with a synthetic terminal frame. The timing report
marks that frame as synthetic; it is not another observation of the game. If the
saved video still fails to cover the observed interval, the status warns about the
missing tail.

To capture a bounded set of controls for a teaching session, turn on **Record
game keys and mouse** before starting the video. This option starts off unchecked
and is not a game-control feature. While the selected game window is foreground,
Maelstrom observes W/A/S/D, the arrow keys, Space, Shift, Ctrl, and mouse moves,
buttons and wheel inside that window's client area. These keys can also be
captured if typed into in-game chat. Other keys, clipboard text, gamepad, raw
relative mouse movement, and input to other windows are not recorded. Focus
changes are marked; held-key state across focus changes is unknown. On stopping,
`input-events.json` is saved next to the video. It includes the video SHA-256,
event offsets relative to the first video capture timestamp, the first-frame
callback offset, target process identity, focus markers and an overflow flag.
Mouse coordinates are client-area pixels, not relative mouse deltas. Injected
events are marked. The recording stops collecting inputs when video capture ends;
there is no audio. If the event buffer fills, the file is marked truncated and
must not be treated as a complete ground-truth session.

Input callbacks and the video timestamp use the Windows performance-counter time
base, but callback delivery, focus notification and video capture have latency.
The first-frame offset is a diagnostic, not a measurement of full input-to-screen
latency. Test alignment on a controlled target before relying on sub-frame timing.
Input logs are observations, never macros to replay automatically. Compare only
predictions bound to the same video hash; skip intervals without game focus,
unsupported controls and injected events when evaluating human input.
Development checks verified hook installation, video hash and shared time origin
on an owned test window. A later focused Dodge test captured automated mouse and
key-down/key-up transitions marked as injected, and the recording visually
showed the start-screen transition after the captured click. This does not
establish human-input coverage or calibrate sub-frame timing. In that run the
first callback offset was about 113 ms and the capture averaged about 17 fps.

For an offline comparison, the source checkout includes
`tools/compare_game_inputs.py`. It accepts an input log and a prediction JSON
with `video_sha256`, `coverage_start_ms`, `coverage_end_ms`, and an `events`
array containing `time_ms`, `type` (`key_down` or `mouse_down`) and `control`.
The prediction producer must calculate the hash from the same video and set
coverage to the portion it actually examined. The comparison verifies that hash,
rejects unverified or truncated recordings, and scores only focused,
non-injected supported presses. It reports matched, unmatched and missed presses
within a configurable tolerance; movement, releases, unsupported controls and
gameplay success are not scored. `--include-injected` explicitly includes
automated test inputs and marks the report accordingly; it is not a measure of
human-demonstration accuracy. When a prediction file declares an invalid or
incomplete `model_output_status`, the tool refuses to score it as accuracy.
No model adapter or comparison screen is bundled
in the app yet, and old videos without input logs cannot provide ground truth.

Optionally select **Pause preview during video** before recording. The preview and
recognition pause while the selected window is recorded, then reconnect when the
recording stops. Results vary; this option does not guarantee faster capture.

To label a local video, choose **Sample video for labels…** and select the file.
Maelstrom extracts up to 600 frames at the selected rate (1–60 per second) from
up to three minutes into a new reviewable recording, leaving the source video untouched.
Set **start at (seconds)** to review a later section: the saved times refer to the
original video. At the default five samples per second, the limit is two minutes;
at 60 samples per second it is ten seconds. Sampling above the source frame rate
can repeat frames. These frame times are approximate video positions; quick actions
may fall between samples. Select
**Review and label** to label the samples and export them as a teaching pack.
This is not full-motion video scrubbing or a live player.
The portable and installed packages include FFmpeg/ffprobe for local inspection
and sampling; the Windows media encoder must also be available. Closing the
workspace cancels sampling and keeps any already saved recording data.

An optional `review-suggestions.json` beside a recording can supply suggestions
from a separate review. Each suggestion is bound to its recorded image hash and
shows its source in review. Suggestions do not become labels unless you accept or
enter them yourself; existing labels are never replaced. Suggestions are not loaded
for independent check recordings.

Local AI pre-screening reuses a suggestion for exact duplicate images with the
same selected crop, and reports how many model requests and reuses it made. It
checks each image against the saved recording and does not reuse across different
crops. Unusable model responses are counted and skipped, while service or recording
errors still stop the batch. AI suggestions still require review; they are not
gameplay controls.

In review, mark a rectangle around an object on an earlier sample. Select the
next frame and choose **Suggest motion**, or choose **Track motion forward**
from the marked frame to propose screen-relative directions over later samples.
Tracking stops when the match is weak or ambiguous. Suggestions are not saved
unless you review and accept them; a screen-relative direction is not proof of
a jump, attack or other game action. Diagonal displacement is labeled with both
directions. When a match is borderline, the tracker may compare back to the
earlier image and continue only if the return match is sufficiently distinct and
close to the starting location; this remains a heuristic, not a confidence score.
With at least two distinct human labels on
frames that follow other frames, **Suggest taught transitions** compares pairs
with your examples and proposes close matches. A region selected on a labeled
frame restricts that example's comparison. It also requires the direction of
visual change to resemble the taught example; unchanged and conflicting pairs
are not accepted as reliable evidence. Both features are experimental and
can miss actions; neither qualifies game control. Suggestions are disabled for
independent check recordings.

In recording review, **Pre-screen recording with AI** proposes labels for all
unlabeled frames through the local Ollama server (`qwen2.5vl:3b`), sequentially.
Stop at any time; suggestions completed in that review remain available. They are
not persisted until accepted, and are discarded on closing the review. If you selected a region,
the model receives that crop; otherwise it receives the full frame. Ollama and that model must
be installed separately; the app never downloads either. Images are sent only to
`127.0.0.1` for this feature. The user must review and accept a suggestion; it is
not proof of accuracy or permission to act. Suggestions are unavailable for
independent check sessions. Model startup can be slow. **Suggest labels** separately
compares frames with existing human labels without a model.

From the Maelstrom home screen, **Settings** independently enables guide control
suggestions and tips/hidden-item suggestions. Both are enabled by default; changes
apply the next time Game Research opens. Preferences are stored in `settings.json`
in the user data folder and survive app updates and uninstall. These settings do
not enable gameplay control.

**Game Research…** opens a web search in your browser. Copy a guide URL and enter
a hypothesis, then save it for the sampler. The immutable note lives in the data
folder's `research` directory and the selected note is embedded in `sampler.json`
and check reports. Research is unverified evidence: Maelstrom can fetch a selected
HTTPS text/HTML guide, but does not summarize pages, analyze video, record input, or
execute instructions from a source. Review the guide and play manually. Loading
an existing research note is supported; the active selection is session-only.

You can also paste an excerpt and choose **Suggest control hypotheses**. This
simple keyword-based extractor selects up to 20 sentences mentioning controls;
review and edit them before saving. It is not an AI interpretation and never turns
extracted text into actions automatically. Only the
source URL and edited hypothesis are saved, not the pasted excerpt.

When reviewing a teaching frame, drag over the preview to save one rectangular
region for that frame. The region coordinates and hashed source image travel in
the exported teaching session and are preserved when Workshop saves a new pack
version. Label at least two visually distinct states before learning. Observe
compares each selected region at the same relative location; examples without a
region still use the whole frame. This first matcher does not search for moving
objects, apply masks, or qualify gameplay. Collect independent check recordings
to measure false matches. Region metadata is saved with the recording as well.

### Guided Gameplay Sampler

In an active Observe workspace, choose **Gameplay Sampler…**. Set a learning goal
and choose **Teaching examples** or **Independent check**. You play manually while
Maelstrom samples the selected game window. The existing recording limits apply.
Stop the sample and choose **Review and label**.

For teaching examples, save a pack and choose **Learn this pack in Observe** to
continue without browsing for the exported file. Include at least two distinct
states. For an independent check, label a new round and choose **Check recognition
on these labels**. This review does not offer teaching-pack export. Predictions
are hidden during check collection and labeling, and the check must use the pack
hash selected when sampling began. Exact training-image duplicates are still
excluded by evaluation. No automatic gameplay qualification results from a check.

`sampler.json` beside each recording stores its purpose, goal and (for checks)
pack identity. Reopening the recording restores its review workflow. Load the
original pack before checking a reopened sample. Evaluation reports include the
sampler plan and recording path. Normal recording and manual pack browsing remain
available. Sampling does not record key/mouse actions or operate the game.

1. While observing, choose **Record stills**. Maelstrom saves at most one PNG
   frame per second, up to 600 frames or 1 GiB. A visible warning appears when a
   limit stops the recording. This is a sampled image session,
   not a full-motion video or an audio/input recording.
2. Choose **Stop stills**, then **Review and label**. Pausing, losing the target,
   resizing it, closing the workspace or reaching the limits also stops recording.
3. Select a frame and describe its visible state, such as `enemy-high` or
   `board-ready`. Save the label; changing frames or closing also saves the current
   text. Clear the label to exclude that frame. No label is inferred from frame order.
4. Enter the pack ID, game ID and title, then choose **Save teaching pack**. Each
   export creates a new draft version containing only labeled images, image hashes,
   and a replay session. It starts with no action rules; exporting under an existing
   pack ID does not merge or inherit that pack's rules.
5. Choose **Edit rules in Workshop**. The saved pack and its example session open
   together. Add rules matching your labels, change the version, save and replay.
   Attached images and sessions are copied into the new version. Close an already
   open Workshop before opening another pack through the workspace.

Use **Open recording…** to reopen `recording.json` under the data folder's
`recordings` directory. Labels persist there. A partially completed recording can
be recovered from its last saved manifest; it never resumes automatically. Saved
recordings and packs remain after update/uninstall because they are user data.
Back up the entire portable Data folder when moving installations; the current
installer's optional portable importer copies packs and reports only.

These labels are human-authored examples. The separate Learn command builds an
exemplar matcher; Workshop replay still checks label-based rules only. The original one-frame-per-second still recorder is suitable
for initial state examples, not qualification of fast timing games. Add region
labels and sequence annotation in later stages.

## Opening a workspace

### Recognize labeled states in Observe

After selecting the game window, choose **Learn from teaching pack…** and select
its `pack.json`. Include at least two visibly different state labels. The app
verifies the attached image hashes and builds a local, in-memory exemplar matcher;
reopen the same saved pack to rebuild it after restarting. No extra runtime,
download, service, or neural-model training is required.

During Observe, the workspace displays the predicted state, similarity score,
separation from the next state and update time. Similarity is **not a probability**.
Weak matches, near-ties and incompatible window shapes return **Unknown**. Capture
pause/disconnection clears the live prediction; changing the selected target
turns recognition off. **Recognition off** also removes the active matcher.

The first algorithm compares a 32×24 RGB summary of the entire captured window
against each example. It accepts similarity ≥94% only when the gap to the next
state is ≥2.5 percentage points, and requires aspect ratios within 2%. These are
fixed experimental thresholds, not learned or calibrated guarantees. Identical
images with conflicting labels fail learning. Example order has no role.

Choose **Check on another recording…** to evaluate separately captured, manually
labeled frames. A report in `reports/visual-check-*.json` records correct,
incorrect and unknown predictions, image-level results, training image hashes,
pack hash, algorithm and thresholds. Exact training duplicates are excluded;
zero tested frames is not evidence of accuracy. Nearby frames can still be nearly
identical, so use a separate session with varied positions and backgrounds.

This is an initial visual baseline for distinct screens and broad states. It
does not locate objects, read text or robustly distinguish tiny enemies against
the same background. Region annotation, object features, temporal evidence and
calibration on independent recordings are next. It sends no game inputs and
does not qualify any pack for gameplay.

Planned refinement: suggest regions/masks from differences between labeled states
and let the user preview and confirm ignored areas. Motion alone must not define
importance: animated backgrounds may be irrelevant while static game objects
matter. Region/mask support is not part of this first whole-frame matcher.

The first screen should offer **Open running game**, **Launch a saved game**, and
**Open a recording**. For a live game, show window thumbnails, titles and executable
names. The user selects the intended window, sees a highlighted preview, and
confirms the matching game pack. Multiple clients remain distinct choices.

A workspace groups the selected game's profile, pack/version, screen setup,
recordings, labels, tasks and results. Its saved identity describes the game and
preferences. Its current live connection binds a process ID plus process creation
time and a specific window; those handles are never reused blindly after restart.

After selection, run a readable readiness check: correct window, visible game area,
supported display/scale, active pack and available inputs. Start in Observe so the
user can see what Maelstrom recognizes. Assist shows proposals; Play enables only
qualified skills with clear pause/stop and manual takeover.

## Session ownership

Game packs contain game knowledge, while a workspace records which pack and target
are in use. A task belongs to that workspace. One input owner dispatches actions
only to that verified target. Changing games ends or pauses the old control session.

Focus loss, window closure, process replacement, geometry change or stale imagery
invalidate pending actions and release held input. A game launcher may create a
different process/window; Maelstrom must reconfirm the resulting target. It must
never assume that the next foreground window is the game.

For ordinary foreground keyboard/mouse input, Play pauses when the chosen game
loses focus. Background-capable adapters require separate capability checks; they
are not a blanket permission to control arbitrary windows.

## Hosting and presentation

The default is an external game window plus Maelstrom's workspace and overlay.
The workspace may show a live preview and offer to arrange windows. Games retain
their own rendering, launcher and lifecycle. Reparenting arbitrary game windows
inside Maelstrom, injecting a shell, or requiring a VM is not the default.

Controlled test games or emulators can later provide an embedded surface through
an explicit adapter. Their convenience must not make the general desktop target
contract depend on embedding. The initial Wizard101 home remains a bounded live
navigation workspace; offline recordings remain useful when the game is closed.

## Experimental Dodge advice

The Observe workspace offers an opt-in **Dodge specialist (advice only)** check
box. It examines the existing low-rate preview with a fixed-size CPU image and
shows a possible direction; it does not load a model or send input. It is
calibrated only for the portrait Godot Dodge the Creeps demo's current colors,
not Wizard101 or arbitrary games. It returns Unknown if the player cannot be
uniquely identified. The direction is experimental, not proof that movement is
safe; occlusion, appearance changes, undetected threats and stale capture can
make it wrong. This is a single-frame baseline, not temporal tracking or a
trained controller. Stop Observe to stop receiving advice.

For an offline test of the decision rules without foreground focus, open
**Local test games** and choose **Run headless Dodge test**. The game-native
adapter uses an isolated instrumented copy and engine state, so it is not a test
of visual capture or ordinary desktop input. Reports and copied test projects
are kept with Maelstrom's user data. The selected AutoWizard101 test-game folder
is saved there as well, so app updates do not discard the selection.

AI pre-screening remains manual. A run makes at most 30 local model requests,
with a 500 ms pause between requests; repeated images may be reused without a
request. The limit is per run, not a global CPU/GPU cap, and cancelling a
request does not necessarily unload the separate model server. Review or accept
earlier suggestions before restarting if the first part of the recording uses
the request budget.

## Data and distribution

Installed workspace records will live beside packs and reports under
`%LOCALAPPDATA%/Maelstrom`. Portable records will live in the package's `Data` folder.
Installer update/uninstall never owns those records. Existing prototype libraries
are not automatically migrated; setup offers a non-overwriting copy from a
portable Data folder. Runtime session handles and held-input state are never
restored as active gameplay after a restart.

Next implementation: recording/image references, labeling and game-pack binding
within the Observe workspace. Live control follows the shared input
ownership and qualification gates in the desktop player plan.

Capture API references: [Microsoft screen capture overview](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture),
[CreateForWindow](https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createforwindow).
