using System;
using System.Collections.Generic;
using System.Drawing;
using ProjectMaelstrom.Utilities;
using Xunit;

namespace ProjectMaelstrom.Tests;

public class CombatMctsSimulatorTests
{
    [Fact]
    public void ImmediateAoeWipe_SelectsAoeAttackAndEstimatesOneTurn()
    {
        var simulator = new CombatMctsSimulator();

        var player = new CombatantState(
            Name: "Pyromancer",
            School: "Fire",
            CurrentHp: 1500,
            MaxHp: 1500,
            NormalPips: 0,
            PowerPips: 2, // 4 pips total
            BattleSlot: 4,
            GearDamageBonus: 0.0);

        var enemies = new List<CombatantState>
        {
            new("Lost Soul A", "Death", CurrentHp: 300, MaxHp: 300, NormalPips: 1, PowerPips: 0, BattleSlot: 0),
            new("Lost Soul B", "Death", CurrentHp: 280, MaxHp: 300, NormalPips: 1, PowerPips: 0, BattleSlot: 1)
        };

        var hand = new List<CombatCardState>
        {
            new("Meteor Strike", "Fire", "Attack", PipCost: 4, BaseDamage: 305, IsAoe: true, HandIndex: 0),
            new("Firecat", "Fire", "Attack", PipCost: 1, BaseDamage: 80, IsAoe: false, HandIndex: 1)
        };

        var decision = simulator.SelectBestAction(player, enemies, hand);

        Assert.Equal("LethalStrike", decision.ActionType);
        Assert.NotNull(decision.PrimaryCard);
        Assert.Equal("Meteor Strike", decision.PrimaryCard.Name);
        Assert.Equal(1, decision.EstimatedTurnsToWin);
        Assert.True(decision.ExpectedDamage >= 305);
        Assert.Contains("Immediate AoE lethal wipe", decision.Reasoning);

        // Click sequence for AoE has no target click
        var clicks = simulator.ResolveClickSequence(decision, totalCardsInHand: 2);
        Assert.Single(clicks);
        Assert.Contains("Cast Card (Meteor Strike)", clicks[0].Description);
    }

    [Fact]
    public void SingleTargetLethalWithEnchant_SelectsEnchantAndSpell_AndResolvesClicks()
    {
        var simulator = new CombatMctsSimulator();

        var player = new CombatantState(
            Name: "Pyromancer",
            School: "Fire",
            CurrentHp: 2000,
            MaxHp: 2000,
            NormalPips: 2,
            PowerPips: 0,
            BattleSlot: 4,
            GearDamageBonus: 0.0);

        var enemies = new List<CombatantState>
        {
            new("Field Boss", "Storm", CurrentHp: 360, MaxHp: 500, NormalPips: 2, PowerPips: 0, BattleSlot: 2, IsBoss: true)
        };

        var hand = new List<CombatCardState>
        {
            new("Epic", "Sun", "Enchant", PipCost: 0, BaseDamage: 300, HandIndex: 0),
            new("Firecat", "Fire", "Attack", PipCost: 1, BaseDamage: 80, HandIndex: 1)
        };

        var decision = simulator.SelectBestAction(player, enemies, hand);

        Assert.Equal("EnchantCast", decision.ActionType);
        Assert.NotNull(decision.PrimaryCard);
        Assert.Equal("Firecat", decision.PrimaryCard.Name);
        Assert.NotNull(decision.SecondaryEnchantCard);
        Assert.Equal("Epic", decision.SecondaryEnchantCard.Name);
        Assert.Equal(2, decision.TargetSlot);
        Assert.Equal(380, decision.ExpectedDamage);
        Assert.Equal(1, decision.EstimatedTurnsToWin);

        var clicks = simulator.ResolveClickSequence(decision, totalCardsInHand: 2);
        Assert.Equal(4, clicks.Count);
        Assert.Contains("Select Enchant Card (Epic)", clicks[0].Description);
        Assert.Contains("Apply Enchant to (Firecat)", clicks[1].Description);
        Assert.Contains("Cast Enchanted Spell (Epic-Firecat)", clicks[2].Description);
        Assert.Contains("Select Target Slot 2", clicks[3].Description);

        // Verify target coordinate for slot 2 (174 * 2 + 130 = 478, y = 50)
        Assert.Equal(new Point(478, 50), clicks[3].Coordinate);
    }

