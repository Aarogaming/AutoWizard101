# Commons and Pet Dance demonstration — 2026-11-08

The user performed the route and one five-round Dance game while Maelstrom
passively recorded. This study uses the saved footage; no desktop control,
automatic gameplay, model inference or web upload occurred during collection
or review. The recording ends with the user having restored native Idle at the
Dance sigil. No further desktop interaction was needed.

## Preserved evidence

Root: `.local/w101-route-study/human-route-01/` (local evidence, not bundled).

- `video/capture.mp4`: 147.950 seconds, SHA-256
  `07B52BE94A5C1471E817F84B09C37AFF2C15958460950404119880FC265321EF`.
- `video/input-events.json`: 6,202 events, no truncation; selected foreground
  W101 keys and mouse only. Video digest independently rechecked after recording.
- `input-review.json`: 58 observed key holds, all with releases; repeated
  key-down events collapsed as repeats rather than additional intended actions.
  Also 12 mouse presses and releases, 5,964 mouse moves, and focus transitions.
- `video/timing.json`, `video/source-timestamps.json`, `section-timing.json`:
  source, submission and decoded clocks; geometry and rate-conversion disclosure.
- `overview.png`, `round1-cues.png`, `feedback-overview.png`: review contact
  sheets. The coarse overview samples near 5, 15, 25… seconds, not at exact
  scene boundaries. Full source video remains the authoritative visual evidence.
- `native-calibration-02/calibration-review.json`: compiled native matcher
  output, source/configuration hashes, draft-pack location and prediction scores.

Recording stopped on the user's Done message, finalized successfully, released
input-observation hooks and exited. It sent zero game inputs. CPU time reported
by the recorder covers only its own process, not total system/driver/encoder work.

## What the demonstration establishes

The recording starts at the existing Commons port anchor. No End key was
recorded, so this demonstration alone does not establish a repeated port reset.
It shows walking across the bridge, approaching the signed Pet Pavilion tunnel,
loading into the Pavilion, approaching the Dance tent/sigil, opening Dance,
playing five rounds, reaching the pet reward screen, returning outside, viewing
the Pavilion map, and entering native Idle. These are observed landmarks and
human actions, not an executable or independently validated autonomous route.

The observed forward holds include simultaneous short A/D corrections. Mount,
camera, player position and zone loading are route context. A future route should
use those observations to propose bounded movement between recognized landmarks;
it must not replay a seven-second forward hold without progress checks. Loading
needs its own wait state. The visible Shopping District tunnel at the initial
anchor is distinct from the Pavilion entrance later reached by the user.

## Capture quality

| Interval | Captured frames/s | 95th-percentile gap | Largest gap | Gaps over 150 ms |
| --- | ---: | ---: | ---: | ---: |
| Entire recording | 52.64 | 22.24 ms | 694.43 ms | See source clock |
| Route window, approximately 5–45 s | 51.92 | 22.22 ms | 694.43 ms | 2 |
| Dance window, approximately 58–100 s | 58.35 | 22.22 ms | 22.23 ms | 0 |

7,788 source frames were captured; the application queue rejected none. This
does not prove every rendered game frame was delivered. Encoded video remains
1282×760 from a 1282×759 captured window and includes Windows frame-rate
conversion. The 60 fps output is not 60 independent source observations. Exact
input-to-decoded-frame alignment remains unverified. Section offsets are review
windows, not independently measured scene-transition timestamps.

## Tiny UI calibration and an actual limitation

The same compiled `PetDanceVision` used by the teacher UI was calibrated on the
recorded neutral frame at 60.000 s and right arrow at 61.100 s. It selected scale
**0.55**, with cue region **610, 680, 64, 64** in the **decoded 1282×760 video**.
This calibration must not be applied unchanged to the 1282×759 live surface.

Native review recognized neutral, Right, Left, Up and Down in selected clear
frames. It left the fading Up frame at 62.200 s Unknown (score 0.955, below the
existing 0.96 threshold). Clear Up at 62.000 s scored 0.969 and was recognized.
No threshold was relaxed. Seven selected frames are a calibration sanity check,
not an accuracy benchmark or an independent-game evaluation; two are calibration
references, and all come from this one recording.

The first round visibly shows Right → Left → Up during MATCH THIS, followed by
GO and the same three observed user keys. The bottom DONE label alone is not a
success label. Top feedback reads AWESOME at 65, 72.5, 80.5 and 99.5 seconds and
OOPS at 90 seconds. Keep those outcome observations separate from the user's
inputs: the failed round's keys must not become an assumed correct cue sequence.
These feedback labels were reviewed by the assistant, not entered by the user.

The current strict sequence state machine invalidates a round on an Unknown
cue. Real fade transitions therefore need separate treatment and replay tests
before live teaching. A guessed neutral gap would incorrectly merge or split
repeated arrows; preserving Unknown is preferable until that logic is qualified.

## Reproduction and next gate

The development app now exposes the existing UI calibration machinery for a
bounded, repeatable offline review:

```text
Maelstrom.exe --calibrate-pet-dance-recording <petdance.zip> <recording.json> <request.json> <new-output-folder>
```

Request fields: `CueIndex`, `NeutralIndex`, `Region` (`[x,y,width,height]`) and
`KnownCue` (`Up`, `Down`, `Left` or `Right`). Use distinct frames in an ordinary
completed teaching recording; independent check recordings remain excluded.
The command creates a new draft and report, leaves source labels unchanged,
checks source/configuration hashes, uses one OpenCV thread, and has a two-minute
timeout. It neither dispatches keys nor changes native Idle. The report does not
qualify phase recognition, sequence accuracy or gameplay success.

Next: preserve original capture geometry/timing for runtime calibration; recognize
Watch/Go/result regions; distinguish verified fades from missing observations;
replay the five recorded rounds against the observed inputs and outcomes. Then
test on an independent game through the shared scheduler and bounded input owner,
restoring native Idle afterward. The Commons route still needs landmark-based
execution and independent arrival verification. This turn did not package or
install an update, and the draft is not enabled for live input.

Validation: 351 application tests passed, including calibration-request bounds
and named-direction checks. Targeted C# formatting/style checks passed, and 62
local documentation links resolved. The native calibration review completed
with no game inputs. No recorder, model or desktop-control session remains active.
