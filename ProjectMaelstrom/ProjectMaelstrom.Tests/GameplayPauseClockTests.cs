using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class GameplayPauseClockTests
{
    [Fact]
    public void TwentySecondLoadPreservesTheFullGameplayBudget()
    {
        var clock = new GameplayPauseClock();
        clock.Advance(0, loading: true, gameplayObserved: false);
        Assert.False(clock.Started);
        Assert.False(clock.GameplayAllowed);

        clock.Advance(20000, loading: false, gameplayObserved: true);
        Assert.True(clock.Started);
        Assert.True(clock.GameplayAllowed);
        Assert.Equal(20000, clock.PausedMs);
        Assert.Equal(0, clock.ActiveMs);

        clock.Advance(94999, loading: false, gameplayObserved: true);
        Assert.True(clock.GameplayAllowed);
        clock.Advance(95000, loading: false, gameplayObserved: true);
        Assert.Equal(75000, clock.ActiveMs);
        Assert.Equal("game-duration-limit", clock.StopReason);
        Assert.False(clock.GameplayAllowed);
    }

    [Fact]
    public void MultipleLoadingPeriodsAccumulateAndNeverResetEitherBudget()
    {
        var clock = new GameplayPauseClock(pauseBudgetMs: 10000);
        clock.Advance(0, loading: false, gameplayObserved: true);
        clock.Advance(1000, loading: true, gameplayObserved: false);
        Assert.Equal(1000, clock.ActiveMs);
        Assert.False(clock.GameplayAllowed);
        clock.Advance(4000, loading: false, gameplayObserved: true);
        Assert.Equal(3000, clock.PausedMs);
        clock.Advance(5000, loading: true, gameplayObserved: false);
        clock.Advance(11999, loading: true, gameplayObserved: false);
        Assert.Null(clock.StopReason);
        Assert.Equal(2000, clock.ActiveMs);
        clock.Advance(12000, loading: false, gameplayObserved: true);
        Assert.Equal(10000, clock.PausedMs);
        Assert.Equal("loading-wait-limit", clock.StopReason);
        Assert.False(clock.GameplayAllowed);
    }

    [Fact]
    public void UnknownBeforeGameplayWaitsAndUnknownAfterGameplayConsumesActiveTime()
    {
        var clock = new GameplayPauseClock();
        clock.Advance(0, loading: false, gameplayObserved: false);
        clock.Advance(5000, loading: false, gameplayObserved: true);
        Assert.Equal(5000, clock.PausedMs);
        clock.Advance(6000, loading: false, gameplayObserved: false);
        Assert.True(clock.Started);
        Assert.False(clock.IsLoading);
        Assert.False(clock.GameplayAllowed);
        clock.Advance(8000, loading: false, gameplayObserved: false);
        Assert.Equal(3000, clock.ActiveMs);
        Assert.Equal(5000, clock.PausedMs);
        clock.Advance(9000, loading: false, gameplayObserved: true);
        Assert.True(clock.GameplayAllowed);
        Assert.Equal(4000, clock.ActiveMs);
    }

    [Theory]
    [InlineData(false, "game-start-not-observed")]
    [InlineData(true, "loading-wait-limit")]
    public void MissingFramesCannotPreventStartupDeadline(bool lastSceneLoading, string reason)
    {
        var clock = new GameplayPauseClock();
        clock.Advance(0, loading: lastSceneLoading, gameplayObserved: false);
        // No new images are needed to account for wall time in the latest scene.
        clock.Advance(44999, loading: lastSceneLoading, gameplayObserved: false);
        Assert.Null(clock.StopReason);
        clock.Advance(45000, loading: lastSceneLoading, gameplayObserved: false);
        Assert.Equal(reason, clock.StopReason);
        Assert.False(clock.Started);
        Assert.False(clock.GameplayAllowed);
    }

    [Fact]
    public void MissingFramesAfterStartDoNotPauseTheActiveBudget()
    {
        var clock = new GameplayPauseClock();
        clock.Advance(0, loading: false, gameplayObserved: true);
        clock.Advance(1000, loading: false, gameplayObserved: false);
        clock.Advance(75000, loading: false, gameplayObserved: false);
        Assert.Equal("game-duration-limit", clock.StopReason);
        Assert.Equal(75000, clock.ActiveMs);
        Assert.Equal(0, clock.PausedMs);
        Assert.False(clock.GameplayAllowed);
    }

    [Fact]
    public void ExpiredClockCannotResumeOrChangeItsStopReason()
    {
        var clock = new GameplayPauseClock(pauseBudgetMs: 10, gameplayBudgetMs: 10);
        clock.Advance(0, loading: true, gameplayObserved: false);
        clock.Advance(10, loading: false, gameplayObserved: true);
        clock.Advance(30, loading: false, gameplayObserved: true);
        Assert.True(clock.Started);
        Assert.Equal("loading-wait-limit", clock.StopReason);
        Assert.False(clock.GameplayAllowed);
    }

    [Fact]
    public void FirstNonzeroCallAccountsForElapsedStartupTime()
    {
        var clock = new GameplayPauseClock();
        clock.Advance(5000, loading: false, gameplayObserved: true);
        Assert.Equal(5000, clock.PausedMs);
        Assert.Equal(0, clock.ActiveMs);
        Assert.True(clock.GameplayAllowed);
    }

    [Fact]
    public void ReadyAtStartupDeadlineCannotForgiveExpiredUnknownWait()
    {
        var clock = new GameplayPauseClock();
        clock.Advance(0, loading: false, gameplayObserved: false);
        clock.Advance(45000, loading: false, gameplayObserved: true);
        Assert.Equal("game-start-not-observed", clock.StopReason);
        Assert.False(clock.GameplayAllowed);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidInitialTimeIsRejectedWithoutChangingState(double time)
    {
        var clock = new GameplayPauseClock();
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(time, false, true));
        Assert.False(clock.Started);
        Assert.Equal(0, clock.ActiveMs);
        Assert.Equal(0, clock.PausedMs);
        clock.Advance(0, loading: false, gameplayObserved: true);
        Assert.True(clock.GameplayAllowed);
    }

    [Fact]
    public void DuplicateAndReversedTimesAreRejectedWithoutChangingLatestScene()
    {
        var clock = new GameplayPauseClock();
        clock.Advance(0, loading: false, gameplayObserved: true);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(0, true, false));
        clock.Advance(10, loading: false, gameplayObserved: true);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(9, true, false));
        Assert.True(clock.GameplayAllowed);
        Assert.False(clock.IsLoading);
        Assert.Equal(10, clock.ActiveMs);
        clock.Advance(20, loading: false, gameplayObserved: true);
        Assert.Equal(20, clock.ActiveMs);
    }

    [Fact]
    public void ContradictorySceneIsRejectedBeforeAnyTimeOrStateChanges()
    {
        var clock = new GameplayPauseClock();
        clock.Advance(0, loading: true, gameplayObserved: false);
        Assert.Throws<ArgumentException>(() => clock.Advance(10, true, true));
        Assert.True(clock.IsLoading);
        Assert.False(clock.Started);
        Assert.Equal(0, clock.PausedMs);
        clock.Advance(10, loading: false, gameplayObserved: true);
        Assert.Equal(10, clock.PausedMs);
        Assert.True(clock.GameplayAllowed);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(double.NaN, 1)]
    [InlineData(1, double.NaN)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(1, double.PositiveInfinity)]
    public void BudgetsMustBeFiniteAndPositive(double paused, double active)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameplayPauseClock(paused, active));
    }
}
