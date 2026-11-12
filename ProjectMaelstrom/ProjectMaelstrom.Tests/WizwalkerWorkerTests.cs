using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class WizwalkerWorkerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(60)]
    public void UiWatchLimitsAreAdmittedBeforeAWorkerCanOpenTheGame(int duration) =>
        WizwalkerWorker.ValidateLimits(duration + 18, W101UiTreeOperation.MaximumResultBytes);

    [Theory]
    [InlineData(0, 1000, "maximumSeconds")]
    [InlineData(91, 1000, "maximumSeconds")]
    [InlineData(1, 0, "maximumResultBytes")]
    [InlineData(1, 262145, "maximumResultBytes")]
    public void InvalidLimitsIdentifyTheActualParameter(int seconds, int bytes, string parameter) =>
        Assert.Equal(parameter, Assert.Throws<ArgumentOutOfRangeException>(() => WizwalkerWorker.ValidateLimits(seconds, bytes)).ParamName);
}
