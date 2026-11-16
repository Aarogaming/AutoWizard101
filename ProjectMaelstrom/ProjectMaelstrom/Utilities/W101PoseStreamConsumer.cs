using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Single-owner stream state. Identity is pinned by the trusted owner,
/// never chosen from an incoming sample. Terminal events/errors require a new
/// consumer. Candidate transport does not establish wizard/zone qualification.</summary>
internal sealed class W101PoseStreamConsumer(string runId, string streamId, WizwalkerTarget target,
    string zone, string context, double maximumAgeMs, double uncertaintyMs)
{
    private long sequence;
    private double lastHostMs = -1, lastSampleMs = -1;
    private PathObservation? current;
    internal bool Ended { get; private set; }
    internal string? Reason { get; private set; }

    internal void Invalidate(string reason)
    {
        current = null; Ended = true; Reason = reason;
    }

    internal bool Accept(string line, long receivedUtcTicks, double hostMs, bool ownerCurrent)
    {
        if (Ended) return false;
        if (!ownerCurrent || !double.IsFinite(hostMs) || hostMs < lastHostMs)
        { Invalidate("owner-or-clock-changed"); return false; }
        lastHostMs = hostMs;
        try
        {
            if (line.Length > 16384) throw new InvalidDataException("event-budget");
            using var document = JsonDocument.Parse(line);
            string? kind = document.RootElement.GetProperty("kind").GetString();
            if (kind is "moving-pose-ended" or "moving-pose-invalidated")
            { Invalidate(kind); return false; }
            if (kind != "moving-pose-candidate") throw new InvalidDataException("unexpected-stream-event");
            var sample = W101PathSession.DecodeBoundStreamCandidate(line, runId, streamId, sequence,
                target, zone, context, receivedUtcTicks, hostMs, maximumAgeMs, uncertaintyMs);
            if (sample.TimeMs <= lastSampleMs) throw new InvalidDataException("sample-clock-not-increasing");
            sequence++; lastSampleMs = sample.TimeMs; current = sample;
            return true;
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { Invalidate("stream-envelope-rejected"); return false; }
    }

    internal PathObservation? ReadCandidate(double nowMs, bool ownerCurrent)
    {
        if (Ended) return null;
        if (!ownerCurrent || !double.IsFinite(nowMs) || nowMs < lastHostMs)
        { Invalidate("owner-or-clock-changed"); return null; }
        lastHostMs = nowMs;
        if (current is not null && nowMs - current.TimeMs > maximumAgeMs)
        { Invalidate("stream-expired"); return null; }
        return current;
    }
}
