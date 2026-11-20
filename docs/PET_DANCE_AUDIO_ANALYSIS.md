# Offline Pet Dance audio analysis

The [analyzer](../tools/analyze_pet_dance_audio.py) compares recorded sound with visual Dance cues and observed key presses. It uses the existing offline NumPy environment, one numerical worker, and no model, GPU inference, capture device, application launch or game input. **The first measured mapping now covers all four preview arrows:** the [2026-11-09 followup](PET_DANCE_SOUND_MAPPING_20261109.md) records 18 examples from two visits and nine matching predictions in withheld whole rounds. This small same-client study does not qualify audio-only or autonomous play. Synthetic checks establish analysis mechanics separately from the game's measured sound patterns.

The user requested quiet background work overnight. This tool only reads finalized recordings; it neither plays snippets nor changes volume. An Idle-only recording can establish capture/clock behavior but cannot establish Dance direction tones. Earlier video-only recordings contain no recoverable audio.

## Inputs and clock contract

Required inputs are the finalized `game-audio.wav`, `audio-timing.json`, a native panel `replay.json`, and its opt-in `input-events.json`. The source `capture.mp4` remains alongside the input log so its identity can be verified. The analyzer validates WAV/video hashes, replay source identity, selected process ID/start time, video origin/end, and the complete nontruncated input log. It rejects incomplete/unfinalized captures, malformed PCM, unsupported channel layout, inconsistent metadata and files beyond the review limits.

The timing contract is the version-1 output of [GameAudioRecording](../ProjectMaelstrom/ProjectMaelstrom/Utilities/GameAudioRecording.cs):

| Field | Required meaning |
| --- | --- |
| `kind`, `scope`, `status`, `finalized` | `selected-process-audio`, `selected-process-tree`, `complete`, `true`. No microphone/system-mix substitution. |
| `wav_file`, `wav_sha256`, `format` | Exact WAV filename and hash; PCM16 stereo, declared sample rate between 8 and 96 kHz. Current capture is expected at 44.1 or 48 kHz. |
| `target_process_id`, `target_process_start_utc_ticks` | Same target process instance as the input log. |
| `video_capture_origin_ms`, `video_capture_end_ms`, `video_verified`, `video_sha256` | Shared absolute-QPC origin/end and verified video identity. |
| `packets[].frame_offset`, `frame_count` | Contiguous sample-frame positions in the WAV. A frame contains both channels. |
| `qpc_position_100ns`, `qpc_ms`, `device_position` | Original packet timestamp/position; `qpc_ms = qpc_position_100ns / 10000`. Alignment uses QPC. A zero device position on every packet does not invalidate advancing QPC. |
| `device_position_qualified` | Recorder's qualification flag, retained in the report and false when absent. Raw device positions remain provenance; the analyzer does not use them to infer time. |
| `flags`, `silent`, `discontinuity`, `timestamp_error` | Consistent WASAPI flags: 1 = discontinuity, 2 = silence, 4 = timestamp error. |

Audio can begin before video. The audio allowance is 210 seconds to accommodate up to 30 seconds of startup before a 180-second video. WAV index zero is **not** assumed to be video time zero. Each requested window maps through packet QPC timestamps, including the recorded sample offset. A window outside coverage or crossing a discontinuity/clock gap receives `unknown`; silence is a separate measured state. Missing packets are never replaced with invented samples. Adjacent packet clock joins have a fixed 2 ms tolerance, exposed in the report; larger overlaps are rejected and larger gaps split usable coverage.

Input timestamps use their recorded shared-QPC origin. Visual events use that origin plus decoded video time and remain provisional: rate-converted video is not a verified source-frame timeline. Neither the tool nor the presence of both clocks proves audio latency, drift or exact visual/input alignment. Reports retain `exact_decoded_video_alignment_verified=false`.

## Event types and features

| Event type | Evidence and treatment |
| --- | --- |
| Watch direction | A growing native `Cues` list must agree with the proposed sequence and a run of full-arrow observations. At least two observations over 50 ms confirm the run. The first full-arrow frame estimates its onset; fades/held duplicates do not create extra onsets. Repeated directions require a new visual run. |
| Response key | A focused, non-injected direction key-down inside one unambiguous Go-to-result interval. OS repeats while held collapse. Injected/out-of-focus/ambiguous-round inputs are excluded. The flag does not identify a person; user attribution belongs in a separate session annotation. |
| Go and result | Kept as separate sounds. Success/Failure stays attached to the observed round; a failed round is not relabeled correct. |
| Scene/phase change | Watch, Loading, Unknown and Resolving transitions remain descriptive events, not direction labels. An empty replay creates no invented negative labels. |

