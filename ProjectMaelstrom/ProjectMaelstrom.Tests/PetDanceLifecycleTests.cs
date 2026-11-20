using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class PetDanceLifecycleTests
{
    private static readonly PetDanceLifecycleOptions Options = new("window-1", new Size(1282, 759), "w101-pet-dance",
        LevelId: "wizard-city", ApproachRouteId: "recorded-approach", ApproachRouteQualified: true);
    private double _time;
    private PetDanceLifecycleObservation Frame(PetDanceScene scene, string? transition = null) => new(++_time,
        transition ?? "transition-" + _time, scene, Options.TargetId, Options.Geometry, Options.ContextId,
        Fresh: true, TargetReady: true, ContextVerified: true, SelectedLevelId: Options.LevelId);
    private PetDanceLifecyclePlan Sample(PetDanceLifecycle planner, PetDanceScene scene) => planner.Observe(Frame(scene));
    private void Start(PetDanceLifecycle planner)
    {
        Assert.Equal(PetDanceLifecycleAction.Play, Sample(planner, PetDanceScene.InstructionsReady).Action);
        Assert.Equal(PetDanceLifecycleAction.TeacherHandoff, Sample(planner, PetDanceScene.Watch).Action);
    }
    private void Result(PetDanceLifecycle planner) => Assert.Equal(PetDanceLifecycleAction.Next,
        planner.Observe(Frame(PetDanceScene.GameResult) with { OutcomeVerified = true }).Action);

    [Fact]
    public void WholeVisitProposesApproachThroughVerifiedNativeIdleWithoutFeeding()
    {
        var planner = new PetDanceLifecycle(Options);
        Assert.False(Sample(planner, PetDanceScene.NativeIdle).Completed);
        var approach = Sample(planner, PetDanceScene.OutsideNoSigil);
        Assert.Equal(PetDanceLifecycleAction.ApproachRoute, approach.Action);
        Assert.Equal("recorded-approach", approach.Argument);
        Assert.Equal(PetDanceLifecycleAction.InteractX, Sample(planner, PetDanceScene.DanceSigil).Action);
        Assert.Equal(PetDanceLifecycleAction.ChooseLevel, Sample(planner, PetDanceScene.InstructionsUnselected).Action);
        Assert.Equal(PetDanceLifecycleAction.Play, Sample(planner, PetDanceScene.InstructionsReady).Action);
        var loading = Sample(planner, PetDanceScene.Loading);
        Assert.True(loading.LoadingPaused);
        Assert.False(loading.IsProposal);
        Assert.Equal(PetDanceLifecycleAction.TeacherHandoff, Sample(planner, PetDanceScene.Watch).Action);
        foreach (var scene in new[] { PetDanceScene.Go, PetDanceScene.RoundFeedback, PetDanceScene.Watch, PetDanceScene.Go, PetDanceScene.RoundFeedback })
            Assert.False(Sample(planner, scene).IsProposal);
        Result(planner);
        Assert.Equal(PetDanceLifecycleAction.FinishWithoutSnack, Sample(planner, PetDanceScene.SnackOffer).Action);
        Assert.Equal(PetDanceLifecycleAction.OpenMenu, Sample(planner, PetDanceScene.OutsideAfterGame).Action);
        Assert.Equal(PetDanceLifecycleAction.SelectIdle, Sample(planner, PetDanceScene.MenuObserved).Action);
        Assert.True(Sample(planner, PetDanceScene.NativeIdle).Completed);
        Assert.True(Sample(planner, PetDanceScene.NativeIdle).Completed);
        Assert.False(Sample(planner, PetDanceScene.DanceSigil).IsProposal);
    }

    [Fact]
    public void UnknownAndRepaintCannotRepeatPlayOrNext()
    {
        var planner = new PetDanceLifecycle(Options);
        Start(planner);
        Assert.False(Sample(planner, PetDanceScene.Unknown).IsProposal);
        Assert.False(Sample(planner, PetDanceScene.InstructionsReady).IsProposal);
        Result(planner);
        Assert.False(Sample(planner, PetDanceScene.Unknown).IsProposal);
        Assert.False(planner.Observe(Frame(PetDanceScene.GameResult) with { OutcomeVerified = true }).IsProposal);
    }

    [Fact]
    public void RoundFeedbackOrUnverifiedResultCannotAdvanceTheGame()
    {
        var planner = new PetDanceLifecycle(Options);
        Start(planner);
        Assert.False(Sample(planner, PetDanceScene.RoundFeedback).IsProposal);
        Assert.False(Sample(planner, PetDanceScene.GameResult).IsProposal);
        Assert.False(Sample(planner, PetDanceScene.SnackOffer).IsProposal);
        Assert.False(Sample(planner, PetDanceScene.OutsideAfterGame).IsProposal);
        Assert.False(Sample(planner, PetDanceScene.NativeIdle).Completed);
    }

    [Fact]
    public void OptionalSnackHandoffRequiresEnablementAndNeverMeansFeed()
    {
        var planner = new PetDanceLifecycle(Options with { SnacksEnabled = true });
        Start(planner); Result(planner);
        Assert.Equal(PetDanceLifecycleAction.OptionalSnackPlannerHandoff, Sample(planner, PetDanceScene.SnackOffer).Action);
        Assert.False(Sample(planner, PetDanceScene.SnackOffer).IsProposal);
        var finished = planner.Observe(Frame(PetDanceScene.SnackOffer) with { SnackReviewComplete = true });
        Assert.Equal(PetDanceLifecycleAction.FinishWithoutSnack, finished.Action);
        Assert.False(planner.Observe(Frame(PetDanceScene.SnackOffer) with { SnackReviewComplete = true }).IsProposal);
    }

    [Fact]
    public void MenuNeedsItsOwnObservedStateAndActionsAreNeverRepeated()
    {
        var planner = new PetDanceLifecycle(Options);
        Assert.False(Sample(planner, PetDanceScene.MenuObserved).IsProposal);
        Start(planner); Result(planner);
        Assert.Equal(PetDanceLifecycleAction.OpenMenu, Sample(planner, PetDanceScene.OutsideAfterGame).Action);
        Assert.False(Sample(planner, PetDanceScene.OutsideAfterGame).IsProposal);
        Assert.Equal(PetDanceLifecycleAction.SelectIdle, Sample(planner, PetDanceScene.MenuObserved).Action);
        Assert.False(Sample(planner, PetDanceScene.Unknown).IsProposal);
        Assert.False(Sample(planner, PetDanceScene.MenuObserved).IsProposal);
    }

    [Fact]
    public void ATransitionIdentityCannotAuthorizeTwoDifferentActions()
    {
        var planner = new PetDanceLifecycle(Options);
        Assert.True(planner.Observe(Frame(PetDanceScene.DanceSigil, "same-transition")).IsProposal);
        Assert.False(planner.Observe(Frame(PetDanceScene.InstructionsUnselected, "same-transition")).IsProposal);
        Assert.True(planner.Observe(Frame(PetDanceScene.InstructionsUnselected, "real-new-transition")).IsProposal);
    }

    [Fact]
    public void ReusedTransitionCannotFabricateAWholeGameOrCompletionMilestones()
    {
        var planner = new PetDanceLifecycle(Options);
        Assert.Equal(PetDanceLifecycleAction.Play, planner.Observe(Frame(PetDanceScene.InstructionsReady, "one-transition")).Action);
        foreach (var scene in new[] { PetDanceScene.Watch, PetDanceScene.GameResult, PetDanceScene.OutsideAfterGame, PetDanceScene.NativeIdle })
        {
            var rejected = planner.Observe(Frame(scene, "one-transition") with { OutcomeVerified = true });
            Assert.Equal("conflicting-scene-transition", rejected.Reason);
            Assert.False(rejected.IsProposal);
            Assert.False(rejected.Completed);
        }
        Assert.False(planner.Observe(Frame(PetDanceScene.GameResult) with { OutcomeVerified = true }).IsProposal);
        Assert.False(Sample(planner, PetDanceScene.OutsideAfterGame).IsProposal);
        Assert.False(Sample(planner, PetDanceScene.NativeIdle).Completed);

        Assert.Equal(PetDanceLifecycleAction.TeacherHandoff, Sample(planner, PetDanceScene.Watch).Action);
        Result(planner);
        Assert.Equal(PetDanceLifecycleAction.OpenMenu, Sample(planner, PetDanceScene.OutsideAfterGame).Action);
        Assert.Equal(PetDanceLifecycleAction.SelectIdle, Sample(planner, PetDanceScene.MenuObserved).Action);
        Assert.True(Sample(planner, PetDanceScene.NativeIdle).Completed);
    }

    [Fact]
    public void RepaintingASceneWaitsAndRepeatedLoadingKeepsPauseContext()
    {
        var planner = new PetDanceLifecycle(Options);
        Assert.True(planner.Observe(Frame(PetDanceScene.InstructionsReady, "ready")).IsProposal);
        var repaint = planner.Observe(Frame(PetDanceScene.InstructionsReady, "ready"));
        Assert.Equal(PetDanceLifecycleAction.Wait, repaint.Action);
        Assert.False(repaint.IsProposal);
        Assert.True(planner.Observe(Frame(PetDanceScene.Loading, "loading")).LoadingPaused);
        var loadingRepaint = planner.Observe(Frame(PetDanceScene.Loading, "loading"));
        Assert.Equal(PetDanceLifecycleAction.Wait, loadingRepaint.Action);
        Assert.True(loadingRepaint.LoadingPaused);
        Assert.False(planner.Observe(Frame(PetDanceScene.Watch, "loading")).IsProposal);
        Assert.False(planner.Observe(Frame(PetDanceScene.GameResult) with { OutcomeVerified = true }).IsProposal);
    }

    [Fact]
    public void RejectedResultAndOutsideTransitionsDoNotUnlockCleanup()
    {
        var planner = new PetDanceLifecycle(Options);
        Assert.True(planner.Observe(Frame(PetDanceScene.InstructionsReady, "play")).IsProposal);
        Assert.True(planner.Observe(Frame(PetDanceScene.Watch, "watch")).IsProposal);
        Assert.False(planner.Observe(Frame(PetDanceScene.GameResult, "watch") with { OutcomeVerified = true }).IsProposal);
        Assert.False(Sample(planner, PetDanceScene.SnackOffer).IsProposal);
        Assert.False(Sample(planner, PetDanceScene.OutsideAfterGame).IsProposal);

        Assert.True(planner.Observe(Frame(PetDanceScene.GameResult, "result") with { OutcomeVerified = true }).IsProposal);
        Assert.False(planner.Observe(Frame(PetDanceScene.OutsideAfterGame, "result")).IsProposal);
        Assert.False(Sample(planner, PetDanceScene.MenuObserved).IsProposal);
        Assert.False(Sample(planner, PetDanceScene.NativeIdle).Completed);

        Assert.True(Sample(planner, PetDanceScene.OutsideAfterGame).IsProposal);
        Assert.True(Sample(planner, PetDanceScene.MenuObserved).IsProposal);
        Assert.True(Sample(planner, PetDanceScene.NativeIdle).Completed);
    }

    [Fact]
    public void UnknownRouteAndWrongSelectedLevelProvidePrerequisitesNotInventedActions()
    {
        var planner = new PetDanceLifecycle(Options with { ApproachRouteQualified = false });
        var approach = Sample(planner, PetDanceScene.OutsideNoSigil);
        Assert.False(approach.IsProposal);
        Assert.NotEmpty(approach.EvidenceNeeded);
        Assert.False(planner.Observe(Frame(PetDanceScene.InstructionsReady) with { SelectedLevelId = "different-level" }).IsProposal);
        Assert.False(Sample(planner, PetDanceScene.Watch).IsProposal);
    }

    [Fact]
    public void StaleOrUnverifiedFramesNeverAdvanceThePlanner()
    {
        var planner = new PetDanceLifecycle(Options);
        Assert.False(planner.Observe(Frame(PetDanceScene.DanceSigil) with { Fresh = false }).IsProposal);
        Assert.False(planner.Observe(Frame(PetDanceScene.DanceSigil) with { TargetReady = false }).IsProposal);
        Assert.False(planner.Observe(Frame(PetDanceScene.DanceSigil) with { ContextVerified = false }).IsProposal);
        var fresh = Frame(PetDanceScene.DanceSigil);
        Assert.True(planner.Observe(fresh).IsProposal);
        Assert.False(planner.Observe(fresh).IsProposal);
        Assert.False(planner.Observe(Frame(PetDanceScene.InstructionsReady) with { TimeMs = double.NaN }).IsProposal);
    }

    [Theory]
    [InlineData("takeover")]
    [InlineData("target")]
    [InlineData("geometry")]
    [InlineData("context")]
    public void TakeoverAndContextDriftRequireANewVisit(string change)
    {
        var planner = new PetDanceLifecycle(Options);
        var frame = Frame(PetDanceScene.DanceSigil);
        frame = change switch
        {
            "takeover" => frame with { TakenOver = true },
            "target" => frame with { TargetId = "other-window" },
            "geometry" => frame with { Geometry = new Size(800, 600) },
            _ => frame with { ContextId = "other-game" }
        };
        Assert.False(planner.Observe(frame).IsProposal);
        Assert.False(Sample(planner, PetDanceScene.DanceSigil).IsProposal);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("geometry")]
    [InlineData("context")]
    public void KnownBindingDriftLatchesEvenWhenPixelsAndContextAreNotReady(string change)
    {
        var planner = new PetDanceLifecycle(Options);
        var frame = Frame(PetDanceScene.Unknown) with { Fresh = false, TargetReady = false, ContextVerified = false };
        frame = change switch
        {
            "target" => frame with { TargetId = "other-window" },
            "geometry" => frame with { Geometry = new Size(800, 600) },
            _ => frame with { ContextId = "other-game" }
        };
        Assert.Equal("bound-context-changed", planner.Observe(frame).Reason);
        Assert.Equal("visit-interrupted", Sample(planner, PetDanceScene.DanceSigil).Reason);
        Assert.False(Sample(planner, PetDanceScene.InstructionsReady).IsProposal);
    }
}
