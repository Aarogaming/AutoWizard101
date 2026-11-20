using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101NavigationTransitionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public async Task WalkingReleasesOnLoadingOrUnknown(bool? next)
    {
        var values = new Queue<bool?>([false, next]);
        var released = false;
        var result = await W101NavigationTransition.WalkAsync(_ => Task.FromResult(values.Dequeue()), async continuing =>
        {
            while (continuing()) await Task.Delay(1);
            released = true;
            return new RoutineReceipt("p", "game.move-forward", 0, false, true);
        }, () => true, () => 0, default);
        Assert.True(released);
        Assert.Equal(next == true, result.SawLoading);
        Assert.True(result.Receipt.InputReleased);
    }

    [Fact]
    public async Task WalkingDoesNotBeginWithoutClearEvidence()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => W101NavigationTransition.WalkAsync(_ => Task.FromResult<bool?>(null),
            _ => throw new Exception("Must not dispatch"), () => true, () => 0, default));
    }

    [Fact]
    public async Task ObserverFailureStillReleasesMovement()
    {
        var count = 0; var released = false;
        await Assert.ThrowsAsync<IOException>(() => W101NavigationTransition.WalkAsync(_ =>
            ++count == 1 ? Task.FromResult<bool?>(false) : throw new IOException(), async continuing =>
        {
            while (continuing()) await Task.Delay(1);
            released = true;
            return new RoutineReceipt("p", "game.move-forward", 0, false, true);
        }, () => true, () => 0, default));
        Assert.True(released);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequiresTwoClearReadingsAndPreservesLoadingEvidence(bool loading)
    {
        var values = new Queue<bool?>(loading ? [true, null, false, true, false, false] : [false, false]);
        double time = 0;
        var result = await W101NavigationTransition.WaitAsync(_ => Task.FromResult(values.Dequeue()), () => true, () => time += 100, default);
        Assert.True(result.Ready);
        Assert.Equal(loading, result.SawLoading);
        Assert.Empty(values);
    }

    [Fact]
    public async Task HomeWaitDoesNotMistakeCastDelayForArrival()
    {
        var values = new Queue<bool?>([false, false, true, false, false]);
        double time = 0;
        var result = await W101NavigationTransition.WaitAsync(_ => Task.FromResult(values.Dequeue()), () => true, () => time += 100, default, requireLoading: true);
        Assert.True(result.Ready);
        Assert.True(result.SawLoading);
        Assert.Equal(5, result.Samples);
    }

    [Fact]
    public async Task StopsOnTakeoverAndNeverReadsAgain()
    {
        var owner = true;
        var result = await W101NavigationTransition.WaitAsync(_ => { owner = false; return Task.FromResult<bool?>(false); }, () => owner, () => 0, default);
        Assert.False(result.Ready);
        Assert.Equal("control-changed", result.Reason);
        Assert.Equal(1, result.Samples);
    }

    [Fact]
    public async Task UnknownAndPersistentLoadingHaveFixedDeadline()
    {
        double time = 0;
        var result = await W101NavigationTransition.WaitAsync(_ => Task.FromResult<bool?>(true), () => true, () => time += 5000, default);
        Assert.False(result.Ready);
        Assert.Equal("timed-out", result.Reason);
    }
}