Each event uses a 220 ms sound window beginning at the estimated event time and a 120 ms baseline ending 80 ms before it. The report stores bounded source sample ranges for later snippet review; it does not export or play sound files. Features include channel-preserving spectral power, dominant spectral peak, secondary peaks, RMS level, local spectral change, background similarity and an 84-bin normalized spectral-change descriptor. Stereo channels are combined by power, avoiding cancellation of opposite-phase channels.

The baseline and sound window use the same spectral resolution; otherwise an unchanged note can appear different merely because the windows have different lengths. The strongest peak is **not** asserted to be the fundamental pitch. Mixed peaks may represent music, another cue or harmonics. Silence, unclear mixtures, unchanged music/held sounds and timing gaps remain explicit states. A pure-looking tone can still be music; these features do not establish causation.

Feature version `spectral-novelty-v2-boundaries` also rejects dominant energy outside the analysis band or a peak at its boundary. During the first audio visit, 23 of 24 examined energy rises initially peaked at 80.75 Hz, the first included frequency bin, while sparse video samples showed loading. Filtering out low bass can leave such a spectral tail; it is not evidence of an 80.75 Hz Dance note. The unfiltered peak and boundary flags are retained, and these windows are excluded from template fitting. This is a descriptive negative-context finding, not a direction-tone measurement.

## Round separation and reporting

Without explicit calibration and held-out round lists, the tool produces descriptive features only. A requested split must use disjoint whole rounds from this recording. Templates use eligible **Watch cues in calibration rounds only**. Held-out Watch cues and response keys are evaluated separately; response-key sounds are never silently assumed to be identical to preview sounds.

All four directional templates are required before classification. A prediction also requires cosine similarity of at least 0.8 and a best/second-best margin of at least 0.08; otherwise it is Unknown. These fixed exploratory thresholds have not been qualified on real game audio. Results preserve raw similarity scores and counts of agreement, disagreement and unknowns by event type. They do not present a single-session fit as general accuracy.

Round-level withholding reduces direct fit leakage but does not eliminate shared music, volume, recording or scene confounds. Real qualification needs new sessions, repeated notes, mixed/quiet conditions and measured timing uncertainty. If evidence is later used for tuning, move subsequent evaluation to fresh recordings. This tool never makes a pack live-qualified or automatically eligible for training.

## Run locally

Example using the first complete Dance audio recording and native replay:

```powershell
& '.local/envs/dance/Scripts/python.exe' tools/analyze_pet_dance_audio.py `
  '.local/w101-audio-study/dance-02/video/game-audio.wav' `
  '.local/w101-audio-study/dance-02/video/audio-timing.json' `
  '.local/w101-audio-study/dance-02/review/native-replay-01/replay.json' `
  '.local/w101-audio-study/dance-02/video/input-events.json' `
  --output '.local/w101-audio-study/dance-02/review/audio-round-split-new.json' `
  --calibration-rounds 1,2 --heldout-rounds 3
```

The source paths are real local evidence, but choose a new output path on each run. This single-recording example returns insufficient direction coverage because Right never appears in `dance-02`; it must not be reported as a four-direction test. The later combined study uses explicitly identified whole rounds from both visits and preserves their source IDs. Its cross-recording comparison is a research artifact, not a new CLI mode. Omitting both split arguments leaves a run descriptive. Existing outputs are never overwritten.

Bounds: at most 180 seconds of video/events and 210 seconds of audio samples/QPC coverage, 84 MiB WAV (enough for 210 seconds of PCM16 stereo at the supported 96 kHz maximum), 512 MiB source video for identity verification, 24 MiB per JSON input, 40,000 audio packets and 512 selected events. Numerical-library thread counts are set to one before NumPy imports. Processing holds the bounded PCM16 recording in memory and computes short windows; it performs no audio/video transcode and starts no background service.

## Validation and limits

[Synthetic tests](../tools/tests/test_analyze_pet_dance_audio.py) cover shared-clock offsets, early audio start, gaps, discontinuities, timestamp errors, silence versus missing coverage, malformed metadata, source/target mismatch, unfinished or truncated WAV, duration limits, repeated arrows, held keys, focus loss, injected input, ambiguous results, opposite-phase stereo, pure/mixed/steady tones, 44.1/48 kHz features and round-separated fitting. No real game files or audio devices are used by tests.

Synthetic coverage includes a full 210-second audio sidecar with 30 seconds of startup before 180 seconds of video, the unchanged 180-second event limit, and zero device-position counters with advancing QPC. A separate event-only check against the existing `lifecycle-01` replay/input evidence recovered 20 Watch cues, 12 manual key presses, five Go events and five outcomes, with no extraction warnings. That recording has no audio; this check validates compatibility with native event records only.

