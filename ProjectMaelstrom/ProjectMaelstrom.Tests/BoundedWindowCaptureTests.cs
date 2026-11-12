using System.Diagnostics;
using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class BoundedWindowCaptureTests
{
    [Fact]
    public async Task AStalledReadAndItsBlockedCleanupHaveFiniteSeparateBudgets()
    {
        var pending = new TaskCompletionSource<CapturedWindowFrame?>();
        var cleanup = new TaskCompletionSource();
        var reads = 0; var disposals = 0; var clock = Stopwatch.StartNew();
        await Assert.ThrowsAsync<WindowCaptureCleanupException>(() => BoundedWindowCapture.ReadAsync(
            () => { reads++; return pending.Task; }, () => { disposals++; return cleanup.Task; }, _ => true,
            CancellationToken.None, captureTimeoutMs: 20, cleanupTimeoutMs: 20));
        Assert.InRange(clock.Elapsed.TotalSeconds, 0, 2);
        Assert.Equal(1, reads); Assert.Equal(1, disposals);
        using var image = new Bitmap(4, 4);
        pending.SetResult(new(image, TimeSpan.FromMilliseconds(1)));
        Assert.ThrowsAny<Exception>(() => image.GetPixel(0, 0));
        cleanup.SetException(new IOException("Late cleanup error is observed."));
    }

    [Fact]
    public async Task AReadTimeoutWithConfirmedCleanupMeansNoEvidenceNotSuccessfulCapture()
    {
        var pending = new TaskCompletionSource<CapturedWindowFrame?>();
        var disposals = 0;
        var result = await BoundedWindowCapture.ReadAsync(() => pending.Task,
            () => { disposals++; return Task.CompletedTask; }, _ => true, CancellationToken.None, 20, 20);
        Assert.Null(result); Assert.Equal(1, disposals);
        pending.SetException(new IOException("Late read error is observed."));
    }

    [Fact]
    public async Task CapturedEvidenceTransfersOnlyAfterCleanupCompletes()
    {
        var cleanup = new TaskCompletionSource();
        using var image = new Bitmap(4, 4);
        var task = BoundedWindowCapture.ReadAsync(() => Task.FromResult<CapturedWindowFrame?>(new(image, TimeSpan.FromMilliseconds(100))),
            () => cleanup.Task, _ => true, CancellationToken.None);
        Assert.False(task.IsCompleted);
        cleanup.SetResult();
        using var result = await task;
        Assert.NotNull(result); Assert.Same(image, result.Image);
        Assert.Equal(TimeSpan.FromMilliseconds(100), result.CaptureTime);
        Assert.Equal(4, result.Image.Width);
    }

    [Fact]
    public async Task AcceptedEvidenceIsDiscardedWhenCleanupFails()
    {
        using var image = new Bitmap(4, 4);
        await Assert.ThrowsAsync<WindowCaptureCleanupException>(() => BoundedWindowCapture.ReadAsync(
            () => Task.FromResult<CapturedWindowFrame?>(new(image, TimeSpan.Zero)),
            () => Task.FromException(new IOException("Cleanup failed.")), _ => true, CancellationToken.None));
        Assert.ThrowsAny<Exception>(() => image.GetPixel(0, 0));
    }

    [Fact]
    public async Task CancellationWhileReadingStopsWithoutReplacementReadAndStillDisposesLateFrame()
    {
        using var cancel = new CancellationTokenSource();
        var pending = new TaskCompletionSource<CapturedWindowFrame?>(); var reads = 0; var disposals = 0;
        var task = BoundedWindowCapture.ReadAsync(() => { reads++; return pending.Task; },
            () => { disposals++; return Task.CompletedTask; }, _ => true, cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(1, reads); Assert.Equal(1, disposals);
        using var image = new Bitmap(4, 4); pending.SetResult(new(image, TimeSpan.Zero));
        Assert.ThrowsAny<Exception>(() => image.GetPixel(0, 0));
    }

    [Fact]
    public async Task CancellationDuringCleanupDiscardsPreviouslyAcceptedEvidence()
    {
        using var cancel = new CancellationTokenSource();
        var cleanup = new TaskCompletionSource(); using var image = new Bitmap(4, 4);
        var task = BoundedWindowCapture.ReadAsync(() => Task.FromResult<CapturedWindowFrame?>(new(image, TimeSpan.Zero)),
            () => cleanup.Task, _ => true, cancel.Token);
        cancel.Cancel(); cleanup.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.ThrowsAny<Exception>(() => image.GetPixel(0, 0));
    }

    [Fact]
    public async Task UnconfirmedCleanupIsPreservedEvenWhenTheOperationWasCancelled()
    {
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); var reads = 0; var disposals = 0;
        await Assert.ThrowsAsync<WindowCaptureCleanupException>(() => BoundedWindowCapture.ReadAsync(
            () => { reads++; return Task.FromResult<CapturedWindowFrame?>(null); },
            () => { disposals++; return new TaskCompletionSource().Task; }, _ => true, cancel.Token, 20, 20));
        Assert.Equal(0, reads); Assert.Equal(1, disposals);
    }

    [Fact]
    public async Task RejectedAndUnreadableFramesAreDisposedBeforeTheNextAttempt()
    {
        using var rejected = new Bitmap(4, 4); using var accepted = new Bitmap(4, 4); var reads = 0;
        using var result = await BoundedWindowCapture.ReadAsync(() =>
        {
            if (++reads == 1) return Task.FromResult<CapturedWindowFrame?>(new(rejected, TimeSpan.Zero));
            Assert.ThrowsAny<Exception>(() => rejected.GetPixel(0, 0));
            return Task.FromResult<CapturedWindowFrame?>(new(accepted, TimeSpan.FromMilliseconds(20)));
        }, () => Task.CompletedTask, frame => frame.CaptureTime!.Value.TotalMilliseconds > 0, CancellationToken.None);
        Assert.Equal(2, reads); Assert.NotNull(result); Assert.Same(accepted, result.Image);
    }

    [Fact]
    public async Task FailedRecognitionStillDisposesTheCandidateAndTheSession()
    {
        using var image = new Bitmap(4, 4); var disposals = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => BoundedWindowCapture.ReadAsync(
            () => Task.FromResult<CapturedWindowFrame?>(new(image, TimeSpan.Zero)),
            () => { disposals++; return Task.CompletedTask; }, _ => throw new InvalidDataException("Rejected image."), CancellationToken.None));
        Assert.Equal(1, disposals); Assert.ThrowsAny<Exception>(() => image.GetPixel(0, 0));
    }
}
