using System.Text.Json;
using System.Threading.Channels;
using NAudio.Wave;

namespace ProjectMaelstrom.Utilities;

internal sealed record GameAudioPacketStamp(long frame_offset, int frame_count, ulong qpc_position_100ns,
    double qpc_ms, ulong device_position, uint flags, bool silent, bool discontinuity, bool timestamp_error);

/// <summary>PCM frames retain their source QPC mapping; no samples are invented to cover gaps.</summary>
internal sealed class GameAudioPacketTimeline
{
    internal const int MaximumPackets = 22000;
    internal const long MaximumBytes = 64 * 1024 * 1024;
    private readonly int _blockAlign;
    private readonly List<GameAudioPacketStamp> _packets = [];
    public IReadOnlyList<GameAudioPacketStamp> Packets => _packets;
    public long Frames { get; private set; }
    public long Bytes => Frames * _blockAlign;
    public bool DevicePositionQualified { get; private set; } = true;

    public GameAudioPacketTimeline(int blockAlign)
    {
        if (blockAlign is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(blockAlign));
        _blockAlign = blockAlign;
    }

    public GameAudioPacketStamp Append(ProcessAudioPacket packet)
    {
        if (packet.FrameCount <= 0 || packet.DevicePosition > ulong.MaxValue - (ulong)packet.FrameCount ||
            packet.PcmBytes == null || packet.PcmBytes.Length > 1048576 ||
            packet.PcmBytes.Length != (long)packet.FrameCount * _blockAlign || packet.Flags > 7 ||
            _packets.Count >= MaximumPackets || Bytes + packet.PcmBytes.Length > MaximumBytes)
            throw new InvalidDataException("Invalid or excessive game audio packet.");
        var timestampError = (packet.Flags & 4) != 0;
        if (!timestampError && packet.QpcPosition100Ns == 0) throw new InvalidDataException("Missing audio source clock.");
        if (_packets.Count > 0)
        {
            var last = _packets[^1];
            if (!timestampError && !last.timestamp_error &&
                (packet.QpcPosition100Ns <= last.qpc_position_100ns ||
                 packet.QpcPosition100Ns / 10000.0 < last.qpc_ms + last.frame_count * 1000.0 / 44100 - 2))
                throw new InvalidDataException($"Audio packet clock moved backwards or overlapped: previous device={last.device_position}, frames={last.frame_count}, qpc={last.qpc_position_100ns}; current device={packet.DevicePosition}, frames={packet.FrameCount}, qpc={packet.QpcPosition100Ns}, flags={packet.Flags}.");
            // Windows' virtual process-loopback source was observed returning zero
            // for every device position while QPC advances exactly one packet.
            // Retain that evidence; never invent a device cursor or use it to align.
            if (packet.DevicePosition != last.device_position + (ulong)last.frame_count)
                DevicePositionQualified = false;
        }
        var item = new GameAudioPacketStamp(Frames, packet.FrameCount, packet.QpcPosition100Ns,
            packet.QpcPosition100Ns / 10000.0, packet.DevicePosition, packet.Flags,
            (packet.Flags & 2) != 0, (packet.Flags & 1) != 0, timestampError);
        _packets.Add(item); Frames += packet.FrameCount;
        return item;
    }
}

/// <summary>Opt-in selected-process audio sidecar. One bounded queue and writer;
/// never opens a microphone or falls back to the desktop's mixed audio.</summary>
internal sealed class GameAudioRecording : IAsyncDisposable
{
    private readonly string _directory;
    private readonly int _processId;
    private readonly long _startedTicks;
    private readonly Action<ProcessAudioPacket>? _packetObserver;
    private readonly CancellationTokenSource _stop;
    private readonly Channel<ProcessAudioPacket> _queue = Channel.CreateBounded<ProcessAudioPacket>(new BoundedChannelOptions(64)
    { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = true });
    private readonly object _gate = new();
    private ProcessAudioCapture? _capture;
    private Task? _writer;
    private Task? _finished;
    private GameAudioPacketTimeline? _timeline;
    private string? _writeFailure;
    private long _dropped;
    private bool _finalized;
    private double? _videoOrigin, _videoEnd, _callback;
    public bool IsCapturing => _capture != null && !_capture.Completion.IsCompleted &&
        Volatile.Read(ref _writeFailure) == null && Interlocked.Read(ref _dropped) == 0;

