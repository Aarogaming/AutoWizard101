using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record TeachingChunk(string Directory, double StartMs, double EndMs, string Status, double? VideoOriginMs);
internal sealed record TeachingActivity(string Id, string Label, double StartMs, double EndMs,
    string Trigger, string Action, string Outcome, string Commentary = "");
internal sealed record TeachingTarget(int ProcessId, long StartedUtcTicks, long WindowHandle, string ProcessName);
internal sealed record TeachingSession(int Version, string Id, string Game, double ClockOriginMs,
    string Status, TeachingChunk[] Chunks, TeachingActivity[] Activities, TeachingTarget? Target = null,
    long UtcOriginTicks = 0, string? StateTrack = null);

/// <summary>Small recoverable index. Chunk times use the same QPC clock as video and input.
/// Activity annotations are user hypotheses, never fabricated observations or dispatch receipts.</summary>
internal static class LongTeachingSession
{
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static TimeSpan[] Plan(int minutes)
    {
        if (minutes is < 1 or > 60) throw new ArgumentOutOfRangeException(nameof(minutes));
        return Enumerable.Range(0, (minutes + 1) / 2)
            .Select(i => TimeSpan.FromMinutes(Math.Min(2, minutes - i * 2))).ToArray();
    }

    public static void Save(string path, TeachingSession session)
    {
        Validate(session);
        var temporary = path + ".pending";
        File.WriteAllText(temporary, JsonSerializer.Serialize(session, Json));
        File.Move(temporary, path, true);
    }

    public static TeachingSession Read(string path)
    {
        if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException("Session index is too large.");
        var session = JsonSerializer.Deserialize<TeachingSession>(File.ReadAllText(path), Json)
            ?? throw new InvalidDataException("Missing session index.");
        Validate(session);
        return session;
    }

    private static void Validate(TeachingSession session)
    {
        if (session.Version != 1 || session.Chunks is null || session.Activities is null ||
            session.Chunks.Length > 30 || session.Activities.Length > 1000 ||
            !double.IsFinite(session.ClockOriginMs) || string.IsNullOrWhiteSpace(session.Id))
            throw new InvalidDataException("Invalid session index.");
        if (session.StateTrack is { } track && (track.Length != 76 || !track.StartsWith("state-", StringComparison.Ordinal) ||
            !track.EndsWith(".jsonl", StringComparison.Ordinal) || !track.Substring(6, 64).All(char.IsAsciiHexDigit)))
            throw new InvalidDataException("State track must be a local content-addressed file.");
        double previous = 0;
        foreach (var chunk in session.Chunks)
        {
            if (chunk is null || !double.IsFinite(chunk.StartMs) || !double.IsFinite(chunk.EndMs) ||
                chunk.StartMs < previous || chunk.EndMs < chunk.StartMs || chunk.EndMs > 7200000 ||
                string.IsNullOrWhiteSpace(chunk.Directory) || chunk.Directory is "." or ".." ||
                chunk.Directory.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') ||
                chunk.VideoOriginMs is { } origin && !double.IsFinite(origin))
                throw new InvalidDataException("Invalid chunk timeline or path.");
            previous = chunk.EndMs;
        }
        foreach (var activity in session.Activities)
            if (activity is null || !double.IsFinite(activity.StartMs) || !double.IsFinite(activity.EndMs) ||
                activity.StartMs < 0 || activity.EndMs <= activity.StartMs || activity.EndMs > previous ||
                string.IsNullOrWhiteSpace(activity.Label) || activity.Label.Length > 200 || activity.Commentary is null || activity.Commentary.Length > 4000)
                throw new InvalidDataException("Activity must be within the recorded timeline.");
    }

    public static RoutineGraph Compile(TeachingActivity activity, string gameId)
    {
        var duration = activity.EndMs - activity.StartMs;
        if (!double.IsFinite(duration) || duration <= 0) throw new InvalidDataException("Invalid activity duration.");
        var timeout = Math.Clamp(duration * 2, 1000, 300000);
        var graph = new RoutineGraph(1, activity.Id, gameId, "start", timeout, 1, ["loading", "unknown"],
            [new("start", [new("perform", activity.Trigger, activity.Action, [activity.Outcome], "done", timeout)]),
             new("done", [], activity.Outcome)]);
        RoutineGraphs.Validate(graph);
        return graph;
    }
}
