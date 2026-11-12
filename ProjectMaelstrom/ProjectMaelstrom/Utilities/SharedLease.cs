namespace ProjectMaelstrom.Utilities;

/// <summary>Shares ownership of one disposable resource between independent consumers.</summary>
internal sealed class SharedLease<T> : IDisposable where T : class, IDisposable
{
    private sealed class State(T resource)
    {
        internal readonly T Resource = resource;
        internal int References = 1;
    }

    private State? _state;
    private readonly object _gate = new();
    public SharedLease(T resource) => _state = new(resource);
    private SharedLease(State state) => _state = state;

    public T Resource => Volatile.Read(ref _state)?.Resource ?? throw new ObjectDisposedException(nameof(SharedLease<T>));

    public SharedLease<T> Retain()
    {
        lock (_gate)
        {
            var state = _state ?? throw new ObjectDisposedException(nameof(SharedLease<T>));
            Interlocked.Increment(ref state.References);
            return new SharedLease<T>(state);
        }
    }

    public void Dispose()
    {
        State? state;
        lock (_gate) { state = _state; _state = null; }
        if (state != null && Interlocked.Decrement(ref state.References) == 0) state.Resource.Dispose();
    }
}
