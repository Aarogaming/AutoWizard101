using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Native C# implementation of the 55-zone WizardCity topology and route pathfinder.
/// Ported from Automatus-v2 (Automatus-v2-master/data/locations.py).
/// </summary>
public class ZoneTopologyService
{
    private static readonly Dictionary<string, string> FileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "WizardCity/WC_Hub", "COMMONS" },
        { "WizardCity/WC_Ravenwood", "RAVENWOOD" },
        { "WizardCity/Interiors/WC_SchoolFire", "SCHOOL_OF_FIRE" },
        { "WizardCity/Interiors/WC_TowerFire", "FIRE_TOWER" },
        { "WizardCity/Interiors/WC_SchoolFrost", "SCHOOL_OF_ICE" },
        { "WizardCity/Interiors/WC_TowerIce", "ICE_TOWER" },
        { "WizardCity/Interiors/WC_SchoolStorm", "SCHOOL_OF_STORM" },
        { "WizardCity/Interiors/WC_TowerStorm", "STORM_TOWER" },
        { "WizardCity/Interiors/WC_SchoolLife", "SCHOOL_OF_LIFE" },
        { "WizardCity/Interiors/WC_TowerLife", "LIFE_TOWER" },
        { "WizardCity/Interiors/WC_SchoolMyth", "SCHOOL_OF_MYTH" },
        { "WizardCity/Interiors/WC_TowerMyth", "MYTH_TOWER" },
        { "WizardCity/WC_Ravenwood_Teleporter", "WORLD_TREE" },
        { "WizardCity/WC_NightSide", "NIGHTSIDE" },
        { "WizardCity/Interiors/WC_SchoolDeath", "SCHOOL_OF_DEATH" },
        { "WizardCity/Interiors/WC_TowerDeath", "DEATH_TOWER" },
        { "G14_DM/DM_Z01_CastleDarkmoor", "DARKMOOR" },
        { "WizardCity/WC_Streets/WC_Unicorn", "UNICORN_WAY" },
        { "WizardCity/WC_Duel_Arena", "ARENA" },
        { "WizardCity/WC_Streets/Interiors/WC_Unicorn_HedgeMaze", "HEDGE_MAZE" },
        { "WizardCity/Interiors/WC_Library", "LIBRARY" },
        { "WizardCity/Interiors/WC_Castle_Tours", "CASTLE_TOURS" },
        { "WizardCity/WC_Streets/Interiors/WC_PET_Park", "PET_PAVILION" },
        { "WizardCity/Interiors/WC_Hatchery", "HATCHERY" },
        { "WizardCity/Interiors/WC_Headmistress_House", "HEADMASTERS_HOUSE" },
        { "WizardCity/Interiors/WC_Headmaster_Tower", "HEADMASTERS_TOWER" },
        { "WizardCity/WC_Golem_Tower", "GOLEM_COURT" },
        { "WizardCity/WC_Streets/WC_Drains/Z00_Drains_HUB", "DRAINS" },
        { "WizardCity/WC_Streets/WC_Catacombs", "CATACOMBS" },
        { "WizardCity/WC_Streets/WC_Catacombs_B", "LOWER_CATACOMBS" },
        { "WizardCity/WC_Shop_Area", "SHOPPING_DISTRICT" },
        { "WizardCity/Interiors/WC_Shop_Makeup", "MIRROR_SHOP" },
        { "WizardCity/Interiors/WC_ShopDye", "DYE_SHOP" },
        { "WizardCity/Interiors/WC_Shop_Hat", "HAT_SHOP" },
        { "WizardCity/Interiors/WC_Shop_Pets", "PET_SHOP" },
        { "WizardCity/Interiors/WC_Shop_Boots", "BOOT_SHOP" },
        { "WizardCity/Interiors/WC_Shop_Decks", "DECK_SHOP" },
        { "WizardCity/Interiors/WC_Shop_House", "HOUSING_SHOP" },
        { "WizardCity/Interiors/WC_Shop_Rings", "RING_SHOP" },
        { "WizardCity/Interiors/WC_Shop_Athames", "ATHAME_SHOP" },
        { "WizardCity/Interiors/WC_Shop_Jeweler", "JEWEL_SHOP" },
        { "WizardCity/Interiors/WC_Shop_Robes", "ROBE_SHOP" },
        { "WizardCity/Interiors/WC_Shop_Wand", "WAND_SHOP" },
        { "WizardCity/Interiors/WC_Shop_Amulets", "AMULET_SHOP" },
        { "WizardCity/WC_Streets/WC_Colossus", "COLOSSUS_BOULEVARD" },
        { "WizardCity/WC_Streets/Interiors/WC_Colossus_H1", "MILDREDS_HOUSE" },
        { "WizardCity/WC_Streets/WC_OldeTown", "OLDE_TOWN" },
        { "WizardCity/WC_Streets/Interiors/WC_OldeTown_AuctionHouse", "BAZAAR" },
        { "WizardCity/WC_Streets/WC_Cyclops", "CYCLOPS_LANE" },
        { "Aquila/AQ_Z00_Hub", "AQUILA" },
        { "WizardCity/WC_Streets/WC_Triton", "TRITON_AVENUE" },
        { "WizardCity/WC_Streets/WC_HauntedCave", "HAUNTED_CAVE" },
        { "WizardCity/WC_Streets/WC_Firecat", "FIRECAT_ALLEY" },
        { "WizardCity/WC_Streets/Interiors/WC_Firecat_H2", "GRETTAS_HOUSE" },
        { "WizardCity/WC_Streets/WC_DarkCave", "DARK_CAVE" }
    };

    private static readonly Dictionary<string, string> ReverseMap =
        FileNames.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<string>> _adjacency;

    public ZoneTopologyService()
    {
        _adjacency = BuildAdjacencyGraph();
    }

    public static string? GetZonePath(string codename) =>
        ReverseMap.TryGetValue(codename, out var path) ? path : null;

    public static string? GetCodename(string internalPath) =>
        FileNames.TryGetValue(internalPath, out var code) ? code : null;

    private static Dictionary<string, List<string>> BuildAdjacencyGraph()
    {
        var graph = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void AddEdge(string u, string v)
        {
            if (!graph.TryGetValue(u, out var listU))
            {
                listU = [];
                graph[u] = listU;
            }
            if (!listU.Contains(v, StringComparer.OrdinalIgnoreCase))
            {
                listU.Add(v);
            }

            if (!graph.TryGetValue(v, out var listV))
            {
                listV = [];
                graph[v] = listV;
            }
            if (!listV.Contains(u, StringComparer.OrdinalIgnoreCase))
            {
                listV.Add(u);
            }
        }

        // Commons hub connections
        AddEdge("COMMONS", "RAVENWOOD");
        AddEdge("COMMONS", "UNICORN_WAY");
        AddEdge("COMMONS", "SHOPPING_DISTRICT");
        AddEdge("COMMONS", "GOLEM_COURT");
        AddEdge("COMMONS", "PET_PAVILION");
        AddEdge("COMMONS", "LIBRARY");
        AddEdge("COMMONS", "CASTLE_TOURS");
        AddEdge("COMMONS", "HEADMASTERS_HOUSE");
        AddEdge("COMMONS", "HEADMASTERS_TOWER");
        AddEdge("COMMONS", "NIGHTSIDE");

        // Ravenwood
        string[] schools = ["FIRE", "ICE", "STORM", "LIFE", "MYTH"];
        foreach (var school in schools)
        {
            AddEdge("RAVENWOOD", $"SCHOOL_OF_{school}");
            AddEdge("RAVENWOOD", $"{school}_TOWER");
        }
        AddEdge("RAVENWOOD", "WORLD_TREE");

        // Nightside
        AddEdge("NIGHTSIDE", "SCHOOL_OF_DEATH");
        AddEdge("NIGHTSIDE", "DEATH_TOWER");
        AddEdge("NIGHTSIDE", "DARKMOOR");

        // Unicorn Way
        AddEdge("UNICORN_WAY", "ARENA");
        AddEdge("UNICORN_WAY", "HEDGE_MAZE");

        // Pet Pavilion
        AddEdge("PET_PAVILION", "HATCHERY");

        // Golem Court
        AddEdge("GOLEM_COURT", "DRAINS");
        AddEdge("DRAINS", "CATACOMBS");
        AddEdge("CATACOMBS", "LOWER_CATACOMBS");

        // Shopping District
        AddEdge("SHOPPING_DISTRICT", "COLOSSUS_BOULEVARD");
        AddEdge("SHOPPING_DISTRICT", "OLDE_TOWN");
        AddEdge("COLOSSUS_BOULEVARD", "MILDREDS_HOUSE");

        string[] shops =
        [
            "MIRROR_SHOP", "DYE_SHOP", "HAT_SHOP", "PET_SHOP", "BOOT_SHOP",
            "DECK_SHOP", "HOUSING_SHOP", "RING_SHOP", "ATHAME_SHOP", "JEWEL_SHOP",
            "ROBE_SHOP", "WAND_SHOP", "AMULET_SHOP"
        ];
        foreach (var shop in shops)
        {
            AddEdge("SHOPPING_DISTRICT", shop);
        }

        // Olde Town
        AddEdge("OLDE_TOWN", "BAZAAR");
        AddEdge("OLDE_TOWN", "CYCLOPS_LANE");
        AddEdge("OLDE_TOWN", "TRITON_AVENUE");
        AddEdge("OLDE_TOWN", "FIRECAT_ALLEY");

        AddEdge("CYCLOPS_LANE", "DARK_CAVE");
        AddEdge("CYCLOPS_LANE", "AQUILA");
        AddEdge("TRITON_AVENUE", "HAUNTED_CAVE");
        AddEdge("FIRECAT_ALLEY", "GRETTAS_HOUSE");

        return graph;
    }

    /// <summary>
    /// Computes the shortest path of zones to travel through to reach the destination zone.
    /// </summary>
    public IReadOnlyList<string> FindShortestZonePath(string start, string goal)
    {
        if (string.Equals(start, goal, StringComparison.OrdinalIgnoreCase))
        {
            return [start.ToUpperInvariant()];
        }

        if (!_adjacency.ContainsKey(start) || !_adjacency.ContainsKey(goal))
        {
            return [];
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };
        var queue = new Queue<List<string>>();
        queue.Enqueue([start.ToUpperInvariant()]);

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var node = path[^1];

            if (!_adjacency.TryGetValue(node, out var neighbors))
            {
                continue;
            }

            foreach (var neighbor in neighbors)
            {
                if (string.Equals(neighbor, goal, StringComparison.OrdinalIgnoreCase))
                {
                    var result = new List<string>(path) { neighbor.ToUpperInvariant() };
                    return result;
                }

                if (visited.Add(neighbor))
                {
                    var newPath = new List<string>(path) { neighbor.ToUpperInvariant() };
                    queue.Enqueue(newPath);
                }
            }
        }

        return [];
    }
}
