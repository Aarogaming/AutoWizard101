# Pet Dance panel replay and bounded live validation

Status: **2026-11-08 study, updated 2026-11-09 after attempt 04 and full-visit recording**. Native small-panel
recognition passes the full five-round development recording. The first live
attempts stopped after 12 seconds of loading images, before sending arrows.
The user confirmed the game began as attempt 03 stopped; later inspection showed
its reward screen. A game stall or capture freeze was not established.
Native Idle was subsequently restored and visually confirmed. The new
[loading pause](GAMEPLAY_SCENE_GATES.md) separates startup from gameplay;
independent live qualification remains pending. No new release/install is included. See the
[delivery tracker](PET_DANCE_DELIVERY_TRACKER_20261108.md) for gates and parallel
snack/knowledge work.

## Architecture and evidence scope

[`PetDancePanelVision`](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDancePanelVision.cs)
reads a small composite panel using calibrated pixels, with no hosted model or
new production dependency. It fits arrow opacity against neutral and four arrow
examples; reads the bottom Watch/Go/Resolving region; and separately checks top
success/failure feedback. [`PetDanceSequence`](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceSequence.cs)
turns continuous, timestamped observations into a single sequence proposal at Go.
[`PetDancePanelReplay`](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDancePanelReplay.cs)
never dispatches those proposals. [`PetDanceLiveTrial`](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceLiveTrial.cs)
is the separately guarded, experimental live consumer.

References were selected from the same human video used in the development
replay. This is a regression/calibration study, not a held-out accuracy estimate
or proof of autonomous success. The
[original demonstration study](W101_DEMONSTRATION_20261108.md) preserves source
provenance and measurement limits. Exact input-to-decoded-frame alignment remains
unverified.

## Native replay CLI

Arguments are positional; use a locally built Maelstrom executable and a **new**
output directory. These command shapes document the contract; placeholders must
be replaced with actual paths. They are not instructions to start live testing.

```text
<Maelstrom.exe> --replay-pet-dance-panels <profile.json> <packet.json> <new-output-directory>
python tools/compare_pet_dance_replay.py <input-log.json> <replay.json> --output <new-comparison.json>
```

The replay command has a two-minute cancellation budget. It sequentially reads
the packet, analyzes every other frame, and retains the selected frame's actual
decoded timestamp. This is 30 samples/s only for a 60 fps packet; it is not a
general cadence resampler. Output `replay.json` contains pinned source/profile/
packet hashes, observations, proposals with `dispatched=false`, recognized
outcomes, rejection reasons, analysis timing, `inputs_sent=0` and
`live_qualified=false`. Comparison refuses to overwrite existing evidence and
keeps visual sequence, human input and result separate.

### Profile version 1

The profile is bounded to 65,536 bytes. JSON names/case match the C# record:

| Field | Contract |
| --- | --- |
| `Version` | Integer `1`. |
| `Width`, `Height` | Canonical full captured-image dimensions, validated by the capture-size guard. Current profile: 1282×760. |
| `Top`, `Bottom` | `[x,y,width,height]` in the canonical image. They are vertically stacked to form the reference panel. Same width; resulting panel no larger than 256×256. |
| `Cue`, `Phase`, `Feedback` | `[x,y,width,height]` within that composite panel, not the original window. Each rectangle must fit. |
| `Mask` | Cue-relative `[centerX,centerY,radius]`; radius at least 8, bounded inside the cue rectangle. |
| `References` | Ten required unique local PNG references: Neutral, Up, Down, Left, Right, Watch, Go, Resolving, Success, Failure; optional eleventh Loading. Each contains `Label`, `File`, `Sha256` and optional `Source` provenance. |
| `SourceVideoSha256` | Hash of the video that supplied calibration references. Must match the packet's source-video identifier. |

