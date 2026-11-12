using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101UiTreeOperationTests : IDisposable
{
    [Fact]
    public void PoseCandidatesRequireFreshBoundedOptInAndNeverGrantMovement()
    {
        var now = DateTime.UtcNow.Ticks;
        var pose = new W101PoseCandidate("archived-wizwalker-actor-body", "candidate", false, false,
            new(10, 20, 30), 1.5, now, 6, 72);
        Assert.True(W101UiTreeOperation.ValidPose(pose, true, now));
        Assert.False(W101UiTreeOperation.ValidPose(pose, false, now));
        Assert.False(W101UiTreeOperation.ValidPose(pose with { MovementAuthorized = true }, true, now));
        Assert.False(W101UiTreeOperation.ValidPose(pose with { Position = new(double.NaN, 20, 30) }, true, now));
        Assert.False(W101UiTreeOperation.ValidPose(pose, true, now + TimeSpan.FromSeconds(6).Ticks));
        Assert.True(W101UiTreeOperation.ValidPose(pose with { Status = "unknown", Position = null, YawRadians = null }, true, now));
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ui-tree-operation-" + Guid.NewGuid().ToString("N"));
    private readonly WizwalkerLaunch _launch;
    private readonly WizwalkerTarget _target;
    private string Locks => Path.Combine(_root, "locks");
    public W101UiTreeOperationTests()
    {
        Directory.CreateDirectory(_root);
        string Fixture(string name) { var path = Path.Combine(_root, name); File.WriteAllText(path, "inert fixture"); return path; }
        _launch = new(Fixture("python.exe"), Fixture("w101_ui_tree.py"), _root);
        Fixture("wizwalker_adapter.py");
        Fixture("w101_combat_reader.py");
        Fixture("w101_duel_reader.py");
        var image = Fixture("WizardGraphicalClient.exe");
        _target = new(42, 123, 100, "WizardGraphicalClient", image, GameExperiments.Digest(image));
    }
    private static Dictionary<string, bool?> States() => new()
    { ["interactionPrompt"] = false, ["dialogue"] = false, ["menu"] = false, ["nativeIdle"] = true };

    [Theory]
    [InlineData("valid", true)]
    [InlineData("absent", true)]
    [InlineData("orphan", false)]
    [InlineData("different", false)]
    [InlineData("oversize", false)]
    [InlineData("stale", false)]
    [InlineData("loading", false)]
    [InlineData("prompt-hidden", false)]
    [InlineData("one-sample", false)]
    public void PromptEvidenceMustBeBoundedCurrentAndConsistent(string fault, bool expected)
    {
        var now = DateTime.UtcNow.Ticks;
        var states = States(); states["interactionPrompt"] = fault != "prompt-hidden";
        var title = fault is "absent" or "orphan" ? null : fault == "oversize" ? new string('x', 161) : "Sir Nigel";
        var result = new W101UiTreeResult(1, "test", W101UiTreeOperation.Version, WizwalkerOperation.SourceSha256,
            _target, "observed", "test", fault == "loading", states, now,
            fault == "stale" ? now - TimeSpan.FromSeconds(3).Ticks : now, 1000, true, true, 4,
            fault == "one-sample" ? 1 : 2, 0, new(states),
            InteractionTitle: title, StableInteractionTitle: fault == "absent" ? null : fault == "different" ? "Other NPC" : "Sir Nigel");
        Assert.Equal(expected, W101UiTreeOperation.ValidInteractionTitle(result, now));
    }
    private W101UiTreeResult Result(W101UiTreeRequest r)
    {
        var stamp = DateTime.UtcNow.Ticks;
        return new(1, r.RunId, r.Operation, r.SourceSha256, r.Target, "observed", "bounded-ui-watch-finished",
            false, States(), stamp, stamp, 1000, true, true, 4, 2, 0, States());
    }
    private W101UiTreeRuntime Runtime(Func<W101UiTreeRequest, W101UiTreeResult>? result = null) => new(_ => true,
        (_, r, context, _) =>
        {
            Assert.Same(context.Input, GameInputLease.Current);
            Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competitor", Locks));
            Assert.False(r.NetworkAllowed); Assert.True(r.UiTreeEnabled);
            Assert.Contains(Path.Combine(_root, "wizwalker_adapter.py"), context.Request.Pins.Keys);
            return Task.FromResult((result ?? Result)(r));
        });

    [Fact]
    public async Task QuestContextOptInTravelsThroughTheSameOwnerAndIsValidated()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var context = new W101QuestContext("Talk to Quinn", "Go speak to the guard.", true);
        var runtime = Runtime(r =>
        {
            Assert.True(r.IncludeQuestContext);
            return Result(r) with { QuestContext = context, StableQuestContext = context };
        });
        Assert.Equal(OperationState.Completed, (await W101UiTreeOperation.Submit(scheduler, new(_target, true), _launch, true,
            runtime: runtime, includeQuestContext: true).Completion).State);
    }

    [Theory]
    [InlineData("default-off")]
    [InlineData("oversize")]
    [InlineData("conflicting")]
    [InlineData("loading")]
    [InlineData("orphan-stability")]
    public async Task InvalidQuestContextCannotBecomeUsable(string fault)
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var context = new W101QuestContext(fault == "oversize" ? new string('x', 257) : "Talk to Quinn", "Dialogue", true);
        var runtime = Runtime(r => Result(r) with
        {
            QuestContext = fault == "orphan-stability" ? null : context,
            StableQuestContext = fault == "conflicting" ? context with { DialogueText = "different" } : context,
            Loading = fault == "loading"
        });
        Assert.Equal(OperationState.Failed, (await W101UiTreeOperation.Submit(scheduler, new(_target, true), _launch, true,
            runtime: runtime, includeQuestContext: fault != "default-off").Completion).State);
    }

    [Fact]
    public async Task DefaultOffOrUnreviewedSourceDoesNotAttach()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        Assert.Throws<InvalidOperationException>(() => W101UiTreeOperation.Submit(scheduler, new(_target, true), _launch, false, runtime: Runtime()));
        Assert.Throws<InvalidOperationException>(() => W101UiTreeOperation.Submit(scheduler, new(_target), _launch, true, runtime: Runtime()));
        Assert.Throws<InvalidOperationException>(() => W101UiTreeOperation.Submit(scheduler, new(_target, true), _launch, true));
        Assert.Empty(OperationScheduler.History(_root));
    }

    private static W101CombatUiContext Combat() => new(1, "structural-unqualified", "raw-layout-unqualified", false, false,
        true, true, [new(2, 1, "PlanningPhase", "planning-container", [0, 0, 100, 100], true, true),
            new(7, 6, "Hand", "hand-container", null, true, false)]);

    [Fact]
    public void MaximumUnicodeContextFitsTheBoundedWorkerResult()
    {
        var request = new W101UiTreeRequest(1, new string('a', 32), W101UiTreeOperation.Version, true, false,
            _target, _root, WizwalkerOperation.SourceSha256, 8, 2, true, true, true);
        var controls = Enumerable.Range(0, 64).Select(i => new W101CombatUiControl(i, null, new string('\u754c', 128),
            i == 0 ? "planning-container" : i == 1 ? "hand-container" : i is >= 18 and < 26 ? "combatant-container" : "hand-descendant", [0, 0, 100, 100], true, true)).ToArray();
        var quest = new W101QuestContext(new string('\u754c', 256), new string('\u754c', 2048), true);
        var cards = Enumerable.Range(2, 16).Select(i => new W101CombatCardCandidate(i, 1, (uint)i, new string('\u754c', 128),
            new string('\u754c', 128), 234, 100, 0, false, false, true, false, null,
            Enumerable.Range(0, 8).Select(_ => new W101CombatEffectCandidate(1024, -10000000, 0, uint.MaxValue, 17, 10000, int.MaxValue, true)).ToArray())).ToArray();
        var participants = Enumerable.Range(18, 8).Select(i => new W101CombatParticipantCandidate(i, i.ToString(), "0", 1, false,
            234, 100, 100, false, new()
            {
                ["generic"] = 1,
                ["power"] = 0,
                ["balance"] = 0,
                ["death"] = 0,
                ["fire"] = 0,
                ["ice"] = 0,
                ["life"] = 0,
                ["myth"] = 0,
                ["storm"] = 0,
                ["shadow"] = 0
            }, true, null)).ToArray();
        var fields = new W101CombatFieldCandidates(1, "deimos-development-df2283f", "source-layout-only", false, null, null, null, cards, participants, [], 65536, 768);
        var result = Result(request) with { CombatContext = Combat() with { Controls = controls, FieldCandidates = fields }, QuestContext = quest, StableQuestContext = quest };
        Assert.True(W101UiTreeOperation.ValidCombatContext(result, true, DateTime.UtcNow.Ticks));
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(result, WizwalkerOperation.Json);
        Assert.True(bytes.Length > 131072, "Independent field maxima must cover the old insufficient result cap.");
        Assert.True(bytes.Length < W101UiTreeOperation.MaximumResultBytes);
        var path = Path.Combine(_root, "large-ui-result.json"); File.WriteAllBytes(path, bytes);
        var read = System.Text.Json.JsonSerializer.Deserialize<W101UiTreeResult>(GameExperiments.BoundedBytes(path, W101UiTreeOperation.MaximumResultBytes), WizwalkerOperation.Json);
        Assert.Equal(64, read!.CombatContext!.Controls.Length);
    }

    [Fact]
    public async Task CombatCandidatesUseSameOwnerWithoutBecomingActionable()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var runtime = Runtime(r =>
        {
            Assert.True(r.IncludeCombatContext); Assert.False(r.IncludeQuestContext);
            return Result(r) with { CombatContext = Combat() };
        });
        var receipt = await W101UiTreeOperation.Submit(scheduler, new(_target, true), _launch, true,
            runtime: runtime, includeCombatContext: true).Completion;
        Assert.Equal(OperationState.Completed, receipt.State);
        Assert.Contains("Candidate states only", receipt.Message);
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    [Theory]
    [InlineData("default-off")]
    [InlineData("actionable")]
    [InlineData("pixels")]
    [InlineData("schema")]
    [InlineData("loading")]
    [InlineData("stale")]
    [InlineData("duplicate")]
    [InlineData("unknown-kind")]
    [InlineData("rectangle")]
    [InlineData("too-many")]
    [InlineData("contradictory")]
    public async Task InvalidCombatCandidatesAreRejected(string fault)
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var combat = Combat();
        combat = fault switch
        {
            "actionable" => combat with { CastabilityKnown = true },
            "pixels" => combat with { GeometrySpace = "client-pixels" },
            "schema" => combat with { SchemaVersion = 2 },
            "duplicate" => combat with { Controls = [combat.Controls[0], combat.Controls[0]] },
            "unknown-kind" => combat with { Controls = [combat.Controls[0] with { Kind = "confirmed-spell" }, combat.Controls[1]] },
            "rectangle" => combat with { Controls = [combat.Controls[0] with { RawRectangle = [1, 2, 3] }, combat.Controls[1]] },
            "too-many" => combat with { Controls = Enumerable.Repeat(combat.Controls[0], 65).ToArray() },
            "contradictory" => combat with { PlanningVisible = false },
            _ => combat
        };
        var runtime = Runtime(r => Result(r) with
        {
            CombatContext = combat,
            Loading = fault == "loading",
            StatesObservedUtcTicks = DateTime.UtcNow.AddSeconds(fault == "stale" ? -10 : -0.1).Ticks
        });
        var receipt = await W101UiTreeOperation.Submit(scheduler, new(_target, true), _launch, true,
            runtime: runtime, includeCombatContext: fault != "default-off").Completion;
        Assert.Equal(OperationState.Failed, receipt.State);
    }

    [Fact]
    public async Task ObservedCandidatesUseExistingLeaseAndVerifiedCleanup()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var receipt = await W101UiTreeOperation.Submit(scheduler, new(_target, true), _launch, true, runtime: Runtime()).Completion;
        Assert.Equal(OperationState.Completed, receipt.State);
        Assert.Contains("Candidate states only", receipt.Message);
        Assert.All(receipt.Artifacts, path => Assert.True(File.Exists(path)));
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    [Theory]
    [InlineData("root-signature-unavailable")]
    [InlineData("root-instruction-changed")]
    [InlineData("scratch-signature-unavailable")]
    [InlineData("client-build-unreviewed")]
    public async Task IncompatibleClientExplainsWhySetupWasWithheld(string code)
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var runtime = Runtime(r => Result(r) with { Status = "unavailable", Code = code, TreeSamples = 0 });
        var receipt = await W101UiTreeOperation.Submit(scheduler, new(_target, true), _launch, true, runtime: runtime).Completion;
        Assert.Equal(OperationState.Failed, receipt.State);
        Assert.Contains("does not match the reviewed UI reader", receipt.Message);
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    [Theory]
    [InlineData("disconnect")]
    [InlineData("restoration")]
    [InlineData("identity")]
    [InlineData("schema")]
    public async Task UncertainRestorationOrIdentityBlocksFutureGameplay(string fault)
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var runtime = Runtime(r => fault switch
        {
            "disconnect" => Result(r) with { CleanupConfirmed = false },
            "restoration" => Result(r) with { HookRestored = false },
            "identity" => Result(r) with { Target = _target with { ProcessId = 987 } },
            _ => Result(r) with { SchemaVersion = 2 }
        });
        var receipt = await W101UiTreeOperation.Submit(scheduler, new(_target, true), _launch, true, runtime: runtime).Completion;
        Assert.Equal(OperationState.CleanupFailed, receipt.State);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", Locks));
    }

    [Theory]
    [InlineData("old-tree")]
    [InlineData("old-loading")]
    [InlineData("future")]
    [InlineData("unknown-fields")]
    [InlineData("stability-conflict")]
    [InlineData("no-trees")]
    public async Task InvalidStateEvidenceCannotBecomeUsable(string fault)
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var runtime = Runtime(r => fault switch
        {
            "old-tree" => Result(r) with { StatesObservedUtcTicks = DateTime.UtcNow.AddSeconds(-10).Ticks },
            "old-loading" => Result(r) with { ObservedUtcTicks = DateTime.UtcNow.AddSeconds(-10).Ticks },
            "future" => Result(r) with { StatesObservedUtcTicks = DateTime.UtcNow.AddMinutes(1).Ticks },
            "unknown-fields" => Result(r) with { States = [] },
            "stability-conflict" => Result(r) with { StableStates = new(States()) { ["nativeIdle"] = false } },
            _ => Result(r) with { TreeSamples = 0 }
        });
        Assert.Equal(OperationState.Failed, (await W101UiTreeOperation.Submit(scheduler, new(_target, true), _launch, true, runtime: runtime).Completion).State);
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    [Fact]
    public async Task CleanUnavailableIsNotReadyAndCancellationKeepsOwnerUntilDisconnect()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        var unavailable = Runtime(r => Result(r) with { Status = "unavailable", HookRestored = false });
        Assert.Equal(OperationState.Failed, (await W101UiTreeOperation.Submit(scheduler, new(_target, true), _launch, true, runtime: unavailable).Completion).State);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool restored = false;
        var runtime = new W101UiTreeRuntime(_ => true, async (_, r, context, token) =>
        {
            entered.SetResult();
            try { await Task.Delay(10000, token); } catch (OperationCanceledException) { }
            Assert.True(context.Input!.CanDispatch); restored = true;
            return Result(r) with { Status = "cancelled" };
        });
        var ticket = W101UiTreeOperation.Submit(scheduler, new(_target, true), _launch, true, runtime: runtime);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); ticket.Cancel();
        Assert.Equal(OperationState.Cancelled, (await ticket.Completion).State); Assert.True(restored);
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
