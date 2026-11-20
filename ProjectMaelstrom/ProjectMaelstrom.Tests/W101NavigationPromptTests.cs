using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101NavigationPromptTests
{
    [Theory]
    [InlineData("World Gate", true)]
    [InlineData("world gate", true)]
    [InlineData("World Gatekeeper", false)]
    [InlineData("Dance Game", false)]
    [InlineData(null, false)]
    public void RequiresExactConfiguredPrompt(string? title, bool expected) =>
        Assert.Equal(expected, W101NavigationPrompt.Matches(title, "World Gate"));

    [Fact]
    public void LoadingAndMissingEvidenceInvalidatePrompt()
    {
        var prior = new W101NavigationPrompt.Snapshot(1, true, "World Gate", "World Gate");
        Assert.Null(W101NavigationPrompt.ParseLine("{\"kind\":\"loading\",\"loading\":true}", prior));
        Assert.Null(W101NavigationPrompt.ParseLine("{\"kind\":\"loading\",\"loading\":null}", prior));
        Assert.Null(W101NavigationPrompt.ParseLine("{\"kind\":\"tree-unavailable\"}", prior));
        Assert.Equal(prior, W101NavigationPrompt.ParseLine("{\"kind\":\"loading\",\"loading\":false}", prior));
    }
}
