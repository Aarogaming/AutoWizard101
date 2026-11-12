# Pet Dance lifecycle observation — 2026-11-09

The unchanged Pet Dance calibration produced five proposed sequences on a **different recording**. Three sequences matched the user's recorded arrows and received native replay `Success` outcomes. The two other rounds contained no arrow presses and received `Failure` outcomes. This is useful offline perception evidence, not an autonomous teacher qualification or a measured 60% prediction accuracy.

## Source and supervision

The local recording is [lifecycle-01/video/capture.mp4](../.local/w101-route-study/lifecycle-01/video/capture.mp4), lasting 130.133 seconds. Its SHA256 matches the verified, nontruncated [input log](../.local/w101-route-study/lifecycle-01/video/input-events.json). The recording manifest and input log identify the same target process instance.

The user confirmed they manually pressed arrows during several rounds to compare directions with tones. The session therefore combines supervised menu navigation with a user demonstration. All 12 directional presses and releases have `Injected=false`; that flag alone would not establish identity. Menu X/Escape/mouse events have `Injected=true`. The recorder's `inputs_sent=0` describes the recorder, not all session interaction.

The original [demonstration manifest](../.local/w101-route-study/lifecycle-01/demonstration.json) remains unchanged. [supervision-annotation.json](../.local/w101-route-study/lifecycle-01/review/supervision-annotation.json) records the additional attribution. The input log shows an initial focus loss, focus gained at approximately 4.820 seconds, and no subsequent focus loss. All directional presses occurred while focus was logged active and all were released.

**Audio was disabled.** This recording cannot label tones, establish a direction-to-tone mapping, or test the user's audio hypothesis. Exact decoded-frame/input alignment remains unverified because the encoder converted the capture cadence. Automatic training eligibility remains false pending explicit label review.

## Bounded inspection and packet

Only 12 full frames were extracted for visual inspection, at requested video times 6, 13, 20, 31, 36, 44, 54, 64, 72, 82, 101 and 123 seconds. See the [contact sheet](../.local/w101-route-study/lifecycle-01/review/inspection-contact-sheet.jpg) and [inspection manifest](../.local/w101-route-study/lifecycle-01/review/inspection.json). These requested times select scenes; sequence comparison uses actual decoded timestamps below.

| Requested time | Visible evidence |
| --- | --- |
| 6 and 13 s | Dance selection screen; hovered level reads `Wizard City`; `Games Won 1/25`. |
| 20 s | Loading artwork. |
| 31 s | `OOPS!!!` and `DONE!`; failed-round feedback, not an overall game result. |
| 36, 44 and 54 s | `GO!` response phase. |
| 64 s | `MATCH THIS...` and a Down arrow. |
| 72 and 82 s | Reward screen. |
| 101 s | Returned to Pet Pavilion with the dance prompt. |
| 123 s | `YOU ARE IDLE` visible. |

The [panel packet](../.local/w101-route-study/lifecycle-01/review/packet/packet.json) contains 3,300 decoded frames from 20 seconds inclusive to 75 seconds exclusive. Their actual first/last timestamps are 20,000 and 74,983.333333 milliseconds. FFmpeg's integer PTS and time base `1/60000` supply the timestamps; nominal frame indexes were not substituted. Extraction used one decoder/filter/encoder thread and no models, desktop access, application launch or native build.

The original [frozen profile](../.local/w101-route-study/human-route-01/fade-study/profile.json), all 10 reference images, coordinates and thresholds remain unchanged. The panel stacks these decoded-pixel rectangles without resizing:

| Region | X | Y | Width | Height |
| --- | ---: | ---: | ---: | ---: |
| Top | 512 | 100 | 256 | 40 |
| Bottom | 512 | 646 | 256 | 100 |

The output is 256 × 140 RGB. Seven spot checks at 20, 31, 36, 44, 54, 64 and 72 seconds matched independently extracted full-frame crops exactly: panel, phase and cue RMS differences were all zero. Packet timestamps match the recording's existing decoded-timestamp inspection within 0.000334 ms. [panel-provenance.json](../.local/w101-route-study/lifecycle-01/review/panel-provenance.json) retains the extraction command, integer PTS, raw hash and original profile hash; [validation.json](../.local/w101-route-study/lifecycle-01/review/validation.json) retains the checks.

## Native replay and input comparison

The parent session ran `--evaluate-pet-dance-panels` against this new packet and the original frozen profile. Evaluation explicitly retains both the new `source_video_sha256` and original `calibration_source_video_sha256`; the source identity was not rewritten to bypass a check. The native evaluator caps review cadence at 30 fps while retaining selected decoded timestamps, so it analyzed **1,650 of the 3,300 packet frames**.

Evidence: [native-replay-01/replay.json](../.local/w101-route-study/lifecycle-01/review/native-replay-01/replay.json) and [native-comparison-01.json](../.local/w101-route-study/lifecycle-01/review/native-comparison-01.json). All times below are seconds on the decoded video clock or provisional input-log clock, rounded to three decimals.