Each reference filename is exactly `<Label>.png`, in the profile directory;
linked files and path separators are rejected. Reference bytes are bounded to
262,144 and checked against their hash. Every decoded PNG must have the composite
panel's exact dimensions. Arrows need sufficient difference from neutral; outcome
examples need sufficient colored text. The profile and reference bytes are
pinned and verified unchanged at run completion.

Current [profile](../.local/w101-route-study/human-route-01/fade-study/profile.json):

```json
{
  "Version": 1,
  "Width": 1282,
  "Height": 760,
  "Top": [512, 100, 256, 40],
  "Bottom": [512, 646, 256, 100],
  "Cue": [98, 74, 64, 64],
  "Phase": [13, 51, 232, 28],
  "Feedback": [38, 0, 180, 40],
  "Mask": [31, 30, 19]
}
```

This excerpt omits mandatory references and source hash; it is not an importable
complete profile. The composed panel is 256×140. Reference provenance must retain
the source recording and selected reference frames; hashes alone do not document
how an example was chosen.

### Panel packet

`packet.json` is bounded to 262,144 bytes and has four fields: `RawFile`, `Sha256`,
`TimesMs`, `SourceVideoSha256`. `RawFile` must be `panels.rgb` in the packet's own
directory. The raw file cannot be a link; its SHA-256 is checked before reading.
It contains packed RGB24 panels with no row padding: exact byte count is
`panelWidth × panelHeight × 3 × TimesMs.Length`.

There must be 1–3,600 timestamps. Use finite, nonnegative, strictly increasing
decoded-video milliseconds. Timeline metadata is validated; the observation
gates also reject non-increasing analyzed timestamps. Packet bytes are pinned
and checked unchanged at completion. Do not substitute a source clock or invent
timestamps for missing frames.

The current [packet](../.local/w101-route-study/human-route-01/fade-study/packet.json)
contains 2,580 panels from 58,000 to 100,983.333 ms, covering all five Dance rounds.
The native reader analyzes 1,290 of those panels. Packet extraction/reference
selection is still study tooling; the app does not yet provide a completed
profile-authoring/import workflow for this format.

## Sequence and outcome rules

- Clear directions must persist for 70 ms. Observation gaps over 150 ms discard
  continuity during a round; timestamps must increase and geometry must stay
  fixed.
- A fade belongs only to the last confirmed direction. It has bounded fit error,
  opacity below 0.7 and maximum duration 350 ms. Brightening more than 0.12 above
  its running minimum is rejected. It never counts as a new arrow.
- Low opacity at or below 0.25 followed by another low/neutral sample at least
  16 ms later can establish a trough. A stable neutral interval also rearms the
  reader. This supports repeated identical arrows without counting a held arrow
  twice. The next clear arrow still needs its own stability check.
- Go requires exactly the expected number of separated cues and a final
  confirmed trough/neutral gap. One proposal is emitted; another Watch requires
  an observed result/Ready boundary. The current trial starts at three cues.
- Bottom DONE maps to Resolving. Top success/failure feedback must remain
  consistent for at least 50 ms before latching; stale/discontinuous evidence
  resets the candidate. Success advances the length only after a complete
  proposal. Failure preserves it. Unknown images are not silently converted to
  the previous phase.

These thresholds describe the current implementation and calibrated evidence;
they are not established rules for every W101 UI scale or game level.

## Replay 04 results

[Replay](../.local/w101-route-study/human-route-01/fade-study/replay-04/replay.json)
and [comparison](../.local/w101-route-study/human-route-01/fade-study/comparison-04.json):

| Round | Visual sequence | Observed human input | Observed result |
| --- | --- | --- | --- |
| 1 | Right, Left, Up | Same | Success |
| 2 | Up, Left, Down, Left | Same | Success |
| 3 | Up, Left, Right, Right, Up | Same | Success |
| 4 | Down, Up, Down, Down, Right, Down | Down, Up, Down, Down, Up | Failure |
| 5 | Down, Up, Up, Down, Right, Right | Same | Success |

