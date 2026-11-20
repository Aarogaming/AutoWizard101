using System.Threading.Channels;

namespace ProjectMaelstrom.Utilities;

/// <summary>One bounded diagnostic writer. Slow PNG encoding never queues gameplay frames.</summary>
internal sealed class PetDanceSnapshotWriter
{
    private sealed record Item(Bitmap Image, string Path);
    private readonly Channel<Item> _queue = Channel.CreateBounded<Item>(new BoundedChannelOptions(2)
    { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly Task _worker;
    private int _accepted, _saved, _skipped;
    private string? _error;
    public int Saved => Volatile.Read(ref _saved);
    public int Skipped => Volatile.Read(ref _skipped);
    public string? Error => Volatile.Read(ref _error);

    public PetDanceSnapshotWriter()
    {
        _worker = Task.Run(async () =>
        {
            await foreach (var item in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                using (item.Image)
                {
                    try { item.Image.Save(item.Path); Interlocked.Increment(ref _saved); }
                    catch (Exception error) { Volatile.Write(ref _error, error.Message); }
                }
            }
        });
    }

    public bool TrySave(Bitmap image, string path)
    {
        if (_accepted >= 64 || Error != null) { Interlocked.Increment(ref _skipped); return false; }
        var owned = (Bitmap)image.Clone();
        if (_queue.Writer.TryWrite(new(owned, path))) { _accepted++; return true; }
        owned.Dispose(); Interlocked.Increment(ref _skipped); return false;
    }

    public Task CompleteAsync()
    {
        _queue.Writer.TryComplete();
        return _worker;
    }
}
