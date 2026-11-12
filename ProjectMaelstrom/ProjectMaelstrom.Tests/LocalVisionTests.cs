using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class LocalVisionTests
{
    [Theory]
    [InlineData(" Game Over! ", "game-over")]
    [InlineData("level 88", "level-88")]
    public void NormalizesUsableScreenLabels(string response, string expected)
    {
        Assert.Equal(expected, LocalVision.NormalizeLabel(response));
    }

    [Theory]
    [InlineData("")]
    [InlineData("8888888888888888888888888888888888888888")]
    [InlineData("aaaaaaaaaaaa")]
    [InlineData("12345")]
    public void RejectsDegenerateModelOutput(string response)
    {
        Assert.Throws<UnusableVisionSuggestionException>(() => LocalVision.NormalizeLabel(response));
    }
}