All five proposals were compared, with no incomplete/unscorable rounds and no
sequence rejection. The incorrect human input in round four remains negative
evidence, not a correct-action label. Analysis mean/max were 2.1473/13.9221 ms;
total replay elapsed time was 19.752 seconds, including I/O and pacing. These
are small-panel processing measurements, not capture fps or input latency.

## Bounded live CLI and supervised cleanup

```text
<Maelstrom.exe> --live-pet-dance-teacher <decimal-window-handle> <profile.json> <new-output-directory>
```

This experimental command requires an identified Wizard101 window. It does not
choose a world/level, navigate menus, establish desktop ownership on the user's
behalf, or perform feeding. The target must already be at an appropriate fresh
game start. The host avoids activation; gameplay input requires current target
focus and unchanged geometry, and stops on observed human activity/takeover.

The output-directory marker protocol is a supervised development interface:

| Marker/report | Meaning |
| --- | --- |
| `standby.json` | Target identified; waiting up to 300 seconds for `start.request`. No gameplay yet. |
| `start.request` | Supervisor permits one bounded trial; activity monitoring starts and the scheduler acquires shared game input ownership. |
| `running.json` | Game observation has started. |
| `stop.request` | Requests cancellation through the host's 50 ms timer. Never a request to resume or refocus. |
| `teacher-complete.json` | Gameplay stopped, inputs disabled, report path recorded; cleanup remains separate. |
| `idle-confirmed.request` | Supervisor has actually used/observed the game menu's native Idle state. It must not be created from a stopped process or an elapsed timeout. |
| `idle-final.png`, `idle-return.json` | Fresh post-confirmation image and hash. The code records supervision; `autonomous_idle_recognition=false`. It does not itself classify the Idle banner. |
| `finished.json` | Final scheduler receipt. Missing confirmation or capture yields CleanupFailed. |
| `failed.json` | Host/setup failure, explicitly leaving native Idle unconfirmed. |

The updated scheduler operation is `w101.pet-dance-live-trial`, version
`panel-geometry-v3`, with a 200-second operation budget. Gameplay has a 75-second
and 32-key ceiling; five observed results end it. Startup/loading has a separate
cumulative 45-second budget and Loading sends no inputs. The old 12-second
unknown-scene rule described the first attempts, not the current code. Frames
must be newer than the previous capture and at most 150 ms old. Every arrow is a
100 ms pulse, with at least 220 ms after release before the next pulse and a fresh
Go observation. Input failures, loss of Go, stale frames, target change, takeover
or cancellation stop the attempt. A released input receipt is distinct from
verified game acceptance.

The operation retains shared ownership through a bounded, at-most-60-second
supervised cleanup wait. No automatic menu actions occur during that wait. Do not
start a competing routine. If the game is loading or the user resumes desktop
use, record unavailable cleanup instead of sending more inputs. On exit the host
disposes its activity hooks; a separately used desktop helper must also stop.

Live extraction now requires exact canonical geometry unless the pinned profile
declares `CaptureGeometry` with `Width`, `Height`, `Transform` and `Evidence`.
Only same-width, one-pixel-shorter source height is currently supported. The two
explicit transforms are `preserve-pixels` (crop the calibrated rectangles without
stretching, only if they fit the source) and `scale-one-pixel-height` (the former
GDI stretch, now opt-in). No automatic mapping selection or threshold widening
occurs. `Evidence` identifies the calibration study; declaring it is not proof
of independent live qualification. Existing profiles are unchanged and are now
exact-geometry-only; they must not silently resume the old live stretch.

The [geometry diagnostic](PET_DANCE_CALIBRATION_DIAGNOSTIC.md) reproduces all
eight known cases becoming Unknown after removing a bottom row and applying
that stretch. Other simulated mappings retain recognition. These are sensitivity
tests, not proof of the live encoder's actual mapping. Recording dimension
ratios alone never establish a measured interior pixel transform.

## Actual independent live result and remaining gates

