using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class OperationRecoveryDrainTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-recovery-drain-" + Guid.NewGuid().ToString("N"));
    private string Leases => Path.Combine(_root, "leases");
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static OperationRequest Request(bool live = true, bool delegated = false) => new(Guid.NewGuid().ToString("N"),
        "test.recovery-drain", "test-v1", 15, live, live ? "isolated-target" : "offline", new Dictionary<string, string>(), delegated);
    private OperationScheduler Scheduler(string name, string? leases = null) => new(Path.Combine(_root, name), leases ?? Leases);
    private static Task<OperationOutcome> Completed(OperationContext _, CancellationToken token) => Task.FromResult(OperationOutcome.Completed("test-only callback"));

    [Fact]
    public async Task HoldCoversDirectAndLaterSchedulersButNotOfflineOrOtherLeaseDirectories()
    {
        await using var first = Scheduler("first");
        await using var second = Scheduler("second");
        using var hold = OperationScheduler.PauseLiveForRecovery(Path.Combine(Leases, "."));
        await using var later = Scheduler("created-during-hold");
        await using var unrelated = Scheduler("unrelated", Path.Combine(_root, "other-leases"));
        foreach (var scheduler in new[] { first, second, later })
        {
            Assert.Throws<InvalidOperationException>(() => scheduler.Submit(Request(), Completed));
            Assert.Throws<InvalidOperationException>(() => scheduler.Submit(Request(delegated: true), Completed));
            Assert.Equal(OperationState.Completed, (await scheduler.Submit(Request(false), Completed).Completion.WaitAsync(Limit)).State);
        }
        Assert.Equal(OperationState.Completed, (await unrelated.Submit(Request(), Completed).Completion.WaitAsync(Limit)).State);
        Assert.True(hold.IsHeld); Assert.True(hold.IsDrained);
        Assert.True(await hold.StopAndWaitAsync(Limit, CancellationToken.None));
    }

    [Fact]
    public async Task CancelsQueuedLiveWorkWhileKeepingOfflineSchedulerUsable()
    {
        await using var scheduler = Scheduler("jobs");
        var entered = Signal(); var release = Signal(); int liveCalls = 0;
        var offline = scheduler.Submit(Request(false), async (_, _) =>
        { entered.TrySetResult(); await release.Task; return OperationOutcome.Completed("offline work retained"); });
        try
        {
            await entered.Task.WaitAsync(Limit);
            var native = scheduler.Submit(Request(), (_, _) => { liveCalls++; return Task.FromResult(OperationOutcome.Completed("must not run")); });
            var delegated = scheduler.Submit(Request(delegated: true), (_, _) => { liveCalls++; return Task.FromResult(OperationOutcome.Completed("must not run")); });
            var nextOffline = scheduler.Submit(Request(false), Completed);
            using var hold = OperationScheduler.PauseLiveForRecovery(Leases);
            Assert.True(await hold.StopAndWaitAsync(Limit, CancellationToken.None));
            Assert.Equal(OperationState.Cancelled, (await native.Completion.WaitAsync(Limit)).State);
            Assert.Equal(OperationState.Cancelled, (await delegated.Completion.WaitAsync(Limit)).State);
            Assert.False(offline.Completion.IsCompleted); Assert.True(hold.IsDrained);
            release.TrySetResult();
            Assert.Equal(OperationState.Completed, (await offline.Completion.WaitAsync(Limit)).State);
            Assert.Equal(OperationState.Completed, (await nextOffline.Completion.WaitAsync(Limit)).State);
            hold.Dispose();
            var explicitNewRun = scheduler.Submit(Request(), (_, _) => { liveCalls++; return Task.FromResult(OperationOutcome.Completed("new run")); });
            Assert.Equal(OperationState.Completed, (await explicitNewRun.Completion.WaitAsync(Limit)).State);
            Assert.Equal(1, liveCalls);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task DrainsActiveNativeAndDelegatedCallbacksAcrossDifferentDataRoots()
    {
        await using var first = Scheduler("first");
        await using var second = Scheduler("second");
        var entered = new[] { Signal(), Signal() }; var cleanup = new[] { Signal(), Signal() }; var release = Signal();
        GameInputLease? owner = null;
        var native = first.Submit(Request(), async (context, token) =>
        {
            owner = context.Input; entered[0].TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cleanup[0].TrySetResult(); await release.Task; }
            return OperationOutcome.Completed("unreachable");
        });
        var delegated = second.Submit(Request(delegated: true), async (_, token) =>
        {
            entered[1].TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cleanup[1].TrySetResult(); await release.Task; }
            return OperationOutcome.Completed("unreachable");
        });
        try
        {
            await Task.WhenAll(entered.Select(signal => signal.Task)).WaitAsync(Limit);
            using var hold = OperationScheduler.PauseLiveForRecovery(Leases);
            var stopping = hold.StopAndWaitAsync(Limit, CancellationToken.None);
            await Task.WhenAll(cleanup.Select(signal => signal.Task)).WaitAsync(Limit);
            Assert.False(stopping.IsCompleted); Assert.False(hold.IsDrained);
            Assert.NotNull(owner); Assert.True(owner!.IsHeld);
            release.TrySetResult();
            Assert.True(await stopping); Assert.True(hold.IsDrained); Assert.False(owner.IsHeld);
            Assert.Equal(OperationState.Cancelled, (await native.Completion.WaitAsync(Limit)).State);
            Assert.Equal(OperationState.Cancelled, (await delegated.Completion.WaitAsync(Limit)).State);
            Assert.Equal(OperationState.Completed, (await second.Submit(Request(false), Completed).Completion.WaitAsync(Limit)).State);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task DrainTimeoutRetainsHoldAndOwnershipAndCanBeAwaitedAgain()
    {
        await using var scheduler = Scheduler("jobs");
        var entered = Signal(); var cancelled = Signal(); var release = Signal(); GameInputLease? owner = null;
        var ticket = scheduler.Submit(Request(), async (context, token) =>
        {
            owner = context.Input;
            using var registration = token.Register(() => cancelled.TrySetResult());
            entered.TrySetResult(); await release.Task;
            return OperationOutcome.Completed("finished cleanup");
        });
        try
        {
            await entered.Task.WaitAsync(Limit);
            using var hold = OperationScheduler.PauseLiveForRecovery(Leases);
            Assert.False(await hold.StopAndWaitAsync(TimeSpan.FromMilliseconds(25), CancellationToken.None));
            await cancelled.Task.WaitAsync(Limit);
            Assert.True(hold.IsHeld); Assert.False(hold.IsDrained); Assert.True(owner!.IsHeld);
            Assert.Throws<InvalidOperationException>(() => scheduler.Submit(Request(), Completed));
            release.TrySetResult();
            Assert.True(await hold.StopAndWaitAsync(Limit, CancellationToken.None));
            Assert.Equal(OperationState.Cancelled, (await ticket.Completion.WaitAsync(Limit)).State);
            Assert.False(owner.IsHeld);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task CancellingTheWaitDoesNotReleaseHoldOrAbandonWorkerCleanup()
    {
        await using var scheduler = Scheduler("jobs");
        var entered = Signal(); var cleanup = Signal(); var release = Signal();
        var ticket = scheduler.Submit(Request(), async (_, token) =>
        {
            entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cleanup.TrySetResult(); await release.Task; }
            return OperationOutcome.Completed("unreachable");
        });
        try
        {
            await entered.Task.WaitAsync(Limit);
            using var hold = OperationScheduler.PauseLiveForRecovery(Leases);
            using var cancel = new CancellationTokenSource();
            var stopping = hold.StopAndWaitAsync(Limit, cancel.Token);
            await cleanup.Task.WaitAsync(Limit); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopping);
            Assert.True(hold.IsHeld); Assert.False(hold.IsDrained); Assert.False(ticket.Completion.IsCompleted);
            release.TrySetResult();
            Assert.True(await hold.StopAndWaitAsync(Limit, CancellationToken.None));
            Assert.Equal(OperationState.Cancelled, (await ticket.Completion.WaitAsync(Limit)).State);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task BlockingCancellationCallbackCannotBlockTheDrainTimeout()
    {
        await using var scheduler = Scheduler("jobs");
        var entered = Signal(); var callbackEntered = Signal();
        using var releaseCallback = new ManualResetEventSlim();
        var ticket = scheduler.Submit(Request(), async (_, token) =>
        {
            using var registration = token.Register(() =>
            { callbackEntered.TrySetResult(); releaseCallback.Wait(Limit); });
            entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token);
            return OperationOutcome.Completed("unreachable");
        });
        try
        {
            await entered.Task.WaitAsync(Limit);
            using var hold = OperationScheduler.PauseLiveForRecovery(Leases);
            Assert.False(await hold.StopAndWaitAsync(TimeSpan.FromMilliseconds(25), CancellationToken.None));
            await callbackEntered.Task.WaitAsync(Limit);
            Assert.True(hold.IsHeld); Assert.False(hold.IsDrained); Assert.False(ticket.Completion.IsCompleted);
            releaseCallback.Set();
            Assert.True(await hold.StopAndWaitAsync(Limit, CancellationToken.None));
            Assert.Equal(OperationState.Cancelled, (await ticket.Completion.WaitAsync(Limit)).State);
        }
        finally { releaseCallback.Set(); }
    }

    [Fact]
    public async Task ThrowingWorkerCannotStrandDrainOrDisposeTheScheduler()
    {
        await using var scheduler = Scheduler("jobs");
        var entered = Signal(); var release = Signal();
        var ticket = scheduler.Submit(Request(), async (_, _) =>
        { entered.TrySetResult(); await release.Task; throw new IOException("test worker failure"); });
        try
        {
            await entered.Task.WaitAsync(Limit);
            using var hold = OperationScheduler.PauseLiveForRecovery(Leases);
            var stopping = hold.StopAndWaitAsync(Limit, CancellationToken.None);
            release.TrySetResult();
            Assert.True(await stopping); Assert.True(hold.IsDrained);
            var receipt = await ticket.Completion.WaitAsync(Limit);
            Assert.True(receipt.State is OperationState.Failed or OperationState.Cancelled);
            Assert.Equal(OperationState.Completed, (await scheduler.Submit(Request(false), Completed).Completion.WaitAsync(Limit)).State);
            Assert.Throws<InvalidOperationException>(() => scheduler.Submit(Request(), Completed));
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task DrainedCleanupFailureDoesNotClearRecoveryOrGrantLiveAdmission()
    {
        await using var scheduler = Scheduler("jobs");
        var entered = Signal(); var release = Signal();
        var ticket = scheduler.Submit(Request(), async (_, _) =>
        {
            entered.TrySetResult(); await release.Task;
            return new(OperationState.CleanupFailed, "test uncertain release", []);
        });
        try
        {
            await entered.Task.WaitAsync(Limit);
            using var hold = OperationScheduler.PauseLiveForRecovery(Leases);
            var stopping = hold.StopAndWaitAsync(Limit, CancellationToken.None);
            release.TrySetResult();
            Assert.True(await stopping); Assert.True(hold.IsDrained);
            Assert.Equal(OperationState.CleanupFailed, (await ticket.Completion.WaitAsync(Limit)).State);
            Assert.Throws<InvalidOperationException>(() => scheduler.Submit(Request(), Completed));
            Assert.Equal(OperationState.Completed, (await scheduler.Submit(Request(false), Completed).Completion.WaitAsync(Limit)).State);
            hold.Dispose();
            Assert.Equal(OperationState.Failed, (await scheduler.Submit(Request(), Completed).Completion.WaitAsync(Limit)).State);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task EndingAnUnusedHoldCannotReplayPreviouslyQueuedLiveWork()
    {
        await using var scheduler = Scheduler("jobs");
        var entered = Signal(); var release = Signal(); int calls = 0;
        var offline = scheduler.Submit(Request(false), async (_, _) =>
        { entered.TrySetResult(); await release.Task; return OperationOutcome.Completed("offline"); });
        try
        {
            await entered.Task.WaitAsync(Limit);
            var queued = scheduler.Submit(Request(), (_, _) => { calls++; return Task.FromResult(OperationOutcome.Completed("must not replay")); });
            using (OperationScheduler.PauseLiveForRecovery(Leases)) { }
            release.TrySetResult();
            await offline.Completion.WaitAsync(Limit);
            Assert.Equal(OperationState.Cancelled, (await queued.Completion.WaitAsync(Limit)).State);
            Assert.Equal(0, calls);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task NestedHoldsRequireEveryOwnerToReleaseAndKeepSchedulersReusable()
    {
        await using var scheduler = Scheduler("jobs");
        using var first = OperationScheduler.PauseLiveForRecovery(Leases);
        using var second = OperationScheduler.PauseLiveForRecovery(Leases);
        first.Dispose();
        Assert.False(first.IsHeld); Assert.False(first.IsDrained);
        Assert.Throws<InvalidOperationException>(() => scheduler.Submit(Request(), Completed));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => first.StopAndWaitAsync(Limit, CancellationToken.None));
        Assert.True(await second.StopAndWaitAsync(Limit, CancellationToken.None));
        second.Dispose();
        Assert.Equal(OperationState.Completed, (await scheduler.Submit(Request(), Completed).Completion.WaitAsync(Limit)).State);
    }

    [Fact]
    public async Task DelayedSnapshotCannotCancelANewRunSubmittedAfterTheOldHoldCloses()
    {
        await using var first = Scheduler("first");
        await using var second = Scheduler("second");
        // Force a real queue-snapshot delay without adding a production callback.
        // Determine the registry's current order instead of assuming HashSet order.
        const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var scope = typeof(OperationScheduler).GetField("_recoveryScope", fields)!.GetValue(first)!;
        var order = Assert.IsAssignableFrom<IEnumerable<OperationScheduler>>(scope.GetType().GetField("Schedulers", fields)!.GetValue(scope)).ToArray();
        var blocked = order[0]; var running = ReferenceEquals(blocked, first) ? second : first;
        var queueGate = typeof(OperationScheduler).GetField("_gate", fields)!.GetValue(blocked)!;
        var entered = Signal(); var gateEntered = Signal(); var freshEntered = Signal(); var releaseFresh = Signal();
        using var releaseSnapshot = new ManualResetEventSlim();
        var freshCancelled = 0;
        var old = running.Submit(Request(), async (_, token) =>
        { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return OperationOutcome.Completed("unreachable"); });
        Task? blocker = null;
        try
        {
            await entered.Task.WaitAsync(Limit);
            blocker = Task.Run(() =>
            {
                lock (queueGate)
                {
                    gateEntered.TrySetResult();
                    if (!releaseSnapshot.Wait(Limit)) throw new TimeoutException("Test snapshot gate was not released.");
                }
            });
            await gateEntered.Task.WaitAsync(Limit);
            using var hold = OperationScheduler.PauseLiveForRecovery(Leases);
            Assert.False(await hold.StopAndWaitAsync(TimeSpan.FromMilliseconds(25), CancellationToken.None));
            hold.Dispose();
            var fresh = running.Submit(Request(), async (_, token) =>
            {
                using var registration = token.Register(() => Interlocked.Increment(ref freshCancelled));
                freshEntered.TrySetResult(); await releaseFresh.Task;
                return OperationOutcome.Completed("explicit new run");
            });
            releaseSnapshot.Set();
            await blocker.WaitAsync(Limit);
            Assert.Equal(OperationState.Cancelled, (await old.Completion.WaitAsync(Limit)).State);
            await Task.WhenAny(freshEntered.Task, fresh.Completion).WaitAsync(Limit);
            Assert.True(freshEntered.Task.IsCompleted); Assert.False(fresh.Completion.IsCompleted);
            Assert.Equal(0, Volatile.Read(ref freshCancelled));
            releaseFresh.TrySetResult();
            Assert.Equal(OperationState.Completed, (await fresh.Completion.WaitAsync(Limit)).State);
        }
        finally
        {
            releaseSnapshot.Set(); releaseFresh.TrySetResult();
            if (blocker is not null) await blocker.WaitAsync(Limit);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(30001)]
    public async Task InvalidDrainBudgetsNeverStartCancellation(int milliseconds)
    {
        using var hold = OperationScheduler.PauseLiveForRecovery(Leases);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => hold.StopAndWaitAsync(TimeSpan.FromMilliseconds(milliseconds), CancellationToken.None));
        Assert.True(hold.IsHeld); Assert.True(hold.IsDrained);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
