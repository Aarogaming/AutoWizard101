using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Bounded backend timeline for a manual-capture session. Records gate
/// snapshots against one UTC origin so front-end frames (TeachingManifest.StartedUtc
/// + TeachingFrame.TimeMs) and backend facts can be cross-compared offline.
/// No input, no capture, no inference; pure data retention.</summary>
internal sealed class ManualCaptureTimeline
{
    internal const int MaximumSamples = 1200; // 4 minutes at 5 Hz
    private readonly string _directory;
    private readonly long _originUtcTicks;
    private readonly string? _frontEndSession;
    private readonly List<Entry> _samples = [];

    private sealed record Entry(long UtcTicks, double RelativeMs, ManualCaptureSnapshot Snapshot);

    internal ManualCaptureTimeline(string directory, long originUtcTicks, string? frontEndSession = null)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A session directory is required.", nameof(directory));
        if (frontEndSession != null && !Directory.Exists(frontEndSession))
            throw new DirectoryNotFoundException("The selected front-end session folder does not exist: " + frontEndSession);
        _directory = directory;
        _originUtcTicks = originUtcTicks;
        _frontEndSession = frontEndSession;
    }

    internal int Count => _samples.Count;

    internal bool Add(ManualCaptureSnapshot snapshot, long utcTicks)
    {
        if (_samples.Count >= MaximumSamples) return false;
        if (utcTicks < _originUtcTicks) return false;
        var relative = (utcTicks - _originUtcTicks) / (double)TimeSpan.TicksPerMillisecond;
        if (relative > TimeSpan.FromMinutes(10).TotalMilliseconds) return false;
        _samples.Add(new Entry(utcTicks, relative, snapshot));
        return true;
    }

    internal void Write()
    {
        Directory.CreateDirectory(_directory);
        var lines = _samples.Select(s => JsonSerializer.Serialize(new
        {
            s.UtcTicks,
            s.RelativeMs,
            mode = s.Snapshot.Mode.ToString(),
            state = s.Snapshot.State.ToString(),
            trigger = s.Snapshot.Trigger.ToString(),
            mayDispatch = s.Snapshot.MayDispatch,
            explicitLatch = s.Snapshot.ExplicitLatch,
            quietMs = s.Snapshot.QuietMs,
            countdownMs = s.Snapshot.CountdownMs,
            s.Snapshot.Reason
        }, TimelineJson.Compact));
        File.WriteAllLines(Path.Combine(_directory, "backend-timeline.jsonl"), lines);
        File.WriteAllText(Path.Combine(_directory, "cross-compare.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            originUtcTicks = _originUtcTicks,
            samples = _samples.Count,
            frontEndSession = _frontEndSession,
            backendFile = "backend-timeline.jsonl",
            note = "Backend gate snapshots on the session clock. TeachingManifest.StartedUtc + TeachingFrame.TimeMs provide the front-end clock; alignment is offline and no dispatch is authorized by this data."
        }, TimelineJson.Options));
    }
}

internal static class TimelineJson
{
    internal static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    internal static readonly JsonSerializerOptions Compact = new();
}
