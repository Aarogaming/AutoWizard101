using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Represents a combat buff, ward, or charm modifier on a wizard or creature.
/// </summary>
public record CombatModifier(
    string Name,
    string School,
    double Percent,
    string Kind); // "Blade", "Shield", "Trap", "Weakness"

/// <summary>
/// Modern Wizard101 Combat & Damage Calculator.
/// Ingests and enhances KursovaLibrary with modern game mechanics:
/// - Archmastery School Pips & Shadow Pips
/// - Dynamic DPP (Damage Per Pip) scaling (Tempest, Judgement, Heckhound)
/// - DoT (Damage over Time) tick calculations (Fire Dragon, Deer Knight)
/// - Multiplicative Blade & Trap stacking with distinct source naming
/// - Pierce calculation: piercing through shields before penetrating base resist
/// - Modern Critical Rating scaling (1.25x - 2.0x vs Block)
/// - Stat soft caps (diminishing returns above 150% damage)
/// </summary>
public static class ModernCombatCalculator
{
    public const double DamageSoftCapThreshold = 150.0;

    /// <summary>
    /// Calculates effective gear damage percent after applying modern diminishing returns above 150%.
    /// </summary>
    public static double ApplyDamageSoftCap(double rawDamagePercent)
    {
        if (rawDamagePercent <= DamageSoftCapThreshold)
        {
            return rawDamagePercent;
        }

        // KingsIsle diminishing returns formula: asymptotic soft cap
        double excess = rawDamagePercent - DamageSoftCapThreshold;
        double damped = (excess * 100.0) / (excess + 100.0);
        return DamageSoftCapThreshold + damped;
    }

    /// <summary>
    /// Checks whether a combatant has sufficient pips, school pips, and shadow pips to cast a spell.
    /// </summary>
    public static bool CanCastSpell(CombatantState caster, CombatCardState spell)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(spell);

        if (!spell.IsCastable) return false;

        // Shadow Pip prerequisite
        if (spell.ShadowPipCost > 0 && caster.ShadowPips < spell.ShadowPipCost)
        {
            return false;
        }

        // Archmastery School Pip prerequisite
        if (spell.SchoolPipCost > 0 && caster.SchoolPips < spell.SchoolPipCost)
        {
            return false;
        }

        // Total effective pips available:
        // Normal Pips = 1 each
        // Power Pips = 2 each (if matching school or universal)
        // School Pips = 2 each (matching school pips act as power pips)
        int effectivePips = caster.NormalPips + (caster.PowerPips * 2) + (caster.SchoolPips * 2);

        // DPP spells can be cast with at least 1 pip or as long as they meet shadow/school pip requirements
        if (spell.IsDpp)
        {
            return effectivePips >= Math.Max(1, spell.PipCost);
        }

        return effectivePips >= spell.PipCost;
    }

    /// <summary>
    /// Calculates the full damage breakdown for a spell cast from caster to target.
    /// Implements KursovaLibrary's CheckPierce, CheckStats, and blade/trap multiplication.
    /// </summary>
    public static (double InitialHit, double TotalDotDamage, double FinalTotalDamage, bool IsLethal) CalculateDetailedDamage(
        CombatantState caster,
        CombatantState target,
        CombatCardState spell,
        CombatCardState? enchant = null,
        bool isCritical = false)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(spell);

        int totalPips = caster.NormalPips + (caster.PowerPips * 2) + (caster.SchoolPips * 2);
        double enchantBonus = enchant?.BaseDamage ?? 0.0;

        // 1. Base Damage Calculation (Handling DPP, Flat, or Range)
        double baseHit = 0.0;
        double baseDot = 0.0;

        if (spell.IsDpp)
        {
            int spentPips = Math.Max(1, Math.Min(totalPips, 14));
            baseHit = (spell.DppMultiplier + (enchantBonus / Math.Max(1, spentPips))) * spentPips;
        }
        else
        {
            baseHit = spell.BaseDamage + (spell.BaseDamage > 0 ? enchantBonus : 0.0);
            baseDot = spell.DoTDamage + (spell.BaseDamage == 0 && spell.DoTDamage > 0 ? enchantBonus : 0.0);
        }

        // 2. Caster Gear Damage & Soft Cap
        double effectiveGearDamage = ApplyDamageSoftCap(caster.GearDamageBonus);
        double gearMultiplier = 1.0 + (effectiveGearDamage / 100.0);

        baseHit *= gearMultiplier;
        baseDot *= gearMultiplier;

        // 3. Multiplicative Blades
        if (caster.ActiveBlades != null && caster.ActiveBlades.Count > 0)
        {
            foreach (var blade in caster.ActiveBlades)
            {
                double mult = 1.0 + (blade.Percent / 100.0);
                baseHit *= mult;
                baseDot *= mult;
            }
        }

        // 4. Multiplicative Weaknesses on Caster
        if (caster.ActiveWeaknesses != null && caster.ActiveWeaknesses.Count > 0)
        {
            foreach (var weakness in caster.ActiveWeaknesses)
            {
                double mult = Math.Max(0.0, 1.0 - (weakness.Percent / 100.0));
                baseHit *= mult;
                baseDot *= mult;
            }
        }

        // 5. Critical Multiplier
        if (isCritical)
        {
            double critMult = 1.25;
            if (caster.CriticalRating + target.BlockRating > 0)
            {
                critMult = 1.25 + (0.75 * (caster.CriticalRating / (caster.CriticalRating + target.BlockRating)));
            }
            baseHit *= critMult;
            baseDot *= critMult;
        }

        // 6. Multiplicative Traps on Target
        if (target.ActiveTraps != null && target.ActiveTraps.Count > 0)
        {
            foreach (var trap in target.ActiveTraps)
            {
                double mult = 1.0 + (trap.Percent / 100.0);
                baseHit *= mult;
                baseDot *= mult;
            }
        }

        // 7. Pierce vs Shields & Resistance (KursovaLibrary CheckPierce)
        double totalPierce = caster.PiercePercent + spell.PiercePercent;
        double targetResist = target.ResistPercent;

        // Apply pierce to shields first
        double remainingShieldPercent = 0.0;
        if (target.ActiveShields != null && target.ActiveShields.Count > 0)
        {
            foreach (var shield in target.ActiveShields)
            {
                double shieldVal = Math.Abs(shield.Percent);
                if (totalPierce >= shieldVal)
                {
                    totalPierce -= shieldVal; // Shield fully broken by pierce
                }
                else
                {
                    remainingShieldPercent += (shieldVal - totalPierce);
                    totalPierce = 0.0;
                }
            }
        }

        // Remaining pierce penetrates base resistance
        double effectiveResist = Math.Max(0.0, targetResist - totalPierce);
        double totalReduction = Math.Min(100.0, remainingShieldPercent + effectiveResist);
        double mitigationMultiplier = Math.Max(0.0, 1.0 - (totalReduction / 100.0));

        baseHit = (baseHit * mitigationMultiplier) + caster.FlatDamageBonus - target.FlatResist;
        baseDot = (baseDot * mitigationMultiplier) + caster.FlatDamageBonus - target.FlatResist;

        double finalInitialHit = Math.Max(0.0, Math.Round(baseHit, MidpointRounding.AwayFromZero));
        double finalDot = Math.Max(0.0, Math.Round(baseDot, MidpointRounding.AwayFromZero));
        double totalFinalDamage = finalInitialHit + finalDot;
        bool isLethal = totalFinalDamage >= target.CurrentHp;

        return (finalInitialHit, finalDot, totalFinalDamage, isLethal);
    }
}
