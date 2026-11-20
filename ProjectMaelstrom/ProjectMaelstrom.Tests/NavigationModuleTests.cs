using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class NavigationModuleTests
{
    private static readonly RoutineBinding Binding = new("offline-target", "test-geometry", "test-visit");
    private static NavigationConfiguration Configuration(string key = "I") => new(
        [new("first", "Start", "Junction", "FirstProgress", "move-a"),
            new("second", "Junction", "Destination", "SecondProgress", "move-b")],
        [new("move-a", "keyboard-pulse", key, 60, "route.corridor"),
            new("move-b", "keyboard-pulse", "J", 80, "route.turn")]);
    private static OperationModuleDefinition Definition(NavigationConfiguration? configuration = null, string game = "test-game") =>
        NavigationModule.Compile(OperationModules.Spec("test-navigation", "navigation", game,
            configuration ?? Configuration(), "route.known"));
    private static RoutineRunner Runner(OperationModuleDefinition definition) =>
        new(definition.Graph, Binding, definition.Actions.Select(a => a.ActionId).ToHashSet(StringComparer.Ordinal), "fixture");
    private static RoutineObservation Frame(double time, string scene, string? proposal = null) =>
        new(time, scene, Binding, Fresh: true, TargetReady: true, ContextVerified: true, InputOwned: true,
            ControlVerified: true, OutcomeForProposalId: proposal);
    private static void Receipt(RoutineRunner runner, RoutineDecision action, double time) =>
        runner.RecordReceipt(new(action.ProposalId!, action.ActionId!, time, true, true));

    private static NavigationConfiguration Steering() => new(
        [new("corridor", "Aligned", "Arrival", "Progress", "forward",
            [new("LeftOfPath", "right"), new("RightOfPath", "left")])],
        [new("forward", "keyboard-pulse", "W", 100, "path"),
            new("left", "keyboard-pulse", "A", 100, "path"),
            new("right", "keyboard-pulse", "D", 100, "path")]);

    [Fact]
    public void ObservedCorrectionsAdaptWithoutReplayingAFixedSequence()
    {
        var runner = Runner(Definition(Steering()));
        var right = runner.Observe(Frame(10, "LeftOfPath")); Assert.Equal("right", right.ActionId);
        Receipt(runner, right, 20);
        Assert.False(runner.Observe(Frame(25, "Arrival", right.ProposalId)).Completed);
        var forward = runner.Observe(Frame(30, "Aligned", right.ProposalId)); Assert.Equal("forward", forward.ActionId);
        Receipt(runner, forward, 40);
        var left = runner.Observe(Frame(50, "RightOfPath", forward.ProposalId)); Assert.Equal("left", left.ActionId);
        Receipt(runner, left, 60);
        Assert.False(runner.Observe(Frame(65, "Progress", forward.ProposalId)).IsProposal);
        var next = runner.Observe(Frame(70, "Progress", left.ProposalId)); Assert.Equal("forward", next.ActionId);
        Receipt(runner, next, 80);
        Assert.True(runner.Observe(Frame(90, "Arrival", next.ProposalId)).Completed);
    }

    [Fact]
    public void RepeatedCorrectionConsumesSharedBudgetAndCannotProveArrival()
    {
        var runner = Runner(Definition(Steering() with { MaxActions = 2 }));
        var first = runner.Observe(Frame(10, "RightOfPath")); Receipt(runner, first, 20);
        Assert.False(runner.Observe(Frame(25, "LeftOfPath", first.ProposalId) with { Fresh = false }).IsProposal);
        var second = runner.Observe(Frame(30, "LeftOfPath", first.ProposalId)); Receipt(runner, second, 40);
        var stop = runner.Observe(Frame(50, "Aligned", second.ProposalId));
        Assert.True(stop.Stopped); Assert.False(stop.Completed);
        Assert.Equal("action-budget-exceeded", stop.Reason);
    }

    [Theory]
    [InlineData("Loading")]
    [InlineData("Unknown")]
    [InlineData("Aligned")]
    [InlineData("Progress")]
    [InlineData("Arrival")]
    [InlineData("LeftOfPath")]
    public void AmbiguousCorrectionScenesAreRejected(string scene)
    {
        var config = Steering();
        var segment = config.Segments[0];
        Assert.Throws<InvalidDataException>(() => Definition(config with
        { Segments = [segment with { Corrections = [new("LeftOfPath", "right"), new(scene, "left")] }] }));
    }

    [Fact]
    public void CompilesDifferentGamesAndBindingsWithoutEmbeddingWizard101Knowledge()
    {
        var first = Definition(game: "maze-game"); var second = Definition(Configuration("UP"), "platform-game");
        Assert.Equal("maze-game", first.Graph.GameId); Assert.Equal("platform-game", second.Graph.GameId);
        Assert.Equal("I", first.Actions[0].Key); Assert.Equal("UP", second.Actions[0].Key);
        Assert.Equal(first.Spec.Id, first.Graph.Id);
        Assert.All(first.Graph.Nodes.SelectMany(node => node.Edges), edge =>
        { Assert.True(edge.RequiresControl); Assert.True(edge.RequiresOutcomeBinding); });
        Assert.Equal("Destination", first.Graph.Nodes[^1].CompleteScene);
        Assert.DoesNotContain(first.Actions, action => action.ActionId.Contains("idle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VerifiedProgressRepeatsOnlyThePendingSegmentAndArrivalSelectsTheNext()
    {
        var runner = Runner(Definition());
        var first = runner.Observe(Frame(10, "Start")); Assert.Equal("move-a", first.ActionId);
        Assert.False(runner.Observe(Frame(20, "FirstProgress", first.ProposalId)).IsProposal);
        Receipt(runner, first, 30);
        var repeated = runner.Observe(Frame(40, "FirstProgress", first.ProposalId)); Assert.Equal("move-a", repeated.ActionId);
        Receipt(runner, repeated, 50);
        Assert.False(runner.Observe(Frame(60, "FirstProgress", first.ProposalId)).IsProposal);
        Assert.False(runner.Observe(Frame(70, "Junction")).IsProposal);
        var turn = runner.Observe(Frame(80, "Junction", repeated.ProposalId)); Assert.Equal("move-b", turn.ActionId);
        Receipt(runner, turn, 90);
        Assert.False(runner.Observe(Frame(100, "Destination", repeated.ProposalId)).Completed);
        Assert.True(runner.Observe(Frame(110, "Destination", turn.ProposalId)).Completed);
        Assert.False(runner.Observe(Frame(120, "Start")).IsProposal);
    }

    [Fact]
    public void WrongLandmarksStaleFramesAndTimeAloneCannotCompleteNavigation()
    {
        var runner = Runner(Definition());
        Assert.False(runner.Observe(Frame(10, "Destination")).Completed);
        var first = runner.Observe(Frame(20, "Start")); Receipt(runner, first, 30);
        Assert.False(runner.Observe(Frame(40, "Destination", first.ProposalId)).Completed);
        Assert.False(runner.Observe(Frame(50, "Junction", first.ProposalId) with { Fresh = false }).IsProposal);
        Assert.False(runner.Observe(Frame(60, "Unknown")).IsProposal);
        var end = runner.Observe(Frame(10020, "Loading"));
        Assert.True(end.Stopped); Assert.Equal("transition-deadline-exceeded", end.Reason);
    }

    [Fact]
    public void ControlChecksLoadingAndTakeoverUseTheSharedRunner()
    {
        var runner = Runner(Definition());
        Assert.Equal("scene-pause", runner.Observe(Frame(10, "Loading")).Reason);
        Assert.False(runner.Observe(Frame(20, "Start") with { ControlVerified = false }).IsProposal);
        var action = runner.Observe(Frame(30, "Start")); Receipt(runner, action, 40);
        Assert.Equal("scene-pause", runner.Observe(Frame(50, "Loading")).Reason);
        var stop = runner.Observe(Frame(60, "Junction", action.ProposalId) with { UserActive = true });
        Assert.True(stop.Stopped);
        Assert.False(runner.Observe(Frame(70, "Junction", action.ProposalId)).IsProposal);
    }

    [Fact]
    public void ActionBudgetBoundsRepeatedProgressWithoutInventingArrival()
    {
        var runner = Runner(Definition(Configuration() with { MaxActions = 2 }));
        var first = runner.Observe(Frame(10, "Start")); Receipt(runner, first, 20);
        var second = runner.Observe(Frame(30, "FirstProgress", first.ProposalId)); Receipt(runner, second, 40);
        var end = runner.Observe(Frame(50, "FirstProgress", second.ProposalId));
        Assert.True(end.Stopped); Assert.Equal("action-budget-exceeded", end.Reason); Assert.False(end.Completed);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("oversized")]
    [InlineData("broken-chain")]
    [InlineData("repeated-landmark")]
    [InlineData("repeated-progress")]
    [InlineData("progress-is-landmark")]
    [InlineData("pause-landmark")]
    [InlineData("unregistered")]
    [InlineData("unused-action")]
    [InlineData("bad-budget")]
    [InlineData("bad-time")]
    public void InvalidOrAmbiguousConfigurationsAreRejected(string problem)
    {
        var config = Configuration(); var first = config.Segments[0]; var second = config.Segments[1];
        config = problem switch
        {
            "empty" => config with { Segments = [] },
            "oversized" => config with { Segments = Enumerable.Repeat(first, 17).ToArray() },
            "broken-chain" => config with { Segments = [first, second with { FromScene = "Wrong" }] },
            "repeated-landmark" => config with { Segments = [first, second with { ToScene = "Start" }] },
            "repeated-progress" => config with { Segments = [first, second with { ProgressScene = first.ProgressScene }] },
            "progress-is-landmark" => config with { Segments = [first with { ProgressScene = "Junction" }, second] },
            "pause-landmark" => config with { Segments = [first with { FromScene = "Loading" }, second] },
            "unregistered" => config with { Segments = [first with { ActionId = "unregistered" }, second] },
            "unused-action" => config with { Actions = [.. config.Actions, new("unused", "keyboard-pulse", "K", 40, "unused.control")] },
            "bad-budget" => config with { MaxActions = 1 },
            _ => config with { TransitionTimeoutMs = double.PositiveInfinity }
        };
        Assert.Throws<InvalidDataException>(() => Definition(config));
    }

    [Fact]
    public void WrongModuleKindAndUnknownConfigurationFieldsAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => NavigationModule.Compile(
            OperationModules.Spec("wrong", "menu", "test-game", Configuration())));
        var original = OperationModules.Spec("wrong", "navigation", "test-game", Configuration());
        using var json = System.Text.Json.JsonDocument.Parse(original.Configuration.GetRawText().TrimEnd('}') + ",\"script\":\"run\"}");
        Assert.Throws<System.Text.Json.JsonException>(() => NavigationModule.Compile(original with { Configuration = json.RootElement }));
    }
}
