using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

/// <summary>
/// Offline contract tests for the Pet Dance visit state machine, lifecycle transitions,
/// and scheduled execution boundaries (GEM-REPLAY-MATRIX-72).
/// All tests use deterministic fixtures and existing seams without game or process dependencies.
/// Status: SOURCE-ONLY, UNRUN (coordinator owns execution).
/// </summary>
public sealed class GeminiDanceVisitContractTests
{
    private static readonly Size Geometry = new(1282, 760);
    private static readonly PetDanceVisitOptions Options = new("window-target", Geometry, "visit-context", PetDanceArena.WizardCity, Enabled: true);
    private static readonly RectangleF PlayBounds = new(778, 574, 42, 12);
    private static readonly RectangleF LevelBounds = new(452, 485, 59, 58);
    private static readonly RectangleF ControlBounds = new(760, 574, 50, 13);
    private static readonly string ValidImageHash = new('b', 64);
    private double _time;

    private PetDanceVisitObservation MakeFrame(
        PetDanceScene scene,
        PetDanceArena arena = PetDanceArena.WizardCity,
        bool targetReady = true,
        bool userActive = false,
        bool cancelled = false,
        bool entryCostApproved = true,
        bool wholeGameResultVerified = true,
        bool focusPaused = false)
    {
        var time = ++_time * 100.0;
        var entry = scene is PetDanceScene.DanceSigil or PetDanceScene.InstructionsUnselected or PetDanceScene.InstructionsReady
            ? new PetDanceEntryScene(
                scene,
                ValidImageHash,
                time,
                Geometry,
                "synthetic-contract-cue",
                PlayBounds,
                scene == PetDanceScene.InstructionsReady ? arena : null,
                [new(arena, LevelBounds, true)],
                SelectionVerified: scene == PetDanceScene.InstructionsReady,
                PlayEnabledVerified: scene == PetDanceScene.InstructionsReady)
            : null;

        return new(
            time,
            ValidImageHash,
            scene,
            Options.TargetId,
            Geometry,
            Options.ContextId,
            entry,
            ControlBounds,
            Fresh: true,
            TargetReady: targetReady,
            ContextVerified: true,
            InputOwned: true,
            EntryCostApproved: entryCostApproved,
            WatchStartVerified: true,
            SpecialistQualified: true,
            WholeGameResultVerified: wholeGameResultVerified,
            UserActive: userActive,
            Cancelled: cancelled,
            FocusPaused: focusPaused);
    }

    private static void Acknowledge(PetDanceVisit visit, PetDanceVisitPlan proposal, double time)
    {
        Assert.True(proposal.IsProposal, "Attempted to acknowledge a non-proposal plan.");
        visit.RecordDispatch(proposal.ProposalId!, proposal.Action, time + 1.0, accepted: true, inputReleased: true);
    }

    private PetDanceVisitPlan Step(PetDanceVisit visit, PetDanceScene scene, PetDanceLifecycleAction expected)
    {
        var frame = MakeFrame(scene);
        var proposal = visit.Observe(frame);
        Assert.True(proposal.IsProposal, $"Expected proposal for scene {scene}, got reason: {proposal.Reason}");
        Assert.Equal(expected, proposal.Action);
        Acknowledge(visit, proposal, frame.TimeMs);
        return proposal;
    }

    private void PlayThroughTeacher(PetDanceVisit visit)
    {
        Step(visit, PetDanceScene.DanceSigil, PetDanceLifecycleAction.InteractX);
        Step(visit, PetDanceScene.InstructionsUnselected, PetDanceLifecycleAction.ChooseLevel);
        Step(visit, PetDanceScene.InstructionsReady, PetDanceLifecycleAction.Play);

        var watchFrame = MakeFrame(PetDanceScene.Watch);
        var proposal = visit.Observe(watchFrame);
        Assert.Equal(PetDanceLifecycleAction.TeacherHandoff, proposal.Action);
        visit.RecordSpecialist(new(
            proposal.ProposalId!,
            watchFrame.TimeMs + 25.0,
            Options.TargetId,
            Geometry,
            Options.ContextId,
            Options.Arena,
            Started: true,
            Completed: true,
            InputReleased: true));
    }

