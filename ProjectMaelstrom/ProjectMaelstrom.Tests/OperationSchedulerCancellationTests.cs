using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class OperationSchedulerCancellationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-operation-cancellation-" + Guid.NewGuid().ToString("N"));
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static OperationRequest Request(int timeout = 15) => new(Guid.NewGuid().ToString("N"),
        "test.cancellation", "test-v1", timeout, true, "isolated-test-target", new Dictionary<string, string>());

    [Fact]
    public async Task ThrowingExplicitCancellationKeepsCleanupOwnedAndAllowsJournalReopen()
    {
        var scheduler = new OperationScheduler(_root, _root);
        var entered = Signal(); var cleanup = Signal(); var release = Signal();
        var ticket = scheduler.Submit(Request(), async (context, token) =>
        {
            using var registration = token.Register(() => throw new InvalidOperationException("explicit-callback-failure"));
            Assert.True(context.Input!.IsHeld); entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cleanup.TrySetResult(); await release.Task; }
            return OperationOutcome.Completed("unreachable");
        });
        try
        {
            await entered.Task.WaitAsync(WaitLimit);
            await Task.Run(ticket.Cancel).WaitAsync(WaitLimit);
            await cleanup.Task.WaitAsync(WaitLimit);
            Assert.False(ticket.Completion.IsCompleted);
            AssertInputRemainsOwned();
            release.TrySetResult();
            var receipt = await ticket.Completion.WaitAsync(WaitLimit);
            Assert.Equal(OperationState.Cancelled, receipt.State);
            Assert.Contains("explicit-callback-failure", receipt.Message, StringComparison.Ordinal);
            await scheduler.DisposeAsync().AsTask().WaitAsync(WaitLimit);
            await using var reopened = new OperationScheduler(_root, _root);
            Assert.Equal(OperationState.Cancelled, Assert.Single(OperationScheduler.History(_root)).State);
            using var nextOwner = GameInputLease.Acquire("later-test-owner", _root);
            Assert.True(nextOwner.IsHeld);
        }
        finally
        {
            release.TrySetResult();
            await scheduler.DisposeAsync().AsTask().WaitAsync(WaitLimit);
        }
    }

    [Fact]
    public async Task ThrowingDeadlineCallbackIsContainedUntilCleanupFinishes()
    {
        var scheduler = new OperationScheduler(_root, _root);
        var entered = Signal(); var cleanup = Signal(); var release = Signal();
        var callbackRan = Signal();
        var ticket = scheduler.Submit(Request(timeout: 1), async (context, token) =>
        {
            using var registration = token.Register(() =>
            {
                callbackRan.TrySetResult();
                throw new InvalidOperationException("deadline-callback-failure");
            });
            Assert.True(context.Input!.IsHeld); entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cleanup.TrySetResult(); await release.Task; }
            return OperationOutcome.Completed("unreachable");
        });
        try
        {
            await entered.Task.WaitAsync(WaitLimit);
            await callbackRan.Task.WaitAsync(WaitLimit);
            await cleanup.Task.WaitAsync(WaitLimit);
            Assert.False(ticket.Completion.IsCompleted);
            AssertInputRemainsOwned();
            release.TrySetResult();
            var receipt = await ticket.Completion.WaitAsync(WaitLimit);
            Assert.Equal(OperationState.TimedOut, receipt.State);
            Assert.Contains("deadline-callback-failure", receipt.Message, StringComparison.Ordinal);
            using var nextOwner = GameInputLease.Acquire("later-test-owner", _root);
            Assert.True(nextOwner.IsHeld);
        }
        finally
        {
            release.TrySetResult();
            await scheduler.DisposeAsync().AsTask().WaitAsync(WaitLimit);
        }
    }

    [Fact]
    public async Task InlineWorkerContinuationCannotReleaseInputBeforeLaterCancellationCallback()
    {
        var scheduler = new OperationScheduler(_root, _root);
        var entered = Signal(); var workerUnwound = Signal(); var observerEntered = Signal(); var releaseObserver = Signal();
        // Deliberately allow synchronous continuations to exercise cancellation reentrancy.
        var workerCancellation = new TaskCompletionSource();
        CancellationTokenRegistration observer = default; CancellationTokenRegistration trigger = default;
        OperationTicket? ticket = null; Task? cancellation = null;
        bool leaseHeldInObserver = false, completionVisibleInObserver = true, observerReleased = false;
        ticket = scheduler.Submit(Request(), async (context, token) =>
        {
            // Cancellation invokes callbacks in reverse registration order. Keep these
            // registrations alive outside the worker so its unwind cannot unregister them.
            observer = token.Register(() =>
            {
                leaseHeldInObserver = context.Input!.IsHeld;
                completionVisibleInObserver = ticket!.Completion.IsCompleted;
                observerEntered.TrySetResult();
                observerReleased = releaseObserver.Task.Wait(WaitLimit);
            });
            trigger = token.Register(() => workerCancellation.TrySetCanceled(token));
            entered.TrySetResult();
            try { await workerCancellation.Task.ConfigureAwait(false); }
            finally { workerUnwound.TrySetResult(); }
            return OperationOutcome.Completed("unreachable");
        });
        try
        {
            await entered.Task.WaitAsync(WaitLimit);
            cancellation = Task.Run(ticket.Cancel);
            await observerEntered.Task.WaitAsync(WaitLimit);
            await workerUnwound.Task.WaitAsync(WaitLimit);
            Assert.True(leaseHeldInObserver);
            Assert.False(completionVisibleInObserver);
            Assert.False(ticket.Completion.IsCompleted);
            AssertInputRemainsOwned();
            releaseObserver.TrySetResult();
            await cancellation.WaitAsync(WaitLimit);
            Assert.Equal(OperationState.Cancelled, (await ticket.Completion.WaitAsync(WaitLimit)).State);
            Assert.True(observerReleased);
            using var nextOwner = GameInputLease.Acquire("later-test-owner", _root);
            Assert.True(nextOwner.IsHeld);
        }
        finally
        {
            releaseObserver.TrySetResult();
            workerCancellation.TrySetCanceled();
            if (cancellation is not null) await cancellation.WaitAsync(WaitLimit);
            await scheduler.DisposeAsync().AsTask().WaitAsync(WaitLimit);
            trigger.Dispose(); observer.Dispose();
        }
    }

    private void AssertInputRemainsOwned()
    {
        GameInputLease? competing = null;
        try { Assert.Throws<InvalidOperationException>(() => competing = GameInputLease.Acquire("competing-test-owner", _root)); }
        finally { competing?.Dispose(); }
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
