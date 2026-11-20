using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101ResourceFactsTests
{
    [Fact]
    public void ObservedGlobeMaximaReachAliasesAndPercentConditionsWithoutBaseBonusMath()
    {
        var facts = new Dictionary<string, JsonElement>();
        var reading = new W101ResourceReading("observed", 101, new() { ["health"] = new(2460, 4920) }, new() { ["health"] = 2460 }, "hud-controls-and-correlated-stats");
        W101ResourceFacts.Add(facts, reading, 100);
        Assert.Equal(4920, facts["health.max"].GetDouble());
        Assert.Equal(50, facts["health.percent"].GetDouble());
        var now = DateTime.UtcNow;
        var snapshot = new JobFacts("wizard101", "target", now, facts, "fixture");
        var card = new JobCardSpec("test", "test", "Test", Conditions: [new("Health.Percent", "lte", JsonSerializer.SerializeToElement(50))]);
        Assert.Null(JobDeckRules.FactBlock(card, "wizard101", snapshot, now, true));
        facts.Clear();
        W101ResourceFacts.Add(facts, reading with { CurrentValues = new() { ["health"] = 4000 } }, 100);
        Assert.Empty(facts);
    }
    [Fact]
    public void CandidateInventoryDoesNotBecomeExecutableFacts()
    {
        var raw = JsonSerializer.SerializeToElement(new
        {
            statesObservedUtcTicks = 100,
            resourceCandidate = new { observedUtcTicks = 100, health = new { current = 100, maximum = 200 }, liveQualified = false }
        });
        Assert.Empty(W101RecordedFacts.Read(raw));
    }
    [Fact]
    public void CurrentOnlyEvidenceNeverInventsMaximumOrPercentage()
    {
        var facts = new Dictionary<string, JsonElement>();
        var reading = new W101ResourceReading("observed", 101, new(), new() { ["health"] = 4559, ["mana"] = 446 });
        W101ResourceFacts.Add(facts, reading, 100);
        Assert.Equal(2, facts.Count);
        Assert.Equal(4559, facts["health"].GetDouble());
        Assert.DoesNotContain("health.max", facts.Keys);
        facts.Clear();
        W101ResourceFacts.Add(facts, reading, 0);
        Assert.Empty(facts);
    }
    [Fact]
    public void ResourcePairsReachAliasesConditionsAndRecordingProjection()
    {
        var reading = new W101ResourceReading("observed", 100, new()
        {
            ["health"] = new(100, 200),
            ["mana"] = new(50, 100),
            ["energy"] = new(80, 120),
            ["potions"] = new(3.5, 4)
        });
        var facts = W101RecordedFacts.Read(JsonSerializer.SerializeToElement(new { resources = reading, statesObservedUtcTicks = 100 }));
        Assert.Equal(12, facts.Count);
        Assert.Equal(50, facts["health.percent"].GetDouble());
        Assert.Equal(3.5, facts["potions"].GetDouble());
        var condition = new JobCondition("Health.Max", "gte", JsonSerializer.SerializeToElement(200));
        JobFactCatalog.ValidateCondition(condition);
        var now = DateTime.UtcNow;
        var snapshot = new JobFacts("wizard101", "target", now, facts, "synthetic-resource-fixture");
        Assert.Null(JobDeckRules.FactBlock(new("test", "test", "Test", Conditions: [condition]), "wizard101", snapshot, now, true));
        Assert.Equal(4, JobAliasRegister.Snapshot(snapshot, now).Single(v => v.Id == "potions.max").Value!.Value.GetDouble());
    }
    [Theory]
    [InlineData(-1, 100, 100)]
    [InlineData(101, 100, 100)]
    [InlineData(1, 0, 100)]
    [InlineData(1.5, 100, 100)]
    [InlineData(1, 100, 99)]
    [InlineData(1, 100, 20000101)]
    public void InvalidOrDifferentlyTimedEvidenceCannotBecomeFacts(double current, double maximum, long stamp)
    {
        var facts = new Dictionary<string, JsonElement>();
        W101ResourceFacts.Add(facts, new("observed", stamp, new() { ["health"] = new(current, maximum) }), 100);
        Assert.Empty(facts);
    }
}
