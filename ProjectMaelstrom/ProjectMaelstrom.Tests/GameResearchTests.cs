using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class GameResearchTests
{
    [Fact]
    public void ResearchIsSavedAndBoundToSamplerWithoutExecutingItsContents()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-research-" + Guid.NewGuid().ToString("N"));
        try
        {
            var note = GameResearch.Create("Game", "https://example.org/guide", "Try the left arrow in the tutorial");
            var path = note.Save(root);
            Assert.Equal(note, GameResearch.Load(path));
            Assert.Throws<IOException>(() => note.Save(root));
            Assert.Throws<InvalidDataException>(() => GameResearch.Create("Game", "file:///local", "test"));
            Assert.Throws<InvalidDataException>(() => GameResearch.Create("Game", "https://user:pass@example.org", "test"));
            var directory = Path.Combine(root, "recording");
            Directory.CreateDirectory(directory);
            var plan = new GameplaySamplePlan("teaching", "Test the tutorial", null, null, note);
            plan.Save(directory);
            Assert.Equal(note, GameplaySamplePlan.Load(directory)!.Research);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
