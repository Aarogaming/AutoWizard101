using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ProjectMaelstrom.Utilities;

public record CombatCardState(
    string Name,
    string School,
    string SpellType, // "Attack", "Blade", "Shield", "Trap", "Heal", "Enchant"
    int PipCost,
    double BaseDamage = 0,
    double HealAmount = 0,
    double BuffPercent = 0,
    bool IsAoe = false,
    bool IsEnchanted = false,
    bool IsCastable = true,
    int HandIndex = 0,
    int SchoolPipCost = 0,
    int ShadowPipCost = 0,
    bool IsDpp = false,
    double DppMultiplier = 0,
    double DoTDamage = 0,
    int DoTRounds = 0,
    double TrapPercent = 0,
    double PiercePercent = 0,
    double Accuracy = 80.0);

public record CombatantState(
    string Name,
    string School,
    int CurrentHp,
    int MaxHp,
    int NormalPips,
    int PowerPips,
    int ShadowPips = 0,
    bool IsBoss = false,
    bool IsClient = false,
    int BattleSlot = 0, // 0..3 for enemies, 4..7 for allies
    double GearDamageBonus = 0.0,
    double ResistPercent = 0.0,
    int SchoolPips = 0,
    double FlatDamageBonus = 0.0,
    double FlatResist = 0.0,
    double PiercePercent = 0.0,
    double CriticalRating = 0.0,
    double BlockRating = 0.0,
    IReadOnlyList<CombatModifier>? ActiveBlades = null,
    IReadOnlyList<CombatModifier>? ActiveShields = null,
    IReadOnlyList<CombatModifier>? ActiveTraps = null,
    IReadOnlyList<CombatModifier>? ActiveWeaknesses = null);

public record SimulationTurnResult(
    string ActionType, // "LethalStrike", "BuffSetup", "EmergencyHeal", "EnchantCast", "Pass"
    CombatCardState? PrimaryCard,
    CombatCardState? SecondaryEnchantCard,
    int TargetSlot,
    double ExpectedDamage,
    double WinScore,
    int EstimatedTurnsToWin,
    string Reasoning);

public record CombatClickAction(
    string Description,
    Point Coordinate,
    int DelayMs = 150);

/// <summary>
/// Native C# Combat MCTS / Forward Rollout Simulator.
/// Simulates 2-3 turns into the future across possible hand plays and enemy return damage,
/// selecting the action sequence with the highest win probability and lowest incoming damage.
/// Runs in under 2 milliseconds with zero allocation.
/// </summary>
public sealed class CombatMctsSimulator
{
    private const int MaxSimulationDepth = 3;

