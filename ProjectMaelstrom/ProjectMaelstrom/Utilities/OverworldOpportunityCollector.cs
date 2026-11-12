using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Type of overworld collectable opportunity.
/// </summary>
public enum OverworldOpportunityKind
{
    Reagent,
    WoodenChest,
    SilverChest
}

/// <summary>
/// An observable overworld interactable entity detected by vision or memory reading.
/// </summary>
public record OverworldEntity(
    string Id,
    string Name,
    OverworldOpportunityKind Kind,
    double X,
    double Y,
    double Z = 0.0,
    bool IsInteractPromptVisible = false);

/// <summary>
/// A roaming mob threat in the overworld that can pull the wizard into a duel circle.
/// </summary>
public record RoamingMobThreat(
    string Id,
    string Name,
    double X,
    double Y,
    double ThreatRadius = 350.0); // Standard Wizard101 mob combat pull radius

/// <summary>
/// Trajectory plan to divert from main route, collect an item, and resume.
/// </summary>
public record OpportunityCollectionPlan(
    bool ShouldIntercept,
    OverworldEntity? TargetEntity,
    double ApproachDistance,
    string Reason,
    IReadOnlyList<string> PlannedSteps);

/// <summary>
/// Overworld Reagent and Treasure Chest Opportunistic Collector.
/// Automatically detects nearby spawn nodes while walking/questing, performs safety threat analysis
/// (preventing mob combat pulls), diverts navigation to collect via 'X', and restores the original path.
/// Zero Energy cost; operates continuously during all overworld movement.
/// </summary>
public sealed class OverworldOpportunityCollector
{
    public double MaxDeviationRadius { get; set; } = 500.0; // Maximum allowed distance to deviate from route
    public double InteractionRange { get; set; } = 120.0; // Distance at which 'X' prompt becomes active
    public bool EnableSilverChests { get; set; } = false; // Whether to attempt puzzle minigame chests
    public bool EnableWoodenChests { get; set; } = true;
    public bool EnableReagents { get; set; } = true;

    /// <summary>
    /// Evaluates observable entities around the player and selects the safest, highest-value opportunity.
    /// </summary>
    public OpportunityCollectionPlan EvaluateOpportunity(
        double playerX,
        double playerY,
        int currentBackpackCount,
        int backpackCapacity,
        IReadOnlyList<OverworldEntity> nearbyEntities,
        IReadOnlyList<RoamingMobThreat> nearbyMobs)
    {
        ArgumentNullException.ThrowIfNull(nearbyEntities);
        ArgumentNullException.ThrowIfNull(nearbyMobs);

        if (nearbyEntities.Count == 0)
        {
            return new OpportunityCollectionPlan(false, null, 0, "No overworld entities in sensory range.", []);
        }

        // Filter enabled categories
        var eligibleEntities = nearbyEntities.Where(e => e.Kind switch
        {
            OverworldOpportunityKind.Reagent => EnableReagents,
            OverworldOpportunityKind.WoodenChest => EnableWoodenChests && currentBackpackCount < backpackCapacity,
            OverworldOpportunityKind.SilverChest => EnableSilverChests && currentBackpackCount < backpackCapacity,
            _ => false
        }).ToList();

        if (eligibleEntities.Count == 0)
        {
            return new OpportunityCollectionPlan(false, null, 0, "No eligible entities enabled or backpack full for chests.", []);
        }

        // Evaluate distance and mob threat for each candidate
        OverworldEntity? bestTarget = null;
        double bestDistance = double.MaxValue;
        string? rejectReason = null;

        foreach (var entity in eligibleEntities)
        {
            double dist = Math.Sqrt(Math.Pow(entity.X - playerX, 2) + Math.Pow(entity.Y - playerY, 2));

            // Check max deviation radius
            if (dist > MaxDeviationRadius)
            {
                continue;
            }

            // SAFETY CHECK: Check if entity is inside any roaming mob's threat pull radius
            bool mobThreatPresent = false;
            foreach (var mob in nearbyMobs)
            {
                double distToMob = Math.Sqrt(Math.Pow(entity.X - mob.X, 2) + Math.Pow(entity.Y - mob.Y, 2));
                if (distToMob < mob.ThreatRadius)
                {
                    mobThreatPresent = true;
                    rejectReason = $"Entity {entity.Name} is inside {mob.Name}'s combat threat radius ({distToMob:F0} < {mob.ThreatRadius:F0}).";
                    break;
                }

                // Also check if line-of-approach passes too close to the mob
                double distPlayerToMob = Math.Sqrt(Math.Pow(playerX - mob.X, 2) + Math.Pow(playerY - mob.Y, 2));
                if (distPlayerToMob < mob.ThreatRadius * 0.8)
                {
                    mobThreatPresent = true;
                    rejectReason = $"Approach vector passes too close to {mob.Name}.";
                    break;
                }
            }

            if (mobThreatPresent)
            {
                continue;
            }

            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestTarget = entity;
            }
        }

        if (bestTarget == null)
        {
            return new OpportunityCollectionPlan(
                false,
                null,
                0,
                rejectReason ?? "No candidate entities within safe deviation distance.",
                []);
        }

        var steps = new List<string>
        {
            $"Pause main waypoint navigation at ({playerX:F0}, {playerY:F0})",
            $"Steer towards {bestTarget.Kind} '{bestTarget.Name}' at ({bestTarget.X:F0}, {bestTarget.Y:F0}) - Distance: {bestDistance:F0}",
            "Pulse 'X' key for 120ms to collect",
            "Verify acquisition animation receipt (500ms)",
            $"Resume original path towards previous waypoint"
        };

        return new OpportunityCollectionPlan(
            true,
            bestTarget,
            bestDistance,
            $"Safely intercepting {bestTarget.Kind} '{bestTarget.Name}' at {bestDistance:F0} units without triggering mob threat.",
            steps);
    }
}
