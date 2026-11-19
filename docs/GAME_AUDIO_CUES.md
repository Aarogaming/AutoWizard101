# Game audio cues: capture implementation and learning gates

Updated **2026-11-09**. Selected-process audio capture and opt-in recording are
implemented in the development source. Quiet-process and passive W101 recordings
completed with packet timing and cleanup receipts. A subsequent two-process
tone test passed with each process selected in turn: the selected tone was
present and the unrelated tone remained below its measured threshold. This
qualifies that controlled isolation check on this machine, not Dance sound
meanings or exact audio/video synchronization. The recording update is packaged
and installed; its installed passive W101 capture passed with zero audio queue
drops. See the [release receipt](GAME_AUDIO_RELEASE_20261109.md). The
[validation record](GAME_AUDIO_VALIDATION_20261109.md) preserves results and limits.

The initial audit was read-only and used no audio device. The earlier quiet
validation played no test tones; the later authorized isolation check played
bounded tones from two owned hosts. Neither changed system volume or used a
microphone. The user reported 2% volume during the quiet checks and 20% during
the later check; those are reported settings, not a volume measurement or change
made by Maelstrom. No production dependency or driver was added.

The subsequent `dance-01` recording saved **108.12 seconds of audio with zero
writer queue drops**, but W101 remained on its loading screen during the attempt
and the supervising session's roughly four-minute observation. No arrow/note
pairs were qualified. The supervisor used Escape → Idle and visually confirmed
the native Idle banner over the loading scene; the follow-up passive capture
finalized. That confirms the observed menu state, not resumed gameplay or a
diagnosis of why loading persisted. See the
[current validation evidence](GAME_AUDIO_VALIDATION_20261109.md#controlled-isolation-and-the-dance-attempt).

The user reports music near the Dance sigil, sounds on screen changes and
potentially consistent notes for directions. These are useful hypotheses,
not measured classifier labels. During `lifecycle-01` the user supplied arrow
presses intending a button-to-tone comparison, while the supervisor handled the
menus. That run still had audio disabled. Preserve the input/video evidence, but
do not infer missing audio or claim a note mapping from it.

## What is present

| Component | Finding |
| --- | --- |
| [Selected-process backend](PROCESS_AUDIO_CAPTURE.md) | Windows process-loopback capture, selected PID and children, fixed PCM16 stereo 44100 Hz, bounded worker and explicit cancellation/cleanup. No microphone or system-mix fallback. |
| [Recording sidecar](../ProjectMaelstrom/ProjectMaelstrom/Utilities/GameAudioRecording.cs) | Saves `game-audio.wav` and `audio-timing.json` with target identity, raw packet QPC, flags, bounded queue, source hashes and independent completion status. |
| [Observe workspace](../ProjectMaelstrom/ProjectMaelstrom/GameWorkspaceForm.cs) | **Record game audio** is an explicit video option, off by default. The supervised lifecycle CLI also has a separate audio-enabled entry point. These are source integrations, not an installed-build claim. |
| [Controlled signal check](../ProjectMaelstrom/ProjectMaelstrom/Utilities/ProcessAudioVerification.cs) | Small per-channel tone/silence measurements with synthetic tests. It is not a Dance-note classifier; silence cannot pass a positive tone-isolation check. |
| [Legacy AudioRecognizer](../ProjectMaelstrom/ProjectMaelstrom/Utilities/AudioRecognizer.cs) | A coarse amplitude detector, not a Pet Dance sound classifier or game-specific recorder. Its initial code used system loopback, fell back to microphone, decoded every packet as 16-bit PCM, and used UTC for a two-second cooldown. The bounded repair described below addresses those defects but does not add process isolation. |
| [Main startup](../ProjectMaelstrom/ProjectMaelstrom/Main.cs) | The legacy recognizer is behind `ENABLE_AUDIO_RECOGNIZER`, with false as its code default. Effective installed settings were not inspected or changed. |
| [GameAudioCue](../ProjectMaelstrom/ProjectMaelstrom/Models/GameAudioCue.cs) | A generic cue/UTC/metadata record. Direction names in its comment are examples, not evidence that direction recognition exists. |
| [GPU video capture](../ProjectMaelstrom/ProjectMaelstrom/Utilities/GpuSurfaceVideoProbe.cs) | The MP4 itself still has no embedded audio. Requested audio is a separate timed WAV sidecar. The alternate [raw video recorder](../ProjectMaelstrom/ProjectMaelstrom/Utilities/WindowVideoProbe.cs) excludes audio. |
| Existing human route/Dance recording | `human-route-01/video/capture.mp4` has one H.264 video stream and no audio stream, confirmed by the replay worker's bounded metadata inspection. Its report also says no audio was recorded. Audio cannot be recovered from those frames. |

The legacy detector's peak amplitude is not calibrated confidence in a game
event. Its generic two-second cooldown is unsuitable as a presumed Dance-arrow
timing rule. Do not enable it as a shortcut to the feature below.

## Game-only capture path

Microsoft's application-loopback example can include a selected process and
its children while excluding other applications' rendered audio. It documents
Windows build 20348 or later and silence when the selected process tree renders
no audio. This differs from endpoint/system loopback.
[Microsoft application-loopback sample, dated 2026-02-19](https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/)

The implemented **Record game audio** option is off by default. It binds to the
selected game process/session, not a launcher, default sound device or arbitrary
executable name. The capture scope includes sounds rendered
by the selected process tree, including its music; it is not automatically
limited to sound effects.

The backend checks capability and target continuity. Unsupported OS, activation
failure and invalid target identity are unavailable; a target lost during capture
leaves an incomplete recording rather than a successful silent result.
**No microphone or system-mix fallback.** Selecting a different game or process
requires renewed binding. Audio observation provides no input permission.

The backend uses explicit Windows COM interop and the existing NAudio callback
interfaces; NAudio writes the PCM WAV. No new package was added. See the
[backend guide](PROCESS_AUDIO_CAPTURE.md) for API/source provenance, supported
format, activation lifetime and the documented Windows-build qualification.

## One monotonic recording timeline

Audio needs packet sample position, frame count, negotiated format and a monotonic
time anchor. Microsoft's capture API exposes a performance-counter timestamp for
the packet's first audio frame, already converted to 100-nanosecond units, along
with silence/discontinuity/timestamp-error flags.
[Audio packet/timestamp contract, updated 2025-09-05](https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nf-audioclient-iaudiocaptureclient-getbuffer)

Windows Graphics Capture's `SystemRelativeTime` describes the compositor's
QPC time for the frame. It is the appropriate video-side clock reference to
investigate for alignment.
[Video frame clock](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.direct3d11captureframe.systemrelativetime?view=winrt-26100)

Implemented recording behavior and remaining measurement requirements:

- Preserve original audio/video clock values, units and a common origin; convert
  explicitly rather than assuming raw QPC ticks equal 100-nanosecond timestamps.
- Timestamp capture packets/frames at their source, not file-write completion,
  callback arrival, UTC wall time or encoded frame number.
- Preserve sample counts, silence, gaps, discontinuities and timing errors.
  A failed capture is not evidence of quiet gameplay.
- Keep UTC only for human-readable session dates. Measure synchronization offset,
  drift and uncertainty on held-out recordings before using audio for reactions.
- Retain source-frame timing. This recording's encoded video contains extra
  rate-converted frames, which are not additional observations.

Audio is saved as a bounded sidecar with timing metadata; muxing for playback
remains a separate export step. The saved report includes target identity,
format, output frame offset, packet length, raw source QPC, raw device position,
flags and the WAV hash. The WAV concatenates received packets; consult source
timestamps before aligning it with video. No samples are invented to fill gaps.

On this machine the virtual source repeatedly reported device position zero
while QPC advanced normally. Those raw values remain in the report with
`device_position_qualified=false`; they are not replaced by invented source
positions. W101's passive check had contiguous packet QPC and matching stored
input/video origins, but exact decoded-video alignment remains unverified.

A future learned cue still needs start/end time, feature version, template ID,
score and timing uncertainty. Those cue fields remain design work.

## Lightweight cue learning

The [offline Dance audio analyzer](PET_DANCE_AUDIO_ANALYSIS.md) now reads finalized
WAV/timing/replay/input evidence, extracts bounded spectral-change features and
keeps calibration rounds separate from held-out rounds. It runs with one
numerical worker and no model, playback or game input. Twenty-five synthetic
tests pass. Loading the actual passive W101 WAV and packet report succeeded at
44,100 Hz with 572 packets in one continuous segment; this validates that input
contract, not a game-note classification.

Live cue inference is still future work: use a small rolling buffer and one
bounded analysis worker, off the capture callback. Report dropped analysis work
rather than accumulate an unbounded queue. Preserve channels until evidence
supports downmixing; the offline analyzer currently combines spectral power
without cancelling opposite-phase channels.

Use opt-in synchronized examples to compare cue timing with visual arrows,
Watch/Go phases and outcomes. Do not assume different sounds encode different
directions: audio may identify only an onset, phase or feedback, and music may
mask it. Begin with unnamed sound events; assign semantics only from labeled,
independent examples.

For the first comparison, annotate preview notes separately from notes triggered
by the user's button presses. Test whether pitch/timbre identifies direction,
whether repeated arrows produce separate onsets, whether Go/result cues are
distinct, and whether sigil music is shared with nearby locations. Start with
an audio event detector whose interpretation is constrained by the observed
routine state. It can improve timing without giving audio sole authority over
menu choices, rewards, feeding or window identity.

Evaluate false detections, missed cues, duplicates, latency and cross-session
stability across music/volume settings. Keep explicit unknown/no-evidence states.
Audio can support visual timing when qualified; conflicting or absent audio
must not become a guessed arrow. No language model or GPU inference is needed
for this initial signal-processing path.

Cue-learning budgets still need bounded snippet length, analysis queue and
measured latency. Capture already has a 64-packet writer queue, 64 MiB storage
ceiling, 22,000-packet limit and a 210-second absolute ceiling. This gives a
maximum 180-second video recording bounded setup headroom; video stop requests
audio stop. These limits are not a measured real-time cue-recognition guarantee.

## Quiet installed-asset research

A [bounded local inspection](../.local/w101-audio-asset-study-20261109/inspection.json)
read 12,115,793 installation bytes, within its 20 MiB limit, and wrote only a
workspace report. It did not read audio payloads, play sounds or change installed
files. Both saved archive-index hashes matched and source metadata remained
stable. `PetGames.xml` decoded to 16,701 bytes of `BINd` serialization, not plain
XML; only printable reference fragments were inspected.

Five observed music candidates in `ThePhantomZoneWorld-WorldData.wad` are
`Sound/PetGames/dance_wiz.mp3`, `dance_krok.mp3`, `dance_marley.mp3`,
`dance_mooshu.mp3` and `dance_dragonspyre.mp3`. `PetGames.xml` contains printable
`PetGames_DanceTrack0` through `PetGames_DanceTrack4` references and corresponding
world-named dance-floor references. Names are hypotheses for calibration, not a
verified track assignment or arrow-to-note mapping. No game asset is imported
into an executable routine or distributed with a pack by this inspection.

## Bounded legacy repair and remaining gates

The accompanying repair removes the microphone fallback, decodes supported
PCM/float packets using their declared format, rejects malformed/unsupported
data, and uses a monotonic cooldown. Tests use synthetic samples/fake capture,
not audio devices. It remains the old optional **system-loopback amplitude
detector**, separate from the implemented selected-process recorder.

The controlled simultaneous-source check, including swapped positive controls,
passed in [isolation-02](../.local/w101-audio-study/isolation-02/isolation.json).
Remaining gates: qualify target exit and interruption across machines; measure
actual audio/video cue alignment;
collect labeled Dance audio across independent games; then validate cue semantics
and expose reviewed audio evidence to packs. Passive Idle music and older
audio-free demonstrations do not substitute for that calibration data.

---

## Native Zero-Memory, Zero-OCR Audio-Cue Event Classifier (`audio_cue.rs`)

Simple amplitude detectors (such as the legacy `AudioRecognizer.cs`) trigger false positives on ambient music, background waterfalls, or character footsteps. To provide robust, tamper-proof event detection that is 100% immune to camera angles, lighting conditions, UI scaling, and memory offset shifts, Maelstrom embeds a native, pure-Rust spectral classifier in `crates/maelstrom-core/src/audio_cue.rs` (claim `W101-AUDIO-CORE-02`).

### 1. Mathematical Architecture: Single-Bin Goertzel & Band Power Ratios
The classifier operates directly over standard PCM audio sample buffers (44.1 kHz / 48.0 kHz, f32/i16):
1. **Total Signal RMS Power**:
   $$\text{RMS} = \sqrt{\frac{1}{N} \sum_{i=0}^{N-1} x_i^2}$$
   Establishes the real-time noise floor and rejects silence/ambient quiet.
2. **Goertzel Frequency Magnitude**:
   Computes exact spectral energy at target characteristic frequencies with $O(N)$ computational complexity and zero trigonometric runtime overhead:
   $$s_k[n] = x[n] + 2 \cos(\omega_k) s_k[n-1] - s_k[n-2]$$
   $$|X(k)|^2 = s_k^2[N-1] + s_k^2[N-2] - 2 \cos(\omega_k) s_k[N-1] s_k[N-2]$$
3. **Signal-to-Noise Ratio (SNR) in Target Band**:
   $$\text{Ratio} = \frac{E_{\text{target}}}{E_{\text{out-of-band}} + \epsilon}$$
   A cue is verified only when in-band spectral energy substantially exceeds the broad spectrum, eliminating false triggers from volume spikes or broad white noise.

### 2. Pre-Calibrated Acoustic Signature Profiles
- **`FishingBobberSplash`**:
  - Center frequency: $2.4\text{ kHz}$ (bandwidth $1.2\text{ kHz} - 3.5\text{ kHz}$).
  - Temporal envelope: steep acoustic attack ($< 50\text{ ms}$) followed by fast decay ($< 300\text{ ms}$), distinguishing water splash dynamics from ambient lake hum.
- **`CombatInitiationFanfare`**:
  - Center frequency: $550\text{ Hz}$ with brass chord harmonics ($300\text{ Hz} - 850\text{ Hz}$).
  - Triggers instant `in_combat = true` on the first frame of battle circle transition.
- **`TurnTimerWarningChime`**:
  - Center frequency: $3.2\text{ kHz}$ ($2.0\text{ kHz} - 4.5\text{ kHz}$ bell resonance).
  - Fires during the final 5 seconds of the 30-second turn-planning window, triggering fast-turn fallback spell selection.
- **`DialogueAdvanceChime`**:
  - Center frequency: $1.9\text{ kHz}$ ($1.5\text{ kHz} - 2.5\text{ kHz}$).
  - Detects NPC dialogue prompt completion to advance conversation pages.

### 3. Blackboard Routing & Boundary Invariant
Audio detection conforms strictly to `Facts ≠ Authority ≠ Actions`:
- Detected cues emit immutable `AudioCueEvent` records.
- Events update the Behavior Tree `W101Blackboard` facts (`in_combat`, `fishing_splash`, `turn_timer_warning`).
- The audio engine possesses zero input authority; physical action dispatch remains exclusively owned by the serialized scheduler and input lease.


