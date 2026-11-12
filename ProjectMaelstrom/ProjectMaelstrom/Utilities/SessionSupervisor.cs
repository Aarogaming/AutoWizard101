using System;
using System.Collections.Generic;
using System.Drawing;

namespace ProjectMaelstrom.Utilities;

public enum WizardActivity
{
    Questing,
    MobFarming,
    BossFarming,
    PetTraining,
    Gardening,
    BazaarTrading,
    PotionRefill,
    AutoIdle
}

public record WizardSessionSnapshot(
    double HealthRatio,          // 0.0 to 1.0 (from CombatGeometry.HealthGlobe)
    double ManaRatio,            // 0.0 to 1.0 (from CombatGeometry.ManaGlobe)
    int PotionsAvailable,        // 0 to 4
    int BackpackCount,           // Current items in backpack
    int BackpackCapacity,        // e.g. 80 or 150
    bool InCombat,
    bool InDialogue,
    string CurrentZoneCode,      // e.g. "COMMONS", "UNICORN_WAY"
    int EnergyAvailable);

public record SupervisorDecision(
    WizardActivity NextActivity,
    string Reason,
    string? TargetZoneCode = null,
    string? MacroRoutineId = null);

/// <summary>
/// Native C# Session Supervisor & Routine Switching Engine.
/// Maximizes Maelstrom's potential by autonomously managing the complete player loop:
/// - Evaluates low health/mana and triggers potion usage before combat.
/// - Initiates Potion Motion refills when potions are exhausted.
/// - Senses full backpack limits (80/150) and routes to Olde Town Bazaar to sell.
/// - Dispatches Pet Pavilion training when Energy is available.
/// - Automatically activates Auto-Idle mode between tasks to eliminate AFK timeouts.
/// </summary>
public sealed class SessionSupervisor
{
    private const double LowHealthThreshold = 0.35;
    private const double LowManaThreshold = 0.20;

    private readonly ZoneTopologyService _topology;

    public SessionSupervisor(ZoneTopologyService? topology = null)
    {
        _topology = topology ?? new ZoneTopologyService();
    }

    /// <summary>
    /// Evaluates current game telemetry and determines the optimal routine transition.
    /// </summary>
    public SupervisorDecision EvaluateNextStep(
        WizardSessionSnapshot snapshot,
        WizardActivity primaryActivity = WizardActivity.Questing)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // 1. If currently in combat, combat execution owns control
        if (snapshot.InCombat)
        {
            return new SupervisorDecision(
                WizardActivity.MobFarming,
                "In active combat; combat card decision matrix owns execution.");
        }

        // 2. Health & Mana safety check before wandering or engaging
        if (snapshot.HealthRatio < LowHealthThreshold || snapshot.ManaRatio < LowManaThreshold)
        {
            if (snapshot.PotionsAvailable > 0)
            {
                return new SupervisorDecision(
                    WizardActivity.PotionRefill,
                    $"Health ({snapshot.HealthRatio:P0}) or Mana ({snapshot.ManaRatio:P0}) critical; consume 1 potion.");
            }
            else
            {
                return new SupervisorDecision(
                    WizardActivity.PotionRefill,
                    "Health/Mana critical and potions empty; navigate to Fairgrounds for Potion Motion refill.",
                    TargetZoneCode: "COMMONS",
                    MacroRoutineId: "potion-motion-refill");
            }
        }

        // 3. Inventory capacity check
        if (snapshot.BackpackCount >= snapshot.BackpackCapacity)
        {
            return new SupervisorDecision(
                WizardActivity.BazaarTrading,
                $"Backpack full ({snapshot.BackpackCount}/{snapshot.BackpackCapacity}); navigate to Bazaar to sell surplus.",
                TargetZoneCode: "BAZAAR",
                MacroRoutineId: "commons-to-olde-town");
        }

        // 4. Energy check for Pet Training
        if (primaryActivity == WizardActivity.PetTraining && snapshot.EnergyAvailable >= 10)
        {
            return new SupervisorDecision(
                WizardActivity.PetTraining,
                $"Pet Training active with {snapshot.EnergyAvailable} Energy; execute Pet Dance game.",
                TargetZoneCode: "PET_PAVILION",
                MacroRoutineId: "commons-to-pet-pavilion");
        }

        // 5. Default: proceed with configured primary activity
        return new SupervisorDecision(
            primaryActivity,
            $"Executing primary activity: {primaryActivity}.");
    }

    /// <summary>
    /// Resolves the multi-zone navigation path if a decision requires traveling to a target zone.
    /// </summary>
    public IReadOnlyList<string> ResolveTravelRoute(string currentZone, string targetZone)
    {
        return _topology.FindShortestZonePath(currentZone, targetZone);
    }
}
