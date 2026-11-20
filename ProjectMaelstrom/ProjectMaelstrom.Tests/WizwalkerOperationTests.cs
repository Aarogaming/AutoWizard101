using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class WizwalkerOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-wizwalker-" + Guid.NewGuid().ToString("N"));
    private string Locks => Path.Combine(_root, "locks");
    private readonly WizwalkerLaunch _launch;
    private readonly WizwalkerTarget _target;
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public WizwalkerOperationTests()
    {
        Directory.CreateDirectory(_root);
        string FileAt(string name) { var path = Path.Combine(_root, name); File.WriteAllText(path, "inert fixture"); return path; }
        _launch = new(FileAt("python.exe"), FileAt("worker.py"), _root);
        var image = FileAt("WizardGraphicalClient.exe");
        _target = new(42, 123, 100, "WizardGraphicalClient", image, GameExperiments.Digest(image));
    }

    private WizwalkerResult Result(WizwalkerRequest request) => new(1, request.RunId, request.Operation, request.SourceSha256,
        request.Target, "observed", "loading-state-observed", false, DateTime.UtcNow.Ticks, 1, true);
    private WizwalkerRuntime Runtime(Func<WizwalkerRequest, WizwalkerResult>? result = null) => new(_ => true,
        (_, request, context, _) =>
        {
            Assert.True(context.Input!.CanDispatch);
            Assert.Same(context.Input, GameInputLease.Current);
            Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competitor", Locks));
            return Task.FromResult((result ?? Result)(request));
        });

    [Fact]
    public async Task DefaultOffRejectsWithoutSchedulingOrCallingWorker()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        Assert.Throws<InvalidOperationException>(() => WizwalkerOperation.Submit(scheduler, new(_target), _launch, Runtime()));
        Assert.Empty(OperationScheduler.History(_root));
        Assert.False(File.Exists(Path.Combine(Locks, "game-input.lock")));
    }

    [Fact]
    public async Task ProductionAdmissionRejectsUnreviewedWorkerBeforeTargetOrProcessAccess()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var error = Assert.Throws<InvalidOperationException>(() => WizwalkerOperation.Submit(scheduler, new(_target, true), _launch));
        Assert.Contains("reviewed client adapter changed", error.Message);
        Assert.Empty(OperationScheduler.History(_root));
    }

    [Fact]
    public async Task ObservesWithExistingOwnerAndSavesEvidenceThenReleases()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var ticket = WizwalkerOperation.Submit(scheduler, new(_target, true), _launch, Runtime());
        var receipt = await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(4));
        Assert.Equal(OperationState.Completed, receipt.State);
        Assert.True(ticket.Request.WritesGame); Assert.False(ticket.Request.DelegatedInput);
        Assert.Equal(2, receipt.Artifacts.Length);
        Assert.All(receipt.Artifacts, path => Assert.True(File.Exists(path)));
        Assert.Contains("not loading", receipt.Message);
        Assert.Contains("does not prove", receipt.Message);
        using var next = GameInputLease.Acquire("next", Locks);
        Assert.True(next.CanDispatch);
        Assert.NotNull(W101InteractionPolicy.ToolBlockReason("wizwalker"));
    }

    [Fact]
    public async Task AnotherControllerPreventsConnection()
    {
        using var owner = GameInputLease.Acquire("other", Locks);
        await using var scheduler = new OperationScheduler(_root, Locks);
        int called = 0;
        var runtime = new WizwalkerRuntime(_ => true, (_, request, _, _) => { called++; return Task.FromResult(Result(request)); });
        var ticket = WizwalkerOperation.Submit(scheduler, new(_target, true), _launch, runtime);
        Assert.Equal(OperationState.Failed, (await ticket.Completion).State);
        Assert.Equal(0, called);
    }

    [Fact]
    public async Task MissingDependencyExplainsRepairWithoutBlockingTheNextOwner()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var runtime = Runtime(request => Result(request) with { Status = "unavailable", Code = "dependency-unavailable", Loading = null });
        var receipt = await WizwalkerOperation.Submit(scheduler, new(_target, true), _launch, runtime).Completion;
        Assert.Equal(OperationState.Failed, receipt.State);
        Assert.Contains("dependencies", receipt.Message);
        Assert.Contains("No client was opened", receipt.Message);
        using var next = GameInputLease.Acquire("next", Locks);
        Assert.True(next.CanDispatch);
    }

    [Fact]
    public async Task QueuedTargetReplacementDoesNotReachWorker()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var entered = Signal(); var release = Signal();
        var blocker = scheduler.Submit(new(Guid.NewGuid().ToString("N"), "offline", "v1", 10, false, "offline", new Dictionary<string, string>()),
            async (_, _) => { entered.SetResult(); await release.Task; return OperationOutcome.Completed("done"); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        bool current = true; int calls = 0;
        var runtime = new WizwalkerRuntime(_ => current, (_, request, _, _) => { calls++; return Task.FromResult(Result(request)); });
        var next = WizwalkerOperation.Submit(scheduler, new(_target, true), _launch, runtime);
        current = false; release.SetResult(); await blocker.Completion;
        Assert.Equal(OperationState.Failed, (await next.Completion).State);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task QueuedSourceChangeDoesNotReachWorker()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var entered = Signal(); var release = Signal();
        var blocker = scheduler.Submit(new(Guid.NewGuid().ToString("N"), "offline", "v1", 10, false, "offline", new Dictionary<string, string>()),
            async (_, _) => { entered.SetResult(); await release.Task; return OperationOutcome.Completed("done"); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        int calls = 0;
        var runtime = new WizwalkerRuntime(_ => true, (_, request, _, _) => { calls++; return Task.FromResult(Result(request)); });
        var ticket = WizwalkerOperation.Submit(scheduler, new(_target, true), _launch, runtime);
        File.WriteAllText(_launch.Worker, "changed after admission");
        release.SetResult(); await blocker.Completion;
        Assert.Equal(OperationState.Failed, (await ticket.Completion).State); Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("unknown")]
    [InlineData("unavailable")]
    public async Task InvalidObservationNeverBecomesSuccess(string kind)
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var runtime = Runtime(request => kind switch
        {
            "stale" => Result(request) with { ObservedUtcTicks = DateTime.UtcNow.AddMinutes(-1).Ticks },
            "future" => Result(request) with { ObservedUtcTicks = DateTime.UtcNow.AddMinutes(1).Ticks },
            "unknown" => Result(request) with { Loading = null },
            _ => Result(request) with { Status = "unavailable", Loading = null }
        });
        Assert.Equal(OperationState.Failed, (await WizwalkerOperation.Submit(scheduler, new(_target, true), _launch, runtime).Completion).State);
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    [Theory]
    [InlineData("cleanup")]
    [InlineData("run")]
    [InlineData("target")]
    [InlineData("method")]
    [InlineData("source")]
    [InlineData("protocol")]
    public async Task UnconfirmedDisconnectOrProvenanceBlocksNextOwner(string kind)
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var runtime = Runtime(request => kind switch
        {
            "cleanup" => Result(request) with { CleanupConfirmed = false },
            "run" => Result(request) with { RunId = Guid.NewGuid().ToString("N") },
            "target" => Result(request) with { Target = _target with { StartedUtcTicks = 101 } },
            "method" => Result(request) with { Operation = "teleport" },
            "source" => Result(request) with { SourceSha256 = new string('0', 64) },
            _ => Result(request) with { Status = "unexpected" }
        });
        var receipt = await WizwalkerOperation.Submit(scheduler, new(_target, true), _launch, runtime).Completion;
        Assert.Equal(OperationState.CleanupFailed, receipt.State);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", Locks));
    }

    [Fact]
    public async Task FailedWarningSaveCannotUnblockUncertainCleanup()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var runtime = Runtime(request =>
        {
            Directory.CreateDirectory(Path.Combine(Locks, "input-recovery-required.txt"));
            return Result(request) with { CleanupConfirmed = false };
        });
        var receipt = await WizwalkerOperation.Submit(scheduler, new(_target, true), _launch, runtime).Completion;
        Assert.Equal(OperationState.CleanupFailed, receipt.State);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", Locks));
    }

    [Fact]
    public async Task CancelHoldsOwnerUntilWorkerConfirmsDisconnect()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var entered = Signal(); var cleaning = Signal(); var finish = Signal();
        var runtime = new WizwalkerRuntime(_ => true, async (_, request, _, token) =>
        {
            entered.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { cleaning.SetResult(); await finish.Task; }
            return Result(request) with { Status = "cancelled", Loading = null };
        });
        var ticket = WizwalkerOperation.Submit(scheduler, new(_target, true), _launch, runtime);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); ticket.Cancel();
        await cleaning.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(ticket.Completion.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competitor", Locks));
        finish.SetResult(); Assert.Equal(OperationState.Cancelled, (await ticket.Completion).State);
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    [Fact]
    public async Task WorkerExceptionIsNotAConfirmedDisconnect()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var runtime = new WizwalkerRuntime(_ => true, (_, _, _, _) => throw new IOException("worker failed"));
        Assert.Equal(OperationState.CleanupFailed, (await WizwalkerOperation.Submit(scheduler, new(_target, true), _launch, runtime).Completion).State);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", Locks));
    }

    [Fact]
    public void LaunchUsesIsolatedPythonNoWindowAndNoShell()
    {
        var info = WizwalkerWorker.BuildStartInfo(_launch, _root);
        Assert.False(info.UseShellExecute); Assert.True(info.CreateNoWindow);
        Assert.True(info.RedirectStandardInput);
        Assert.Equal(new[] { "-I", "-B", _launch.Worker, "--directory", _root }, info.ArgumentList);
    }

    private WizwalkerLaunch FakeWorkerLaunch(string source)
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "tools", "fixtures", "wizwalker_fake_worker.py"))) repo = repo.Parent;
        Assert.NotNull(repo);
        var python = Environment.GetEnvironmentVariable("MAELSTROM_TEST_PYTHON") ??
            (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(p => Path.Combine(p, "python.exe")).FirstOrDefault(File.Exists);
        Assert.True(File.Exists(python), "Set MAELSTROM_TEST_PYTHON to the existing Python executable for this offline transport test.");
        return new WizwalkerLaunch(python!, Path.Combine(repo!.FullName, "tools", "fixtures", "wizwalker_fake_worker.py"), source);
    }

    [Fact]
    public async Task RealWorkerTransportWithFakePythonClientUsesSameScheduler()
    {
        var launch = FakeWorkerLaunch(_root);
        await using var scheduler = new OperationScheduler(_root, Locks);
        var runtime = new WizwalkerRuntime(_ => true, WizwalkerWorker.ExecuteAsync);
        var receipt = await WizwalkerOperation.Submit(scheduler, new(_target, true), launch, runtime).Completion.WaitAsync(TimeSpan.FromSeconds(25));
        Assert.Equal(OperationState.Completed, receipt.State);
        var result = JsonSerializer.Deserialize<WizwalkerResult>(File.ReadAllText(receipt.Artifacts[1]));
        Assert.True(result!.Loading); Assert.True(result.CleanupConfirmed);
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualChildCancellationConfirmsExitOrKeepsRecoveryBlocked(bool forced)
    {
        var source = Path.Combine(_root, forced ? "uncooperative" : "cooperative"); Directory.CreateDirectory(source);
        await using var scheduler = new OperationScheduler(_root, Locks);
        var runtime = new WizwalkerRuntime(_ => true, WizwalkerWorker.ExecuteAsync);
        var ticket = WizwalkerOperation.Submit(scheduler, new(_target, true), FakeWorkerLaunch(source), runtime);
        var marker = Path.Combine(_root, "operations", ticket.Request.Id, "fake-read-started");
        // Python start-up on a loaded CI runner has taken longer than the previous 4 s bound; wait generously and say which case failed.
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!File.Exists(marker) && !ticket.Completion.IsCompleted && DateTime.UtcNow < deadline) await Task.Delay(20);
        Assert.True(File.Exists(marker), ticket.Completion.IsCompleted
            ? "The operation finished before the fake Python client reached its read."
            : "Fake Python client did not reach its read within 20 seconds.");
        var pid = int.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(marker)!, "fake-worker.pid")));
        using var child = System.Diagnostics.Process.GetProcessById(pid);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competitor", Locks));
        ticket.Cancel();
        // The forced path chains several 2 s transport waits (grace, kill, output drain), so 8 s left little slack.
        var receipt = await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.True(child.HasExited, "The owned fake worker must exit before its terminal receipt.");
        Assert.Equal(forced ? OperationState.CleanupFailed : OperationState.Cancelled, receipt.State);
        if (forced) Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", Locks));
        else { using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch); }
    }

    public void Dispose()
    {
        // Windows can briefly retain directory handles after a confirmed child exit.
        // Bound this fixture-only retry; a worker still alive fails the assertion above.
        for (var attempt = 0; Directory.Exists(_root); attempt++)
        {
            try { Directory.Delete(_root, true); return; }
            catch (IOException) when (attempt < 20) { Thread.Sleep(50); }
        }
    }
}
