using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101BackgroundReaderTests
{
    private static W101Fact Fact(string id, string value) =>
        new(id, value, "w101.resources", 0, "r15-archived", W101FactQualification.Candidate);

    private static W101BackgroundReader Reader(Func<(bool, string)> validate,
        Func<CancellationToken, Task<IReadOnlyList<W101Fact>>> read,
        Action<string> invalidate, Func<bool>? recovery = null, int cycles = 4) =>
        new(validate, read, invalidate, recovery,
            utcTicks: () => 639000000000000000,
            delay: (_, _) => Task.CompletedTask,
            maximumCycles: cycles);

    [Fact]
    public async Task NoForegroundGate_PublishesFromMemoryReader()
    {
        var published = new List<W101Fact>();
        var invalidations = new List<string>();
        var reader = Reader(() => (true, ""),
            _ => Task.FromResult<IReadOnlyList<W101Fact>>([Fact("health", "4920")]),
            invalidations.Add);
        var reason = await reader.RunAsync(published.Add, CancellationToken.None);
        Assert.Equal("cycle-budget", reason);
        Assert.Equal(4, published.Count);
        Assert.All(published, f => Assert.Equal(639000000000000000, f.ObservedUtcTicks));
        Assert.Empty(invalidations);
    }

    [Fact]
    public async Task IdentityChangeInvalidatesAndStopsImmediately()
    {
        var calls = 0;
        var invalidations = new List<string>();
        var reader = Reader(() => ++calls == 1 ? (true, "") : (false, "process-restarted"),
            _ => Task.FromResult<IReadOnlyList<W101Fact>>([Fact("health", "4920")]),
            invalidations.Add);
        var published = new List<W101Fact>();
        var reason = await reader.RunAsync(published.Add, CancellationToken.None);
        Assert.Equal("target-changed", reason);
        Assert.Single(published);
        Assert.Equal(["process-restarted"], invalidations);
    }

    [Fact]
    public async Task RecoveryHoldBlocksPublication()
    {
        var invalidations = new List<string>();
        var reader = Reader(() => (true, ""),
            _ => Task.FromResult<IReadOnlyList<W101Fact>>([Fact("health", "1")]),
            invalidations.Add, recovery: () => true);
        var published = new List<W101Fact>();
        Assert.Equal("recovery-hold", await reader.RunAsync(published.Add, CancellationToken.None));
        Assert.Empty(published);
        Assert.Equal(["recovery-hold"], invalidations);
    }

    [Fact]
    public async Task ReaderFaultInvalidatesAllFacts()
    {
        var invalidations = new List<string>();
        var reader = Reader(() => (true, ""),
            _ => throw new IOException("hook restoration unconfirmed"),
            invalidations.Add);
        Assert.Equal("reader-fault", await reader.RunAsync(_ => { }, CancellationToken.None));
        Assert.Equal(["reader-fault"], invalidations);
    }

    [Fact]
    public async Task CancellationStopsWithoutRetry()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var invalidations = new List<string>();
        var reader = Reader(() => (true, ""),
            _ => Task.FromResult<IReadOnlyList<W101Fact>>([Fact("health", "1")]),
            invalidations.Add);
        var published = new List<W101Fact>();
        Assert.Equal("cancelled", await reader.RunAsync(published.Add, cts.Token));
        Assert.Empty(published);
    }

    [Fact]
    public void BoundsAreValidated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Reader(() => (true, ""),
            _ => Task.FromResult<IReadOnlyList<W101Fact>>([]), _ => { }, cycles: 0));
    }
}
