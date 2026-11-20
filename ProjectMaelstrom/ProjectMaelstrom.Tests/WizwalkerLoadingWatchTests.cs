using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class WizwalkerLoadingWatchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-loading-watch-" + Guid.NewGuid().ToString("N"));
    private readonly WizwalkerLaunch _launch;
    private readonly WizwalkerTarget _target;
    private string Locks => Path.Combine(_root, "locks");
    public WizwalkerLoadingWatchTests()
    {
        Directory.CreateDirectory(_root);
        string Fixture(string name) { var path = Path.Combine(_root, name); File.WriteAllText(path, "inert fixture"); return path; }
        _launch = new(Fixture("python.exe"), Fixture("worker.py"), _root);
        var image = Fixture("WizardGraphicalClient.exe");
        _target = new(42, 123, 100, "WizardGraphicalClient", image, GameExperiments.Digest(image));
    }
    private static WizwalkerResult Result(WizwalkerRequest r, bool loading) =>
        new(1, r.RunId, r.Operation, r.SourceSha256, r.Target, "observed", "loading-state-observed", loading, DateTime.UtcNow.Ticks, 1, true);

    [Fact]
    public async Task SequenceUsesOneOwnerAndEveryWorkerDisconnectsBeforeNextRead()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        int calls = 0; GameInputLease? owner = null;
        var runtime = new WizwalkerRuntime(_ => true, (_, r, context, _) =>
        {
            owner ??= context.Input; Assert.Same(owner, context.Input); Assert.Same(owner, GameInputLease.Current);
            Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competitor", Locks));
            Assert.EndsWith("sample-" + calls.ToString("D3"), context.Directory);
            return Task.FromResult(Result(r, calls++ == 1));
        });
        var request = WizwalkerOperation.Prepare(new(_target, true), _launch, runtime, WizwalkerLoadingWatch.Id, WizwalkerLoadingWatch.Version);
        // This checks ownership and sequence, not disk speed. The three-sample cap
        // ends promptly; a one-second deadline can expire while durable receipts flush.
        var ticket = scheduler.Submit(request, (context, token) => WizwalkerLoadingWatch.RunOwnedAsync(context, _target, _launch, token, runtime, 3, 10000));
        var receipt = await ticket.Completion;
        Assert.Equal(OperationState.Completed, receipt.State); Assert.Equal(3, calls);
        using var json = JsonDocument.Parse(File.ReadAllText(receipt.Artifacts[1]));
        Assert.True(json.RootElement.GetProperty("observedClearLoadingClear").GetBoolean());
        Assert.Equal(1, json.RootElement.GetProperty("loadingSamples").GetInt32());
        Assert.Equal(3, File.ReadAllLines(receipt.Artifacts[0]).Length);
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    [Fact]
    public async Task FailedObservationStopsInsteadOfTreatingItAsClear()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        int calls = 0;
        var runtime = new WizwalkerRuntime(_ => true, (_, r, _, _) => { calls++; return Task.FromResult(Result(r, false) with { Status = "unavailable", Loading = null }); });
        var receipt = await WizwalkerLoadingWatch.Submit(scheduler, new(_target, true), _launch, runtime).Completion;
        Assert.Equal(OperationState.Failed, receipt.State); Assert.Equal(1, calls);
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    [Fact]
    public async Task FailedDisconnectStopsAndPreservesRecoveryBlock()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var runtime = new WizwalkerRuntime(_ => true, (_, r, _, _) => Task.FromResult(Result(r, false) with { CleanupConfirmed = false }));
        var receipt = await WizwalkerLoadingWatch.Submit(scheduler, new(_target, true), _launch, runtime).Completion;
        Assert.Equal(OperationState.CleanupFailed, receipt.State);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", Locks));
    }

    [Fact]
    public async Task CancellationDuringObservationRetainsOwnerThroughCleanup()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool closed = false;
        var runtime = new WizwalkerRuntime(_ => true, async (_, r, context, token) =>
        {
            entered.SetResult();
            try { await Task.Delay(10000, token); } catch (OperationCanceledException) { }
            Assert.True(context.Input!.CanDispatch); closed = true;
            return Result(r, false) with { Status = "cancelled", Loading = null };
        });
        var ticket = WizwalkerLoadingWatch.Submit(scheduler, new(_target, true), _launch, runtime);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); ticket.Cancel();
        Assert.Equal(OperationState.Cancelled, (await ticket.Completion).State); Assert.True(closed);
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    [Fact]
    public async Task DefaultOffStillRejectsWatchBeforeScheduling()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        Assert.Throws<InvalidOperationException>(() => WizwalkerLoadingWatch.Submit(scheduler, new(_target), _launch));
        Assert.Empty(OperationScheduler.History(_root));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
