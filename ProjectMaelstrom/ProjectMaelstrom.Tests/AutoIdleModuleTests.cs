using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class AutoIdleModuleTests
{
    private static readonly RoutineBinding Binding = new("offline-target", "test-geometry", "test-area.visit.one");
    private static ModuleAction Pulse(string key = "J", int duration = 60, string? id = null) =>
        new(id ?? "test.key-" + key, "keyboard-pulse", key, duration, "test.clear-area");
    private static AutoIdleConfiguration Motion(string mode = "routine", params string[] keys) =>
        new(mode, (keys.Length == 0 ? new[] { "J", "I", "J" } : keys).Select(key => Pulse(key)).ToArray(),
            OutcomeScenes: Enumerable.Repeat("MotionObserved", keys.Length == 0 ? 3 : keys.Length).ToArray());
    private static MenuConfiguration Menu(string final = "NativeIdle", bool alreadyComplete = true) =>
        new([
            new("open", "OutsideReady", "MenuObserved", new("test.menu", "keyboard-pulse", "ESCAPE", 40, "test.menu-open")),
            new("idle", "MenuObserved", final, new("test.idle", "verified-control-click", null, 40, "test.idle-button"))
        ], AllowIntermediateEntry: true, AllowAlreadyComplete: alreadyComplete);
    private static OperationModuleSpec Spec(AutoIdleConfiguration configuration) =>
        OperationModules.Spec("test-idle", "auto-idle", "another-game", configuration, "test.clear-area");
    private static OperationModuleDefinition Compile(AutoIdleConfiguration configuration) => AutoIdleModule.Compile(Spec(configuration));
    private static RoutineRunner Runner(OperationModuleDefinition definition) =>
        new(definition.Graph, Binding, definition.Actions.Select(action => action.ActionId).ToHashSet(StringComparer.Ordinal), "test");
    private static RoutineObservation Observe(double time, string scene, string? outcome = null) =>
        new(time, scene, Binding, Fresh: true, TargetReady: true, ContextVerified: true,
            InputOwned: true, ControlVerified: true, OutcomeForProposalId: outcome);
    private static void Receipt(RoutineRunner runner, RoutineDecision proposal, double time) =>
        runner.RecordReceipt(new(proposal.ProposalId!, proposal.ActionId!, time, Accepted: true, InputReleased: true));

    [Theory]
    [InlineData("spin", "J")]
    [InlineData("step", "I,K")]
    [InlineData("routine", "J,I,J")]
    public void MotionModesPreserveOtherGameKeysOrderAndExactActionBounds(string mode, string keys)
    {
        var configuration = Motion(mode, keys.Split(','));
        var definition = Compile(configuration);
        Assert.Equal("another-game", definition.Graph.GameId);
        Assert.Equal(definition.Spec.Id, definition.Graph.Id);
        Assert.Equal("auto-idle", definition.Spec.Kind);
        Assert.Equal(new[] { "test.clear-area" }, definition.Spec.RequiredFacts);
        Assert.Equal(configuration.Pulses.Length, definition.Graph.MaxActions);
        var bindings = definition.Actions.ToDictionary(action => action.ActionId);
        Assert.Equal(keys, string.Join(',', definition.Graph.Nodes.SelectMany(node => node.Edges).Select(edge => bindings[edge.ActionId].Key)));
        Assert.All(definition.Graph.Nodes.SelectMany(node => node.Edges), edge =>
        {
            Assert.True(edge.RequiresControl);
            Assert.True(edge.RequiresOutcomeBinding);
        });
        Assert.Empty(definition.Graph.Nodes.Single(node => node.Id == "done").Edges);
    }

    [Fact]
    public void RepeatedOutcomeLabelsNeedANewProposalBoundProofForEachPulse()
    {
        var runner = Runner(Compile(Motion("step", "I", "K")));
        var first = runner.Observe(Observe(100, "IdleAreaReady"));
        Assert.Equal("test.key-I", first.ActionId);
        Assert.Equal("dispatch-receipt-required", runner.Observe(Observe(110, "MotionObserved", first.ProposalId)).Reason);
        Receipt(runner, first, 120);
        Assert.Equal("outcome-proposal-binding-required", runner.Observe(Observe(140, "MotionObserved")).Reason);
        var second = runner.Observe(Observe(160, "MotionObserved", first.ProposalId));
        Assert.Equal("test.key-K", second.ActionId);
        Receipt(runner, second, 180);
        Assert.Equal("outcome-proposal-binding-required", runner.Observe(Observe(200, "MotionObserved", first.ProposalId)).Reason);
        Assert.True(runner.Observe(Observe(220, "MotionObserved", second.ProposalId)).Completed);
        Assert.False(runner.Observe(Observe(240, "IdleAreaReady")).IsProposal);
        Assert.True(runner.Status.Completed);
    }

    [Fact]
    public void MotionWaitsForControlFreshnessAndOutcomeThroughUnknownAndLoading()
    {
        var runner = Runner(Compile(Motion("spin", "J")));
        Assert.Equal("action-control-unverified", runner.Observe(Observe(10, "IdleAreaReady") with { ControlVerified = false }).Reason);
        Assert.Equal("observation-not-ready", runner.Observe(Observe(20, "IdleAreaReady") with { Fresh = false }).Reason);
        Assert.Equal("scene-pause", runner.Observe(Observe(30, "Loading")).Reason);
        var action = runner.Observe(Observe(40, "IdleAreaReady"));
        Receipt(runner, action, 60);
        Assert.Equal("scene-pause", runner.Observe(Observe(80, "Unknown", action.ProposalId)).Reason);
        Assert.Equal("scene-pause", runner.Observe(Observe(100, "Loading", action.ProposalId)).Reason);
        Assert.Equal("observation-not-ready", runner.Observe(Observe(120, "MotionObserved", action.ProposalId) with { Fresh = false }).Reason);
        Assert.True(runner.Observe(Observe(140, "MotionObserved", action.ProposalId)).Completed);
    }

    [Fact]
    public void MotionNeverWakesTheConfiguredNativeIdleStateAndWaitsAreFinite()
    {
        var configuration = Motion("spin", "J") with { NativeIdleScene = "AFK", TimeoutMs = 1000 };
        var runner = Runner(Compile(configuration));
        Assert.Equal("scene-pause", runner.Observe(Observe(0, "AFK")).Reason);
        Assert.False(runner.Observe(Observe(500, "AFK")).IsProposal);
        var stopped = runner.Observe(Observe(1000, "AFK"));
        Assert.True(stopped.Stopped);
        Assert.Equal("routine-deadline-exceeded", stopped.Reason);
        Assert.False(runner.Observe(Observe(1100, "IdleAreaReady")).IsProposal);
    }

    [Theory]
    [InlineData("takeover")]
    [InlineData("cancel")]
    [InlineData("ownership")]
    [InlineData("target")]
    [InlineData("binding")]
    public void InterruptionLatchesWithoutRestartOrAnotherProposal(string change)
    {
        var runner = Runner(Compile(Motion("step", "I", "K")));
        var action = runner.Observe(Observe(100, "IdleAreaReady")); Receipt(runner, action, 120);
        var frame = Observe(140, "MotionObserved", action.ProposalId);
        frame = change switch
        {
            "takeover" => frame with { UserActive = true },
            "cancel" => frame with { Cancelled = true },
            "ownership" => frame with { InputOwned = false },
            "target" => frame with { TargetReady = false },
            _ => frame with { Binding = Binding with { GeometryId = "changed" } }
        };
        Assert.True(runner.Observe(frame).Stopped);
        Assert.False(runner.Observe(Observe(200, "MotionObserved", action.ProposalId)).IsProposal);
    }

    [Fact]
    public void RejectedOrUnreleasedReceiptCannotCompleteMotion()
    {
        foreach (var accepted in new[] { false, true })
        {
            var runner = Runner(Compile(Motion("spin", "J")));
            var action = runner.Observe(Observe(100, "IdleAreaReady"));
            runner.RecordReceipt(new(action.ProposalId!, action.ActionId!, 120, Accepted: accepted, InputReleased: false));
            var decision = runner.Observe(Observe(140, "MotionObserved", action.ProposalId));
            Assert.True(decision.Stopped);
            Assert.False(decision.Completed);
        }
    }

    [Fact]
    public void NativeIdleDelegatesToMenuWithoutDiscardingOriginalModuleIdentity()
    {
        var definition = Compile(new("native", [], NativeMenu: Menu()));
        Assert.Equal("auto-idle", definition.Spec.Kind);
        Assert.Equal("another-game", definition.Graph.GameId);
        Assert.Equal(new[] { "test.menu", "test.idle" }, definition.Actions.Select(action => action.ActionId));
        var runner = Runner(definition);
        var open = runner.Observe(Observe(100, "OutsideReady"));
        Assert.Equal("test.menu", open.ActionId); Receipt(runner, open, 120);
        Assert.False(runner.Observe(Observe(140, "MenuObserved")).IsProposal);
        var idle = runner.Observe(Observe(160, "MenuObserved", open.ProposalId));
        Assert.Equal("test.idle", idle.ActionId); Receipt(runner, idle, 180);
        Assert.False(runner.Observe(Observe(200, "NativeIdle")).Completed);
        Assert.True(runner.Observe(Observe(220, "NativeIdle", idle.ProposalId)).Completed);
    }

    [Fact]
    public void AnotherGameMayUseAnExplicitAfkTerminalAndAlreadyCompletePolicy()
    {
        var definition = Compile(new("native", [], NativeMenu: Menu("AFK"), NativeIdleScene: "AFK"));
        Assert.True(Runner(definition).Observe(Observe(100, "AFK")).Completed);
        var explicitEntry = Compile(new("native", [], NativeMenu: Menu("AFK", alreadyComplete: false), NativeIdleScene: "AFK"));
        var observation = Runner(explicitEntry).Observe(Observe(100, "AFK"));
        Assert.False(observation.Completed);
        Assert.False(observation.IsProposal);
    }

    [Fact]
    public void NativeMenuCannotExtendTheOuterIdleBudget()
    {
        var menu = Menu() with { TransitionTimeoutMs = 800 };
        var definition = Compile(new("native", [], NativeMenu: menu, TimeoutMs: 1000));
        Assert.Equal(1000, definition.Graph.TimeoutMs);
        Assert.Throws<InvalidDataException>(() => Compile(new("native", [], NativeMenu: Menu(), TimeoutMs: 1000)));
    }

    [Fact]
    public void MotionBoundsAllowEightShortPulsesButRejectLongOrAmbiguousBindings()
    {
        var valid = new AutoIdleConfiguration("routine", Enumerable.Repeat(Pulse("J", 75), 8).ToArray(),
            OutcomeScenes: Enumerable.Repeat("MotionObserved", 8).ToArray());
        Assert.Equal(8, Compile(valid).Graph.MaxActions);
        Assert.Single(Compile(valid).Actions);
        foreach (var duration in new[] { 39, 151 })
            Assert.Throws<InvalidDataException>(() => Compile(new("spin", [Pulse("J", duration)], OutcomeScenes: ["Moved"])));
        Assert.Throws<InvalidDataException>(() => Compile(new("routine", Enumerable.Repeat(Pulse("J", 150), 5).ToArray(), OutcomeScenes: Enumerable.Repeat("Moved", 5).ToArray())));
        Assert.Throws<InvalidDataException>(() => Compile(new("routine", Enumerable.Repeat(Pulse(), 9).ToArray(), OutcomeScenes: Enumerable.Repeat("Moved", 9).ToArray())));
        Assert.Throws<InvalidDataException>(() => Compile(new("step", [Pulse("J", id: "same"), Pulse("K", id: "same")], OutcomeScenes: ["Moved", "Moved"])));
        Assert.Throws<InvalidDataException>(() => Compile(new("step", [new("click", "verified-control-click", null, 40, "button")], OutcomeScenes: ["Moved"])));
    }

    [Fact]
    public void WrongModesMissingOutcomesPauseTriggersAndMixedMenuMotionAreRejected()
    {
        var configurations = new AutoIdleConfiguration[]
        {
            new("unknown", []), new("spin", []), new("spin", [Pulse()]),
            new("spin", [Pulse()], OutcomeScenes: []),
            new("spin", [Pulse()], OutcomeScenes: ["Unknown"]),
            new("spin", [Pulse()], EntryScene: "NativeIdle", OutcomeScenes: ["Moved"]),
            new("spin", [Pulse()], OutcomeScenes: ["Moved"], NativeMenu: Menu()),
            new("spin", [Pulse()], OutcomeScenes: ["Moved"], NativeIdleScene: "Loading"),
            new("native", []), new("native", [Pulse()], NativeMenu: Menu()),
            new("native", [], OutcomeScenes: ["Moved"], NativeMenu: Menu()),
            new("native", [], NativeMenu: Menu("WrongTerminal")),
            new("native", [], NativeMenu: new([new("wake", "NativeIdle", "OutsideReady", Pulse()), new("idle", "OutsideReady", "NativeIdle", Pulse())]))
        };
        foreach (var configuration in configurations)
            Assert.Throws<InvalidDataException>(() => Compile(configuration));
        foreach (var timeout in new double[] { 0, -1, 300001 })
            Assert.Throws<InvalidDataException>(() => Compile(Motion("spin", "J") with { TimeoutMs = timeout }));
        foreach (var timeout in new[] { double.NaN, double.PositiveInfinity })
            Assert.Throws<InvalidDataException>(() => Spec(Motion("spin", "J") with { TimeoutMs = timeout }));
        Assert.Throws<InvalidDataException>(() => AutoIdleModule.Compile(Spec(Motion()) with { Kind = "menu" }));
    }
}
