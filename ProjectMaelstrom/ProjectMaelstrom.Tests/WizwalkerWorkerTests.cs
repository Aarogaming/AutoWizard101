using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class WizwalkerWorkerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(60)]
    public void UiWatchLimitsAreAdmittedBeforeAWorkerCanOpenTheGame(int duration) =>
        WizwalkerWorker.ValidateLimits(duration + W101UiTreeOperation.StartupAllowanceSeconds,
            W101UiTreeOperation.MaximumResultBytes, W101UiTreeOperation.CooperativeStopSeconds);

    [Theory]
    [InlineData(1, 3)]
    [InlineData(60, 10)]
    public void CooperativeStopGraceIsBoundedAndAdmitted(int seconds, int grace) =>
        WizwalkerWorker.ValidateLimits(seconds, W101UiTreeOperation.MaximumResultBytes, grace);

    [Theory]
    [InlineData(0, "cooperativeStopSeconds")]
    [InlineData(11, "cooperativeStopSeconds")]
    public void InvalidCooperativeStopGraceIdentifiesTheParameter(int grace, string parameter) =>
        Assert.Equal(parameter, Assert.Throws<ArgumentOutOfRangeException>(
            () => WizwalkerWorker.ValidateLimits(18, W101UiTreeOperation.MaximumResultBytes, grace)).ParamName);

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(60)]
    public void OperationDeadlineOutlastsWorkerBudgetAndForcedStopGrace(int duration)
    {
        var workerBudget = duration + W101UiTreeOperation.StartupAllowanceSeconds;
        var worstCase = workerBudget + W101UiTreeOperation.CooperativeStopSeconds;
        var deadline = duration + W101UiTreeOperation.TimeoutAllowanceSeconds;
        Assert.True(worstCase < deadline, $"duration={duration}: forced stop must precede the scheduler deadline");
        WizwalkerWorker.ValidateLimits(workerBudget, W101UiTreeOperation.MaximumResultBytes,
            W101UiTreeOperation.CooperativeStopSeconds);
    }

    [Theory]
    [InlineData(0, 1000, "maximumSeconds")]
    [InlineData(91, 1000, "maximumSeconds")]
    [InlineData(1, 0, "maximumResultBytes")]
    [InlineData(1, 262145, "maximumResultBytes")]
    public void InvalidLimitsIdentifyTheActualParameter(int seconds, int bytes, string parameter) =>
        Assert.Equal(parameter, Assert.Throws<ArgumentOutOfRangeException>(() => WizwalkerWorker.ValidateLimits(seconds, bytes)).ParamName);
}
