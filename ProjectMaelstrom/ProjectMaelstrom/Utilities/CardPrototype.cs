using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record CardPrototypeDocument(int SchemaVersion, string GameId, JobCardSpec Card);
internal sealed record CardPrototypeAttempt(int SchemaVersion, string Id, string CardHash, DateTime CreatedUtc, string RecordedOutcome)
{
    public override string ToString() => $"{CreatedUtc.ToLocalTime():g} · {RecordedOutcome} · {Id[..8]}";
}
internal sealed record CardPrototypeTestResult(CardPrototypeAttempt Attempt, CardQualificationStatus Qualification, string ReportDirectory);

/// <summary>Offline authoring over the existing catalog, replay and evidence ledger.
/// A prototype is configuration, never a runtime callback, dealt card or permission.</summary>
internal static class CardPrototype
{
    private const int Limit = 65536;
    internal const string ReplayAdapterVersion = "deck.review.routine-1+prototype-conditions-1";

    internal static CardPrototypeDocument Create(string routineId, string gameId, string title,
        IReadOnlyDictionary<string, JobRoutine> routines)
    {
        if (!routines.TryGetValue(routineId, out var routine)) throw new InvalidDataException("Choose a catalog action.");
        var document = new CardPrototypeDocument(1, gameId, JobDeckRoutines.NewCard(routine.Info) with { Title = title });
        Validate(document, routines); return document;
    }

