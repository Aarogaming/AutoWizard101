namespace ProjectMaelstrom.Utilities;

/// <summary>Shares a bounded focus allowance across nested active deadlines.
/// Pause only at a released-input checkpoint. This clock grants no input rights,
/// does not clear cancellation, and never repeats or resumes an action itself.</summary>
internal sealed class OperationFocusBudget : IDisposable
{
    private readonly object _gate = new();
    private readonly TimeProvider _time;
    private readonly TimeSpan _maximumPause;
    private readonly HashSet<Registration> _registrations = [];
    private readonly ITimer _pauseTimer;
    private TimeSpan _paused;
    private long _pauseStarted;
    private int _pauseDepth;
    private bool _disposed, _pauseExhausted;
    internal Exception? CallbackFailure { get; private set; }

    internal OperationFocusBudget(TimeSpan? maximumPause = null, TimeProvider? timeProvider = null)
    {
        _maximumPause = maximumPause ?? TimeSpan.FromSeconds(300);
        ValidateDuration(_maximumPause);
        _time = timeProvider ?? TimeProvider.System;
        _pauseTimer = _time.CreateTimer(_ => PauseExpired(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    internal TimeSpan PausedDuration
    {
        get { lock (_gate) return PausedAt(_time.GetTimestamp()); }
    }
    internal bool PauseExhausted { get { lock (_gate) return _pauseExhausted; } }

    /// <summary>Register an active-time deadline. Callback runs outside the budget
    /// lock; the scheduler should use its existing cancellation/drain transaction.</summary>
    internal IDisposable Register(TimeSpan activeBudget, Action onExpired)
    {
        ValidateDuration(activeBudget); ArgumentNullException.ThrowIfNull(onExpired);
        Registration registration;
        bool expired;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            registration = new(this, activeBudget, onExpired);
            _registrations.Add(registration);
            expired = _pauseExhausted;
            if (expired) registration.Expired = true;
            else if (_pauseDepth == 0) registration.Arm(_time.GetTimestamp());
        }
        if (expired) Invoke(registration);
        return registration;
    }

    /// <summary>Linked cancellation remains immediate even while the active
    /// timeout is paused. Disposing a deadline removes only its own timer.</summary>
    internal Deadline CreateDeadline(TimeSpan activeBudget, CancellationToken parentToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        try
        {
            var registration = Register(activeBudget, () =>
            {
                try { source.Cancel(); }
                catch (ObjectDisposedException) { }
            });
            return new(source, registration);
        }
        catch { source.Dispose(); throw; }
    }

    internal IDisposable Pause()
    {
        List<Registration> expired = [];
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_pauseDepth++ == 0 && !_pauseExhausted)
            {
                var now = _time.GetTimestamp();
                foreach (var registration in _registrations)
                {
                    if (registration.Expired) continue;
                    registration.Consume(now);
                    registration.Timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    if (registration.Remaining <= TimeSpan.Zero)
                    { registration.Expired = true; expired.Add(registration); }
                }
                _pauseStarted = now;
                _pauseTimer.Change(_maximumPause - _paused, Timeout.InfiniteTimeSpan);
            }
        }
        foreach (var registration in expired) Invoke(registration);
        return new PauseLease(this);
    }

    private TimeSpan PausedAt(long now)
    {
        var value = _paused + (_pauseDepth > 0 && !_pauseExhausted ? _time.GetElapsedTime(_pauseStarted, now) : TimeSpan.Zero);
        return value > _maximumPause ? _maximumPause : value;
    }

    private void Resume()
    {
        List<Registration> expired = [];
        lock (_gate)
        {
            if (_disposed || _pauseDepth == 0 || --_pauseDepth != 0) return;
            if (_pauseExhausted) return;
            var now = _time.GetTimestamp();
            _paused += _time.GetElapsedTime(_pauseStarted, now);
            _pauseTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            if (_paused >= _maximumPause) expired = ExhaustPause();
            else foreach (var registration in _registrations)
                    if (!registration.Expired) registration.Arm(now);
        }
        foreach (var registration in expired) Invoke(registration);
    }

    private void PauseExpired()
    {
        List<Registration> expired;
        lock (_gate)
        {
            if (_disposed || _pauseDepth == 0 || _pauseExhausted) return;
            var remaining = _maximumPause - (_paused + _time.GetElapsedTime(_pauseStarted, _time.GetTimestamp()));
            if (remaining > TimeSpan.Zero)
            { _pauseTimer.Change(remaining, Timeout.InfiniteTimeSpan); return; }
            expired = ExhaustPause();
        }
        foreach (var registration in expired) Invoke(registration);
    }

    private List<Registration> ExhaustPause()
    {
        _paused = _maximumPause; _pauseExhausted = true;
        var expired = _registrations.Where(r => !r.Expired).ToList();
        foreach (var registration in expired)
        { registration.Expired = true; registration.Timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan); }
        return expired;
    }

    private void ActiveExpired(Registration registration)
    {
        lock (_gate)
        {
            if (_disposed || registration.Disposed || registration.Expired || _pauseDepth > 0) return;
            var now = _time.GetTimestamp();
            registration.Consume(now);
            if (registration.Remaining > TimeSpan.Zero) { registration.Arm(now); return; }
            registration.Expired = true;
        }
        Invoke(registration);
    }

    private void Invoke(Registration registration)
    {
        // User cancellation callbacks must not crash a thread-pool timer or
        // prevent the other nested deadlines receiving pause-cap cancellation.
        try { registration.Callback(); }
        catch (Exception error) { lock (_gate) CallbackFailure ??= error; }
    }

    public void Dispose()
    {
        Registration[] registrations;
        lock (_gate)
        {
            if (_disposed) return;
            _paused = PausedAt(_time.GetTimestamp()); _pauseDepth = 0; _disposed = true;
            registrations = _registrations.ToArray(); _registrations.Clear();
            foreach (var registration in registrations) registration.Disposed = true;
        }
        _pauseTimer.Dispose();
        foreach (var registration in registrations) registration.Timer.Dispose();
    }

    private static void ValidateDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || duration.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(duration), "Use a positive finite timer budget.");
    }

    private sealed class Registration : IDisposable
    {
        private readonly OperationFocusBudget _owner;
        internal readonly Action Callback;
        internal readonly ITimer Timer;
        internal TimeSpan Remaining;
        internal long Started;
        internal bool Expired, Disposed;
        internal Registration(OperationFocusBudget owner, TimeSpan remaining, Action callback)
        {
            _owner = owner; Remaining = remaining; Callback = callback;
            Timer = owner._time.CreateTimer(_ => owner.ActiveExpired(this), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        internal void Arm(long now)
        { Started = now; Timer.Change(Remaining, Timeout.InfiniteTimeSpan); }
        internal void Consume(long now)
        { Remaining -= _owner._time.GetElapsedTime(Started, now); Started = now; }
        public void Dispose()
        {
            lock (_owner._gate)
            {
                if (Disposed) return;
                Disposed = true; _owner._registrations.Remove(this);
            }
            Timer.Dispose();
        }
    }

    private sealed class PauseLease(OperationFocusBudget owner) : IDisposable
    {
        private OperationFocusBudget? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Resume();
    }

    internal sealed class Deadline(CancellationTokenSource source, IDisposable registration) : IDisposable
    {
        internal CancellationToken Token => source.Token;
        public void Dispose() { registration.Dispose(); source.Dispose(); }
    }
}
