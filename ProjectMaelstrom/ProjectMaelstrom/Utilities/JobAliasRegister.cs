using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record JobAliasValue(string Id, string[] Aliases, string DataType, string Unit,
    JsonElement? Value, string Quality, string? Source, DateTime? ObservedUtc, string? GameId, string? TargetId);

/// <summary>Read-only views of canonical observations. No polling, conversion, writable tags or input.</summary>
internal static class JobAliasRegister
{
    internal static string[] Names(JobFactDefinition fact) => fact.Aliases ?? (fact.Id switch
    {
        "nativeIdle" => ["Idle"], "menu" => ["MenuOpen"], "dialogue" => ["DialogueOpen"],
        "interactionPrompt" => ["CanInteract"], "energy" => ["Energy"], "health" => ["Health"],
        "mana" => ["Mana"], "potions" => ["Potions"], _ => []
    });
    internal static string Type(JobFactDefinition fact) => fact.DataType ?? fact.Kind switch
    { "boolean" => "BOOL", "string" => "STRING", _ => "REAL" };
    internal static bool Matches(string type, JsonElement value) => type switch
    {
        "BOOL" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "STRING" => value.ValueKind == JsonValueKind.String,
        "INT" => value.ValueKind == JsonValueKind.Number && value.TryGetInt16(out _),
        "DINT" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
        "REAL" => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n) && double.IsFinite(n),
        _ => false
    };
    internal static void Validate(IReadOnlyList<JobFactDefinition> definitions)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fact in definitions)
        {
            var type = Type(fact);
            if (fact.Kind switch { "boolean" => type != "BOOL", "string" => type != "STRING", "number" => type is not ("INT" or "DINT" or "REAL"), _ => true })
                throw new InvalidDataException("Fact storage type does not match its kind.");
            if (Names(fact).Length > 16) throw new InvalidDataException("Too many aliases.");
            foreach (var name in new[] { fact.Id }.Concat(Names(fact)))
                if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_')) || !names.Add(name))
                    throw new InvalidDataException("Invalid or ambiguous fact alias.");
        }
    }
    internal static JobAliasValue[] Snapshot(JobFacts? facts, DateTime now, IReadOnlyList<JobFactDefinition>? extensions = null) =>
        JobFactCatalog.All.Concat(extensions ?? []).Select(definition =>
        {
            JsonElement value = default;
            var found = facts?.Values?.TryGetValue(definition.Id, out value) == true;
            var quality = !found ? "unknown" : !JobFactCatalog.ValueMatches(definition.Id, value, extensions) ? "invalid" :
                string.IsNullOrWhiteSpace(facts!.TargetId) || facts.ObservedUtc > now ? "unknown" :
                now - facts.ObservedUtc > TimeSpan.FromSeconds(definition.FreshnessSeconds) ? "stale" : "fresh";
            if (quality == "fresh" && definition.Producer == "w101.wizard-verification" &&
                (!W101WizardFacts.ValidSubject(facts!.WizardSubject) || facts.WizardSubject!.ObservedUtcTicks > now.Ticks ||
                    now.Ticks - facts.WizardSubject.ObservedUtcTicks > TimeSpan.FromSeconds(2).Ticks)) quality = "unknown";
            return new JobAliasValue(definition.Id, Names(definition).ToArray(), Type(definition), definition.Unit,
                quality == "fresh" ? value.Clone() : null, quality, facts?.Source, facts?.ObservedUtc, facts?.GameId, facts?.TargetId);
        }).ToArray();
}
