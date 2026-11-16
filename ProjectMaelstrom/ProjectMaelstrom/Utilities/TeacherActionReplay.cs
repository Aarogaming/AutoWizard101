using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Derives one editable keyboard-action card from an owned teacher trace.
/// Replay IDs are remapped explicitly; receipt acceptance and state are never fabricated.</summary>
internal static class TeacherActionReplay
{
    internal sealed record LinkedAction(RoutineObservation Before, RoutineReceipt Receipt, RoutineObservation After,
        ModuleAction Action, string OriginalProposalId,
        Dictionary<string, JsonElement>? BeforeFacts = null, Dictionary<string, JsonElement>? AfterFacts = null);

    private static Dictionary<string, JsonElement>? Facts(JsonElement entry)
    {
        if (!entry.TryGetProperty("sample", out var observation) ||
            !observation.TryGetProperty("Sample", out var sample) || !sample.TryGetProperty("Reading", out var reading)) return null;
        // Stored module readings use C# property casing; normalize only the top-level contract.
        var normalized = reading.EnumerateObject().ToDictionary(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..], p => p.Value.Clone());
        if (normalized.TryGetValue("stableQuestContext", out var quest) && quest.ValueKind == JsonValueKind.Object)
            normalized["stableQuestContext"] = JsonSerializer.SerializeToElement(quest.EnumerateObject().ToDictionary(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..], p => p.Value.Clone()));
        return W101RecordedFacts.Read(JsonSerializer.SerializeToElement(normalized));
    }

    private static object? Envelope(Dictionary<string, JsonElement>? facts, RoutineObservation observation, double origin) =>
        facts == null ? null : new { game_id = "wizard101", target_id = observation.Binding.TargetId,
            context_id = observation.Binding.ContextId, observed_time_ms = observation.TimeMs - origin, values = facts };

    internal static LinkedAction Link(JsonElement report) => LinkAll(report).FirstOrDefault()
        ?? throw new InvalidDataException("No accepted/released keyboard action with a fresh, changed outcome was found.");

    /// <summary>Bounded non-overlapping actions; duplicate logs are not independent evidence.</summary>
    internal static LinkedAction[] LinkAll(JsonElement report)
    {
        var links = new List<LinkedAction>();
        var seen = new Dictionary<string, LinkedAction>(StringComparer.Ordinal);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        if (!report.GetProperty("finalIdleVerified").GetBoolean()) throw new InvalidDataException("Teacher did not verify return to Idle.");
        var rawEvents = report.GetProperty("events");
        if (rawEvents.GetArrayLength() > 4096) throw new InvalidDataException("Teacher event budget exceeded.");
        var events = rawEvents.EnumerateArray().ToArray();
        for (var i = 0; i + 2 < events.Length; i++)
        {
            if (events[i].GetProperty("kind").GetString() != "observation" || events[i + 1].GetProperty("kind").GetString() != "receipt") continue;
            var before = events[i].GetProperty("observed").Deserialize<RoutineObservation>(options)!;
            var decision = events[i].GetProperty("decision").Deserialize<ModuleDecision>(options)!;
            var receipt = events[i + 1].GetProperty("receipt").Deserialize<RoutineReceipt>(options)!;
            if (decision.Action is not { Kind: "keyboard-pulse" } action || decision.ProposalId == null ||
                receipt.ProposalId != decision.ProposalId || receipt.ActionId != action.ActionId || !receipt.Accepted || !receipt.InputReleased) continue;
            for (var j = i + 2; j < events.Length; j++)
            {
                if (events[j].GetProperty("kind").GetString() == "receipt") break;
                if (!events[j].TryGetProperty("observed", out var value)) continue;
                var after = value.Deserialize<RoutineObservation>(options)!;
                // A lost target, owner or context breaks attribution even after recovery.
                if (after.UserActive || after.Cancelled || before.Binding != after.Binding ||
                    !after.InputOwned || !after.TargetReady || !after.ContextVerified) break;
                if (before.Binding != after.Binding || !before.Fresh || !after.Fresh || !before.InputOwned || !after.InputOwned ||
                    !before.TargetReady || !after.TargetReady || !before.ContextVerified || !after.ContextVerified ||
                    before.Cancelled || after.Cancelled || before.UserActive || after.UserActive ||
                    !double.IsFinite(before.TimeMs) || !double.IsFinite(receipt.TimeMs) || !double.IsFinite(after.TimeMs) ||
                    receipt.TimeMs < before.TimeMs || after.TimeMs <= receipt.TimeMs || after.TimeMs - before.TimeMs > 15000 ||
                    before.Scene is "Unknown" or "Loading" || after.Scene is "Unknown" or "Loading" || after.Scene == before.Scene) continue;
                var linked = new LinkedAction(before, receipt, after, action, decision.ProposalId, Facts(events[i]), Facts(events[j]));
                if (seen.TryGetValue(decision.ProposalId, out var previous))
                {
                    if (previous.Before != before || previous.Receipt != receipt || previous.After != after || previous.Action != action)
                        throw new InvalidDataException("Conflicting evidence reuses a teacher proposal identity.");
                    break;
                }
                if (links.Count >= 64 || links.Count > 0 && before.TimeMs < links[^1].After.TimeMs)
                    throw new InvalidDataException("Teacher actions exceed the bounded non-overlapping sequence.");
                seen.Add(decision.ProposalId, linked); links.Add(linked);
                break;
            }
        }
        return links.ToArray();
    }

    public static void Export(string teacherPath, string inputPath, string outputDirectory)
    {
        if (new FileInfo(teacherPath).Length > 16 * 1024 * 1024 || new FileInfo(inputPath).Length > 16 * 1024 * 1024)
            throw new InvalidDataException("Teacher evidence exceeds budget.");
        using var teacher = JsonDocument.Parse(File.ReadAllText(teacherPath));
        using var input = JsonDocument.Parse(File.ReadAllText(inputPath));
        var linked = Link(teacher.RootElement); var raw = input.RootElement;
        var origin = raw.GetProperty("VideoCaptureOriginMs").GetDouble(); var end = raw.GetProperty("CaptureEndMs").GetDouble();
        var target = $"pid-{raw.GetProperty("TargetProcessId").GetInt32()}-start-{raw.GetProperty("TargetProcessStartUtcTicks").GetInt64()}";
        if (!raw.GetProperty("VideoVerified").GetBoolean() || raw.GetProperty("Truncated").GetBoolean() ||
            target != linked.Before.Binding.TargetId || origin > linked.Before.TimeMs || end < linked.After.TimeMs)
            throw new InvalidDataException("Capture does not cover the same teacher target/action interval.");
        var video = Path.Combine(Path.GetDirectoryName(inputPath)!, "capture.mp4");
        if (!GameExperiments.Digest(video).Equals(raw.GetProperty("VideoSha256").GetString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Recorded video hash changed.");
        if (Directory.Exists(outputDirectory)) throw new IOException("Choose a new teacher export folder.");
        Directory.CreateDirectory(outputDirectory);
        var id = "teacher-action"; var sessionId = Guid.NewGuid().ToString("N"); var proposal = sessionId + "/" + id + "/1";
        var activity = new TeachingActivity(id, "Observed " + linked.Action.ActionId,
            linked.Before.TimeMs - origin, linked.After.TimeMs - origin, linked.Before.Scene, linked.Action.ActionId, linked.After.Scene,
            "Automated bounded teacher; input evidence is an accepted/released window-message transport receipt, not a keyboard-hook event.");
        var graph = LongTeachingSession.Compile(activity, "wizard101");
        File.WriteAllText(Path.Combine(outputDirectory, "routine.json"), JsonSerializer.Serialize(graph, RoutineGraphs.Json));
        File.WriteAllText(Path.Combine(outputDirectory, "activity.json"), JsonSerializer.Serialize(activity, LongTeachingSession.Json));
        GameExperiments.WriteAtomic(Path.Combine(outputDirectory, "provenance.json"), new { teacher = teacherPath, sha256_teacher = GameExperiments.Digest(teacherPath),
            input = inputPath, sha256_input = GameExperiments.Digest(inputPath), source_proposal_id = linked.OriginalProposalId,
            replay_proposal_id = proposal, input_source = "window-message-dispatch-receipt", actor = "bounded-scripted-teacher", live_qualified = false });
        File.WriteAllText(Path.Combine(outputDirectory, "session.json"), JsonSerializer.Serialize(new { schema_version = 1, session_id = sessionId,
            clock_origin = "video-QPC-origin", events_file = "events.jsonl", routine_binding = linked.Before.Binding }, RoutineGraphs.Json));
        var json = new JsonSerializerOptions(RoutineGraphs.Json) { WriteIndented = false };
        File.WriteAllLines(Path.Combine(outputDirectory, "events.jsonl"), new[]
        {
            JsonSerializer.Serialize(new { event_id = "before", sequence = 0, time_ms = activity.StartMs, kind = "observation", payload = new { routine_observation = linked.Before with { TimeMs = activity.StartMs }, recorded_facts = Envelope(linked.BeforeFacts, linked.Before, origin) } }, json),
            JsonSerializer.Serialize(new { event_id = "dispatch", sequence = 1, time_ms = linked.Receipt.TimeMs - origin, kind = "action_receipt", payload = new { routine_receipt = linked.Receipt with { ProposalId = proposal, TimeMs = linked.Receipt.TimeMs - origin } } }, json),
            JsonSerializer.Serialize(new { event_id = "after", sequence = 2, time_ms = activity.EndMs, kind = "observation", payload = new { routine_observation = linked.After with { TimeMs = activity.EndMs, OutcomeForProposalId = proposal }, recorded_facts = Envelope(linked.AfterFacts, linked.After, origin) } }, json)
        });
        if (!RoutineReplay.Run(Path.Combine(outputDirectory, "routine.json"), Path.Combine(outputDirectory, "session.json"), Path.Combine(outputDirectory, "replay.json")))
            throw new InvalidDataException("Derived card did not complete evidence replay; inspect the report.");
        // Reconstruction on the discovery trace is not independent validation.
        // Export an editable, undealt draft; never insert or qualify a live card.
        var routines = JobDeckRoutines.Create().ToDictionary(routine => routine.Info.Id, StringComparer.Ordinal);
        var title = "Observed " + linked.Action.ActionId;
        var prototype = CardPrototype.Create("review.routine", "wizard101", title[..Math.Min(title.Length, 90)], routines);
        prototype = prototype with { Card = prototype.Card with
        {
            InDeck = false,
            Settings = new Dictionary<string, string>
            {
                ["graphFile"] = Path.GetFullPath(Path.Combine(outputDirectory, "routine.json")),
                ["sessionFile"] = Path.GetFullPath(Path.Combine(outputDirectory, "session.json"))
            }
        } };
        CardPrototype.Save(Path.Combine(outputDirectory, "prototype.json"), prototype, routines);
    }
}
