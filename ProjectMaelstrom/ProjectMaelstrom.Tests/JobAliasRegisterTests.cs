using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class JobAliasRegisterTests
{
    [Theory]
    [InlineData("INT", "32767", true)]
    [InlineData("INT", "32768", false)]
    [InlineData("INT", "-32769", false)]
    [InlineData("DINT", "2147483647", true)]
    [InlineData("DINT", "2147483648", false)]
    [InlineData("DINT", "1.5", false)]
    [InlineData("BOOL", "0", false)]
    [InlineData("BOOL", "false", true)]
    [InlineData("REAL", "1.5", true)]
    public void ValuesAreValidatedWithoutCoercion(string type, string json, bool expected)
    {
        using var value = JsonDocument.Parse(json);
        Assert.Equal(expected, JobAliasRegister.Matches(type, value.RootElement));
    }
    [Fact]
    public void AliasConditionsReadOnlyCanonicalValuesAndPreserveFalse()
    {
        var now = DateTime.UtcNow;
        var facts = new JobFacts("game", "target", now, new() { ["nativeIdle"] = JsonSerializer.SerializeToElement(false) }, "fixture");
        var card = new JobCardSpec("test", "test", "Test", Conditions: [new("Idle", "eq", JsonSerializer.SerializeToElement(false))]);
        JobFactCatalog.ValidateCondition(card.Conditions![0]);
        Assert.Null(JobDeckRules.FactBlock(card, "game", facts, now, true));
        var entry = JobAliasRegister.Snapshot(facts, now).Single(v => v.Id == "nativeIdle");
        Assert.Equal("fresh", entry.Quality); Assert.False(entry.Value!.Value.GetBoolean());
        Assert.Equal("fixture", entry.Source); Assert.Equal(now, entry.ObservedUtc);
        Assert.Null(JobAliasRegister.Snapshot(facts, now.AddSeconds(3)).Single(v => v.Id == entry.Id).Value);
        Assert.Equal("stale", JobAliasRegister.Snapshot(facts, now.AddSeconds(3)).Single(v => v.Id == entry.Id).Quality);
        Assert.Equal("unknown", JobAliasRegister.Snapshot(null, now).Single(v => v.Id == entry.Id).Quality);
        Assert.Contains("Unknown", JobDeckRules.FactBlock(card, "game", facts with { Values = new() { ["Idle"] = JsonSerializer.SerializeToElement(false) } }, now, true));
    }
    [Fact]
    public void TrustedExtensionsRejectCollisionsAndHonorIntegerRanges()
    {
        var fact = new JobFactDefinition("lives", "number", "count", "fixture", true, "Lives", DataType: "INT", Aliases: ["Lives"]);
        var definitions = JobFactCatalog.Freeze([fact]);
        Assert.Equal("lives", JobFactCatalog.Find("Lives", definitions)!.Id);
        Assert.False(JobFactCatalog.ValueMatches("Lives", JsonSerializer.SerializeToElement(32768), definitions));
        Assert.Throws<InvalidDataException>(() => JobFactCatalog.Freeze([fact with { Aliases = ["Idle"] }]));
        Assert.Throws<InvalidDataException>(() => JobFactCatalog.Freeze([fact with { DataType = "BOOL" }]));
        Assert.Throws<InvalidDataException>(() => JobFactCatalog.Freeze([fact with { Aliases = ["Lives", "Lives"] }]));
    }
}
