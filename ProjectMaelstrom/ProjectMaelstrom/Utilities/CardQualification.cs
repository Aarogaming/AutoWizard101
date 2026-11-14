using System.Text;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal enum CardQualificationStage { Unqualified, ReplayPassed, LivePassed, Repeatable }
internal sealed record CardQualificationBinding(string CardHash, string ExecutionGraphHash, string RoutineId,
    string AdapterVersion, string ScopeHash, string FactSchemaHash);
internal sealed record CardQualificationEvidence(int SchemaVersion, string Id, CardQualificationBinding Binding,
    string SessionId, string EventsFile, string ManifestHash, string EventsHash, string ReportHash);
internal sealed record CardQualificationStatus(CardQualificationStage Stage, string Reason, string? EvidenceId = null)
{
    // Evidence never grants adapter availability, consent, ownership or dispatch permission.
    public bool GrantsExecutionPermission => false;
}

/// <summary>Offline, immutable qualification evidence. Only a canonical replay can
/// create evidence. No live verifier or promotion path exists in this version.</summary>
internal static class CardQualification
{
    private const int SmallLimit = 65536;
    private const int EvidenceLimit = 16 * 1024 * 1024;

    /// <summary>The expected graph hash and scope come from trusted adapter compilation,
    /// not a user-selected passing graph. Scope must cover game/build/assets/calibration.
    /// The native editor does not supply this association yet.</summary>
    internal static CardQualificationBinding Bind(JobCardSpec card, string expectedGraphHash, string adapterVersion,
        string scopeHash, IReadOnlyList<JobFactDefinition> definitions)
    {
        var binding = new CardQualificationBinding(RecordedCardReview.Hash(card).ToLowerInvariant(),
            expectedGraphHash.ToLowerInvariant(), card.RoutineId, adapterVersion, scopeHash.ToLowerInvariant(), FactsHash(definitions));
        ValidateBinding(binding); return binding;
    }