    private GameAudioRecording(int processId, long startedTicks, string directory, CancellationToken token,
        Action<ProcessAudioPacket>? packetObserver)
    {
        _processId = processId; _startedTicks = startedTicks; _directory = directory;
        _packetObserver = packetObserver;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
    }

    public static async Task<GameAudioRecording> StartAsync(int processId, long startedTicks, string directory,
        Func<bool> targetCurrent, CancellationToken token, Action<ProcessAudioPacket>? packetObserver = null)
    {
        if (processId <= 0 || startedTicks <= 0 || !Directory.Exists(directory) || !targetCurrent())
            throw new InvalidOperationException("Bind game audio to a current selected process and recording directory.");
        if (File.Exists(Path.Combine(directory, "game-audio.wav")) || File.Exists(Path.Combine(directory, "audio-timing.json")))
            throw new IOException("Audio evidence already exists; choose a new recording.");
        var recording = new GameAudioRecording(processId, startedTicks, directory, token, packetObserver);
        try
        {
            recording._capture = await ProcessAudioCapture.StartAsync(processId, recording.Accept, targetCurrent, recording._stop.Token).ConfigureAwait(false);
            var format = recording._capture.Format;
            if (format.BitsPerSample != 16 || format.Encoding != "pcm-s16le" || format.BlockAlign != format.Channels * 2)
                throw new InvalidDataException("The game audio writer requires declared PCM16 samples.");
            recording._timeline = new(format.BlockAlign);
            var stream = new FileStream(Path.Combine(directory, "game-audio.wav"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            WaveFileWriter writer;
            try { writer = new WaveFileWriter(stream, new WaveFormat(format.SampleRate, format.BitsPerSample, format.Channels)); }
            catch { stream.Dispose(); throw; }
            recording._writer = Task.Run(() => recording.WriteAsync(writer));
            return recording;
        }
        catch (Exception error)
        {
            recording.RequestStop();
            string? cleanupError = null;
            try { if (recording._capture != null) await recording._capture.DisposeAsync().ConfigureAwait(false); }
            catch (Exception cleanup) { cleanupError = cleanup.Message; }
            finally { recording._queue.Writer.TryComplete(); recording._stop.Dispose(); }
            GameExperiments.WriteAtomic(Path.Combine(directory, "audio-timing.json"), new
            {
                schema_version = 1,
                kind = "selected-process-audio",
                status = "unavailable",
                finalized = false,
                target_process_id = processId,
                target_process_start_utc_ticks = startedTicks,
                scope = "selected-process-tree",
                error = error.Message,
                cleanup_error = cleanupError,
                pending_native_activations = ProcessAudioCapture.PendingNativeActivations,
                microphone = false,
                system_mix_fallback = false
            });
            throw;
        }
    }

    private void Accept(ProcessAudioPacket packet)
    {
        if (_queue.Writer.TryWrite(packet)) return;
        Interlocked.Increment(ref _dropped);
        RequestStop();
    }

    private async Task WriteAsync(WaveFileWriter writer)
    {
        try
        {
            using (writer)
            {
                await foreach (var packet in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
                {
                    _timeline!.Append(packet);
                    writer.Write(packet.PcmBytes, 0, packet.PcmBytes.Length);
                    // Optional bounded analysis runs on this consumer, never on the
                    // native audio callback. Failure/queue exhaustion stops capture.
                    _packetObserver?.Invoke(packet);
                }
            }
            _finalized = true;
        }
        catch (Exception error) { _writeFailure = error.Message; RequestStop(); }
    }

    public void SetVideoOrigin(double captureMs, double callbackMs)
    {
        if (!double.IsFinite(captureMs) || !double.IsFinite(callbackMs) || captureMs < 0 || callbackMs < captureMs)
            throw new InvalidDataException("Invalid shared recording clock.");
        lock (_gate)
        {
            if (_videoOrigin.HasValue) throw new InvalidOperationException("Video origin is already bound.");
            _videoOrigin = captureMs; _callback = callbackMs;
        }
    }

    public void SetVideoEnd(double endMs)
    {
        if (!double.IsFinite(endMs) || endMs < 0) throw new InvalidDataException("Invalid video end clock.");
        lock (_gate)
        {
            if (_videoOrigin.HasValue && endMs < _videoOrigin.Value) throw new InvalidDataException("Video ends before its origin.");
            _videoEnd = endMs;
        }
        RequestStop();
    }

    public void RequestStop() { try { _stop.Cancel(); } catch (ObjectDisposedException) { } }

    public Task StopAsync(bool videoVerified = false)
    {
        lock (_gate) return _finished ??= FinishAsync(videoVerified);
    }

    private async Task FinishAsync(bool videoVerified)
    {
        RequestStop();
        var capture = _capture!;
        ProcessAudioCaptureResult? result = null;
        string? cleanupFailure = null;
        try { result = await capture.StopAsync().ConfigureAwait(false); }
        catch (Exception error) { cleanupFailure = error.Message; }
        finally { _queue.Writer.TryComplete(); }
        if (_writer != null) await _writer.ConfigureAwait(false);
        try { await capture.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { cleanupFailure ??= error.Message; }
        _stop.Dispose();
        var wav = Path.Combine(_directory, "game-audio.wav");
        var video = Path.Combine(_directory, "capture.mp4");
        var complete = _finalized && _timeline!.Frames > 0 && _dropped == 0 && _writeFailure == null && cleanupFailure == null && result?.Status == "stopped";
        GameExperiments.WriteAtomic(Path.Combine(_directory, "audio-timing.json"), new
        {
            schema_version = 1,
            kind = "selected-process-audio",
            status = complete ? "complete" : "incomplete",
            finalized = _finalized,
            wav_file = "game-audio.wav",
            wav_sha256 = _finalized ? GameExperiments.Digest(wav) : null,
            format = new { sample_rate = capture.Format.SampleRate, channels = capture.Format.Channels, bits_per_sample = capture.Format.BitsPerSample, encoding = "pcm16" },
            target_process_id = _processId,
            target_process_start_utc_ticks = _startedTicks,
            scope = "selected-process-tree",
            microphone = false,
            system_mix_fallback = false,
            video_capture_origin_ms = _videoOrigin,
            video_capture_end_ms = _videoEnd,
            first_frame_callback_ms = _callback,
            video_verified = videoVerified,
            video_sha256 = videoVerified && File.Exists(video) ? GameExperiments.Digest(video) : null,
            exact_decoded_video_alignment_verified = false,
            clock = "Source audio QPC in 100ns units; qpc_ms = qpc_position_100ns / 10000. Video and inputs use QPC milliseconds.",
            timeline_note = "WAV concatenates received packets. Use packet timestamps for alignment; gaps/discontinuities are not filled or hidden.",
            queue_capacity = 64,
            queue_dropped = _dropped,
            maximum_packets = GameAudioPacketTimeline.MaximumPackets,
            frames = _timeline!.Frames,
            packets = _timeline.Packets,
            capture_result = result,
            device_position_qualified = _timeline.DevicePositionQualified && _timeline.Packets.Count > 1,
            device_position_note = "Raw device positions are retained. Alignment uses source QPC; the virtual source may report repeated zero device positions.",
            write_error = _writeFailure,
            cleanup_error = cleanupFailure,
            pending_native_activations = ProcessAudioCapture.PendingNativeActivations,
            capture_cleanup_confirmed = cleanupFailure == null && result != null && result.Status != "failed" && ProcessAudioCapture.PendingNativeActivations == 0,
            automatic_training_eligible = false
        });
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
