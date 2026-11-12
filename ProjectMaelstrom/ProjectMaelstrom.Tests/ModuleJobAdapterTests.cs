using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class ModuleJobAdapterTests
{
    [Theory]
    [InlineData("bundleFile")]
    [InlineData("sessionFile")]
    [InlineData("events")]
    public async Task ChangedQueuedSourceFailsBeforeReplay(string changed)
    {
        using var fixture = new ModuleJobCardTests(); var card = fixture.Fixture("sigil");
        var root = Path.Combine(Path.GetDirectoryName(card.Settings!["bundleFile"])!, "pin-test");
        await using var scheduler = new OperationScheduler(root, Path.Combine(root, "locks"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = scheduler.Submit(new(Guid.NewGuid().ToString("N"), "test.block", "1", 10, false, "offline", new Dictionary<string, string>()),
            async (_, token) => { entered.SetResult(); await release.Task.WaitAsync(token); return OperationOutcome.Completed("done"); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var job = ModuleJobAdapter.PrepareReplay(card.Settings["bundleFile"], card.Settings["sessionFile"], "sigil");
        var ticket = scheduler.Submit(job.Request, job.Execute);
        try
        {
            var path = changed == "events" ? Path.Combine(Path.GetDirectoryName(card.Settings["sessionFile"])!, "events.jsonl") : card.Settings[changed];
            File.AppendAllText(path, "modified after admission");
        }
        finally { release.TrySetResult(); }
        await blocker.Completion;
        Assert.Equal(OperationState.Failed, (await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(3))).State);
        Assert.False(File.Exists(Path.Combine(root, "operations", ticket.Request.Id, "module-operation.json")));
        Assert.Null(GameInputLease.Current);
    }

    [Fact]
    public async Task QueuedCancellationNeverStartsReplay()
    {
        using var fixture = new ModuleJobCardTests(); var card = fixture.Fixture("sigil");
        var root = Path.Combine(Path.GetDirectoryName(card.Settings!["bundleFile"])!, "cancel-test");
        await using var scheduler = new OperationScheduler(root, Path.Combine(root, "locks"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = scheduler.Submit(new(Guid.NewGuid().ToString("N"), "test.block", "1", 10, false, "offline", new Dictionary<string, string>()),
            async (_, token) => { entered.SetResult(); await release.Task.WaitAsync(token); return OperationOutcome.Completed("done"); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var job = ModuleJobAdapter.PrepareReplay(card.Settings["bundleFile"], card.Settings["sessionFile"], "sigil");
        var ticket = scheduler.Submit(job.Request, job.Execute);
        try
        {
            ticket.Cancel();
            Assert.Equal(OperationState.Cancelled, (await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(3))).State);
            Assert.False(File.Exists(Path.Combine(root, "operations", ticket.Request.Id, "module-operation.json")));
            Assert.Null(GameInputLease.Current);
        }
        finally { release.TrySetResult(); }
        await blocker.Completion;
    }

    [Theory]
    [InlineData(false, "Unknown")]
    [InlineData(true, "Failed")]
    public async Task MissingAndRejectedEvidenceHaveDistinctOutcomes(bool rejected, string expected)
    {
        using var fixture = new ModuleJobCardTests(); var card = fixture.Fixture("sigil", rejected);
        var events = Path.Combine(Path.GetDirectoryName(card.Settings!["sessionFile"])!, "events.jsonl");
        if (rejected) File.WriteAllText(events, File.ReadAllText(events).Replace("\"accepted\":true", "\"accepted\":false"));
        var root = Path.Combine(Path.GetDirectoryName(events)!, "outcome-test");
        await using var scheduler = new OperationScheduler(root, Path.Combine(root, "locks"));
        var job = ModuleJobAdapter.PrepareReplay(card.Settings["bundleFile"], card.Settings["sessionFile"], "sigil");
        var receipt = await scheduler.Submit(job.Request, job.Execute).Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(expected, receipt.State.ToString()); Assert.Null(GameInputLease.Current);
    }

    [Fact]
    public void PreparationDoesNotAcquireInputOrInvokeTrustedCallback()
    {
        var source = ModuleRunnerTests.LabBundle();
        var spec = source.Modules.Single(m => m.Kind == "sigil");
        var bundle = source with { Modules = [spec] };
        var called = false;
        var job = ModuleJobAdapter.PrepareOwned(bundle, "sigil", ModuleRunnerTests.Binding(bundle), (_, _) =>
        {
            called = true;
            return Task.FromResult(new OperationOutcome(OperationState.Unknown, "No observed result", []));
        });
        Assert.False(called); Assert.Null(GameInputLease.Current);
        Assert.True(job.Request.WritesGame); Assert.False(job.Request.DelegatedInput);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public void TrustedPreparationRejectsUnboundedTimeout(int timeout)
    {
        var source = ModuleRunnerTests.LabBundle();
        var bundle = source with { Modules = [source.Modules.Single(m => m.Kind == "sigil")] };
        Assert.Throws<InvalidDataException>(() => ModuleJobAdapter.PrepareOwned(bundle, "sigil", ModuleRunnerTests.Binding(bundle),
            (_, _) => throw new InvalidOperationException("Must not execute"), timeout));
    }

    [Fact]
    public void KindAndGameMustMatchEvenForTrustedPreparations()
    {
        var source = ModuleRunnerTests.LabBundle();
        var bundle = source with { Modules = [source.Modules.Single(m => m.Kind == "sigil")] };
        Assert.Throws<InvalidDataException>(() => ModuleJobAdapter.ValidateKind(bundle, "menu"));
        Assert.Throws<InvalidDataException>(() => ModuleJobAdapter.ValidateKind(bundle, "sigil", "wrong-game"));
        Assert.Throws<InvalidDataException>(() => ModuleJobAdapter.ValidateKind(source, "sigil"));
    }

    [Fact]
    public void CancelledReplayNeverReadsSourcesOrWritesReport()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => ModuleReplay.Run("missing-bundle", "missing-session", "missing-report", cancellation.Token));
    }
}
