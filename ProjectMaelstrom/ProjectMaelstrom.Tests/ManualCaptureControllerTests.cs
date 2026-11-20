using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class ManualCaptureControllerTests
{
    private static ManualCaptureController New(ManualCaptureMode mode, bool verified = true, bool recovery = false) =>
        new(mode, () => verified, () => recovery);

    [Fact]
    public void NormalStartsDispatchingWhenVerified()
    {
        var c = New(ManualCaptureMode.Normal);
        c.Start(0);
        Assert.True(c.MayDispatch(0));
        Assert.Equal(ManualCaptureState.Running, c.Snapshot(0).State);
    }

    [Fact]
    public void NormalAutoResumesOnlyAfterQuietAndCountdown()
    {
        var c = New(ManualCaptureMode.Normal);
        c.Start(0);
        c.ImplicitPause(ManualPauseTrigger.MouseActivity, 100);
        c.Observe(100 + ManualCaptureController.QuietRequiredMs - 1);
        Assert.Equal(ManualCaptureState.ImplicitlyPaused, c.Snapshot(0).State);

        c.Observe(100 + ManualCaptureController.QuietRequiredMs);
        Assert.Equal(ManualCaptureState.ResumePending, c.Snapshot(0).State);
        Assert.False(c.MayDispatch(100 + ManualCaptureController.QuietRequiredMs));

        c.Observe(100 + ManualCaptureController.QuietRequiredMs + ManualCaptureController.ResumeCountdownMs - 1);
        Assert.False(c.MayDispatch(0));

        c.Observe(100 + ManualCaptureController.QuietRequiredMs + ManualCaptureController.ResumeCountdownMs);
        Assert.Equal(ManualCaptureState.Running, c.Snapshot(0).State);
        Assert.True(c.MayDispatch(0));
    }

    [Fact]
    public void ExplicitPauseIsALatchThatObserveCannotClear()
    {
        var c = New(ManualCaptureMode.Normal);
        c.Start(0);
        c.ExplicitPause(10);
        for (var t = 10.0; t < 60_000; t += 500)
        {
            c.Observe(t);
            Assert.False(c.MayDispatch(t));
        }
        Assert.True(c.Snapshot(0).ExplicitLatch);
        Assert.Equal(ManualCaptureState.ExplicitlyPaused, c.Snapshot(0).State);
        Assert.True(c.Resume(60_000), "explicit pause may be cleared by an explicit resume");
        Assert.True(c.MayDispatch(60_000));
    }

    [Fact]
    public void SafeNeverAutoResumesWithoutExplicitResume()
    {
        var c = New(ManualCaptureMode.Safe);
        c.Start(0);
        c.ImplicitPause(ManualPauseTrigger.FocusLost, 10);
        c.Observe(10 + ManualCaptureController.QuietRequiredMs);
        c.Observe(10 + ManualCaptureController.QuietRequiredMs + ManualCaptureController.ResumeCountdownMs);
        Assert.Equal(ManualCaptureState.ImplicitlyPaused, c.Snapshot(0).State);
        Assert.False(c.MayDispatch(0));
        Assert.True(c.Resume(20_000));
        Assert.True(c.MayDispatch(20_000));
    }

    [Fact]
    public void HardStopCannotBeClearedByResumeOrObserve()
    {
        var c = New(ManualCaptureMode.Normal);
        c.Start(0);
        c.Stop(10);
        for (var t = 10.0; t < 60_000; t += 250)
        {
            c.Observe(t);
            Assert.False(c.MayDispatch(t));
            Assert.False(c.Resume(t));
        }
        Assert.Equal(ManualCaptureState.Stopped, c.Snapshot(0).State);
    }

    [Fact]
    public void NewActivityDuringCountdownRestartsQuietWindow()
    {
        var c = New(ManualCaptureMode.Normal);
        c.Start(0);
        c.ImplicitPause(ManualPauseTrigger.MouseActivity, 100);
        c.Observe(3100);
        Assert.Equal(ManualCaptureState.ResumePending, c.Snapshot(0).State);
        c.ImplicitPause(ManualPauseTrigger.MouseActivity, 3200);
        Assert.Equal(ManualCaptureState.ImplicitlyPaused, c.Snapshot(0).State);
        c.Observe(3200 + ManualCaptureController.QuietRequiredMs);
        Assert.Equal(ManualCaptureState.ResumePending, c.Snapshot(0).State);
        Assert.False(c.MayDispatch(0));
    }

    [Fact]
    public void VerificationFailureRestartsQuietAndBlocksResume()
    {
        var verified = true;
        var recovery = false;
        var c = new ManualCaptureController(ManualCaptureMode.Normal, () => verified, () => recovery);
        c.Start(0);
        c.ImplicitPause(ManualPauseTrigger.Loading, 100);
        verified = false;
        c.Observe(10_000);
        Assert.Equal(ManualCaptureState.ImplicitlyPaused, c.Snapshot(0).State);
        Assert.Equal("verification-withheld", c.Snapshot(0).Reason);

        verified = true;
        c.Observe(10_000 + 100);
        Assert.True(c.Snapshot(0).State == ManualCaptureState.ImplicitlyPaused, "quiet window must restart after failure");
        c.Observe(10_000 + 100 + ManualCaptureController.QuietRequiredMs);
        Assert.Equal(ManualCaptureState.ResumePending, c.Snapshot(0).State);

        recovery = true;
        c.Observe(20_000);
        Assert.Equal(ManualCaptureState.ImplicitlyPaused, c.Snapshot(0).State);
        Assert.False(c.MayDispatch(0));
    }

    [Fact]
    public void LoadingAndFocusTriggersCloseTheGate()
    {
        var c = New(ManualCaptureMode.Normal);
        c.Start(0);
        c.ImplicitPause(ManualPauseTrigger.Loading, 10);
        Assert.False(c.MayDispatch(10));
        Assert.Equal("loading", c.Snapshot(10).Reason);
        c.Resume(20);
        c.ImplicitPause(ManualPauseTrigger.FocusLost, 30);
        Assert.False(c.MayDispatch(30));
        Assert.Equal("focus-lost", c.Snapshot(30).Reason);
    }

    [Fact]
    public void NoDispatchBeforeStartOrAfterStop()
    {
        var c = New(ManualCaptureMode.Normal);
        Assert.False(c.MayDispatch(0));
        c.Start(0);
        Assert.True(c.MayDispatch(0));
        c.Stop(1);
        Assert.False(c.MayDispatch(1));
    }
}
