using System.Text.Json;
using ProjectMaelstrom.Utilities;
namespace ProjectMaelstrom.Tests;

public sealed class JobFactCatalogTests
{
    [Theory]
    [InlineData("enery", "gte", "10")]
    [InlineData("energy", "gte", "\"10\"")]
    [InlineData("dialogue", "gte", "true")]
    [InlineData("quest.area", "eq", "false")]
    public void InvalidConditionsRejected(string fact, string op, string json)
    {
        using var doc = JsonDocument.Parse(json);
        Assert.Throws<InvalidDataException>(() => JobFactCatalog.ValidateCondition(new(fact, op, doc.RootElement)));
    }
    [Fact]
    public void UnavailableProducerDoesNotInventAReading()
    {
        Assert.False(JobFactCatalog.Find("energy")!.ProducerAvailable);
        using var value = JsonDocument.Parse("10");
        JobFactCatalog.ValidateCondition(new("energy", "gte", value.RootElement));
        var card = new JobCardSpec(Guid.NewGuid().ToString("N"), "test", "Test", Conditions: [new("energy", "gte", value.RootElement)]);
        var facts = new JobFacts("test", "target", DateTime.UtcNow, [], "synthetic");
        Assert.Contains("Unknown", JobDeckRules.FactBlock(card, "test", facts, DateTime.UtcNow, false));
    }
    [Fact]
    public void FiltersRequireStringsAndDestinationsRemainDistinct()
    {
        Assert.Throws<InvalidDataException>(() => JobFactCatalog.ValidateFilter(new("energy", [], [])));
        JobFactCatalog.ValidateFilter(new("area", ["Commons"], []));
        Assert.NotEqual(JobFactCatalog.Find("area")!.Producer, JobFactCatalog.Find("quest.area")!.Producer);
    }
}
