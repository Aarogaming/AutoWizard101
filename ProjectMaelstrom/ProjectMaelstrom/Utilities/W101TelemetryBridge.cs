using System;
using System.Collections.Generic;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Bridges telemetry snapshots (from memory observation or mock providers) into high-level combat models
/// such as CombatantState and CombatCardState used by CombatMctsSimulator and ActiveCombatCard.
/// </summary>
public static class W101TelemetryBridge
{
    /// <summary>
    /// Builds a combatant model representing the player using telemetry data.
    /// </summary>
    /// <param name="telemetry">Telemetry snapshot containing player stats.</param>
    /// <param name="name">Display name for the combatant (default "Player").</param>
    /// <param name="school">Wizard school (default "Storm").</param>
    /// <param name="damageBonus">Damage bonus multiplier (default 0.50).</param>
    /// <param name="resistPercent">Base resist percent (default 0.20).</param>
    /// <param name="piercePercent">Pierce percent (default 15.0).</param>
    /// <param name="criticalRating">Critical rating (default 300.0).</param>
    /// <returns>A fully populated CombatantState for the player.</returns>
    public static CombatantState BuildPlayerCombatant(
        W101TelemetryData telemetry,
        string name = "Player",
        string school = "Storm",
        double damageBonus = 0.50,
        double resistPercent = 0.20,
        double piercePercent = 15.0,
        double criticalRating = 300.0)
    {
        int pips = telemetry.Pips ?? 0;
        int powerPips = telemetry.PowerPips ?? 0;
        int shadowPips = telemetry.ShadowPips ?? 0;
        int health = telemetry.CurrentHealth ?? 1000;
        int maxHealth = telemetry.MaxHealth ?? 1000;

        return new CombatantState(
            Name: name,
            School: school,
            CurrentHp: health,
            MaxHp: maxHealth,
            NormalPips: pips,
            PowerPips: powerPips,
            ShadowPips: shadowPips,
            IsBoss: false,
            IsClient: true,
            BattleSlot: 4,
            GearDamageBonus: damageBonus,
            ResistPercent: resistPercent,
            SchoolPips: 0,
            PiercePercent: piercePercent,
            CriticalRating: criticalRating);
    }

    /// <summary>
    /// Constructs an enemy combatant model for simulation or live combat mapping.
    /// </summary>
    /// <param name="name">Enemy name.</param>
    /// <param name="battleSlot">Slot index (0‑3) where the enemy appears.</param>
    /// <param name="currentHp">Current health points.</param>
    /// <param name="maxHp">Maximum health points.</param>
    /// <param name="school">Wizard school (default "Myth").</param>
    /// <param name="resistPercent">Base resist percent (default 0.10).</param>
    /// <param name="isBoss">Whether the enemy is a boss (default false).</param>
    /// <returns>A fully populated CombatantState for the enemy.</returns>
    public static CombatantState BuildEnemyCombatant(
        string name,
        int battleSlot,
        int currentHp,
        int maxHp,
        string school = "Myth",
        double resistPercent = 0.10,
        bool isBoss = false)
    {
        return new CombatantState(
            Name: name,
            School: school,
            CurrentHp: currentHp,
            MaxHp: maxHp,
            NormalPips: 2,
            PowerPips: 0,
            ShadowPips: 0,
            IsBoss: isBoss,
            IsClient: false,
            BattleSlot: battleSlot,
            ResistPercent: resistPercent);
    }

    /// <summary>
    /// Resolves turn action intent from current combat cards and targets using MCTS.
    /// </summary>
    /// <param name="player">The player combatant state.</param>
    /// <param name="enemies">List of enemy combatant states.</param>
    /// <param name="handCards">List of combat card states in hand.</param>
    /// <returns>The selected turn result from the MCTS simulator.</returns>
    public static SimulationTurnResult PlanTurnFromState(
        CombatantState player,
        IReadOnlyList<CombatantState> enemies,
        IReadOnlyList<CombatCardState> handCards)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(enemies);
        ArgumentNullException.ThrowIfNull(handCards);

        var simulator = new CombatMctsSimulator();
        return simulator.SelectBestAction(player, enemies, handCards);
    }
}
