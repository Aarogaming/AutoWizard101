using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class JobDeckTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-deck-test-" + Guid.NewGuid().ToString("N"));
    private DateTime _now = DateTime.UtcNow;
    private JobFacts? _facts;
    private int _executions;
    public JobDeckTests() => Directory.CreateDirectory(_root);
    private OperationScheduler Scheduler() => new(_root, Path.Combine(_root, "locks"));
    private JobRoutine Routine(bool available = true, Func<OperationContext, CancellationToken, Task<OperationOutcome>>? run = null) => new(
        new("test", "Test", "Testing", "replay", "*", available ? "Offline" : "Not qualified", available, false, [], "Synthetic test routine"), _ => { },
        (_, _) => new(new(Guid.NewGuid().ToString("N"), "deck.test", "1", 5, false, "synthetic", new Dictionary<string, string>()), async (context, token) =>
        {
            Interlocked.Increment(ref _executions);
            if (run != null) return await run(context, token);
            var report = Path.Combine(context.Directory, "test.json"); GameExperiments.WriteAtomic(report, new { synthetic = true, completed = true });
            return OperationOutcome.Completed("Synthetic test completed.", report);
        }));
    private static JobCardSpec Card(bool inDeck = true) => new(Guid.NewGuid().ToString("N"), "test", "Test", inDeck, CooldownSeconds: 1);
    private static JobDeckDocument Document(params JobCardSpec[] cards) => new("Test deck", "test-game", cards);
    private JobDeckService Service(OperationScheduler scheduler, JobDeckDocument deck, JobRoutine? routine = null) =>
        new(_root, scheduler, [routine ?? Routine()], deck, () => _facts, () => _now);
    private void Facts(int energy = 10) => _facts = new("test-game", "test-target", _now,
        new() { ["energy"] = JsonSerializer.SerializeToElement(energy), ["area"] = JsonSerializer.SerializeToElement("Commons") }, "synthetic");
    private static string Id() => Guid.NewGuid().ToString("N");
    private static async Task Wait(JobDeckService deck)
    {
        using var timeout = new CancellationTokenSource(5000);
        while (deck.State.ActiveCard != null) await Task.Delay(10, timeout.Token);
    }
    [Fact]
    public async Task TrustedCustomFactsAreFrozenAndWorkForAnotherGame()
    {
        await using var scheduler = Scheduler();
        var definitions = new[] { new JobFactDefinition("dodge.threat", "number", "count", "synthetic", true, "Observed threats.") };
        var card = Card() with { Conditions = [new("dodge.threat", "eq", JsonSerializer.SerializeToElement(0))] };
        await using var deck = Service(scheduler, Document(card), Routine() with { Facts = definitions });
        definitions[0] = definitions[0] with { Kind = "string" };
        Assert.Equal("number", deck.FactDefinitions["test"].Single(f => f.Id == "dodge.threat").Kind);
        Assert.Contains(deck.FactDefinitions["test"], f => f.Id == "energy");
        _facts = new("test-game", "target", _now, new() { ["dodge.threat"] = JsonSerializer.SerializeToElement(0) }, "synthetic");
        deck.Start(Id(), false); await Wait(deck);
        Assert.Equal(1, _executions);
    }
    [Fact]
    public async Task CollectionNeverDispatchesUntilDealt()
    {
        await using var scheduler = Scheduler(); var card = Card(false);
        await using var deck = Service(scheduler, Document(card));
        deck.Start(Id(), false); Assert.Equal(0, _executions); Assert.Empty(deck.State.Progress);
        deck.Replace(Document(card with { InDeck = true }), deck.State.Revision);
        deck.Start(Id(), false); await Wait(deck);
        Assert.Equal(1, _executions); Assert.Equal(1, deck.State.Progress[card.Id].Completed);
        Assert.Single(OperationScheduler.History(_root));
    }
    [Fact]
    public async Task UnknownAndInsufficientFactsDeferWithoutSpendingCopies()
    {
        await using var scheduler = Scheduler(); var card = Card() with { Conditions = [new("energy", "gte", JsonSerializer.SerializeToElement(10))] };
        await using var deck = Service(scheduler, Document(card));
        deck.Start(Id(), false); Assert.Empty(deck.State.Progress);
        Facts(5); deck.Start(Id(), false); Assert.Empty(deck.State.Progress);
        Facts(10); deck.Start(Id(), false); await Wait(deck);
        Assert.Equal(1, deck.State.Progress[card.Id].Attempts); Assert.Equal(1, deck.State.Progress[card.Id].Completed);
    }
    [Fact]
    public async Task ExclusionWinsAndUnknownScopeDoesNotPass()
    {
        await using var scheduler = Scheduler(); Facts();
        var card = Card() with { Filters = [new("area", ["Commons"], ["COMMONS"])] };
        await using var deck = Service(scheduler, Document(card));
        Assert.False(deck.Preview().Single().Ready);
        deck.Replace(Document(card with { Filters = [new("quest.type", [], ["PvP"])] }), 0);
        Assert.Contains("Unknown", deck.Preview().Single().Reason); Assert.Equal(0, _executions);
    }
    [Theory]
    [InlineData(-3)]
    [InlineData(3)]
    public async Task FutureAndStaleFactsDefer(int seconds)
    {
        await using var scheduler = Scheduler(); Facts();
        _facts = _facts! with { ObservedUtc = _now.AddSeconds(seconds) };
        var card = Card() with { Conditions = [new("energy", "gte", JsonSerializer.SerializeToElement(1))] };
        await using var deck = Service(scheduler, Document(card)); Assert.False(deck.Preview().Single().Ready);
    }
    [Fact]
    public async Task WrongGameFactsAreNotReusable()
    {
        await using var scheduler = Scheduler(); Facts(); _facts = _facts! with { GameId = "other" };
        await using var deck = Service(scheduler, Document(Card() with { Conditions = [new("energy", "gte", JsonSerializer.SerializeToElement(1))] }));
        Assert.False(deck.Preview().Single().Ready);
    }
    [Fact]
    public async Task UnsupportedCardsCannotClaimSuccessOrIdle()
    {
        await using var scheduler = Scheduler(); var card = Card();
        await using var deck = Service(scheduler, Document(card), Routine(false));
        deck.Start(Id(), false); Assert.Equal("NoPlayableCards", deck.State.Status); Assert.Empty(deck.State.Progress); Assert.Equal(0, _executions);
    }
    [Fact]
    public async Task PrioritySkipsUnplayableCardAndUsesNext()
    {
        await using var scheduler = Scheduler();
        var blocked = Card() with { Priority = 1, Conditions = [new("energy", "gte", JsonSerializer.SerializeToElement(1))] };
        var ready = Card() with { Priority = 2 };
        await using var deck = Service(scheduler, Document(blocked, ready)); deck.Start(Id(), false); await Wait(deck);
        Assert.False(deck.State.Progress.ContainsKey(blocked.Id)); Assert.Equal(1, deck.State.Progress[ready.Id].Completed);
    }
    [Fact]
    public async Task FailureSpendsAttemptNotCopyAndNeverLoopsAutomatically()
    {
        await using var scheduler = Scheduler(); var card = Card() with { AttemptLimit = 1 };
        await using var deck = Service(scheduler, Document(card), Routine(run: (_, _) => Task.FromResult(new OperationOutcome(OperationState.Failed, "Outcome not seen", []))));
        deck.Start(Id(), true); await Wait(deck);
        Assert.Equal("Paused", deck.State.Status); Assert.Equal(0, deck.State.Progress[card.Id].Completed);
        _now = _now.AddSeconds(2); deck.Start(Id(), false); Assert.Equal(1, _executions);
    }
    [Fact]
    public async Task CopiesAndRequestIdsPreventDuplicateExecutionAcrossOtherRequests()
    {
        await using var scheduler = Scheduler(); var card = Card() with { Runs = 2 };
        await using var deck = Service(scheduler, Document(card)); var first = Id();
        deck.Start(first, false); await Wait(deck); _now = _now.AddSeconds(2);
        deck.Start(Id(), false); await Wait(deck); _now = _now.AddSeconds(2);
        deck.Start(first, false); deck.Start(Id(), false);
        Assert.Equal(2, _executions); Assert.Equal(2, deck.State.Progress[card.Id].Completed);
    }
    [Fact]
    public async Task QueuedWorkRechecksFreshFactsBeforeExecuting()
    {
        await using var scheduler = Scheduler(); var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = scheduler.Submit(new(Id(), "test.blocker", "1", 5, false, "synthetic", new Dictionary<string, string>()),
            async (_, token) => { await gate.Task.WaitAsync(token); return OperationOutcome.Completed("Test released."); });
        Facts(); var card = Card() with { Conditions = [new("energy", "gte", JsonSerializer.SerializeToElement(1))] };
        await using var deck = Service(scheduler, Document(card)); deck.Start(Id(), false);
        _now = _now.AddSeconds(4); gate.SetResult(); await blocker.Completion; await Wait(deck);
        Assert.Equal(0, _executions); Assert.Equal(0, deck.State.Progress[card.Id].Completed);
    }
    [Fact]
    public async Task StopDrainsWorkerBeforeEditsAndKeepsCounters()
    {
        await using var scheduler = Scheduler(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var deck = Service(scheduler, Document(Card()), Routine(run: async (_, token) =>
        {
            entered.SetResult(); try { await Task.Delay(Timeout.Infinite, token); }
            finally { await release.Task; }
            return OperationOutcome.Completed("never");
        }));
        deck.Start(Id(), true); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); deck.Stop();
        Assert.Throws<InvalidOperationException>(() => deck.Replace(deck.State.Deck, 0));
        release.SetResult(); await Wait(deck); Assert.Equal(0, deck.State.Progress.Values.Sum(p => p.Completed));
        Assert.Equal(1, deck.State.Progress.Values.Sum(p => p.Attempts));
    }
    [Fact]
    public async Task EditingDoesNotReplenishAttemptsAndStaleRevisionFails()
    {
        await using var scheduler = Scheduler(); var card = Card(); await using var deck = Service(scheduler, Document(card));
        deck.Start(Id(), false); await Wait(deck);
        deck.Replace(Document(card with { Title = "Renamed" }), 0);
        Assert.Equal(1, deck.State.Progress[card.Id].Completed);
        Assert.Throws<InvalidOperationException>(() => deck.Replace(Document(card), 0));
        deck.Reset(1); Assert.Empty(deck.State.Progress);
    }
    [Fact]
    public async Task RemovingAndReplacingCardsDoesNotRefillTotalBudget()
    {
        await using var scheduler = Scheduler(); var first = Card();
        await using var deck = Service(scheduler, Document(first) with { AttemptLimit = 1 });
        deck.Start(Id(), false); await Wait(deck);
        deck.Replace(Document(Card()) with { AttemptLimit = 1 }, 0);
        deck.Start(Id(), false); Assert.Equal("BudgetReached", deck.State.Status); Assert.Equal(1, _executions);
    }
    [Fact]
    public async Task RequestIdCaseDoesNotRepeatAnAction()
    {
        await using var scheduler = Scheduler(); var card = Card() with { Runs = 2 }; await using var deck = Service(scheduler, Document(card));
        var id = Id(); deck.Start(id, false); await Wait(deck); _now = _now.AddSeconds(2);
        deck.Start(id.ToUpperInvariant(), false); Assert.Equal(1, _executions);
    }
    [Fact]
    public async Task QueueWaitCannotExtendAbsoluteDeadline()
    {
        await using var scheduler = Scheduler(); var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = scheduler.Submit(new(Id(), "test.blocker", "1", 5, false, "synthetic", new Dictionary<string, string>()),
            async (_, token) => { await gate.Task.WaitAsync(token); return OperationOutcome.Completed("released"); });
        await using var deck = Service(scheduler, Document(Card()) with { DurationSeconds = 1 });
        deck.Start(Id(), false); _now = _now.AddSeconds(2); gate.SetResult(); await blocker.Completion; await Wait(deck);
        Assert.Equal(0, _executions); Assert.Equal(0, deck.State.Progress.Values.Sum(p => p.Completed));
    }
    [Fact]
    public async Task LongFailureMessagesAreSummarizedWithFullReceiptPreserved()
    {
        await using var scheduler = Scheduler(); var card = Card();
        await using var deck = Service(scheduler, Document(card), Routine(run: (_, _) => Task.FromResult(new OperationOutcome(OperationState.Failed, new string('x', 8000), []))));
        deck.Start(Id(), false); await Wait(deck);
        Assert.Equal(700, deck.State.Progress[card.Id].Reason!.Length);
        Assert.Equal(8000, OperationScheduler.History(_root).Single().Message.Length);
    }
    [Fact]
    public async Task RestartPreservesBudgetsAndDoesNotResume()
    {
        await using var scheduler = Scheduler(); var card = Card(); string request = Id();
        await using (var deck = Service(scheduler, Document(card))) { deck.Start(request, false); await Wait(deck); }
        await using var reopened = Service(scheduler, Document(card));
        Assert.Equal(1, reopened.State.Progress[card.Id].Completed); reopened.Start(request, false); Assert.Equal(1, _executions);
    }
    [Fact]
    public async Task IncompleteRunRestoresAsInterruptedWithoutReplay()
    {
        await using var scheduler = Scheduler(); var card = Card(); Directory.CreateDirectory(Path.Combine(_root, "job-decks"));
        GameExperiments.WriteAtomic(Path.Combine(_root, "job-decks", "active.json"), new JobDeckState(Document(card), 0, "Running",
            new() { [card.Id] = new(1, 0, _now, "Running", Id()) }, card.Id, Id(), _now));
        await using var deck = Service(scheduler, Document(card)); Assert.Equal("Interrupted", deck.State.Status); Assert.Equal(0, _executions);
        Assert.Equal(1, deck.State.Progress[card.Id].Attempts);
    }
    [Fact]
    public async Task DeckOwnerIsExclusiveEvenInsideSameScheduler()
    {
        await using var scheduler = Scheduler(); await using var deck = Service(scheduler, Document(Card()));
        Assert.Throws<IOException>(() => Service(scheduler, Document(Card())));
    }
    [Fact]
    public async Task StorageFailureBlocksFurtherDispatch()
    {
        await using var scheduler = Scheduler(); var card = Card(); await using var deck = Service(scheduler, Document(card));
        Directory.CreateDirectory(Path.Combine(_root, "job-decks", "active.json"));
        var error = Record.Exception(() => deck.Start(Id(), false));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Equal("StorageError", deck.State.Status); Assert.Equal(0, _executions);
        Assert.Throws<InvalidOperationException>(() => deck.Start(Id(), false));
    }
    [Fact]
    public async Task SnapshotAndDocumentReturnedToCallerAreDetached()
    {
        await using var scheduler = Scheduler(); Facts(); var card = Card(); await using var deck = Service(scheduler, Document(card));
        deck.State.Deck.Cards[0] = card with { InDeck = false }; deck.Facts!.Values.Clear();
        Assert.True(deck.State.Deck.Cards[0].InDeck); Assert.NotEmpty(deck.Facts!.Values);
    }
    [Fact]
    public async Task DeckDeadlineBoundsActiveOperation()
    {
        await using var scheduler = Scheduler(); var card = Card();
        await using var deck = Service(scheduler, Document(card) with { DurationSeconds = 1 }, Routine(run: async (_, token) =>
        { await Task.Delay(Timeout.Infinite, token); return OperationOutcome.Completed("never"); }));
        deck.Start(Id(), false); await Wait(deck);
        Assert.Equal(0, deck.State.Progress[card.Id].Completed);
        Assert.Equal(OperationState.TimedOut, OperationScheduler.History(_root).Single().State);
    }
    [Fact]
    public void CatalogSeparatesAllMinigamesAndDraftsAreNotAutomaticallyDealt()
    {
        var registry = JobDeckRoutines.Create(); var initial = JobDeckRoutines.Initial(registry);
        JobDeckRules.Validate(initial, registry.ToDictionary(r => r.Info.Id));
        Assert.Equal(8, registry.Count(r => r.Info.Collection == "Minigames"));
        Assert.All(initial.Cards, c => Assert.False(c.InDeck));
        Assert.All(registry.Where(r => r.Info.Collection == "Minigames"), r => Assert.False(r.Info.Available));
    }
    [Theory]
    [InlineData("Go To Fireglobe Theatre in Firecat Alley", "navigate")]
    [InlineData("Defeat Any Fire Elf and Collect Arrows in Firecat Alley. (0 of 3)", "combat")]
    [InlineData("Collect Crystals in Dragonspyre", "collect")]
    public void GenericQuestAutoFillExplainsMissingCapabilitiesWithoutExecuting(string objective, string need)
    {
        var facts = new JobFacts("wizard101", "synthetic", _now, new()
        {
            ["quest.objective"] = JsonSerializer.SerializeToElement(objective),
            ["dialogue"] = JsonSerializer.SerializeToElement(false)
        }, "synthetic");
        var reason = JobDeckRoutines.QuestBlock(Card(), facts);
        Assert.Contains(need, reason); Assert.Contains("not yet qualified", reason); Assert.Equal(0, _executions);
    }
    [Fact]
    public void OpenConversationUsesGenericQuestAdapterWithoutNpcSpecificCard()
    {
        var facts = new JobFacts("wizard101", "synthetic", _now, new() { ["dialogue"] = JsonSerializer.SerializeToElement(true) }, "synthetic");
        Assert.Null(JobDeckRoutines.QuestBlock(Card(), facts));
    }
    [Fact]
    public void LegacySelectedConversationSettingRemainsExplicitSelection()
    {
        var card = Card() with { Settings = new() { ["selectionFile"] = "original-selection.json" } };
        Assert.Equal("selected-conversation", JobDeckRoutines.QuestMode(card));
        Assert.Null(JobDeckRoutines.QuestBlock(card, null));
    }
    [Theory]
    [InlineData(0, 1)]
    [InlineData(101, 101)]
    [InlineData(2, 1)]
    public void InvalidCardLimitsAreRejected(int runs, int attempts)
    {
        var routine = Routine(); var card = Card() with { Runs = runs, AttemptLimit = attempts };
        Assert.Throws<InvalidDataException>(() => JobDeckRules.Validate(Document(card), new Dictionary<string, JobRoutine> { ["test"] = routine }));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}


