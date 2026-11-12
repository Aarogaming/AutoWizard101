using System;
using System.Collections.Generic;
using ProjectMaelstrom.Utilities;
using Xunit;

namespace ProjectMaelstrom.Tests;

public class ModernCombatCalculatorTests
{
    [Fact]
    public void DamageSoftCap_DampensExcessAbove150Percent()
    {
        // Under 150% -> No dampening
        Assert.Equal(120.0, ModernCombatCalculator.ApplyDamageSoftCap(120.0));
        Assert.Equal(150.0, ModernCombatCalculator.ApplyDamageSoftCap(150.0));

        // Over 150% -> Damped smoothly
        // excess = 20: 20 * 100 / (20 + 100) = 2000 / 120 = 16.67
        double capped170 = ModernCombatCalculator.ApplyDamageSoftCap(170.0);
        Assert.True(capped170 < 170.0);
        Assert.Equal(166.67, Math.Round(capped170, 2));

        // extreme gear 250% (excess 100): 100 * 100 / 200 = 50 -> 200%
        double capped250 = ModernCombatCalculator.ApplyDamageSoftCap(250.0);
        Assert.Equal(200.0, capped250);
    }

    [Fact]
    public void CanCastSpell_EvaluatesArchmasterySchoolPipsAndShadowPips()
    {
        var player = new CombatantState(
            Name: "Novus Wizard",
            School: "Death",
            CurrentHp: 3000,
            MaxHp: 3000,
            NormalPips: 2,
            PowerPips: 2,
            ShadowPips: 0,
            SchoolPips: 0);

        // Spell requiring 1 Shadow Pip (e.g. Call of Khrulhu / Lulu)
        var shadowSpell = new CombatCardState("Call of Khrulhu", "Death", "Attack", PipCost: 4, ShadowPipCost: 1, IsAoe: true);
        Assert.False(ModernCombatCalculator.CanCastSpell(player, shadowSpell));

        // Grant Shadow Pip
        var playerWithShadow = player with { ShadowPips = 1 };
        Assert.True(ModernCombatCalculator.CanCastSpell(playerWithShadow, shadowSpell));

        // Spell requiring 1 School Pip (e.g. Archmastery Dual School spell)
        var archmasterySpell = new CombatCardState("Celestial Calamity", "Fire", "Attack", PipCost: 5, SchoolPipCost: 1);
        Assert.False(ModernCombatCalculator.CanCastSpell(playerWithShadow, archmasterySpell));

        // Grant School Pip
        var playerWithArchmastery = playerWithShadow with { SchoolPips = 1 };
        Assert.True(ModernCombatCalculator.CanCastSpell(playerWithArchmastery, archmasterySpell));
    }

    [Fact]
    public void CalculateDetailedDamage_HandlesDPP_ScalingWithPips()
    {
        // Tempest: 80 DPP
        var tempest = new CombatCardState("Tempest", "Storm", "Attack", PipCost: 1, IsDpp: true, DppMultiplier: 80, IsAoe: true);

        var stormWizard = new CombatantState(
            Name: "Storm Diviner",
            School: "Storm",
            CurrentHp: 2500,
            MaxHp: 2500,
            NormalPips: 1,
            PowerPips: 2, // Total pips = 1 + 4 = 5 pips
            GearDamageBonus: 50.0);

        var target = new CombatantState("Enemy", "Myth", CurrentHp: 1000, MaxHp: 1000, NormalPips: 0, PowerPips: 0);

        // 5 pips * 80 DPP = 400 base damage
        // 400 * 1.50 (50% gear damage) = 600 damage
        var (hit, dot, total, isLethal) = ModernCombatCalculator.CalculateDetailedDamage(stormWizard, target, tempest);

        Assert.Equal(600, total);
        Assert.Equal(0, dot);
        Assert.False(isLethal); // 600 < 1000

        // Now test with 7 pips (1 normal + 3 power pips) -> 7 * 80 = 560 base -> 560 * 1.50 = 840
        var higherPipWizard = stormWizard with { PowerPips = 3 };
        var (_, _, totalHigher, _) = ModernCombatCalculator.CalculateDetailedDamage(higherPipWizard, target, tempest);
        Assert.Equal(840, totalHigher);
    }

