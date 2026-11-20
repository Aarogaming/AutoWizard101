using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Offline adapter for the existing game-session v1 JSONL stream. Only
/// explicit routine observations/receipts affect the runner. Never fabricates
/// receipts, opens a game, loads input modules or waits for wall-clock time.</summary>
internal static class RoutineReplay
{
    internal sealed record Summary(string RoutineId, string GameId, string SessionId, string EvidenceMode,
        bool Completed, bool Stopped, string? Reason, string GraphSha256, string ManifestSha256,
        string EventsSha256, int ProposalCount, int ReceiptCount, bool LiveQualified, string ReportPath);

    public static bool Run(string graphPath, string sessionPath, string reportPath,
        JobCardSpec? card = null, IReadOnlyList<JobFactDefinition>? definitions = null)
        => RunDetailed(graphPath, sessionPath, reportPath, card, definitions).Completed;

    public static Summary RunDetailed(string graphPath, string sessionPath, string reportPath,
        JobCardSpec? card = null, IReadOnlyList<JobFactDefinition>? definitions = null)
    {
        var graphBytes = ReadBounded(graphPath, 65536);
        var graph = RoutineGraphs.Read(Encoding.UTF8.GetString(graphBytes));
        if (card != null)
        {
            foreach (var c in card.Conditions ?? []) JobFactCatalog.ValidateCondition(c, definitions);
            foreach (var f in card.Filters ?? []) JobFactCatalog.ValidateFilter(f, definitions);
        }
        var manifestBytes = ReadBounded(sessionPath, 65536);
        using var manifest = Parse(manifestBytes);
        var root = manifest.RootElement;
        if (root.GetProperty("schema_version").GetInt32() != 1 ||
            string.IsNullOrWhiteSpace(root.GetProperty("session_id").GetString()) ||
            string.IsNullOrWhiteSpace(root.GetProperty("clock_origin").GetString()))
            throw new InvalidDataException("Unsupported or incomplete session manifest.");
        var filename = root.GetProperty("events_file").GetString();
        if (string.IsNullOrWhiteSpace(filename) || filename is "." or ".." ||
            filename.IndexOfAny(['/', '\\', ':']) >= 0 || Path.GetFileName(filename) != filename)
            throw new InvalidDataException("Session events must be a local filename.");
        var binding = root.GetProperty("routine_binding").Deserialize<RoutineBinding>(RoutineGraphs.Json)
            ?? throw new InvalidDataException("Session needs an explicit routine binding.");
        var eventsPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sessionPath))!, filename);
        if (File.GetAttributes(eventsPath).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException("Session event links are not supported.");
        var eventBytes = ReadBounded(eventsPath, 16 * 1024 * 1024);
        // This catalog permits proposals ONLY in this offline reader. It is never
        // passed to a live dispatcher; live adapters use a hard-coded allowlist.
        var runner = new RoutineRunner(graph, binding, graph.Nodes.SelectMany(n => n.Edges)
            .Select(e => e.ActionId).ToHashSet(StringComparer.Ordinal), root.GetProperty("session_id").GetString());
        var trace = new List<object>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        var lastSequence = -1; double lastTime = 0; var proposals = 0; var receipts = 0; var ignored = 0;
        string? conditionBlock = null;
        JsonElement? recordedFacts = null;
        using var lines = new StringReader(Encoding.UTF8.GetString(eventBytes));
        while (lines.ReadLine() is { } line)
        {
            if (line.Length > 1048576) throw new InvalidDataException("Session line exceeds size limit.");
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = Parse(Encoding.UTF8.GetBytes(line));
            var item = document.RootElement;
            var id = item.GetProperty("event_id").GetString();
            var sequence = item.GetProperty("sequence").GetInt32();
            var time = item.GetProperty("time_ms").GetDouble();
            var kind = item.GetProperty("kind").GetString();
            if (string.IsNullOrWhiteSpace(id) || ids.Count >= 20000 || !ids.Add(id) || sequence <= lastSequence ||
                !double.IsFinite(time) || time < lastTime || kind is not ("observation" or "action_proposal" or "action_receipt" or "outcome" or "marker"))
                throw new InvalidDataException("Invalid, repeated or out-of-order session event.");
            lastSequence = sequence; lastTime = time;
            var payload = item.GetProperty("payload");
            if (payload.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Event payload must be an object.");
            if (kind is "observation" or "outcome" && payload.TryGetProperty("routine_observation", out var observation))
            {
                // A complete snapshot replaces the earlier one. Missing values never inherit
                // individual fields. Evidence is scoped to this event stream and binding.
                recordedFacts = payload.TryGetProperty("recorded_facts", out var facts) ? facts.Clone() : null;
                var frame = observation.Deserialize<RoutineObservation>(RoutineGraphs.Json)
                    ?? throw new InvalidDataException("Missing routine observation.");
                var decision = runner.Observe(frame with { TimeMs = time });
                if (decision.IsProposal && card != null)
                {
                    conditionBlock = ConditionBlock(card, graph.GameId, frame with { TimeMs = time }, recordedFacts, definitions);
                    if (conditionBlock != null)
                    {
                        trace.Add(new { event_id = id, time_ms = time, scene = frame.Scene, condition_block = conditionBlock });
                        break;
                    }
                }
                if (decision.IsProposal) proposals++;
                trace.Add(new { event_id = id, time_ms = time, scene = frame.Scene, decision });
            }
            else if (kind == "action_receipt" && payload.TryGetProperty("routine_receipt", out var receipt))
            {
                var dispatch = receipt.Deserialize<RoutineReceipt>(RoutineGraphs.Json)
                    ?? throw new InvalidDataException("Missing routine receipt.");
                runner.RecordReceipt(dispatch with { TimeMs = time }); receipts++;
                trace.Add(new { event_id = id, time_ms = time, receipt = dispatch with { TimeMs = time }, decision = runner.Status });
            }
            else ignored++;
        }
        var status = runner.Status;
        var completed = status.Completed && conditionBlock == null;
        var stopped = status.Stopped || conditionBlock != null;
        var reason = conditionBlock ?? status.Reason;
        var graphDigest = Digest(graphBytes);
        var manifestDigest = Digest(manifestBytes);
        var eventsDigest = Digest(eventBytes);
        var sessionId = root.GetProperty("session_id").GetString()!;
        var report = new
        {
            schema_version = 1, routine_id = graph.Id, game_id = graph.GameId,
            session_id = sessionId, evidence_mode = "labelled-state-replay",
            completed, stopped, reason,
            card_hash = card == null ? null : RecordedCardReview.Hash(card), conditions_checked = card != null,
            desktop_dispatch_count = 0, live_qualified = false, proposal_count = proposals, receipt_count = receipts,
            ignored_events = ignored, sha256_graph = graphDigest, sha256_manifest = manifestDigest,
            sha256_events = eventsDigest, trace
        };
        using var output = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(output, report, RoutineGraphs.Json);
        return new(graph.Id, graph.GameId, sessionId, "labelled-state-replay", completed, stopped, reason,
            graphDigest, manifestDigest, eventsDigest, proposals, receipts, false, Path.GetFullPath(reportPath));
    }

    internal static string? ConditionBlock(JobCardSpec card, string game, RoutineObservation frame,
        JsonElement? evidence, IReadOnlyList<JobFactDefinition>? definitions)
    {
        if ((card.Conditions?.Length ?? 0) == 0 && (card.Filters?.Length ?? 0) == 0) return null;
        if (evidence is not { ValueKind: JsonValueKind.Object } snapshot)
            return "Requirements unknown: this observation has no recorded facts.";
        if (snapshot.GetProperty("game_id").GetString() != game ||
            snapshot.GetProperty("target_id").GetString() != frame.Binding.TargetId ||
            snapshot.GetProperty("context_id").GetString() != frame.Binding.ContextId)
            return "Requirements unknown: recorded fact binding differs.";
        var time = snapshot.GetProperty("observed_time_ms").GetDouble();
        if (!double.IsFinite(time) || time < 0 || time > frame.TimeMs || frame.TimeMs - time > 2000)
            return "Requirements unknown: recorded facts are stale or from the future.";
        var values = snapshot.GetProperty("values").Deserialize<Dictionary<string, JsonElement>>(JobDeckRules.Json)
            ?? throw new InvalidDataException("Missing recorded fact values.");
        if (values.Count > 128) throw new InvalidDataException("Too many recorded facts.");
        var facts = new JobFacts(game, frame.Binding.TargetId, DateTime.UnixEpoch.AddMilliseconds(time), values, "recorded-session");
        return JobDeckRules.FactBlock(card, game, facts, DateTime.UnixEpoch.AddMilliseconds(frame.TimeMs), true, definitions);
    }

    internal static JsonDocument Parse(byte[] bytes)
    {
        var document = JsonDocument.Parse(bytes, new() { MaxDepth = 16 });
        try { RoutineGraphs.CheckProperties(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }
    internal static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static byte[] ReadBounded(string path, int limit)
    {
        using var input = File.OpenRead(path);
        if (input.Length > limit) throw new InvalidDataException("Replay input exceeds size limit.");
        using var output = new MemoryStream(); var buffer = new byte[8192]; int count;
        while ((count = input.Read(buffer)) > 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("Replay input exceeds size limit.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
}