    [Fact]
    public void LoadingPauseSuppressesProposalsAndResumesSeamlessly()
    {
        var visit = new PetDanceVisit(Options);

        Step(visit, PetDanceScene.DanceSigil, PetDanceLifecycleAction.InteractX);
        Step(visit, PetDanceScene.InstructionsReady, PetDanceLifecycleAction.Play);

        // Loading scene must withhold proposals and pause without stopping the visit
        var loadingFrame1 = MakeFrame(PetDanceScene.Loading);
        var plan1 = visit.Observe(loadingFrame1);
        Assert.False(plan1.IsProposal);
        Assert.False(plan1.Stopped);
        Assert.False(plan1.Completed);

        // Multiple consecutive loading frames keep pausing safely
        var loadingFrame2 = MakeFrame(PetDanceScene.Loading);
        var plan2 = visit.Observe(loadingFrame2);
        Assert.False(plan2.IsProposal);
        Assert.False(plan2.Stopped);

        // Transition from Loading to Watch resumes expected specialist handoff
        var watchFrame = MakeFrame(PetDanceScene.Watch);
        var watchPlan = visit.Observe(watchFrame);
        Assert.True(watchPlan.IsProposal);
        Assert.Equal(PetDanceLifecycleAction.TeacherHandoff, watchPlan.Action);
    }

    [Fact]
    public void PhysicalKeyTakeoverStopsVisitImmediatelyWhereasTargetUnreadyPauses()
    {
        var visit = new PetDanceVisit(Options);
        Step(visit, PetDanceScene.DanceSigil, PetDanceLifecycleAction.InteractX);

        // Case A: Target unready (e.g. window focus lost temporarily) pauses without stopping
        var unreadyFrame = MakeFrame(PetDanceScene.InstructionsReady, targetReady: false, focusPaused: true);
        var unreadyPlan = visit.Observe(unreadyFrame);
        Assert.False(unreadyPlan.IsProposal);
        Assert.False(unreadyPlan.Stopped, "Focus loss / unready target should pause, not permanently stop the visit.");

        // Regaining target readiness restores proposal capability
        var readyFrame = MakeFrame(PetDanceScene.InstructionsReady, targetReady: true);
        var readyPlan = visit.Observe(readyFrame);
        Assert.True(readyPlan.IsProposal);
        Assert.Equal(PetDanceLifecycleAction.Play, readyPlan.Action);
        Acknowledge(visit, readyPlan, readyFrame.TimeMs);

        // Case B: User active (physical key pressed while game focused) permanently stops the visit
        var takeoverFrame = MakeFrame(PetDanceScene.Watch, userActive: true);
        var takeoverPlan = visit.Observe(takeoverFrame);
        Assert.False(takeoverPlan.IsProposal);
        Assert.True(takeoverPlan.Stopped, "Physical user input must stop the visit immediately.");

        // Subsequent frames remain stopped
        var subsequentFrame = MakeFrame(PetDanceScene.Watch, userActive: false);
        var subsequentPlan = visit.Observe(subsequentFrame);
        Assert.False(subsequentPlan.IsProposal);
        Assert.True(subsequentPlan.Stopped);
    }

    [Fact]
    public void RepeatedFocusPausePreservesPendingReceiptWithoutReplayingAction()
    {
        var visit = new PetDanceVisit(Options);
        var sigil = MakeFrame(PetDanceScene.DanceSigil);
        var interact = visit.Observe(sigil);
        Assert.True(interact.IsProposal);
        for (var index = 0; index < 3; index++)
        {
            var pause = visit.Observe(MakeFrame(PetDanceScene.InstructionsReady, targetReady: false, focusPaused: true));
            Assert.False(pause.IsProposal);
            Assert.False(pause.Stopped);
            Assert.Equal("target-not-ready-paused", pause.Reason);
        }
        // Merely regaining focus cannot replace the missing input receipt.
        var ready = MakeFrame(PetDanceScene.InstructionsReady);
        var awaitingReceipt = visit.Observe(ready);
        Assert.False(awaitingReceipt.IsProposal);
        Assert.Equal("dispatch-receipt-required", awaitingReceipt.Reason);
        Acknowledge(visit, interact, ready.TimeMs);
        var resumed = visit.Observe(MakeFrame(PetDanceScene.InstructionsReady));
        Assert.True(resumed.IsProposal);
        Assert.Equal(PetDanceLifecycleAction.Play, resumed.Action);
        Assert.NotEqual(interact.ProposalId, resumed.ProposalId);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("geometry")]
    [InlineData("context")]
    [InlineData("owner")]
    public void FocusPauseNeverMasksChangedBindingOrLostInputOwnership(string changed)
    {
        var visit = new PetDanceVisit(Options);
        Step(visit, PetDanceScene.DanceSigil, PetDanceLifecycleAction.InteractX);
        var frame = MakeFrame(PetDanceScene.InstructionsReady, targetReady: false, focusPaused: true);
        frame = changed switch
        {
            "target" => frame with { TargetId = "another-window" },
            "geometry" => frame with { Geometry = new(1920, 1080) },
            "context" => frame with { ContextId = "another-visit" },
            _ => frame with { InputOwned = false }
        };
        var stopped = visit.Observe(frame);
        Assert.True(stopped.Stopped);
        Assert.False(stopped.IsProposal);
        var later = visit.Observe(MakeFrame(PetDanceScene.InstructionsReady));
        Assert.True(later.Stopped);
        Assert.False(later.IsProposal);
    }

