using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101QuestStepTests
{
    private static readonly W101QuestStepSelection Selected = new("quinn-pilot", "Talk to Quinn", "Go speak to the guard.", "Talk to the guard");
    private static readonly WizwalkerTarget Target = new(42, 123, 100, "WizardGraphicalClient", "inert fixture", new string('a', 64));
    private static readonly long Epoch = DateTime.UtcNow.Ticks;
    private static long Stamp(double ms) => Epoch + (long)(ms * TimeSpan.TicksPerMillisecond);
    private static Dictionary<string, bool?> States(bool dialogue) => new()
    { ["interactionPrompt"] = false, ["dialogue"] = dialogue, ["menu"] = false, ["nativeIdle"] = false };
    private static W101UiTreeResult Read(double ms, bool after = false)
    {
        var context = new W101QuestContext(after ? Selected.ObjectiveAfter : Selected.ObjectiveBefore,
            after ? null : Selected.DialogueBefore, after ? null : true);
        return new(1, "observation", W101UiTreeOperation.Version, WizwalkerOperation.SourceSha256, Target, "observed", "test",
            false, States(!after), Stamp(ms), Stamp(ms), 1000, true, true, 8, 2, 0, States(!after), context, context);
    }
    private static RoutineBinding Binding() => W101QuestStep.Compile(Selected).Bind(W101QuestStep.TargetBinding(Target), "grid12", "visit1");
    private static ModuleObservation Frame(double now, W101UiTreeResult? read = null, string? proposal = null, RoutineObservation? authority = null) =>
        W101QuestStep.Project(Selected, Target, read ?? Read(now), Stamp(now), authority ?? new(now, "IgnoredCallerScene", Binding(),
            Fresh: true, TargetReady: true, ContextVerified: true, InputOwned: true, ControlVerified: true, OutcomeForProposalId: proposal));

    [Fact]
    public void SelectedStepReusesMenuRunnerAndCompletesOnlyAfterReceiptAndExpectedObjective()
    {
        var bundle = W101QuestStep.Compile(Selected);
        Assert.False(bundle.LiveQualified); Assert.Equal(1, bundle.MaxActions);
        Assert.Equal("menu", Assert.Single(bundle.Modules).Kind);
        var runner = new ModuleRunner(bundle, Binding(), "pilot");
        var next = runner.Observe(Frame(1000));
        Assert.Equal(W101QuestStep.ActionId, next.ActionId);
        Assert.Equal(W101QuestStep.ControlId, next.Action!.ControlId);
        Assert.False(runner.Observe(Frame(1100, Read(1100, true), next.ProposalId)).Completed);
        runner.RecordReceipt(new(next.ProposalId!, next.ActionId!, 1200, true, true));
        Assert.False(runner.Observe(Frame(1300, Read(1300, true), "wrong-proposal")).Completed);
        Assert.True(runner.Observe(Frame(1400, Read(1400, true), next.ProposalId)).Completed);
        Assert.Equal(1, runner.ProposedActions);
        var completed = runner.Observe(Frame(1500));
        Assert.True(completed.Completed); Assert.False(completed.IsProposal);
        Assert.DoesNotContain(ModuleCatalog.Compile(bundle).SelectMany(m => m.Actions), a => a.ActionId.Contains("idle"));
    }

    [Theory]
    [InlineData("loading")]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("target")]
    [InlineData("no-context")]
    [InlineData("unstable")]
    [InlineData("wrong-dialogue")]
    [InlineData("wrong-objective")]
    [InlineData("disabled")]
    [InlineData("menu")]
    [InlineData("idle")]
    [InlineData("cleanup")]
    [InlineData("source")]
    public void MissingOrUnrelatedEvidenceCannotPropose(string fault)
    {
        var read = Read(5000);
        var changed = fault switch
        {
            "loading" => read with { Loading = true },
            "stale" => read with { StatesObservedUtcTicks = Stamp(1000) },
            "future" => read with { ObservedUtcTicks = Stamp(6000) },
            "target" => read with { Target = Target with { ProcessId = 987 } },
            "no-context" => read with { QuestContext = null, StableQuestContext = null },
            "unstable" => read with { StableQuestContext = null },
            "wrong-dialogue" => Context(read, read.QuestContext! with { DialogueText = "Unrelated conversation" }),
            "wrong-objective" => Context(read, read.QuestContext! with { ObjectiveText = "Something else" }),
            "disabled" => Context(read, read.QuestContext! with { AdvanceEnabled = false }),
            "menu" => read with { States = new(States(true)) { ["menu"] = true } },
            "idle" => read with { States = new(States(true)) { ["nativeIdle"] = true } },
            "cleanup" => read with { CleanupConfirmed = false },
            _ => read with { SourceSha256 = new string('b', 64) }
        };
        var runner = new ModuleRunner(W101QuestStep.Compile(Selected), Binding(), "pilot");
        Assert.False(runner.Observe(Frame(5000, changed)).IsProposal);
    }

    private static W101UiTreeResult Context(W101UiTreeResult read, W101QuestContext context) => read with { QuestContext = context, StableQuestContext = context };

    [Theory]
    [InlineData("closed-only")]
    [InlineData("other-objective")]
    [InlineData("dialogue-page")]
    [InlineData("pre-receipt")]
    public void DispatchDoesNotProveProgressAndUnverifiedOutcomeNeverRetries(string fault)
    {
        var runner = new ModuleRunner(W101QuestStep.Compile(Selected), Binding(), "pilot");
        var proposal = runner.Observe(Frame(1000));
        runner.RecordReceipt(new(proposal.ProposalId!, proposal.ActionId!, 1200, true, true));
        var after = Read(1500, true);
        var read = fault switch
        {
            "closed-only" => Context(after, after.QuestContext! with { ObjectiveText = Selected.ObjectiveBefore }),
            "other-objective" => Context(after, after.QuestContext! with { ObjectiveText = "Other quest" }),
            "dialogue-page" => Context(Read(1500), new(Selected.ObjectiveAfter, "Another dialogue page", true)),
            _ => Read(1100, true)
        };
        var decision = runner.Observe(Frame(1600, read, proposal.ProposalId));
        Assert.False(decision.Completed); Assert.False(decision.IsProposal);
        Assert.True(runner.Observe(Frame(16000, read, proposal.ProposalId)).Stopped);
        Assert.Equal(1, runner.ProposedActions);
    }

    [Theory]
    [InlineData("takeover")]
    [InlineData("owner")]
    [InlineData("cancel")]
    public void ExistingAuthorityStopCannotBeOverriddenByText(string fault)
    {
        var runner = new ModuleRunner(W101QuestStep.Compile(Selected), Binding(), "pilot");
        var authority = new RoutineObservation(1000, "Ignored", Binding(), true, true, true, fault != "owner", true,
            Cancelled: fault == "cancel", UserActive: fault == "takeover");
        Assert.True(runner.Observe(Frame(1000, authority: authority)).Stopped);
        Assert.False(runner.Observe(Frame(1100)).IsProposal);
    }

    [Fact]
    public void ChangingSelectionChangesVisitContextAndUnknownObjectivesAreRejected()
    {
        var changed = Selected with { DialogueBefore = "A different page" };
        Assert.NotEqual(W101QuestStep.Compile(Selected).ContextId, W101QuestStep.Compile(changed).ContextId);
        var evidence = W101QuestStep.Project(changed, Target, Read(1000), Stamp(1000), Frame(1000).Observation);
        Assert.False(evidence.Observation.ContextVerified);
        Assert.Throws<InvalidDataException>(() => W101QuestStep.Compile(Selected with { ObjectiveAfter = Selected.ObjectiveBefore }));
        Assert.Throws<InvalidDataException>(() => W101QuestStep.Compile(Selected with { ObjectiveBefore = "" }));
    }
}
