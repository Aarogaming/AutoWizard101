using System.Diagnostics;

namespace ProjectMaelstrom.Utilities;

/// <summary>Measures delivered frames against an expected cadence using a monotonic clock.</summary>
internal sealed class FrameTimeline
{
    private readonly double _periodMs;
    private double? _first;
    private double? _last;
    private long _frames;
    public FrameTimeline(double framesPerSecond)
    {
        if (!double.IsFinite(framesPerSecond) || framesPerSecond <= 0 || framesPerSecond > 240) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        _periodMs = 1000 / framesPerSecond;
    }

    public void Add(double timestampMs)
    {
        if (!double.IsFinite(timestampMs) || timestampMs < 0 || (_last.HasValue && timestampMs < _last.Value)) throw new ArgumentOutOfRangeException(nameof(timestampMs));
        _first ??= timestampMs;
        _last = timestampMs;
        _frames++;
    }

    public long Frames => _frames;
    // Estimated missing cadence slots; identical frame timestamps do not imply dropped frames.
    public long EstimatedMissing => !_first.HasValue || !_last.HasValue ? 0 : Math.Max(0, (long)Math.Floor((_last.Value - _first.Value) / _periodMs) + 1 - _frames);
    public double DurationMs => _last.GetValueOrDefault() - _first.GetValueOrDefault();
    public static double TimestampMs() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;
}
