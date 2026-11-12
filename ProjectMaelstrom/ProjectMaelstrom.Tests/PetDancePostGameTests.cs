using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class PetDancePostGameTests
{
    private static readonly Size Geometry = new(1282, 760);
    private static readonly PetDancePostGameOptions Options = new("w101-window", Geometry, "dance-visit-1", Enabled: true);
    private double _time;

    private PetDancePostGameObservation Frame(PetDanceScene scene, RectangleF? bounds = null) => new(++_time,
        new(scene, bounds, "test-scene"), Options.TargetId, Geometry, Options.ContextId,
        Fresh: true, TargetReady: true, ContextVerified: true, InputOwned: true,
        GameplayCompleted: true, GameplayInputReleased: true);
    private static readonly RectangleF Control = new(760, 574, 50, 13);

    private void Advance(PetDancePostGame operation, PetDanceScene scene, PetDanceLifecycleAction action)
    {
        var plan = operation.Observe(Frame(scene, Control));
        Assert.True(plan.IsProposal);
        Assert.Equal(action, plan.Action);
        Assert.False(plan.Completed);
        operation.RecordDispatch(plan.Action, accepted: true, inputReleased: true);
    }

    private void ReturnOutside(PetDancePostGame operation)
    {
        Advance(operation, PetDanceScene.GameResult, PetDanceLifecycleAction.Next);
        Advance(operation, PetDanceScene.SnackOffer, PetDanceLifecycleAction.FinishWithoutSnack);
        Advance(operation, PetDanceScene.OutsideAfterGame, PetDanceLifecycleAction.OpenMenu);
    }

    [Fact]
    public void CompletedGameReturnsThroughObservedMenusWithoutFeeding()
    {
        var operation = new PetDancePostGame(Options);
        ReturnOutside(operation);
        Advance(operation, PetDanceScene.MenuObserved, PetDanceLifecycleAction.SelectIdle);
        var done = operation.Observe(Frame(PetDanceScene.NativeIdle));
        Assert.True(done.Completed);
        Assert.True(done.ResultObserved);
        Assert.False(done.IsProposal);
        Assert.False(done.Stopped);
        Assert.Equal(PetDanceLifecycleAction.Done, done.Action);
        Assert.True(operation.Observe(Frame(PetDanceScene.NativeIdle)).Completed);
        Assert.False(operation.Observe(Frame(PetDanceScene.OutsideAfterGame)).IsProposal);
    }

    [Fact]
    public void DisabledByDefaultEvenWithOtherwiseValidEvidence()
    {
        var operation = new PetDancePostGame(new(Options.TargetId, Geometry, Options.ContextId));
        var plan = operation.Observe(Frame(PetDanceScene.GameResult, Control));
        Assert.False(plan.IsProposal);
        Assert.True(plan.Stopped);
        Assert.Equal("postgame-disabled", plan.Reason);
    }

    [Fact]
    public void RealCompletedTeacherAndReleasedGameplayInputAreBothRequired()
    {
        var operation = new PetDancePostGame(Options);
        var missingCompletion = operation.Observe(Frame(PetDanceScene.GameResult, Control) with { GameplayCompleted = false });
        Assert.False(missingCompletion.ResultObserved);
        Assert.False(missingCompletion.IsProposal);
        Assert.False(operation.Observe(Frame(PetDanceScene.GameResult, Control) with { GameplayInputReleased = false }).IsProposal);
        Assert.True(operation.Observe(Frame(PetDanceScene.GameResult, Control)).IsProposal);
    }

    [Fact]
    public void InitialIdleAndRoundFeedbackCannotInventAPlayedGame()
    {
        var operation = new PetDancePostGame(Options);
        foreach (var scene in new[] { PetDanceScene.NativeIdle, PetDanceScene.RoundFeedback, PetDanceScene.SnackOffer,
            PetDanceScene.OutsideAfterGame, PetDanceScene.MenuObserved })
        {
            var plan = operation.Observe(Frame(scene, Control));
            Assert.False(plan.IsProposal);
            Assert.False(plan.ResultObserved);
            Assert.False(plan.Completed);
        }
    }

    [Fact]
    public void EveryActionNeedsItsOwnDispatchReceiptAndNewObservedOutcome()
    {
        var operation = new PetDancePostGame(Options);
        Assert.True(operation.Observe(Frame(PetDanceScene.GameResult, Control)).IsProposal);
        Assert.False(operation.Observe(Frame(PetDanceScene.GameResult, Control)).IsProposal);
        Assert.False(operation.Observe(Frame(PetDanceScene.SnackOffer, Control)).IsProposal);
        operation.RecordDispatch(PetDanceLifecycleAction.Next, true, true);
        Assert.False(operation.Observe(Frame(PetDanceScene.GameResult, Control)).IsProposal);
        Advance(operation, PetDanceScene.SnackOffer, PetDanceLifecycleAction.FinishWithoutSnack);
        Assert.False(operation.Observe(Frame(PetDanceScene.SnackOffer, Control)).IsProposal);
        Assert.False(operation.Observe(Frame(PetDanceScene.MenuObserved, Control)).IsProposal);
        Advance(operation, PetDanceScene.OutsideAfterGame, PetDanceLifecycleAction.OpenMenu);
        Assert.False(operation.Observe(Frame(PetDanceScene.OutsideAfterGame)).IsProposal);
        Advance(operation, PetDanceScene.MenuObserved, PetDanceLifecycleAction.SelectIdle);
        Assert.False(operation.Observe(Frame(PetDanceScene.MenuObserved, Control)).IsProposal);
        Assert.True(operation.Observe(Frame(PetDanceScene.NativeIdle)).Completed);
    }

    [Fact]
    public void WeakOutsidePromptRequiresThisVisitsSnackFinishTransition()
    {
        var operation = new PetDancePostGame(Options);
        Advance(operation, PetDanceScene.GameResult, PetDanceLifecycleAction.Next);
        Assert.False(operation.Observe(Frame(PetDanceScene.OutsideAfterGame)).IsProposal);
        var finish = operation.Observe(Frame(PetDanceScene.SnackOffer, Control));
        Assert.Equal(PetDanceLifecycleAction.FinishWithoutSnack, finish.Action);
        Assert.False(operation.Observe(Frame(PetDanceScene.OutsideAfterGame)).IsProposal);
        operation.RecordDispatch(finish.Action, true, true);
        Assert.Equal(PetDanceLifecycleAction.OpenMenu, operation.Observe(Frame(PetDanceScene.OutsideAfterGame)).Action);
    }

    [Fact]
    public void UnknownAndLoadingNeverSendEscapeOrLoseTheResult()
    {
        var operation = new PetDancePostGame(Options);
        Advance(operation, PetDanceScene.GameResult, PetDanceLifecycleAction.Next);
        foreach (var scene in new[] { PetDanceScene.Unknown, PetDanceScene.Loading, PetDanceScene.Unknown })
        {
            var plan = operation.Observe(Frame(scene));
            Assert.Equal(PetDanceLifecycleAction.Wait, plan.Action);
            Assert.True(plan.ResultObserved);
            Assert.False(plan.Completed);
        }
        Assert.True(operation.Observe(Frame(PetDanceScene.SnackOffer, Control)).IsProposal);
    }

    [Fact]
    public void StaleUnverifiedAndRepeatedFramesCannotAdvance()
    {
        var operation = new PetDancePostGame(Options);
        var frame = Frame(PetDanceScene.GameResult, Control);
        Assert.False(operation.Observe(frame with { Fresh = false }).IsProposal);
        Assert.False(operation.Observe(frame).IsProposal);
        Assert.False(operation.Observe(Frame(PetDanceScene.GameResult, Control) with { ContextVerified = false }).IsProposal);
        Assert.False(operation.Observe(Frame(PetDanceScene.GameResult, Control) with { TimeMs = double.NaN }).IsProposal);
        Assert.True(operation.Observe(Frame(PetDanceScene.GameResult, Control)).IsProposal);
    }

    [Fact]
    public void CancellationUserInputFocusAndOwnershipLossLatchAnInterruption()
    {
        foreach (var change in new Func<PetDancePostGameObservation, PetDancePostGameObservation>[]
        {
            frame => frame with { Cancelled = true }, frame => frame with { UserActive = true },
            frame => frame with { TargetReady = false }, frame => frame with { InputOwned = false },
            frame => frame with { TargetId = "other-window" }, frame => frame with { ContextId = "new-visit" },
            frame => frame with { Geometry = new Size(800, 600) }
        })
        {
            var operation = new PetDancePostGame(Options);
            Advance(operation, PetDanceScene.GameResult, PetDanceLifecycleAction.Next);
            var interrupted = operation.Observe(change(Frame(PetDanceScene.SnackOffer, Control)));
            Assert.True(interrupted.Stopped);
            Assert.True(interrupted.ResultObserved);
            Assert.False(interrupted.Completed);
            Assert.False(interrupted.IsProposal);
            Assert.True(operation.Observe(Frame(PetDanceScene.SnackOffer, Control)).Stopped);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void RejectedOrUnreleasedDispatchCannotRetry(bool accepted, bool released)
    {
        var operation = new PetDancePostGame(Options);
        operation.Observe(Frame(PetDanceScene.GameResult, Control));
        operation.RecordDispatch(PetDanceLifecycleAction.Next, accepted, released);
        var plan = operation.Observe(Frame(PetDanceScene.GameResult, Control));
        Assert.True(plan.Stopped);
        Assert.False(plan.IsProposal);
        Assert.True(plan.ResultObserved);
    }

    [Fact]
    public void UnsolicitedAndWrongActionReceiptsCannotSkipSteps()
    {
        var operation = new PetDancePostGame(Options);
        operation.RecordDispatch(PetDanceLifecycleAction.Next, true, true);
        Assert.True(operation.Observe(Frame(PetDanceScene.GameResult, Control)).Stopped);
        operation = new(Options);
        operation.Observe(Frame(PetDanceScene.GameResult, Control));
        operation.RecordDispatch(PetDanceLifecycleAction.SelectIdle, true, true);
        Assert.False(operation.Observe(Frame(PetDanceScene.NativeIdle)).Completed);
        Assert.True(operation.Observe(Frame(PetDanceScene.GameResult, Control)).Stopped);
    }

    [Fact]
    public void MissingOrInvalidClickBoundsAreNeverGuessed()
    {
        var operation = new PetDancePostGame(Options);
        foreach (var bounds in new RectangleF?[] { null, new(-1, 1, 20, 20), new(0, 0, float.NaN, 20), new(0, 0, 2000, 20) })
            Assert.False(operation.Observe(Frame(PetDanceScene.GameResult, bounds)).IsProposal);
        Assert.True(operation.Observe(Frame(PetDanceScene.GameResult, Control)).IsProposal);
    }

    [Fact]
    public void LoadingAndStalenessCannotExtendTheBoundedCleanupDeadline()
    {
        var operation = new PetDancePostGame(Options with { TimeoutMs = 1000 });
        Advance(operation, PetDanceScene.GameResult, PetDanceLifecycleAction.Next);
        var plan = operation.Observe(Frame(PetDanceScene.Loading) with { TimeMs = 1001, Fresh = false });
        Assert.True(plan.Stopped);
        Assert.True(plan.ResultObserved);
        Assert.False(plan.Completed);
        Assert.Equal("postgame-deadline-exceeded", plan.Reason);
    }

    [Fact]
    public void InvalidBindingOrUnboundedDeadlineIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new PetDancePostGame(Options with { TargetId = "" }));
        Assert.Throws<ArgumentException>(() => new PetDancePostGame(Options with { Geometry = Size.Empty }));
        foreach (var timeout in new[] { 0d, -1, double.NaN, double.PositiveInfinity, 180001 })
            Assert.Throws<ArgumentException>(() => new PetDancePostGame(Options with { TimeoutMs = timeout }));
    }
}

