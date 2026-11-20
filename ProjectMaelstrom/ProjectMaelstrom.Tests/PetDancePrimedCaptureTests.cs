using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDancePrimedCaptureTests
{
    [Theory]
    [InlineData(2101)]
    [InlineData(99)]
    [InlineData(double.NaN)]
    public async Task FrameArrivalCannotOverrunOrInvalidatePreparationClock(double arrivalTime)
    {
        var clock = 100d;
        var called = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => PetDancePrimedCapture.StartAsync(() =>
        {
            clock = arrivalTime;
            // A freshly timestamped image does not renew the preparation budget.
            return Task.FromResult<CapturedWindowFrame?>(new(new Bitmap(2, 2), TimeSpan.FromMilliseconds(double.IsFinite(clock) ? clock : 100)));
        }, () => true, _ =>
        {
            called = true;
            return Task.FromResult(new PetDanceStartReceipt(true, true));
        }, () => clock, CancellationToken.None));
        Assert.False(called);
    }

    [Fact]
    public async Task FrameThatAgesBeforeScheduledPlayCannotDispatch()
    {
        var clockReads = 0;
        var called = false;
        using var capture = await PetDancePrimedCapture.StartAsync(
            () => Task.FromResult<CapturedWindowFrame?>(new(new Bitmap(2, 2), TimeSpan.FromMilliseconds(100))),
            () => true, _ => { called = true; return Task.FromResult(new PetDanceStartReceipt(true, true)); },
            () => Interlocked.Increment(ref clockReads) <= 3 ? 110 : 400, CancellationToken.None);
        for (var i = 0; i < 100 && capture.Pending; i++) await Task.Delay(10);
        Assert.False(capture.Pending);
        Assert.False(called);
        Assert.False(capture.Accepted);
        Assert.True(await capture.FinishAsync());
    }

    [Fact]
    public async Task FreshCapturePrecedesPlayAndObservationDoesNotAwaitItsReceipt()
    {
        var dispatched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<PetDanceStartReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);
        var frames = 0;
        using var capture = await PetDancePrimedCapture.StartAsync(() =>
        {
            frames++;
            return Task.FromResult<CapturedWindowFrame?>(new(new Bitmap(2, 2), TimeSpan.FromMilliseconds(100)));
        }, () => true, async token =>
        {
            Assert.Equal(1, frames); dispatched.SetResult();
            return await released.Task.WaitAsync(token);
        }, () => 110, CancellationToken.None);
        await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(capture.Pending);
        Assert.False(capture.Accepted);
        // The caller is already back in its frame loop while Play finishes.
        capture.CheckDispatch();
        released.SetResult(new(true, true));
        for (var i = 0; i < 100 && capture.Pending; i++) await Task.Delay(10);
        Assert.True(capture.Accepted);
        Assert.True(await capture.FinishAsync());
    }

    [Fact]
    public async Task MissingReadinessCannotDispatchPlay()
    {
        var called = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => PetDancePrimedCapture.StartAsync(
            () => Task.FromResult<CapturedWindowFrame?>(null), () => false,
            token => { called = true; return Task.FromResult(new PetDanceStartReceipt(true, true)); }, () => 100, CancellationToken.None));
        Assert.False(called);
    }

    [Fact]
    public async Task RejectedPlayNeverAdmitsInputAndCleanupIsAwaited()
    {
        using var capture = await PetDancePrimedCapture.StartAsync(
            () => Task.FromResult<CapturedWindowFrame?>(new(new Bitmap(2, 2), TimeSpan.FromMilliseconds(100))),
            () => true, _ => Task.FromResult(new PetDanceStartReceipt(false, true)), () => 110, CancellationToken.None);
        for (var i = 0; i < 100 && capture.Pending; i++) await Task.Delay(10);
        Assert.False(capture.Accepted);
        Assert.Throws<InvalidOperationException>(capture.CheckDispatch);
        Assert.True(await capture.FinishAsync());
    }

    [Fact]
    public async Task StaleCaptureExpiresWithoutPlay()
    {
        var clock = 1000d; var called = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => PetDancePrimedCapture.StartAsync(
            () => Task.FromResult<CapturedWindowFrame?>(new(new Bitmap(2, 2), TimeSpan.FromMilliseconds(1))),
            () => true, _ => { called = true; return Task.FromResult(new PetDanceStartReceipt(true, true)); }, () => clock += 1000, CancellationToken.None));
        Assert.False(called);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FaultOrCancellationCannotConfirmInputRelease(bool fault)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var capture = await PetDancePrimedCapture.StartAsync(
            () => Task.FromResult<CapturedWindowFrame?>(new(new Bitmap(2, 2), TimeSpan.FromMilliseconds(100))),
            () => true, async token =>
            {
                entered.SetResult();
                if (fault) throw new InvalidOperationException("dispatch failed after possible key-down");
                await Task.Delay(Timeout.Infinite, token);
                return new PetDanceStartReceipt(true, true);
            }, () => 110, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(await capture.FinishAsync());
        Assert.False(capture.Accepted);
        Assert.Throws<InvalidOperationException>(capture.CheckDispatch);
    }

    [Fact]
    public async Task AcceptedDispatchWithoutReleaseEvidenceCannotAdmitResponses()
    {
        using var capture = await PetDancePrimedCapture.StartAsync(
            () => Task.FromResult<CapturedWindowFrame?>(new(new Bitmap(2, 2), TimeSpan.FromMilliseconds(100))),
            () => true, _ => Task.FromResult(new PetDanceStartReceipt(true, false)), () => 110, CancellationToken.None);
        for (var i = 0; i < 100 && capture.Pending; i++) await Task.Delay(10);
        Assert.False(capture.Pending);
        Assert.False(await capture.FinishAsync());
        Assert.False(capture.Accepted);
        Assert.Throws<InvalidOperationException>(capture.CheckDispatch);
    }
}