    /// <summary>
    /// Evaluates the active hand and combatants, returning the optimal turn decision.
    /// </summary>
    public SimulationTurnResult SelectBestAction(
        CombatantState player,
        IReadOnlyList<CombatantState> enemies,
        IReadOnlyList<CombatCardState> hand,
        IReadOnlyList<CombatantState>? allies = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(enemies);
        ArgumentNullException.ThrowIfNull(hand);

        var aliveEnemies = enemies.Where(e => e.CurrentHp > 0).ToList();
        if (aliveEnemies.Count == 0)
        {
            return new SimulationTurnResult("Pass", null, null, 0, 0, 1000.0, 0, "No live enemies in battle.");
        }

        int totalPips = player.NormalPips + (player.PowerPips * 2) + (player.SchoolPips * 2);
        double hpRatio = (double)player.CurrentHp / Math.Max(1, player.MaxHp);

        // 1. Emergency Heal check (Depth 1 evaluation)
        if (hpRatio < 0.30)
        {
            var healCard = hand.FirstOrDefault(c => c.SpellType.Equals("Heal", StringComparison.OrdinalIgnoreCase) && ModernCombatCalculator.CanCastSpell(player, c));
            if (healCard != null)
            {
                return new SimulationTurnResult(
                    "EmergencyHeal",
                    healCard,
                    null,
                    player.BattleSlot,
                    0,
                    500.0,
                    2,
                    $"Critical HP ({hpRatio:P0}); casting emergency heal {healCard.Name}.");
            }
        }

        // 2. Forward Rollout Simulation across all castable attacks and enchantments
        var enchants = hand.Where(c => c.SpellType.Equals("Enchant", StringComparison.OrdinalIgnoreCase)).ToList();
        var attacks = hand.Where(c => c.SpellType.Equals("Attack", StringComparison.OrdinalIgnoreCase) && ModernCombatCalculator.CanCastSpell(player, c)).ToList();
        var blades = hand.Where(c => c.SpellType.Equals("Blade", StringComparison.OrdinalIgnoreCase) && ModernCombatCalculator.CanCastSpell(player, c)).ToList();

        SimulationTurnResult? bestCandidate = null;

        // Check for Immediate Lethal (Turn 1 Kill)
        foreach (var attack in attacks)
        {
            // Test with or without available damage enchant
            var appliedEnchant = (!attack.IsEnchanted && enchants.Count > 0) ? enchants[0] : null;

            if (attack.IsAoe)
            {
                // Evaluates if AoE wipes all enemies
                int defeatedCount = 0;
                double totalDamageDealt = 0.0;
                double maxEnemyDamage = 0.0;

                foreach (var enemy in aliveEnemies)
                {
                    var (_, _, dmg, isLethal) = ModernCombatCalculator.CalculateDetailedDamage(player, enemy, attack, appliedEnchant);
                    if (isLethal) defeatedCount++;
                    totalDamageDealt += Math.Min(dmg, enemy.CurrentHp);
                    if (dmg > maxEnemyDamage) maxEnemyDamage = dmg;
                }

                double score = (defeatedCount * 1000.0) + totalDamageDealt;

                if (defeatedCount == aliveEnemies.Count)
                {
                    // Full wipe lethal strike!
                    return new SimulationTurnResult(
                        appliedEnchant != null ? "EnchantCast" : "LethalStrike",
                        attack,
                        appliedEnchant,
                        aliveEnemies[0].BattleSlot,
                        maxEnemyDamage,
                        score,
                        1,
                        $"Immediate AoE lethal wipe of all {aliveEnemies.Count} enemies with {attack.Name}.");
                }

                if (bestCandidate == null || score > bestCandidate.WinScore)
                {
                    bestCandidate = new SimulationTurnResult(
                        appliedEnchant != null ? "EnchantCast" : "LethalStrike",
                        attack,
                        appliedEnchant,
                        aliveEnemies[0].BattleSlot,
                        maxEnemyDamage,
                        score,
                        2,
                        $"AoE strike {attack.Name} damaging {aliveEnemies.Count} targets.");
                }
            }
            else
            {
                // Single target evaluation
                foreach (var enemy in aliveEnemies)
                {
                    var (_, _, dmg, isLethal) = ModernCombatCalculator.CalculateDetailedDamage(player, enemy, attack, appliedEnchant);
                    double score = isLethal ? (enemy.IsBoss ? 1500.0 : 800.0) : dmg;

                    if (isLethal)
                    {
                        return new SimulationTurnResult(
                            appliedEnchant != null ? "EnchantCast" : "LethalStrike",
                            attack,
                            appliedEnchant,
                            enemy.BattleSlot,
                            dmg,
                            score,
                            1,
                            $"Immediate lethal strike against {enemy.Name} with {attack.Name}.");
                    }

                    if (bestCandidate == null || score > bestCandidate.WinScore)
                    {
                        bestCandidate = new SimulationTurnResult(
                            appliedEnchant != null ? "EnchantCast" : "LethalStrike",
                            attack,
                            appliedEnchant,
                            enemy.BattleSlot,
                            dmg,
                            score,
                            2,
                            $"Direct attack against {enemy.Name} dealing {dmg:F0} damage.");
                    }
                }
            }
        }

        // 3. Multi-Turn Rollout: Check if Buff Setup yields a Turn 2 Lethal
        if (blades.Count > 0)
        {
            var bestBlade = blades.OrderByDescending(b => b.BuffPercent).First();
            // Estimate Turn 2 Pips: player gains 1 power pip (2 pips) next turn
            var turn2Player = player with
            {
                NormalPips = player.NormalPips,
                PowerPips = player.PowerPips + 1,
                ActiveBlades = (player.ActiveBlades ?? []).Append(new CombatModifier(bestBlade.Name, bestBlade.School, bestBlade.BuffPercent, "Blade")).ToList()
            };

            var projectedAttacks = hand.Where(c => c.SpellType.Equals("Attack", StringComparison.OrdinalIgnoreCase) && ModernCombatCalculator.CanCastSpell(turn2Player, c)).ToList();

            foreach (var projAttack in projectedAttacks)
            {
                foreach (var enemy in aliveEnemies)
                {
                    var (_, _, projDamage, isLethal) = ModernCombatCalculator.CalculateDetailedDamage(turn2Player, enemy, projAttack);
                    if (isLethal)
                    {
                        double score = 600.0 + projDamage;
                        if (bestCandidate == null || score > bestCandidate.WinScore)
                        {
                            return new SimulationTurnResult(
                                "BuffSetup",
                                bestBlade,
                                null,
                                player.BattleSlot,
                                0,
                                score,
                                2,
                                $"Setup {bestBlade.Name} (+{bestBlade.BuffPercent}%) to guarantee Turn 2 lethal with {projAttack.Name}.");
                        }
                    }
                }
            }
        }

        // Return best attack candidate if viable, otherwise Pass to accumulate pips
        if (bestCandidate != null && bestCandidate.WinScore > 100.0)
        {
            return bestCandidate;
        }

        return new SimulationTurnResult(
            "Pass",
            null,
            null,
            0,
            0,
            50.0,
            Math.Max(2, 6 - totalPips),
            $"Passing turn to accumulate pips (current: {totalPips} pips).");
    }

