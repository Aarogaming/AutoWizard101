using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Pure projection of recorded UI evidence into the shared condition vocabulary.
/// Unknown, conflicting and unavailable readings are omitted, never guessed from scenes.</summary>
internal static class W101RecordedFacts
{
    internal static Dictionary<string, JsonElement> Read(JsonElement reading)
    {
        var facts = new Dictionary<string, JsonElement>();
        if (reading.TryGetProperty("loading", out var loading) && loading.ValueKind == JsonValueKind.True) return facts;
        if (reading.TryGetProperty("stableStates", out var stable) && stable.ValueKind == JsonValueKind.Object &&
            reading.TryGetProperty("states", out var current) && current.ValueKind == JsonValueKind.Object)
            foreach (var name in new[] { "nativeIdle", "dialogue", "menu", "interactionPrompt" })
                if (stable.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                    current.TryGetProperty(name, out var latest) && latest.ValueKind == value.ValueKind)
                    facts[name] = value.Clone();
        if (reading.TryGetProperty("stableQuestContext", out var quest) && quest.ValueKind == JsonValueKind.Object &&
            quest.TryGetProperty("objectiveText", out var objective) && objective.ValueKind == JsonValueKind.String)
        {
            facts["quest.objective"] = objective.Clone();
            var intent = W101QuestIntent.Parse(objective.GetString());
            if (intent.IsRecognized)
            {
                facts["quest.action"] = JsonSerializer.SerializeToElement(intent.Action.ToString());
                facts["quest.area"] = JsonSerializer.SerializeToElement(intent.Area);
                facts["quest.target"] = JsonSerializer.SerializeToElement(intent.Target);
            }
        }
        if (reading.TryGetProperty("resources", out var resources) && resources.ValueKind == JsonValueKind.Object &&
            reading.TryGetProperty("statesObservedUtcTicks", out var stamp) && stamp.TryGetInt64(out var ticks))
            W101ResourceFacts.Add(facts, resources.Deserialize<W101ResourceReading>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }), ticks);
        return facts;
    }
}
