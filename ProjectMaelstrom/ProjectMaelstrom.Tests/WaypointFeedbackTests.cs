using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class WaypointFeedbackTests
{
    [Fact]
    public void FeedbackCliIsExplicitAndRequiresBoundedMeasuredWaypoint()
    {
        var root = Path.GetTempPath();
        string[] args = ["--cli", "navigation-visit", "--output", Path.Combine(root, Guid.NewGuid() + ".json"),
            "--workspace", root, "--window", "42", "--process-id", "123", "--started-utc-ticks", "100",
            "--enable-client-observation", "--enable-ui-tree", "--enable-module-actions", "--key", "W", "--pulse-ms", "40"];
        Assert.False(MaelstromCli.Parse(args, root).FeedbackWaypoint);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--feedback-waypoint", "true"], root));
        Assert.True(MaelstromCli.Parse([.. args, "--include-pose-context", "--waypoint-distance", "60", "--feedback-waypoint", "true"], root).FeedbackWaypoint);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--include-pose-context", "--waypoint-distance", "60", "--feedback-waypoint", "yes"], root));
    }

    [Theory]
    [InlineData(0.3)]
    [InlineData(0.6)]
    [InlineData(0.9)]
    public void AdaptsToMeasuredMotionAndRequiresTwoArrivalReadings(double speed)
    {
        var planner = new WaypointFeedback(new(0, -60, 0, 0, 0), 60);
        var pose = new WaypointPose(0, 0, 0, 0, 1);
        bool confirmed = false;
        for (int i = 0; i < 6; i++)
        {
            var next = planner.Observe(pose);
            if (next.State == "arrived") { confirmed = true; break; }
            Assert.NotEqual("stopped", next.State);
            if (next.State == "move") Assert.InRange(next.PulseMs, 20, 60);
            pose = pose with { Y = pose.Y - next.PulseMs * speed, Timestamp = pose.Timestamp + 1 };
        }
        Assert.True(confirmed);
    }

    [Fact]
    public void StopsOnStallAndDoesNotRepeatForever()
    {
        var planner = new WaypointFeedback(new(0, -60, 0, 0, 0), 60);
        planner.Observe(new(0, 0, 0, 0, 1));
        planner.Observe(new(0, 0, 0, 0, 2));
        Assert.Equal("movement-stalled", planner.Observe(new(0, 0, 0, 0, 3)).Reason);
    }

    [Fact]
    public void RejectsWrongHeadingLevelStalePoseAndMovingAway()
    {
        WaypointFeedback Create() => new(new(0, -60, 0, 0, 0), 60);
        Assert.Equal("heading-correction-needs-calibration", Create().Observe(new(0, 0, 0, Math.PI, 1)).Reason);
        Assert.Equal("level-changed", Create().Observe(new(0, 0, 30, 0, 1)).Reason);
        var stale = Create(); stale.Observe(new(0, 0, 0, 0, 1));
        Assert.Equal("pose-not-newer", stale.Observe(new(0, -60, 0, 0, 1)).Reason);
        var away = Create(); away.Observe(new(0, 0, 0, 0, 1));
        Assert.Equal("moving-away-from-destination", away.Observe(new(0, 20, 0, 0, 2)).Reason);
    }

    [Fact]
    public void HeadingWrapDoesNotCreateSpuriousTurn()
    {
        var planner = new WaypointFeedback(new(0, -60, 0, 0, 0), 60);
        Assert.Equal("move", planner.Observe(new(0, 0, 0, 2 * Math.PI - 0.01, 1)).State);
    }
}
