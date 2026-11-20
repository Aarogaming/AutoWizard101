using System.Threading.Channels;

namespace ProjectMaelstrom.Utilities;

/// <summary>Bounded frame handoff; ownership transfers only when TryWrite succeeds.</summary>
internal sealed class FrameBuffer<T> where T : IDisposable
{
    private readonly Channel<T> _channel;
    private long _dropped;
    public FrameBuffer(int capacity)
    {
        if (capacity < 1 || capacity > 120) throw new ArgumentOutOfRangeException(nameof(capacity));
        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false });
    }
    public long Dropped => Interlocked.Read(ref _dropped);
    public bool TryWrite(T frame)
    {
        if (_channel.Writer.TryWrite(frame)) return true;
        Interlocked.Increment(ref _dropped);
        return false;
    }
    public ValueTask WriteAsync(T frame, CancellationToken token) => _channel.Writer.WriteAsync(frame, token);
    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken token = default) => _channel.Reader.ReadAllAsync(token);
    public void Complete(Exception? error = null) => _channel.Writer.TryComplete(error);
}
