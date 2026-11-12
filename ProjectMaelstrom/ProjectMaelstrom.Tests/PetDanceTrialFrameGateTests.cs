using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceTrialFrameGateTests
{
    [Fact]
    public void BufferedFrameAfterOwnedPulseIsSkippedThenFreshGoCanSendNextKey()
    {
        // First Right already dispatched. Its bounded hold consumed 100ms while
        // capture retained an older surface; do not throw away the remaining R,U.
        var pending = new Queue<string>(["RIGHT", "UP"]);
        Assert.Equal(PetDanceFrameAdmission.Wait, PetDanceTrialFrameGate.Inspect(1010, 1000, 1190, 190, true));
        Assert.False(PetDanceTrialFrameGate.MayDispatch(1010, 1190));
        Assert.Equal(2, pending.Count);
        Assert.Equal(PetDanceFrameAdmission.Fresh, PetDanceTrialFrameGate.Inspect(1200, 1000, 1210, 210, true));
        var dispatch = PetDanceTrialFrameGate.Pending(DancePhase.Go, PetDanceTrialFrameGate.MayDispatch(1200, 1210), true);
        Assert.Equal(PetDanceInputContinuation.Dispatch, dispatch);
        Assert.Equal("RIGHT", pending.Dequeue());
        Assert.Equal("UP", pending.Peek());
    }

    [Theory]
    [InlineData(1000, 1150, true)]
    [InlineData(1000, 1150.001, false)]
    [InlineData(1151, 1150, false)]
    [InlineData(double.NaN, 1150, false)]
    public void EveryInputStillRequiresTheUnchanged150msFreshnessGate(double capture, double now, bool allowed)
        => Assert.Equal(allowed, PetDanceTrialFrameGate.MayDispatch(capture, now));

    [Fact]
    public void MissingRepeatedAndFutureFramesCannotRefreshTheWaitBudget()
    {
        Assert.Equal(PetDanceFrameAdmission.Wait, PetDanceTrialFrameGate.Inspect(null, 1000, 2000, 1000, true));
        Assert.Equal(PetDanceFrameAdmission.Wait, PetDanceTrialFrameGate.Inspect(1000, 1000, 2000, 1000, true));
        Assert.Equal(PetDanceFrameAdmission.Wait, PetDanceTrialFrameGate.Inspect(2100, 1000, 2000, 1000, true));
        Assert.Equal(PetDanceFrameAdmission.Wait, PetDanceTrialFrameGate.Inspect(1001, 1000, 5999, 4999, true));
        Assert.Equal(PetDanceFrameAdmission.Expired, PetDanceTrialFrameGate.Inspect(null, 1000, 6000, 5000, true));
        Assert.Equal(PetDanceFrameAdmission.Expired, PetDanceTrialFrameGate.Inspect(6000, 1000, 6000, 5000, true));
    }

    [Fact]
    public void PreArmIdleUsesTheSeparateStartupBudget()
        => Assert.Equal(PetDanceFrameAdmission.Wait, PetDanceTrialFrameGate.Inspect(1000, -1, 10000, 9000, false));

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Resolving")]
    [InlineData("Ready")]
    [InlineData("Failure")]
    [InlineData("Success")]
    public void TransientNonGoWithholdsKeysWithoutDemandingVisitTermination(string phase)
        => Assert.Equal(PetDanceInputContinuation.Wait, PetDanceTrialFrameGate.Pending(Enum.Parse<DancePhase>(phase), true, true));

    [Fact]
    public void FreshWatchDiscardsPriorResponseButStaleWatchCannotChangeIt()
    {
        Assert.Equal(PetDanceInputContinuation.DiscardForWatch, PetDanceTrialFrameGate.Pending(DancePhase.Watch, true, true));
        Assert.Equal(PetDanceInputContinuation.Wait, PetDanceTrialFrameGate.Pending(DancePhase.Watch, false, true));
        Assert.Equal(PetDanceInputContinuation.Wait, PetDanceTrialFrameGate.Pending(DancePhase.Go, true, false));
        Assert.Equal(PetDanceInputContinuation.Wait, PetDanceTrialFrameGate.Pending(DancePhase.Go, false, true));
    }

    [Fact]
    public void UnknownThenFreshGoRetainsAndContinuesTheSameResponse()
    {
        var pending = new Queue<string>(["RIGHT", "UP"]);
        Assert.Equal(PetDanceInputContinuation.Wait, PetDanceTrialFrameGate.Pending(DancePhase.Unknown, true, false));
        Assert.Equal(2, pending.Count);
        Assert.Equal(PetDanceInputContinuation.Wait, PetDanceTrialFrameGate.Pending(DancePhase.Resolving, true, true));
        Assert.Equal(2, pending.Count);
        Assert.Equal(PetDanceInputContinuation.Dispatch, PetDanceTrialFrameGate.Pending(DancePhase.Go, true, true));
        Assert.Equal("RIGHT", pending.Dequeue());
        Assert.Equal("UP", pending.Peek());
    }
}
