using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101ControlScopeTests
{
    [Fact]
    public void FocusOnlyWaitNeverClearsTakeoverOrOtherFaults()
    {
        Assert.True(W101ControlScope.FocusWaitAllowed(Valid() with { ForegroundHandle = 99 }));
        Assert.True(W101ControlScope.FocusWaitAllowed(Valid() with { OperatorPaused = true }));
        Assert.False(W101ControlScope.FocusWaitAllowed(Valid() with { OperatorStopped = true, OperatorPaused = true }));
        Assert.False(W101ControlScope.FocusWaitAllowed(Valid() with { OperatorPaused = true, ActivityRevision = 8 }));
        Assert.False(W101ControlScope.FocusWaitAllowed(Valid() with { ForegroundHandle = 99, ActivityRevision = 8 }));
        Assert.False(W101ControlScope.FocusWaitAllowed(Valid() with { Cancelled = true }));
        Assert.False(W101ControlScope.FocusWaitAllowed(Valid() with { EscapeRequested = true }));
        Assert.True(DesktopActivityMonitor.TargetActivityAllowed(42, 42));
        Assert.False(DesktopActivityMonitor.TargetActivityAllowed(42, 99));
        Assert.True(DesktopActivityMonitor.TargetActivityAllowed(0, 99));
    }

    [Fact]
    public void KeyboardOnlyPolicyIgnoresMouseButRetainsKeyboardAndEscape()
    {
        Assert.False(DesktopActivityMonitor.CountsActivity(false, true));
        Assert.True(DesktopActivityMonitor.CountsActivity(false, false));
        Assert.True(DesktopActivityMonitor.CountsActivity(true, true));
        Assert.False(DesktopActivityMonitor.IncludesHeldKey(1, true));
        Assert.True(DesktopActivityMonitor.IncludesHeldKey(0x1B, true));
        Assert.Equal("target-not-foreground", (Valid() with { ForegroundHandle = 99 }).Code);
        Assert.Equal("escape-takeover", (Valid() with { EscapeRequested = true }).Code);
    }

    private static W101ControlReadiness Valid() => new(null, true, false, 42, 42,
        new(1280, 720), new(1280, 720), true, false, false, false, 7, 7, 1000, 900);

    [Theory]
    [InlineData("owner", "input-owner-scope-mismatch")]
    [InlineData("target", "target-instance-changed")]
    [InlineData("minimized", "target-minimized")]
    [InlineData("foreground", "target-not-foreground")]
    [InlineData("geometry", "client-geometry-changed")]
    [InlineData("monitor", "activity-monitor-unavailable")]
    [InlineData("activity", "desktop-activity-takeover")]
    [InlineData("cancel", "cancelled")]
    [InlineData("stop", "scope-stopping")]
    [InlineData("escape", "escape-takeover")]
    [InlineData("heartbeat", "message-loop-stalled")]
    [InlineData("future", "future-heartbeat")]
    [InlineData("clock", "invalid-heartbeat-clock")]
    [InlineData("inspection", "target-inspection-failed")]
    public void ReadinessExplainsTheActualFailedBoundary(string fault, string expected)
    {
        var original = Valid();
        var snapshot = fault switch
        {
            "owner" => original with { AdmissionFailure = "input-owner-scope-mismatch" },
            "target" => original with { TargetCurrent = false },
            "minimized" => original with { Minimized = true },
            "foreground" => original with { ForegroundHandle = 99 },
            "geometry" => original with { ActualGeometry = new(1920, 1080) },
            "monitor" => original with { MonitorInstalled = false },
            "activity" => original with { ActivityRevision = 8 },
            "cancel" => original with { Cancelled = true },
            "stop" => original with { Stopping = true },
            "escape" => original with { EscapeRequested = true },
            "heartbeat" => original with { NowMs = 2401 },
            "future" => original with { HeartbeatMs = 1001 },
            "clock" => original with { HeartbeatMs = double.NaN },
            "inspection" => original with { InspectionFailure = "InvalidOperationException" },
            _ => throw new InvalidOperationException("Invalid fixture")
        };
        Assert.False(snapshot.Ready);
        Assert.Equal(expected, snapshot.Code);
        Assert.StartsWith(expected + ";", snapshot.Describe());
    }

    [Fact]
    public void DiagnosticIncludesHandlesGeometryBaselineAndHeartbeatWithoutCollapsingThem()
    {
        var snapshot = Valid() with { ForegroundHandle = 99, ActualGeometry = new(1920, 1080) };
        var detail = snapshot.Describe();
        Assert.Contains("target=42", detail);
        Assert.Contains("foreground=99", detail);
        Assert.Contains("expected=1280x720", detail);
        Assert.Contains("actual=1920x1080", detail);
        Assert.Contains("revision=7; baseline=7", detail);
        Assert.Contains("heartbeatAgeMs=100.000", detail);
        Assert.True(detail.Length < 512);
        Assert.True(Valid().Ready);
    }

    [Theory]
    [InlineData(false, false, true, false, 7, 7, true, 1000, 1000, true)]
    [InlineData(false, false, true, false, 7, 7, true, 2500, 1000, true)]
    [InlineData(false, false, true, false, 7, 7, true, 2501, 1000, false)]
    [InlineData(false, false, true, false, 8, 7, true, 1000, 1000, false)]
    [InlineData(false, false, true, false, 6, 7, true, 1000, 1000, false)]
    [InlineData(true, false, true, false, 7, 7, true, 1000, 1000, false)]
    [InlineData(false, true, true, false, 7, 7, true, 1000, 1000, false)]
    [InlineData(false, false, false, false, 7, 7, true, 1000, 1000, false)]
    [InlineData(false, false, true, true, 7, 7, true, 1000, 1000, false)]
    [InlineData(false, false, true, false, 7, 7, false, 1000, 1000, false)]
    [InlineData(false, false, true, false, 7, 7, true, 999, 1000, false)]
    public void ScopeRequiresOriginalActivityTargetAndServicedHooks(bool cancelled, bool stopping,
        bool installed, bool escape, long revision, long baseline, bool targetReady, double now, double heartbeat, bool expected)
    {
        Assert.Equal(expected, W101ControlScope.Allows(cancelled, stopping, installed, escape, revision,
            baseline, targetReady, now, heartbeat));
    }

    [Fact]
    public void InvalidClocksDoNotAuthorizeInput()
    {
        Assert.False(W101ControlScope.Allows(false, false, true, false, 0, 0, true, double.NaN, 10));
        Assert.False(W101ControlScope.Allows(false, false, true, false, 0, 0, true, 10, double.PositiveInfinity));
    }

    [Fact]
    public void OwnTaggedInputDoesNotCountAsTakeoverButOtherInjectedInputDoes()
    {
        Assert.False(DesktopActivityMonitor.IsTakeover(true, 123, 123));
        Assert.True(DesktopActivityMonitor.IsTakeover(true, 124, 123));
        Assert.True(DesktopActivityMonitor.IsTakeover(false, 123, 123));
    }

    [Fact]
    public async Task MissingOwnerIsRejectedBeforeAnyHookThreadStarts()
    {
        var context = new OperationContext(new("test", "test", "v1", 10, true,
            "pid-123-start-456", new Dictionary<string, string>()), "unused", null);
        var target = new GameWindow(42, 123, 456, "WizardGraphicalClient", "test");
        await Assert.ThrowsAsync<InvalidOperationException>(() => W101ControlScope.CreateAsync(context,
            target, new(1280, 720), default));
    }
}
