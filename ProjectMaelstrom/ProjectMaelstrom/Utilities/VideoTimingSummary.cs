namespace ProjectMaelstrom.Utilities;

/// <summary>Measured intervals between compositor timestamps, not assumed display refreshes.</summary>
internal sealed record VideoTimingSummary(int Frames, double DurationMs, double? DeliveredFps, double? GapP50Ms, double? GapP95Ms, double? GapP99Ms, double? MaximumGapMs)
{
    public static VideoTimingSummary Create(IReadOnlyList<double> timestamps)
    {
        if (timestamps.Any(t => !double.IsFinite(t) || t < 0)) throw new InvalidDataException("Invalid capture timestamp.");
        var gaps = new List<double>();
        for (var i = 1; i < timestamps.Count; i++)
        {
            var gap = timestamps[i] - timestamps[i - 1];
            if (gap < 0) throw new InvalidDataException("Capture timestamps are out of order.");
            gaps.Add(gap);
        }
        gaps.Sort();
        var duration = timestamps.Count > 1 ? timestamps[^1] - timestamps[0] : 0;
        double? percentile(double p) => gaps.Count == 0 ? null : gaps[(int)Math.Ceiling(gaps.Count * p) - 1];
        return new(timestamps.Count, duration, duration > 0 ? (timestamps.Count - 1) * 1000 / duration : null,
            percentile(.5), percentile(.95), percentile(.99), gaps.Count == 0 ? null : gaps[^1]);
    }
}

/// <summary>Difference between capture time and saved playback time, after aligning their first frames.</summary>
internal sealed record VideoTimingError(double MedianMs, double P95Ms, double MaximumMs)
{
    public static VideoTimingError? Create(IReadOnlyList<double> capture, IReadOnlyList<double> playback)
    {
        if (capture.Count != playback.Count) throw new InvalidDataException("Capture and playback frame counts do not match.");
        if (capture.Count == 0) return null;
        _ = VideoTimingSummary.Create(capture);
        _ = VideoTimingSummary.Create(playback);
        var errors = new List<double>(capture.Count);
        for (var i = 0; i < capture.Count; i++)
            errors.Add(Math.Abs((capture[i] - capture[0]) - (playback[i] - playback[0])));
        errors.Sort();
        return new(errors[(int)Math.Ceiling(errors.Count * .5) - 1], errors[(int)Math.Ceiling(errors.Count * .95) - 1], errors[^1]);
    }
}

/// <summary>Compares the observed recording interval with the saved video duration.</summary>
internal sealed record VideoCoverageSummary(double ObservedDurationMs, double VideoDurationMs, double UnrepresentedTailMs, double LastFrameAfterStopMs)
{
    public static VideoCoverageSummary? Create(IReadOnlyList<double> captureTimestampsMs, double stoppedAtMs, double videoDurationSeconds)
    {
        _ = VideoTimingSummary.Create(captureTimestampsMs);
        if (!double.IsFinite(stoppedAtMs) || stoppedAtMs < 0 || !double.IsFinite(videoDurationSeconds) || videoDurationSeconds < 0)
            throw new InvalidDataException("Invalid recording coverage timing.");
        if (captureTimestampsMs.Count == 0) return null;
        var lead = Math.Max(0, captureTimestampsMs[^1] - stoppedAtMs);
        if (lead > 100) throw new InvalidDataException($"Recording ended before its last captured frame by {lead:0.###} ms.");
        var observed = Math.Max(stoppedAtMs, captureTimestampsMs[^1]) - captureTimestampsMs[0];
        var video = videoDurationSeconds * 1000;
        return new(observed, video, Math.Max(0, observed - video), lead);
    }
}

/// <summary>Describes measured timings; signed values can reflect clock differences.</summary>
internal sealed record TimingDistribution(int Samples, double MedianMs, double P95Ms, double MaximumMs)
{
    public static TimingDistribution? Create(IReadOnlyList<double> values)
    {
        if (values.Any(value => !double.IsFinite(value))) throw new InvalidDataException("Invalid measured timing.");
        if (values.Count == 0) return null;
        var sorted = values.OrderBy(value => value).ToArray();
        double percentile(double fraction) => sorted[(int)Math.Ceiling(sorted.Length * fraction) - 1];
        return new(sorted.Length, percentile(.5), percentile(.95), sorted[^1]);
    }
}
