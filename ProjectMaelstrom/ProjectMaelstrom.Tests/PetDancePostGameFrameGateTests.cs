using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDancePostGameFrameGateTests
{
    [Fact]
    public async Task RetainedEightMillisecondFutureFrameBecomesFreshWithoutRetimestamping()
    {
        var now = 1000d;
        var waits = new List<int>();
        var readyChecks = 0;
        var admission = await PetDancePostGameFrameGate.InspectAsync(true, 1008, 900, () => now,
            () => { readyChecks++; return now >= 1008; }, CancellationToken.None,
            (wait, _) => { waits.Add(wait); now += wait; return Task.CompletedTask; });
        Assert.True(admission.Accepted);
        Assert.Equal(new[] { 8 }, waits);
        Assert.Equal(1, readyChecks);
        Assert.Equal(1008, admission.CapturedMs);
        Assert.Equal(1008, admission.ObservedMs);
        Assert.Equal(0, admission.AgeMs);
        Assert.Equal(8, admission.AlignmentWaitMs);
    }

    [Theory]
    [InlineData(1031, 900, "future-timestamp")]
    [InlineData(900, 900, "repeated-timestamp")]
    [InlineData(899, 900, "repeated-timestamp")]
    [InlineData(699, 0, "stale-frame")]
    [InlineData(double.NaN, 0, "invalid-timestamp")]
    [InlineData(double.PositiveInfinity, 0, "invalid-timestamp")]
    [InlineData(-1, -2, "invalid-timestamp")]
    public async Task InvalidFutureRepeatedAndStaleFramesCannotReachOcr(double captured, double previous, string reason)
    {
        var waits = 0;
        var ocrCalls = 0;
        var admission = await PetDancePostGameFrameGate.InspectAsync(true, captured, previous, () => 1000,
            () => true, CancellationToken.None, (_, _) => { waits++; return Task.CompletedTask; });
        if (admission.Accepted) ocrCalls++;
        Assert.False(admission.Accepted);
        Assert.Equal(reason, admission.Reason);
        Assert.Equal(0, waits);
        Assert.Equal(0, ocrCalls);
        Assert.True(admission.CapturedMs is null || double.IsFinite(admission.CapturedMs.Value));
    }

    [Theory]
    [InlineData(1000d)]
    [InlineData(700d)]
    public async Task CurrentAndExactlyThreeHundredMillisecondFramesKeepStrictFreshness(double captured)
    {
        var waits = 0;
        var admission = await PetDancePostGameFrameGate.InspectAsync(true, captured, 0, () => 1000,
            () => true, CancellationToken.None, (_, _) => { waits++; return Task.CompletedTask; });
        Assert.True(admission.Accepted);
        Assert.Equal(captured, admission.CapturedMs);
        Assert.Equal(1000 - captured, admission.AgeMs);
        Assert.Equal(0, waits);
    }

    [Fact]
    public async Task FractionalAheadWaitsUpwardButKeepsTheOriginalCaptureTime()
    {
        var now = 1000d;
        var admission = await PetDancePostGameFrameGate.InspectAsync(true, 1000.25, 900, () => now,
            () => true, CancellationToken.None, (wait, _) => { now += wait; return Task.CompletedTask; });
        Assert.True(admission.Accepted);
        Assert.Equal(1, admission.AlignmentWaitMs);
        Assert.Equal(1000.25, admission.CapturedMs);
        Assert.Equal(.75, admission.AgeMs);
    }

    [Fact]
    public async Task WaitingDoesNotMakeAFutureFrameFreshIfTheClockHasNotReachedIt()
    {
        var admission = await PetDancePostGameFrameGate.InspectAsync(true, 1008, 900, () => 1000,
            () => true, CancellationToken.None, (_, _) => Task.CompletedTask);
        Assert.False(admission.Accepted);
        Assert.Equal("future-timestamp", admission.Reason);
        Assert.Equal(-8, admission.AgeMs);
        Assert.Equal(1008, admission.CapturedMs);
    }

    [Fact]
    public async Task CancellationDuringAlignmentCannotReachReadinessOcrOrInput()
    {
        using var stop = new CancellationTokenSource();
        var now = 1000d;
        var readyChecks = 0;
        var ocrCalls = 0;
        var inputCalls = 0;
        var admission = await PetDancePostGameFrameGate.InspectAsync(true, 1008, 900, () => now,
            () => { readyChecks++; return true; }, stop.Token,
            (wait, token) => { now += wait; stop.Cancel(); return Task.FromCanceled(token); });
        if (admission.Accepted) { ocrCalls++; inputCalls++; }
        Assert.False(admission.Accepted);
        Assert.Equal("cancelled", admission.Reason);
        Assert.Equal(0, readyChecks);
        Assert.Equal(0, ocrCalls);
        Assert.Equal(0, inputCalls);
    }

    [Fact]
    public async Task ActivityDuringAlignmentMustBeRecheckedBeforeAdmission()
    {
        var now = 1000d;
        var ready = true;
        var admission = await PetDancePostGameFrameGate.InspectAsync(true, 1008, 900, () => now,
            () => ready, CancellationToken.None,
            (wait, _) => { now += wait; ready = false; return Task.CompletedTask; });
        Assert.False(admission.Accepted);
        Assert.Equal("not-ready", admission.Reason);
        Assert.Equal(1008, admission.CapturedMs);
        Assert.Equal(8, admission.AlignmentWaitMs);
    }

    [Fact]
    public async Task AbsentFramesAndAbsentTimestampsHaveDistinctDiagnosticReasons()
    {
        var noFrame = await PetDancePostGameFrameGate.InspectAsync(false, null, -1, () => 1000, () => true, CancellationToken.None);
        var noTimestamp = await PetDancePostGameFrameGate.InspectAsync(true, null, -1, () => 1000, () => true, CancellationToken.None);
        Assert.Equal("missing-frame", noFrame.Reason);
        Assert.Equal("missing-timestamp", noTimestamp.Reason);
        Assert.False(noFrame.Accepted);
        Assert.False(noTimestamp.Accepted);
        Assert.Null(noFrame.AgeMs);
        Assert.Null(noTimestamp.AgeMs);
    }
}
