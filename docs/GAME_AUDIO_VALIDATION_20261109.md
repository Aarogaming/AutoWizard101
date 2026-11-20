# Selected-game audio: validation, 2026-11-09

Selected-process capture completed on this machine for quiet test hosts and
W101. The later controlled two-process test passed with selected/excluded tones
swapped. **Dance-note recognition, autonomous play and exact audio/video
synchronization remain unqualified:** the Dance attempt stayed on loading. A
portable candidate exists, with final archive/installer/install validation still
pending. Implementation and future cue-learning gates are in
[Game audio cues](GAME_AUDIO_CUES.md) and the
[native backend contract](PROCESS_AUDIO_CAPTURE.md).

During the earlier quiet checks, the user was going to bed and reported volume
at 2%. No test-tone start marker
was created, no volume setting was read or changed, and no microphone, game input
or focus acquisition was used. The passive W101 recorder released its input
observation hooks. A fresh [Idle image](../.local/w101-audio-study/w101-idle-01/idle-01.png)
was visually confirmed by the supervising session to show **YOU ARE IDLE**; this
is not an autonomous Idle detector result. The later user-authorized tone tests
below used the unchanged user-reported 20% volume setting. These reported values
describe different checks; the recorder did not adjust the volume.

## Controlled isolation and the Dance attempt

[Isolation 02](../.local/w101-audio-study/isolation-02/isolation.json) ran two owned
tone hosts simultaneously and swapped which process was recorded. Both hosts
reported `playback-complete` in both runs; capture completed and cleanup was
confirmed. Each measured payload contained 89,082 frames / 2.02 seconds:

| Selected / unrelated tone | Selected amplitude | Unrelated amplitude / allowed maximum | Result |
| --- | --- | --- | --- |
| 697 Hz / 1209 Hz | 0.00220178 | 0.000000147 / 0.000176143 | Selected present; unrelated below threshold. |
| 1209 Hz / 697 Hz | 0.01038494 | 0.000000178 / 0.000830795 | Selected present; unrelated below threshold. |

Both selected amplitudes exceeded the 0.0015 minimum. This is a positive
controlled isolation result, not a claim of mathematically zero leakage or
qualification for every application/device. No microphone, game input or volume
change was used by this check.

The [Dance audio receipt](../.local/w101-audio-study/dance-01/video/audio-timing.json)
is complete and finalized: **10,812 packets, 4,768,092 frames, 108.12 seconds**,
zero writer queue drops. The
[finished receipt](../.local/w101-audio-study/dance-01/finished.json) confirms video
verification, no recorder-sent inputs and released observation hooks. It does
not establish a completed minigame. The recording's
[sparse visual inspection](../.local/w101-audio-study/dance-01/review/offline-inspection-01/inspection.json)
sampled eight times through 105 seconds; it is not an every-frame review. The
supervisor continued observing W101 on loading for roughly four minutes.
**No paired Dance arrows/notes were qualified.** The cause of the persistent
loading is unknown; capture completion does not diagnose the game.

The supervisor then selected Escape → Idle through the visible game menu and
confirmed the **YOU ARE IDLE** banner over the loading scene. A subsequent
[passive cleanup recording](../.local/w101-audio-study/w101-idle-after-dance-01/finished.json)
finalized with verified video, no recorder-sent inputs and released hooks. Native
Idle was observed; this does not mean gameplay resumed underneath the banner.
The recorder can be released as an experimental opt-in feature while Dance cue
qualification remains blocked on reaching an actual game.

## Preserved attempts

| Attempt | Observed result | Interpretation |
| --- | --- | --- |
| [Quiet 01](../.local/w101-audio-study/quiet-capture-01/silent-host.json) | Test host timed out waiting for its start marker; no tone was submitted and cleanup succeeded. No successful capture receipt. | Setup history, not an audio test pass. |
| [Quiet 02](../.local/w101-audio-study/quiet-capture-02/stderr.txt) | Capture rejected the selected process identity. Host stopped before playback. | Admission rejection, not silent audio. |
| [Quiet 03](../.local/w101-audio-study/quiet-capture-03/capture/audio-timing.json) | Backend delivered two packets; writer retained one 441-frame packet and rejected the next as an overlapping device cursor. Audio incomplete. | An assumed device-position progression did not match the virtual source. |
| [Quiet 04](../.local/w101-audio-study/quiet-capture-04/capture/audio-timing.json) | Both raw device positions were zero while QPC advanced 100,000 units, exactly 10 ms. The same writer rejection remained explicit. | Diagnostic evidence; source positions must remain unqualified rather than synthesized. |
| [Quiet 05](../.local/w101-audio-study/quiet-capture-05/capture/audio-timing.json) | 153 packets, 67,473 frames, 1.53 s at 44,100 Hz stereo PCM16; finalized, zero queue drops, no cleanup error or pending native activation. | Native startup, packet persistence and bounded cleanup passed. Tone isolation did not. |
| [W101 Idle 01](../.local/w101-audio-study/w101-idle-01/inspection.json) | Passive five-second video request; complete audio with 572 packets, 252,252 frames and 5.72 s of PCM. Native Idle remained visible. | Working selected-game recording path in a static scene; no gameplay or cue mapping tested. |
| [Quiet target exit](../.local/w101-audio-study/quiet-target-exit-01/capture/audio-timing.json) | The owned silent host exited during capture. Backend reported `target-exited`; 48 packets/21,168 frames finalized, zero queue drops, cleanup confirmed. | Correctly retained the partial WAV as `incomplete`; no fallback or successful-full-recording claim. Both owned processes exited. |