Latest 2026-11-09 status: attempt 04 recognized loading (93 frames), followed by
642 Unknown frames. Capture content changed 598 times; no arrows or outcomes were
recognized. Supervised cleanup confirmed native Idle within its deadline. A
Completed scheduler receipt describes that cleanup, not successful gameplay.
The [scene-gate log](GAMEPLAY_SCENE_GATES.md) preserves details. A subsequent
continuous supervised visit is saved under `.local/w101-route-study/lifecycle-01/`;
its recorder finalized verified video and released hooks. The supervisor returned
through reward Next, snack Finish without feeding, and the menu's native Idle.
Its non-injected arrows were subsequently confirmed by the user as their
demonstration inputs. It is neither input-free nor autonomous Maelstrom play.
The frozen-profile evaluation and input comparison are documented in
[the full-visit study](PET_DANCE_LIFECYCLE_OBSERVATION_20261109.md).

[Live teacher 02](../.local/w101-route-study/live-teacher-02/live-report.json)
attempted the Wizard City level and saw a persistent W101 loading screen in both
app capture and independent desktop screenshots. It observed 230 frames in
12.176 seconds, mean analysis 10.8901 ms, zero key receipts and zero outcomes,
then stopped with `unrecognized-game-state`. This verifies the no-input stop in
that observed scene; it provides no live Dance accuracy or timing measurement.

[Finished receipt](../.local/w101-route-study/live-teacher-02/finished.json):
**CleanupFailed**. The game was still loading and native Idle was **not
confirmed**. The application and desktop helper were stopped. There is no active
trial, no autonomous success claim and no new package/install. Reports must keep
this failed cleanup separate from zero gameplay inputs. The cause of the loading
screen is unknown. Attempt 03 and its later recovery are documented in
[scene gates](GAMEPLAY_SCENE_GATES.md); its game did start and the supervisor
subsequently confirmed native Idle. Preserve these earlier reports as history.

The earlier fade-study checkpoint passed 362/362 C# and 22 Python tests.
The 2026-11-09 consolidated checkpoint passes 475/475 C# tests, none skipped,
and 41 Python comparator/schema/policy/importer tests. Scoped C# whitespace,
style and analyzer checks pass, as do Python formatting/lint checks. These
checks complement the reports; they do not close the live qualification gate.

Next qualification requires a new sequence with the frozen profile, complete
visual observations, accepted/released keys, independently observed results and
supervised native Idle return. Product integration still needs profile/pack
authoring and validation, clear Start/Stop and reports, and repeatability across
fresh games/allowed geometry. Snack feeding and the knowledge database remain
separate tracks; neither is enabled by this experimental CLI.

## Continuous supervised visit recording

```text
<Maelstrom.exe> --record-w101-lifecycle <decimal-window-handle> <new-output-directory> <seconds>
```

This reuses the bounded passive recorder (2–180 seconds; start/stop request
markers). Start before interacting with the sigil and stop after observed native
Idle. It captures only the selected game window and allowed game inputs, sends
no input, changes no focus and records no audio. It labels the actor
`supervised-ui` and kind `supervised-ui-lifecycle-observation`, with automatic
training disabled. A successful recording is not a qualified controller.
Keep supervisor actions, other observed input, scene evidence and game outcomes
separate. An injected-input flag does not establish who supplied an input.

## Evaluation against a different recording

```text
<Maelstrom.exe> --evaluate-pet-dance-panels <frozen-profile.json> <new-packet.json> <new-output-directory>
```

The original `--replay-pet-dance-panels` command requires the calibration video
identity. Explicit evaluation permits different footage while preserving the
original profile/reference hashes. The report records observation
`source_video_sha256` separately from `calibration_source_video_sha256`, plus
`evaluation_mode` and `separate_recording`. It never rewrites a profile to claim
the new video supplied the old references. Packet bytes/timestamps remain pinned
and checked. Using evaluation mode on the original video still reports
`separate_recording=false`; the switch alone cannot manufacture independence.
