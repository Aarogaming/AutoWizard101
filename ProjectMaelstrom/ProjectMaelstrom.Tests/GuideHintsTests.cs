using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class GuideHintsTests
{
    [Fact]
    public void ExtractsBoundedCandidateSentencesWithoutExecutingThem()
    {
        var hints = GuideHints.Extract("The game begins. Press the left arrow to move. Click the menu. Press the left arrow to move.");
        Assert.Equal(2, hints.Count);
        Assert.Contains("Press the left arrow to move.", hints);
        Assert.Empty(GuideHints.Extract("A score appears."));
        Assert.Contains("A hidden treasure is behind the tower.", GuideHints.ExtractGuidance("A hidden treasure is behind the tower. Press space."));
        Assert.Throws<InvalidDataException>(() => GuideHints.Extract(new string('x', 100001)));
    }
}