Synthetic coverage also verifies that bass below the analysis band and cutoff-boundary peaks do not become accepted in-band notes. Formatting, lint and unit results are recorded with each change; no test-suite timing is a real recording throughput benchmark.

```powershell
& '.local/envs/dance/Scripts/python.exe' -m unittest discover `
  -s tools/tests -p test_analyze_pet_dance_audio.py -v
```

## First real audio recording: `dance-01`

The finalized [recording](../.local/w101-audio-study/dance-01/video/capture.mp4) and [audio sidecar](../.local/w101-audio-study/dance-01/video/audio-timing.json) provide usable signal/timing evidence but no qualified arrow-to-tone examples. The offline review extracted exactly eight full video frames at 0, 5, 12, 18, 30, 55, 80 and 105 seconds. The first shows native Idle at the Dance sigil; 5/12 seconds show the Dance menu, including the `Wizard City` tooltip at 12 seconds; all five later samples show loading artwork. **No gameplay appears in those samples; uninspected intervals remain unknown.** No full-video classification or native panel replay was run.

See [contact sheet](../.local/w101-audio-study/dance-01/review/offline-inspection-01/contact-sheet.jpg), [scene observations](../.local/w101-audio-study/dance-01/review/offline-inspection-01/observations.json) and [v2 signal review](../.local/w101-audio-study/dance-01/review/offline-inspection-01/audio-signal-review-v2.json). Original recording metadata and the earlier v1 descriptive review remain unchanged.

| Measurement | Result and scope |
| --- | --- |
| Audio | 108.12 seconds, PCM16 stereo at 44.1 kHz; 10,812 packets in one usable QPC segment. |
| Timing/identity | WAV and video hashes verified against metadata/input identity. Audio begins 591.7455 ms before the first video frame. All raw device positions are zero; QPC advances normally. |
| Capture integrity | Zero queue drops, packet discontinuities, timestamp errors and clipped sample values. |
| Signal level | Whole-file RMS −48.68 dBFS; peak −27.12 dBFS. Audio is present; no playback or volume change was used in review. |
| Energy changes | 163 coarse rises using a fixed 100 ms envelope screen; 136 occur in the 18–107.55-second interval where sampled frames show loading. They are unlabeled changes, not 163 detected cues. |
| Bounded spectral inspection | Only 24 strongest rises received detailed spectra. V2 marks 23 as out-of-band/boundary peaks and one as a mixed/harmonic ambiguity. None supplies an accepted directional sound label. |
| Input provenance | Injected X and mouse navigation only; zero directional key-downs. Mouse button `Left` is not an arrow key. |
| Video throughput | 5,853 source frames, approximately 54.42 fps, maximum source gap 133.33 ms; 6,453 nominal-60-fps encoded frames. Extra encoded frames are not new observations. |
| Geometry/latency | Encoded 1282 × 760, source 1282 × 759; the pixel transform remains unverified. The recording also reports a negative median delivery-delay metric (−8.93 ms); this is retained, not treated as qualified end-to-end latency. |

The menu's `Games Won 2/25` is a pre-game literal, not a new win attributed to this attempt. No successes, failed rounds, direction notes or autonomous actions were inferred. The coordinator separately reported restoring native Idle after the trial; that later cleanup is outside this recording's inspected endpoint.

After the real-data boundary correction, **all 25 synthetic tests passed in 0.233 seconds**; Black formatting/AST-equivalence/stability and Pyflakes passed. These are test-suite timings, not a real recording throughput benchmark. The Python analyzer remains an offline checkout tool, separate from the native capture package.

Status at the end of `dance-01`: offline analyzer and synthetic validation implemented; real source audio readable and mapped to packet QPC, but no actual Dance note mapping available. This failed-loading evidence is preserved rather than relabeled by the subsequent success.

## Followup after restart: `dance-02` and `dance-03`

The [measured mapping study](PET_DANCE_SOUND_MAPPING_20261109.md) supplies 18 visually checked preview cues across six complete, unanswered rounds. Frozen visual references and unchanged audio features recognized the four recorded sound patterns. An explicit whole-round split produced nine matching withheld predictions, with no disagreements or Unknown results. Both recordings contribute calibration data and share a restarted client; this is exploratory evidence, not untouched-session accuracy.

Up and Down share a dominant component near 522 Hz, so the measured mapping uses spectral shape rather than a single pitch. Exact timing, continuous audio onset detection, music rejection, response-key sounds and cross-session/live reliability remain unqualified. Both visits returned to courtyard native Idle without snacks or volume changes. No audio-driven actions or production-code changes were introduced. See [game audio design](GAME_AUDIO_CUES.md) for capture scope and later qualification gates.
