using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record JobFactDefinition(string Id, string Kind, string Unit, string Producer,
    bool ProducerAvailable, string Description, int FreshnessSeconds = 2, string? DataType = null, string[]? Aliases = null);

/// <summary>Trusted vocabulary only. A descriptor grants neither observations nor game input.</summary>
internal static class JobFactCatalog
{
    internal static IReadOnlyList<JobFactDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new JobFactDefinition("interactionPrompt", "boolean", "", "w101.ui-tree", true, "Stable interaction prompt."),
        new JobFactDefinition("dialogue", "boolean", "", "w101.ui-tree", true, "Stable spoken dialogue."),
        new JobFactDefinition("menu", "boolean", "", "w101.ui-tree", true, "Stable menu."),
        new JobFactDefinition("nativeIdle", "boolean", "", "w101.ui-tree", true, "Stable native Idle."),
        new JobFactDefinition("quest.objective", "string", "", "w101.ui-tree", true, "Observed objective text."),
        new JobFactDefinition("quest.action", "string", "", "w101.quest-intent", true, "Parsed objective action."),
        new JobFactDefinition("quest.area", "string", "", "w101.quest-intent", true, "Objective destination; not current location."),
        new JobFactDefinition("quest.target", "string", "", "w101.quest-intent", true, "Parsed objective target."),
        new JobFactDefinition("health", "number", "points", "w101.resources", true, "Current health; optional build-pinned HUD-correlated snapshot.", Aliases: ["Health", "Health.Current", "health.current"]),
        new JobFactDefinition("mana", "number", "points", "w101.resources", true, "Current mana; optional build-pinned HUD-correlated snapshot.", Aliases: ["Mana", "Mana.Current", "mana.current"]),
        new JobFactDefinition("energy", "number", "points", "w101.resources", true, "Current energy from the HUD; optional qualified snapshot.", Aliases: ["Energy", "Energy.Current", "energy.current"]),
        new JobFactDefinition("potions", "number", "bottles", "w101.resources", true, "Potion charge, including fractions; requires PotionButton/stats agreement.", Aliases: ["Potions", "Potions.Current", "potions.current"]),
        new JobFactDefinition("health.max", "number", "points", "w101.resources", true, "Health max; requires an exact-build observed StatGlobe maximum.", DataType: "DINT", Aliases: ["Health.Max"]),
        new JobFactDefinition("health.percent", "number", "percent", "w101.resources", true, "Health percent; requires an exact-build observed StatGlobe maximum.", DataType: "REAL", Aliases: ["Health.Percent"]),
        new JobFactDefinition("mana.max", "number", "points", "w101.resources", true, "Mana max; requires an exact-build observed StatGlobe maximum.", DataType: "DINT", Aliases: ["Mana.Max"]),
        new JobFactDefinition("mana.percent", "number", "percent", "w101.resources", true, "Mana percent; requires an exact-build observed StatGlobe maximum.", DataType: "REAL", Aliases: ["Mana.Percent"]),
        new JobFactDefinition("energy.max", "number", "points", "w101.resources", true, "Energy max; requires an exact-build observed StatGlobe maximum.", DataType: "DINT", Aliases: ["Energy.Max"]),
        new JobFactDefinition("energy.percent", "number", "percent", "w101.resources", true, "Energy percent; requires an exact-build observed StatGlobe maximum.", DataType: "REAL", Aliases: ["Energy.Percent"]),
        new JobFactDefinition("potions.max", "number", "points", "w101.resources", true, "Potions max; requires observed PotionButton/stats agreement.", DataType: "REAL", Aliases: ["Potions.Max"]),
        new JobFactDefinition("potions.percent", "number", "percent", "w101.resources", true, "Potions percent; requires observed PotionButton/stats agreement.", DataType: "REAL", Aliases: ["Potions.Percent"]),
        new JobFactDefinition("gold.current", "number", "gold", "w101.wizard-verification", true, "Current gold from exact-build stats with verified active-wizard ownership; optional resource scan.", DataType: "DINT", Aliases: ["Gold", "Gold.Current"]),
        new JobFactDefinition("wizard.level", "number", "level", "w101.wizard-verification", true, "Displayed level from owner-qualified stats; mutable metadata, never identity. Optional resource scan.", DataType: "DINT", Aliases: ["Wizard.Level"]),
        new JobFactDefinition("inventory", "number", "items", "unavailable", false, "Inventory count; not capacity."),
        new JobFactDefinition("area", "string", "", "unavailable", false, "Current area; not objective destination."),
        new JobFactDefinition("quest.type", "string", "", "unavailable", false, "Qualified quest classification."),
        new JobFactDefinition("quest.id", "string", "", "unavailable", false, "Stable quest identity.")
    });
    internal static JobFactDefinition? Find(string id, IReadOnlyList<JobFactDefinition>? extensions = null) =>
        All.Concat(extensions ?? []).FirstOrDefault(f => f.Id == id || JobAliasRegister.Names(f).Contains(id, StringComparer.Ordinal));
    internal static bool ValueMatches(string id, JsonElement value, IReadOnlyList<JobFactDefinition>? extensions = null) =>
        Find(id, extensions) is { } fact && JobAliasRegister.Matches(JobAliasRegister.Type(fact), value) && (fact.Kind switch
    {
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "string" => value.ValueKind == JsonValueKind.String,
        "number" => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n) && double.IsFinite(n),
        _ => false
    });
    internal static IReadOnlyList<JobFactDefinition> Freeze(IReadOnlyList<JobFactDefinition>? extensions)
    {
        var copy = extensions?.ToArray() ?? [];
        if (copy.Length > 64 || copy.Any(f => f is null || string.IsNullOrWhiteSpace(f.Id) || f.Id.Length > 80 ||
            f.Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_')) ||
            Find(f.Id) is not null || f.Kind is not ("boolean" or "string" or "number") ||
            string.IsNullOrWhiteSpace(f.Producer) || string.IsNullOrWhiteSpace(f.Description) || f.FreshnessSeconds != 2) ||
            copy.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new InvalidDataException("Invalid trusted fact definitions or attempt to override a standard fact.");
        copy = copy.Select(f => f with { Aliases = f.Aliases?.ToArray() }).ToArray();
        JobAliasRegister.Validate(All.Concat(copy).ToArray());
        return Array.AsReadOnly(copy);
    }
    internal static void ValidateCondition(JobCondition condition, IReadOnlyList<JobFactDefinition>? extensions = null)
    {
        var fact = Find(condition.Fact, extensions) ?? throw new InvalidDataException($"Unknown fact: {condition.Fact}.");
        if (!ValueMatches(fact.Id, condition.Value, extensions) || condition.Operator is not ("eq" or "gte" or "lte") ||
            condition.Operator != "eq" && fact.Kind != "number")
            throw new InvalidDataException($"{fact.Id} requires a {fact.Kind} value and a supported operator.");
    }
    internal static void ValidateFilter(JobFilter filter, IReadOnlyList<JobFactDefinition>? extensions = null)
    {
        if (Find(filter.Fact, extensions)?.Kind != "string")
            throw new InvalidDataException($"Filter {filter.Fact} requires a registered string fact.");
    }
}

