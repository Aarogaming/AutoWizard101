using System.Text;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Replays module evidence from the existing game-session v1 format.
/// Shares the routine replay's strict bounded readers and hash format; never
/// fabricates a receipt, launches a service or acquires desktop control.</summary>
internal static class ModuleReplay
{
    public static bool Run(string bundlePath, string sessionPath, string reportPath, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var bundleBytes = RoutineReplay.ReadBounded(bundlePath, ModuleCatalog.MaximumBundleCharacters);
        var bundle = ModuleCatalog.ReadBundle(Encoding.UTF8.GetString(bundleBytes));
        var manifestBytes = RoutineReplay.ReadBounded(sessionPath, 65536);
        using var manifest = RoutineReplay.Parse(manifestBytes);
        var root = manifest.RootElement;
        if (root.GetProperty("schema_version").GetInt32() != 1 ||
            string.IsNullOrWhiteSpace(root.GetProperty("clock_origin").GetString()))
            throw new InvalidDataException("Unsupported or incomplete session manifest.");
        var runId = root.GetProperty("session_id").GetString();
        OperationModules.CheckId(runId);
        var filename = root.GetProperty("events_file").GetString();
        if (string.IsNullOrWhiteSpace(filename) || filename is "." or ".." ||
            filename.IndexOfAny(['/', '\\', ':']) >= 0 || Path.GetFileName(filename) != filename)
            throw new InvalidDataException("Session events must be a local filename.");
        var binding = root.GetProperty("routine_binding").Deserialize<RoutineBinding>(OperationModules.Json)
            ?? throw new InvalidDataException("Session needs an explicit routine binding.");
        var eventsPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sessionPath))!, filename);
        if (File.GetAttributes(eventsPath).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException("Session event links are not supported.");
        var eventBytes = RoutineReplay.ReadBounded(eventsPath, 16 * 1024 * 1024);
        var runner = new ModuleRunner(bundle, binding, runId);
        var trace = new List<object>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        var lastSequence = -1; double lastTime = 0; var receipts = 0; var ignored = 0;
        using var lines = new StringReader(Encoding.UTF8.GetString(eventBytes));
        while (lines.ReadLine() is { } line)
        {
            token.ThrowIfCancellationRequested();
            if (line.Length > 1048576) throw new InvalidDataException("Session line exceeds size limit.");
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = RoutineReplay.Parse(Encoding.UTF8.GetBytes(line));
            var item = document.RootElement;
            var id = item.GetProperty("event_id").GetString(); var sequence = item.GetProperty("sequence").GetInt32();
            var time = item.GetProperty("time_ms").GetDouble(); var kind = item.GetProperty("kind").GetString();
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || ids.Count >= 20000 || !ids.Add(id) || sequence <= lastSequence ||
                !double.IsFinite(time) || time < lastTime || kind is not ("observation" or "action_proposal" or "action_receipt" or "outcome" or "marker"))
                throw new InvalidDataException("Invalid, repeated or out-of-order session event.");
            lastSequence = sequence; lastTime = time;
            var payload = item.GetProperty("payload");
            if (payload.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Event payload must be an object.");
            if (kind is "observation" or "outcome" && payload.TryGetProperty("module_observation", out var observation))
            {
                var frame = observation.Deserialize<ModuleObservation>(OperationModules.Json)
                    ?? throw new InvalidDataException("Missing module observation.");
                if (frame.Observation is null) throw new InvalidDataException("Missing routine observation inside module evidence.");
                var decision = runner.Observe(frame with { Observation = frame.Observation with { TimeMs = time } });
                trace.Add(new { event_id = id, time_ms = time, scene = frame.Observation.Scene, completed_modules = runner.CompletedModules, decision });
            }
            else if (kind == "action_receipt" && payload.TryGetProperty("routine_receipt", out var receipt))
            {
                var dispatch = receipt.Deserialize<RoutineReceipt>(OperationModules.Json)
                    ?? throw new InvalidDataException("Missing routine receipt.");
                runner.RecordReceipt(dispatch with { TimeMs = time }); receipts++;
                trace.Add(new { event_id = id, time_ms = time, receipt = dispatch with { TimeMs = time }, decision = runner.Status });
            }
            else ignored++;
        }
        var status = runner.Status;
        var report = new
        {
            schema_version = 1, bundle_id = bundle.Id, game_id = bundle.GameId, session_id = runId,
            evidence_mode = "labelled-module-replay", completed = status.Completed, stopped = status.Stopped, reason = status.Reason,
            desktop_dispatch_count = 0, live_qualified = false, module_count = bundle.Modules.Length, completed_modules = runner.CompletedModules,
            proposal_count = runner.ProposedActions, receipt_count = receipts, ignored_events = ignored,
            sha256_bundle = RoutineReplay.Digest(bundleBytes), sha256_manifest = RoutineReplay.Digest(manifestBytes),
            sha256_events = RoutineReplay.Digest(eventBytes), trace
        };
        token.ThrowIfCancellationRequested();
        using var output = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(output, report, OperationModules.Json);
        return status.Completed;
    }
}