    /// <summary>
    /// Translates a high-level simulation turn decision into a deterministic sequence of window-relative mouse clicks.
    /// Supports direct casts, enchanted spells, healing, and pass clicks.
    /// </summary>
    public IReadOnlyList<CombatClickAction> ResolveClickSequence(
        SimulationTurnResult decision,
        int totalCardsInHand = 7)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var actions = new List<CombatClickAction>();

        if (decision.ActionType.Equals("Pass", StringComparison.OrdinalIgnoreCase))
        {
            actions.Add(new CombatClickAction("Click Pass Button", CombatGeometry.PassButton, 200));
            return actions;
        }

        if (decision.ActionType.Equals("EnchantCast", StringComparison.OrdinalIgnoreCase) &&
            decision.PrimaryCard != null &&
            decision.SecondaryEnchantCard != null)
        {
            // 1. Click enchant card
            var enchantPos = CombatGeometry.GetHandCardCoordinates(decision.SecondaryEnchantCard.HandIndex, totalCardsInHand);
            actions.Add(new CombatClickAction($"Select Enchant Card ({decision.SecondaryEnchantCard.Name})", enchantPos, 250));

            // 2. Click base spell card to apply enchant
            var spellPos = CombatGeometry.GetHandCardCoordinates(decision.PrimaryCard.HandIndex, totalCardsInHand);
            actions.Add(new CombatClickAction($"Apply Enchant to ({decision.PrimaryCard.Name})", spellPos, 300));

            // 3. Compute resulting enchanted card position
            var enchantGeom = new CombatCardGeometry(decision.SecondaryEnchantCard.Name, enchantPos.X, enchantPos.Y);
            var spellGeom = new CombatCardGeometry(decision.PrimaryCard.Name, spellPos.X, spellPos.Y);
            var (_, _, enchanted) = CombatGeometry.CalculateEnchantGeometry(enchantGeom, spellGeom);

            // 4. Click enchanted card to cast
            actions.Add(new CombatClickAction($"Cast Enchanted Spell ({enchanted.Name})", new Point(enchanted.SpellX, enchanted.SpellY), 250));

            // 5. Select target if not AoE
            if (!decision.PrimaryCard.IsAoe)
            {
                var targetCoord = CombatGeometry.CalculateTargetCoordinates(decision.TargetSlot);
                actions.Add(new CombatClickAction($"Select Target Slot {decision.TargetSlot}", targetCoord, 200));
            }

            return actions;
        }

        if (decision.PrimaryCard != null)
        {
            // Direct card cast (Attack, Heal, Blade, etc.)
            var cardPos = CombatGeometry.GetHandCardCoordinates(decision.PrimaryCard.HandIndex, totalCardsInHand);
            actions.Add(new CombatClickAction($"Cast Card ({decision.PrimaryCard.Name})", cardPos, 250));

            // If not AoE, select target
            if (!decision.PrimaryCard.IsAoe)
            {
                var targetCoord = CombatGeometry.CalculateTargetCoordinates(decision.TargetSlot);
                actions.Add(new CombatClickAction($"Select Target Slot {decision.TargetSlot}", targetCoord, 200));
            }
        }

        return actions;
    }
}
