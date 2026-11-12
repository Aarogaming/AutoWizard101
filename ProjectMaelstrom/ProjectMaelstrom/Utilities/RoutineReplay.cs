using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Offline adapter for the existing game-session v1 JSONL stream. Only
/// explicit routine observations/receipts affect the runner. Never fabricates
/// receipts, opens a game, loads input modules or waits for wall-clock time.</summary>
internal static class RoutineReplay
{
    public static bool Run(string graphPath, string sessionPath, string reportPath)
    {
        var graphBytes = ReadBounded(graphPath, 65536);
        var graph = RoutineGraphs.Read(Encoding.UTF8.GetString(graphBytes));
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
                var frame = observation.Deserialize<RoutineObservation>(RoutineGraphs.Json)
                    ?? throw new InvalidDataException("Missing routine observation.");
                var decision = runner.Observe(frame with { TimeMs = time });
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
        var report = new
        {
            schema_version = 1, routine_id = graph.Id, game_id = graph.GameId,
            session_id = root.GetProperty("session_id").GetString(), evidence_mode = "labelled-state-replay",
            completed = status.Completed, stopped = status.Stopped, reason = status.Reason,
            desktop_dispatch_count = 0, live_qualified = false, proposal_count = proposals, receipt_count = receipts,
            ignored_events = ignored, sha256_graph = Digest(graphBytes), sha256_manifest = Digest(manifestBytes),
            sha256_events = Digest(eventBytes), trace
        };
        using var output = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(output, report, RoutineGraphs.Json);
        return status.Completed;
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
