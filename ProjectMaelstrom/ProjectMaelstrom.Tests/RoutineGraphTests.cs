using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class RoutineGraphTests
{
    private static readonly RoutineBinding Binding = new("window-1", "1280x720", "visit-1");
    private static RoutineRunner Runner(RoutineGraph graph) => new(graph, Binding,
        graph.Nodes.SelectMany(n => n.Edges).Select(e => e.ActionId).ToHashSet(StringComparer.Ordinal));
    private static RoutineObservation Frame(double time, string scene) => new(time, scene, Binding,
        Fresh: true, TargetReady: true, ContextVerified: true, InputOwned: true, ControlVerified: true);
    private static void Ack(RoutineRunner runner, RoutineDecision plan, double time) =>
        runner.RecordReceipt(new(plan.ProposalId!, plan.ActionId!, time, true, true));

    [Theory]
    [InlineData(1)]
    [InlineData(27)]
    [InlineData(941)]
    public void WholeVisitReplaysVaryingLoadingAndUnknownGapsWithoutAnyDesktop(int seed)
    {
        var graph = RoutineGraphs.BuiltIn("pet-dance-visit-draft");
        var runner = Runner(graph); var random = new Random(seed); double time = 0;
        var scenes = new[] { "DanceSigil", "InstructionsUnselected", "InstructionsReady", "Watch",
            "GameResult", "SnackOffer", "OutsideAfterGame", "MenuObserved" };
        var count = 0;
        foreach (var scene in scenes)
        {
            var proposal = runner.Observe(Frame(++time, scene));
            Assert.True(proposal.IsProposal); Assert.False(proposal.Completed); count++;
            // Repeated observation and future states cannot substitute for a receipt.
            Assert.False(runner.Observe(Frame(time, scene)).IsProposal);
            Assert.False(runner.Observe(Frame(++time, "NativeIdle")).Completed);
            Ack(runner, proposal, ++time);
            for (var i = 0; i < random.Next(1, 8); i++)
            {
                time += random.Next(20, 900);
                var waiting = runner.Observe(Frame(time, i % 2 == 0 ? "Loading" : "Unknown"));
                Assert.False(waiting.IsProposal); Assert.False(waiting.Stopped);
            }
        }
        Assert.Equal(8, count);
        Assert.True(runner.Observe(Frame(++time, "NativeIdle")).Completed);
    }

    [Fact]
    public void RepeatedActionsUseDifferentReceiptsAndReactToFreshThreatOrder()
    {
        var runner = Runner(RoutineGraphs.BuiltIn("dodge-movement-draft"));
        var ids = new HashSet<string>(); double time = 0;
        foreach (var scene in new[] { "threat-left", "threat-left", "threat-right", "threat-left", "threat-right" })
        {
            var proposal = runner.Observe(Frame(++time, scene));
            Assert.Equal(scene == "threat-left" ? "move-right" : "move-left", proposal.ActionId);
            Assert.True(ids.Add(proposal.ProposalId!));
            Ack(runner, proposal, ++time);
            Assert.False(runner.Observe(Frame(++time, "movement-observed")).IsProposal);
        }
        Assert.True(runner.Observe(Frame(++time, "game-over")).Completed);
    }

    [Fact]
    public void SentInputIsNotAnOutcomeAndPreReceiptFramesCannotAdvance()
    {
        var runner = Runner(RoutineGraphs.BuiltIn("pet-dance-postgame"));
        var plan = runner.Observe(Frame(10, "GameResult"));
        Assert.Equal("dispatch-receipt-required", runner.Observe(Frame(20, "SnackOffer")).Reason);
        Ack(runner, plan, 100);
        Assert.False(runner.Observe(Frame(50, "SnackOffer")).IsProposal);
        Assert.False(runner.Observe(Frame(100, "SnackOffer")).IsProposal);
        Assert.True(runner.Observe(Frame(101, "SnackOffer")).IsProposal);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void RejectedOrUnreleasedReceiptLatchesFailure(bool accepted, bool released)
    {
        var runner = Runner(RoutineGraphs.BuiltIn("pet-dance-postgame"));
        var plan = runner.Observe(Frame(1, "GameResult"));
        runner.RecordReceipt(new(plan.ProposalId!, plan.ActionId!, 2, accepted, released));
        var failure = runner.Observe(Frame(3, "SnackOffer"));
        Assert.True(failure.Stopped); Assert.False(failure.IsProposal);
    }

    [Fact]
    public void ReusedOrWrongProposalIdCannotAcknowledgeAnotherAction()
    {
        var runner = Runner(RoutineGraphs.BuiltIn("dodge-movement-draft"));
        var first = runner.Observe(Frame(1, "threat-left")); Ack(runner, first, 2);
        runner.Observe(Frame(3, "movement-observed"));
        var second = runner.Observe(Frame(4, "threat-left"));
        Assert.NotEqual(first.ProposalId, second.ProposalId);
        Ack(runner, first, 5);
        Assert.True(runner.Observe(Frame(6, "movement-observed")).Stopped);
    }

    [Fact]
    public void MissingEvidenceWaitsWhileLostOwnershipAndUserActivityStopWithoutCleanup()
    {
        foreach (var transform in new Func<RoutineObservation, RoutineObservation>[]
        {
            f => f with { UserActive = true }, f => f with { Cancelled = true },
            f => f with { InputOwned = false }, f => f with { TargetReady = false },
            f => f with { Binding = Binding with { ContextId = "other-visit" } }
        })
        {
            var runner = Runner(RoutineGraphs.BuiltIn("pet-dance-postgame"));
            Assert.True(runner.Observe(transform(Frame(1, "GameResult"))).Stopped);
            Assert.False(runner.Observe(Frame(2, "GameResult")).IsProposal);
        }
        foreach (var transform in new Func<RoutineObservation, RoutineObservation>[]
        {
            f => f with { Fresh = false }, f => f with { ContextVerified = false },
            f => f with { ControlVerified = false }
        })
        {
            var runner = Runner(RoutineGraphs.BuiltIn("pet-dance-postgame"));
            Assert.False(runner.Observe(transform(Frame(1, "GameResult"))).IsProposal);
            Assert.True(runner.Observe(Frame(2, "GameResult")).IsProposal);
        }
    }

    [Fact]
    public void LoadingAndUnknownDoNotExtendBudgetsOrEnableRetries()
    {
        var graph = RoutineGraphs.BuiltIn("dodge-movement-draft");
        var runner = Runner(graph);
        var plan = runner.Observe(Frame(0, "threat-left")); Ack(runner, plan, 1);
        Assert.Equal("transition-deadline-exceeded", runner.Observe(Frame(2000, "Loading")).Reason);
        runner = Runner(graph with { MaxActions = 1 });
        plan = runner.Observe(Frame(0, "threat-left")); Ack(runner, plan, 1);
        runner.Observe(Frame(2, "movement-observed"));
        Assert.Equal("action-budget-exceeded", runner.Observe(Frame(3, "threat-right")).Reason);
        runner = Runner(graph);
        runner.Observe(Frame(0, "Unknown"));
        Assert.Equal("routine-deadline-exceeded", runner.Observe(Frame(120000, "Unknown")).Reason);
    }

    [Fact]
    public void DataCannotAddScriptsUnregisteredActionsAmbiguityOrUnboundedWork()
    {
        var graph = RoutineGraphs.BuiltIn("pet-dance-postgame");
        Assert.Throws<InvalidDataException>(() => new RoutineRunner(graph, Binding, new HashSet<string>()));
        var text = JsonSerializer.Serialize(graph, RoutineGraphs.Json);
        Assert.Throws<JsonException>(() => RoutineGraphs.Read(text.Replace("\"schema_version\": 1", "\"schema_version\": 1, \"script\": \"anything\"")));
        Assert.Throws<InvalidDataException>(() => RoutineGraphs.Read(text.Replace("\"schema_version\": 1", "\"schema_version\": 1, \"schema_version\": 1")));
        Assert.Throws<InvalidDataException>(() => Runner(graph with { TimeoutMs = double.PositiveInfinity }));
        var first = graph.Nodes[0];
        var ambiguous = first with { Edges = [first.Edges[0], first.Edges[0] with { Id = "another" }] };
        Assert.Throws<InvalidDataException>(() => Runner(graph with { Nodes = [ambiguous, .. graph.Nodes.Skip(1)] }));
    }

    [Fact]
    public void ReceiptFromAnotherRunCannotAcknowledgeTheCurrentVisit()
    {
        var graph = RoutineGraphs.BuiltIn("pet-dance-postgame");
        var firstRun = Runner(graph); var secondRun = Runner(graph);
        var old = firstRun.Observe(Frame(1, "GameResult"));
        var current = secondRun.Observe(Frame(1, "GameResult"));
        Assert.NotEqual(old.ProposalId, current.ProposalId);
        Ack(secondRun, old, 2);
        Assert.True(secondRun.Observe(Frame(3, "SnackOffer")).Stopped);
    }

    [Fact]
    public void GraphArraysCannotChangeAfterRunnerConstruction()
    {
        var graph = RoutineGraphs.BuiltIn("pet-dance-postgame");
        var runner = Runner(graph);
        graph.Nodes[0].Edges[0] = graph.Nodes[0].Edges[0] with { ActionId = "unexpected" };
        Assert.Equal("pet-dance.next", runner.Observe(Frame(1, "GameResult")).ActionId);
    }

    [Fact]
    public void BoundOutcomesCannotBeReusedByHoldingProgressOrAdvancingOnlyTime()
    {
        var graph = new RoutineGraph(1, "progress-loop", "example", "start", 10000, 4, ["Unknown", "Loading"],
        [
            new("start", [new("begin", "Ready", "move", ["Progress", "Done"], "moving", 2000,
                RequiresOutcomeBinding: true)]),
            new("moving", [new("continue", "Progress", "move", ["Progress", "Done"], "moving", 2000,
                RequiresOutcomeBinding: true)], "Done")
        ]);
        var runner = Runner(graph);
        var first = runner.Observe(Frame(1, "Ready")); Ack(runner, first, 2);
        Assert.Equal("outcome-proposal-binding-required", runner.Observe(Frame(3, "Progress")).Reason);
        Assert.Equal("outcome-proposal-binding-required", runner.Observe(Frame(4, "Progress") with
        { OutcomeForProposalId = "another-run" }).Reason);
        var second = runner.Observe(Frame(5, "Progress") with { OutcomeForProposalId = first.ProposalId });
        Assert.True(second.IsProposal); Assert.NotEqual(first.ProposalId, second.ProposalId);
        Ack(runner, second, 6);
        foreach (var time in new[] { 7, 8, 9 })
            Assert.Equal("outcome-proposal-binding-required", runner.Observe(Frame(time, "Progress") with
            { OutcomeForProposalId = first.ProposalId }).Reason);
        Assert.False(runner.Observe(Frame(10, "Done") with { OutcomeForProposalId = first.ProposalId }).Completed);
        Assert.True(runner.Observe(Frame(11, "Done") with { OutcomeForProposalId = second.ProposalId }).Completed);
        Assert.True(RoutineGraphs.Read(JsonSerializer.Serialize(graph, RoutineGraphs.Json)).Nodes[0].Edges[0].RequiresOutcomeBinding);
    }

    [Fact]
    public void CombatTurnDecisionRoutineLoadsAndAdvancesCorrectly()
    {
        var graph = RoutineGraphs.BuiltIn("combat-turn-decision");
        Assert.Equal("combat-turn-decision", graph.Id);
        Assert.Equal("wizard101", graph.GameId);
        Assert.Equal(6, graph.Nodes.Length);

        var runner = Runner(graph);
        double time = 100.0;

        // 1. Enter combat -> detect hand
        var p1 = runner.Observe(Frame(++time, "CombatEntered"));
        Assert.True(p1.IsProposal);
        Assert.Equal("combat.detect-hand", p1.ActionId);
        Ack(runner, p1, ++time);

        // 2. Hand observed -> evaluate turn
        var p2 = runner.Observe(Frame(++time, "HandObserved"));
        Assert.True(p2.IsProposal);
        Assert.Equal("combat.evaluate-turn", p2.ActionId);
        Ack(runner, p2, ++time);

        // 3. Plan ready -> dispatch card
        var p3 = runner.Observe(Frame(++time, "TurnPlanReady"));
        Assert.True(p3.IsProposal);
        Assert.Equal("combat.dispatch-card", p3.ActionId);
        Ack(runner, p3, ++time);

        // 4. Action dispatched -> observe round
        var p4 = runner.Observe(Frame(++time, "ActionDispatched"));
        Assert.True(p4.IsProposal);
        Assert.Equal("combat.observe-round", p4.ActionId);
        Ack(runner, p4, ++time);

        // 5. Round completes with victory -> collect rewards
        var p5 = runner.Observe(Frame(++time, "CombatVictory"));
        Assert.True(p5.IsProposal);
        Assert.Equal("combat.collect-rewards", p5.ActionId);
        Ack(runner, p5, ++time);

        // 6. Combat finished -> Completed
        var p6 = runner.Observe(Frame(++time, "CombatFinished"));
        Assert.True(p6.Completed);
    }
}
