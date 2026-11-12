using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class FrameBufferTests
{
    private sealed class Frame : IDisposable { public bool Disposed; public void Dispose() => Disposed = true; }
    [Fact]
    public void CapturedPixelBufferIsReturnedExactlyOnce()
    {
        var returns = 0;
        var pixels = new byte[16];
        var frame = new CapturedWindowPixels(pixels, 2, 2, TimeSpan.Zero, returned => { Assert.Same(pixels, returned); returns++; });
        frame.Dispose();
        frame.Dispose();
        Assert.Equal(1, returns);
    }
    [Fact]
    public async Task BufferIsBoundedAndCountsRejectedFrames()
    {
        var buffer = new FrameBuffer<Frame>(1);
        var first = new Frame();
        var rejected = new Frame();
        Assert.True(buffer.TryWrite(first));
        Assert.False(buffer.TryWrite(rejected));
        Assert.Equal(1, buffer.Dropped);
        Assert.False(rejected.Disposed); // Caller retains rejected frame ownership.
        rejected.Dispose();
        buffer.Complete();
        await foreach (var frame in buffer.ReadAllAsync()) frame.Dispose();
        Assert.True(first.Disposed);
    }

    [Fact]
    public void SharedFrameSurvivesEitherConsumerAndIsDisposedExactlyOnce()
    {
        var releases = 0;
        using var original = new SharedLease<TrackedFrame>(new TrackedFrame(() => releases++));
        var second = original.Retain();
        original.Dispose();
        Assert.Equal(0, releases);
        Assert.NotNull(second.Resource);
        second.Dispose();
        second.Dispose();
        Assert.Equal(1, releases);
        Assert.Throws<ObjectDisposedException>(() => original.Retain());
    }

    [Fact]
    public async Task TerminalFrameWaitsForCapacityWithoutCountingAsDropped()
    {
        var buffer = new FrameBuffer<Frame>(1);
        var first = new Frame();
        var terminal = new Frame();
        Assert.True(buffer.TryWrite(first));
        var write = buffer.WriteAsync(terminal, CancellationToken.None).AsTask();
        Assert.False(write.IsCompleted);
        await using var reader = buffer.ReadAllAsync().GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        Assert.Same(first, reader.Current);
        reader.Current.Dispose();
        await write;
        buffer.Complete();
        Assert.True(await reader.MoveNextAsync());
        Assert.Same(terminal, reader.Current);
        reader.Current.Dispose();
        Assert.False(await reader.MoveNextAsync());
        Assert.Equal(0, buffer.Dropped);
    }

    private sealed class TrackedFrame(Action release) : IDisposable
    {
        public void Dispose() => release();
    }
}
