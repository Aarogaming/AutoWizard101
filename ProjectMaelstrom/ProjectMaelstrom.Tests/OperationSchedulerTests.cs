using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class OperationSchedulerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-operations-" + Guid.NewGuid().ToString("N"));
    private OperationRequest Request(bool live = false, int timeout = 10) => new(Guid.NewGuid().ToString("N"), "test.operation", "test-v1", timeout, live, live ? "owned-test-target" : "offline", new Dictionary<string, string>());
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private OperationReceipt Read(string id) => JsonSerializer.Deserialize<OperationReceipt>(File.ReadAllText(Path.Combine(_root, "operations", id, "receipt.json")))!;

    [Fact]
    public async Task FocusPauseDoesNotConsumeActiveTimeoutOrReplayBody()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var calls = 0;
        var ticket = scheduler.Submit(Request(timeout: 1), async (context, token) =>
        {
            calls++;
            using (context.FocusBudget!.Pause()) await Task.Delay(1300, token);
            token.ThrowIfCancellationRequested();
            return OperationOutcome.Completed("resumed same invocation");
        });
        var result = await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(OperationState.Completed, result.State);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task UserCancellationStillEndsFocusPausedRun()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var entered = Signal();
        var ticket = scheduler.Submit(Request(), async (context, token) =>
        {
            using var pause = context.FocusBudget!.Pause();
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return OperationOutcome.Completed("unreachable");
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        ticket.Cancel();
        Assert.Equal(OperationState.Cancelled, (await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(3))).State);
    }

    [Fact]
    public async Task RunsSequentiallyAndCancelsQueuedWorkImmediately()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var entered = Signal(); var release = Signal(); int executed = 0;
        var first = scheduler.Submit(Request(), async (_, token) => { entered.SetResult(); await release.Task.WaitAsync(token); return OperationOutcome.Completed("first"); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var cancelled = scheduler.Submit(Request(), (_, _) => { Interlocked.Increment(ref executed); return Task.FromResult(OperationOutcome.Completed("should not execute")); });
        cancelled.Cancel();
        Assert.Equal(OperationState.Cancelled, (await cancelled.Completion.WaitAsync(TimeSpan.FromSeconds(1))).State);
        var last = scheduler.Submit(Request(), (_, _) => { Interlocked.Increment(ref executed); return Task.FromResult(OperationOutcome.Completed("last")); });
        Assert.False(last.Completion.IsCompleted);
        release.SetResult();
        Assert.Equal(OperationState.Completed, (await first.Completion).State);
        Assert.Equal(OperationState.Completed, (await last.Completion).State);
        Assert.Equal(1, executed);
    }

    [Fact]
    public async Task CancelDoesNotReleaseOwnershipBeforeWorkerFinishesCleanup()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var entered = Signal(); var cleanup = Signal(); var release = Signal();
        var ticket = scheduler.Submit(Request(true), async (context, token) =>
        {
            Assert.True(context.Input!.IsHeld); entered.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cleanup.SetResult(); await release.Task; }
            return OperationOutcome.Completed("unreachable");
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); ticket.Cancel();
        await cleanup.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Throws<InvalidOperationException>(() => HomeMovementLease.Acquire(_root));
        Assert.False(ticket.Completion.IsCompleted);
        release.SetResult();
        Assert.Equal(OperationState.Cancelled, (await ticket.Completion).State);
        using var owner = HomeMovementLease.Acquire(_root); Assert.True(owner.IsHeld);
    }

    [Fact]
    public async Task DeadlineIsDistinctFromUserCancellation()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var ticket = scheduler.Submit(Request(timeout: 1), async (_, token) => { await Task.Delay(Timeout.Infinite, token); return OperationOutcome.Completed("unreachable"); });
        Assert.Equal(OperationState.TimedOut, (await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(4))).State);
    }

    [Fact]
    public async Task InputsChangedWhileQueuedNeverReachExecutor()
    {
        Directory.CreateDirectory(_root); var path = Path.Combine(_root, "pack.json"); File.WriteAllText(path, "old");
        await using var scheduler = new OperationScheduler(_root, _root);
        var entered = Signal(); var release = Signal();
        var first = scheduler.Submit(Request(), async (_, _) => { entered.SetResult(); await release.Task; return OperationOutcome.Completed("release"); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        int executed = 0;
        var next = scheduler.Submit(Request() with { Pins = new Dictionary<string, string> { [path] = GameExperiments.Digest(path) } }, (_, _) => { executed++; return Task.FromResult(OperationOutcome.Completed("bad")); });
        File.WriteAllText(path, "edited"); release.SetResult(); await first.Completion;
        Assert.Equal(OperationState.Failed, (await next.Completion).State); Assert.Equal(0, executed);
    }

    [Fact]
    public async Task RestartMarksUnfinishedJobsInterruptedAndRejectsDuplicateIds()
    {
        var request = Request();
        var folder = Path.Combine(_root, "operations", request.Id); Directory.CreateDirectory(folder);
        GameExperiments.WriteAtomic(Path.Combine(folder, "receipt.json"), new OperationReceipt(request, OperationState.Running, DateTime.UtcNow, "previous process", []));
        await using var scheduler = new OperationScheduler(_root, _root);
        Assert.Equal(OperationState.Interrupted, Read(request.Id).State);
        Assert.Equal(0, scheduler.Pending);
        Assert.Throws<InvalidOperationException>(() => scheduler.Submit(request, (_, _) => Task.FromResult(OperationOutcome.Completed("bad"))));
    }

    [Fact]
    public async Task QueueCapacityAndExclusiveJournalAreEnforced()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        Assert.Throws<IOException>(() => new OperationScheduler(_root, _root));
        var entered = Signal(); var release = Signal();
        var first = scheduler.Submit(Request(), async (_, _) => { entered.SetResult(); await release.Task; return OperationOutcome.Completed("ok"); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var tickets = Enumerable.Range(1, 31).Select(_ => scheduler.Submit(Request(), (_, _) => Task.FromResult(OperationOutcome.Completed("ok")))).ToArray();
        Assert.Throws<InvalidOperationException>(() => scheduler.Submit(Request(), (_, _) => Task.FromResult(OperationOutcome.Completed("overflow"))));
        foreach (var ticket in tickets) ticket.Cancel();
        release.SetResult(); await first.Completion;
    }

    [Fact]
    public async Task CompetingInputOwnerBlocksLiveJobButNotOfflineReview()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        using var home = HomeMovementLease.Acquire(_root);
        var live = scheduler.Submit(Request(true), (_, _) => throw new Xunit.Sdk.XunitException("Input conflict must prevent execution"));
        Assert.Equal(OperationState.Failed, (await live.Completion).State);
        var offline = scheduler.Submit(Request(), (context, _) => { Assert.Null(context.Input); return Task.FromResult(OperationOutcome.Completed("offline")); });
        Assert.Equal(OperationState.Completed, (await offline.Completion).State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1801)]
    public void UnboundedOrInvalidBudgetsAreRejected(int seconds) => Assert.Throws<InvalidDataException>(() => OperationScheduler.Validate(Request(timeout: seconds)));

    [Fact]
    public void LegacyInboxRejectsWholeInvalidBatchBeforeAdmission()
    {
        var valid = new ProjectMaelstrom.Models.SmartPlayTaskDefinition { Type = "nav", Target = "pet" };
        var endless = new ProjectMaelstrom.Models.SmartPlayTaskDefinition { Type = "script_run", ScriptName = "teacher", RepeatCount = 0 };
        Assert.Throws<InvalidDataException>(() => SmartPlayManager.ValidateInbox([valid, endless], 32));
        Assert.Throws<InvalidDataException>(() => SmartPlayManager.ValidateInbox([valid], 0));
        SmartPlayManager.ValidateInbox([valid], 1);
    }

    [Fact]
    public async Task CancellationCallbacksCanReadQueueWithoutDeadlock()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var entered = Signal(); bool callbackCompleted = false;
        var ticket = scheduler.Submit(Request(), async (_, token) =>
        {
            using var registration = token.Register(() => { callbackCompleted = Task.Run(() => scheduler.Pending).Wait(TimeSpan.FromSeconds(1)); });
            entered.SetResult(); await Task.Delay(Timeout.Infinite, token);
            return OperationOutcome.Completed("unreachable");
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        ticket.Cancel(); await ticket.Completion;
        Assert.True(callbackCompleted);
    }

    [Fact]
    public async Task FailedInputCleanupBlocksNextLiveOwnerButPreservesOfflineWork()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var failed = scheduler.Submit(Request(true), (_, _) => Task.FromResult(new OperationOutcome(OperationState.CleanupFailed, "Test release was not confirmed", [])));
        Assert.Equal(OperationState.CleanupFailed, (await failed.Completion).State);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("new owner", _root));
        var offline = scheduler.Submit(Request(), (_, _) => Task.FromResult(OperationOutcome.Completed("review evidence")));
        Assert.Equal(OperationState.Completed, (await offline.Completion).State);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
