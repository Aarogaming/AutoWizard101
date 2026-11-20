# Pet Dance: first measured arrow/sound mapping

**2026-11-09.** Two supervised visits after the user's Wizard101 restart
produced 18 preview arrows with corresponding recorded sound windows, covering
all four directions. Both visits reached the reward screen and returned to
confirmed native Idle in the courtyard. The unchanged visual reader recognized
six complete sequences with no rejection. These were deliberately unanswered
games for sound observation, not successful autonomous play.

## Question and limits

The question was whether Dance's preview sounds carry a repeatable direction
signature that a small local classifier can use alongside visual arrows. The
installed selected-process recorder captured game video, game audio and opted-in
inputs. Analysis used the existing NumPy environment, one numerical worker,
bounded decoding and small image panels. No model, audio playback, microphone,
system-mix fallback, volume change or game-memory access was used.

Both visits used the same restarted client, 1280 × 720 and Tiny UI. The user had
reported setting volume to 20%; this study did not read or alter the volume
setting. Calibration and test rounds from these visits share the game session,
music, configuration and recording implementation. They cannot establish
reliability across clients, levels or audio settings.

## Observed sequences

| Visit | Round 1 | Round 2 | Round 3 |
| --- | --- | --- | --- |
| `dance-02` | Left, Up, Down | Down, Left, Down | Left, Up, Down |
| `dance-03` | Right, Down, Left | Up, Up, Up | Right, Right, Up |

All six native outcomes were Failure, consistent with sending no response
arrows. The supervisor used ordinary UI controls to start each visit, leave
the result, skip snack feeding and select native Idle. The recorder itself sent
no inputs. Replayed sequence proposals were not dispatched. These distinctions
prevent a recorded preview or an accepted menu click from being counted as a
correctly played round.

The visual profile and all ten reference images were frozen from an earlier
human demonstration. Profile SHA256:
`65201CE7CAB3093DBF5AFCAF6C2FEA35C1B7F1EDC1A462FE2D6FD0E56CD2D40B`.
Native replay retained actual decoded timestamps; it did not invent a linear
sequence of notes. In particular, the second visit includes repeated Up and
Right cues. Full-arrow crops provide a separate visual check of the labels.

## Measured sound patterns

| Visual direction | Preview examples | Observed spectral pattern |
| --- | ---: | --- |
| Left | 4 | Components around 328–334 Hz and 662 Hz; the strongest varies. |
| Up | 6 | Strong component around 522 Hz. |
| Down | 5 | Strong component around 517–522 Hz, with another around 791 Hz. |
| Right | 3 | Strong component around 393 Hz. |

These are measured components in short, mixed game-audio windows, not certified
fundamental pitches or musical-note names. **Up and Down cannot be separated
by strongest pitch alone in these recordings.** The wider spectral pattern is
more informative. All 18 Watch windows passed the existing descriptive feature
checks; that is not proof that every usable window in a complete soundtrack is
a direction cue.

The analyzer's 84-bin spectral-change descriptor compares a 220 ms sound window
with a preceding baseline. Background music remains in the selected-process
audio. There is no source separation or neural inference. The numerical
comparison uses the existing four-direction gate, minimum cosine similarity
0.8 and best-versus-second margin 0.08. The first visit alone had no Right
example, so its standard evaluation correctly returned Unknown rather than
pretending three available classes covered the game.

## Whole-round evaluation

Before classification, the declared split used `dance-02` rounds 1–2 and
`dance-03` round 1 for calibration. The latter is the earliest complete round
containing the previously missing Right. Calibration therefore has nine cues:
Left 3, Up 1, Down 4 and Right 1.

The withheld set is `dance-02` round 3 and `dance-03` rounds 2–3: nine cues,
Left 1, Up 5, Down 1 and Right 2. No direction thresholds, features or visual
references were retuned for those rounds. The split is at whole-round level;
neither recording nor the client session is wholly untouched by calibration.
In particular, only Up in the second recording has no calibration example
from that recording. Report this as exploratory round-separated evidence,
not independent-session accuracy.

