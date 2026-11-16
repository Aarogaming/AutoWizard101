using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class OperationFocusBudgetTests
{
    [Fact]
    public void FocusPauseBeyondSixtySecondsPreservesRemainingActiveBudget()
    {
        var time = new ManualTime();
        using var group = new OperationFocusBudget(timeProvider: time);
        using var deadline = group.CreateDeadline(TimeSpan.FromSeconds(5), default);
        time.Advance(2);
        using (group.Pause())
        {
            time.Advance(90);
            Assert.False(deadline.Token.IsCancellationRequested);
            Assert.Equal(TimeSpan.FromSeconds(90), group.PausedDuration);
        }
        time.Advance(2.9);
        Assert.False(deadline.Token.IsCancellationRequested);
        time.Advance(.1);
        Assert.True(deadline.Token.IsCancellationRequested);
    }

    [Fact]
    public void NestedPausesCountOnceAndNewDeadlinesStartAfterResume()
    {
        var time = new ManualTime();
        using var group = new OperationFocusBudget(timeProvider: time);
        using var outer = group.CreateDeadline(TimeSpan.FromSeconds(20), default);
        time.Advance(2);
        var first = group.Pause();
        time.Advance(100);
        using var inner = group.CreateDeadline(TimeSpan.FromSeconds(3), default);
        var second = group.Pause();
        time.Advance(100);
        first.Dispose(); first.Dispose();
        time.Advance(50);
        Assert.False(inner.Token.IsCancellationRequested);
        Assert.Equal(TimeSpan.FromSeconds(250), group.PausedDuration);
        second.Dispose();
        time.Advance(3);
        Assert.True(inner.Token.IsCancellationRequested);
        Assert.False(outer.Token.IsCancellationRequested);
        time.Advance(15);
        Assert.True(outer.Token.IsCancellationRequested);
    }

    [Fact]
    public void CumulativePauseCapExpiresAllNestedBudgetsAndCannotBeReset()
    {
        var time = new ManualTime();
        using var group = new OperationFocusBudget(timeProvider: time);
        var callbacks = 0;
        using var root = group.Register(TimeSpan.FromSeconds(1000), () => callbacks++);
        using var child = group.CreateDeadline(TimeSpan.FromSeconds(20), default);
        using (group.Pause()) time.Advance(200);
        time.Advance(2);
        using (group.Pause())
        {
            time.Advance(99);
            Assert.False(child.Token.IsCancellationRequested);
            time.Advance(1);
            Assert.True(child.Token.IsCancellationRequested);
            Assert.True(group.PauseExhausted);
            Assert.Equal(1, callbacks);
        }
        using var later = group.CreateDeadline(TimeSpan.FromSeconds(20), default);
        Assert.True(later.Token.IsCancellationRequested);
        time.Advance(1000);
        Assert.Equal(1, callbacks);
        Assert.Equal(TimeSpan.FromSeconds(300), group.PausedDuration);
    }

    [Fact]
    public void ParentStopCancelsImmediatelyWhilePaused()
    {
        var time = new ManualTime();
        using var group = new OperationFocusBudget(timeProvider: time);
        using var stop = new CancellationTokenSource();
        using var deadline = group.CreateDeadline(TimeSpan.FromSeconds(20), stop.Token);
        using var pause = group.Pause();
        stop.Cancel();
        Assert.True(deadline.Token.IsCancellationRequested);
        Assert.False(group.PauseExhausted);
    }

    [Fact]
    public void DisposedRegistrationCannotExpireDuringPauseOrAfterResume()
    {
        var time = new ManualTime();
        using var group = new OperationFocusBudget(timeProvider: time);
        var calls = 0;
        var registration = group.Register(TimeSpan.FromSeconds(1), () => calls++);
        using (group.Pause())
        {
            registration.Dispose();
            time.Advance(300);
        }
        time.Advance(100);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void ThrowingCallbackDoesNotPreventOtherDeadlinesBeingCancelled()
    {
        var time = new ManualTime();
        using var group = new OperationFocusBudget(TimeSpan.FromSeconds(1), time);
        using var throwing = group.Register(TimeSpan.FromSeconds(20), () => throw new InvalidOperationException("fixture"));
        using var child = group.CreateDeadline(TimeSpan.FromSeconds(20), default);
        using var pause = group.Pause();
        time.Advance(1);
        Assert.True(child.Token.IsCancellationRequested);
        Assert.IsType<InvalidOperationException>(group.CallbackFailure);
    }

    [Fact]
    public void CallbacksRunOutsideBudgetLock()
    {
        var time = new ManualTime();
        using var group = new OperationFocusBudget(timeProvider: time);
        var inspected = false;
        using var registration = group.Register(TimeSpan.FromSeconds(1), () =>
        {
            // A different thread must be able to inspect the group before this
            // cancellation callback returns, as scheduler drain callbacks do.
            var read = Task.Run(() => group.PausedDuration);
            inspected = read.Wait(TimeSpan.FromSeconds(2));
        });
        time.Advance(1);
        Assert.True(inspected);
    }

    [Fact]
    public void GroupDisposalRemovesTimersAndRejectsNewWork()
    {
        var time = new ManualTime();
        var group = new OperationFocusBudget(timeProvider: time);
        var calls = 0;
        using var registration = group.Register(TimeSpan.FromSeconds(1), () => calls++);
        using var pause = group.Pause();
        time.Advance(12);
        group.Dispose();
        time.Advance(1000);
        Assert.Equal(0, calls);
        Assert.Equal(TimeSpan.FromSeconds(12), group.PausedDuration);
        Assert.Throws<ObjectDisposedException>(() => group.Pause());
        Assert.Throws<ObjectDisposedException>(() => group.Register(TimeSpan.FromSeconds(1), () => { }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4294967295)]
    public void InvalidDurationsCannotCreateTimers(double milliseconds)
    {
        var duration = TimeSpan.FromMilliseconds(milliseconds);
        Assert.Throws<ArgumentOutOfRangeException>(() => new OperationFocusBudget(duration));
        using var group = new OperationFocusBudget();
        Assert.Throws<ArgumentOutOfRangeException>(() => group.Register(duration, () => { }));
    }

    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;
        private readonly List<ManualTimer> _timers = [];
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_ticks);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            _timers.Add(timer); timer.Change(dueTime, period); return timer;
        }
        internal void Advance(double seconds)
        {
            var end = _ticks + TimeSpan.FromSeconds(seconds).Ticks;
            for (var count = 0; ; count++)
            {
                if (count > 1000) throw new InvalidOperationException("Unbounded fixture timer callbacks.");
                var next = _timers.Where(t => !t.Disposed && t.Due <= end).OrderBy(t => t.Due).FirstOrDefault();
                if (next is null) break;
                _ticks = next.Due; next.Due = long.MaxValue; next.Fire();
            }
            _ticks = end;
        }
        private sealed class ManualTimer(ManualTime owner, TimerCallback callback, object? state) : ITimer
        {
            internal long Due = long.MaxValue;
            internal bool Disposed;
            internal void Fire() => callback(state);
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (Disposed) return false;
                Assert.Equal(Timeout.InfiniteTimeSpan, period);
                Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner._ticks + dueTime.Ticks;
                return true;
            }
            public void Dispose() { Disposed = true; Due = long.MaxValue; }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
