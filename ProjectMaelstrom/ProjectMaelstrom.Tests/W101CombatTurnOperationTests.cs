using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101CombatTurnOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "combat-turn-" + Guid.NewGuid().ToString("N"));
    private string Locks => Path.Combine(_root, "locks");
    private readonly WizwalkerTarget _target;
    private readonly long _utc = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc).Ticks;
    private double _time = 1000;
    private readonly List<W101CombatDispatch> _sent = [];
    private readonly List<W101CombatSnapshot> _read = [];

    public W101CombatTurnOperationTests()
    {
        Directory.CreateDirectory(_root);
        var image = Path.Combine(_root, "WizardGraphicalClient.exe");
        File.WriteAllText(image, "inert test identity");
        _target = new(123, 42, 100, "WizardGraphicalClient", image, GameExperiments.Digest(image));
    }

    private W101CombatSnapshot Snapshot(int index, W101CombatPhase phase, int round = 1) => new(
        "snapshot-" + index, _target, "encounter-1", round, "1280x720.client", _utc + (long)(_time * TimeSpan.TicksPerMillisecond), phase,
        [new("card-" + index, "spell-instance-1", W101CombatControlRole.Card, new(.1f, .7f, .1f, .2f), true),
         new("target-" + index, "combatant-1", W101CombatControlRole.Enemy, new(.6f, .4f, .1f, .1f), true, Alive: true),
         new("pass-" + index, "pass", W101CombatControlRole.Pass, new(.5f, .8f, .1f, .1f), true)],
        phase == W101CombatPhase.Targeting ? "spell-instance-1" : null, true,
        JsonSerializer.SerializeToElement(new { source = "synthetic", player = new { hp = 100 } }));

    private W101CombatTurnRuntime Runtime(W101CombatPhase[] phases, bool pass = false,
        Func<W101CombatSnapshot, int, W101CombatSnapshot>? alter = null,
        Func<W101CombatSnapshot, W101CombatTurnPlan>? plan = null,
        Func<W101CombatDispatch, RoutineReceipt>? dispatch = null)
    {
        return new(_ => true, () => 12, (_, _) => true,
            (context, _, index, _) =>
            {
                Assert.Same(context.Input, GameInputLease.Current);
                Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competing-input", Locks));
                _time += 150;
                var phase = phases[Math.Min(index, phases.Length - 1)];
                var round = index >= phases.Length - 1 && phase == W101CombatPhase.Planning ? 2 : 1;
                var read = Snapshot(index, phase, round);
                if (alter is not null) read = alter(read, index);
                _read.Add(read);
                return Task.FromResult(read);
            },
            (snapshot, _) => Task.FromResult<W101CombatTurnPlan?>(plan?.Invoke(snapshot) ?? new(snapshot.Id,
                pass ? W101CombatPlanKind.Pass : W101CombatPlanKind.Card, pass ? "pass" : "spell-instance-1", pass ? null : "combatant-1")),
            (action, context, gate, _) =>
            {
                Assert.Same(context.Input, GameInputLease.Current);
                Assert.True(gate());
                _sent.Add(action); _time += 40;
                return Task.FromResult(dispatch?.Invoke(action) ?? new(action.ProposalId, action.ActionId, _time, true, true));
            }, () => _time, () => _utc + (long)(_time * TimeSpan.TicksPerMillisecond),
            (delay, token) => { token.ThrowIfCancellationRequested(); _time += delay; return Task.CompletedTask; });
    }

    private async Task<OperationReceipt> Run(W101CombatTurnRuntime runtime, int reads = 12, CancellationToken cancellation = default, int timeoutSeconds = 60)
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var prepared = W101CombatTurnOperation.Prepare(_target, true, runtime, new(TimeoutSeconds: timeoutSeconds, MaximumReads: reads));
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
    public async Task TargetedCardUsesFreshSemanticControlsAndObservesNextRound()
    {
        var runtime = Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning, W101CombatPhase.Targeting,
            W101CombatPhase.Targeting, W101CombatPhase.Resolving, W101CombatPhase.Planning], alter: (read, index) =>
        {
            if (index == 1) read.Controls[0] = read.Controls[0] with { Bounds = new(.3f, .7f, .1f, .2f) };
            if (index == 3) Array.Reverse(read.Controls);
            return read;
        });
        var receipt = await Run(runtime);
        Assert.Equal(OperationState.Completed, receipt.State);
        Assert.Equal(2, _sent.Count);
        Assert.Equal("card-1", _sent[0].Control.Id);
        Assert.Equal(.3f, _sent[0].Control.Bounds.X);
        Assert.Equal("target-3", _sent[1].Control.Id);
        Assert.True(Report(receipt).GetProperty("turnVerified").GetBoolean());
        Assert.False(Report(receipt).GetProperty("victoryObserved").GetBoolean());
        Assert.False(Report(receipt).GetProperty("questProgressVerified").GetBoolean());
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    [Fact]
    public async Task PassDoesNotCompleteUntilRoundAdvances()
    {
        var receipt = await Run(Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning,
            W101CombatPhase.Resolving, W101CombatPhase.Planning], pass: true));
        Assert.Equal(OperationState.Completed, receipt.State);
        Assert.Equal(W101CombatTurnOperation.Pass, Assert.Single(_sent).ActionId);
        Assert.Equal(4, _read.Count);
    }

    [Fact]
    public async Task ExplicitVictoryIsSeparateFromQuestProgress()
    {
        var receipt = await Run(Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning, W101CombatPhase.Victory], pass: true));
        Assert.Equal(OperationState.Completed, receipt.State);
        Assert.True(Report(receipt).GetProperty("victoryObserved").GetBoolean());
        Assert.False(Report(receipt).GetProperty("questProgressVerified").GetBoolean());
    }

    [Fact]
    public async Task LoadingBeforeDispatchAndDuringResolutionPausesWithinReadBudget()
    {
        var receipt = await Run(Runtime([W101CombatPhase.Loading, W101CombatPhase.Planning, W101CombatPhase.Loading,
            W101CombatPhase.Planning, W101CombatPhase.Loading, W101CombatPhase.Resolving, W101CombatPhase.Planning], pass: true));
        Assert.Equal(OperationState.Completed, receipt.State);
        Assert.Equal("snapshot-3", Assert.Single(_sent).SnapshotId);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("target")]
    [InlineData("geometry")]
    [InlineData("round")]
    [InlineData("missing-card")]
    [InlineData("ambiguous")]
    public async Task ChangedOrInvalidDispatchEvidenceCannotSend(string fault)
    {
        var receipt = await Run(Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning], pass: true, alter: (read, index) =>
        {
            if (index != 1) return read;
            return fault switch
            {
                "stale" => read with { ObservedUtcTicks = read.ObservedUtcTicks - TimeSpan.FromSeconds(3).Ticks },
                "future" => read with { ObservedUtcTicks = read.ObservedUtcTicks + TimeSpan.TicksPerSecond },
                "target" => read with { Target = _target with { StartedUtcTicks = 101 } },
                "geometry" => read with { GeometryId = "changed" },
                "round" => read with { Round = 3 },
                "missing-card" => read with { Round = 1, Controls = read.Controls.Where(c => c.Role != W101CombatControlRole.Pass).ToArray() },
                _ => read with { Round = 1, Controls = [read.Controls[2], read.Controls[2] with { Id = "different" }] }
            };
        }));
        Assert.Equal(OperationState.Failed, receipt.State);
        Assert.Empty(_sent);
        Assert.Contains(fault switch
        {
            "stale" or "future" => "observation-invalid-or-stale",
            "target" => "target-changed",
            "geometry" => "geometry-changed",
            "round" => "context-is-not-newer-and-matching",
            "missing-card" => "selected-control-unavailable",
            _ => "control-ambiguous-or-invalid"
        }, receipt.Message);
    }

    [Fact]
    public async Task WrongSelectedCardNeverClicksTarget()
    {
        var receipt = await Run(Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning, W101CombatPhase.Targeting],
            alter: (read, index) => index < 2 ? read : read with { SelectedCardSubjectId = "different-card" }), reads: 5);
        Assert.Equal(OperationState.Unknown, receipt.State);
        Assert.Equal(W101CombatTurnOperation.SelectCard, Assert.Single(_sent).ActionId);
        Assert.True(Report(receipt).GetProperty("outcomeUnverified").GetBoolean());
    }

    [Fact]
    public async Task ReceiptAndHiddenHandCannotPretendToBeVictory()
    {
        var receipt = await Run(Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning, W101CombatPhase.Resolving], pass: true), reads: 5);
        Assert.Equal(OperationState.Unknown, receipt.State);
        Assert.Single(_sent);
        Assert.False(Report(receipt).GetProperty("turnVerified").GetBoolean());
        Assert.False(Report(receipt).GetProperty("victoryObserved").GetBoolean());
    }

    [Fact]
    public async Task CancellationAfterInputPreservesUnverifiedOutcome()
    {
        using var cancelled = new CancellationTokenSource();
        var runtime = Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning, W101CombatPhase.Victory], pass: true,
            alter: (read, _) => read with { Round = 1 }, dispatch: action =>
            {
                cancelled.Cancel();
                return new(action.ProposalId, action.ActionId, _time, true, true);
            });
        var receipt = await Run(runtime, cancellation: cancelled.Token);
        Assert.Equal(OperationState.Cancelled, receipt.State);
        Assert.Single(_sent);
        Assert.True(Report(receipt).GetProperty("outcomeUnverified").GetBoolean());
        Assert.False(Report(receipt).GetProperty("victoryObserved").GetBoolean());
    }

    [Fact]
    public async Task UnconfirmedReleaseKeepsExistingOwnerRecoveryBlocked()
    {
        var receipt = await Run(Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning, W101CombatPhase.Victory], pass: true,
            dispatch: action => new(action.ProposalId, action.ActionId, _time, true, false)));
        Assert.Equal(OperationState.CleanupFailed, receipt.State);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", Locks));
    }

    [Fact]
    public async Task UnconfirmedObserverCleanupBlocksBeforeAnyInput()
    {
        var receipt = await Run(Runtime([W101CombatPhase.Planning], alter: (read, _) => read with { CleanupConfirmed = false }));
        Assert.Equal(OperationState.CleanupFailed, receipt.State);
        Assert.Empty(_sent);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", Locks));
    }

    [Fact]
    public async Task TargetRoleCannotChangeBetweenPlanningAndTargetClick()
    {
        var receipt = await Run(Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning, W101CombatPhase.Targeting,
            W101CombatPhase.Targeting, W101CombatPhase.Victory], alter: (read, index) =>
        {
            if (index == 3) read.Controls[1] = read.Controls[1] with { Role = W101CombatControlRole.Self };
            return read;
        }));
        Assert.Equal(OperationState.Unknown, receipt.State);
        Assert.Single(_sent);
        Assert.Contains("control-role-or-availability-changed", receipt.Message);
    }

    [Fact]
    public async Task DesktopTakeoverAfterReadingPreventsDispatch()
    {
        var ready = true;
        var runtime = Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning, W101CombatPhase.Victory], pass: true,
            alter: (read, index) => { if (index == 1) ready = false; return read; });
        runtime = runtime with { Ready = (_, _) => ready };
        var receipt = await Run(runtime);
        Assert.Equal(OperationState.Failed, receipt.State);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task PlannerCannotMutateAdmittedControlEvidence()
    {
        var runtime = Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning, W101CombatPhase.Victory], pass: true,
            plan: read =>
            {
                read.Controls[2] = read.Controls[2] with { SubjectId = "mutated" };
                return new(read.Id, W101CombatPlanKind.Pass, "pass");
            });
        var receipt = await Run(runtime);
        Assert.Equal(OperationState.Completed, receipt.State);
        Assert.Equal("pass", Assert.Single(_sent).Control.SubjectId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreshDecisionFactsAreReplannedBeforeAnyInput(bool reject)
    {
        var calls = 0;
        var runtime = Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning, W101CombatPhase.Targeting],
            alter: (read, index) => read with { PlannerInput = JsonSerializer.SerializeToElement(new { pips = index == 0 ? 4 : 0 }) },
            plan: read =>
            {
                calls++;
                if (read.PlannerInput.GetProperty("pips").GetInt32() == 0)
                {
                    if (reject) throw new InvalidDataException("current-pips-unavailable");
                    return new(read.Id, W101CombatPlanKind.Pass, "pass");
                }
                return new(read.Id, W101CombatPlanKind.Card, "spell-instance-1", "combatant-1");
            });
        var receipt = await Run(runtime);
        Assert.Equal(OperationState.Failed, receipt.State);
        Assert.Equal(2, calls);
        Assert.Empty(_sent);
        Assert.Contains(reject ? "current-pips-unavailable" : "plan-changed-before-input", receipt.Message);
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("unknown")]
    [InlineData("disabled")]
    public async Task TargetMustStillBeEnabledAndKnownAliveBeforeSecondInput(string state)
    {
        var receipt = await Run(Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning, W101CombatPhase.Targeting,
            W101CombatPhase.Targeting, W101CombatPhase.Victory], alter: (read, index) =>
        {
            if (index == 3) read.Controls[1] = read.Controls[1] with
            {
                Enabled = state != "disabled",
                Alive = state == "unknown" ? null : state != "dead"
            };
            return read;
        }));
        Assert.Equal(OperationState.Unknown, receipt.State);
        Assert.Single(_sent);
        Assert.Contains(state == "disabled" ? "control-role-or-availability-changed" : "target-not-confirmed-alive", receipt.Message);
    }

    [Fact]
    public async Task FreshEvidenceCannotExpireDuringReplanningAndStillDispatch()
    {
        var calls = 0;
        var runtime = Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning, W101CombatPhase.Victory], pass: true,
            plan: read =>
            {
                if (++calls == 2) _time += 2000;
                return new(read.Id, W101CombatPlanKind.Pass, "pass");
            });
        var receipt = await Run(runtime);
        Assert.Equal(OperationState.Failed, receipt.State);
        Assert.Empty(_sent);
        Assert.Contains("dispatch-evidence-expired", receipt.Message);
    }

    [Fact]
    public async Task TurnDeadlineCancelsCallbackButRetainsOwnerUntilCallbackReturns()
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning, W101CombatPhase.Victory], pass: true);
        runtime = runtime with
        {
            Plan = async (read, token) =>
            {
                using var registration = token.Register(() => cancelled.TrySetResult());
                await release.Task;
                return new(read.Id, W101CombatPlanKind.Pass, "pass");
            }
        };
        var pending = Run(runtime, timeoutSeconds: 1);
        try
        {
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("premature-release", Locks));
        }
        finally
        {
            release.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        var receipt = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(OperationState.TimedOut, receipt.State);
        Assert.Empty(_sent);
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    [Theory]
    [InlineData("wrong-proposal")]
    [InlineData("old-time")]
    [InlineData("future-time")]
    public async Task MalformedReceiptCannotVerifyTurn(string fault)
    {
        var receipt = await Run(Runtime([W101CombatPhase.Planning, W101CombatPhase.Planning, W101CombatPhase.Victory], pass: true,
            dispatch: action => new(fault == "wrong-proposal" ? "unrelated" : action.ProposalId, action.ActionId,
                fault == "old-time" ? _time - 500 : fault == "future-time" ? _time + 500 : _time, true, true)));
        Assert.Equal(OperationState.Unknown, receipt.State);
        Assert.Single(_sent);
        Assert.False(Report(receipt).GetProperty("turnVerified").GetBoolean());
    }

    [Fact]
    public async Task UnknownFramesStopAtTheReadLimitWithoutInput()
    {
        var receipt = await Run(Runtime([W101CombatPhase.Unknown]), reads: 4);
        Assert.Equal(OperationState.Failed, receipt.State);
        Assert.Equal(4, _read.Count);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task InvalidNormalizedBoundsCannotReachPlannerOrInput()
    {
        var receipt = await Run(Runtime([W101CombatPhase.Planning], alter: (read, _) =>
        {
            read.Controls[0] = read.Controls[0] with { Bounds = new(float.NaN, .2f, .1f, .1f) };
            return read;
        }));
        Assert.Equal(OperationState.Failed, receipt.State);
        Assert.Empty(_sent);
        Assert.Contains("control-ambiguous-or-invalid", receipt.Message);
    }

    [Fact]
    public void DisabledOperationDoesNotCallRuntimeOrSchedule()
    {
        var runtime = Runtime([W101CombatPhase.Planning]);
        Assert.Throws<InvalidOperationException>(() => W101CombatTurnOperation.Prepare(_target, false, runtime));
        Assert.Empty(_read); Assert.Empty(_sent);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
