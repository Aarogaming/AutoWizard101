using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class InputRecoveryOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-recovery-flow-" + Guid.NewGuid().ToString("N"));
    private long _clock = 100;
    private int _reads, _recoveries;
    private readonly List<string> _challenges = [];
    private Func<InputRecoverySample, InputRecoverySample> _change = sample => sample;
    private InputRecoveryOperation Create(
        Func<string, Func<InputRecoveryChallenge, CancellationToken, Task<InputRecoverySample>>, CancellationToken, Task<InputRecoveryResult>>? recover = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null) => new(_root, new Probe((challenge, stopped) =>
        {
            _reads++; _challenges.Add(challenge.Id);
            return _change(new(challenge.Id, _clock, 7, stopped, true, true, true));
        }), recover ?? Recover, () => _clock, delay ?? Advance);
    private Task Advance(TimeSpan duration, CancellationToken token)
    { token.ThrowIfCancellationRequested(); _clock += (long)duration.TotalMilliseconds; return Task.CompletedTask; }
    private Task<InputRecoveryResult> Recover(string directory,
        Func<InputRecoveryChallenge, CancellationToken, Task<InputRecoverySample>> verify, CancellationToken token)
    {
        _recoveries++;
        return GameInputRecovery.RunAsync(directory, verify, token,
            new(() => _clock, Advance, GameInputLease.PersistState, GameInputRecovery.Publish));
    }
    private void Seed()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "game-input.lock"), "1");
        File.WriteAllText(Path.Combine(_root, "input-recovery-required.txt"), "Uncertain release");
    }
    private static OperationRequest Live() => new(Guid.NewGuid().ToString("N"), "test.recovery", "v1", 10, true, "fake-target", new Dictionary<string, string>());
    private static Task<OperationOutcome> Complete(OperationContext _, CancellationToken __) => Task.FromResult(OperationOutcome.Completed("No input was sent."));

    [Fact]
    public async Task OpeningAndOutOfOrderActionsNeitherProbeNorClearRecovery()
    {
        Seed(); using var flow = Create();
        Assert.Equal(RecoveryFlowStage.Initial, flow.State.Stage);
        Assert.False((await flow.CheckAsync(default)).CanRestore);
        Assert.False((await flow.RestoreAsync(default)).CanRestore);
        Assert.Equal(0, _reads); Assert.Equal(0, _recoveries);
        Assert.Equal("1", File.ReadAllText(Path.Combine(_root, "game-input.lock")));
    }

    [Fact]
    public async Task PreviewDoesNotResetAndRestoreUsesNewBoundProofBeforeExplicitNewRun()
    {
        Seed(); await using var scheduler = new OperationScheduler(Path.Combine(_root, "jobs"), _root);
        using var flow = Create();
        Assert.False((await flow.StopAsync(default)).CanRestore);
        Assert.Throws<InvalidOperationException>(() => scheduler.Submit(Live(), Complete));
        Assert.True((await flow.CheckAsync(default)).CanRestore);
        Assert.Equal(2, _reads); Assert.Equal(0, _recoveries);
        Assert.Equal("1", File.ReadAllText(Path.Combine(_root, "game-input.lock")));
        var result = await flow.RestoreAsync(default);
        Assert.Equal(RecoveryFlowStage.Restored, result.Stage);
        Assert.False(result.CanRestore); Assert.NotNull(result.EvidenceDirectory);
        Assert.Equal(4, _reads); Assert.Equal(1, _recoveries);
        Assert.Equal(_challenges[0], _challenges[1]); Assert.Equal(_challenges[2], _challenges[3]);
        Assert.NotEqual(_challenges[0], _challenges[2]);
        Assert.Equal("0", File.ReadAllText(Path.Combine(_root, "game-input.lock")));
        Assert.Equal(0, scheduler.Pending);
        var ticket = scheduler.Submit(Live(), Complete);
        Assert.Equal(OperationState.Completed, (await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(5))).State);
    }

    [Fact]
    public async Task NewlyHeldInputAfterPreviewBlocksRestoreAndAdmission()
    {
        Seed(); await using var scheduler = new OperationScheduler(Path.Combine(_root, "jobs"), _root);
        using var flow = Create(); await flow.StopAsync(default); await flow.CheckAsync(default);
        _change = sample => sample with { InputsReleased = false };
        var result = await flow.RestoreAsync(default);
        Assert.Equal(RecoveryFlowStage.Blocked, result.Stage); Assert.False(result.CanRestore);
        Assert.Contains("could not be verified", result.Message);
        Assert.Equal("1", File.ReadAllText(Path.Combine(_root, "game-input.lock")));
        Assert.Throws<InvalidOperationException>(() => scheduler.Submit(Live(), Complete));
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("blocked", _root));
    }

    [Theory]
    [InlineData("workers")]
    [InlineData("desktop")]
    [InlineData("unknown")]
    [InlineData("held")]
    [InlineData("challenge")]
    [InlineData("future")]
    [InlineData("old")]
    [InlineData("revision")]
    public async Task PreviewRequiresCompleteFreshEvidence(string failure)
    {
        using var flow = Create(); await flow.StopAsync(default);
        _change = sample => failure switch
        {
            "workers" => sample with { WorkersStopped = false },
            "desktop" => sample with { DesktopAvailable = false },
            "unknown" => sample with { InputStateKnown = false },
            "held" => sample with { InputsReleased = false },
            "challenge" => sample with { ChallengeId = "old-attempt" },
            "future" => sample with { ObservedAtMilliseconds = _clock + 1 },
            "old" => sample with { ObservedAtMilliseconds = _clock - 101 },
            _ => sample with { ActivityRevision = -1 }
        };
        var result = await flow.CheckAsync(default);
        Assert.Equal(RecoveryFlowStage.Blocked, result.Stage); Assert.False(result.CanRestore);
        Assert.Equal(0, _recoveries);
        if (failure is "desktop" or "unknown") Assert.Contains("keep Maelstrom active", result.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PreviewRequiresFullQuietInterval(bool activityChanged)
    {
        using var flow = Create(delay: (duration, _) =>
        {
            _clock += activityChanged ? 1000 : 999;
            if (activityChanged) _change = sample => sample with { ActivityRevision = 8 };
            return Task.CompletedTask;
        });
        await flow.StopAsync(default);
        Assert.False((await flow.CheckAsync(default)).CanRestore);
        Assert.Equal(0, _recoveries);
    }

    [Fact]
    public async Task CancelledCheckRetainsHoldAndCanBeRetried()
    {
        using var cancel = new CancellationTokenSource();
        using var flow = Create(delay: (duration, token) =>
        { cancel.Cancel(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; });
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "jobs"), _root);
        await flow.StopAsync(default);
        Assert.Equal(RecoveryFlowStage.Blocked, (await flow.CheckAsync(cancel.Token)).Stage);
        Assert.Throws<InvalidOperationException>(() => scheduler.Submit(Live(), Complete));
        Assert.Equal(0, _recoveries);
    }

    [Fact]
    public async Task DisposeDoesNotReleaseBarrierUntilPendingVerificationReturns()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "jobs"), _root);
        using var flow = Create(recover: async (_, _, _) =>
        { entered.TrySetResult(); await release.Task; return new(false, "cancelled", "ignored", null); });
        await flow.StopAsync(default); await flow.CheckAsync(default);
        var pending = flow.RestoreAsync(default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); flow.Dispose();
            Assert.Throws<InvalidOperationException>(() => scheduler.Submit(Live(), Complete));
            Assert.False(pending.IsCompleted);
        }
        finally { release.TrySetResult(); }
        Assert.Equal(RecoveryFlowStage.Blocked, (await pending.WaitAsync(TimeSpan.FromSeconds(5))).Stage);
        var ticket = scheduler.Submit(Live(), Complete);
        Assert.Equal(OperationState.Completed, (await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(5))).State);
    }

    [Fact]
    public async Task CancellationAfterSuccessfulCommitDoesNotMisreportFailure()
    {
        Seed(); using var cancel = new CancellationTokenSource();
        using var flow = Create(recover: async (directory, verify, token) =>
        { var result = await Recover(directory, verify, token); cancel.Cancel(); return result; });
        await flow.StopAsync(default); await flow.CheckAsync(default);
        Assert.Equal(RecoveryFlowStage.Restored, (await flow.RestoreAsync(cancel.Token)).Stage);
        Assert.Equal("0", File.ReadAllText(Path.Combine(_root, "game-input.lock")));
    }

    [Theory]
    [InlineData("owner-or-recovery-changed", "Another worker")]
    [InlineData("timed-out", "took too long")]
    [InlineData("verification-or-storage-failed", "could not be verified")]
    [InlineData("unknown-code", "disk space")]
    public async Task TransactionDiagnosticsAreExplainedWithoutRawPaths(string code, string expected)
    {
        using var flow = Create(recover: (_, _, _) => Task.FromResult(new InputRecoveryResult(false, code, "private/raw/path/password", null)));
        await flow.StopAsync(default); await flow.CheckAsync(default);
        var result = await flow.RestoreAsync(default);
        Assert.Equal(RecoveryFlowStage.Blocked, result.Stage); Assert.Contains(expected, result.Message);
        Assert.DoesNotContain("private", result.Message); Assert.False(result.CanRestore);
    }

    [Fact]
    public async Task CommittedCloseWarningIsTruthfulAndHidesInternalDiagnostic()
    {
        using var flow = Create(recover: (_, _, _) => Task.FromResult(new InputRecoveryResult(true,
            "recovered-with-close-warning", "private/path", null)));
        await flow.StopAsync(default); await flow.CheckAsync(default);
        var result = await flow.RestoreAsync(default);
        Assert.Equal(RecoveryFlowStage.Restored, result.Stage);
        Assert.Contains("restart Maelstrom", result.Message);
        Assert.DoesNotContain("private", result.Message);
    }

    [Fact]
    public async Task ExceptionsDoNotLeakDetailsOrEnableRetryWithoutRechecking()
    {
        using var flow = Create(recover: (_, _, _) => throw new IOException("private/path"));
        await flow.StopAsync(default); await flow.CheckAsync(default);
        var result = await flow.RestoreAsync(default);
        Assert.Equal(RecoveryFlowStage.Blocked, result.Stage); Assert.False(result.CanRestore);
        Assert.DoesNotContain("private", result.Message);
        Assert.Contains("Stop routines", (await flow.RestoreAsync(default)).Message);
    }

    [Theory]
    [InlineData("good", true, true)]
    [InlineData("session", false, false)]
    [InlineData("desktop", false, false)]
    [InlineData("same-desktop", false, false)]
    [InlineData("foreground", false, false)]
    [InlineData("readable", false, false)]
    [InlineData("held", true, false)]
    [InlineData("exception", false, false)]
    public void WindowsAdapterRequiresPositiveFactsWithoutAccessingDesktop(string failure, bool known, bool released)
    {
        var probe = new WindowsInputRecoveryProbe(() =>
        {
            if (failure == "exception") throw new IOException("private/path");
            return new(failure != "session", failure != "desktop", failure != "same-desktop",
                failure != "foreground", failure != "readable", failure == "held", 4);
        }, () => 100);
        var sample = probe.Read(new("fresh", 100), true);
        Assert.Equal("fresh", sample.ChallengeId); Assert.Equal(100, sample.ObservedAtMilliseconds);
        Assert.True(sample.WorkersStopped); Assert.Equal(known, sample.InputStateKnown); Assert.Equal(released, sample.InputsReleased);
    }

    private sealed class Probe(Func<InputRecoveryChallenge, bool, InputRecoverySample> read) : IInputRecoveryProbe
    { public InputRecoverySample Read(InputRecoveryChallenge challenge, bool workersStopped) => read(challenge, workersStopped); }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
