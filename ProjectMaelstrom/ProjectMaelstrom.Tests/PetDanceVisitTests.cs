using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceVisitTests
{
    private static readonly Size Geometry = new(1282, 760);
    private static readonly PetDanceVisitOptions Options = new("window", Geometry, "visit", PetDanceArena.WizardCity, Enabled: true);
    private static readonly RectangleF Play = new(778, 574, 42, 12), Level = new(452, 485, 59, 58), Menu = new(760, 574, 50, 13);
    private static readonly string Hash = new('a', 64);
    private double _time;
    private PetDanceVisitObservation Frame(PetDanceScene scene, PetDanceArena arena = PetDanceArena.WizardCity)
    {
        var time = ++_time * 100;
        var entry = scene is PetDanceScene.DanceSigil or PetDanceScene.InstructionsUnselected or PetDanceScene.InstructionsReady
            ? new PetDanceEntryScene(scene, Hash, time, Geometry, "synthetic-qualified-cue", Play,
                scene == PetDanceScene.InstructionsReady ? arena : null, [new(arena, Level, true)],
                SelectionVerified: scene == PetDanceScene.InstructionsReady, PlayEnabledVerified: scene == PetDanceScene.InstructionsReady) : null;
        return new(time, Hash, scene, Options.TargetId, Geometry, Options.ContextId, entry, Menu,
            Fresh: true, TargetReady: true, ContextVerified: true, InputOwned: true, EntryCostApproved: true,
            WatchStartVerified: true, SpecialistQualified: true, WholeGameResultVerified: true);
    }
    private static void Ack(PetDanceVisit visit, PetDanceVisitPlan proposal, double time) =>
        visit.RecordDispatch(proposal.ProposalId!, proposal.Action, time + 1, true, true);
    private PetDanceVisitPlan Step(PetDanceVisit visit, PetDanceScene scene, PetDanceLifecycleAction expected)
    {
        var frame = Frame(scene); var proposal = visit.Observe(frame);
        Assert.True(proposal.IsProposal); Assert.Equal(expected, proposal.Action);
        Ack(visit, proposal, frame.TimeMs); return proposal;
    }
    private void Teacher(PetDanceVisit visit)
    {
        var frame = Frame(PetDanceScene.Watch); var proposal = visit.Observe(frame);
        Assert.Equal(PetDanceLifecycleAction.TeacherHandoff, proposal.Action);
        visit.RecordSpecialist(new(proposal.ProposalId!, frame.TimeMs + 20, Options.TargetId, Geometry,
            Options.ContextId, Options.Arena, Started: true, Completed: true, InputReleased: true));
    }
    private void Finish(PetDanceVisit visit)
    {
        Step(visit, PetDanceScene.GameResult, PetDanceLifecycleAction.Next);
        Step(visit, PetDanceScene.SnackOffer, PetDanceLifecycleAction.FinishWithoutSnack);
        Step(visit, PetDanceScene.OutsideAfterGame, PetDanceLifecycleAction.OpenMenu);
        Step(visit, PetDanceScene.MenuObserved, PetDanceLifecycleAction.SelectIdle);
        Assert.True(visit.Observe(Frame(PetDanceScene.NativeIdle)).Completed);
        Assert.True(visit.Status.ResultObserved); Assert.True(visit.Status.Completed);
    }

    [Fact]
    public void FullVisitNeedsEveryReceiptAndResultThenReturnsIdleWithoutFeeding()
    {
        var visit = new PetDanceVisit(Options);
        Assert.False(visit.Observe(Frame(PetDanceScene.NativeIdle)).Completed);
        Step(visit, PetDanceScene.DanceSigil, PetDanceLifecycleAction.InteractX);
        Step(visit, PetDanceScene.InstructionsUnselected, PetDanceLifecycleAction.ChooseLevel);
        Step(visit, PetDanceScene.InstructionsReady, PetDanceLifecycleAction.Play);
        Assert.False(visit.Observe(Frame(PetDanceScene.Loading)).IsProposal);
        Assert.False(visit.Observe(Frame(PetDanceScene.Unknown)).IsProposal);
        Teacher(visit); Finish(visit);
        Assert.False(visit.Observe(Frame(PetDanceScene.DanceSigil) with { UserActive = true }).IsProposal);
        Assert.True(visit.Status.Completed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AlreadySelectedInstructionsCanStartDirectlyOrAfterSigilInteraction(bool fromSigil)
    {
        var visit = new PetDanceVisit(Options);
        if (fromSigil) Step(visit, PetDanceScene.DanceSigil, PetDanceLifecycleAction.InteractX);
        Step(visit, PetDanceScene.InstructionsReady, PetDanceLifecycleAction.Play);
        Teacher(visit); Finish(visit);
    }

    [Fact]
    public void WrongArenaOnlyAllowsChoosingAnIndependentlyIdentifiedRequestedControl()
    {
        var visit = new PetDanceVisit(Options);
        var wrong = Frame(PetDanceScene.InstructionsReady, PetDanceArena.Krokotopia);
        Assert.False(visit.Observe(wrong).IsProposal);
        wrong = Frame(PetDanceScene.InstructionsReady, PetDanceArena.Krokotopia);
        wrong = wrong with { Entry = wrong.Entry! with { LevelControls = [new(PetDanceArena.WizardCity, Level, true)] } };
        var proposal = visit.Observe(wrong);
        Assert.Equal(PetDanceLifecycleAction.ChooseLevel, proposal.Action); Assert.Equal(PetDanceArena.WizardCity, proposal.Arena);
        Ack(visit, proposal, wrong.TimeMs);
        Assert.False(visit.Observe(Frame(PetDanceScene.InstructionsReady, PetDanceArena.Krokotopia)).IsProposal);
        Step(visit, PetDanceScene.InstructionsReady, PetDanceLifecycleAction.Play);
    }

    [Fact]
    public void SelectionEnabledStateCostAndSameFrameBoundsAreIndependentGates()
    {
        foreach (var change in new Func<PetDanceVisitObservation, PetDanceVisitObservation>[]
        {
            f => f with { EntryCostApproved = false }, f => f with { Entry = null },
            f => f with { Entry = f.Entry! with { SelectionVerified = false } },
            f => f with { Entry = f.Entry! with { PlayEnabledVerified = false } },
            f => f with { Entry = f.Entry! with { CapturedMs = f.TimeMs - 1 } },
            f => f with { Entry = f.Entry! with { ImageSha256 = new('b', 64) } },
            f => f with { Entry = f.Entry! with { Geometry = new(1920, 1080) } },
            f => f with { Entry = f.Entry! with { PlayBounds = new(-1, 1, 10, 10) } },
            f => f with { Entry = f.Entry! with { LevelControls = [] } }
        }) Assert.False(new PetDanceVisit(Options).Observe(change(Frame(PetDanceScene.InstructionsReady))).IsProposal);
    }

    [Fact]
    public void BeginningInTheMiddleOfAGameOrAtItsResultCannotAdoptIt()
    {
        var visit = new PetDanceVisit(Options);
        foreach (var scene in new[] { PetDanceScene.Watch, PetDanceScene.Go, PetDanceScene.RoundFeedback,
            PetDanceScene.GameResult, PetDanceScene.SnackOffer, PetDanceScene.OutsideAfterGame, PetDanceScene.MenuObserved, PetDanceScene.NativeIdle })
        {
            var plan = visit.Observe(Frame(scene)); Assert.False(plan.IsProposal); Assert.False(plan.Completed);
        }
    }

    [Fact]
    public void InteractDispatchNeedsAnObservedOutcomeAndNoProposalIsBlindlyRepeated()
    {
        var visit = new PetDanceVisit(Options);
        var start = Frame(PetDanceScene.DanceSigil); var proposal = visit.Observe(start);
        Assert.False(visit.Observe(Frame(PetDanceScene.InstructionsReady)).IsProposal);
        visit.RecordDispatch(proposal.ProposalId!, proposal.Action, _time * 100 + 20, true, true);
        Assert.False(visit.Observe(Frame(PetDanceScene.DanceSigil)).IsProposal);
        Step(visit, PetDanceScene.InstructionsReady, PetDanceLifecycleAction.Play);
    }

    [Fact]
    public void AQualifiedBeginningIsRequiredBeforeSpecialistHandoff()
    {
        var visit = new PetDanceVisit(Options); Step(visit, PetDanceScene.InstructionsReady, PetDanceLifecycleAction.Play);
        Assert.False(visit.Observe(Frame(PetDanceScene.Watch) with { WatchStartVerified = false }).IsProposal);
        Assert.False(visit.Observe(Frame(PetDanceScene.Watch) with { SpecialistQualified = false }).IsProposal);
        Teacher(visit);
    }

    [Fact]
    public void TeacherDispatchAloneCannotBeACompletedSpecialistReceipt()
    {
        var visit = new PetDanceVisit(Options); Step(visit, PetDanceScene.InstructionsReady, PetDanceLifecycleAction.Play);
        var watch = Frame(PetDanceScene.Watch); var proposal = visit.Observe(watch);
        Ack(visit, proposal, watch.TimeMs);
        Assert.True(visit.Status.Stopped);
        Assert.False(visit.Observe(Frame(PetDanceScene.GameResult)).IsProposal);
    }

    [Fact]
    public void SpecialistCompletionMustMatchVisitArenaProposalAndReleasedInput()
    {
        foreach (var change in new Func<PetDanceSpecialistReceipt, PetDanceSpecialistReceipt>[]
        {
            r => r with { Started = false }, r => r with { Completed = false }, r => r with { InputReleased = false },
            r => r with { ContextId = "other-visit" }, r => r with { TargetId = "other-window" },
            r => r with { Geometry = new(1920, 1080) }, r => r with { Arena = PetDanceArena.Krokotopia },
            r => r with { ProposalId = "other-proposal" }, r => r with { CompletedMs = 0 }
        })
        {
            var visit = new PetDanceVisit(Options); Step(visit, PetDanceScene.InstructionsReady, PetDanceLifecycleAction.Play);
            var watch = Frame(PetDanceScene.Watch); var proposal = visit.Observe(watch);
            visit.RecordSpecialist(change(new(proposal.ProposalId!, watch.TimeMs + 20, Options.TargetId, Geometry,
                Options.ContextId, Options.Arena, Started: true, Completed: true, InputReleased: true)));
            Assert.True(visit.Status.Stopped); Assert.False(visit.Observe(Frame(PetDanceScene.GameResult)).IsProposal);
        }
    }

    [Fact]
    public void WholeGameResultRequiresNewerVerifiedEvidenceAfterSpecialistCompletion()
    {
        var visit = new PetDanceVisit(Options); Step(visit, PetDanceScene.InstructionsReady, PetDanceLifecycleAction.Play);
        Teacher(visit);
        Assert.False(visit.Observe(Frame(PetDanceScene.GameResult) with { WholeGameResultVerified = false }).IsProposal);
        Assert.False(visit.Observe(Frame(PetDanceScene.RoundFeedback)).IsProposal);
        Step(visit, PetDanceScene.GameResult, PetDanceLifecycleAction.Next);
    }

    [Fact]
    public void TakeoverCancellationAndChangedBindingsLatchStopWithoutCleanupActions()
    {
        foreach (var change in new Func<PetDanceVisitObservation, PetDanceVisitObservation>[]
        {
            f => f with { UserActive = true }, f => f with { Cancelled = true }, f => f with { InputOwned = false },
            f => f with { TargetReady = false }, f => f with { TargetId = "other-window" },
            f => f with { ContextId = "other-visit" }, f => f with { Geometry = new(1920, 1080) }
        })
        {
            var visit = new PetDanceVisit(Options); Step(visit, PetDanceScene.InstructionsReady, PetDanceLifecycleAction.Play);
            Assert.True(visit.Observe(change(Frame(PetDanceScene.Loading))).Stopped);
            Assert.False(visit.Observe(Frame(PetDanceScene.MenuObserved)).IsProposal);
        }
    }

    [Fact]
    public void UncertainFramesNeverAdvanceAndLoadingCannotExtendTheVisitBudget()
    {
        var visit = new PetDanceVisit(Options with { TimeoutMs = 1000 });
        var frame = Frame(PetDanceScene.InstructionsReady);
        Assert.False(visit.Observe(frame with { Fresh = false }).IsProposal);
        Assert.False(visit.Observe(frame).IsProposal);
        Assert.False(visit.Observe(Frame(PetDanceScene.InstructionsReady) with { ContextVerified = false }).IsProposal);
        Assert.False(visit.Observe(Frame(PetDanceScene.InstructionsReady) with { ImageSha256 = "missing" }).IsProposal);
        var loading = Frame(PetDanceScene.Loading) with { TimeMs = frame.TimeMs + 1000 };
        Assert.True(visit.Observe(loading).Stopped);
    }

    [Fact]
    public void DefaultDisabledVisitAndStartingIdleCannotWakeOrFinishAGame()
    {
        var disabled = new PetDanceVisit(Options with { Enabled = false });
        Assert.False(disabled.Observe(Frame(PetDanceScene.InstructionsReady)).IsProposal);
        Assert.True(disabled.Status.Stopped); Assert.Equal("visit-disabled", disabled.Status.Reason);
        var visit = new PetDanceVisit(Options);
        Assert.False(visit.Observe(Frame(PetDanceScene.NativeIdle)).Completed);
        Assert.False(visit.Status.Completed);
    }
}
