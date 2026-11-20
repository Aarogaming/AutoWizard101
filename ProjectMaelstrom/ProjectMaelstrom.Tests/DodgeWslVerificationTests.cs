using System.Drawing;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class DodgeWslVerificationTests
{
    [Fact]
    public void AuditDigestIdentifiesCapturedBytesAndMissingFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jsonl");
        try
        {
            Assert.Null(DodgeWslVerification.AuditDigest(path));
            File.WriteAllText(path, "abc");
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", DodgeWslVerification.AuditDigest(path));
            File.WriteAllText(path, "changed");
            Assert.NotEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", DodgeWslVerification.AuditDigest(path));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("directional-outcome-v34")]
    [InlineData("directional-explore-v35")]
    [InlineData("directional-outcome-turn-v36")]
    [InlineData("directional-outcome-reference-v37")]
    [InlineData("directional-outcome-escape-v38")]
    public void ResearchPoliciesRequireSlowPrivateEndurance(string policy)
    {
        Assert.True(DodgeWslVerification.ValidResearchMode(policy, true, true, .25));
        Assert.False(DodgeWslVerification.ValidResearchMode(policy, false, true, .25));
        Assert.False(DodgeWslVerification.ValidResearchMode(policy, true, false, .25));
        Assert.False(DodgeWslVerification.ValidResearchMode(policy, true, true, .5));
    }
    [Fact]
    public void HoldDecisionsPreserveVisualStateForFutureTraining()
    {
        var observation = new DodgeObservation(new PointF(.4f, .6f), [], "Hold", "test", "playing");
        DodgeThreatMotion[] threats = [new(new PointF(.6f, .4f), new PointF(20, -30), true)];
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(DodgeWslVerification.DecisionRecord("Hold", "Hold", null, 250, observation, threats, true)));
        var root = json.RootElement;
        Assert.Equal("Hold", root.GetProperty("advice").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("action").ValueKind);
        Assert.Equal("playing", root.GetProperty("state").GetString());
        Assert.Equal(.4, root.GetProperty("before_player").GetProperty("X").GetDouble(), 5);
        Assert.True(root.GetProperty("threats")[0].GetProperty("velocity_known").GetBoolean());
        using var stale = JsonDocument.Parse(JsonSerializer.Serialize(DodgeWslVerification.DecisionRecord("Hold", "Hold", null, 250, observation, threats, false)));
        Assert.False(stale.RootElement.TryGetProperty("before_player", out _));
    }
    [Fact]
    public void TrainingSpeedIsExplicitAndBounded()
    {
        Assert.True(DodgeWslVerification.ValidTrainingSpeed(true, 0.25));
        Assert.True(DodgeWslVerification.ValidTrainingSpeed(true, 0.5));
        Assert.True(DodgeWslVerification.ValidTrainingSpeed(true, 1));
        Assert.False(DodgeWslVerification.ValidTrainingSpeed(false, 0.25));
        Assert.False(DodgeWslVerification.ValidTrainingSpeed(true, double.NaN));
        Assert.False(DodgeWslVerification.ValidTrainingSpeed(true, 2));
    }

    [Fact]
    public void StaleFramesAreOnlyRetriedInThePrivateSteppedTest()
    {
        Assert.True(DodgeWslVerification.ShouldRetryStaleObservation(true, 700, 0));
        Assert.True(DodgeWslVerification.ShouldRetryStaleObservation(true, 700, 1));
        Assert.False(DodgeWslVerification.ShouldRetryStaleObservation(true, 700, 2));
        Assert.False(DodgeWslVerification.ShouldRetryStaleObservation(false, 700, 0));
        Assert.False(DodgeWslVerification.ShouldRetryStaleObservation(true, double.NaN, 0));
        Assert.False(DodgeWslVerification.ShouldRetryStaleObservation(true, -1, 0));
    }

    [Fact]
    public void EnduranceScoreMustMatchTheCurrentSimulatedFrame()
    {
        var directory = Path.Combine(Path.GetTempPath(), "maelstrom-evaluation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "evaluation.json"), "{\"score\":50,\"alive\":true,\"ticks\":120}");
            Assert.Equal((50, true), DodgeWslVerification.ReadEvaluation(directory, 1000, .25));
            Assert.Throws<InvalidDataException>(() => DodgeWslVerification.ReadEvaluation(directory, 1100, .25));
        }
        finally { File.Delete(Path.Combine(directory, "evaluation.json")); Directory.Delete(directory); }
    }

    [Fact]
    public void OpenEvaluationAllowsOtherSharedReadersAndWriters()
    {
        var directory = Path.Combine(Path.GetTempPath(), "maelstrom-evaluation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var current = Path.Combine(directory, "evaluation.json");
        var next = Path.Combine(directory, "evaluation.tmp");
        try
        {
            File.WriteAllText(current, "{\"score\":1,\"alive\":true,\"ticks\":120}");
            File.WriteAllText(next, "{\"score\":2,\"alive\":true,\"ticks\":120}");
            using (var reader = DodgeWslVerification.OpenEvaluation(directory))
            using (var writer = new FileStream(current, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
                Assert.True(writer.CanWrite);
            File.Move(next, current, overwrite: true);
            Assert.Equal((2, true), DodgeWslVerification.ReadEvaluation(directory, 1000, .25));
        }
        finally { File.Delete(current); File.Delete(next); Directory.Delete(directory); }
    }

    [Fact]
    public void CompleteGameBenchmarkNeverStopsAtTheTrainingGoal()
    {
        Assert.True(DodgeWslVerification.ShouldStopAtScoreGoal(false, 50, true, "playing"));
        Assert.False(DodgeWslVerification.ShouldStopAtScoreGoal(true, 50, true, "playing"));
        Assert.False(DodgeWslVerification.ShouldStopAtScoreGoal(false, 50, false, "game_over"));
        Assert.False(DodgeWslVerification.ShouldStopAtScoreGoal(false, 49, true, "playing"));
    }

    [Fact]
    public void OnlyTheOptInSteppedPlayerReusesSuccessfullyObservedActionFrames()
    {
        Assert.True(DodgeWslVerification.ShouldReuseAfterFrame(true, "center-continuous-v16", "playing", "movement-observed"));
        Assert.True(DodgeWslVerification.ShouldReuseAfterFrame(true, "center-continuous-track-v17", "playing", "movement-observed"));
        Assert.False(DodgeWslVerification.ShouldReuseAfterFrame(false, "center-continuous-track-v17", "playing", "movement-observed"));
        Assert.False(DodgeWslVerification.ShouldReuseAfterFrame(true, "center-continuous-track-v17", "unknown", "movement-observed"));
        Assert.False(DodgeWslVerification.ShouldReuseAfterFrame(false, "center-continuous-v16", "playing", "movement-observed"));
        Assert.False(DodgeWslVerification.ShouldReuseAfterFrame(true, "center-first-v12", "playing", "movement-observed"));
        Assert.False(DodgeWslVerification.ShouldReuseAfterFrame(true, "center-continuous-v16", "unknown", "movement-observed"));
        Assert.False(DodgeWslVerification.ShouldReuseAfterFrame(true, "center-continuous-v16", "playing", "unknown"));
    }
    [Fact]
    public void MapsLocalDrivePathsToWslMounts()
    {
        Assert.Equal("/mnt/f/Game Labs/Dodge", DodgeWslVerification.WslPath(@"F:\Game Labs\Dodge"));
        Assert.Throws<InvalidOperationException>(() => DodgeWslVerification.WslPath(@"\\server\share\game"));
    }

    [Fact]
    public void PreservesUncertainOutcomeWhenActionLimitIsReached()
    {
        Assert.Equal("unknown", DodgeWslVerification.FinalStopReason("unknown", 20, 10));
        Assert.Equal("action-limit", DodgeWslVerification.FinalStopReason("time-limit", 20, 10));
        Assert.Equal("observation-limit", DodgeWslVerification.FinalStopReason("time-limit", 0, 120));
        Assert.Equal("time-limit", DodgeWslVerification.FinalStopReason("time-limit", 20, 120, 40, 180));
        Assert.Equal("action-limit", DodgeWslVerification.FinalStopReason("time-limit", 40, 120, 40, 180));
    }

    [Fact]
    public void OnlyStopForConsecutiveMissesAfterAPlayerWasSeen()
    {
        Assert.False(DodgeWslVerification.LostPlayer(false, 10));
        Assert.False(DodgeWslVerification.LostPlayer(true, 2));
        Assert.True(DodgeWslVerification.LostPlayer(true, 3));
    }

    [Fact]
    public void SurvivalEvidenceDistinguishesGameOverFromCensoredRuns()
    {
        Assert.Equal("game-over-observed", DodgeWslVerification.SurvivalObservation("game-over", true));
        Assert.Equal("censored-at-stop", DodgeWslVerification.SurvivalObservation("action-limit", true));
        Assert.Equal("not-observed", DodgeWslVerification.SurvivalObservation("game-over", false));
    }

    [Fact]
    public void OnlyGuardedCandidateReobservesUnknownFramesAndNeverIndefinitely()
    {
        Assert.True(DodgeWslVerification.ShouldReobserve("guarded-v4", "unknown", 0));
        Assert.False(DodgeWslVerification.ShouldReobserve("guarded-v4", "unknown", 2));
        Assert.False(DodgeWslVerification.ShouldReobserve("guarded-v4", "game_over", 0));
        Assert.False(DodgeWslVerification.ShouldReobserve("motion-v1", "unknown", 0));
        Assert.True(DodgeWslVerification.ShouldReobserve("motion-v1", "unknown", 0, stepped: true));
        Assert.False(DodgeWslVerification.ShouldReobserve("motion-v1", "unknown", 2, stepped: true));
    }
}