public class PetDancePostGameClassifierTests
{
    private static readonly Size Geometry = new(1282, 760);
    private static PetDanceMenuWord Word(string text, float x, float y, float width, float height = 15) =>
        new(text, new(x, y, width, height));
    private static PetDancePostGameScene Read(params PetDanceMenuWord[] words) =>
        PetDancePostGameClassifier.Classify(new(Geometry, words));

    // Word text and bounds below are copied from offline local Windows OCR of the
    // recorded postgame frames (dance-menu-review-3.json), not hand-drawn UI positions.
    private static PetDanceMenuWord[] Reward => [Word("YOUR", 582, 212, 44), Word("REWARD", 631, 212, 67), Word("NEXT", 776, 574, 49, 11)];
    private static PetDanceMenuWord[] Snack => [Word("FEED", 581, 212, 35), Word("yotn", 621, 212, 44), Word("PET", 670, 212, 31), Word("FINISH", 465, 574, 43, 12)];
    private static PetDanceMenuWord[] Menu => [Word("GRAPHICS", 600, 190, 66, 13), Word("0K", 770, 550, 21, 13), Word("IDLE", 572, 550, 28, 12), Word("HELP", 666, 550, 34, 12)];
    private static PetDanceMenuWord[] Outside => [Word("to", 689, 676, 15, 11), Word("Play", 711, 675, 27, 16), Word("Press", 568, 675, 36, 12)];
    private static PetDanceMenuWord[] Idle => [Word("YOU", 589, 55, 32, 13), Word("ARE", 625, 55, 32, 13), Word("IDLE", 660, 55, 30, 13), Word("IDLE'", 659.6667f, 52.666664f, 35.333332f, 15.333333f)];

