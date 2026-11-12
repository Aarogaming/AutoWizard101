# Selected-game audio capture

Implementation: [ProcessAudioCapture.cs](../ProjectMaelstrom/ProjectMaelstrom/Utilities/ProcessAudioCapture.cs). Device-free tests: [ProcessAudioCaptureTests.cs](../ProjectMaelstrom/ProjectMaelstrom.Tests/ProcessAudioCaptureTests.cs). Written 2026-11-09; the root integration run owns build and live validation results.

## Scope and consent

This backend is an explicitly started, bounded capture of a selected process and its child processes. It does not activate itself, choose a game, record a microphone, alter volume, or fall back to the system mix. The recording integration must require audio opt-in and verify that its selected window still belongs to the intended process. A failed or unsupported activation is an explicit failure; video recording can decide separately whether to continue without audio.

Microsoft's application-loopback sample describes process-tree capture independent of a particular audio endpoint, excluding other applications. A target that is not rendering audio can yield silence. Its documented minimum is Windows build 20348. The function reference currently says build 20438 for the activation-parameters extension; the implementation follows the sample's 20348 gate and still treats any activation failure as unavailable. This difference is a documentation qualification, not evidence that every supported machine will activate successfully. [Microsoft sample overview](https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/), [activation API](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nf-mmdeviceapi-activateaudiointerfaceasync).

The previous human-route recording contains no audio stream, as established by the separate metadata inspection. It cannot provide Dance sound templates retrospectively. Successful silent startup is also insufficient evidence of game-only isolation or useful cues; that requires a controlled later capture with identifiable selected and excluded sound sources.

## Interface

```csharp
Task<ProcessAudioCapture> StartAsync(
    int pid, Action<ProcessAudioPacket> onPacket,
    Func<bool> targetCurrent, CancellationToken token);

ProcessAudioFormat Format { get; }
Task<ProcessAudioCaptureResult> Completion { get; }
void RequestStop();
Task<ProcessAudioCaptureResult> StopAsync();
ValueTask DisposeAsync();
```

