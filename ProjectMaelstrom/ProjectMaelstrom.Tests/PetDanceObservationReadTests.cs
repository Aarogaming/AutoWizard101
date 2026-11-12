using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class PetDanceObservationReadTests
{
    [Fact]
    public async Task TimeoutDoesNotRetryAndDisposesTheLateFrame()
    {
        var pending = new TaskCompletionSource<CapturedWindowFrame?>();
        var calls = 0;
        await Assert.ThrowsAsync<TimeoutException>(() => PetDanceObservationRead.ReadAsync(() => { calls++; return pending.Task; }, CancellationToken.None, 20));
        using var image = new Bitmap(4, 4);
        pending.SetResult(new(image, TimeSpan.Zero));
        Assert.Equal(1, calls);
        Assert.ThrowsAny<Exception>(() => image.GetPixel(0, 0));
    }

    [Fact]
    public async Task ACompletedReadTransfersTheLiveFrameToItsCaller()
    {
        using var result = await PetDanceObservationRead.ReadAsync(() => Task.FromResult<CapturedWindowFrame?>(new(new Bitmap(4, 4), TimeSpan.Zero)), CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(4, result.Image.Width);
    }

    [Fact]
    public async Task CancellationDoesNotRetryAPendingRead()
    {
        var pending = new TaskCompletionSource<CapturedWindowFrame?>();
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PetDanceObservationRead.ReadAsync(() => pending.Task, cancel.Token));
        pending.SetException(new IOException("Late capture error is observed."));
    }
}
