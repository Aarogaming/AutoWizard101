using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

[Collection("Windows OCR")]
public sealed class ModuleOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-module-operation-" + Guid.NewGuid().ToString("N"));
    public ModuleOperationTests() => Directory.CreateDirectory(_root);
    private string Artifact(OperationTicket ticket, string file) => Path.Combine(_root, "operations", ticket.Request.Id, file);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static OperationRequest BlockingRequest() => new(Guid.NewGuid().ToString("N"), "test.module-blocker", "1", 10, false,
        "offline", new Dictionary<string, string>());
    private static OperationModuleBundle Bundle() => ModuleRunnerTests.LabBundle();
    private static RoutineBinding Bind(OperationModuleBundle bundle) => ModuleRunnerTests.Binding(bundle);

    private (ModuleScreenReviewSnapshot Snapshot, Dictionary<string, string> Sources) PrepareOffline()
    {
        var directory = Path.Combine(_root, "source"); Directory.CreateDirectory(directory);
        var sources = new Dictionary<string, string>
        {
            ["bundle"] = Path.Combine(directory, "bundle.json"),
            ["manifest"] = Path.Combine(directory, "session.json"),
            ["events"] = Path.Combine(directory, "events.jsonl"),
            ["image"] = Path.Combine(directory, "native-idle.png")
        };
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "Modules", "W101Menus", "native-idle.png"), sources["image"]);
        var bundle = W101ModulePrograms.NativeIdle();
        ModuleCatalog.Save(bundle, sources["bundle"]);
        var binding = bundle.Bind("recorded-w101", "1282x760", "attempt1");
        File.WriteAllText(sources["manifest"], JsonSerializer.Serialize(new
        {
            schema_version = 1,
            session_id = "offline-scheduler",
            clock_origin = "synthetic-test-milliseconds",
            evidence_mode = "saved-images-synthetic-rehearsal",
            bundle_file = "bundle.json",
            bundle_sha256 = GameExperiments.Digest(sources["bundle"]),
            events_file = "events.jsonl",
            routine_binding = binding,
            geometry = new { width = 1282, height = 760 }
        }, OperationModules.Json));
        var compact = new JsonSerializerOptions(OperationModules.Json) { WriteIndented = false };
        File.WriteAllLines(sources["events"], new[] { 0, 500 }.Select((time, index) => JsonSerializer.Serialize(new
        {
            event_id = "frame" + index,
            sequence = index,
            time_ms = time + 20,
            kind = "observation",
            payload = new { module_image = new { file = "native-idle.png", sha256 = GameExperiments.Digest(sources["image"]), captured_ms = time } }
        }, compact)));
        return (ModuleScreenReviewSnapshot.Load(sources["manifest"]), sources);
    }

    [Fact]
    public async Task OfflineReviewUsesSchedulerWithoutAcquiringInputEvenWhenAnotherOwnerExists()
    {
        var source = PrepareOffline();
        await using var scheduler = new OperationScheduler(_root, _root);
        using var otherOwner = GameInputLease.Acquire("other-offline-test-owner", _root);
        var ticket = ModuleOperation.SubmitOffline(scheduler, source.Snapshot);
        Assert.False(ticket.Request.WritesGame); Assert.False(ticket.Request.DelegatedInput);
        var receipt = await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(receipt.State == OperationState.Completed, $"{receipt.State}: {receipt.Message} ({WindowsOcrWarmup.Summary})");
        Assert.Equal(2, receipt.Artifacts.Length);
        Assert.True(otherOwner.IsHeld); Assert.Null(GameInputLease.Current);
        using var admission = JsonDocument.Parse(File.ReadAllText(Artifact(ticket, "module-operation.json")));
        Assert.Equal("offline-screen-review", admission.RootElement.GetProperty("mode").GetString());
        Assert.Equal(0, admission.RootElement.GetProperty("desktop_dispatch_count").GetInt32());
        Assert.False(admission.RootElement.GetProperty("live_qualified").GetBoolean());
        Assert.False(admission.RootElement.GetProperty("input_lease_acquired").GetBoolean());
        Assert.True(File.Exists(Artifact(ticket, "module-screen-review.json")));
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("bundle")]
    [InlineData("events")]
    [InlineData("image")]
    public async Task ChangedQueuedSourceFailsBeforeTheReviewerStarts(string changed)
    {
        var source = PrepareOffline();
        await using var scheduler = new OperationScheduler(_root, _root);
        var entered = Signal(); var release = Signal();
        var first = scheduler.Submit(BlockingRequest(), async (_, token) =>
        { entered.SetResult(); await release.Task.WaitAsync(token); return OperationOutcome.Completed("released"); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var ticket = ModuleOperation.SubmitOffline(scheduler, source.Snapshot);
        try { File.AppendAllText(source.Sources[changed], "changed after submission"); }
        finally { release.TrySetResult(); }
        await first.Completion;
        Assert.Equal(OperationState.Failed, (await ticket.Completion).State);
        Assert.False(File.Exists(Artifact(ticket, "module-operation.json")));
        Assert.False(File.Exists(Artifact(ticket, "module-screen-review.json")));
    }

    [Fact]
    public async Task QueuedOfflineCancellationDoesNotStartReviewOrCreateItsArtifacts()
    {
        var source = PrepareOffline();
        await using var scheduler = new OperationScheduler(_root, _root);
        var entered = Signal(); var release = Signal();
        var first = scheduler.Submit(BlockingRequest(), async (_, token) =>
        { entered.SetResult(); await release.Task.WaitAsync(token); return OperationOutcome.Completed("released"); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var ticket = ModuleOperation.SubmitOffline(scheduler, source.Snapshot);
        try
        {
            ticket.Cancel();
            Assert.Equal(OperationState.Cancelled, (await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(3))).State);
            Assert.False(File.Exists(Artifact(ticket, "module-operation.json")));
        }
        finally { release.TrySetResult(); }
        await first.Completion;
    }

    [Fact]
    public async Task TrustedOwnedWorkReceivesTheExactSchedulerLeaseAndNoSecondOwner()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var bundle = Bundle(); var called = false;
        var ticket = ModuleOperation.SubmitOwned(scheduler, bundle, Bind(bundle), (context, _) =>
        {
            called = true;
            Assert.NotNull(context.Input); Assert.True(context.Input.IsHeld);
            Assert.Same(context.Input, GameInputLease.Current);
            Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competing-test-owner", _root));
            return Task.FromResult(new OperationOutcome(OperationState.Unknown, "Fake adapter sent no input; no game outcome observed.", []));
        });
        Assert.True(ticket.Request.WritesGame); Assert.False(ticket.Request.DelegatedInput);
        var receipt = await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(called); Assert.Equal(OperationState.Unknown, receipt.State);
        Assert.Equal(2, receipt.Artifacts.Length); Assert.Null(GameInputLease.Current);
        using var next = GameInputLease.Acquire("later-test-owner", _root); Assert.True(next.IsHeld);
        using var admission = JsonDocument.Parse(File.ReadAllText(Artifact(ticket, "module-operation.json")));
        Assert.Equal("trusted-owned-module-operation", admission.RootElement.GetProperty("mode").GetString());
        Assert.False(admission.RootElement.GetProperty("live_qualified").GetBoolean());
    }

    [Fact]
    public async Task CompetingOwnerPreventsOwnedCallbackFromRunning()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var bundle = Bundle(); var called = false;
        using var otherOwner = GameInputLease.Acquire("other-test-owner", _root);
        var ticket = ModuleOperation.SubmitOwned(scheduler, bundle, Bind(bundle), (_, _) =>
        { called = true; return Task.FromResult(OperationOutcome.Completed("must not execute")); });
        Assert.Equal(OperationState.Failed, (await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(3))).State);
        Assert.False(called); Assert.False(File.Exists(Artifact(ticket, "module-operation.json")));
    }

    [Fact]
    public async Task OwnedCancellationRetainsLeaseUntilCallbackCleanupExitsAndKeepsAdmissionEvidence()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var bundle = Bundle(); var entered = Signal(); var cleaning = Signal(); var release = Signal();
        var ticket = ModuleOperation.SubmitOwned(scheduler, bundle, Bind(bundle), async (context, token) =>
        {
            entered.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally
            {
                cleaning.SetResult(); await release.Task;
                Assert.Same(context.Input, GameInputLease.Current); Assert.True(context.Input!.IsHeld);
            }
            return OperationOutcome.Completed("unreachable");
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); ticket.Cancel();
        try
        {
            await cleaning.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(ticket.Completion.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("too-early-test-owner", _root));
        }
        finally { release.TrySetResult(); }
        var receipt = await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(OperationState.Cancelled, receipt.State); Assert.Equal(2, receipt.Artifacts.Length);
        using var later = GameInputLease.Acquire("later-test-owner", _root); Assert.True(later.IsHeld);
    }

    [Fact]
    public async Task FailedCleanupRetainsSharedRecoveryBoundaryAndDoesNotClaimCompletion()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var bundle = Bundle();
        var ticket = ModuleOperation.SubmitOwned(scheduler, bundle, Bind(bundle), (_, _) =>
            Task.FromResult(new OperationOutcome(OperationState.CleanupFailed, "Synthetic unconfirmed release; no OS input sent.", [])));
        var receipt = await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(OperationState.CleanupFailed, receipt.State); Assert.Equal(2, receipt.Artifacts.Length);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("blocked-test-owner", _root));
    }

    [Fact]
    public async Task OwnedAdmissionRejectsInvalidBindingAndFreezesItsJournalBeforeQueueing()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var bundle = Bundle(); var binding = Bind(bundle);
        Assert.Throws<InvalidDataException>(() => ModuleOperation.SubmitOwned(scheduler, bundle,
            binding with { ContextId = "unrelated.visit.test" }, (_, _) => Task.FromResult(OperationOutcome.Completed("bad"))));
        Assert.Equal(0, scheduler.Pending);
        var entered = Signal(); var release = Signal();
        var first = scheduler.Submit(BlockingRequest(), async (_, token) =>
        { entered.SetResult(); await release.Task.WaitAsync(token); return OperationOutcome.Completed("released"); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var ticket = ModuleOperation.SubmitOwned(scheduler, bundle, binding, (_, _) =>
            Task.FromResult(new OperationOutcome(OperationState.Unknown, "No input or game outcome.", [])));
        try { bundle.Modules[0].RequiredFacts[0] = "changed-after-submission"; }
        finally { release.TrySetResult(); }
        await first.Completion; await ticket.Completion;
        var admitted = ModuleCatalog.ReadBundle(File.ReadAllText(Artifact(ticket, "admitted-bundle.json")));
        Assert.Equal("lab.context", admitted.Modules[0].RequiredFacts[0]);
    }

    [Fact]
    public async Task SchedulerDeadlineRemainsDistinctFromUserCancellation()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var bundle = Bundle();
        var ticket = ModuleOperation.SubmitOwned(scheduler, bundle, Bind(bundle), async (_, token) =>
        { await Task.Delay(Timeout.Infinite, token); return OperationOutcome.Completed("unreachable"); }, timeoutSeconds: 1);
        var receipt = await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(4));
        Assert.Equal(OperationState.TimedOut, receipt.State); Assert.Equal(2, receipt.Artifacts.Length);
        using var later = GameInputLease.Acquire("later-test-owner", _root); Assert.True(later.IsHeld);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1801)]
    public async Task InvalidOwnedBudgetsFailBeforeAdmission(int seconds)
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var bundle = Bundle();
        Assert.Throws<InvalidDataException>(() => ModuleOperation.SubmitOwned(scheduler, bundle, Bind(bundle),
            (_, _) => Task.FromResult(OperationOutcome.Completed("bad")), seconds));
        Assert.Equal(0, scheduler.Pending);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
