using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class ModuleRunnerTests
{
    // An independent game profile, deliberately using different scenes, keys and
    // native-idle label. No W101 compiler or constants are used by this fixture.
    internal static OperationModuleBundle LabBundle() => new(1, "lab-visit", "offline-lab", "lab.station", 5000, 5,
    [
        OperationModules.Spec("approach", "navigation", "offline-lab", new NavigationConfiguration(
            [new("walk", "Spawn", "Pad", "TowardPad", "lab.walk")],
            [new("lab.walk", "keyboard-pulse", "UP", 50, "walk-area")], 3, 5000, 1000), "lab.context"),
        OperationModules.Spec("interact", "sigil", "offline-lab", new SigilConfiguration("Pad", "Station",
            new("lab.interact", "keyboard-pulse", "E", 40, "station-prompt"), 1000), "lab.context"),
        OperationModules.Spec("confirm", "menu", "offline-lab", new MenuConfiguration(
            [new("confirm", "Station", "Ready", new("lab.confirm", "keyboard-pulse", "ENTER", 40, "confirm-button"))],
            TimeoutMs: 1000, TransitionTimeoutMs: 1000), "lab.context"),
        OperationModules.Spec("rest", "auto-idle", "offline-lab", new AutoIdleConfiguration("native", [],
            NativeMenu: new MenuConfiguration([
                new("pause", "Ready", "PauseMenu", new("lab.pause", "keyboard-pulse", "P", 40, "pause-command")),
                new("rest", "PauseMenu", "Resting", new("lab.rest", "verified-control-click", null, 40, "rest-button"))
            ], TimeoutMs: 2000, TransitionTimeoutMs: 1000), TimeoutMs: 2000, NativeIdleScene: "Resting"), "lab.context")
    ]);

    internal static RoutineBinding Binding(OperationModuleBundle bundle) => bundle.Bind("lab-window", "1600x900-grid12", "attempt1");
    internal static readonly string[] Controls = ["walk-area", "station-prompt", "confirm-button", "pause-command", "rest-button"];
    internal static ModuleObservation Frame(RoutineBinding binding, double time, string scene, string? outcome = null) =>
        new(new(time, scene, binding, Fresh: true, TargetReady: true, ContextVerified: true,
            InputOwned: true, ControlVerified: true, OutcomeForProposalId: outcome), ["lab.context"], Controls);

    [Fact]
    public void IndependentGameCompletesFourModulesWithExactOutcomesAndNoGameSpecificCode()
    {
        var bundle = LabBundle(); var binding = Binding(bundle); var runner = new ModuleRunner(bundle, binding, "test");
        var decision = runner.Observe(Frame(binding, 0, "Spawn"));
        Assert.Equal("UP", decision.Action!.Key);
        string[] scenes = ["Pad", "Station", "Ready", "PauseMenu", "Resting"];
        for (var i = 0; i < scenes.Length; i++)
        {
            Assert.True(decision.IsProposal);
            runner.RecordReceipt(new(decision.ProposalId!, decision.ActionId!, i * 100 + 1, true, true));
            decision = runner.Observe(Frame(binding, (i + 1) * 100, scenes[i], decision.ProposalId));
        }
        Assert.True(decision.Completed); Assert.False(decision.IsProposal);
        Assert.Equal(4, runner.CompletedModules); Assert.Equal(5, runner.ProposedActions);
        Assert.True(runner.Observe(Frame(binding, 1000, "Unknown") with
        { Observation = Frame(binding, 1000, "Unknown").Observation with { UserActive = true } }).Completed);
    }

    [Fact]
    public void HandoffChecksTheNextNamedControlRatherThanReusingThePreviousControl()
    {
        var bundle = LabBundle(); var binding = Binding(bundle); var runner = new ModuleRunner(bundle, binding, "test");
        var first = runner.Observe(Frame(binding, 0, "Spawn"));
        runner.RecordReceipt(new(first.ProposalId!, first.ActionId!, 1, true, true));
        var blocked = runner.Observe(Frame(binding, 100, "Pad", first.ProposalId) with { VerifiedControls = ["walk-area"] });
        Assert.False(blocked.IsProposal); Assert.Equal("interact", blocked.ModuleId);
        Assert.Equal("action-control-unverified", blocked.Reason); Assert.Equal(1, runner.CompletedModules);
        var next = runner.Observe(Frame(binding, 101, "Pad") with { VerifiedControls = ["station-prompt"] });
        Assert.Equal("lab.interact", next.ActionId);
        Assert.Equal("test.m1/interact/1", next.ProposalId);
    }

    [Fact]
    public void MultiStepMenuRechecksControlAfterConsumingOutcomeOnSameFrame()
    {
        var source = LabBundle(); var bundle = source with { Modules = [source.Modules[^1]] };
        var binding = Binding(bundle); var runner = new ModuleRunner(bundle, binding, "test");
        var first = runner.Observe(Frame(binding, 0, "Ready"));
        runner.RecordReceipt(new(first.ProposalId!, first.ActionId!, 1, true, true));
        Assert.False(runner.Observe(Frame(binding, 2, "PauseMenu", first.ProposalId) with { VerifiedControls = ["pause-command"] }).IsProposal);
        var next = runner.Observe(Frame(binding, 3, "PauseMenu") with { VerifiedControls = ["rest-button"] });
        Assert.Equal("lab.rest", next.ActionId);
    }

    [Fact]
    public void RepeatedProgressNeedsNewReceiptAndExactProposalBoundOutcome()
    {
        var bundle = LabBundle(); var binding = Binding(bundle); var runner = new ModuleRunner(bundle, binding, "test");
        var first = runner.Observe(Frame(binding, 0, "Spawn"));
        Assert.Equal("dispatch-receipt-required", runner.Observe(Frame(binding, 1, "TowardPad", first.ProposalId)).Reason);
        runner.RecordReceipt(new(first.ProposalId!, first.ActionId!, 2, true, true));
        Assert.Equal("outcome-proposal-binding-required", runner.Observe(Frame(binding, 3, "TowardPad", "other")).Reason);
        var next = runner.Observe(Frame(binding, 4, "TowardPad", first.ProposalId));
        Assert.True(next.IsProposal); Assert.NotEqual(first.ProposalId, next.ProposalId);
        runner.RecordReceipt(new(next.ProposalId!, next.ActionId!, 5, true, true));
        Assert.Equal("outcome-proposal-binding-required", runner.Observe(Frame(binding, 6, "TowardPad", first.ProposalId)).Reason);
    }

    [Fact]
    public void MissingFactsBlockCompletionAndActionsAndCannotResetDeadline()
    {
        var bundle = LabBundle(); var binding = Binding(bundle); var runner = new ModuleRunner(bundle, binding, "test");
        Assert.Equal("observation-not-ready", runner.Observe(Frame(binding, 0, "Spawn") with { VerifiedFacts = [] }).Reason);
        Assert.False(runner.Observe(Frame(binding, 4999, "Loading") with { VerifiedFacts = [] }).IsProposal);
        Assert.Equal("bundle-deadline-exceeded", runner.Observe(Frame(binding, 5000, "Spawn")).Reason);
    }

    [Theory]
    [InlineData("takeover")]
    [InlineData("cancel")]
    [InlineData("target")]
    [InlineData("ownership")]
    public void RunStopsOnLostAuthorityAndNeverRestarts(string fault)
    {
        var bundle = LabBundle(); var binding = Binding(bundle); var runner = new ModuleRunner(bundle, binding, "test");
        var frame = Frame(binding, 0, "Spawn");
        frame = frame with
        {
            Observation = frame.Observation with
            {
                UserActive = fault == "takeover",
                Cancelled = fault == "cancel",
                Binding = fault == "target" ? binding with { GeometryId = "changed" } : binding,
                InputOwned = fault != "ownership"
            }
        };
        Assert.True(runner.Observe(frame).Stopped);
        Assert.True(runner.Observe(Frame(binding, 1, "Spawn")).Stopped); Assert.Equal(0, runner.ProposedActions);
    }

    [Fact]
    public void PreviousModuleReceiptCannotAcknowledgeTheNextModuleAction()
    {
        var bundle = LabBundle(); var binding = Binding(bundle); var runner = new ModuleRunner(bundle, binding, "test");
        var first = runner.Observe(Frame(binding, 0, "Spawn"));
        var receipt = new RoutineReceipt(first.ProposalId!, first.ActionId!, 1, true, true);
        runner.RecordReceipt(receipt);
        Assert.True(runner.Observe(Frame(binding, 2, "Pad", first.ProposalId)).IsProposal);
        runner.RecordReceipt(receipt with { TimeMs = 3 });
        Assert.True(runner.Status.Stopped); Assert.Equal("dispatch-unconfirmed", runner.Status.Reason);
    }

    [Fact]
    public void BundleActionLimitIsSharedAcrossModules()
    {
        var bundle = LabBundle() with { MaxActions = 1 }; var binding = Binding(bundle);
        var runner = new ModuleRunner(bundle, binding, "test"); var first = runner.Observe(Frame(binding, 0, "Spawn"));
        runner.RecordReceipt(new(first.ProposalId!, first.ActionId!, 1, true, true));
        var last = runner.Observe(Frame(binding, 2, "Pad", first.ProposalId));
        Assert.True(last.Stopped); Assert.False(last.IsProposal); Assert.Equal("bundle-action-budget-exceeded", last.Reason);
        Assert.Equal(1, runner.ProposedActions);
    }

    [Fact]
    public void BundleDeadlineAppliesToReceiptsAndSurvivesModuleHandoffs()
    {
        var bundle = LabBundle() with { TimeoutMs = 250 }; var binding = Binding(bundle);
        var runner = new ModuleRunner(bundle, binding, "test"); var first = runner.Observe(Frame(binding, 0, "Spawn"));
        runner.RecordReceipt(new(first.ProposalId!, first.ActionId!, 1, true, true));
        var second = runner.Observe(Frame(binding, 200, "Pad", first.ProposalId));
        runner.RecordReceipt(new(second.ProposalId!, second.ActionId!, 250, true, true));
        Assert.Equal("bundle-deadline-exceeded", runner.Status.Reason); Assert.True(runner.Status.Stopped);
    }

    [Fact]
    public void RepeatedAndBackwardHandoffFramesNeitherAdvanceNorExtendDeadline()
    {
        var bundle = LabBundle() with { TimeoutMs = 250 }; var binding = Binding(bundle);
        var runner = new ModuleRunner(bundle, binding, "test"); var first = runner.Observe(Frame(binding, 0, "Spawn"));
        runner.RecordReceipt(new(first.ProposalId!, first.ActionId!, 1, true, true));
        var second = runner.Observe(Frame(binding, 200, "Pad", first.ProposalId));
        runner.RecordReceipt(new(second.ProposalId!, second.ActionId!, 201, true, true));
        Assert.Equal("invalid-or-repeated-time", runner.Observe(Frame(binding, 201, "Station", second.ProposalId)).Reason);
        Assert.Equal("invalid-or-repeated-time", runner.Observe(Frame(binding, 150, "Station", second.ProposalId)).Reason);
        Assert.Equal(1, runner.CompletedModules);
        Assert.Equal("bundle-deadline-exceeded", runner.Observe(Frame(binding, 250, "Station", second.ProposalId)).Reason);
        Assert.Equal(2, runner.ProposedActions);
    }

    [Fact]
    public void EditingSourceBundleAfterConstructionCannotChangeRunningBindings()
    {
        var bundle = LabBundle(); var binding = Binding(bundle); var runner = new ModuleRunner(bundle, binding, "test");
        bundle.Modules[0].RequiredFacts[0] = "new-fact";
        bundle.Modules[0] = bundle.Modules[^1];
        Assert.Equal("lab.walk", runner.Observe(Frame(binding, 0, "Spawn")).ActionId);
    }

    [Fact]
    public void BindingMustMatchBundleContextAndVisit()
    {
        var bundle = LabBundle();
        Assert.Throws<InvalidDataException>(() => new ModuleRunner(bundle, Binding(bundle) with { ContextId = "other.visit.attempt1" }));
        Assert.Throws<InvalidDataException>(() => new ModuleRunner(bundle, Binding(bundle) with { ContextId = bundle.ContextId + ".visit." }));
    }
}