| Round | Go | Proposed sequence | User arrow sequence | Key-down times | Native outcome/time | Comparison |
| --- | ---: | --- | --- | --- | --- | --- |
| 1 | 24.100 | Up, Down, Right | None | — | Failure / 30.167 | Different: no response recorded. |
| 2 | 34.700 | Right, Right, Up | Right, Right, Up | 36.355, 36.867, 37.431 | Success / 37.533 | Exact sequence agreement. |
| 3 | 43.267 | Down, Down, Left, Left | Down, Down, Left, Left | 44.345, 44.869, 45.406, 45.882 | Success / 45.967 | Exact sequence agreement. |
| 4 | 51.833 | Left, Left, Right, Up, Left | None | — | Failure / 59.900 | Different: no response recorded. |
| 5 | 65.933 | Down, Down, Down, Up, Left | Down, Down, Down, Up, Left | 66.355, 66.571, 66.764, 66.977, 67.213 | Success / 67.300 | Exact sequence agreement. |

There were five compared rounds, three matching sequences, two different sequences, zero incomplete/unscorable rounds and zero replay rejection reasons. The two differences are absent user responses; they do not demonstrate incorrect proposed cues. The three matching, successful manual responses corroborate the visual interpretation in those rounds. They are a small independent-recording sample, not broad accuracy or timing certification. Outcomes in the table come from the native feedback recognizer; the 12-frame manual inspection independently confirms the first failed-round message but does not independently annotate every outcome glyph.

Mean native analysis time was **1.852 ms/frame**, maximum 13.663 ms/frame, and total replay elapsed time 25.161 seconds. This includes a bounded offline review, not an end-to-end live reaction benchmark. Every proposal was undispatched, replay sent zero inputs, and `live_qualified=false`.

## Reward and capture observations

The selection screenshot shows `Games Won 1/25` and Agility `24/230`. The reward screenshot literally shows `GOOD JOB!`, `YOUR PET HAS IMPROVED.`, `Games Won 2/25`, Experience `15/250` with `+2`, and Agility `26/230` with `+2`. These literals and counter changes are preserved separately from round-level outcomes. No overall win/loss rule is inferred solely from the reward wording or counter.

Both the earlier calibration recording and this recording report a **1282 × 759 capture source** and **1282 × 760 encoded video**, with Windows Media Transcoder. This new encoded gameplay works with the old decoded-pixel calibration: fixed-profile spot checks recognized Resolving at 31 seconds, Go at 36/44/54 seconds, and Watch/Down at 64 seconds. The old 10-reference profile returns Unknown on menu/loading/reward scenes; it contains no Loading reference.

The matching dimensions do **not** establish the raw-capture-to-encoded-pixel transform. The metadata's vertical scale value is calculated from dimensions, not a measured registration. The [calibration diagnostic](PET_DANCE_CALIBRATION_DIAGNOSTIC.md) showed that an implicit one-pixel resize can invalidate otherwise correct phase/cue matches. This recorded-video result therefore does not qualify any particular live 759-to-760 mapping.

Capture delivered 7,056 source frames at approximately **54.22 fps**, with a maximum source gap of **316.66 ms**. The encoded stream contains 7,808 frames at nominal 60 fps. Additional encoded frames are not independent observations. Source/encoded timing and the new geometry declaration must be qualified before claiming reliable live throughput or reaction timing.

## Preserved evidence and next qualification

[evidence-hashes.json](../.local/w101-route-study/lifecycle-01/review/evidence-hashes.json) indexes the original manifest, input log, frozen profile and reference images, packet, native replay, comparison and review annotations. Large media hashes are retained from the verified extraction pass; small evidence files were checked again. Key SHA256 values:

| Artifact | SHA256 |
| --- | --- |
| New lifecycle video | `88329347100875165C93A777B0AD7B997D372DC73C8267F8FD4EDDCC04B40317` |
| Original calibration video identity | `07B52BE94A5C1471E817F84B09C37AFF2C15958460950404119880FC265321EF` |
| Frozen profile | `65201CE7CAB3093DBF5AFCAF6C2FEA35C1B7F1EDC1A462FE2D6FD0E56CD2D40B` |
| Input log | `C6C48CC15C73CF9ACE84E2541A7676E4CA69B57A9189955622BC0DA68BD75211` |
| Packet manifest | `86C3778B9C1D5A89B495DC872323581D978B3E9852954491EDCC7EC6ED1011E5` |
| Native replay | `ED322820E82275EE59ADF826C2A919FD8AF231E7DD9DDD74136709B55031659C` |
| Input comparison | `ACF5935B8FEC8F98454CF01388A6751B26189589BAF50BC7ED5A654078BAB65C` |

Next qualification is a measured live-frame geometry mapping followed by Observe-only recognition on new live frames, then a bounded teacher attempt under the existing input-owner/cleanup gates. Keep this recording as frozen evaluation evidence; do not tune thresholds against it and continue calling it independent evaluation. If later used for training, retain its original observations and move subsequent evaluation to fresh sessions. An optional future game-only audio recording needs separate timing/provenance before it can supply tone labels.