The [saved comparison](../.local/w101-audio-study/comparison-02-03/comparison.json)
matched **all nine withheld cues** to their visual labels: Left 1/1, Up 5/5,
Down 1/1 and Right 2/2, with zero disagreements and zero Unknown results. The
lowest best similarity was 0.981623 and the smallest separation from the
runner-up was 0.210822, above the unchanged 0.8/0.08 thresholds. The
[split receipt](../.local/w101-audio-study/comparison-02-03/split-receipt.json)
was saved before classification. Reports retain original recording, round,
event and source identities. This small result supports further development;
it is not a general accuracy percentage or a successful playing result.

## Capture, cleanup and loading

| Measurement | `dance-02` | `dance-03` |
| --- | ---: | ---: |
| Finalized audio | 120.26 seconds | 119.01 seconds |
| Audio format | PCM16 stereo, 44.1 kHz | PCM16 stereo, 44.1 kHz |
| Audio writer drops / errors | 0 / 0 | 0 / 0 |
| Unique source capture rate | about 52.74 fps | about 53.16 fps |
| Largest source-frame gap | about 283 ms | about 300 ms |
| Observed rounds | 3 complete, unanswered | 3 complete, unanswered |
| Final observed game state | Courtyard, native Idle | Courtyard, native Idle |

The nominal 60 fps encoded video includes duplicated observations and is not
reliable 60 fps source capture. Encoded geometry is 1282 × 760 while source
capture is 1282 × 759; live pixel mapping remains unqualified. Packet QPC maps
the WAV to requested windows, but exact decoded-frame/audio latency remains
unmeasured. Negative delivery-delay metrics are preserved in raw receipts and
are not interpreted as physical latency.

Both owned recorders stopped and released their hooks. Desktop control was
released after each visit. The final contact sheet also preserves the native
Idle banner over the courtyard. No snack was fed; game energy decreased by
four per visit. This study therefore did consume eight energy in total.

The user's restart resolved the observed loading transition for these two
visits. The earlier client had been running about 12 h 44 min at capture start;
the first new visit began about 2 min 15 s after creation of the restarted
client. See the [restart comparison](W101_SESSION_RECOVERY_20261109.md).
This is not a diagnosed memory leak, server fault or justified restart interval.

## Evidence and reproduction

- First visit: [recording and timing](../.local/w101-audio-study/dance-02/video/),
  [native replay](../.local/w101-audio-study/dance-02/review/native-replay-01/replay.json),
  [audio features](../.local/w101-audio-study/dance-02/review/audio-comparison-01.json)
  and [observations](../.local/w101-audio-study/dance-02/review/OBSERVATIONS.md).
- Second visit: [recording and timing](../.local/w101-audio-study/dance-03/video/),
  [native replay](../.local/w101-audio-study/dance-03/review/native-replay-01/replay.json),
  [audio features](../.local/w101-audio-study/dance-03/review/audio-comparison-01.json)
  and [scene contact sheet](../.local/w101-audio-study/dance-03/review/contact-sheet.jpg).
- Combined test: [declared split](../.local/w101-audio-study/comparison-02-03/split-receipt.json)
  and [each prediction with all class scores](../.local/w101-audio-study/comparison-02-03/comparison.json).
- [Analyzer contract and command](PET_DANCE_AUDIO_ANALYSIS.md) describes identity,
  timing, window, feature and classification checks. The original recordings,
  including the failed loading attempt, remain unchanged.

This turn's focused validation passed all 25 audio-analysis unit tests in
0.224 seconds, Pyflakes, and Black formatting/equivalence/stability checks.
No production code changed for this study, and no new package or installation
was needed. Test duration is not a gameplay-processing throughput measurement.

Raw `.local` artifacts are workspace evidence and are not bundled with the app.
Preserve the recordings and hash receipts when exporting the study. Audio
templates remain research data; this study does not enable audio-driven input
or automatically mark a pack as trained.

## Next qualification

Use sound first as a second observation of a visually identified Watch cue.
Before audio may fill visual gaps, measure audio onset detection without using
arrow timestamps, false positives during music/menus/loading, response-key
sounds separately from preview sounds, timing uncertainty and fresh-session
performance. A reliable combined detector must retain cue order, repeated-note
boundaries and Watch/Go/result state. This measured mapping is a useful basis
for that work; it does not close those gates by itself.
