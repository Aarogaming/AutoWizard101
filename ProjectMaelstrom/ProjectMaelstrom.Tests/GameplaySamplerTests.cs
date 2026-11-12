using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class GameplaySamplerTests
{
    [Fact]
    public void SamplePurposeAndCheckedPackSurviveReopening()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-sampler-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Null(GameplaySamplePlan.Load(root));
            var plan = new GameplaySamplePlan("check", "Distinguish ready and finished", "pack-hash", "My pack");
            plan.Save(root);
            Assert.Equal(plan, GameplaySamplePlan.Load(root));
            Assert.Throws<InvalidDataException>(() => (plan with { PackHash = null }).Save(root));
            Assert.Equal(plan, GameplaySamplePlan.Load(root));
            Assert.Throws<InvalidDataException>(() => (plan with { Purpose = "play" }).Save(root));
            File.WriteAllText(Path.Combine(root, "sampler.json"), "{\"Purpose\":\"check\",\"Goal\":\"test\"}");
            Assert.Throws<InvalidDataException>(() => GameplaySamplePlan.Load(root));
        }
        finally { Directory.Delete(root, true); }
    }
}