    [Fact]
    public void BuffSetup_SelectsBladeWhenImmediateAttackCannotKill()
    {
        var simulator = new CombatMctsSimulator();

        var player = new CombatantState(
            Name: "Pyromancer",
            School: "Fire",
            CurrentHp: 2000,
            MaxHp: 2000,
            NormalPips: 2, // 2 pips
            PowerPips: 0,
            BattleSlot: 4,
            GearDamageBonus: 20.0);

        var enemies = new List<CombatantState>
        {
            new("Tower Boss", "Ice", CurrentHp: 950, MaxHp: 1200, NormalPips: 2, PowerPips: 1, BattleSlot: 1, IsBoss: true)
        };

        // Fireblade (+35%, 0 pips), Helephant (6 pips, 625 dmg)
        // Turn 2 projected pips = 2 - 0 + 2 = 4 pips (still not enough for Helephant 6 pips)
        // But with Sunbird (3 pips, 295 dmg) or Firecat (1 pip)
        // With Sunbird: (295) * (1 + 0.55) = 457.25
        // Let's test with Firecat (1 pip, 800 base damage test):
        var hand = new List<CombatCardState>
        {
            new("Fireblade", "Fire", "Blade", PipCost: 0, BuffPercent: 35.0, HandIndex: 0),
            new("Phoenix", "Fire", "Attack", PipCost: 4, BaseDamage: 600, HandIndex: 1)
        };
        // Phoenix with +55% (20% gear + 35% blade) = 600 * 1.55 = 930 < 950
        // If Phoenix BaseDamage is 650: 650 * 1.55 = 1007.5 >= 950!
        hand[1] = hand[1] with { BaseDamage = 650 };

        var decision = simulator.SelectBestAction(player, enemies, hand);

        Assert.Equal("BuffSetup", decision.ActionType);
        Assert.NotNull(decision.PrimaryCard);
        Assert.Equal("Fireblade", decision.PrimaryCard.Name);
        Assert.Equal(4, decision.TargetSlot); // Player slot
        Assert.Equal(2, decision.EstimatedTurnsToWin);
        Assert.Contains("guarantee Turn 2 lethal", decision.Reasoning);

        var clicks = simulator.ResolveClickSequence(decision, totalCardsInHand: 2);
        Assert.Equal(2, clicks.Count);
        Assert.Contains("Cast Card (Fireblade)", clicks[0].Description);
        Assert.Contains("Select Target Slot 4", clicks[1].Description);
        Assert.Equal(CombatGeometry.CalculateTargetCoordinates(4), clicks[1].Coordinate);
    }

    [Fact]
    public void EmergencyHeal_TriggersWhenHealthBelowThirtyPercent()
    {
        var simulator = new CombatMctsSimulator();

        var player = new CombatantState(
            Name: "Pyromancer",
            School: "Fire",
            CurrentHp: 250,
            MaxHp: 1000, // 25% HP ratio (< 30%)
            NormalPips: 2,
            PowerPips: 0,
            BattleSlot: 4);

        var enemies = new List<CombatantState>
        {
            new("Fire Elemental", "Fire", CurrentHp: 500, MaxHp: 500, NormalPips: 2, PowerPips: 0, BattleSlot: 0)
        };

        var hand = new List<CombatCardState>
        {
            new("Pixie", "Life", "Heal", PipCost: 2, HealAmount: 420, HandIndex: 0),
            new("Firecat", "Fire", "Attack", PipCost: 1, BaseDamage: 80, HandIndex: 1)
        };

        var decision = simulator.SelectBestAction(player, enemies, hand);

        Assert.Equal("EmergencyHeal", decision.ActionType);
        Assert.NotNull(decision.PrimaryCard);
        Assert.Equal("Pixie", decision.PrimaryCard.Name);
        Assert.Equal(4, decision.TargetSlot);
        Assert.Contains("Critical HP (25%)", decision.Reasoning);

        var clicks = simulator.ResolveClickSequence(decision, totalCardsInHand: 2);
        Assert.Equal(2, clicks.Count);
        Assert.Contains("Cast Card (Pixie)", clicks[0].Description);
        Assert.Contains("Select Target Slot 4", clicks[1].Description);
    }

