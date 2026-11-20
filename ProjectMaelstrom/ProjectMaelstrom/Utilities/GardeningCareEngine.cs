using System;
using System.Collections.Generic;
using System.Drawing;

namespace ProjectMaelstrom.Utilities;

public record PlantTypeInfo(
    string Name,
    int BaseHoursToMature,
    int BaseHoursToElder,
    int EnergyToCare,
    IReadOnlyList<string> Likes,
    IReadOnlyList<string> Needs);

/// <summary>
/// Native C# implementation of the Wizard101 gardening care state machine and growth rate multipliers.
/// Ported directly from gardening-bot-main (config/plant_info.json & core/gardener.py).
/// </summary>
public static class GardeningCareEngine
{
    // Standard likes giving +20% growth rate each (multiplier = 1.20)
    public const double StandardLikeMultiplier = 1.20;

    public static readonly Dictionary<string, PlantTypeInfo> Catalog = new(StringComparer.OrdinalIgnoreCase)
    {
        {
            "Couch Potatoes",
            new PlantTypeInfo(
                "Couch Potatoes",
                BaseHoursToMature: 48,
                BaseHoursToElder: 96,
                EnergyToCare: 15,
                Likes: ["Tropical Garden Gnome", "Red Barn Farm", "King Parsley", "Sandwich Station", "Litter"],
                Needs: ["Water", "Music"])
        },
        {
            "Evil Magma Peas",
            new PlantTypeInfo(
                "Evil Magma Peas",
                BaseHoursToMature: 36,
                BaseHoursToElder: 72,
                EnergyToCare: 20,
                Likes: ["Tropical Garden Gnome", "Red Barn Farm", "Djembe Drum", "Egg Basket"],
                Needs: ["Water", "Pollination"])
        }
    };

    /// <summary>
    /// Computes the effective growth multiplier based on active likes present in the garden.
    /// Each active like adds 20% growth speed (compounds multiplicatively: 1.20^N).
    /// </summary>
    public static double CalculateGrowthMultiplier(string plantName, IEnumerable<string> activeGardenItems)
    {
        if (!Catalog.TryGetValue(plantName, out var plant))
        {
            return 1.0;
        }

        var activeSet = new HashSet<string>(activeGardenItems, StringComparer.OrdinalIgnoreCase);
        int matchedLikes = 0;
        foreach (var like in plant.Likes)
        {
            if (activeSet.Contains(like))
            {
                matchedLikes++;
            }
        }

        return Math.Pow(StandardLikeMultiplier, matchedLikes);
    }

    /// <summary>
    /// Calculates the effective hours to reach Mature and Elder given active garden likes.
    /// </summary>
    public static (double HoursToMature, double HoursToElder) CalculateGrowthTime(
        string plantName, IEnumerable<string> activeGardenItems)
    {
        if (!Catalog.TryGetValue(plantName, out var plant))
        {
            return (0.0, 0.0);
        }

        double multiplier = CalculateGrowthMultiplier(plantName, activeGardenItems);
        return (
            Math.Round(plant.BaseHoursToMature / multiplier, 2),
            Math.Round(plant.BaseHoursToElder / multiplier, 2)
        );
    }

    /// <summary>
    /// Resolves the spell name and energy cost needed to satisfy a specific plant need.
    /// </summary>
    public static (string SpellName, int EnergyCost) ResolveGardeningSpell(string need) => need.ToLowerInvariant() switch
    {
        "water" => ("Downpour", 15),
        "music" => ("Flute Ensemble", 15),
        "sunlight" or "sun" => ("Sunburst", 15),
        "pollination" => ("Pest Zapper", 15),
        _ => ("Minor Blessing", 5)
    };
}