    [Fact]
    public void FocusPauseRemainsBoundedByExistingTransitionDeadline()
    {
        var visit = new PetDanceVisit(Options with { TimeoutMs = 1000 });
        Step(visit, PetDanceScene.DanceSigil, PetDanceLifecycleAction.InteractX);
        var pause = MakeFrame(PetDanceScene.InstructionsReady, targetReady: false, focusPaused: true) with { TimeMs = 1100 };
        var stopped = visit.Observe(pause);
        Assert.True(stopped.Stopped);
        Assert.False(stopped.IsProposal);
        Assert.Contains("deadline-exceeded", stopped.Reason);
    }

    [Fact]
    public void SpecialistFailurePreventsResultAdvancementAndBlocksExit()
    {
        var visit = new PetDanceVisit(Options);
        Step(visit, PetDanceScene.DanceSigil, PetDanceLifecycleAction.InteractX);
        Step(visit, PetDanceScene.InstructionsReady, PetDanceLifecycleAction.Play);

        // Watch handoff occurs
        var watchFrame = MakeFrame(PetDanceScene.Watch);
        var handoff = visit.Observe(watchFrame);
        Assert.Equal(PetDanceLifecycleAction.TeacherHandoff, handoff.Action);

        // Specialist reports failure (e.g. aborted playback or timeout)
        visit.RecordSpecialist(new(
            handoff.ProposalId!,
            watchFrame.TimeMs + 50.0,
            Options.TargetId,
            Geometry,
            Options.ContextId,
            Options.Arena,
            Started: true,
            Completed: false, // Failure
            InputReleased: true));

        // Attempting to advance to GameResult must be strictly withheld
        var resultFrame = MakeFrame(PetDanceScene.GameResult);
        var resultPlan = visit.Observe(resultFrame);
        Assert.False(resultPlan.IsProposal, "GameResult must not be proposed if specialist did not successfully complete.");
        // A failed specialist receipt terminates the shared runner before the
        // scene-specific gate can apply. Preserve that stronger failure reason.
        Assert.True(resultPlan.Stopped);
        Assert.Equal("dispatch-unconfirmed", resultPlan.Reason);
        var exitPlan = visit.Observe(MakeFrame(PetDanceScene.SnackOffer));
        Assert.True(exitPlan.Stopped);
        Assert.False(exitPlan.IsProposal);
        Assert.False(exitPlan.Completed);
    }

    [Fact]
    public void UnknownSceneDoesNotCorruptVisitStateOrDispatchBlindRetries()
    {
        var visit = new PetDanceVisit(Options);
        Step(visit, PetDanceScene.DanceSigil, PetDanceLifecycleAction.InteractX);

        // Unknown scene yields no proposal and does not stop the routine
        var unknown1 = MakeFrame(PetDanceScene.Unknown);
        var plan1 = visit.Observe(unknown1);
        Assert.False(plan1.IsProposal);
        Assert.False(plan1.Stopped);

        // Once valid scene appears, the expected action is proposed
        Step(visit, PetDanceScene.InstructionsReady, PetDanceLifecycleAction.Play);

        // Intermittent unknown frame during loading/watch boundary
        var unknown2 = MakeFrame(PetDanceScene.Unknown);
        var plan2 = visit.Observe(unknown2);
        Assert.False(plan2.IsProposal);
        Assert.False(plan2.Stopped);

        // Expected Watch proposal is preserved
        var watchFrame = MakeFrame(PetDanceScene.Watch);
        var watchPlan = visit.Observe(watchFrame);
        Assert.True(watchPlan.IsProposal);
        Assert.Equal(PetDanceLifecycleAction.TeacherHandoff, watchPlan.Action);
    }