    [Fact]
    public void PassTurn_WhenLowPipsAndNoViablePlays()
    {
        var simulator = new CombatMctsSimulator();

        var player = new CombatantState(
            Name: "Pyromancer",
            School: "Fire",
            CurrentHp: 1200,
            MaxHp: 1200,
            NormalPips: 0,
            PowerPips: 0,
            BattleSlot: 4);

        var enemies = new List<CombatantState>
        {
            new("Krok Sentinel", "Balance", CurrentHp: 400, MaxHp: 400, NormalPips: 2, PowerPips: 0, BattleSlot: 0)
        };

        var hand = new List<CombatCardState>
        {
            new("Helephant", "Fire", "Attack", PipCost: 6, BaseDamage: 625, HandIndex: 0)
        };

        var decision = simulator.SelectBestAction(player, enemies, hand);

        Assert.Equal("Pass", decision.ActionType);
        Assert.Null(decision.PrimaryCard);
        Assert.Contains("Passing turn to accumulate pips", decision.Reasoning);

        var clicks = simulator.ResolveClickSequence(decision, totalCardsInHand: 1);
        Assert.Single(clicks);
        Assert.Equal(CombatGeometry.PassButton, clicks[0].Coordinate);
    }

    [Fact]
    public void CombatGeometry_HandCardCoordinates_CorrectDistribution()
    {
        // 7 cards in hand
        for (int i = 0; i < 7; i++)
        {
            var pt = CombatGeometry.GetHandCardCoordinates(i, 7);
            Assert.True(CombatGeometry.SpellArea.Contains(pt), $"Card {i} at {pt} should be inside SpellArea {CombatGeometry.SpellArea}");
        }

        // Single card centered in tray
        var singleCard = CombatGeometry.GetHandCardCoordinates(0, 1);
        Assert.Equal(new Point(430, 325), singleCard);
        Assert.True(CombatGeometry.SpellArea.Contains(singleCard));

        // Argument validation
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatGeometry.GetHandCardCoordinates(-1, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatGeometry.GetHandCardCoordinates(5, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatGeometry.GetHandCardCoordinates(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatGeometry.GetHandCardCoordinates(0, 8));
    }

    [Fact]
    public void ActiveCombatCard_PlansAndResolvesTurnClicks()
    {
        var combatCard = new ActiveCombatCard();
        Assert.Equal("job.active-combat", combatCard.Id);
        Assert.Equal(1, combatCard.Priority);

        var player = new CombatantState(
            Name: "Pyromancer",
            School: "Fire",
            CurrentHp: 1000,
            MaxHp: 1000,
            NormalPips: 0,
            PowerPips: 2,
            BattleSlot: 4);

        var enemies = new List<CombatantState>
        {
            new("Skeletal Pirate", "Death", CurrentHp: 250, MaxHp: 300, NormalPips: 1, PowerPips: 0, BattleSlot: 0)
        };

        var hand = new List<CombatCardState>
        {
            new("Meteor Strike", "Fire", "Attack", PipCost: 4, BaseDamage: 305, IsAoe: true, HandIndex: 0)
        };

        var decision = combatCard.PlanTurn(player, enemies, hand);
        Assert.Equal("LethalStrike", decision.ActionType);

        var clicks = combatCard.ResolveTurnClicks(decision, totalCardsInHand: 1);
        Assert.Single(clicks);
        Assert.Contains("Cast Card (Meteor Strike)", clicks[0].Description);
    }
}
