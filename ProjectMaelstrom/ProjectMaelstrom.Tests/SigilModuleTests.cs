using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class SigilModuleTests
{
    private static readonly ModuleAction Interact = new("demo.interact", "keyboard-pulse", "Q", 60, "sigil.console");
    private static readonly RoutineBinding Binding = new("offline-target", "measured-geometry", "visit-1");
    private static OperationModuleSpec Spec(SigilConfiguration? configuration = null) =>
        OperationModules.Spec("console-entry", "sigil", "example-game",
            configuration ?? new("ConsoleReady", "ConsoleMenu", Interact), "location.console");
    private static RoutineRunner Runner(OperationModuleDefinition definition) =>
        new(definition.Graph, Binding, definition.Actions.Select(action => action.ActionId).ToHashSet(StringComparer.Ordinal));
    private static RoutineObservation Frame(double time, string scene, string? outcome = null) =>
        new(time, scene, Binding, Fresh: true, TargetReady: true, ContextVerified: true,
            InputOwned: true, ControlVerified: true, OutcomeForProposalId: outcome);

    [Fact]
    public void ConfigurationCompilesToSharedGraphWithoutInventingGameBindings()
    {
        var spec = Spec();
        var roundtrip = JsonSerializer.Deserialize<OperationModuleSpec>(JsonSerializer.Serialize(spec, OperationModules.Json), OperationModules.Json)!;
        var definition = SigilModule.Compile(roundtrip);
        Assert.Equal(spec.Id, definition.Graph.Id);
        Assert.Equal(spec.GameId, definition.Graph.GameId);
        Assert.Equal("location.console", Assert.Single(definition.Spec.RequiredFacts));
        Assert.Equal(Interact, Assert.Single(definition.Actions));
        Assert.Equal(1, definition.Graph.MaxActions);
        var edge = Assert.Single(definition.Graph.Nodes.SelectMany(node => node.Edges));
        Assert.True(edge.RequiresControl);
        Assert.True(edge.RequiresOutcomeBinding);
        Assert.Equal("ConsoleMenu", Assert.Single(edge.ExpectedScenes));
    }

    [Fact]
    public void OpeningRequiresControlReceiptAndNewerExactlyBoundMenuOutcome()
    {
        var runner = Runner(SigilModule.Compile(Spec()));
        Assert.False(runner.Observe(Frame(0, "ConsoleMenu")).Completed);
        Assert.Equal("action-control-unverified", runner.Observe(Frame(1, "ConsoleReady") with { ControlVerified = false }).Reason);
        var proposal = runner.Observe(Frame(2, "ConsoleReady"));
        Assert.True(proposal.IsProposal);
        Assert.Equal("dispatch-receipt-required", runner.Observe(Frame(3, "ConsoleMenu", proposal.ProposalId)).Reason);
        runner.RecordReceipt(new(proposal.ProposalId!, proposal.ActionId!, 4, true, true));
        Assert.False(runner.Observe(Frame(4, "ConsoleMenu", proposal.ProposalId)).Completed);
        Assert.Equal("outcome-proposal-binding-required", runner.Observe(Frame(5, "ConsoleMenu", "another-proposal")).Reason);
        Assert.False(runner.Observe(Frame(6, "ConsoleMenu", proposal.ProposalId) with { Fresh = false }).Completed);
        Assert.True(runner.Observe(Frame(7, "ConsoleMenu", proposal.ProposalId)).Completed);
        Assert.False(runner.Observe(Frame(8, "ConsoleReady")).IsProposal);
        Assert.True(runner.Status.Completed);
    }

    [Fact]
    public void LoadingAndUnknownPauseWithoutRetryingOrExtendingTheDeadline()
    {
        var runner = Runner(SigilModule.Compile(Spec(new("ConsoleReady", "ConsoleMenu", Interact, 1000))));
        var proposal = runner.Observe(Frame(0, "ConsoleReady"));
        runner.RecordReceipt(new(proposal.ProposalId!, proposal.ActionId!, 1, true, true));
        foreach (var frame in new[] { Frame(250, "Loading"), Frame(500, "Unknown"), Frame(750, "Loading") })
        {
            var paused = runner.Observe(frame);
            Assert.Equal("scene-pause", paused.Reason);
            Assert.False(paused.IsProposal);
        }
        Assert.True(runner.Observe(Frame(1000, "ConsoleMenu", proposal.ProposalId)).Stopped);
    }

    [Theory]
    [InlineData("takeover")]
    [InlineData("cancelled")]
    [InlineData("ownership")]
    [InlineData("target")]
    [InlineData("visit")]
    public void SafetyBoundaryStopsWithoutCompensatingInput(string change)
    {
        var runner = Runner(SigilModule.Compile(Spec()));
        var frame = Frame(0, "ConsoleReady");
        frame = change switch
        {
            "takeover" => frame with { UserActive = true },
            "cancelled" => frame with { Cancelled = true },
            "ownership" => frame with { InputOwned = false },
            "target" => frame with { TargetReady = false },
            _ => frame with { Binding = Binding with { ContextId = "other-visit" } }
        };
        Assert.True(runner.Observe(frame).Stopped);
        Assert.False(runner.Observe(Frame(1, "ConsoleReady")).IsProposal);
    }

    [Fact]
    public void InvalidScenesKindsAndActionBindingsCannotCompile()
    {
        foreach (var configuration in new[]
        {
            new SigilConfiguration("ConsoleReady", "ConsoleReady", Interact),
            new SigilConfiguration("Unknown", "ConsoleMenu", Interact),
            new SigilConfiguration("ConsoleReady", "Loading", Interact),
            new SigilConfiguration("", "ConsoleMenu", Interact),
            new SigilConfiguration("ConsoleReady", "ConsoleMenu", Interact with { ControlId = "" }),
            new SigilConfiguration("ConsoleReady", "ConsoleMenu", Interact with { Kind = "script" })
        }) Assert.Throws<InvalidDataException>(() => SigilModule.Compile(Spec(configuration)));
        Assert.Throws<InvalidDataException>(() => SigilModule.Compile(Spec() with { Kind = "menu" }));
        Assert.Throws<InvalidDataException>(() => SigilModule.Compile(Spec() with { SchemaVersion = 2 }));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(300001)]
    public void UnboundedOrNonpositiveWorkCannotCompile(double timeout) =>
        Assert.Throws<InvalidDataException>(() => SigilModule.Compile(Spec(new("ConsoleReady", "ConsoleMenu", Interact, timeout))));
}
