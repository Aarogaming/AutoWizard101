using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class OperationSchedulerJournalTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-scheduler-journal-" + Guid.NewGuid().ToString("N"));
    private static OperationRequest Request(bool live = false) => new(Guid.NewGuid().ToString("N"), "test.journal", "v1", 10,
        live, live ? "synthetic-no-input" : "offline", new Dictionary<string, string>());
    private string Folder(OperationRequest request) => Path.Combine(_root, "operations", request.Id);
    private string Projection(OperationRequest request) => Path.Combine(Folder(request), "receipt.json");
    private static OperationReceipt Receipt(OperationRequest request, OperationState state) =>
        new(request, state, DateTime.UtcNow, "recorded state", []);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private OperationReceipt Prepare(OperationRequest request, bool completed = false)
    {
        var folder = Folder(request); Directory.CreateDirectory(folder);
        OperationJournal.Create(folder, request);
        OperationJournal.Append(folder, Receipt(request, OperationState.Queued));
        var running = OperationJournal.Append(folder, Receipt(request, OperationState.Running));
        if (completed) OperationJournal.Append(folder, Receipt(request, OperationState.Completed));
        return running;
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("stale")]
    [InlineData("malformed")]
    public async Task CommittedTerminalWinsOverInterruptedProjectionPublication(string projection)
    {
        var request = Request(); var running = Prepare(request, completed: true);
        if (projection == "stale") OperationJournal.Project(Folder(request), running);
        if (projection == "malformed") File.WriteAllText(Projection(request), "incomplete view");
        var terminalPath = OperationJournal.SnapshotPath(Folder(request), 3);
        var terminalBytes = File.ReadAllBytes(terminalPath);
        var before = File.Exists(Projection(request)) ? File.ReadAllBytes(Projection(request)) : null;
        Assert.Equal(OperationState.Completed, Assert.Single(OperationScheduler.History(_root)).State);
        if (before is null) Assert.False(File.Exists(Projection(request)));
        else Assert.Equal(before, File.ReadAllBytes(Projection(request)));
        await using (var scheduler = new OperationScheduler(_root, _root))
        {
            Assert.Equal(0, scheduler.Pending);
            Assert.Throws<InvalidOperationException>(() => scheduler.Submit(request,
                (_, _) => Task.FromResult(OperationOutcome.Completed("must not rerun"))));
        }
        Assert.Equal(OperationState.Completed, JsonSerializer.Deserialize<OperationReceipt>(File.ReadAllBytes(Projection(request)))!.State);
        Assert.Equal(terminalBytes, File.ReadAllBytes(terminalPath));
        Assert.Equal(3, OperationJournal.Read(Folder(request))!.Sequence);
    }

    [Fact]
    public async Task RecoveryPreservesRunningEvidenceAndNeverAddsAnotherAttempt()
    {
        var request = Request(); var running = Prepare(request);
        OperationJournal.Project(Folder(request), running);
        var second = OperationJournal.SnapshotPath(Folder(request), 2); var bytes = File.ReadAllBytes(second);
        await using (var scheduler = new OperationScheduler(_root, _root)) Assert.Equal(0, scheduler.Pending);
        var state = OperationJournal.Read(Folder(request))!;
        Assert.Equal(OperationState.Interrupted, state.Latest!.State); Assert.Equal(3, state.Sequence);
        Assert.Equal(bytes, File.ReadAllBytes(second));
        var interrupted = File.ReadAllBytes(OperationJournal.SnapshotPath(Folder(request), 3));
        await using (var scheduler = new OperationScheduler(_root, _root)) Assert.Equal(0, scheduler.Pending);
        Assert.Equal(interrupted, File.ReadAllBytes(OperationJournal.SnapshotPath(Folder(request), 3)));
        Assert.Equal(3, OperationJournal.Read(Folder(request))!.Sequence);
    }

    [Fact]
    public async Task AdmissionOnlyCrashRecoversWithoutInventingQueuedOrRunningEvidence()
    {
        var request = Request(); Directory.CreateDirectory(Folder(request)); OperationJournal.Create(Folder(request), request);
        Assert.Equal(OperationState.Unknown, Assert.Single(OperationScheduler.History(_root)).State);
        Assert.Null(OperationJournal.Read(Folder(request))!.Latest);
        await using (var scheduler = new OperationScheduler(_root, _root)) Assert.Equal(0, scheduler.Pending);
        var state = OperationJournal.Read(Folder(request))!;
        Assert.Equal(1, state.Sequence); Assert.Equal(OperationState.Interrupted, state.Latest!.State);
    }

    [Fact]
    public async Task LegacyRecoveryStaysReceiptOnlyAndDoesNotInventEarlierJournalRecords()
    {
        var request = Request(); Directory.CreateDirectory(Folder(request));
        GameExperiments.WriteAtomic(Projection(request), Receipt(request, OperationState.Running) with { Artifacts = ["legacy-report.json"] });
        await using (var scheduler = new OperationScheduler(_root, _root)) Assert.Equal(0, scheduler.Pending);
        Assert.False(Directory.Exists(OperationJournal.DirectoryPath(Folder(request))));
        var recovered = Assert.Single(OperationScheduler.History(_root));
        Assert.Equal(OperationState.Interrupted, recovered.State); Assert.Equal(new[] { "legacy-report.json" }, recovered.Artifacts);
    }

    [Fact]
    public void AllJobsAreValidatedBeforeAnyRecoveryMutation()
    {
        var valid = Request(); Prepare(valid);
        var legacy = Request(); Directory.CreateDirectory(Folder(legacy));
        GameExperiments.WriteAtomic(Projection(legacy), Receipt(legacy, OperationState.Running));
        var oldLegacy = File.ReadAllBytes(Projection(legacy));
        var oldRunning = File.ReadAllBytes(OperationJournal.SnapshotPath(Folder(valid), 2));
        var corrupt = Request(); Prepare(corrupt);
        File.WriteAllText(OperationJournal.SnapshotPath(Folder(corrupt), 2), "malformed committed journal");
        Assert.Throws<InvalidDataException>(() => new OperationScheduler(_root, _root));
        Assert.Equal(oldLegacy, File.ReadAllBytes(Projection(legacy)));
        Assert.Equal(oldRunning, File.ReadAllBytes(OperationJournal.SnapshotPath(Folder(valid), 2)));
        Assert.False(File.Exists(OperationJournal.SnapshotPath(Folder(valid), 3)));
        using var journalLock = new FileStream(Path.Combine(_root, "operations", "scheduler.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public async Task UncommittedStagingRemainsUntouchedAndCannotBeReusedAsANewRun()
    {
        var request = Request(); var directory = OperationJournal.DirectoryPath(Folder(request)); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, ".pending-" + Guid.NewGuid().ToString("N") + ".tmp"); File.WriteAllText(path, "partial bytes");
        await using var scheduler = new OperationScheduler(_root, _root);
        Assert.Empty(OperationScheduler.History(_root)); Assert.Equal("partial bytes", File.ReadAllText(path));
        Assert.Throws<InvalidOperationException>(() => scheduler.Submit(request,
            (_, _) => Task.FromResult(OperationOutcome.Completed("must not execute"))));
    }

    [Fact]
    public void MissingAdmissionCannotDowngradeCommittedJournalToLegacyReceipt()
    {
        var request = Request(); var running = Prepare(request); OperationJournal.Project(Folder(request), running);
        File.Delete(Path.Combine(OperationJournal.DirectoryPath(Folder(request)), "admission.json"));
        var bytes = File.ReadAllBytes(Projection(request));
        Assert.Throws<InvalidDataException>(() => new OperationScheduler(_root, _root));
        Assert.Throws<InvalidDataException>(() => OperationScheduler.History(_root));
        Assert.Equal(bytes, File.ReadAllBytes(Projection(request)));
    }

    [Fact]
    public async Task QueuedPinsAreFrozenAndEditedSourcesFailBeforeRunning()
    {
        Directory.CreateDirectory(_root); var path = Path.Combine(_root, "source.json"); File.WriteAllText(path, "original");
        var pins = new Dictionary<string, string> { [path] = GameExperiments.Digest(path) };
        var request = Request() with { Pins = pins };
        await using var scheduler = new OperationScheduler(_root, _root);
        var entered = Signal(); var release = Signal();
        var first = scheduler.Submit(Request(), async (_, token) =>
        { entered.SetResult(); await release.Task.WaitAsync(token); return OperationOutcome.Completed("released"); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var invoked = false;
        var ticket = scheduler.Submit(request, (_, _) =>
        { invoked = true; return Task.FromResult(OperationOutcome.Completed("unexpected")); });
        try
        {
            File.WriteAllText(path, "edited"); pins[path] = GameExperiments.Digest(path);
            var exposed = Assert.IsAssignableFrom<IDictionary<string, string>>(ticket.Request.Pins);
            Assert.Throws<NotSupportedException>(() => exposed[path] = pins[path]);
        }
        finally { release.TrySetResult(); }
        await first.Completion;
        Assert.Equal(OperationState.Failed, (await ticket.Completion).State); Assert.False(invoked);
        var state = OperationJournal.Read(Folder(request))!;
        Assert.Equal(2, state.Sequence); Assert.Equal(OperationState.Failed, state.Latest!.State);
    }

    [Theory]
    [InlineData("undefined-state")]
    [InlineData("nonterminal-state")]
    [InlineData("message")]
    [InlineData("null-artifacts")]
    [InlineData("artifact-count")]
    [InlineData("artifact-path")]
    public async Task InvalidWorkerOutcomeBecomesBoundedFailureAndCannotPoisonRestart(string invalid)
    {
        var request = Request();
        await using (var scheduler = new OperationScheduler(_root, _root))
        {
            var result = new OperationOutcome(OperationState.Completed, "worker result", []);
            result = invalid switch
            {
                "undefined-state" => result with { State = (OperationState)999 },
                "nonterminal-state" => result with { State = OperationState.Running },
                "message" => result with { Message = new string('x', 20000) },
                "null-artifacts" => result with { Artifacts = null! },
                "artifact-count" => result with { Artifacts = Enumerable.Repeat("report.json", 65).ToArray() },
                "artifact-path" => result with { Artifacts = [new string('x', 32769)] },
                _ => throw new InvalidOperationException()
            };
            var receipt = await scheduler.Submit(request, (_, _) => Task.FromResult(result)).Completion;
            Assert.Equal(OperationState.Failed, receipt.State); Assert.Empty(receipt.Artifacts);
        }
        await using var restarted = new OperationScheduler(_root, _root);
        Assert.Equal(OperationState.Failed, Assert.Single(OperationScheduler.History(_root)).State);
    }

    [Fact]
    public async Task LongThrownDiagnosticIsBoundedWithoutLosingTerminalEvidence()
    {
        var request = Request();
        await using var scheduler = new OperationScheduler(_root, _root);
        var receipt = await scheduler.Submit(request, (_, _) => throw new InvalidOperationException(new string('x', 20000))).Completion;
        Assert.Equal(OperationState.Failed, receipt.State);
        Assert.EndsWith("[diagnostic truncated]", receipt.Message);
        Assert.True(receipt.Message.Length <= OperationJournal.MaximumMessageCharacters);
        Assert.Equal(OperationState.Failed, OperationJournal.Read(Folder(request))!.Latest!.State);
    }

    [Fact]
    public async Task InvalidCleanupOutcomePreservesRecoveryBarrierAndArtifactOrderRemainsUntouched()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var normal = await scheduler.Submit(Request(), (_, _) => Task.FromResult(
            OperationOutcome.Completed("done", "report.json", "pack.json"))).Completion;
        Assert.Equal(new[] { "report.json", "pack.json" }, normal.Artifacts);
        var request = Request(live: true);
        var cleanup = await scheduler.Submit(request, (_, _) => Task.FromResult(
            new OperationOutcome(OperationState.CleanupFailed, new string('x', 20000), []))).Completion;
        Assert.Equal(OperationState.CleanupFailed, cleanup.State);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("blocked-fake-owner", _root));
        Assert.Equal(OperationState.CleanupFailed, OperationJournal.Read(Folder(request))!.Latest!.State);
    }

    [Fact]
    public async Task SameConfigurationRemainsTwoExplicitEpisodesWithDistinctAdmissions()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var first = Request(); var second = first with { Id = Guid.NewGuid().ToString("N") };
        foreach (var request in new[] { first, second })
            Assert.Equal(OperationState.Completed, (await scheduler.Submit(request, (_, _) => Task.FromResult(OperationOutcome.Completed("offline episode"))).Completion).State);
        Assert.NotEqual(OperationJournal.Read(Folder(first))!.Request.Id, OperationJournal.Read(Folder(second))!.Request.Id);
        Assert.Equal(2, OperationScheduler.History(_root).Length);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