    /// <summary>Freezes exact inputs and reproduces the existing report with the native
    /// runner before publishing evidence.json last. A failed import can leave an
    /// incomplete evidence directory, which never qualifies. Never overwrites files.</summary>
    internal static CardQualificationEvidence RecordReplay(string newDirectory, JobCardSpec card,
        CardQualificationBinding expected, string graphPath, string sessionPath, string reportPath,
        IReadOnlyList<JobFactDefinition> definitions)
    {
        ValidateBinding(expected);
        if (Bind(card, expected.ExecutionGraphHash, expected.AdapterVersion, expected.ScopeHash, definitions) != expected)
            throw new InvalidDataException("Card or fact definitions differ from the expected execution binding.");
        var graph = Read(graphPath, SmallLimit); var manifest = Read(sessionPath, SmallLimit);
        if (RoutineReplay.Digest(graph) != expected.ExecutionGraphHash)
            throw new InvalidDataException("Replay graph is not the card's trusted execution graph.");
        using var parsed = RoutineReplay.Parse(manifest);
        var eventsFile = parsed.RootElement.GetProperty("events_file").GetString();
        ValidateFilename(eventsFile);
        var events = Read(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sessionPath))!, eventsFile!), EvidenceLimit);
        var report = Read(reportPath, EvidenceLimit);
        var evidence = new CardQualificationEvidence(1, Guid.NewGuid().ToString("N"), expected,
            parsed.RootElement.GetProperty("session_id").GetString()!, eventsFile!,
            RoutineReplay.Digest(manifest), RoutineReplay.Digest(events), RoutineReplay.Digest(report));
        ValidateReport(evidence, graph, report);
        var directory = Path.GetFullPath(newDirectory);
        if (Path.Exists(directory)) throw new IOException("Choose a new qualification evidence directory.");
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, "sources"));
        Directory.CreateDirectory(Path.Combine(directory, "session"));
        WriteNew(Path.Combine(directory, "sources", "graph.json"), graph);
        WriteNew(Path.Combine(directory, "sources", "card.json"), JsonSerializer.SerializeToUtf8Bytes(card, JobDeckRules.Json));
        WriteNew(Path.Combine(directory, "sources", "facts.json"), FactsBytes(definitions));
        var frozenSession = Path.Combine(directory, "session", "manifest.json");
        WriteNew(frozenSession, manifest);
        WriteNew(Path.Combine(directory, "session", eventsFile!), events);
        var frozenGraph = Path.Combine(directory, "sources", "graph.json");
        var frozenReport = Path.Combine(directory, "replay.json");
        // A caller-supplied completed flag is not proof: reproduce outcomes and receipts.
        var frozenCard = JsonSerializer.Deserialize<JobCardSpec>(Read(Path.Combine(directory, "sources", "card.json"), SmallLimit), JobDeckRules.Json)!;
        var frozenFacts = JsonSerializer.Deserialize<JobFactDefinition[]>(Read(Path.Combine(directory, "sources", "facts.json"), SmallLimit), JobDeckRules.Json)!;
        if (!RoutineReplay.Run(frozenGraph, frozenSession, frozenReport, frozenCard, frozenFacts) ||
            RoutineReplay.Digest(Read(frozenReport, EvidenceLimit)) != evidence.ReportHash)
            throw new InvalidDataException("Replay evidence could not be reproduced from its frozen inputs.");
        WriteNew(Path.Combine(directory, "evidence.json"), JsonSerializer.SerializeToUtf8Bytes(evidence, JobDeckRules.Json));
        return evidence;
    }

    /// <summary>Assessment against the CURRENT binding without changing the ledger.
    /// Frozen inputs are replayed in a bounded temporary directory; mutable hashes
    /// in evidence.json are integrity checks, never proof of a recorded outcome.</summary>
    internal static CardQualificationStatus Assess(string directory, CardQualificationBinding current)
    {
        try
        {
            ValidateBinding(current);
            directory = Path.GetFullPath(directory);
            foreach (var folder in new[] { directory, Path.Combine(directory, "sources"), Path.Combine(directory, "session") })
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Qualification evidence directories cannot be links.");
            var evidence = JsonSerializer.Deserialize<CardQualificationEvidence>(Read(Path.Combine(directory, "evidence.json"), SmallLimit), JobDeckRules.Json)
                ?? throw new InvalidDataException("Missing qualification evidence.");
            if (evidence.SchemaVersion != 1 || !Guid.TryParseExact(evidence.Id, "N", out _))
                throw new InvalidDataException("Unsupported qualification evidence identity.");
            ValidateBinding(evidence.Binding);
            if (evidence.Binding != current) return new(CardQualificationStage.Unqualified, "Card, graph, adapter, facts or scope changed; prior evidence is historical.");
            ValidateFilename(evidence.EventsFile);
            var graph = Read(Path.Combine(directory, "sources", "graph.json"), SmallLimit);
            CheckHash(graph, current.ExecutionGraphHash);
            var card = Read(Path.Combine(directory, "sources", "card.json"), SmallLimit);
            var facts = Read(Path.Combine(directory, "sources", "facts.json"), SmallLimit);
            var manifest = Read(Path.Combine(directory, "session", "manifest.json"), SmallLimit);
            var events = Read(Path.Combine(directory, "session", evidence.EventsFile), EvidenceLimit);
            CheckHash(card, current.CardHash); CheckHash(facts, current.FactSchemaHash);
            CheckHash(manifest, evidence.ManifestHash); CheckHash(events, evidence.EventsHash);
            var report = Read(Path.Combine(directory, "replay.json"), EvidenceLimit);
            CheckHash(report, evidence.ReportHash); ValidateReport(evidence, graph, report);
            Reproduce(graph, card, facts, manifest, events, evidence);
            return new(CardQualificationStage.ReplayPassed, "Exact recorded replay passed; live gameplay and repeatability remain unverified.", evidence.Id);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {
            return new(CardQualificationStage.Unqualified, "Qualification evidence unavailable or invalid: " + error.Message);
        }
    }

    private static void Reproduce(byte[] graph, byte[] card, byte[] facts, byte[] manifest, byte[] events,
        CardQualificationEvidence evidence)
    {
        var parsedCard = JsonSerializer.Deserialize<JobCardSpec>(card, JobDeckRules.Json)
            ?? throw new InvalidDataException("Missing frozen card.");
        var parsedFacts = JsonSerializer.Deserialize<JobFactDefinition[]>(facts, JobDeckRules.Json)
            ?? throw new InvalidDataException("Missing frozen fact schema.");
        if (parsedCard.RoutineId != evidence.Binding.RoutineId || FactsHash(parsedFacts) != evidence.Binding.FactSchemaHash)
            throw new InvalidDataException("Frozen execution identity differs from its binding.");
        var temporary = Path.Combine(Path.GetTempPath(), "maelstrom-card-qualification-" + Guid.NewGuid().ToString("N"));
        var sessionDirectory = Path.Combine(temporary, "session");
        var graphPath = Path.Combine(temporary, "graph.json");
        var sessionPath = Path.Combine(sessionDirectory, "manifest.json");
        var eventsPath = Path.Combine(sessionDirectory, evidence.EventsFile);
        var reportPath = Path.Combine(temporary, "replay.json");
        Directory.CreateDirectory(temporary);
        try
        {
            Directory.CreateDirectory(sessionDirectory);
            // Replay copies of the bytes just verified, not paths that can change
            // between hash checking and the canonical reader reopening them.
            WriteNew(graphPath, graph); WriteNew(sessionPath, manifest); WriteNew(eventsPath, events);
            if (!RoutineReplay.Run(graphPath, sessionPath, reportPath, parsedCard, parsedFacts) ||
                RoutineReplay.Digest(Read(reportPath, EvidenceLimit)) != evidence.ReportHash)
                throw new InvalidDataException("Stored replay outcome cannot be reproduced; hash claims do not grant qualification.");
        }
        finally
        {
            // Delete only this invocation's known temporary files, never recurse
            // over a supplied path or modify the durable evidence directory.
            foreach (var file in new[] { graphPath, sessionPath, eventsPath, reportPath })
                if (File.Exists(file)) File.Delete(file);
            if (Directory.Exists(sessionDirectory)) Directory.Delete(sessionDirectory);
            Directory.Delete(temporary);
        }
    }

    private static void ValidateReport(CardQualificationEvidence evidence, byte[] graphBytes, byte[] reportBytes)
    {
        var graph = RoutineGraphs.Read(Encoding.UTF8.GetString(graphBytes));
        using var parsed = RoutineReplay.Parse(reportBytes);
        var report = parsed.RootElement;
        if (report.GetProperty("schema_version").GetInt32() != 1 ||
            report.GetProperty("evidence_mode").GetString() != "labelled-state-replay" ||
            !report.GetProperty("completed").GetBoolean() || report.GetProperty("stopped").GetBoolean() ||
            report.GetProperty("reason").GetString() != "observed-completion" ||
            !report.GetProperty("conditions_checked").GetBoolean() || report.GetProperty("live_qualified").GetBoolean() ||
            report.GetProperty("desktop_dispatch_count").GetInt32() != 0 ||
            report.GetProperty("proposal_count").GetInt32() < 1 ||
            report.GetProperty("receipt_count").GetInt32() != report.GetProperty("proposal_count").GetInt32() ||
            report.GetProperty("card_hash").GetString()?.ToLowerInvariant() != evidence.Binding.CardHash ||
            report.GetProperty("sha256_graph").GetString() != evidence.Binding.ExecutionGraphHash ||
            report.GetProperty("sha256_manifest").GetString() != evidence.ManifestHash ||
            report.GetProperty("sha256_events").GetString() != evidence.EventsHash ||
            report.GetProperty("session_id").GetString() != evidence.SessionId ||
            report.GetProperty("routine_id").GetString() != graph.Id || report.GetProperty("game_id").GetString() != graph.GameId)
            throw new InvalidDataException("Report is not a completed, condition-bound replay of this exact card and execution graph.");
    }

    private static void ValidateBinding(CardQualificationBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        foreach (var hash in new[] { binding.CardHash, binding.ExecutionGraphHash, binding.ScopeHash, binding.FactSchemaHash })
            if (hash is not { Length: 64 } || hash.Any(c => !char.IsAsciiDigit(c) && c is not (>= 'a' and <= 'f')))
                throw new InvalidDataException("Qualification requires normalized SHA-256 bindings.");
        foreach (var token in new[] { binding.RoutineId, binding.AdapterVersion })
            if (string.IsNullOrWhiteSpace(token) || token.Length > 128) throw new InvalidDataException("Qualification requires routine and adapter version.");
    }

    private static byte[] FactsBytes(IReadOnlyList<JobFactDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (definitions.Count > 512 || definitions.Any(definition => definition is null)) throw new InvalidDataException("Fact schema exceeds its bounds.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(definitions.OrderBy(definition => definition.Id, StringComparer.Ordinal).ToArray(), JobDeckRules.Json);
        if (bytes.Length > SmallLimit) throw new InvalidDataException("Fact schema exceeds its size limit.");
        return bytes;
    }
    private static string FactsHash(IReadOnlyList<JobFactDefinition> definitions) => RoutineReplay.Digest(FactsBytes(definitions));
    private static void ValidateFilename(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename) || filename.Length > 200 || filename is "." or ".." ||
            filename.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) || filename.IndexOfAny(['/', '\\', ':']) >= 0 || Path.GetFileName(filename) != filename)
            throw new InvalidDataException("Replay events require a local nonreserved filename.");
    }
    private static byte[] Read(string path, int limit)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Qualification artifacts cannot be links.");
        return RoutineReplay.ReadBounded(path, limit);
    }
    private static void CheckHash(byte[] bytes, string expected)
    {
        if (RoutineReplay.Digest(bytes) != expected) throw new InvalidDataException("Qualification artifact changed.");
    }
    private static void WriteNew(string path, byte[] bytes)
    {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(bytes); output.Flush(flushToDisk: true);
    }
}
