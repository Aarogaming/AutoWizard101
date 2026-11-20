using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class OperationRecoveryFailureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-operation-recovery-" + Guid.NewGuid().ToString("N"));
    private string Leases => Path.Combine(_root, "shared-leases");
    private string Notice => Path.Combine(Leases, "input-recovery-required.txt");
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static OperationRequest Request(bool live = true) => new(Guid.NewGuid().ToString("N"),
        live ? "test.recovery-live" : "test.offline-review", "test-v1", 15, live,
        live ? "isolated-test-target" : "saved-evidence", new Dictionary<string, string>());

    private Exception? FailNoticeWrite(GameInputLease owner)
    {
        // Create the obstruction only after ownership was granted. Keep it through
        // terminal processing, then remove it before testing later admission.
        Directory.CreateDirectory(Notice);
        try
        {
            owner.RequireRecovery("test: input release could not be confirmed");
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return error; }
    }
    private void RemoveNoticeObstruction() { if (Directory.Exists(Notice)) Directory.Delete(Notice); }

    [Fact]
    public async Task MissingRecoveryNoticeBlocksBothSchedulersWhileOfflineWorkContinues()
    {
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "first"), Leases);
        await using var other = new OperationScheduler(Path.Combine(_root, "second"), Leases);
        var entered = Signal(); var release = Signal();
        Exception? noticeFailure = null; GameInputLease? originalOwner = null;
        bool currentHidden = false, offlineHadOwnership = true; int liveCallbacks = 0;
        var failed = scheduler.Submit(Request(), async (context, _) =>
        {
            originalOwner = context.Input; entered.TrySetResult(); await release.Task;
            noticeFailure = FailNoticeWrite(context.Input!);
            currentHidden = GameInputLease.Current is null;
            return new(OperationState.CleanupFailed, "Release remains uncertain despite unavailable notice.", []);
        });
        try
        {
            await entered.Task.WaitAsync(WaitLimit);
            var next = scheduler.Submit(Request(), (_, _) =>
            { Interlocked.Increment(ref liveCallbacks); return Task.FromResult(OperationOutcome.Completed("must not execute")); });
            var offline = scheduler.Submit(Request(false), (context, _) =>
            {
                offlineHadOwnership = context.Input is not null || GameInputLease.Current is not null;
                return Task.FromResult(OperationOutcome.Completed("offline review ran"));
            });
            release.TrySetResult();
            Assert.Equal(OperationState.CleanupFailed, (await failed.Completion.WaitAsync(WaitLimit)).State);
            RemoveNoticeObstruction();
            Assert.NotNull(noticeFailure); Assert.True(currentHidden);
            Assert.NotNull(originalOwner); Assert.True(originalOwner!.RecoveryRequired); Assert.False(originalOwner.IsHeld);
            Assert.False(Path.Exists(Notice));
            var afterRemoval = scheduler.Submit(Request(), (_, _) =>
            { Interlocked.Increment(ref liveCallbacks); return Task.FromResult(OperationOutcome.Completed("must not execute")); });
            Assert.Equal(OperationState.Failed, (await next.Completion.WaitAsync(WaitLimit)).State);
            Assert.Equal(OperationState.Failed, (await afterRemoval.Completion.WaitAsync(WaitLimit)).State);
            Assert.Equal(OperationState.Completed, (await offline.Completion.WaitAsync(WaitLimit)).State);
            Assert.False(offlineHadOwnership);

            var otherLive = other.Submit(Request(), (_, _) =>
            { Interlocked.Increment(ref liveCallbacks); return Task.FromResult(OperationOutcome.Completed("must not execute")); });
            var otherOffline = other.Submit(Request(false), (context, _) =>
            {
                offlineHadOwnership |= context.Input is not null || GameInputLease.Current is not null;
                return Task.FromResult(OperationOutcome.Completed("second offline review ran"));
            });
            Assert.Equal(OperationState.Failed, (await otherLive.Completion.WaitAsync(WaitLimit)).State);
            Assert.Equal(OperationState.Completed, (await otherOffline.Completion.WaitAsync(WaitLimit)).State);
            Assert.Equal(0, liveCallbacks); Assert.False(offlineHadOwnership); Assert.False(Path.Exists(Notice));
        }
        finally { release.TrySetResult(); RemoveNoticeObstruction(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkerCompletionOrCancellationCannotEraseUncertainCleanup(bool cancel)
    {
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "jobs"), Leases);
        var poisoned = Signal(); var release = Signal(); Exception? noticeFailure = null;
        var ticket = scheduler.Submit(Request(), async (context, token) =>
        {
            noticeFailure = FailNoticeWrite(context.Input!);
            poisoned.TrySetResult(); await release.Task;
            if (cancel) token.ThrowIfCancellationRequested();
            return OperationOutcome.Completed("Worker mistakenly claims completion after uncertain cleanup.");
        });
        try
        {
            await poisoned.Task.WaitAsync(WaitLimit);
            if (cancel) ticket.Cancel();
            release.TrySetResult();
            var receipt = await ticket.Completion.WaitAsync(WaitLimit);
            RemoveNoticeObstruction();
            Assert.NotNull(noticeFailure); Assert.False(Path.Exists(Notice));
            Assert.Equal(OperationState.CleanupFailed, receipt.State);
            Assert.Equal(OperationState.CleanupFailed,
                Assert.Single(OperationScheduler.History(Path.Combine(_root, "jobs"))).State);
        }
        finally { release.TrySetResult(); RemoveNoticeObstruction(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PoisonedOwnedAdapterRejectsNextFreshProposalWithoutAnotherTransportCall(bool transportThrows)
    {
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "jobs"), Leases);
        var bundle = W101ModulePrograms.NativeIdle(); var binding = ModuleScreenSessionTests.Binding(bundle);
        RoutineReceipt? first = null, second = null; int calls = 0; bool poisonObserved = false;
        var ticket = ModuleOperation.SubmitOwned(scheduler, bundle, binding, async (context, token) =>
        {
            var input = new ModuleOwnedInput(context, binding, ModuleScreenSessionTests.Geometry, () => true, () => 200,
                (_, _, _) =>
                {
                    calls++; Directory.CreateDirectory(Notice);
                    if (transportThrows) throw new IOException("test transport failed without a release receipt");
                    return Task.FromResult(new PetDanceMenuReceipt(true, false, "test release was not confirmed"));
                });
            first = await input.DispatchAsync(Proposal(binding, "first", 100), token);
            poisonObserved = context.Input!.RecoveryRequired && !context.Input.CanDispatch && GameInputLease.Current is null;
            second = await input.DispatchAsync(Proposal(binding, "second", 150), token);
            return OperationOutcome.Completed("Uncertain adapter release must override this result.");
        });
        try
        {
            var result = await ticket.Completion.WaitAsync(WaitLimit);
            RemoveNoticeObstruction();
            Assert.NotNull(first); Assert.False(first!.InputReleased);
            Assert.NotNull(second); Assert.False(second!.Accepted);
            Assert.True(poisonObserved); Assert.Equal(1, calls); Assert.False(Path.Exists(Notice));
            Assert.Equal(OperationState.CleanupFailed, result.State);
        }
        finally { RemoveNoticeObstruction(); }
    }

    private static ModuleScreenDecision Proposal(RoutineBinding binding, string id, double captured)
    {
        var evidence = ModuleScreenSessionTests.Evidence("graphics-menu", captured, binding);
        // Test-only provenance to exercise the owned adapter; there is no OS sink,
        // local OCR invocation or assertion that these are live observations.
        evidence = evidence with { Frame = evidence.Frame with { TextOrigin = "local-windows-ocr" } };
        var action = W101ModulePrograms.Policy().Actions[^1];
        return new(new("idle", "menu", "action-proposed", id, action.ActionId, action), evidence, true);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