    internal static void Validate(CardPrototypeDocument document, IReadOnlyDictionary<string, JobRoutine> routines)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != 1 || document.Card is null || document.Card.InDeck)
            throw new InvalidDataException("A prototype must use schema 1 and remain undealt.");
        JobDeckRules.Validate(new("Prototype", document.GameId, [document.Card]), routines);
        if (JsonSerializer.SerializeToUtf8Bytes(document, JobDeckRules.Json).Length > Limit)
            throw new InvalidDataException("Prototype exceeds its size limit.");
    }

    internal static void Save(string newPath, CardPrototypeDocument document, IReadOnlyDictionary<string, JobRoutine> routines)
    {
        var frozen = JobDeckRules.Copy(document); Validate(frozen, routines);
        using var output = new FileStream(Path.GetFullPath(newPath), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(output, frozen, JobDeckRules.Json); output.Flush(flushToDisk: true);
    }

    internal static CardPrototypeDocument Load(string path, IReadOnlyDictionary<string, JobRoutine> routines)
    {
        var bytes = RoutineReplay.ReadBounded(Path.GetFullPath(path), Limit);
        using var parsed = RoutineReplay.Parse(bytes);
        var document = parsed.RootElement.Deserialize<CardPrototypeDocument>(JobDeckRules.Json)
            ?? throw new InvalidDataException("Missing prototype.");
        Validate(document, routines); return document;
    }

    /// <summary>The only supported association is the Replay action's configured graph.
    /// Other routines cannot borrow an unrelated user-selected graph for qualification.</summary>
    internal static CardQualificationBinding ReplayBinding(CardPrototypeDocument document,
        IReadOnlyDictionary<string, JobRoutine> routines)
    {
        Validate(document, routines);
        if (document.Card.RoutineId != "review.routine")
            throw new InvalidOperationException("This action has no prototype replay association yet. It stays unqualified; an unrelated graph cannot qualify it.");
        JobDeckRules.Validate(new("Prototype test", document.GameId, [document.Card with { InDeck = true }]), routines);
        var graphPath = ConfiguredPath(document.Card, "graphFile");
        _ = ConfiguredPath(document.Card, "sessionFile");
        var bytes = RoutineReplay.ReadBounded(graphPath, Limit);
        var graph = RoutineGraphs.Read(System.Text.Encoding.UTF8.GetString(bytes));
        if (graph.GameId != document.GameId)
            throw new InvalidDataException("The configured routine belongs to a different game workspace.");
        var scope = RoutineReplay.Digest(JsonSerializer.SerializeToUtf8Bytes(new
        { mode = "configured-recorded-replay", gameId = document.GameId, graphGameId = graph.GameId }, JobDeckRules.Json));
        return CardQualification.Bind(document.Card, RoutineReplay.Digest(bytes), ReplayAdapterVersion, scope, Facts(document, routines));
    }

    internal static CardPrototypeTestResult Test(string dataRoot, CardPrototypeDocument document,
        IReadOnlyDictionary<string, JobRoutine> routines)
    {
        document = JobDeckRules.Copy(document);
        var binding = ReplayBinding(document, routines); // Reject unsupported association before writing history.
        var graph = ConfiguredPath(document.Card, "graphFile");
        var session = ConfiguredPath(document.Card, "sessionFile");
        var facts = Facts(document, routines);
        var id = Guid.NewGuid().ToString("N");
        var directory = AttemptDirectory(dataRoot, document.Card.Id, id);
        Directory.CreateDirectory(directory);
        var report = Path.Combine(directory, "replay.json");
        CardQualificationStatus status;
        try
        {
            if (!RoutineReplay.Run(graph, session, report, document.Card, facts))
                status = new(CardQualificationStage.Unqualified, "Recorded requirements and outcomes did not pass together. Inspect the saved report.");
            else
            {
                var evidence = CardQualification.RecordReplay(Path.Combine(directory, "evidence"), document.Card,
                    binding, graph, session, report, facts);
                status = new(CardQualificationStage.ReplayPassed, "This exact configured replay passed offline. Live play remains unqualified.", evidence.Id);
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or ArgumentException or InvalidOperationException or UnauthorizedAccessException or KeyNotFoundException or FormatException or OverflowException)
        { status = new(CardQualificationStage.Unqualified, "Test did not establish qualification: " + error.Message); }
        var attempt = new CardPrototypeAttempt(1, id, binding.CardHash, DateTime.UtcNow,
            status.Stage == CardQualificationStage.ReplayPassed ? "Offline replay passed (historical)" : "Unqualified test (historical)");
        using (var output = new FileStream(Path.Combine(directory, "attempt.json"), FileMode.CreateNew))
            JsonSerializer.Serialize(output, attempt, JobDeckRules.Json);
        return new(attempt, status, directory);
    }

    internal static CardPrototypeAttempt[] History(string dataRoot, string cardId)
    {
        var root = AttemptDirectory(dataRoot, cardId, null);
        if (!Directory.Exists(root)) return [];
        var history = new List<CardPrototypeAttempt>();
        foreach (var directory in Directory.EnumerateDirectories(root).Take(256))
        {
            if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _) ||
                (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
            try
            {
                var entry = JsonSerializer.Deserialize<CardPrototypeAttempt>(RoutineReplay.ReadBounded(Path.Combine(directory, "attempt.json"), 8192), JobDeckRules.Json);
                if (entry is { SchemaVersion: 1, CardHash.Length: 64,
                    RecordedOutcome: "Offline replay passed (historical)" or "Unqualified test (historical)" } && entry.Id == Path.GetFileName(directory)) history.Add(entry);
            }
            catch (Exception error) when (error is IOException or JsonException or InvalidDataException or UnauthorizedAccessException) { }
        }
        return history.OrderByDescending(entry => entry.CreatedUtc).ToArray();
    }

    internal static CardQualificationStatus ReviewHistory(string dataRoot, CardPrototypeDocument document,
        string attemptId, IReadOnlyDictionary<string, JobRoutine> routines)
    {
        var binding = ReplayBinding(document, routines);
        return CardQualification.Assess(Path.Combine(AttemptDirectory(dataRoot, document.Card.Id, attemptId), "evidence"), binding);
    }

    internal static IReadOnlyList<JobFactDefinition> Facts(CardPrototypeDocument document, IReadOnlyDictionary<string, JobRoutine> routines)
        => JobFactCatalog.All.Concat(routines[document.Card.RoutineId].Facts ?? []).GroupBy(fact => fact.Id, StringComparer.Ordinal).Select(group => group.Last()).ToArray();

    private static string ConfiguredPath(JobCardSpec card, string name)
    {
        var path = card.Settings?.GetValueOrDefault(name);
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw new InvalidDataException("Choose an absolute " + name + " path in this card's settings.");
        return Path.GetFullPath(path);
    }
    private static string AttemptDirectory(string dataRoot, string cardId, string? attemptId)
    {
        if (!Path.IsPathFullyQualified(dataRoot) || !Guid.TryParseExact(cardId, "N", out _) ||
            attemptId is not null && !Guid.TryParseExact(attemptId, "N", out _))
            throw new InvalidDataException("Prototype history requires an absolute data folder and local identifiers.");
        var root = Path.Combine(Path.GetFullPath(dataRoot), "card-prototype-tests", cardId);
        return attemptId is null ? root : Path.Combine(root, attemptId);
    }
}
