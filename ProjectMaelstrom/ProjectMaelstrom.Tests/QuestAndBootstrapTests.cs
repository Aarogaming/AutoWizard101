using System;
using System.Drawing;
using ProjectMaelstrom.Utilities;
using Xunit;

namespace ProjectMaelstrom.Tests;

public class QuestAndBootstrapTests
{
    [Fact]
    public void QuestExecutionEngine_ResolvesTransit_WhenInDifferentZone()
    {
        var rawObjective = "Defeat Skeletal Pirates in Unicorn Way (1 of 3)";
        var intent = W101QuestIntent.Parse(rawObjective);

        Assert.True(intent.IsRecognized);
        Assert.Equal(W101QuestIntentAction.Defeat, intent.Action);
        Assert.Equal("Skeletal Pirates", intent.Target);
        Assert.Equal("Unicorn Way", intent.Area);
        Assert.Equal(1, intent.CurrentCount);
        Assert.Equal(3, intent.RequiredCount);

        // Wizard currently standing in The Commons (Unicorn Way gate connects directly to Commons)
        var resolution = QuestExecutionEngine.ResolveQuestStep("COMMONS", intent);

        Assert.False(resolution.IsInTargetZone);
        Assert.Equal("UNICORN_WAY", resolution.TargetZoneCode);
        Assert.Equal(["COMMONS", "UNICORN_WAY"], resolution.ZonePath);
        Assert.Equal("UNICORN_WAY", resolution.NextTransitZone);
        Assert.Equal("TransitToZone:UNICORN_WAY", resolution.ImmediateAction);
        Assert.Equal(CardExecutionMode.ContainedLoop, resolution.RecommendedMode);

        // Multi-hop path test: Commons to Bazaar (Commons -> Shopping District -> Olde Town -> Bazaar)
        var bazaarIntent = W101QuestIntent.Parse("Explore Olde Town in Bazaar");
        var multiHopResolution = QuestExecutionEngine.ResolveQuestStep("COMMONS", bazaarIntent);
        Assert.False(multiHopResolution.IsInTargetZone);
        Assert.Equal("BAZAAR", multiHopResolution.TargetZoneCode);
        Assert.Equal(["COMMONS", "SHOPPING_DISTRICT", "OLDE_TOWN", "BAZAAR"], multiHopResolution.ZonePath);
        Assert.Equal("SHOPPING_DISTRICT", multiHopResolution.NextTransitZone);
    }

    [Fact]
    public void QuestExecutionEngine_ResolvesCombat_WhenInSameZone()
    {
        var rawObjective = "Defeat Skeletal Pirates in Unicorn Way (2 of 3)";
        var intent = W101QuestIntent.Parse(rawObjective);

        // Wizard is already in Unicorn Way
        var resolution = QuestExecutionEngine.ResolveQuestStep("UNICORN_WAY", intent);

        Assert.True(resolution.IsInTargetZone);
        Assert.Equal("UNICORN_WAY", resolution.TargetZoneCode);
        Assert.Equal("SeekAndDefeat:Skeletal Pirates", resolution.ImmediateAction);
        Assert.Equal(CardExecutionMode.ContainedLoop, resolution.RecommendedMode);
        Assert.Contains("(2/3)", resolution.Summary);
    }

    [Fact]
    public void QuestExecutionEngine_ResolvesTalk_WhenInSameZone()
    {
        var rawObjective = "Talk To Headmaster Ambrose in Ravenwood";
        var intent = W101QuestIntent.Parse(rawObjective);

        Assert.True(intent.IsRecognized);
        Assert.Equal(W101QuestIntentAction.Talk, intent.Action);

        var resolution = QuestExecutionEngine.ResolveQuestStep("RAVENWOOD", intent);

        Assert.True(resolution.IsInTargetZone);
        Assert.Equal("RAVENWOOD", resolution.TargetZoneCode);
        Assert.Equal("ApproachAndTalk:Headmaster Ambrose", resolution.ImmediateAction);
        Assert.Equal(CardExecutionMode.ContainedLoop, resolution.RecommendedMode);
    }

    [Fact]
    public void QuestExecutionEngine_ResolvesCollect_AsOneShot_WhenInSameZone()
    {
        var rawObjective = "Collect Golem Cog in Golem Court";
        var intent = W101QuestIntent.Parse(rawObjective);

        Assert.True(intent.IsRecognized);
        Assert.Equal(W101QuestIntentAction.Collect, intent.Action);

        var resolution = QuestExecutionEngine.ResolveQuestStep("GOLEM_COURT", intent);

        Assert.True(resolution.IsInTargetZone);
        Assert.Equal("GOLEM_COURT", resolution.TargetZoneCode);
        Assert.Equal("CollectItem:Golem Cog", resolution.ImmediateAction);
        Assert.Equal(CardExecutionMode.OneShot, resolution.RecommendedMode);
    }

    [Fact]
    public void W101BootstrapHandler_CalculatesCharacterSlotCoordinates()
    {
        for (int slot = 1; slot <= 6; slot++)
        {
            var coord = W101BootstrapHandler.GetCharacterSlotCoordinate(slot);
            Assert.True(coord.X > 0 && coord.Y > 0);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => W101BootstrapHandler.GetCharacterSlotCoordinate(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => W101BootstrapHandler.GetCharacterSlotCoordinate(7));
    }

    [Fact]
    public void W101BootstrapHandler_PlansCharacterSelectionSequence()
    {
        var plan = W101BootstrapHandler.PlanCharacterSelection(targetSlot: 2);

        Assert.Equal(2, plan.TargetCharacterSlot);
        Assert.Equal(BootstrapStage.CharacterSelect, plan.InitialStage);
        Assert.Equal(2, plan.RequiredClicks.Count);

        // Click 1: Character slot 2 pedestal
        Assert.Equal(new Point(340, 420), plan.RequiredClicks[0].Coordinate);
        Assert.Contains("Slot 2", plan.RequiredClicks[0].Description);

        // Click 2: Play button
        Assert.Equal(W101BootstrapHandler.PlayButtonCoord, plan.RequiredClicks[1].Coordinate);
        Assert.Contains("Play", plan.RequiredClicks[1].Description);
    }
}
