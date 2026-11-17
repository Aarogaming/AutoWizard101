using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101ActivityFlagsTests
{
    private static readonly string[] DiegoNodes = ["DiegoMeter", "DuelTimerWindow"];
    private const long T0 = 639000000000000000;
    private static long Ms(long value) => value * TimeSpan.TicksPerMillisecond;

    private static Dictionary<string, bool> Nodes(bool diegoMeter, bool duelTimer) => new()
    {
        ["DiegoMeter"] = diegoMeter,
        ["DuelTimerWindow"] = duelTimer,
        ["PlanningPhase"] = false,
    };

    private static bool Sample(W101ActivityFlags flags, bool bound = true, bool image = true,
        bool loading = false, bool idle = false, bool meter = true, bool timer = true, long at = T0) =>
        flags.Observe("activity.diego.visible", Nodes(meter, timer), DiegoNodes,
            bound, image, loading, idle, at);

    [Fact]
    public void HiddenDormantNodesNeverFire()
    {
        var flags = new W101ActivityFlags();
        for (var i = 0; i < 6; i++)
            Assert.False(Sample(flags, meter: false, timer: false, at: T0 + Ms(i * 250)));
    }

    [Fact]
    public void ThreeVisibleSamplesAcross600MsFireOnce()
    {
        var flags = new W101ActivityFlags();
        Assert.False(Sample(flags, at: T0));
        Assert.False(Sample(flags, at: T0 + Ms(300)));
        Assert.True(Sample(flags, at: T0 + Ms(600)), "streak completes at >=600ms");
    }

    [Fact]
    public void FastSamplesWithoutSpanDoNotFire()
    {
        var flags = new W101ActivityFlags();
        for (var i = 0; i < 10; i++)
            Assert.False(Sample(flags, at: T0 + Ms(i * 10)));
    }

    [Fact]
    public void AnyFailedGateResetsTheStreak()
    {
        var flags = new W101ActivityFlags();
        Assert.False(Sample(flags, at: T0));
        Assert.False(Sample(flags, at: T0 + Ms(300)));
        Assert.False(Sample(flags, loading: true, at: T0 + Ms(600)));
        Assert.False(Sample(flags, at: T0 + Ms(900)));
        Assert.False(Sample(flags, at: T0 + Ms(1200)));
        Assert.True(Sample(flags, at: T0 + Ms(1500)));
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, false, true)]
    public void UnboundWrongImageOrIdleNeverFire(bool bound, bool image, bool loading, bool idle)
    {
        var flags = new W101ActivityFlags();
        for (var i = 0; i < 5; i++)
            Assert.False(Sample(flags, bound, image, loading, idle, at: T0 + Ms(i * 300)));
    }

    [Fact]
    public void StaleSampleRestartsTheStreak()
    {
        var flags = new W101ActivityFlags();
        Assert.False(Sample(flags, at: T0));
        Assert.False(Sample(flags, at: T0 + Ms(300)));
        Assert.False(Sample(flags, at: T0 + Ms(5000)));
        Assert.False(Sample(flags, at: T0 + Ms(5300)));
        Assert.True(Sample(flags, at: T0 + Ms(5600)));
    }
}
