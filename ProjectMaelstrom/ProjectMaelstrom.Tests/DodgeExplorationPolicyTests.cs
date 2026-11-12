using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class DodgeExplorationPolicyTests
{
    private static DodgeGameContext Context() => DodgeLivePack.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContext", "pack.json")).Context!;

    [Fact]
    public void ExplorationIsRepeatableWithARecordedSeedAndReportsItsProbability()
    {
        DodgeThreatMotion[] threats = [new(new PointF(.6f, .5f), PointF.Empty, false)];
        var first = new Random(42); var replay = new Random(42);
        var player = new PointF(.5f, .5f);
        var baseline = DodgeDirectionalAvoidance.Recommend(player, threats, Context(), 60);
        var observed = new HashSet<string>(); var explorations = 0;
        for (var i = 0; i < 200; i++)
        {
            var choice = DodgeExplorationPolicy.Select(player, threats, Context(), 60, first);
            Assert.Equal(choice, DodgeExplorationPolicy.Select(player, threats, Context(), 60, replay));
            observed.Add(choice.Advice);
            if (choice.Explored) explorations++;
            Assert.InRange(choice.Probability, .2 / choice.EligibleCount, 1);
            Assert.Equal(.2 / choice.EligibleCount + (choice.Advice == baseline ? .8 : 0), choice.Probability, 10);
        }
        Assert.True(explorations > 0);
        Assert.True(observed.Count > 1);
    }

    [Fact]
    public void ExplorationNeverSelectsMovementOutOfBounds()
    {
        DodgeThreatMotion[] threats = [new(new PointF(.5f, .5f), PointF.Empty, false)];
        var random = new Random(23);
        for (var i = 0; i < 200; i++)
        {
            var choice = DodgeExplorationPolicy.Select(new PointF(5f / 480, .5f), threats, Context(), 60, random);
            Assert.DoesNotContain("Left", choice.Advice);
        }
        var quiet = DodgeExplorationPolicy.Select(new PointF(.5f, .5f), [], Context(), 60, random);
        Assert.Equal("Hold", quiet.Advice); Assert.Equal(1, quiet.Probability);
    }
}
