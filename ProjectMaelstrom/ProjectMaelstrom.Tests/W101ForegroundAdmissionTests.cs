using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101ForegroundAdmissionTests
{
    [Fact]
    public void OnlyExplicitlyReviewedBuildsAdmitUiAdapter()
    {
        Assert.True(W101UiTreeOperation.SupportsClientImage("544ab726ef7b920a5ccb67aa3d06b245db7e0012317d2fe7856d58c17c31cf7e"));
        Assert.True(W101UiTreeOperation.SupportsClientImage("818dbe66037bcb6913a87ab4c2b5f77b0c4fad8d2d10bed921c9fe36ef7d1114"));
        Assert.True(W101UiTreeOperation.SupportsClientImage("d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a"));
        Assert.False(W101UiTreeOperation.SupportsClientImage(""));
        Assert.False(W101UiTreeOperation.SupportsClientImage(new string('f', 64)));
    }

    [Fact]
    public void RecoveryIsExplicitAndDoesNotAuthorizeGameplay()
    {
        var root = Path.GetTempPath();
        string[] args = ["--cli", "recover-input", "--output", Path.Combine(root, Guid.NewGuid() + ".json")];
        Assert.Equal("recover-input", MaelstromCli.Parse(args, root).Command);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--enable-module-actions"], root));
    }

    private static W101ControlReadiness State(double now) => new(null, true, false, 42, 99,
        new(1280, 720), new(1280, 720), true, false, false, false, 7, 7, now, now);

    [Fact]
    public void NeedsFiveQuietSecondsAndDoesNotRequireExistingFocus()
    {
        var gate = new W101ForegroundQuietGate();
        Assert.False(gate.Observe(State(1000), false));
        Assert.False(gate.Observe(State(5999), false));
        Assert.True(gate.Observe(State(6000), false));
    }

    [Fact]
    public void ActivityAndOtherGuardsCannotBeAcknowledgedAway()
    {
        var gate = new W101ForegroundQuietGate();
        gate.Observe(State(1000), false);
        foreach (var state in new[] { State(6000) with { ActivityRevision = 8 }, State(6000) with { Cancelled = true },
            State(6000) with { TargetCurrent = false }, State(6000) with { EscapeRequested = true },
            State(6000) with { AdmissionFailure = "owner-lost" }, State(6000) with { Minimized = true } })
            Assert.False(gate.Observe(state, false));
        Assert.False(gate.Observe(State(6000), true));
    }

    [Fact]
    public void ActivationRemainsOptIn()
    {
        Assert.False(new W101VisitPolicy().ActivateWhenQuiet);
    }

    [Fact]
    public void CliRequiresExplicitBooleanAndNavigationVisit()
    {
        var root = Path.GetTempPath();
        string[] args = ["--cli", "navigation-visit", "--output", Path.Combine(root, Guid.NewGuid() + ".json"),
            "--workspace", root, "--window", "42", "--process-id", "123", "--started-utc-ticks", "100",
            "--enable-client-observation", "--enable-ui-tree", "--enable-module-actions", "--key", "W", "--pulse-ms", "100"];
        Assert.True(MaelstromCli.Parse([.. args, "--activate-when-quiet", "true"], root).VisitPolicy!.ActivateWhenQuiet);
        Assert.False(MaelstromCli.Parse([.. args, "--activate-when-quiet", "false"], root).VisitPolicy!.ActivateWhenQuiet);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--activate-when-quiet", "yes"], root));
        args[1] = "navigation-probe";
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--activate-when-quiet", "true"], root));
    }
}