    [Fact]
    public void RecordedMenuWordsLocateTheActualSafeControl()
    {
        var result = Read(Reward);
        Assert.Equal(PetDanceScene.GameResult, result.Scene);
        Assert.Equal(Reward[^1].Bounds, result.ActionBounds);
        var snack = Read(Snack);
        Assert.Equal(PetDanceScene.SnackOffer, snack.Scene);
        Assert.Equal(Snack[^1].Bounds, snack.ActionBounds);
        var menu = Read(Menu);
        Assert.Equal(PetDanceScene.MenuObserved, menu.Scene);
        Assert.Equal(Menu[2].Bounds, menu.ActionBounds);
        Assert.Equal(PetDanceScene.OutsideAfterGame, Read(Outside).Scene);
        Assert.Null(Read(Outside).ActionBounds);
        Assert.Equal(PetDanceScene.NativeIdle, Read(Idle).Scene);
    }

    [Theory]
    [InlineData(.5f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void RegionAndWordGeometryScaleWithTheCapturedImage(float scale)
    {
        foreach (var words in new[] { Reward, Snack, Menu, Outside, Idle })
        {
            var expected = Read(words).Scene;
            var scaled = words.Select(word => word with
            {
                Bounds = new(word.Bounds.X * scale,
                word.Bounds.Y * scale, word.Bounds.Width * scale, word.Bounds.Height * scale)
            }).ToArray();
            Assert.Equal(expected, PetDancePostGameClassifier.Classify(new(
                new Size((int)(Geometry.Width * scale), (int)(Geometry.Height * scale)), scaled)).Scene);
        }
    }

    [Fact]
    public void IndependentUiScaleChangesWithinTheSameWindowKeepRecognition()
    {
        foreach (var words in new[] { Reward, Snack, Menu })
        {
            var resized = words.Select(word => word with
            {
                Bounds = new(
                641 + (word.Bounds.X - 641) * .75f, 380 + (word.Bounds.Y - 380) * .75f,
                word.Bounds.Width * .75f, word.Bounds.Height * .75f)
            }).ToArray();
            Assert.Equal(Read(words).Scene, Read(resized).Scene);
        }
    }

    [Fact]
    public void IdleBannerTakesPrecedenceOverTheStillVisibleSigil()
    {
        Assert.Equal(PetDanceScene.NativeIdle, Read([.. Idle, .. Outside]).Scene);
        var accented = Idle.Select(word => word.Text == "ARE" ? word with { Text = "ÄRE" } : word).ToArray();
        Assert.Equal(PetDanceScene.NativeIdle, Read(accented).Scene);
    }

    [Fact]
    public void FeedbackButtonNamesAndChatTextAloneAreNotMenus()
    {
        Assert.Equal(PetDanceScene.Unknown, Read(Word("PERFECT!", 600, 370, 100), Word("NEXT", 776, 574, 49)).Scene);
        Assert.Equal(PetDanceScene.Unknown, Read(Word("FEED", 465, 574, 43), Word("PET", 520, 574, 30), Word("FINISH", 760, 574, 50)).Scene);
        Assert.Equal(PetDanceScene.Unknown, Read(Word("IDLE", 572, 550, 28)).Scene);
        var chat = Idle.Select(word => word with { Bounds = new(word.Bounds.X - 560, word.Bounds.Y + 600, word.Bounds.Width, word.Bounds.Height) }).ToArray();
        Assert.Equal(PetDanceScene.Unknown, Read(chat).Scene);
    }

    [Fact]
    public void DistantDuplicateControlsAndMisalignedMenuLabelsAreAmbiguous()
    {
        Assert.Equal(PetDanceScene.Unknown, Read([.. Reward, Word("NEXT", 480, 574, 49)]).Scene);
        Assert.Equal(PetDanceScene.Unknown, Read([.. Snack, Word("FINISH", 760, 574, 43)]).Scene);
        Assert.Equal(PetDanceScene.Unknown, Read(Menu.Select(word => word.Text == "HELP"
            ? word with { Bounds = new(666, 620, 34, 12) } : word).ToArray()).Scene);
    }

    [Fact]
    public void OutsideCandidateRequiresLocalizedSameLinePromptAndNoModalHeader()
    {
        Assert.Equal(PetDanceScene.Unknown, Read(Outside.Where(word => word.Text != "Press").ToArray()).Scene);
        Assert.Equal(PetDanceScene.Unknown, Read(Outside.Select(word => word.Text == "Press"
            ? word with { Bounds = new(568, 620, 36, 12) } : word).ToArray()).Scene);
        Assert.Equal(PetDanceScene.Unknown, Read([.. Outside, .. Reward.Take(2)]).Scene);
        Assert.Equal(PetDanceScene.Unknown, Read([.. Outside, Word("GRAPHICS", 600, 190, 66)]).Scene);
        Assert.Equal(PetDanceScene.Unknown, Read([.. Outside, Word("DANCE", 580, 212, 45), Word("GAME", 630, 212, 40)]).Scene);
    }

    [Fact]
    public void MalformedOrUnboundedWordGeometryIsRejected()
    {
        Assert.Equal(PetDanceScene.Unknown, Read([.. Reward, Word("bad", -1, 0, 10)]).Scene);
        Assert.Equal(PetDanceScene.Unknown, Read([.. Reward, Word("bad", 0, 0, float.NaN)]).Scene);
        Assert.Equal(PetDanceScene.Unknown, PetDancePostGameClassifier.Classify(new(Size.Empty, Reward)).Scene);
        Assert.Equal(PetDanceScene.Unknown, Read(Enumerable.Repeat(Reward[0], 513).ToArray()).Scene);
    }
}
