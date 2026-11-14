using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record TeachingStateSample(int Version, string SessionId, TeachingTarget Target,
    double TimeMs, string Source, string Context, string Status, string? Scene,
    Dictionary<string, JsonElement> Facts, JsonElement? UiTree = null);
internal sealed record TeachingStateView(TeachingStateSample? Sample, double? AgeMs, string Status);
internal sealed record TeachingCardProposal(TeachingActivity Activity, string Status, string[] Evidence,
    string[] Gaps, string SourceHash);

/// <summary>Offline, game-neutral instrument log. No process access, input, inference model,
/// or implicit promotion of annotations into measured outcomes.</summary>
internal static class TeachingStateTrack
{
    internal const int MaximumBytes = 32 * 1024 * 1024;
    internal const int MaximumSamples = 20000;
    internal const double FreshnessMs = 2000;

    public static TeachingStateSample[] Read(string path, TeachingSession session)
    {
        if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("State track exceeds 32 MB.");
        var samples = new List<TeachingStateSample>();
        double last = -1;
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length > 262144 || samples.Count >= MaximumSamples) throw new InvalidDataException("State track budget exceeded.");
            var sample = JsonSerializer.Deserialize<TeachingStateSample>(line)
                ?? throw new InvalidDataException("Missing state sample.");
            Validate(sample, session);
            if (sample.TimeMs < last) throw new InvalidDataException("State samples are out of order.");
            last = sample.TimeMs; samples.Add(sample);
        }
        return samples.ToArray();
    }

    internal static void Validate(TeachingStateSample sample, TeachingSession session)
    {
        if (sample.Version != 1 || sample.SessionId != session.Id || session.Target == null || sample.Target != session.Target ||
            !double.IsFinite(sample.TimeMs) || sample.TimeMs < 0 || sample.TimeMs > (session.Chunks.LastOrDefault()?.EndMs ?? 0) ||
            string.IsNullOrWhiteSpace(sample.Source) || sample.Source.Length > 128 ||
            string.IsNullOrWhiteSpace(sample.Context) || sample.Context.Length > 128 ||
            sample.Status is not ("observed" or "unknown" or "loading") || sample.Scene?.Length > 128 ||
            sample.Facts is null || sample.Facts.Count > 128 || sample.Facts.Any(f => string.IsNullOrWhiteSpace(f.Key) || f.Key.Length > 128 ||
                f.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object || f.Value.GetRawText().Length > 4096 ||
                f.Value.ValueKind == JsonValueKind.Number && (!f.Value.TryGetDouble(out var number) || !double.IsFinite(number))))
            throw new InvalidDataException("Invalid state sample, clock, or target/session binding.");
        if (sample.UiTree is { } tree && tree.GetRawText().Length > 131072)
            throw new InvalidDataException("UI tree exceeds 128K characters.");
    }

    public static TeachingStateView At(IReadOnlyList<TeachingStateSample> samples, double timeMs)
    {
        if (!double.IsFinite(timeMs) || timeMs < 0) throw new ArgumentOutOfRangeException(nameof(timeMs));
        // Latest whole snapshot wins, including explicit unknown/loading. Never carry
        // older facts across a transition or borrow the next frame's knowledge.
        var low = 0; var high = samples.Count - 1; var found = -1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            if (samples[middle].TimeMs <= timeMs) { found = middle; low = middle + 1; } else high = middle - 1;
        }
        if (found < 0) return new(null, null, "unknown");
        var sample = samples[found]; var age = timeMs - sample.TimeMs;
        return new(sample, age, age > FreshnessMs ? "stale" : sample.Status);
    }

    public static string Attach(string input, string sessionPath, TeachingSession session)
    {
        _ = Read(input, session);
        var bytes = File.ReadAllBytes(input);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("State track changed or exceeds budget.");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var output = Path.Combine(Path.GetDirectoryName(sessionPath)!, "state-" + hash + ".jsonl");
        if (!File.Exists(output))
        {
            using var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
            file.Write(bytes);
        }
        // Revalidate frozen bytes rather than trusting a file that could change during import.
        _ = Read(output, session);
        return output;
    }

    public static TeachingCardProposal[] Propose(TeachingSession session, IReadOnlyList<TeachingStateSample> samples, string sourceHash)
    {
        return session.Activities.Select(activity =>
        {
            var before = At(samples, activity.StartMs); var after = At(samples, activity.EndMs);
            var gaps = new List<string>(); var evidence = new List<string>();
            var trigger = activity.Trigger; var outcome = activity.Outcome;
            if (before.Status == "observed" && !string.IsNullOrWhiteSpace(before.Sample?.Scene))
            { trigger = before.Sample.Scene; evidence.Add($"Before: {before.Sample.TimeMs:0.###} ms · {before.Sample.Source} · {trigger}"); }
            else gaps.Add("No fresh scene at the activity start.");
            if (after.Status == "observed" && !string.IsNullOrWhiteSpace(after.Sample?.Scene))
            { outcome = after.Sample.Scene; evidence.Add($"After: {after.Sample.TimeMs:0.###} ms · {after.Sample.Source} · {outcome}"); }
            else gaps.Add("No fresh scene at the activity end.");
            if (before.Sample?.Context != after.Sample?.Context) gaps.Add("Context changed; transition needs its own review.");
            if (before.Status == "observed" && after.Status == "observed" && before.Sample != null && after.Sample != null)
                foreach (var fact in before.Sample.Facts)
                    if (after.Sample.Facts.TryGetValue(fact.Key, out var value) && fact.Value.GetRawText() != value.GetRawText())
                        evidence.Add($"Observed change: {fact.Key} = {fact.Value.GetRawText()} → {value.GetRawText()}");
            if (!string.IsNullOrWhiteSpace(activity.Commentary)) evidence.Add("Human commentary: " + activity.Commentary);
            gaps.Add("Action ID is user supplied; timestamps and repeated outcomes do not prove causation or dispatch.");
            return new TeachingCardProposal(activity with { Trigger = trigger, Outcome = outcome }, "needs-review", evidence.ToArray(), gaps.ToArray(), sourceHash);
        }).ToArray();
    }
}
