using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceIdleConfirmationTests
{
    [Theory]
    [InlineData(0, 120)]
    [InlineData(60000, 60)]
    [InlineData(60001, 60)]
    [InlineData(119999, 1)]
    [InlineData(120000, 0)]
    [InlineData(180000, 0)]
    public void ConfirmationCoversDelayedMenusButExpiresExactlyAtItsBound(double elapsed, int expected)
        => Assert.Equal(expected, PetDanceIdleConfirmation.RemainingSeconds(1000, 1000 + elapsed));

    [Theory]
    [InlineData(double.NaN, 1000)]
    [InlineData(1000, double.PositiveInfinity)]
    [InlineData(-1, 0)]
    [InlineData(1000, 999)]
    public void InvalidOrBackwardClockCannotExtendTheHandoff(double started, double now)
        => Assert.Equal(0, PetDanceIdleConfirmation.RemainingSeconds(started, now));

    [Fact]
    public void SchedulerAllowsTheSeparateGameplayLoadingAndIdleBudgets()
        => Assert.True(PetDanceIdleConfirmation.OperationTimeoutSeconds > 75 + 45 + 45 + PetDanceIdleConfirmation.TimeoutSeconds);

    [Fact]
    public void OnlyCompletedReceiptsReportSuccessfulProcessExit()
    {
        foreach (var state in Enum.GetValues<OperationState>())
            Assert.Equal(state == OperationState.Completed ? 0 : 1, PetDanceIdleConfirmation.ExitCode(state));
    }
}
