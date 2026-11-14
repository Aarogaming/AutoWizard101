using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class CardScanDemandTests
{
    [Fact]
    public void ResourceDemandRequiresOptInAndDoesNotPromoteUnavailableInventory()
    {
        var state = State(new JobCardSpec("a", "test", "Test", true, DataRequests: ["Health.Current", "Health.Max", "inventory"]));
        Assert.False(CardScanDemand.Build(state).Resources);
        var plan = CardScanDemand.Build(state, resourcesEnabled: true);
        Assert.Equal(new[] { "health", "health.max" }, plan.Fields);
        Assert.Equal(new[] { "inventory" }, plan.Unavailable);
        Assert.True(plan.Resources);
        Assert.False(CardScanDemand.Build(State(), resourcesEnabled: true).Resources);
    }
    [Theory]
    [InlineData(0, "wizard101", true)]
    [InlineData(3, "wizard101", false)]
    [InlineData(-1, "wizard101", false)]
    [InlineData(0, "other-game", false)]
    public void PredictionsNeedFreshMatchingContextAndDoNotInventResourceValues(int age, string game, bool predicted)
    {
        var now = DateTime.UtcNow;
        var state = State(new JobCardSpec("a", "w101.questing", "Quest", true));
        var values = new Dictionary<string, JsonElement> { ["quest.action"] = JsonSerializer.SerializeToElement("Defeat") };
        var context = new JobFacts(game, "target", now.AddSeconds(-age), values, "fixture");
        var plan = CardScanDemand.Build(state, context: context, now: now);
        Assert.Equal(predicted, plan.Groups!.Contains("combat-preview"));
        Assert.DoesNotContain("health", plan.Fields);
        Assert.Equal(predicted, plan.Unavailable.Contains("health"));
        Assert.Single(values);
        Assert.DoesNotContain("combat-preview", CardScanDemand.Build(State(), context: context, now: now).Groups!);
    }
    private static JobDeckState State(params JobCardSpec[] cards) => new(new("test", "wizard101", cards), 0, "Waiting", new());
    [Fact]
    public void RequestsUnionAliasesAndConditionsWithoutUnavailablePolling()
    {
        var card = new JobCardSpec("a", "test", "Test", true, DataRequests: ["Idle", "energy"], Conditions: [new("nativeIdle", "eq", JsonSerializer.SerializeToElement(true))]);
        var plan = CardScanDemand.Build(State(card, card with { Id = "b" }));
        Assert.Equal(new[] { "nativeIdle" }, plan.Fields);
        Assert.Equal(new[] { "energy" }, plan.Unavailable);
        Assert.True(plan.Ui); Assert.False(plan.Quest);
    }
    [Fact]
    public void CompletedAndUndealtCardsDoNotRequestReads()
    {
        var card = new JobCardSpec("a", "test", "Test", true, DataRequests: ["menu"]);
        var state = State(card); state.Progress[card.Id] = new(Completed: 1);
        Assert.False(CardScanDemand.Build(state).Ui);
        Assert.False(CardScanDemand.Build(State(card with { InDeck = false })).Ui);
    }
    [Fact]
    public void LearningIncludesOnlyAvailableProducersAndQuestDependencies()
    {
        var plan = CardScanDemand.Build(State(), true);
        Assert.True(plan.Quest);
        Assert.DoesNotContain("energy", plan.Fields);
        Assert.Contains("quest.objective", CardScanDemand.Build(State(new JobCardSpec("a", "w101.questing", "Quest", true))).Fields);
    }
}
