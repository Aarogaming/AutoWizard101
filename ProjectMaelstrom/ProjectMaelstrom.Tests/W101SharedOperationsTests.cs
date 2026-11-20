using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class W101SharedOperationsTests
{
    private static W101RouteSpecification Route() => new("synthetic", "wizard-city", "commons", "pet-pavilion", "dance",
        [new("first", "start", "junction", "step-forward"), new("second", "junction", "sigil", "turn-left")]);
    private static OperationModuleBundle Plan() => W101SharedOperations.RouteAndMenuVisit(Route());
    private static RoutineBinding Bind(OperationModuleBundle plan) => plan.Bind("offline-window", "1280x720", "fixture");
    private static ModuleRunner Runner(OperationModuleBundle plan, string runId = "test-run") => new(plan, Bind(plan), runId);
    private static ModuleAction[] Actions(OperationModuleBundle plan) => ModuleCatalog.Compile(plan).SelectMany(module => module.Actions).ToArray();
    private static ModuleObservation Observe(OperationModuleBundle plan, double time, string scene) =>
        new(new(time, scene, Bind(plan), Fresh: true, TargetReady: true, ContextVerified: true,
            InputOwned: true, ControlVerified: true), plan.Modules.SelectMany(module => module.RequiredFacts).Distinct().ToArray(),
            Actions(plan).Select(action => action.ControlId).Distinct().ToArray());
    private static ModuleObservation Outcome(OperationModuleBundle plan, double time, string scene, ModuleDecision action)
    {
        var frame = Observe(plan, time, scene);
        return frame with { Observation = frame.Observation with { OutcomeForProposalId = action.ProposalId } };
    }
    private static void Receipt(ModuleRunner runner, ModuleDecision decision, double time) =>
        runner.RecordReceipt(new(decision.ProposalId!, decision.ActionId!, time, Accepted: true, InputReleased: true));

    [Fact]
    public void Wizard101SuppliesFourReusableModuleBindingsInsteadOfACustomGraph()
    {
        var bundle = Plan();
        Assert.Equal(new[] { "navigation", "sigil", "menu", "auto-idle" }, bundle.Modules.Select(module => module.Kind));
        var definitions = ModuleCatalog.Compile(bundle);
        Assert.Equal("Landmark.sigil", definitions[0].Graph.Nodes[^1].CompleteScene);
        Assert.DoesNotContain(definitions[0].Actions, action => action.ActionId == "game.interact");
        Assert.All(definitions, definition => Assert.Equal("wizard101", definition.Graph.GameId));
        Assert.All(definitions.SelectMany(definition => definition.Graph.Nodes).SelectMany(node => node.Edges), edge =>
        { Assert.True(edge.RequiresControl); Assert.True(edge.RequiresOutcomeBinding); });
        var restored = ModuleCatalog.ReadBundle(JsonSerializer.Serialize(bundle, OperationModules.Json));
        Assert.Equal(bundle.ContextId, restored.ContextId);
        Assert.Equal(bundle.Modules.Select(module => module.Id), restored.Modules.Select(module => module.Id));
    }

    [Fact]
    public void BooleanVerificationDoesNotReplaceNamedRouteFactsOrTheHomeRequirement()
    {
        var route = Plan();
        Assert.False(Runner(route).Observe(Observe(route, 100, "Landmark.start") with { VerifiedFacts = [] }).IsProposal);
        Assert.False(Runner(route).Observe(Observe(route, 100, "Landmark.start") with { VerifiedControls = ["unrelated.control"] }).IsProposal);
        var home = W101SharedOperations.IdleCycle("home", new(Mode: HomeIdleMode.WalkPairs));
        var frame = Observe(home, 100, "IdleAreaReady");
        Assert.False(Runner(home).Observe(frame with
        { VerifiedFacts = frame.VerifiedFacts!.Where(fact => fact != "wizard101.home-confirmed").ToArray() }).IsProposal);
        Assert.True(Runner(home).Observe(frame).IsProposal);
    }

    [Fact]
    public void RouteRepetitionNeedsReceiptAndNewProgressWhileArrivalChangesThePrimitive()
    {
        var plan = Plan(); var runner = Runner(plan);
        var first = runner.Observe(Observe(plan, 100, "Landmark.start"));
        Assert.Equal("step-forward", first.ActionId);
        Assert.Equal("dispatch-receipt-required", runner.Observe(Observe(plan, 110, "Progress.first")).Reason);
        Receipt(runner, first, 120);
        Assert.Equal("invalid-or-repeated-time", runner.Observe(Observe(plan, 120, "Progress.first")).Reason);
        var stale = Observe(plan, 130, "Progress.first");
        Assert.False(runner.Observe(stale with { Observation = stale.Observation with { Fresh = false } }).IsProposal);
        Assert.Equal("observed-outcome-required", runner.Observe(Outcome(plan, 140, "Landmark.start", first)).Reason);
        var repeat = runner.Observe(Outcome(plan, 150, "Progress.first", first));
        Assert.Equal("step-forward", repeat.ActionId);
        Assert.NotEqual(first.ProposalId, repeat.ProposalId);
        Receipt(runner, repeat, 160);
        var next = runner.Observe(Outcome(plan, 180, "Landmark.junction", repeat));
        Assert.Equal("turn-left", next.ActionId);
        Assert.False(plan.LiveQualified);
    }

    [Fact]
    public void WrongLandmarkOrElapsedTimeCannotInventArrival()
    {
        var plan = Plan(); var runner = Runner(plan);
        Assert.False(runner.Observe(Observe(plan, 10, "Landmark.junction")).IsProposal);
        var action = runner.Observe(Observe(plan, 100, "Landmark.start"));
        Receipt(runner, action, 120);
        Assert.False(runner.Observe(Outcome(plan, 200, "Landmark.sigil", action)).IsProposal);
        Assert.False(runner.Observe(Observe(plan, 1000, "Unknown")).IsProposal);
        var deadline = runner.Observe(Observe(plan, 10100, "Loading"));
        Assert.True(deadline.Stopped); Assert.Equal("transition-deadline-exceeded", deadline.Reason);
        Assert.False(deadline.Completed);
    }

    [Fact]
    public void ControlAndNamedSigilAreRequiredBeforeMenuSelection()
    {
        var plan = Plan(); var runner = Runner(plan);
        Assert.False(runner.Observe(Observe(plan, 10, "Landmark.start") with { VerifiedControls = [] }).IsProposal);
        var first = runner.Observe(Observe(plan, 20, "Landmark.start")); Receipt(runner, first, 30);
        var second = runner.Observe(Outcome(plan, 40, "Landmark.junction", first)); Receipt(runner, second, 50);
        Assert.False(runner.Observe(Outcome(plan, 60, "Landmark.sigil", second) with { VerifiedControls = [] }).IsProposal);
        var interact = runner.Observe(Outcome(plan, 70, "Landmark.sigil", second));
        Assert.Equal("game.interact", interact.ActionId); Receipt(runner, interact, 80);
        Assert.False(runner.Observe(Observe(plan, 90, "SigilMenu.cannon")).IsProposal);
        var cancel = runner.Observe(Outcome(plan, 100, "SigilMenu.dance", interact));
        Assert.Equal("game.cancel-menu", cancel.ActionId);
        Assert.Equal("sigil.dance.cancel", cancel.Action!.ControlId);
        Assert.Equal("verified-control-click", cancel.Action.Kind);
    }

    [Theory]
    [InlineData("rejected")]
    [InlineData("unreleased")]
    [InlineData("wrong-action")]
    [InlineData("wrong-run")]
    public void ReceiptFailuresStopWithoutOutcomeOrFurtherInput(string failure)
    {
        var plan = Plan(); var runner = Runner(plan);
        var action = runner.Observe(Observe(plan, 100, "Landmark.start"));
        runner.RecordReceipt(new(failure == "wrong-run" ? "another-run/1" : action.ProposalId!,
            failure == "wrong-action" ? "turn-right" : action.ActionId!, 120,
            Accepted: failure != "rejected", InputReleased: failure != "unreleased"));
        Assert.True(runner.Status.Stopped);
        Assert.Equal("dispatch-unconfirmed", runner.Status.Reason);
        Assert.False(runner.Observe(Observe(plan, 200, "Landmark.junction")).IsProposal);
    }

    [Theory]
    [InlineData("takeover")]
    [InlineData("cancel")]
    [InlineData("geometry")]
    [InlineData("context")]
    [InlineData("ownership")]
    public void ChangedContextOrUserActivityEndsTheAttemptWithoutAutomaticResume(string change)
    {
        var plan = Plan(); var runner = Runner(plan);
        var action = runner.Observe(Observe(plan, 100, "Landmark.start")); Receipt(runner, action, 120);
        var frame = Observe(plan, 140, "Landmark.junction");
        var observation = frame.Observation;
        observation = change switch
        {
            "takeover" => observation with { UserActive = true },
            "cancel" => observation with { Cancelled = true },
            "geometry" => observation with { Binding = observation.Binding with { GeometryId = "different-geometry" } },
            "context" => observation with { Binding = observation.Binding with { ContextId = "different-world" } },
            _ => observation with { InputOwned = false }
        };
        Assert.True(runner.Observe(frame with { Observation = observation }).Stopped);
        Assert.False(runner.Observe(Observe(plan, 200, "Landmark.junction")).IsProposal);
    }

    [Fact]
    public void LoadingAndUnknownPreserveThePendingReceiptAndOutcomeRequirements()
    {
        var plan = Plan(); var runner = Runner(plan);
        Assert.Equal("scene-pause", runner.Observe(Observe(plan, 10, "Loading")).Reason);
        var action = runner.Observe(Observe(plan, 100, "Landmark.start")); Receipt(runner, action, 120);
        foreach (var scene in new[] { "Loading", "Unknown" })
            Assert.Equal("scene-pause", runner.Observe(Observe(plan, scene == "Loading" ? 200 : 300, scene)).Reason);
        Assert.Equal("turn-left", runner.Observe(Outcome(plan, 400, "Landmark.junction", action)).ActionId);
    }

    [Fact]
    public void NativeIdleCanAlreadyBeCompleteAndNeverCreatesAWakeAction()
    {
        var plan = W101SharedOperations.IdleCycle("test-area", new(Mode: HomeIdleMode.NativeIdle));
        var runner = Runner(plan);
        var done = runner.Observe(Observe(plan, 100, "NativeIdle"));
        Assert.True(done.Completed); Assert.False(done.IsProposal);
        Assert.False(runner.Observe(Observe(plan, 200, "OutsideReady")).IsProposal);
        Assert.Equal(new[] { "game.open-menu", "wizard101.native-idle" }, Actions(plan).Select(a => a.ActionId));
        Assert.All(Actions(plan), a => Assert.DoesNotContain("wake", a.ActionId));
    }

    [Fact]
    public void NativeIdleRequiresMenuReceiptAndNewBannerObservation()
    {
        var plan = W101SharedOperations.IdleCycle("test-area", new(Mode: HomeIdleMode.NativeIdle));
        var runner = Runner(plan);
        var menu = runner.Observe(Observe(plan, 100, "OutsideReady"));
        Assert.Equal("game.open-menu", menu.ActionId); Receipt(runner, menu, 120);
        var idle = runner.Observe(Outcome(plan, 140, "MenuObserved", menu));
        Assert.Equal("wizard101.native-idle", idle.ActionId);
        Assert.Equal("game-menu.idle", idle.Action!.ControlId);
        Assert.False(runner.Observe(Observe(plan, 150, "NativeIdle")).Completed);
        Receipt(runner, idle, 160);
        Assert.True(runner.Observe(Outcome(plan, 180, "NativeIdle", idle)).Completed);
    }

    [Theory]
    [InlineData("SpinLeft", "A", false)]
    [InlineData("SpinRight", "D", false)]
    [InlineData("OpposingTurns", "A,D", false)]
    [InlineData("WalkPairs", "W,S", true)]
    [InlineData("Routine", "D,W,A", true)]
    public void EveryExistingMotionChoicePreservesItsOrderAndDoesNotWakeNativeIdle(string mode, string keys, bool home)
    {
        var choice = Enum.Parse<HomeIdleMode>(mode);
        var options = new HomeMovementOptions(Mode: choice, RoutineActions: choice == HomeIdleMode.Routine
            ? ["turn-right", "step-forward", "turn-left"] : null);
        var plan = W101SharedOperations.IdleCycle("test-area", options);
        var definition = Assert.Single(ModuleCatalog.Compile(plan));
        var actions = definition.Actions.ToDictionary(a => a.ActionId);
        Assert.Equal(keys, string.Join(',', definition.Graph.Nodes.SelectMany(n => n.Edges).Select(e => actions[e.ActionId].Key)));
        Assert.Equal(home, definition.Spec.RequiredFacts.Contains("wizard101.home-confirmed"));
        Assert.All(definition.Actions, a => Assert.Equal(60, a.DurationMs));
        Assert.All(definition.Graph.Nodes.SelectMany(n => n.Edges), edge => Assert.True(edge.RequiresOutcomeBinding));
        Assert.Equal("scene-pause", Runner(plan).Observe(Observe(plan, 100, "NativeIdle")).Reason);
        Assert.DoesNotContain(definition.Actions, a => a.ActionId == "game.interact");
    }

    [Fact]
    public void MotionCycleCannotAdvanceOnReceiptAloneOrRepeatAfterCompletion()
    {
        var plan = W101SharedOperations.IdleCycle("home", new(Mode: HomeIdleMode.WalkPairs));
        var runner = Runner(plan);
        var forward = runner.Observe(Observe(plan, 100, "IdleAreaReady")); Receipt(runner, forward, 120);
        Assert.False(runner.Observe(Outcome(plan, 140, "IdleAreaReady", forward)).IsProposal);
        var backward = runner.Observe(Outcome(plan, 160, "MotionObserved.1", forward));
        Assert.Equal("step-back", backward.ActionId); Receipt(runner, backward, 180);
        Assert.False(runner.Observe(Outcome(plan, 190, "MotionObserved.2", forward)).Completed);
        Assert.True(runner.Observe(Outcome(plan, 200, "MotionObserved.2", backward)).Completed);
        Assert.False(runner.Observe(Observe(plan, 220, "IdleAreaReady")).IsProposal);
    }

    [Fact]
    public void AStillVisibleProgressLabelCannotSupplyAnOutcomeForTheNextPulse()
    {
        var plan = Plan(); var runner = Runner(plan);
        var first = runner.Observe(Observe(plan, 100, "Landmark.start")); Receipt(runner, first, 120);
        var second = runner.Observe(Outcome(plan, 140, "Progress.first", first)); Receipt(runner, second, 160);
        var held = runner.Observe(Outcome(plan, 180, "Progress.first", first));
        Assert.False(held.IsProposal); Assert.False(held.Stopped); Assert.False(held.Completed);
        Assert.False(runner.Observe(Observe(plan, 200, "Progress.first")).IsProposal);
        Assert.False(runner.Observe(Outcome(plan, 220, "Landmark.junction", first)).IsProposal);
        var corrected = runner.Observe(Outcome(plan, 240, "Progress.first", second));
        Assert.Equal("step-forward", corrected.ActionId);
        Assert.NotEqual(second.ProposalId, corrected.ProposalId);
        Assert.All(ModuleCatalog.Compile(plan).SelectMany(module => module.Graph.Nodes).SelectMany(n => n.Edges).Where(e => e.ActionId is "step-forward" or "turn-left"),
            edge => Assert.True(edge.RequiresOutcomeBinding));
    }

    [Fact]
    public void RouteAndCycleLimitsRejectUnknownActionsBrokenChainsAndExcessiveBudgets()
    {
        var route = Route();
        Assert.Throws<InvalidDataException>(() => W101SharedOperations.RouteAndMenuVisit(route with { Id = "../escape" }));
        Assert.Throws<InvalidDataException>(() => W101SharedOperations.RouteAndMenuVisit(route with { Segments = [] }));
        Assert.Throws<InvalidDataException>(() => W101SharedOperations.RouteAndMenuVisit(route with
        { Segments = [route.Segments[0], route.Segments[1] with { FromLandmark = "wrong" }] }));
        Assert.Throws<InvalidDataException>(() => W101SharedOperations.RouteAndMenuVisit(route with
        { Segments = [route.Segments[0] with { MotionId = "teleport" }] }));
        Assert.Throws<InvalidDataException>(() => W101SharedOperations.RouteAndMenuVisit(route with
        { Segments = [route.Segments[0], route.Segments[1] with { ToLandmark = "start" }] }));
        Assert.Throws<InvalidDataException>(() => W101SharedOperations.RouteAndMenuVisit(route with
        { Segments = [route.Segments[0], route.Segments[1] with { Id = "first" }] }));
        Assert.Throws<InvalidDataException>(() => W101SharedOperations.RouteAndMenuVisit(route, pulseMs: 151));
        Assert.Throws<InvalidDataException>(() => W101SharedOperations.RouteAndMenuVisit(route, maxActions: 5));
        Assert.Throws<InvalidDataException>(() => W101SharedOperations.RouteAndMenuVisit(route, timeoutMs: double.NaN));
        Assert.Throws<InvalidDataException>(() => W101SharedOperations.RouteAndMenuVisit(route, transitionTimeoutMs: 60001));
        Assert.Throws<InvalidDataException>(() => W101SharedOperations.IdleCycle("home",
            new(PulseMs: 150, Mode: HomeIdleMode.Routine, RoutineActions: Enumerable.Repeat("turn-left", 5).ToArray())));
        Assert.Throws<InvalidDataException>(() => Plan().Bind("window", "geometry", "../visit"));
    }

    [Fact]
    public void CheckedInBundleMatchesTheAdapterAndReplaysWithoutGeneratingItsOwnDefinition()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "TestData", "Routines", "w101-operations");
        var bundlePath = Path.Combine(source, "bundle.json");
        var saved = ModuleCatalog.ReadBundle(File.ReadAllText(bundlePath));
        Assert.Equal(JsonSerializer.Serialize(Plan(), OperationModules.Json), JsonSerializer.Serialize(saved, OperationModules.Json));
        var directory = Path.Combine(Path.GetTempPath(), "w101-saved-module-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var output = Path.Combine(directory, "report.json");
            Assert.True(ModuleReplay.Run(bundlePath, Path.Combine(source, "session.json"), output));
            using var report = JsonDocument.Parse(File.ReadAllText(output));
            Assert.Equal(4, report.RootElement.GetProperty("completed_modules").GetInt32());
            Assert.Equal(7, report.RootElement.GetProperty("receipt_count").GetInt32());
            Assert.Equal(0, report.RootElement.GetProperty("desktop_dispatch_count").GetInt32());
            Assert.False(report.RootElement.GetProperty("live_qualified").GetBoolean());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void SyntheticSessionUsesTheExistingReplayAndClaimsNoLiveQualification()
    {
        var directory = Path.Combine(Path.GetTempPath(), "w101-operations-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var bundlePath = Path.Combine(directory, "bundle.json");
            File.WriteAllText(bundlePath, JsonSerializer.Serialize(Plan(), OperationModules.Json));
            var session = Path.Combine(AppContext.BaseDirectory, "TestData", "Routines", "w101-operations", "session.json");
            var first = Path.Combine(directory, "first.json"); var second = Path.Combine(directory, "second.json");
            Assert.True(ModuleReplay.Run(bundlePath, session, first));
            Assert.True(ModuleReplay.Run(bundlePath, session, second));
            Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
            using var report = JsonDocument.Parse(File.ReadAllText(first));
            Assert.Equal(7, report.RootElement.GetProperty("proposal_count").GetInt32());
            Assert.Equal(7, report.RootElement.GetProperty("receipt_count").GetInt32());
            Assert.Equal(4, report.RootElement.GetProperty("completed_modules").GetInt32());
            Assert.Equal(0, report.RootElement.GetProperty("desktop_dispatch_count").GetInt32());
            Assert.False(report.RootElement.GetProperty("live_qualified").GetBoolean());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
