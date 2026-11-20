using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101CombatBattleOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "combat-battle-" + Guid.NewGuid().ToString("N"));
    private string Locks => Path.Combine(_root, "locks");
    private readonly WizwalkerTarget _target;
    private readonly long _utc = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc).Ticks;
    private double _time = 1000;
    private uint _activity = 12;
    private readonly List<W101CombatDispatch> _sent = [];
    private readonly List<W101CombatSnapshot> _read = [];
    private readonly List<OperationContext> _contexts = [];
    private readonly List<int> _plannedRounds = [];

    public W101CombatBattleOperationTests()
    {
        Directory.CreateDirectory(_root);
        var image = Path.Combine(_root, "WizardGraphicalClient.exe");
        File.WriteAllText(image, "inert test identity");
        _target = new(123, 42, 100, "WizardGraphicalClient", image, GameExperiments.Digest(image));
    }

    private W101CombatTurnRuntime Runtime((W101CombatPhase Phase, int Round)[] states,
        Func<W101CombatSnapshot, int, W101CombatSnapshot>? alter = null,
        Func<W101CombatDispatch, RoutineReceipt>? dispatch = null)
    {
        return new(_ => true, () => _activity, (_, _) => true,
            (context, _, _, _) =>
            {
                Assert.Same(context.Input, GameInputLease.Current);
                Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competitor", Locks));
                if (_contexts.Count > 0) Assert.Same(_contexts[0].Input, context.Input);
                _contexts.Add(context);
                var index = _read.Count;
                _time += 150;
                var state = states[Math.Min(index, states.Length - 1)];
                var snapshot = new W101CombatSnapshot("snapshot-" + index, _target, "encounter-1", state.Round,
                    "1280x720.client", _utc + (long)(_time * TimeSpan.TicksPerMillisecond), state.Phase,
                    [new("pass-" + index, "pass", W101CombatControlRole.Pass, new(.5f, .8f, .1f, .1f), true),
                     new("card-" + index, "spell-round-" + state.Round, W101CombatControlRole.Card, new(.1f, .7f, .1f, .2f), true)],
                    CleanupConfirmed: true);
                if (alter is not null) snapshot = alter(snapshot, index);
                _read.Add(snapshot);
                return Task.FromResult(snapshot);
            },
            (snapshot, _) =>
            {
                _plannedRounds.Add(snapshot.Round!.Value);
                return Task.FromResult<W101CombatTurnPlan?>(new(snapshot.Id,
                    snapshot.Round == 1 ? W101CombatPlanKind.Pass : W101CombatPlanKind.Card,
                    snapshot.Round == 1 ? "pass" : "spell-round-" + snapshot.Round));
            },
            (action, context, gate, _) =>
            {
                Assert.Same(context.Input, GameInputLease.Current);
                Assert.True(gate());
                _sent.Add(action); _time += 40;
                return Task.FromResult(dispatch?.Invoke(action) ?? new(action.ProposalId, action.ActionId, _time, true, true));
            }, () => _time, () => _utc + (long)(_time * TimeSpan.TicksPerMillisecond),
            (delay, token) => { token.ThrowIfCancellationRequested(); _time += delay; return Task.CompletedTask; });
    }

    private static (W101CombatPhase, int)[] TwoTurns =>
        [(W101CombatPhase.Planning, 1), (W101CombatPhase.Planning, 1), (W101CombatPhase.Planning, 2),
         (W101CombatPhase.Planning, 2), (W101CombatPhase.Planning, 2), (W101CombatPhase.Victory, 2)];

    private async Task<OperationReceipt> Run(W101CombatTurnRuntime runtime, W101CombatBattleLimits? limits = null,
        Func<W101CombatSnapshot, bool>? scope = null, CancellationToken cancellation = default,
        IReadOnlyDictionary<string, string>? pins = null)
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var prepared = W101CombatBattleOperation.Prepare(_target, true, runtime,
            limits ?? new(MaximumReads: 18, TurnLimits: new(MaximumReads: 9)), pins, scope);
        return await scheduler.Submit(prepared.Request, async (context, token) =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellation);
            return await prepared.Execute(context, linked.Token);
        }).Completion;
    }

    private static JsonElement Report(OperationReceipt receipt)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Assert.Single(receipt.Artifacts)));
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task TwoFreshTurnsUseOneLeaseDistinctEvidenceAndAnExplicitVictory()
    {
        var receipt = await Run(Runtime(TwoTurns));
        Assert.Equal(OperationState.Completed, receipt.State);
        Assert.Equal(new[] { 1, 1, 2, 2 }, _plannedRounds);
        Assert.Equal(new[] { W101CombatTurnOperation.Pass, W101CombatTurnOperation.SelectCard }, _sent.Select(a => a.ActionId));
        Assert.NotEqual(_sent[0].ProposalId, _sent[1].ProposalId);
        Assert.Equal("spell-round-2", _sent[1].Control.SubjectId);
        Assert.Equal(2, _contexts.Select(c => c.Request.Id).Distinct().Count());
        Assert.All(_contexts, context => Assert.NotEqual(receipt.Request.Id, context.Request.Id));
        Assert.All(_contexts, context => Assert.Equal(receipt.Request.Operation, context.Request.Operation));
        Assert.All(_contexts, context => Assert.Same(_contexts[0].Request.Pins, context.Request.Pins));
        Assert.Equal(new[] { "turn-001", "turn-002" }, _contexts.Select(c => Path.GetFileName(c.Directory)).Distinct());
        Assert.Single(Directory.GetDirectories(Path.Combine(_root, "operations")));
        var report = Report(receipt);
        Assert.True(report.GetProperty("battleVerified").GetBoolean());
        Assert.True(report.GetProperty("victoryObserved").GetBoolean());
        Assert.False(report.GetProperty("questProgressVerified").GetBoolean());
        Assert.Equal(6, report.GetProperty("reads").GetInt32());
        Assert.Equal(2, report.GetProperty("turns").GetArrayLength());
        foreach (var turn in report.GetProperty("turns").EnumerateArray())
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(receipt.Artifacts[0])!, turn.GetProperty("Report").GetString()!)));
        using var next = GameInputLease.Acquire("next", Locks);
        Assert.True(next.CanDispatch);
    }

    [Fact]
    public async Task VerifiedNextRoundIsNotBattleCompletion()
    {
        var receipt = await Run(Runtime(TwoTurns), new(MaximumTurns: 1));
        Assert.Equal(OperationState.Unknown, receipt.State);
        Assert.Equal("combat-battle-turn-budget-exceeded", receipt.Message);
        Assert.Single(_sent);
        Assert.False(Report(receipt).GetProperty("battleVerified").GetBoolean());
    }

    [Fact]
    public async Task TotalReadBudgetIncludesEveryTurnAndDoesNotRestart()
    {
        var receipt = await Run(Runtime(TwoTurns), new(MaximumReads: 4));
        Assert.Equal(OperationState.Unknown, receipt.State);
        Assert.Equal("combat-battle-read-budget-exceeded", receipt.Message);
        Assert.Equal(3, _read.Count);
        Assert.Single(_sent);
        Assert.Equal(1, Report(receipt).GetProperty("turns").GetArrayLength());
    }

    [Theory]
    [InlineData("encounter")]
    [InlineData("geometry")]
    [InlineData("round")]
    [InlineData("replayed")]
    [InlineData("timestamp")]
    public async Task CrossTurnContextChangesCannotDispatchAnotherAction(string fault)
    {
        var receipt = await Run(Runtime(TwoTurns, alter: (snapshot, index) => index == 3 ? fault switch
        {
            "encounter" => snapshot with { EncounterId = "encounter-2" },
            "geometry" => snapshot with { GeometryId = "2560x1440.client" },
            "round" => snapshot with { Round = 3 },
            "replayed" => snapshot with { Id = "snapshot-2" },
            _ => snapshot with { ObservedUtcTicks = _read[2].ObservedUtcTicks }
        } : snapshot));
        Assert.Equal(OperationState.Unknown, receipt.State);
        Assert.Single(_sent);
        Assert.Equal(4, _read.Count);
        Assert.Contains(fault switch
        {
            "encounter" => "encounter-changed",
            "geometry" => "geometry-changed",
            "round" => "next-round-changed",
            "replayed" => "observation-replayed",
            _ => "observation-not-newer"
        }, receipt.Message);
        Assert.True(Report(receipt).GetProperty("cleanupConfirmed").GetBoolean());
    }

    [Fact]
    public async Task UserTakeoverBetweenTurnsCannotBecomeANewActivityBaseline()
    {
        var receipt = await Run(Runtime(TwoTurns), scope: snapshot =>
        {
            if (snapshot.Id == "snapshot-2") _activity++;
            return true;
        });
        Assert.Equal(OperationState.Unknown, receipt.State);
        Assert.Single(_sent);
        Assert.Equal(3, _read.Count);
        Assert.Contains("activity", receipt.Message);
    }

    [Fact]
    public async Task UnknownAfterInputStopsInsteadOfRetryingTheTurn()
    {
        var receipt = await Run(Runtime([(W101CombatPhase.Planning, 1), (W101CombatPhase.Planning, 1),
            (W101CombatPhase.Unknown, 1), (W101CombatPhase.Victory, 1)]));
        Assert.Equal(OperationState.Unknown, receipt.State);
        Assert.Equal("combat-battle-state-unknown", receipt.Message);
        Assert.Equal(3, _read.Count);
        Assert.Single(_sent);
        Assert.True(Report(receipt).GetProperty("cleanupConfirmed").GetBoolean());
    }

    [Fact]
    public async Task DefeatIsARecordedFailureAndNeverRestartsTheEncounter()
    {
        var receipt = await Run(Runtime([(W101CombatPhase.Planning, 1), (W101CombatPhase.Planning, 1), (W101CombatPhase.Defeat, 1)]));
        Assert.Equal(OperationState.Failed, receipt.State);
        Assert.Equal("combat-battle-defeat-observed", receipt.Message);
        Assert.Single(_sent);
        Assert.False(Report(receipt).GetProperty("victoryObserved").GetBoolean());
    }

    [Fact]
    public async Task PreexistingVictoryDoesNotCountAsThisBattlesOutcome()
    {
        var receipt = await Run(Runtime([(W101CombatPhase.Victory, 1)]), new(MaximumReads: 3));
        Assert.Equal(OperationState.Failed, receipt.State);
        Assert.Empty(_sent);
        Assert.False(Report(receipt).GetProperty("battleVerified").GetBoolean());
    }

    [Fact]
    public async Task LoadingPausesButDisappearingControlsDoNotProveVictory()
    {
        var receipt = await Run(Runtime([(W101CombatPhase.Loading, 1), (W101CombatPhase.Planning, 1),
            (W101CombatPhase.Planning, 1), (W101CombatPhase.Resolving, 1)],
            alter: (snapshot, index) => index >= 3 ? snapshot with { Controls = [] } : snapshot), new(MaximumReads: 6));
        Assert.Equal(OperationState.Unknown, receipt.State);
        Assert.Equal(6, _read.Count);
        Assert.Single(_sent);
        Assert.False(Report(receipt).GetProperty("victoryObserved").GetBoolean());
    }

    [Fact]
    public async Task MissingInputReleaseBlocksTheWholeBattleAndNextOwner()
    {
        var receipt = await Run(Runtime(TwoTurns, dispatch: action => new(action.ProposalId, action.ActionId, _time, true, false)));
        Assert.Equal(OperationState.CleanupFailed, receipt.State);
        Assert.Single(_sent);
        Assert.Equal(2, _read.Count);
        Assert.False(Report(receipt).GetProperty("cleanupConfirmed").GetBoolean());
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", Locks));
    }

    [Fact]
    public async Task UnconfirmedObserverDisconnectBlocksAllFurtherTurns()
    {
        var receipt = await Run(Runtime(TwoTurns, alter: (snapshot, index) => index == 3 ? snapshot with { CleanupConfirmed = false } : snapshot));
        Assert.Equal(OperationState.CleanupFailed, receipt.State);
        Assert.Single(_sent);
        Assert.Equal(4, _read.Count);
        Assert.False(Report(receipt).GetProperty("cleanupConfirmed").GetBoolean());
    }

    [Fact]
    public async Task CancellationAfterInputReturnsWithoutAnotherAction()
    {
        using var stop = new CancellationTokenSource();
        var receipt = await Run(Runtime(TwoTurns, dispatch: action =>
        {
            stop.Cancel();
            return new(action.ProposalId, action.ActionId, _time, true, true);
        }), cancellation: stop.Token);
        Assert.Equal(OperationState.Cancelled, receipt.State);
        Assert.Single(_sent);
        Assert.True(Report(receipt).GetProperty("cleanupConfirmed").GetBoolean());
        using var next = GameInputLease.Acquire("next", Locks);
        Assert.True(next.CanDispatch);
    }

    [Fact]
    public async Task OverallClockBudgetCannotResetAtEachTurn()
    {
        var receipt = await Run(Runtime(TwoTurns, alter: (snapshot, _) =>
        {
            _time += 80;
            return snapshot;
        }), new(TimeoutSeconds: 1));
        Assert.Equal(OperationState.TimedOut, receipt.State);
        Assert.Equal("combat-battle-deadline-exceeded", receipt.Message);
        Assert.False(Report(receipt).GetProperty("battleVerified").GetBoolean());
    }

    [Fact]
    public async Task EveryChildRetainsTheFrozenParentWorkerPins()
    {
        var worker = Path.Combine(_root, "worker.py");
        File.WriteAllText(worker, "# inert worker identity");
        var pins = new Dictionary<string, string> { [worker] = GameExperiments.Digest(worker) };
        await using var scheduler = new OperationScheduler(_root, Locks);
        var prepared = W101CombatBattleOperation.Prepare(_target, true, Runtime(TwoTurns), sourcePins: pins);
        pins.Clear();
        var receipt = await scheduler.Submit(prepared.Request, prepared.Execute).Completion;
        Assert.Equal(OperationState.Completed, receipt.State);
        Assert.All(_contexts, context =>
        {
            Assert.Equal(2, context.Request.Pins.Count);
            Assert.Equal(GameExperiments.Digest(worker), context.Request.Pins[worker]);
            Assert.Equal(_target.ImageSha256, context.Request.Pins[_target.ImagePath]);
            Assert.False(context.Request.DelegatedInput);
            Assert.True(context.Request.WritesGame);
            Assert.Equal(prepared.Request.Target, context.Request.Target);
        });
    }

    [Theory]
    [InlineData(0, 180, 256)]
    [InlineData(13, 180, 256)]
    [InlineData(6, 0, 256)]
    [InlineData(6, 601, 256)]
    [InlineData(6, 180, 1)]
    [InlineData(6, 180, 1025)]
    public void InvalidBattleBudgetsCannotPrepare(int turns, int seconds, int reads)
    {
        Assert.Throws<InvalidDataException>(() => W101CombatBattleOperation.Prepare(_target, true, Runtime(TwoTurns), new(turns, seconds, reads)));
        Assert.Empty(_read); Assert.Empty(_sent);
    }

    [Fact]
    public void DisabledByDefaultCannotPrepareAnyInput()
    {
        Assert.Throws<InvalidOperationException>(() => W101CombatBattleOperation.Prepare(_target, false, Runtime(TwoTurns)));
        Assert.Empty(_read); Assert.Empty(_sent);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
