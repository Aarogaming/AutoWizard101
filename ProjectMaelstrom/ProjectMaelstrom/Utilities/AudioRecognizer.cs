using System;
using System.Buffers.Binary;
using System.Diagnostics;
using NAudio.Wave;
using ProjectMaelstrom.Models;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Lightweight audio recognizer that listens to system audio (loopback) and emits coarse cues for SmartPlay.
/// This uses simple amplitude-based detection; it can be expanded to frequency-based templates as we gather data.
/// </summary>
internal sealed class AudioRecognizer : IDisposable
{
    public event Action<GameAudioCue>? CueDetected;

    private readonly Func<IWaveIn> _createLoopback;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private IWaveIn? _loopback;
    private WaveFormat? _captureFormat;
    private readonly object _lock = new();
    private volatile bool _started;
    private volatile bool _disposing;
    private double? _lastCueMs;

    // Tunables
    // Baseline/filters to ignore background music hum.
    private const double AbsoluteThreshold = 0.12; // hard floor to ignore very low ambience
    private readonly double _transientDelta;    // how much the window peak must exceed the running baseline
    private const double BaselineDecay = 0.90;     // exponential decay for baseline (closer to 1 = slower)
    private static readonly TimeSpan CueCooldown = TimeSpan.FromSeconds(2);
    private double _baseline = 0;

    internal bool IsRunning => _started;

    public AudioRecognizer(double? transientDelta = null) : this(() => new WasapiLoopbackCapture(), transientDelta) { }

    internal AudioRecognizer(Func<IWaveIn> createLoopback, double? transientDelta = null)
    {
        _createLoopback = createLoopback ?? throw new ArgumentNullException(nameof(createLoopback));
        _transientDelta = transientDelta.HasValue
            && double.IsFinite(transientDelta.Value) ? Math.Clamp(transientDelta.Value, 0.05, 0.5)
            : 0.12;
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_started || _disposing) return;

            try
            {
                _loopback?.Dispose();
                _loopback = _createLoopback();
                _captureFormat = _loopback.WaveFormat;
                _loopback.DataAvailable += OnDataAvailable;
                _loopback.RecordingStopped += OnStopped;
                _baseline = 0;
                _lastCueMs = null;
                _started = true;
                _loopback.StartRecording();
                Logger.Log("[AudioRecognizer] Started WASAPI loopback capture.");
            }
            catch (Exception ex)
            {
                _started = false;
                try { _loopback?.Dispose(); } catch { }
                _loopback = null;
                _captureFormat = null;
                Logger.LogError("[AudioRecognizer] Loopback capture unavailable; audio remains off. No microphone fallback.", ex);
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_started) return;
            _started = false;
            try { _loopback?.StopRecording(); } catch { }
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_disposing || !_started || !ReferenceEquals(sender, _loopback)) return;
        if (e?.Buffer == null || e.BytesRecorded <= 0 || e.BytesRecorded > e.Buffer.Length) return;
        if (!TryReadPeak(e.Buffer.AsSpan(0, e.BytesRecorded), _captureFormat, out var max)) return;

        // Update baseline (exponential moving average) to model background music bed.
        _baseline = (_baseline * BaselineDecay) + (max * (1 - BaselineDecay));

        var dynamicThreshold = Math.Max(AbsoluteThreshold, _baseline + _transientDelta);

        if (max >= dynamicThreshold)
        {
            var nowMs = _clock.Elapsed.TotalMilliseconds;
            if (!CooldownReady(nowMs, _lastCueMs)) return;
            _lastCueMs = nowMs;

            CueDetected?.Invoke(new GameAudioCue
            {
                Type = "audio_peak",
                Confidence = Math.Min(1.0, max),
                CapturedUtc = DateTime.UtcNow,
                Metadata = $"peak={max:0.000};baseline={_baseline:0.000};threshold={dynamicThreshold:0.000}"
            });
        }
    }

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        if (_disposing || !ReferenceEquals(sender, _loopback)) return;
        _started = false;
        if (e.Exception != null)
        {
            Logger.LogError("[AudioRecognizer] Capture stopped with error", e.Exception);
        }
    }

    public void Dispose()
    {
        _disposing = true;
        Stop();
        _loopback?.Dispose();
        _loopback = null;
        _captureFormat = null;
    }

    /// <summary>Decode supported negotiated sample formats; never interpret unknown bytes as PCM16.</summary>
    internal static bool TryReadPeak(ReadOnlySpan<byte> samples, WaveFormat? format, out double peak)
    {
        peak = 0;
        if (format is WaveFormatExtensible extensible) format = extensible.ToStandardWaveFormat();
        if (format == null || format.Channels <= 0 || format.SampleRate <= 0 || samples.Length is 0 or > 1_048_576)
            return false;
        var floating = format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32;
        var pcm = format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample is 8 or 16 or 24 or 32;
        if (!floating && !pcm) return false;
        var bytesPerSample = format.BitsPerSample / 8;
        if (format.BlockAlign != (long)bytesPerSample * format.Channels || format.BlockAlign <= 0 || samples.Length % format.BlockAlign != 0)
            return false;
        double maximum = 0;
        for (var offset = 0; offset < samples.Length; offset += bytesPerSample)
        {
            double value;
            if (floating) value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(samples[offset..]));
            else if (bytesPerSample == 1) value = (samples[offset] - 128) / 128.0;
            else if (bytesPerSample == 2) value = BinaryPrimitives.ReadInt16LittleEndian(samples[offset..]) / 32768.0;
            else if (bytesPerSample == 3)
            {
                var sample = samples[offset] | (samples[offset + 1] << 8) | (samples[offset + 2] << 16);
                value = ((sample << 8) >> 8) / 8388608.0;
            }
            else value = BinaryPrimitives.ReadInt32LittleEndian(samples[offset..]) / 2147483648.0;
            if (!double.IsFinite(value)) return false;
            maximum = Math.Max(maximum, Math.Abs(value));
        }
        peak = Math.Min(1.0, maximum);
        return true;
    }

    internal static bool CooldownReady(double monotonicMs, double? previousMs) =>
        double.IsFinite(monotonicMs) && monotonicMs >= 0 &&
        (!previousMs.HasValue || (double.IsFinite(previousMs.Value) && previousMs.Value >= 0 &&
            monotonicMs - previousMs.Value >= CueCooldown.TotalMilliseconds));
}
