using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101VisitPolicyTests
{
    private static W101ControlReadiness State(double time, long revision = 8) => new(null,
        true, false, 42, 42, new(1280, 720), new(1280, 720), true, false, false, false,
        revision, 7, time, time);

    [Fact]
    public void ActivityRestartsQuietPeriodRatherThanBeingIgnored()
    {
        var gate = new W101QuietPeriod();
        Assert.False(gate.Observe(State(0), false, 2));
        Assert.False(gate.Observe(State(1900), false, 2));
        Assert.False(gate.Observe(State(2000, 9), false, 2));
        Assert.False(gate.Observe(State(3900, 9), false, 2));
        Assert.True(gate.Observe(State(4000, 9), false, 2));
    }

    [Theory]
    [InlineData("escape")]
    [InlineData("cancel")]
    [InlineData("owner")]
    [InlineData("target")]
    [InlineData("geometry")]
    [InlineData("foreground")]
    [InlineData("heartbeat")]
    [InlineData("keys")]
    public void QuietTimeCannotOverrideOtherGuards(string fault)
    {
        var gate = new W101QuietPeriod();
        gate.Observe(State(0), false, 2);
        var state = State(3000);
        state = fault switch
        {
            "escape" => state with { EscapeRequested = true },
            "cancel" => state with { Cancelled = true },
            "owner" => state with { AdmissionFailure = "owner-lost" },
            "target" => state with { TargetCurrent = false },
            "geometry" => state with { ActualGeometry = new(800, 600) },
            "foreground" => state with { ForegroundHandle = 99 },
            "heartbeat" => state with { HeartbeatMs = 0 },
            _ => state
        };
        Assert.False(gate.Observe(state, fault == "keys", 2));
        Assert.False(gate.Observe(State(3100), false, 2));
    }

    [Fact]
    public void DefaultsAreBoundedAndResumeIsOptIn()
    {
        var policy = new W101VisitPolicy(); policy.Validate();
        Assert.Equal(2, policy.IdleRetries); Assert.False(policy.ResumeAfterQuiet);
        Assert.Equal(250, W101VisitPolicy.BackoffMs(1));
        Assert.Equal(500, W101VisitPolicy.BackoffMs(2));
        Assert.Throws<ArgumentException>(() => new W101VisitPolicy(3).Validate());
        Assert.Throws<ArgumentException>(() => new W101VisitPolicy(QuietSeconds: 1).Validate());
    }
}
