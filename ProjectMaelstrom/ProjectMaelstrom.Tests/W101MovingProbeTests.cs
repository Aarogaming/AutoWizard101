using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101MovingProbeTests
{
    [Theory]
    [InlineData("moving-pose-ended")]
    [InlineData("moving-pose-invalidated")]
    public void TerminalEventRevokesFreshReadiness(string kind)
    {
        var ready = "{\"kind\":\"pose-window-ready\",\"observedUtcTicks\":10000000,\"safe\":true}\n";
        var terminal = "{\"kind\":\"" + kind + "\"}\n";
        Assert.False(W101MovingProbe.WindowReady(ready + terminal, 10000001));
        Assert.False(W101MovingProbe.WindowReady(ready + terminal.TrimEnd(), 10000001));
        Assert.True(W101MovingProbe.WindowReady(terminal + ready, 10000001));
    }

    [Theory]
    [InlineData("{bad}\n")]
    [InlineData("[]\n")]
    [InlineData("{\"kind\":7}\n")]
    [InlineData("{\"kind\":\"pose-window-ready\",\"observedUtcTicks\":\"bad\",\"safe\":true}\n")]
    public void MalformedCompleteEventsWithholdReadiness(string line) =>
        Assert.False(W101MovingProbe.WindowReady(line, 10000001));

    [Fact]
    public void ReadinessRequiresFreshCompleteSafeEvent()
    {
        const long stamp = 10000000;
        var line = "{\"kind\":\"pose-window-ready\",\"observedUtcTicks\":10000000,\"safe\":true}";
        Assert.False(W101MovingProbe.WindowReady(line, stamp));
        Assert.True(W101MovingProbe.WindowReady(line + "\n", stamp));
        Assert.False(W101MovingProbe.WindowReady(line + "\n", stamp - 1));
        Assert.False(W101MovingProbe.WindowReady(line + "\n", stamp + 1000001));
        Assert.False(W101MovingProbe.WindowReady(line.Replace("true", "false") + "\n", stamp));
    }

    [Fact]
    public async Task MoveWaitsForObservationAndJoinsCleanup()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool cleaned = false;
        var result = await W101MovingProbe.RunAsync(async _ => { await done.Task; cleaned = true; },
            () => true, () => true, () => { done.SetResult(); return Task.FromResult(new RoutineReceipt("p", "a", 1, true, true)); },
            new("p", "a", 0, false, true), CancellationToken.None);
        Assert.True(result.Accepted); Assert.True(cleaned);
    }

    [Fact]
    public async Task FinishedObserverDoesNotAuthorizeLateMovement()
    {
        int moves = 0;
        var result = await W101MovingProbe.RunAsync(_ => Task.CompletedTask, () => true, () => true,
            () => { moves++; return Task.FromResult(new RoutineReceipt("p", "a", 1, true, true)); },
            new("p", "a", 0, false, true), CancellationToken.None);
        Assert.False(result.Accepted); Assert.Equal(0, moves);
    }
}
