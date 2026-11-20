using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class GamePackNamingTests
{
    [Theory]
    [InlineData("Dodge the Creeps (DEBUG)", "dodge-the-creeps")]
    [InlineData("Wizard101", "wizard101")]
    [InlineData("CON", "game-con")]
    [InlineData("!!!", "my-game")]
    public void SuggestsAValidGameIdFromTheWorkspace(string workspace, string expected)
    {
        Assert.Equal(expected, GamePackNaming.SuggestGameId(workspace));
    }

    [Fact]
    public void LimitsIdsToThePackFormatLength()
    {
        Assert.Equal(64, GamePackNaming.SuggestGameId(new string('a', 80)).Length);
    }
}
