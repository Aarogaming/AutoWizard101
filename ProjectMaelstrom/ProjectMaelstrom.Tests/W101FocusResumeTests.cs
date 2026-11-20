using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101FocusResumeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "focus-resume-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("focus", true, 1)]
    [InlineData("keyboard", false, 1)]
    [InlineData("recovery", false, 0)]
    [InlineData("cleanup-failed", false, 0)]
    [InlineData("cancelled", false, 0)]
    [InlineData("timed-out", false, 0)]
    [InlineData("already-ready", false, 0)]
    public async Task PartialPulseCanOnlyResumeToObservationWithConfirmedOwnership(string scenario, bool expected, int expectedWaits)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var owner = GameInputLease.EnterScope(lease);
        var context = new OperationContext(new("run", "route", "v1", 60, true, "pid-123-start-456", new Dictionary<string, string>()), _root, lease);
        var ready = scenario == "already-ready";
        var waits = 0;
        if (scenario == "recovery") lease.RequireRecovery("synthetic unconfirmed key-up");
        var state = scenario switch
        {
            "cleanup-failed" => OperationState.CleanupFailed,
            "cancelled" => OperationState.Cancelled,
            "timed-out" => OperationState.TimedOut,
            _ => OperationState.Failed
        };
        var result = await W101FeedbackVisit.ResumeReleasedAsync(context, new(state, "synthetic released outcome", []), () => ready,
            _ => { waits++; ready = scenario == "focus"; return Task.FromResult(ready); }, default);
        Assert.Equal(expected, result);
        Assert.Equal(expectedWaits, waits);
    }

    [Fact]
    public async Task FocusReturnCannotBypassNewCleanupLatch()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var owner = GameInputLease.EnterScope(lease);
        var context = new OperationContext(new("run", "route", "v1", 60, true, "pid-123-start-456", new Dictionary<string, string>()), _root, lease);
        var result = await W101FeedbackVisit.ResumeReleasedAsync(context, new(OperationState.Failed, "released", []), () => false,
            _ => { lease.RequireRecovery("new uncertainty"); return Task.FromResult(true); }, default);
        Assert.False(result);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
