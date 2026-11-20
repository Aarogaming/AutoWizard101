using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class HeadingFeedbackTests
{
    private const long Now = 638000000000000000;
    private static WaypointPose Pose(double heading, int step = 0, double x = 10, double y = 20, double z = 30) =>
        new(x, y, z, heading, Now + step * TimeSpan.TicksPerMillisecond * 100);
    private static HeadingDecision Observe(HeadingFeedback controller, WaypointPose pose) => controller.Observe(pose, pose.Timestamp);

    [Theory]
    [InlineData(1.0, 2.0, "D")]
    [InlineData(2.0, 1.0, "A")]
    [InlineData(6.2, 0.1, "D")]
    [InlineData(0.1, 6.2, "A")]
    public void ChoosesShortestWrappedTurnAndCapsPulse(double start, double target, string key)
    {
        var plan = Observe(new(target, 300), Pose(start));
        Assert.Equal("turn", plan.State); Assert.Equal(key, plan.Key);
        Assert.InRange(plan.PulseMs, 20, 300);
    }

    [Fact]
    public void ArrivalRequiresTwoIndependentNewSamplesWithoutInput()
    {
        var controller = new HeadingFeedback(1, 300);
        var first = Observe(controller, Pose(1.02));
        Assert.Equal("confirm", first.State); Assert.Equal(0, first.PulseMs);
        var second = Observe(controller, Pose(0.98, 1));
        Assert.Equal("arrived", second.State); Assert.Null(second.Key); Assert.Equal(0, controller.Moves);
    }

    [Fact]
    public void RepeatedPoseNeverCountsAsSecondConfirmation()
    {
        var controller = new HeadingFeedback(1, 300);
        Observe(controller, Pose(1));
        Assert.Equal("pose-not-newer", Observe(controller, Pose(1)).Reason);
    }

    [Theory]
    [InlineData(double.NaN, 300)]
    [InlineData(double.PositiveInfinity, 300)]
    [InlineData(-0.1, 300)]
    [InlineData(7, 300)]
    [InlineData(1, 19)]
    [InlineData(1, 301)]
    public void InvalidTargetOrBudgetFailsClosed(double target, int pulse)
    {
        Assert.Equal("stopped", Observe(new(target, pulse), Pose(0)).State);
    }

    [Fact]
    public void StaleFutureOrNonfinitePoseDoesNotPropose()
    {
        Assert.Equal("stopped", new HeadingFeedback(1, 300).Observe(Pose(0), Now + TimeSpan.FromSeconds(3).Ticks).State);
        Assert.Equal("stopped", new HeadingFeedback(1, 300).Observe(Pose(0), Now - 1).State);
        Assert.Equal("stopped", Observe(new(1, 300), Pose(double.NaN)).State);
        Assert.Equal("stopped", Observe(new(1, 300), Pose(0, x: double.PositiveInfinity)).State);
    }

    [Fact]
    public void VerifiedMeasuredTurnUpdatesRateAndConvergesWithinFourAttempts()
    {
        var controller = new HeadingFeedback(2.435511, 300);
        var pose = Pose(3.197033);
        for (var index = 0; index < 4; index++)
        {
            var proposal = Observe(controller, pose);
            if (proposal.State == "confirm")
            {
                Assert.Equal("arrived", Observe(controller, pose with { Timestamp = pose.Timestamp + 1 }).State);
                Assert.InRange(controller.Moves, 1, 4); return;
            }
            Assert.Equal("turn", proposal.State);
            controller.RecordTurn(proposal, completed: true);
            var measured = (proposal.Key == "A" ? -1 : 1) * 2.5384066667 * proposal.PulseMs / 1000;
            pose = pose with { Heading = pose.Heading + measured, Timestamp = pose.Timestamp + TimeSpan.TicksPerSecond };
        }
        var final = Observe(controller, pose);
        Assert.Equal("confirm", final.State);
        Assert.Equal("arrived", Observe(controller, pose with { Timestamp = pose.Timestamp + 1 }).State);
    }

    [Fact]
    public void RateLearnsOnlyAfterCompletedReceiptAndNewPose()
    {
        var controller = new HeadingFeedback(2, 300);
        var first = Observe(controller, Pose(0));
        controller.RecordTurn(first, completed: false);
        var second = Observe(controller, Pose(0.2, 1));
        Assert.Equal(2.5, second.RadiansPerSecond);
        controller.RecordTurn(second, completed: true);
        var third = Observe(controller, Pose(0.8, 2));
        Assert.Equal(2.0, third.RadiansPerSecond, 6);
    }

    [Fact]
    public void FocusDiscardDoesNotConsumeMoveOrLearnRate()
    {
        var controller = new HeadingFeedback(2, 300);
        var first = Observe(controller, Pose(0)); controller.DiscardProposal(first);
        Assert.Equal(0, controller.Moves);
        var next = Observe(controller, Pose(0, 1));
        Assert.Equal("turn", next.State); Assert.Equal(2.5, next.RadiansPerSecond);
    }

    [Fact]
    public void MissingReceiptAndWrongReceiptCannotAdvance()
    {
        var controller = new HeadingFeedback(2, 300);
        var first = Observe(controller, Pose(0));
        Assert.Throws<InvalidOperationException>(() => controller.RecordTurn(first with { PulseMs = 20 }, true));
        Assert.Equal("turn-receipt-required", Observe(controller, Pose(0.1, 1)).Reason);
        Assert.Equal("stopped", Observe(controller, Pose(2, 2)).State);
    }

    [Fact]
    public void TranslationAndWrongDirectionStopBeforeNextInput()
    {
        var controller = new HeadingFeedback(1, 300);
        var first = Observe(controller, Pose(0)); controller.RecordTurn(first, true);
        Assert.Equal("unexpected-translation-during-turn", Observe(controller, Pose(0.5, 1, x: 31)).Reason);
        controller = new(1, 300); first = Observe(controller, Pose(0)); controller.RecordTurn(first, true);
        Assert.Equal("turn-direction-mismatch", Observe(controller, Pose(-0.1, 1)).Reason);
    }

    [Fact]
    public void RepeatedStallsStopRatherThanKeepTurning()
    {
        var controller = new HeadingFeedback(2, 300);
        for (var index = 0; index < 2; index++)
        {
            var plan = Observe(controller, Pose(0, index)); Assert.Equal("turn", plan.State);
            controller.RecordTurn(plan, true);
        }
        Assert.Equal("turn-stalled", Observe(controller, Pose(0, 2)).Reason);
    }

    [Fact]
    public void FourAttemptBudgetIsIndependentOfSuccessfulRateLearning()
    {
        var controller = new HeadingFeedback(3, 20);
        for (var index = 0; index < 4; index++)
        {
            var plan = Observe(controller, Pose(index * 0.02, index)); Assert.Equal("turn", plan.State);
            controller.RecordTurn(plan, false);
        }
        Assert.Equal("turn-budget-exhausted", Observe(controller, Pose(0.08, 4)).Reason);
        Assert.Equal(4, controller.Moves);
    }
}
