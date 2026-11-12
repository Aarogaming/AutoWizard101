using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class JobDeckRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-deck-recovery-test-" + Guid.NewGuid().ToString("N"));
    private readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
    private int _executions;
    private int _observations;
    private string StatePath => Path.Combine(_root, "job-decks", "active.json");

    public JobDeckRecoveryTests() => Directory.CreateDirectory(_root);

    private static string Id() => Guid.NewGuid().ToString("N");
    private static JobCardSpec Card() => new(Id(), "test.recovery", "Synthetic recovery test", true, CooldownSeconds: 1);
    private static JobDeckDocument Document(JobCardSpec card) => new("Recovery test deck", "synthetic", [card]);
    private OperationScheduler Scheduler() => new(_root, Path.Combine(_root, "locks"));

    private JobDeckService Service(OperationScheduler scheduler, JobDeckDocument document,
        Func<OperationContext, CancellationToken, Task<OperationOutcome>>? execute = null)
    {
        var routine = new JobRoutine(
            new("test.recovery", "Synthetic recovery test", "Testing", "replay", "*", "Synthetic offline test", true, false, [], "No game access."),
            _ => { },
            (_, _) => new(new(Id(), "deck.test.recovery", "1", 5, false, "synthetic", new Dictionary<string, string>()), async (context, token) =>
            {
                Interlocked.Increment(ref _executions);
                return execute is null ? OperationOutcome.Completed("Synthetic operation completed.") : await execute(context, token);
            }));
        return new(_root, scheduler, [routine], document,
            () => { Interlocked.Increment(ref _observations); return null; }, () => _now);
    }

    private void SaveFixture(JobDeckState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        GameExperiments.WriteAtomic(StatePath, state);
    }

    private JobDeckState ReadSaved() => JsonSerializer.Deserialize<JobDeckState>(File.ReadAllBytes(StatePath), JobDeckRules.Json)!;

    [Fact]
    public async Task ReservationIsDurableBeforeObservationAndPinsTheEditableRevision()
    {
        await using var scheduler = Scheduler();
        var card = Card();
        await using var deck = Service(scheduler, Document(card));
        var request = Id();
        var reserved = deck.ReserveStart(request.ToUpperInvariant());

        Assert.Equal(request, reserved.PendingRunRequest);
        Assert.Equal(request, Assert.Single(ReadSaved().RunRequests!));
        Assert.Equal(request, ReadSaved().PendingRunRequest);
        Assert.Equal(0, _observations);
        Assert.Equal(0, _executions);
        Assert.Empty(reserved.Progress);
        Assert.Empty(OperationScheduler.History(_root));
        Assert.Throws<InvalidOperationException>(() => deck.Replace(Document(card with { Title = "Changed" }), reserved.Revision));
        Assert.Throws<InvalidOperationException>(() => deck.Reset(reserved.Revision));
        Assert.Throws<InvalidOperationException>(() => deck.ReserveStart(Id()));
        Assert.Throws<InvalidOperationException>(() => deck.CompleteReservedStart(request, reserved.Revision + 1, false));
        Assert.Equal(request, deck.State.PendingRunRequest);
        Assert.Equal(0, _observations);

        deck.CompleteReservedStart(request, reserved.Revision, false);
        await deck.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, _executions);
        Assert.Equal(1, deck.State.Progress[card.Id].Completed);
        Assert.Null(deck.State.PendingRunRequest);
    }

    [Fact]
    public async Task CancelledReservationCannotDispatchWhenItsObservationFinishesLate()
    {
        await using var scheduler = Scheduler();
        var card = Card();
        await using var deck = Service(scheduler, Document(card));
        var cancelled = Id();
        var reserved = deck.ReserveStart(cancelled);
        deck.Stop();
        Assert.Null(ReadSaved().PendingRunRequest);
        Assert.Contains(cancelled, ReadSaved().RunRequests!);
        Assert.Throws<InvalidOperationException>(() => deck.CompleteReservedStart(cancelled, reserved.Revision, true));
        Assert.Empty(deck.State.Progress);
        Assert.Equal(0, _observations);
        Assert.Equal(0, _executions);

        var next = Id();
        var newReservation = deck.ReserveStart(next);
        Assert.Throws<InvalidOperationException>(() => deck.CompleteReservedStart(cancelled, reserved.Revision, false));
        Assert.Equal(next, deck.State.PendingRunRequest);
        deck.CompleteReservedStart(next, newReservation.Revision, false);
        await deck.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, _executions);
        Assert.Single(OperationScheduler.History(_root));
    }

    [Fact]
    public async Task RetryingAStoppedReservationIdNeverAdmitsWorkOrRewritesTheFile()
    {
        await using var scheduler = Scheduler();
        await using var deck = Service(scheduler, Document(Card()));
        var request = Id();
        deck.ReserveStart(request);
        deck.Stop();
        var stopped = File.ReadAllBytes(StatePath);

        Assert.Null(deck.ReserveStart(request.ToUpperInvariant()).PendingRunRequest);
        Assert.Null(deck.Start(request, false).PendingRunRequest);
        Assert.Null(deck.Start(request.ToUpperInvariant(), true).PendingRunRequest);
        Assert.Equal(stopped, File.ReadAllBytes(StatePath));
        Assert.Equal(request, Assert.Single(deck.State.RunRequests!));
        Assert.Equal(0, _observations);
        Assert.Equal(0, _executions);
        Assert.Empty(OperationScheduler.History(_root));
    }

    [Fact]
    public async Task ReopeningAPendingReservationMarksItInterruptedWithoutReplayingIt()
    {
        await using var scheduler = Scheduler();
        var card = Card();
        var document = Document(card);
        var request = Id();
        await using (var deck = Service(scheduler, document)) deck.ReserveStart(request);
        Assert.Equal(request, ReadSaved().PendingRunRequest);

        await using var reopened = Service(scheduler, document);
        Assert.Equal("Interrupted", reopened.State.Status);
        Assert.Equal("Interrupted", ReadSaved().Status);
        Assert.Null(ReadSaved().PendingRunRequest);
        Assert.Contains(request, ReadSaved().RunRequests!);
        Assert.Empty(reopened.State.Progress);
        Assert.Equal(0, _observations);
        Assert.Equal(0, _executions);
        Assert.Throws<InvalidOperationException>(() => reopened.CompleteReservedStart(request, reopened.State.Revision, false));
        reopened.Start(request, false);
        Assert.Empty(OperationScheduler.History(_root));

        reopened.Start(Id(), false);
        await reopened.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, _executions);
        Assert.Equal(1, reopened.State.Progress[card.Id].Completed);
    }

    [Theory]
    [InlineData("Stopped", true)]
    [InlineData("Paused", true)]
    [InlineData("OutcomeUnknown", false)]
    public async Task RecoveryHoldSurvivesStopAndReopenIndependentlyOfTheDisplayStatus(string status, bool recoveryRequired)
    {
        await using var scheduler = Scheduler();
        var card = Card();
        var document = Document(card);
        SaveFixture(new(document, 4, status,
            new() { [card.Id] = new(1, 0, _now, "Unknown", Id(), "Synthetic unconfirmed outcome.") },
            Error: "Synthetic unconfirmed outcome.", RecoveryRequired: recoveryRequired));
        await using (var deck = Service(scheduler, document))
        {
            Assert.Throws<InvalidOperationException>(() => deck.ReserveStart(Id()));
            deck.Stop();
            Assert.True(ReadSaved().RecoveryRequired);
            Assert.Equal(1, ReadSaved().Progress[card.Id].Attempts);
            Assert.Equal(0, ReadSaved().Progress[card.Id].Completed);
        }

        await using var reopened = Service(scheduler, document);
        Assert.True(reopened.State.RecoveryRequired);
        Assert.Throws<InvalidOperationException>(() => reopened.Start(Id(), false));
        Assert.Throws<InvalidOperationException>(() => reopened.Reset(reopened.State.Revision));
        Assert.Throws<InvalidOperationException>(() => reopened.Replace(document, reopened.State.Revision));
        Assert.Equal(0, _executions);
        Assert.Empty(OperationScheduler.History(_root));
    }

    [Fact]
    public async Task FullCardHistoryRejectsNewCardsWithoutGrowingTheProgressIndex()
    {
        await using var scheduler = Scheduler();
        var card = Card();
        var document = Document(card) with { AttemptLimit = 1000 };
        var history = Enumerable.Range(0, 128).ToDictionary(_ => Id(), _ => new JobCardProgress(1, 1, _now, "Completed"));
        SaveFixture(new(document, 0, "Stopped", history));
        var originalProgress = JsonSerializer.SerializeToUtf8Bytes(ReadSaved().Progress, JobDeckRules.Json);
        await using var deck = Service(scheduler, document);
        var request = Id();

        deck.Start(request, false);
        Assert.Equal("Paused", deck.State.Status);
        Assert.Contains("history limit", deck.State.Error);
        Assert.False(deck.State.Progress.ContainsKey(card.Id));
        Assert.Equal(128, deck.State.Progress.Count);
        Assert.Equal(originalProgress, JsonSerializer.SerializeToUtf8Bytes(ReadSaved().Progress, JobDeckRules.Json));
        var rejected = File.ReadAllBytes(StatePath);
        for (var retry = 0; retry < 3; retry++) deck.Start(request, false);
        Assert.Equal(rejected, File.ReadAllBytes(StatePath));

        deck.Start(Id(), false);
        Assert.Equal(originalProgress, JsonSerializer.SerializeToUtf8Bytes(ReadSaved().Progress, JobDeckRules.Json));
        Assert.InRange(new FileInfo(StatePath).Length, 1, 196608);
        Assert.Equal(0, _executions);
        Assert.Empty(OperationScheduler.History(_root));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TerminalPersistenceFailureDrainsAndItsHoldSurvivesStorageRecovery(bool failReceiptProjection)
    {
        await using var scheduler = Scheduler();
        var entered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var card = Card();
        var document = Document(card);
        var deck = Service(scheduler, document, async (context, token) =>
        {
            entered.SetResult(context.Directory);
            await release.Task.WaitAsync(token);
            return OperationOutcome.Completed("Synthetic terminal outcome.");
        });
        try
        {
            deck.Start(Id(), false);
            var jobDirectory = await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var blockedPath = failReceiptProjection ? Path.Combine(jobDirectory, "receipt.json") : StatePath;
            // Only a temporary test file is locked. No real operation or game state is touched.
            using (var heldFile = new FileStream(blockedPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                release.TrySetResult();
                await deck.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
                Assert.False(deck.IsBusy);
                Assert.Null(deck.State.ActiveCard);
                Assert.Null(deck.State.ActiveOperation);
                Assert.True(deck.State.RecoveryRequired);
                Assert.Equal(failReceiptProjection ? "OutcomeUnknown" : "StorageError", deck.State.Status);
                Assert.Equal(failReceiptProjection ? 0 : 1, deck.State.Progress[card.Id].Completed);
            }

            deck.Stop();
            Assert.True(ReadSaved().RecoveryRequired);
            Assert.Throws<InvalidOperationException>(() => deck.Start(Id(), false));
            Assert.Equal(OperationState.Completed, Assert.Single(OperationScheduler.History(_root)).State);
        }
        finally
        {
            release.TrySetResult();
            await deck.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        }

        await using var reopened = Service(scheduler, document);
        Assert.True(reopened.State.RecoveryRequired);
        Assert.False(reopened.IsBusy);
        Assert.Equal(failReceiptProjection ? 0 : 1, reopened.State.Progress[card.Id].Completed);
        Assert.Throws<InvalidOperationException>(() => reopened.Start(Id(), false));
        Assert.Equal(1, _executions);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
