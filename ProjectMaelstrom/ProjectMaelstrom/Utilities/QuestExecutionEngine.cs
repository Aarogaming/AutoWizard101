using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Resolution output of a quest step evaluation.
/// </summary>
internal sealed record QuestExecutionResolution(
    W101QuestIntent Intent,
    string TargetZoneCode,
    bool IsInTargetZone,
    IReadOnlyList<string> ZonePath,
    string NextTransitZone,
    string ImmediateAction,
    CardExecutionMode RecommendedMode,
    string Summary);

/// <summary>
/// Versioned Modular Quest Execution Engine.
/// Bridges tracked quest intent (W101QuestIntent) with world zone topology (ZoneTopologyService)
/// to determine immediate transit directions, dialogue interactions, or combat engagements.
/// </summary>
internal static class QuestExecutionEngine
{
    private static readonly Dictionary<string, string> AreaToZoneMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Commons", "COMMONS" },
        { "The Commons", "COMMONS" },
        { "Ravenwood", "RAVENWOOD" },
        { "Unicorn Way", "UNICORN_WAY" },
        { "Olde Town", "OLDE_TOWN" },
        { "Shopping District", "SHOPPING_DISTRICT" },
        { "Golem Court", "GOLEM_COURT" },
        { "Pet Pavilion", "PET_PAVILION" },
        { "Colossus Boulevard", "COLOSSUS_BOULEVARD" },
        { "Nightside", "NIGHTSIDE" },
        { "Bazaar", "BAZAAR" },
        { "Hedge Maze", "HEDGE_MAZE" },
        { "Library", "LIBRARY" },
        { "Arena", "ARENA" },
        { "Drains", "DRAINS" },
        { "Catacombs", "CATACOMBS" }
    };

    /// <summary>
    /// Normalizes natural language quest area names to standardized zone topology codenames.
    /// </summary>
    public static string NormalizeAreaToZoneCode(string areaName)
    {
        if (string.IsNullOrWhiteSpace(areaName))
        {
            return "UNKNOWN";
        }

        var trimmed = areaName.Trim();
        if (AreaToZoneMap.TryGetValue(trimmed, out var code))
        {
            return code;
        }

        // Fallback: uppercase with underscores
        return trimmed.ToUpperInvariant().Replace(' ', '_');
    }

    /// <summary>
    /// Resolves the next executable quest step based on the player's current zone and tracked objective.
    /// </summary>
    public static QuestExecutionResolution ResolveQuestStep(
        string currentZoneCode,
        W101QuestIntent intent,
        ZoneTopologyService? topology = null)
    {
        ArgumentNullException.ThrowIfNull(currentZoneCode);
        ArgumentNullException.ThrowIfNull(intent);

        var topo = topology ?? new ZoneTopologyService();
        var targetZone = NormalizeAreaToZoneCode(intent.Area ?? "COMMONS");

        bool isInTarget = string.Equals(currentZoneCode, targetZone, StringComparison.OrdinalIgnoreCase);

        // Case 1: Wizard is not in the objective zone -> Transit Path required
        if (!isInTarget)
        {
            var path = topo.FindShortestZonePath(currentZoneCode, targetZone);
            var nextTransit = path.Count > 1 ? path[1] : targetZone;

            return new QuestExecutionResolution(
                Intent: intent,
                TargetZoneCode: targetZone,
                IsInTargetZone: false,
                ZonePath: path,
                NextTransitZone: nextTransit,
                ImmediateAction: $"TransitToZone:{nextTransit}",
                RecommendedMode: CardExecutionMode.ContainedLoop,
                Summary: $"Navigating world transit from {currentZoneCode} to {targetZone}. Next portal/door: {nextTransit}.");
        }

        // Case 2: Wizard is in the destination zone -> Execute the specific action verb
        switch (intent.Action)
        {
            case W101QuestIntentAction.Talk:
                return new QuestExecutionResolution(
                    Intent: intent,
                    TargetZoneCode: targetZone,
                    IsInTargetZone: true,
                    ZonePath: [currentZoneCode],
                    NextTransitZone: currentZoneCode,
                    ImmediateAction: $"ApproachAndTalk:{intent.Target}",
                    RecommendedMode: CardExecutionMode.ContainedLoop,
                    Summary: $"In target zone {targetZone}. Approaching NPC '{intent.Target}' to engage in dialogue.");

            case W101QuestIntentAction.Defeat:
            case W101QuestIntentAction.DefeatAndCollect:
                string enemyName = intent.Enemy ?? intent.Target ?? "Enemy";
                string progress = intent.RequiredCount.HasValue
                    ? $" ({intent.CurrentCount ?? 0}/{intent.RequiredCount})"
                    : "";

                return new QuestExecutionResolution(
                    Intent: intent,
                    TargetZoneCode: targetZone,
                    IsInTargetZone: true,
                    ZonePath: [currentZoneCode],
                    NextTransitZone: currentZoneCode,
                    ImmediateAction: $"SeekAndDefeat:{enemyName}",
                    RecommendedMode: CardExecutionMode.ContainedLoop,
                    Summary: $"In target zone {targetZone}. Seeking mob '{enemyName}' for battle{progress}.");

            case W101QuestIntentAction.Collect:
                return new QuestExecutionResolution(
                    Intent: intent,
                    TargetZoneCode: targetZone,
                    IsInTargetZone: true,
                    ZonePath: [currentZoneCode],
                    NextTransitZone: currentZoneCode,
                    ImmediateAction: $"CollectItem:{intent.Item ?? intent.Target}",
                    RecommendedMode: CardExecutionMode.OneShot,
                    Summary: $"In target zone {targetZone}. Intercepting and collecting quest node '{intent.Item ?? intent.Target}'.");

            case W101QuestIntentAction.Destination:
            case W101QuestIntentAction.Explore:
            default:
                return new QuestExecutionResolution(
                    Intent: intent,
                    TargetZoneCode: targetZone,
                    IsInTargetZone: true,
                    ZonePath: [currentZoneCode],
                    NextTransitZone: currentZoneCode,
                    ImmediateAction: $"ExploreLandmark:{intent.Target}",
                    RecommendedMode: CardExecutionMode.ContainedLoop,
                    Summary: $"In target zone {targetZone}. Reaching destination landmark '{intent.Target}'.");
        }
    }
}
