using ProjectMaelstrom.Utilities;
namespace ProjectMaelstrom.Tests;

public sealed class W101WorldGateVisitTests
{
    [Theory]
    [InlineData("World Gate", 0, true)]
    [InlineData("World Gatekeeper", 0, false)]
    [InlineData("World Gate", 3000, false)]
    public void InteractionRequiresExactFreshStableTitle(string title, int ageMs, bool expected)
    {
        var now = DateTime.UtcNow.Ticks;
        var states = new Dictionary<string, bool?> { ["interactionPrompt"] = true, ["dialogue"] = false, ["menu"] = false, ["nativeIdle"] = false };
        var reading = new W101UiTreeResult(1, "test", W101UiTreeOperation.Version, WizwalkerOperation.SourceSha256,
            W101QuestConversationTests.Target, "observed", "test", false, states, now,
            now - TimeSpan.FromMilliseconds(ageMs).Ticks, 100, true, true, 2, 2, 0, new(states),
            InteractionTitle: title, StableInteractionTitle: title);
        Assert.Equal(expected, W101WorldGateVisit.PromptMatches(reading, now));
        Assert.False(W101WorldGateVisit.PromptMatches(reading with { StableInteractionTitle = null }, now));
        var menuStates = new Dictionary<string, bool?>(states) { ["menu"] = true };
        var overlay = reading with { States = menuStates, StableStates = new(menuStates) };
        Assert.False(W101WorldGateVisit.PromptMatches(overlay, now));
        Assert.Equal(expected, W101WorldGateVisit.CanReinteractOverlay(overlay, now));
        Assert.False(W101WorldGateVisit.CanReinteractOverlay(overlay with { Loading = true }, now));
        Assert.False(W101WorldGateVisit.CanReinteractOverlay(overlay with { StableInteractionTitle = null }, now));
        menuStates["nativeIdle"] = true;
        Assert.False(W101WorldGateVisit.CanReinteractOverlay(overlay, now));
    }
    [Theory]
    [InlineData("World Gate Press X", false)]
    [InlineData("SPIRAL MAP Available Worlds Wizard City Krokotopia EXIT Go To World", true)]
    [InlineData("SPIRAL MAP Available Worlds Zafaria Avalon EXIT Go To World", true)]
    [InlineData("SPIRAL MAP AVAILABLE WRLDS ZAFARIA EXIT GO TO WORLD'", true)]
    [InlineData("Wizard City", false)]
    [InlineData("Shop Mooshu Dragonspyre", false)]
    public void MenuNeedsPageIndependentHeadingAndControls(string text, bool expected) =>
        Assert.Equal(expected, W101WorldGateVisit.MenuMatches(text));
}
