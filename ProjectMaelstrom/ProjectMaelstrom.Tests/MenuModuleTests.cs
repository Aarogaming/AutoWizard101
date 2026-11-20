using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class MenuModuleTests
{
    private static readonly ModuleAction Open = new("demo.open", "keyboard-pulse", "M", 50, "menu.opener");
    private static readonly ModuleAction Choose = new("demo.choose", "verified-control-click", null, 50, "menu.choice");
    private static readonly RoutineBinding Binding = new("offline-target", "measured-geometry", "visit-1");
    private static MenuConfiguration Configuration() => new([
        new("open", "Outside", "Menu", Open), new("choose", "Menu", "Complete", Choose)]);
    private static OperationModuleSpec Spec(MenuConfiguration? configuration = null) =>
        OperationModules.Spec("menu-path", "menu", "example-game", configuration ?? Configuration(), "context.allowed");
    private static RoutineRunner Runner(MenuConfiguration? configuration = null)
    {
        var definition = MenuModule.Compile(Spec(configuration));
        return new(definition.Graph, Binding, definition.Actions.Select(action => action.ActionId).ToHashSet(StringComparer.Ordinal));
    }
    private static RoutineObservation Frame(double time, string scene, string? outcome = null) =>
        new(time, scene, Binding, Fresh: true, TargetReady: true, ContextVerified: true,
            InputOwned: true, ControlVerified: true, OutcomeForProposalId: outcome);
    private static void Ack(RoutineRunner runner, RoutineDecision proposal, double time) =>
        runner.RecordReceipt(new(proposal.ProposalId!, proposal.ActionId!, time, true, true));

    [Fact]
    public void CompiledMenuUsesNamedControlsBoundOutcomesAndFiniteActionBudget()
    {
        var spec = Spec(); var definition = MenuModule.Compile(spec);
        Assert.Equal(spec.Id, definition.Graph.Id);
        Assert.Equal(spec.GameId, definition.Graph.GameId);
        Assert.Equal("context.allowed", Assert.Single(definition.Spec.RequiredFacts));
        Assert.Equal(2, definition.Graph.MaxActions);
        Assert.All(definition.Graph.Nodes.SelectMany(node => node.Edges), edge =>
        {
            Assert.True(edge.RequiresControl); Assert.True(edge.RequiresOutcomeBinding);
            Assert.Contains(definition.Actions, action => action.ActionId == edge.ActionId);
        });
    }

    [Fact]
    public void DefaultEntryCannotSkipStepsOrAdoptAnUnrelatedCompleteScene()
    {
        var runner = Runner();
        Assert.False(runner.Observe(Frame(0, "Menu")).IsProposal);
        Assert.False(runner.Observe(Frame(1, "Complete")).Completed);
        var first = runner.Observe(Frame(2, "Outside")); Ack(runner, first, 3);
        Assert.Equal("outcome-proposal-binding-required", runner.Observe(Frame(4, "Menu")).Reason);
        var second = runner.Observe(Frame(5, "Menu", first.ProposalId));
        Assert.Equal(Choose.ActionId, second.ActionId); Ack(runner, second, 6);
        Assert.False(runner.Observe(Frame(7, "Complete", first.ProposalId)).Completed);
        Assert.True(runner.Observe(Frame(8, "Complete", second.ProposalId)).Completed);
    }

    [Fact]
    public void EachNextControlNeedsCurrentEvidenceAfterItsPrecedingOutcome()
    {
        var runner = Runner(); var first = runner.Observe(Frame(0, "Outside")); Ack(runner, first, 1);
        var waiting = runner.Observe(Frame(2, "Menu", first.ProposalId) with { ControlVerified = false });
        Assert.Equal("action-control-unverified", waiting.Reason);
        var second = runner.Observe(Frame(3, "Menu"));
        Assert.Equal(Choose.ActionId, second.ActionId);
        Assert.NotEqual(first.ProposalId, second.ProposalId);
    }

    [Fact]
    public void IntermediateAndAlreadyCompleteEntryAreSeparateExplicitOptions()
    {
        var runner = Runner(Configuration() with { AllowIntermediateEntry = true });
        var proposal = runner.Observe(Frame(0, "Menu"));
        Assert.Equal(Choose.ActionId, proposal.ActionId); Ack(runner, proposal, 1);
        Assert.True(runner.Observe(Frame(2, "Complete", proposal.ProposalId)).Completed);
        Assert.False(Runner(Configuration() with { AllowIntermediateEntry = true }).Observe(Frame(0, "Complete")).Completed);
        Assert.True(Runner(Configuration() with { AllowAlreadyComplete = true }).Observe(Frame(0, "Complete")).Completed);
        Assert.False(Runner(Configuration() with { AllowAlreadyComplete = true }).Observe(Frame(0, "Menu")).IsProposal);
    }

    [Fact]
    public void SameSceneTransitionsStillNeedNewExactlyBoundOutcomes()
    {
        var configuration = new MenuConfiguration([
            new("first", "Panel", "Panel", Open), new("second", "Panel", "Done", Choose)]);
        var runner = Runner(configuration); var first = runner.Observe(Frame(0, "Panel")); Ack(runner, first, 1);
        Assert.False(runner.Observe(Frame(2, "Panel")).IsProposal);
        var second = runner.Observe(Frame(3, "Panel", first.ProposalId));
        Assert.Equal(Choose.ActionId, second.ActionId);
        Assert.Throws<InvalidDataException>(() => MenuModule.Compile(Spec(configuration with { AllowIntermediateEntry = true })));
    }

    [Fact]
    public void ReusedActionIdsMustRetainExactlyTheSameBinding()
    {
        var configuration = new MenuConfiguration([
            new("first", "A", "B", Open), new("second", "B", "C", Open)]);
        Assert.Single(MenuModule.Compile(Spec(configuration)).Actions);
        configuration.Steps[1] = configuration.Steps[1] with { Action = Open with { Key = "Q" } };
        Assert.Throws<InvalidDataException>(() => MenuModule.Compile(Spec(configuration)));
    }

    [Fact]
    public void LoadingCannotRestartActionsOrExtendTransitionAndOverallBudgets()
    {
        var configuration = Configuration() with { TimeoutMs = 1000, TransitionTimeoutMs = 100 };
        var runner = Runner(configuration); var proposal = runner.Observe(Frame(0, "Outside")); Ack(runner, proposal, 1);
        Assert.Equal("scene-pause", runner.Observe(Frame(50, "Loading")).Reason);
        Assert.Equal("transition-deadline-exceeded", runner.Observe(Frame(100, "Unknown")).Reason);
        runner = Runner(configuration);
        Assert.False(runner.Observe(Frame(0, "Unknown")).IsProposal);
        Assert.Equal("routine-deadline-exceeded", runner.Observe(Frame(1000, "Unknown")).Reason);
    }

    [Fact]
    public void UserTakeoverStopsThePathWithoutProceedingToACompleteScene()
    {
        var runner = Runner(); var first = runner.Observe(Frame(0, "Outside")); Ack(runner, first, 1);
        Assert.True(runner.Observe(Frame(2, "Menu", first.ProposalId) with { UserActive = true }).Stopped);
        Assert.False(runner.Observe(Frame(3, "Complete", first.ProposalId)).Completed);
        Assert.False(runner.Observe(Frame(4, "Outside")).IsProposal);
    }

    [Fact]
    public void BrokenPathsAmbiguousCompletionReservedScenesAndDuplicateStepNamesAreRejected()
    {
        foreach (var configuration in new[]
        {
            new MenuConfiguration([new("a", "A", "B", Open), new("b", "X", "C", Choose)]),
            new MenuConfiguration([new("a", "A", "B", Open), new("a", "B", "C", Choose)]),
            new MenuConfiguration([new("a", "A", "B", Open), new("b", "B", "A", Choose)], AllowAlreadyComplete: true),
            new MenuConfiguration([new("a", "Unknown", "B", Open)]),
            new MenuConfiguration([new("a", "A", "Loading", Open)]),
            new MenuConfiguration([]),
            new MenuConfiguration(Enumerable.Range(0, 17).Select(i => new MenuStep("s" + i, "State" + i,
                "State" + (i + 1), Open)).ToArray())
        }) Assert.Throws<InvalidDataException>(() => MenuModule.Compile(Spec(configuration)));
        Assert.Throws<InvalidDataException>(() => MenuModule.Compile(Spec() with { Kind = "sigil" }));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(100, 101)]
    [InlineData(300001, 100)]
    public void InvalidTimeBudgetsCannotCompile(double overall, double transition) =>
        Assert.Throws<InvalidDataException>(() => MenuModule.Compile(Spec(Configuration() with
        { TimeoutMs = overall, TransitionTimeoutMs = transition })));
}