`ProcessAudioFormat` contains `SampleRate`, `Channels`, `BitsPerSample`, `BlockAlign`, and `Encoding`. The initial implementation explicitly requests **44100 Hz, stereo, signed 16-bit little-endian PCM**, with block alignment 4 and encoding `pcm-s16le`. It accepts that exact format rather than guessing an endpoint format. This choice follows the official sample, with conversion requested from Windows. [Microsoft sample implementation](https://raw.githubusercontent.com/microsoft/Windows-classic-samples/main/Samples/ApplicationLoopback/cpp/LoopbackCapture.cpp).

`ProcessAudioPacket` contains an independently owned `PcmBytes` array, `FrameCount`, `DevicePosition`, `QpcPosition100Ns`, and raw native `Flags`. The array is safe to queue after the callback returns. Native memory has already been released before the consumer runs. The callback and target predicate must be bounded and nonblocking; they must not do disk writes, model work, or wait on their own capture's completion. A callback can request stop.

`ProcessAudioCaptureResult` contains `Status`, optional `Error`, `Packets`, and `Frames`. Statuses are `stopped`, `target-exited`, `duration-limit`, or `failed`. Counts describe callbacks that returned successfully, not necessarily persisted audio; the writer must report persistence independently. Errors are not converted into successful silence.

`Dispose()` requests stop without waiting. Await `StopAsync()` or `DisposeAsync()` to confirm worker cleanup. Format and completion records remain readable afterward. A shutdown timeout means cleanup is still pending, not that it is safe to release resources from another thread.

## Timing and data quality

The API describes device position and QPC as identifying the first frame in each packet. QPC is already expressed in **100-nanosecond units**, not raw `Stopwatch` ticks. Native flags include data discontinuity (1), silence (2), and timestamp error (4). Flags are preserved; silent packets become zero-filled PCM. Timestamp-error packets must not be treated as trustworthy alignment anchors. Discontinuities must remain visible in the sidecar, rather than being erased by concatenating samples. Buffer acquisition and release occur on the same capture thread. [IAudioCaptureClient::GetBuffer](https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nf-audioclient-iaudiocaptureclient-getbuffer).

The root's quiet native check on 2026-11-09 observed consecutive 441-frame packets with device position **0** on both, flags 0, and QPC advancing exactly 100000 units (10 ms). This is recorded as an unqualified device-position source, not silently replaced with a synthesized counter. The native backend preserves these raw values; the recording layer qualifies clock sources separately and can use independently checked QPC for alignment. The official sample requests both positions but does not validate or use them when writing its WAV. This observation establishes an implementation-specific limitation on this machine; it is not a general guarantee about all process-loopback clients.

The recording layer must store a shared monotonic video/audio origin and the original packet positions, then derive relative times with checked arithmetic. It must distinguish submitted video frames, encoded video timestamps, and captured audio timing. This backend does not resample gaps, shift timestamps to make streams appear aligned, or infer arrow direction from a sound.

## Resource and lifetime limits

- One native capture or pending native activation at a time, preventing retry storms.
- Ten-second startup deadline, three-second cooperative shutdown wait, and a **210-second absolute audio capture ceiling**. This permits the recording layer's maximum 180-second video interval plus up to 30 seconds of bounded setup before the first video frame. Video stop still requests audio stop immediately; the extra allowance is not permission to extend gameplay recording.
- Event-driven packet readiness with a 20 ms cancellation/target-check polling bound; target checks approximately every 250 ms.
- At most 64 packets drained per pass and 1 MiB per copied packet. The consumer must add its own bounded queue and total storage limit.
- A dedicated background MTA thread owns native initialization, capture, stop, buffer release and ordinary disposal.

The native activation call is asynchronous. The callback object, activation parameters and native operation stay retained until invocation has returned, the callback has finished, and the caller has accepted or abandoned its result. Startup cancellation therefore does not free memory still owned by Windows. A late successful activation is released rather than starting a recording. `PendingNativeActivations` exposes the retained activation count for diagnostic receipts. The global capture slot remains reserved until native ownership actually ends.

Managed timeouts cannot forcibly interrupt a stalled native call or arbitrary consumer callback. The design bounds the caller's wait and prevents unsafe disposal; it does not claim guaranteed native termination. An activation callback that never arrives remains pending with retained resources, blocking another capture in that process. The application should report this state and avoid automatic retry. Callback agility and asynchronous lifetime requirements are documented by Microsoft. [Activation API](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nf-mmdeviceapi-activateaudiointerfaceasync), [sample callback class](https://raw.githubusercontent.com/microsoft/Windows-classic-samples/main/Samples/ApplicationLoopback/cpp/LoopbackCapture.h).

## Implementation provenance and validation boundary

The activation payload uses the Windows SDK's process-loopback type and include-target-process-tree mode. It is a 12-byte payload inside a native `VT_BLOB` variant. There is no exclude-tree mode or alternative device path. Existing NAudio public activation callback interfaces are reused; explicit private audio-client interfaces provide the required native method order. No production package was added. [Windows SDK activation definitions](https://raw.githubusercontent.com/microsoft/win32metadata/main/generation/WinSDK/RecompiledIdlHeaders/um/audioclientactivationparams.h), [NAudio activation interface](https://raw.githubusercontent.com/naudio/NAudio/v2.2.1/NAudio.Wasapi/CoreAudioApi/Interfaces/IActivateAudioInterfaceCompletionHandler.cs).

Tests use an in-memory native-buffer substitute and the real worker lifecycle, without opening any audio device. They cover activation bytes, delayed callback ownership orderings, owned PCM copies, silence, malformed/oversized packets, release before consumer, MTA cleanup, cancellation, target loss, consumer failure, duration limits, format refusal, and late startup cleanup. These tests do not establish actual Windows COM activation success, sound-source isolation, or measured audio/video synchronization.

Before promoting audio as verified: compile and run the device-free tests; perform a short selected-process startup/stop check; then, when audible testing is appropriate, capture distinguishable target and excluded process tones and inspect the saved evidence. Finally record an authorized Dance demonstration with audio and establish cue meanings from observations. Background quiet work must not start tone playback or change the user's volume.
