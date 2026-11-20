using System;
using System.Collections.Generic;
using ProjectMaelstrom.Utilities;
using Xunit;

namespace ProjectMaelstrom.Tests;

public class OverworldOpportunityCollectorTests
{
    [Fact]
    public void SelectsClosestSafeReagent_WhenNoMobThreatPresent()
    {
        var collector = new OverworldOpportunityCollector();

        var playerX = 0.0;
        var playerY = 0.0;

        var entities = new List<OverworldEntity>
        {
            new("reagent-1", "Ore", OverworldOpportunityKind.Reagent, 100.0, 100.0), // ~141.4 units away
            new("reagent-2", "Black Lotus", OverworldOpportunityKind.Reagent, 50.0, 50.0) // ~70.7 units away
        };

        var noMobs = new List<RoamingMobThreat>();

        var plan = collector.EvaluateOpportunity(playerX, playerY, currentBackpackCount: 30, backpackCapacity: 80, entities, noMobs);

        Assert.True(plan.ShouldIntercept);
        Assert.NotNull(plan.TargetEntity);
        Assert.Equal("Black Lotus", plan.TargetEntity.Name);
        Assert.Equal(OverworldOpportunityKind.Reagent, plan.TargetEntity.Kind);
        Assert.True(plan.ApproachDistance < 71.0);
        Assert.Equal(5, plan.PlannedSteps.Count);
        Assert.Contains("Pulse 'X' key", plan.PlannedSteps[2]);
    }

    [Fact]
    public void RejectsOpportunity_WhenEntityInsideMobThreatRadius()
    {
        var collector = new OverworldOpportunityCollector();

        var playerX = 0.0;
        var playerY = 0.0;

        var entities = new List<OverworldEntity>
        {
            new("node-lotus", "Black Lotus", OverworldOpportunityKind.Reagent, 200.0, 200.0)
        };

        // A roaming mob (e.g. Haunted Minion) patrolling right next to the Black Lotus
        var mobs = new List<RoamingMobThreat>
        {
            new("mob-1", "Haunted Minion", 220.0, 220.0, ThreatRadius: 350.0) // Mob is ~28 units from Lotus
        };

        var plan = collector.EvaluateOpportunity(playerX, playerY, currentBackpackCount: 30, backpackCapacity: 80, entities, mobs);

        // Must NOT intercept! Getting pulled into battle ruins the route
        Assert.False(plan.ShouldIntercept);
        Assert.Null(plan.TargetEntity);
        Assert.Contains("combat threat radius", plan.Reason);
    }

    [Fact]
    public void SkipsWoodenChests_WhenBackpackIsFull()
    {
        var collector = new OverworldOpportunityCollector();

        var entities = new List<OverworldEntity>
        {
            new("chest-1", "Wooden Chest", OverworldOpportunityKind.WoodenChest, 60.0, 60.0)
        };

        var noMobs = new List<RoamingMobThreat>();

        // When backpack is 80/80 (full)
        var plan = collector.EvaluateOpportunity(0, 0, currentBackpackCount: 80, backpackCapacity: 80, entities, noMobs);

        Assert.False(plan.ShouldIntercept);
        Assert.Contains("backpack full for chests", plan.Reason);

        // When backpack has room (70/80) -> Intercepts!
        var planRoom = collector.EvaluateOpportunity(0, 0, currentBackpackCount: 70, backpackCapacity: 80, entities, noMobs);
        Assert.True(planRoom.ShouldIntercept);
        Assert.Equal("Wooden Chest", planRoom.TargetEntity!.Name);
    }

    [Fact]
    public void HonorsSilverChestToggle()
    {
        var collector = new OverworldOpportunityCollector { EnableSilverChests = false };

        var entities = new List<OverworldEntity>
        {
            new("silver-1", "Silver Chest", OverworldOpportunityKind.SilverChest, 50.0, 50.0)
        };

        var planDisabled = collector.EvaluateOpportunity(0, 0, 10, 80, entities, []);
        Assert.False(planDisabled.ShouldIntercept);

        collector.EnableSilverChests = true;
        var planEnabled = collector.EvaluateOpportunity(0, 0, 10, 80, entities, []);
        Assert.True(planEnabled.ShouldIntercept);
        Assert.Equal("Silver Chest", planEnabled.TargetEntity!.Name);
    }

    [Fact]
    public void IgnoresEntitiesOutsideMaxDeviationRadius()
    {
        var collector = new OverworldOpportunityCollector { MaxDeviationRadius = 300.0 };

        var entities = new List<OverworldEntity>
        {
            new("far-node", "Red Mandrake", OverworldOpportunityKind.Reagent, 500.0, 500.0) // ~707 units away
        };

        var plan = collector.EvaluateOpportunity(0, 0, 10, 80, entities, []);
        Assert.False(plan.ShouldIntercept);
    }

    [Fact]
    public void OverworldHarvestCard_HasExpectedPriorityAndMetadata()
    {
        var card = new OverworldHarvestCard();
        Assert.Equal("job.overworld-harvest", card.Id);
        Assert.Equal(45, card.Priority); // Sits right above quest progression (50)
        Assert.Equal("Harvesting", card.Category);

        var context = new JobExecutionContext(new WizardSessionSnapshot(
            HealthRatio: 1.0, ManaRatio: 1.0, PotionsAvailable: 1,
            BackpackCount: 20, BackpackCapacity: 80, InCombat: false, InDialogue: false,
            CurrentZoneCode: "COMMONS", EnergyAvailable: 50));

        Assert.True(card.ShouldTrigger(context));
    }
}
