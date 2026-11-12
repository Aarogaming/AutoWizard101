using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class LoadingTransitionGateTests
{
    [Fact]
    public void LoadingClearsAndResumesOnlyWithFreshDestinationEvidence()
    {
        var gate = new LoadingTransitionGate(0, 10000);
        Assert.Equal("paused-for-loading", gate.Observe(1, true, false, true));
        Assert.Equal("waiting-for-evidence", gate.Observe(2, null, false, true));
        Assert.Equal("confirming-destination", gate.Observe(3, false, true, true));
        Assert.Equal("ready", gate.Observe(4, false, true, true));
    }

    [Fact]
    public void RepeatedLoadingDoesNotExtendTheDeadline()
    {
        var gate = new LoadingTransitionGate(0, 100);
        gate.Observe(99, true, false, true);
        Assert.Equal("timed-out", gate.Observe(100, false, true, true));
    }

    [Fact]
    public void StaleFramesAndNewLoadingInvalidatePartialConfirmation()
    {
        var gate = new LoadingTransitionGate(0, 100);
        gate.Observe(1, false, true, true);
        gate.Observe(1, false, true, true);
        Assert.Equal("confirming-destination", gate.Observe(2, false, true, true));
        gate.Observe(3, true, false, true);
        Assert.Equal("confirming-destination", gate.Observe(4, false, true, true));
        Assert.Equal("stopped", gate.Observe(5, false, true, true, cancelled: true));
    }
}