Earlier failed receipts remain unchanged. The device-position repair preserves
raw zero positions, marks them unqualified and separately checks packet QPC.
It does not replace unknown source timing with an invented sample cursor.

## Quiet-host signal measurement

[Signal verification](../.local/w101-audio-study/quiet-capture-05/capture/signal-verification.json)
measured RMS **0.00001462** and peak **0.00003052**, one PCM16 least-significant
bit. The signal was below the analyzer's silence thresholds; it was not exactly
all-zero PCM. Neither deliberate test tone was present, and `IsolationPassed`
was explicitly **false**. The
[coordinator](../.local/w101-audio-study/quiet-capture-05/coordinator.json) records
`start_marker_created=false`; the host report records no playback submission.

The small [signal analyzer](../ProjectMaelstrom/ProjectMaelstrom/Utilities/ProcessAudioVerification.cs)
uses per-channel Hann-weighted tone measurements and explicit thresholds. Its
[synthetic tests](../ProjectMaelstrom/ProjectMaelstrom.Tests/ProcessAudioVerificationTests.cs)
exercise selected tone, mixed/leaked tone, opposite stereo phase, silence,
missing/malformed samples and QPC conversion. A mathematical test or quiet native
capture cannot prove another process's audible signal was excluded.

## W101 signal, timing and image limits

The [inspection receipt](../.local/w101-audio-study/w101-idle-01/inspection.json)
and [audio packet report](../.local/w101-audio-study/w101-idle-01/video/audio-timing.json)
establish:

- Audio RMS **0.003947** (−48.07 dBFS), peak **0.03146**, zero clipped samples.
  A signal was recorded with volume left at the user-reported setting; these
  measurements do not identify the sound's game meaning.
- All 572 source QPC packets were contiguous at 10 ms, with zero flagged packets
  and zero writer queue drops. Raw device positions remained unqualified.
- Audio began **230.13 ms before** the first video source timestamp and ended
  **3.856 ms before** the reported video stop. Preserve those offsets; do not
  force both files to start at zero and call them synchronized.
- Input/audio origin and end metadata, target PID/creation identity and stored
  video/WAV hashes matched. The input log contained only `focus_lost`, with no
  recorded game key or mouse action; the recorder sent no inputs.
- The [video report](../.local/w101-audio-study/w101-idle-01/video/timing.json)
  contains **one actual source frame**, two submissions including the synthetic
  terminal hold, and **331 encoded frames**. The static Idle image was held and
  rate-converted; this is not 60 fps capture or live-cue latency evidence.

The MP4 remains silent; audio is `game-audio.wav` with its separate packet map.
The recording preserves 1282×759 capture geometry and 1282×760 encoded geometry,
whose interior pixel transform remains unverified. Audio/video packet clocks
sharing a time base does not establish exact decoded-frame alignment. No audio
event was labeled as Watch, Go, an arrow or a result. The
[finished receipt](../.local/w101-audio-study/w101-idle-01/finished.json) reports
video verified, zero inputs sent and hooks released; its approximately 7.95 s
elapsed time includes setup/finalization and is not the PCM duration.

## Consolidated offline checks

The primary session's final checks passed **507 C# application tests, none
skipped**, **14 installer tests** and **25 focused Python audio-analyzer tests**.
Scoped C# whitespace/analyzer checks and Python lint also passed. The portable
candidate passed hidden workspace rendering: its audio checkbox is visible and
off by default, and its final payload excludes legacy `Scripts` and generated
user data. The embedded installer passed its isolated install/update/uninstall
check, then the actual update preserved metadata for all 319 existing user-data
files. The installed app passed a three-second passive audio/video recording,
with zero audio drops/errors and no game inputs. The final recorded image still
shows native Idle over loading. Artifact hashes and receipts are in the
[release record](GAME_AUDIO_RELEASE_20261109.md). The
[offline analyzer](PET_DANCE_AUDIO_ANALYSIS.md) also loaded the actual W101 WAV
and audio report as 44,100 Hz, 572 packets and one continuous segment. No Dance
classification followed from that load: this recording contains no labeled
Dance cues. These checks complement the controlled source-isolation receipts;
they do not establish Dance cue accuracy or exact decoded-frame alignment.

## Remaining qualification

Retain the passed two-process swapped-tone check when changing the backend.
Silent target exit and ordinary stop also have native receipts; retain their
checks when testing cancellation alongside active video. Broader device/OS
coverage remains work. Do not change volume or route audio to a microphone as a
fallback.

Then collect an explicitly opted-in Dance demonstration with labeled visual
cues, preview sounds and button-triggered sounds kept distinct. Measure offset,
drift, missed cues and false positives on fresh recordings before audio influences
a player. Neither this passive study nor the asset-name hypotheses closes those
gates. Raw `.local` evidence is workspace-only and may be absent from a clean
checkout; preserve receipts and hashes when exporting the study.
