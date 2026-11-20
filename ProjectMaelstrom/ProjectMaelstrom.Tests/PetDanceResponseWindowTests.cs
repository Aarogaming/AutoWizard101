using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceResponseWindowTests
{
    private static PetDanceResponseWindow Started()
    {
        var window = new PetDanceResponseWindow();
        window.Observe(100, DancePhase.Watch);
        window.Observe(1000, DancePhase.Go);
        return window;
    }

    [Theory]
    [InlineData(3, 740)]
    [InlineData(4, 1060)]
    [InlineData(5, 1380)]
    [InlineData(6, 1700)]
    [InlineData(7, 2020)]
    public void WholeSequencesFitNominalFiveSecondResponse(int keys, double duration)
    {
        var window = Started();
        var estimate = window.Estimate(1000, keys);
        Assert.True(estimate.Started);
        Assert.True(estimate.CanFinish);
        Assert.False(estimate.Expired);
        Assert.Equal(duration, estimate.ScheduleMs);
        Assert.Equal(1000 + duration, estimate.EstimatedCompleteQpcMs);
        Assert.Equal(5000, estimate.RemainingMs);
    }

    [Fact]
    public void RepeatedGoAndUnknownNeverExtendSourceAnchoredDeadline()
    {
        var window = Started();
        window.Observe(2000, DancePhase.Unknown);
        window.Observe(3000, DancePhase.Go);
        window.Observe(4000, DancePhase.Resolving);
        window.Observe(5000, DancePhase.Go);
        Assert.Equal(1000, window.GoQpcMs);
        Assert.Equal(6000, window.DeadlineQpcMs);
        Assert.Equal(1000, window.Estimate(5000, 1).RemainingMs);
    }

    [Fact]
    public void StaleFrameDelayConsumesBudgetWithoutAnyNewObservation()
    {
        var window = Started();
        Assert.True(window.Estimate(1000, 7).CanFinish);
        var delayed = window.Estimate(4500, 7);
        Assert.False(delayed.CanFinish);
        Assert.False(delayed.Expired);
        Assert.Equal(1500, delayed.RemainingMs);
        Assert.True(window.Estimate(6001, 7).Expired);
        Assert.Equal(6000, window.DeadlineQpcMs);
    }

    [Fact]
    public void ExactDeadlineCompletionFitsButNoNewKeyCanStartAtDeadline()
    {
        var window = Started();
        Assert.True(window.Estimate(5900, 1).CanFinish);
        Assert.False(window.Estimate(5900.001, 1).CanFinish);
        var expired = window.Estimate(6000, 1);
        Assert.True(expired.Expired);
        Assert.False(expired.CanFinish);
        window.Complete(6000);
        Assert.True(window.CompletedWithinBudget);
        Assert.Equal(5000, window.CompletionDurationMs);
    }

    [Fact]
    public void ExistingKeyCooldownCountsTowardRemainingSchedule()
    {
        var window = Started();
        var estimate = window.Estimate(1500, 2, 1620);
        Assert.Equal(540, estimate.ScheduleMs);
        Assert.Equal(2040, estimate.EstimatedCompleteQpcMs);
        Assert.False(window.Estimate(5500, 2, 5700).CanFinish);
    }

    [Theory]
    [InlineData("Loading")]
    [InlineData("Ready")]
    [InlineData("Success")]
    [InlineData("Failure")]
    public void ExplicitRoundBoundariesRequireAnotherWatchBeforeGo(string phase)
    {
        var window = Started();
        window.Observe(1200, Enum.Parse<DancePhase>(phase));
        window.Observe(1300, DancePhase.Go);
        Assert.Null(window.GoQpcMs);
        Assert.False(window.Estimate(1300, 3).CanFinish);
        window.Observe(1400, DancePhase.Watch);
        window.Observe(1500, DancePhase.Watch);
        window.Observe(2000, DancePhase.Go);
        Assert.Equal(2000, window.GoQpcMs);
        Assert.Equal(7000, window.DeadlineQpcMs);
    }

    [Fact]
    public void NewWatchAfterGoClearsPriorRoundWithoutReusingItsDeadline()
    {
        var window = Started();
        window.Complete(1740);
        window.Observe(2000, DancePhase.Watch);
        Assert.Null(window.GoQpcMs);
        Assert.Null(window.CompletedQpcMs);
        window.Observe(2100, DancePhase.Unknown);
        window.Observe(2200, DancePhase.Watch);
        window.Observe(3000, DancePhase.Go);
        Assert.Equal(8000, window.DeadlineQpcMs);
    }

    [Fact]
    public void GoWithoutWatchCannotCreateInputTimingAuthority()
    {
        var window = new PetDanceResponseWindow();
        window.Observe(0, DancePhase.Go);
        Assert.Null(window.GoQpcMs);
        Assert.False(window.Estimate(0, 3).CanFinish);
        Assert.Throws<InvalidOperationException>(() => window.Complete(1));
    }

    [Fact]
    public void CompletionRecordsActualLatenessAndCannotBeOverwritten()
    {
        var window = Started();
        window.Complete(6100);
        Assert.False(window.CompletedWithinBudget);
        Assert.Equal(5100, window.CompletionDurationMs);
        Assert.Throws<InvalidOperationException>(() => window.Complete(6200));
        Assert.False(window.Estimate(6200, 0).CanFinish);
        Assert.Equal(0, window.Estimate(6200, 0).ScheduleMs);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    public void InvalidTimesCannotMutateState(double invalid)
    {
        var window = Started();
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Observe(invalid, DancePhase.Loading));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Estimate(invalid, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Estimate(1000, 3, invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Complete(invalid));
        Assert.Equal(6000, window.DeadlineQpcMs);
    }

    [Fact]
    public void BackwardsSourceOrCurrentTimeAndInvalidCountsAreRejected()
    {
        var window = Started();
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Observe(1000, DancePhase.Loading));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Observe(999, DancePhase.Loading));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Estimate(999, 3));
        window.Estimate(2000, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Estimate(1999, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Estimate(2000, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Estimate(2000, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Observe(2000, (DancePhase)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Complete(999));
        Assert.Equal(6000, window.DeadlineQpcMs);
    }
}