    [Fact]
    public void CalculateDetailedDamage_PierceBreaksShieldBeforeResist()
    {
        // Target has 50% Tower Shield and 20% Base Resist
        var target = new CombatantState(
            Name: "Shielded Boss",
            School: "Ice",
            CurrentHp: 1500,
            MaxHp: 1500,
            NormalPips: 2,
            PowerPips: 0,
            ResistPercent: 20.0,
            ActiveShields: [new CombatModifier("Tower Shield", "Universal", 50.0, "Shield")]);

        var spell = new CombatCardState("Helephant", "Fire", "Attack", PipCost: 6, BaseDamage: 625);

        // Player A: 0% Pierce
        // Shield absorbs 50% -> remaining is 50% -> 20% resist reduces to 40% total
        // Total reduction = 50% + 20% = 70% reduction -> deals 30% of 625 = 187.5 ~ 188
        var pyromancerNoPierce = new CombatantState("Pyro", "Fire", CurrentHp: 2000, MaxHp: 2000, NormalPips: 6, PowerPips: 0, PiercePercent: 0.0);
        var (_, _, totalNoPierce, _) = ModernCombatCalculator.CalculateDetailedDamage(pyromancerNoPierce, target, spell);
        Assert.Equal(188, totalNoPierce);

        // Player B: 60% Pierce
        // 50% pierce breaks the 50% Tower Shield completely (leaves 0% shield).
        // 10% leftover pierce reduces 20% resist to 10% effective resist.
        // Total reduction is only 10% -> deals 90% of 625 = 562.5 ~ 563!
        var pyromancerWithPierce = pyromancerNoPierce with { PiercePercent = 60.0 };
        var (_, _, totalWithPierce, _) = ModernCombatCalculator.CalculateDetailedDamage(pyromancerWithPierce, target, spell);
        Assert.Equal(563, totalWithPierce);
    }

    [Fact]
    public void CalculateDetailedDamage_MultiplicativeBladesAndTraps()
    {
        var target = new CombatantState(
            Name: "Boss",
            School: "Balance",
            CurrentHp: 2500,
            MaxHp: 2500,
            NormalPips: 2,
            PowerPips: 0,
            ActiveTraps: [new CombatModifier("Feint", "Death", 70.0, "Trap")]);

        var caster = new CombatantState(
            Name: "Pyro",
            School: "Fire",
            CurrentHp: 2000,
            MaxHp: 2000,
            NormalPips: 6,
            PowerPips: 0,
            GearDamageBonus: 0.0,
            ActiveBlades:
            [
                new CombatModifier("Fireblade", "Fire", 35.0, "Blade"),
                new CombatModifier("Sharpened Fireblade", "Fire", 45.0, "Blade")
            ]);

        var spell = new CombatCardState("Helephant", "Fire", "Attack", PipCost: 6, BaseDamage: 500);

        // 500 * (1 + 0.35) * (1 + 0.45) * (1 + 0.70)
        // 500 * 1.35 = 675
        // 675 * 1.45 = 978.75
        // 978.75 * 1.70 = 1663.875 -> ~1664
        var (_, _, totalDamage, isLethal) = ModernCombatCalculator.CalculateDetailedDamage(caster, target, spell);
        Assert.Equal(1664, totalDamage);
        Assert.False(isLethal); // 1664 < 2500
    }

    [Fact]
    public void CalculateDetailedDamage_DoTTicksCalculatedAccurately()
    {
        // Fire Dragon: 440 Initial Hit + 351 over 3 turns (DoT)
        var fireDragon = new CombatCardState(
            Name: "Fire Dragon",
            School: "Fire",
            SpellType: "Attack",
            PipCost: 7,
            BaseDamage: 440,
            DoTDamage: 351,
            DoTRounds: 3,
            IsAoe: true);

        var caster = new CombatantState("Pyro", "Fire", CurrentHp: 2000, MaxHp: 2000, NormalPips: 1, PowerPips: 3, GearDamageBonus: 20.0);
        var target = new CombatantState("Mob", "Ice", CurrentHp: 900, MaxHp: 900, NormalPips: 0, PowerPips: 0);

        // 440 * 1.20 = 528 initial hit
        // 351 * 1.20 = 421.2 ~ 421 DoT
        // Total = 528 + 421 = 949 >= 900 -> Lethal!
        var (hit, dot, total, isLethal) = ModernCombatCalculator.CalculateDetailedDamage(caster, target, fireDragon);

        Assert.Equal(528, hit);
        Assert.Equal(421, dot);
        Assert.Equal(949, total);
        Assert.True(isLethal);
    }
}
