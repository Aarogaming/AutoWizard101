using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Offline bridge only; does not launch, hook or poll W101. Keeps the shared
/// timeline independent of W101. UTC alignment is explicitly distinct from QPC samples.</summary>
internal static class W101TeachingStateImport
{
    public static string Import(string requestPath, string sessionPath, TeachingSession session)
    {
        if (session.UtcOriginTicks <= 0 || session.Target == null)
            throw new InvalidDataException("This recording has no UTC synchronization anchor or exact target. Do not guess an offset.");
        if (new FileInfo(requestPath).Length > 65536) throw new InvalidDataException("Observer request exceeds budget.");
        var request = JsonSerializer.Deserialize<W101UiTreeRequest>(File.ReadAllText(requestPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Missing observer request.");
        if (request.Target == null || request.Target.ProcessId != session.Target.ProcessId || request.Target.StartedUtcTicks != session.Target.StartedUtcTicks ||
            request.Target.Handle != session.Target.WindowHandle || request.Target.ProcessName != session.Target.ProcessName)
            throw new InvalidDataException("Observer belongs to another game process/window.");
        var input = Path.Combine(Path.GetDirectoryName(requestPath)!, "ui-observations.jsonl");
        if (new FileInfo(input).Length > TeachingStateTrack.MaximumBytes) throw new InvalidDataException("Observer trace exceeds budget.");
        var temporary = Path.Combine(Path.GetDirectoryName(sessionPath)!, "import-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var count = 0; var context = 0;
            using (var writer = new StreamWriter(new FileStream(temporary, FileMode.CreateNew, FileAccess.Write)))
            foreach (var line in File.ReadLines(input))
            {
                if (line.Length > 262144) throw new InvalidDataException("Observer row exceeds budget.");
                using var doc = JsonDocument.Parse(line); var row = doc.RootElement;
                var kind = row.GetProperty("kind").GetString();
                if (kind is not ("tree" or "loading" or "tree-unavailable")) continue;
                // Not-loading is not a new UI snapshot. Preserve the tree's original age.
                if (kind == "loading" && row.TryGetProperty("loading", out var probe) && probe.ValueKind == JsonValueKind.False) continue;
                var ticks = row.GetProperty("observedUtcTicks").GetInt64();
                // The producer's tree acquisition time is earlier than its emission time.
                if (kind == "tree" && row.TryGetProperty("statesObservedUtcTicks", out var observed)) ticks = observed.GetInt64();
                var time = (ticks - session.UtcOriginTicks) / (double)TimeSpan.TicksPerMillisecond;
                if (time < 0 || time > (session.Chunks.LastOrDefault()?.EndMs ?? 0)) continue;
                var facts = new Dictionary<string, JsonElement>(); string? scene = null;
                var status = "unknown";
                if (kind == "tree")
                {
                    status = "observed";
                    facts = W101RecordedFacts.Read(row);
                    if (row.TryGetProperty("stableStates", out var states))
                    {
                        foreach (var field in states.EnumerateObject()) facts["ui." + field.Name] = field.Value.Clone();
                        foreach (var state in new[] { "nativeIdle", "dialogue", "menu", "interactionPrompt" })
                            if (states.TryGetProperty(state, out var value) && value.ValueKind == JsonValueKind.True) { scene = state; break; }
                    }
                    if (row.TryGetProperty("interactionTitle", out var title)) facts["ui.interactionTitle"] = title.Clone();
                    foreach (var name in new[] { "questContext", "poseCandidate" })
                        if (row.TryGetProperty(name, out var contextValue)) AddScalars(facts, name, contextValue, 0);
                }
                else { context++; if (kind == "loading" && row.TryGetProperty("loading", out var loading) && loading.ValueKind == JsonValueKind.True) status = "loading"; }
                JsonElement? tree = row.TryGetProperty("nodes", out var nodes) ? nodes.Clone() : null;
                var sample = new TeachingStateSample(1, session.Id, session.Target, time,
                    "w101.ui-tree.utc-aligned", "visit-" + context, status, scene, facts, tree);
                TeachingStateTrack.Validate(sample, session);
                writer.WriteLine(JsonSerializer.Serialize(sample));
                if (++count > TeachingStateTrack.MaximumSamples) throw new InvalidDataException("Observer sample budget exceeded.");
            }
            if (count == 0) throw new InvalidDataException("No observer readings overlap this recording. No timing offset was invented.");
            return TeachingStateTrack.Attach(temporary, sessionPath, session);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void AddScalars(Dictionary<string, JsonElement> facts, string prefix, JsonElement value, int depth)
    {
        if (depth > 3 || facts.Count >= 100) return;
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in value.EnumerateObject()) AddScalars(facts, prefix + "." + field.Name, field.Value, depth + 1);
        }
        else if (value.ValueKind != JsonValueKind.Array && value.GetRawText().Length <= 4096 && prefix.Length <= 128)
            facts[prefix] = value.Clone();
    }
}
