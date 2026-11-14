namespace ProjectMaelstrom.Utilities;

/// <summary>Scope-local control signals. Resume cannot clear Stop or game safety guards.</summary>
internal sealed class W101RoutineControl
{
    private int _state;
    internal bool Paused => Volatile.Read(ref _state) == 1;
    internal bool Stopped => Volatile.Read(ref _state) == 2;
    internal void Pause() => Interlocked.CompareExchange(ref _state, 1, 0);
    internal void Resume() => Interlocked.CompareExchange(ref _state, 0, 1);
    internal void Stop() => Interlocked.Exchange(ref _state, 2);
}