    [Fact]
    public void SnackScreenEnforcesNoFeedAndStrictlyProposesFinish()
    {
        var visit = new PetDanceVisit(Options);
        PlayThroughTeacher(visit);

        // Result screen advances with Next
        Step(visit, PetDanceScene.GameResult, PetDanceLifecycleAction.Next);

        // Snack offer scene must strictly propose FinishWithoutSnack
        var snackFrame = MakeFrame(PetDanceScene.SnackOffer);
        var snackPlan = visit.Observe(snackFrame);
        Assert.True(snackPlan.IsProposal);
        Assert.Equal(PetDanceLifecycleAction.FinishWithoutSnack, snackPlan.Action);
        Assert.Equal(ControlBounds, snackPlan.ActionBounds);
    }

    [Fact]
    public void InitialNativeIdleCannotCompleteVisitAndRequiresFullLifecycle()
    {
        var visit = new PetDanceVisit(Options);

        // Initial idle observation must never count as visit completion
        var initialIdleFrame = MakeFrame(PetDanceScene.NativeIdle);
        var initialPlan = visit.Observe(initialIdleFrame);
        Assert.False(initialPlan.Completed, "Initial native Idle frame must not complete an unplayed visit.");
        Assert.False(initialPlan.IsProposal);

        // Full visit progression
        PlayThroughTeacher(visit);
        Step(visit, PetDanceScene.GameResult, PetDanceLifecycleAction.Next);
        Step(visit, PetDanceScene.SnackOffer, PetDanceLifecycleAction.FinishWithoutSnack);
        Step(visit, PetDanceScene.OutsideAfterGame, PetDanceLifecycleAction.OpenMenu);
        Step(visit, PetDanceScene.MenuObserved, PetDanceLifecycleAction.SelectIdle);

        // Final native idle frame after menu select verifies completion
        var finalIdleFrame = MakeFrame(PetDanceScene.NativeIdle);
        var finalPlan = visit.Observe(finalIdleFrame);
        Assert.True(finalPlan.Completed, "Native Idle observed after menu selection must complete the visit.");
        Assert.True(visit.Status.Completed);
        Assert.True(visit.Status.ResultObserved);
    }

    [Fact]
    public void CancellationDuringVisitStopsRoutineAndRequiresInputRelease()
    {
        var visit = new PetDanceVisit(Options);
        Step(visit, PetDanceScene.DanceSigil, PetDanceLifecycleAction.InteractX);

        // Cancelled frame terminates proposals immediately
        var cancelledFrame = MakeFrame(PetDanceScene.InstructionsReady, cancelled: true);
        var cancelledPlan = visit.Observe(cancelledFrame);
        Assert.False(cancelledPlan.IsProposal);
        Assert.True(cancelledPlan.Stopped, "Cancelled observation must stop the visit.");

        // Routine runner marks status as stopped
        Assert.True(visit.Status.Stopped);
    }

    [Fact]
    public void EnergyGatingWithholdsPlayProposalWhenCostNotApproved()
    {
        var visit = new PetDanceVisit(Options);
        Step(visit, PetDanceScene.DanceSigil, PetDanceLifecycleAction.InteractX);

        // InstructionsReady with EntryCostApproved = false must withhold Play proposal
        var unapprovedFrame = MakeFrame(PetDanceScene.InstructionsReady, entryCostApproved: false);
        var unapprovedPlan = visit.Observe(unapprovedFrame);
        Assert.False(unapprovedPlan.IsProposal);
        Assert.Equal("entry-cost-not-approved", unapprovedPlan.Reason);

        // Once energy is verified and approved, Play is proposed
        var approvedFrame = MakeFrame(PetDanceScene.InstructionsReady, entryCostApproved: true);
        var approvedPlan = visit.Observe(approvedFrame);
        Assert.True(approvedPlan.IsProposal);
        Assert.Equal(PetDanceLifecycleAction.Play, approvedPlan.Action);
    }
}
