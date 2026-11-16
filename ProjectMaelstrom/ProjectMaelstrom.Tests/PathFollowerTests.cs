using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PathFollowerTests
{
    private static readonly PathPoint[] Straight = [new(0, 0, 0, 20), new(500, 0, 0, 20)];
    private static PathFollower Make(PathFollowerOptions? options = null, PathPoint[]? path = null) =>
        new(path ?? Straight, options ?? new("zone", "route-v1"));
    private static PathObservation Pose(double time, double x = 0, double y = 0, double z = 0, double heading = 0) =>
        new(x, y, z, heading, time, "zone", "route-v1");
    private static void Release(PathFollower follower, PathMotion motion, double duration = 50) =>
        follower.RecordApplied(motion, new(motion.SequenceId, true, motion.IssuedMs + duration,
            motion.IssuedMs, null, motion.IssuedMs + duration));

    [Fact]
    public void RotationFirstAdapterDoesNotAccumulateForwardStallEffort()
    {
        var follower = Make(new("zone", "route-v1", StuckMs: 100));
        for (int i = 0; i < 8; i++)
        {
            var now = i * 100.0;
            var motion = follower.Observe(Pose(now, heading: 0.3), now);
            Assert.True(motion.Forward); Assert.NotEqual(0, motion.Turn);
            Assert.Equal("follow", motion.State);
            follower.RecordApplied(motion, new(motion.SequenceId, true, now + 50, now, null, now + 50, ForwardApplied: false));
        }
    }

    [Theory]
    [InlineData(0.0, true, 0)]
    [InlineData(1.0, false, -1)]
    [InlineData(-1.0, false, 1)]
    public void UsesCanonicalHeadingAndSignedTurnWithoutKeyBindings(double heading, bool forward, int turn)
    {
        var motion = Make().Observe(Pose(0, heading: heading), 0);
        Assert.Equal(forward, motion.Forward);
        Assert.Equal(turn, motion.Turn);
        Assert.Equal(100, motion.ValidUntilMs);
        Assert.Equal(1, motion.SequenceId);
    }

    [Fact]
    public void AcceptedDispatchDoesNotPermitStackingBeforeRelease()
    {
        var follower = Make();
        var motion = follower.Observe(Pose(0), 0);
        follower.RecordApplied(motion, new(motion.SequenceId, true, 1, 0));
        var blocked = follower.Observe(Pose(10, x: 5), 10);
        Assert.Equal("awaiting-release", blocked.State);
        Assert.False(blocked.Forward);
        follower.RecordApplied(motion, new(motion.SequenceId, true, 50, 0, 20, 50));
        Assert.Equal("follow", follower.Observe(Pose(100, x: 10), 100).State);
    }

    [Fact]
    public void MissingReceiptEventuallyInvalidatesButPreservesReleaseRequirement()
    {
        var follower = Make();
        var motion = follower.Observe(Pose(0), 0);
        Assert.Equal("awaiting-release", follower.Observe(Pose(400), 400).State);
        follower.RecordApplied(motion, new(motion.SequenceId, true, 400, 0, null, 400));
        var outcome = follower.Observe(Pose(450), 450);
        Assert.Equal("invalidated", outcome.State);
        Assert.Equal("release-confirmation-overdue", outcome.Reason);
    }

    [Fact]
    public void StopDuringReleaseIsTerminalAfterReleaseAndNeverReplans()
    {
        var follower = Make();
        var motion = follower.Observe(Pose(0), 0);
        Assert.Equal("awaiting-release", follower.Observe(Pose(10) with { Stopped = true }, 10).State);
        Release(follower, motion);
        Assert.Equal("stopped", follower.Observe(Pose(60), 60).State);
        Assert.False(follower.Observe(Pose(70), 70).Forward);
    }

    [Fact]
    public void SameZoneLoadingInvalidatesEvenAfterLoadingClears()
    {
        var follower = Make();
        Assert.Equal("invalidated", follower.Observe(Pose(0) with { Loading = true }, 0).State);
        Assert.Equal("loading-requires-new-route-context", follower.Observe(Pose(100), 100).Reason);
    }

    [Theory]
    [InlineData("other-zone", "route-v1")]
    [InlineData("zone", "route-v2")]
    public void ZoneOrAssetInstanceProfileContextChangeInvalidates(string zone, string context)
    {
        var result = Make().Observe(Pose(0) with { Zone = zone, Context = context }, 0);
        Assert.Equal("localization-context-changed", result.Reason);
    }

    [Fact]
    public void FocusPauseAllowsMinutesButRevalidatesCorridorOnReturn()
    {
        var follower = Make();
        Assert.Equal("paused", follower.Observe(Pose(0) with { Focused = false }, 0).State);
        Assert.Equal("paused", follower.Observe(Pose(120000) with { Focused = false }, 120000).State);
        var resumed = follower.Observe(Pose(120100, y: 80), 120100);
        Assert.Equal("outside-qualified-corridor-or-elevation", resumed.Reason);
    }

    [Fact]
    public void FocusLossWithPendingReleaseDoesNotCreateFalseStallOrGap()
    {
        var follower = Make();
        var motion = follower.Observe(Pose(0), 0);
        Assert.Equal("awaiting-release", follower.Observe(Pose(20) with { Focused = false }, 20).State);
        Release(follower, motion);
        Assert.Equal("paused", follower.Observe(Pose(10000) with { Focused = false }, 10000).State);
        Assert.Equal("follow", follower.Observe(Pose(10100), 10100).State);
    }

    [Fact]
    public void StaleRepeatedSamplesCannotConfirmArrivalAndWaitIsBounded()
    {
        var follower = Make(new("zone", "route-v1", MaximumWaitMs: 200));
        Assert.Equal("confirm", follower.Observe(Pose(0, x: 500), 0).State);
        Assert.Equal("waiting", follower.Observe(Pose(0, x: 500), 100).State);
        Assert.Equal("fresh-pose-wait-exhausted", follower.Observe(Pose(0, x: 500), 400).Reason);
    }

    [Fact]
    public void ArrivalRequiresTwoDistinctFreshSamplesWithoutOutstandingInput()
    {
        var follower = Make();
        Assert.Equal("confirm", follower.Observe(Pose(0, x: 500), 0).State);
        var arrived = follower.Observe(Pose(50, x: 500), 50);
        Assert.Equal("arrived", arrived.State);
        Assert.Equal("coordinate-arrival-only", arrived.Reason);
    }

    [Fact]
    public void OverlappingFloorIsNotAcceptedByHorizontalProximity()
    {
        Assert.Equal("outside-qualified-corridor-or-elevation", Make().Observe(Pose(0, z: -45), 0).Reason);
    }

    [Fact]
    public void SharpCornerStopsThenAlignsWithoutLookingAcrossIt()
    {
        PathPoint[] path = [new(0, 0, 0, 20), new(100, 0, 0, 20), new(100, 100, 0, 20)];
        var follower = Make(path: path);
        var before = follower.Observe(Pose(0, x: 60), 0);
        Assert.Equal(0, before.Turn);
        Release(follower, before);
        Assert.Equal("corner", follower.Observe(Pose(100, x: 100), 100).State);
        var turn = follower.Observe(Pose(150, x: 100), 150);
        Assert.False(turn.Forward);
        Assert.Equal(1, turn.Turn);
        Release(follower, turn);
        var aligned = follower.Observe(Pose(250, x: 100, heading: Math.PI / 2), 250);
        Assert.True(aligned.Forward);
    }

    [Fact]
    public void OnlyReleasedForwardEffortCountsTowardStall()
    {
        var follower = Make(new("zone", "route-v1", StuckMs: 100));
        var turn = follower.Observe(Pose(0, heading: Math.PI / 2), 0);
        Release(follower, turn);
        var first = follower.Observe(Pose(100), 100);
        Release(follower, first);
        var second = follower.Observe(Pose(200), 200);
        Assert.Equal("follow", second.State);
        Release(follower, second);
        Assert.Equal("forward-progress-stalled", follower.Observe(Pose(300), 300).Reason);
    }

    [Fact]
    public void RejectedDispatchDoesNotCountAsMotionOrStall()
    {
        var follower = Make(new("zone", "route-v1", StuckMs: 1));
        var motion = follower.Observe(Pose(0), 0);
        follower.RecordApplied(motion, new(motion.SequenceId, false, 1));
        Assert.Equal("follow", follower.Observe(Pose(100), 100).State);
    }

    [Fact]
    public void ReceiptCannotApplyExpiredOrDifferentIntent()
    {
        var follower = Make();
        var motion = follower.Observe(Pose(0), 0);
        Assert.Throws<InvalidOperationException>(() => follower.RecordApplied(motion, new(999, false, 1)));
        Assert.Throws<InvalidDataException>(() => follower.RecordApplied(motion, new(motion.SequenceId, true, 150, 101, null, 150)));
        Assert.Equal("awaiting-release", follower.Observe(Pose(160), 160).State);
    }

    [Fact]
    public void ReceiptChronologyAndClockCannotRunBackward()
    {
        var follower = Make();
        var motion = follower.Observe(Pose(0), 0);
        Assert.Throws<InvalidDataException>(() => follower.RecordApplied(motion, new(motion.SequenceId, true, 50, 20, 10, 50)));
        Release(follower, motion);
        Assert.Equal("invalid-monotonic-clock", follower.Observe(Pose(25), 25).Reason);
    }

    [Fact]
    public void FreshnessAndWallBudgetsCannotBeAvoidedByPausingForever()
    {
        var follower = Make(new("zone", "route-v1", MaximumActiveMs: 100, MaximumWallMs: 1000));
        Assert.Equal("paused", follower.Observe(Pose(0) with { Focused = false }, 0).State);
        Assert.Equal("route-wall-budget-exhausted", follower.Observe(Pose(1100) with { Focused = false }, 1100).Reason);
        Assert.Equal("waiting", Make().Observe(Pose(0), 200).State);
    }

    [Fact]
    public void UnknownClearanceOrInvalidGeometryCannotBecomeRunnable()
    {
        Assert.Throws<InvalidDataException>(() => Make(path: [new(0, 0, 0, 0), new(100, 0, 0, 20)]));
        Assert.Throws<InvalidDataException>(() => Make(path: [new(0, 0, 0, 20), new(0, 0, 40, 20)]));
        Assert.Throws<InvalidDataException>(() => Make(new("zone", "route-v1", MaximumWaitMs: double.NaN)));
    }
}
