using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceTrialCleanupTests
{
    [Fact]
    public void MissingIdleWithConfirmedReleaseAndWorkerStopIsFailedNotInputRecovery()
    {
        var result = PetDanceTrialCleanupPolicy.MissingIdle(true, true, "Confirmation timed out.", "trial.json");
        Assert.Equal(OperationState.Failed, result.State);
        Assert.Contains("Native Idle unconfirmed; input released", result.Message);
        Assert.Equal(new[] { "trial.json" }, result.Artifacts);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void UnknownReleaseOrWorkerLifetimeStillRequiresRecovery(bool workers, bool released)
        => Assert.Equal(OperationState.CleanupFailed, PetDanceTrialCleanupPolicy.MissingIdle(workers, released, "Review needed.").State);

    [Theory]
    [InlineData("dispatched")]
    [InlineData("interrupted")]
    [InlineData("release-retried")]
    public void AConfirmedKeyupSurvivesUnsuccessfulGameplayOrTakeover(string status)
    {
        var evidence = new PetDanceTrialReleaseEvidence();
        evidence.Record(new(status, 1000, 1100));
        Assert.True(evidence.Confirmed);
    }

    [Theory]
    [InlineData("rejected")]
    [InlineData("busy")]
    [InlineData("invalid-action")]
    public void ExplicitNoInputRejectionsDoNotInventAHeldKey(string status)
    {
        var evidence = new PetDanceTrialReleaseEvidence();
        evidence.Record(new(status, null, null));
        Assert.True(evidence.Confirmed);
    }

    [Fact]
    public void MissingKeyupAndExceptionsLatchUncertainty()
    {
        var evidence = new PetDanceTrialReleaseEvidence();
        evidence.Record(new("release-failed", 1000, null));
        Assert.False(evidence.Confirmed);
        evidence.Record(new("dispatched", 1200, 1300));
        Assert.False(evidence.Confirmed);
        var thrown = new PetDanceTrialReleaseEvidence();
        thrown.MarkUncertain();
        thrown.Record(new("rejected", null, null));
        Assert.False(thrown.Confirmed);
    }

    [Fact]
    public void ContradictoryOrInvalidPulseEvidenceDoesNotClaimRelease()
    {
        foreach (var receipt in new[]
        {
            new DodgePulseResult("dispatched", null, null), new("dispatched", null, 1200),
            new("dispatched", 1200, 1100), new("dispatched", double.NaN, 1200),
            new("dispatched", 1200, double.PositiveInfinity), new("release-failed", 1000, 1100),
            new("unknown", 1000, 1100)
        })
        {
            var evidence = new PetDanceTrialReleaseEvidence();
            evidence.Record(receipt);
            Assert.False(evidence.Confirmed);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task SchedulerMarksRecoveryOnlyForUncertainCleanup(bool releaseConfirmed, bool expectedMarker)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Maelstrom-Dance-Cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await using (var scheduler = new OperationScheduler(directory, directory))
            {
                var request = new OperationRequest(Guid.NewGuid().ToString("N"), "test.dance-cleanup", "1", 10, true, "synthetic-no-input", new Dictionary<string, string>());
                var ticket = scheduler.Submit(request, (_, _) => Task.FromResult(
                    PetDanceTrialCleanupPolicy.MissingIdle(true, releaseConfirmed, "Synthetic confirmation timeout.")));
                var receipt = await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(releaseConfirmed ? OperationState.Failed : OperationState.CleanupFailed, receipt.State);
            }
            Assert.Equal(expectedMarker, File.Exists(Path.Combine(directory, "input-recovery-required.txt")));
        }
        finally { Directory.Delete(directory, true); }
    }
}
