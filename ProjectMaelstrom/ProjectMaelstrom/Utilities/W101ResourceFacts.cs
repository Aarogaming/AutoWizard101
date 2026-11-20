using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record W101ResourcePair(double Current, double Maximum);
internal sealed record W101ResourceReading(string Status, long ObservedUtcTicks,
    Dictionary<string, W101ResourcePair> Values, Dictionary<string, double>? CurrentValues = null, string? Source = null);

/// <summary>Pure resource transform; only same-snapshot, bounded values become facts.</summary>
internal static class W101ResourceFacts
{
    internal static readonly string[] Resources = ["health", "mana", "energy", "potions"];
    internal static void Add(Dictionary<string, JsonElement> facts, W101ResourceReading? reading, long snapshotTicks)
    {
        if (snapshotTicks <= 0 || reading is not { Status: "observed", Values: not null } || reading.ObservedUtcTicks <= 0 ||
            reading.ObservedUtcTicks < snapshotTicks || reading.ObservedUtcTicks - snapshotTicks > TimeSpan.FromSeconds(2).Ticks) return;
        foreach (var name in new[] { "health", "mana", "energy" })
            if (reading.CurrentValues?.TryGetValue(name, out var current) == true && double.IsFinite(current) &&
                current >= 0 && current <= 100000 && current == Math.Truncate(current))
                facts[name] = JsonSerializer.SerializeToElement(current);
        foreach (var name in Resources)
        {
            if (reading.Values.TryGetValue(name, out var candidate) && candidate is not null &&
                reading.CurrentValues?.TryGetValue(name, out var current) == true && current != candidate.Current)
            { facts.Remove(name); continue; }
            if (!reading.Values.TryGetValue(name, out var pair) || pair is null ||
                !double.IsFinite(pair.Current) || !double.IsFinite(pair.Maximum) || pair.Current < 0 ||
                pair.Maximum <= 0 || pair.Current > pair.Maximum || pair.Maximum > int.MaxValue ||
                name != "potions" && (pair.Current != Math.Truncate(pair.Current) || pair.Maximum != Math.Truncate(pair.Maximum))) continue;
            facts[name] = JsonSerializer.SerializeToElement(pair.Current);
            facts[name + ".max"] = JsonSerializer.SerializeToElement(pair.Maximum);
            facts[name + ".percent"] = JsonSerializer.SerializeToElement(100 * pair.Current / pair.Maximum);
        }
    }
}
