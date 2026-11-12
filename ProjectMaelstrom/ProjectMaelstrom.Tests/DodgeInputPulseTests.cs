using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

[Collection("Dodge input")]
public class DodgeInputPulseTests
{
    private static readonly GameWindow Target = new((nint)123, 1, 2, "test", "test");
    private static readonly Size Client = new(480, 720);
    private sealed class FakeSink : IDodgeKeySink
    {
        public bool IsReady = true, Stop, Held, FailRelease;
        public Action? OnReady, OnDown;
        public readonly List<string> Events = new();
        public bool Ready(GameWindow target, Size size) { OnReady?.Invoke(); return IsReady && target == Target && size == Client; }
        public bool StopRequested() => Stop;
        public bool MovementKeyHeld() => Held;
        public void KeyDown(string key) { Events.Add("down:" + key); OnDown?.Invoke(); }
        public void KeyUp(string key) { Events.Add("up:" + key); if (FailRelease) throw new InvalidOperationException("release rejected"); }
    }

    [Fact]
    public void FocusRequestRequiresMaelstromToBeForeground()
    {
        Assert.True(WindowsDodgeKeySink.MayRequestFocus(123, 123));
        Assert.False(WindowsDodgeKeySink.MayRequestFocus(456, 123));
        Assert.False(WindowsDodgeKeySink.MayRequestFocus(0, 123));
    }

    [Fact]
    public async Task FocusWaitAllowsDelayedActivationAndRejectsChangedTarget()
    {
        var checks = 0;
        Assert.True(await WindowsDodgeKeySink.WaitForFocusAsync(() => true, () => ++checks == 3));
        checks = 0;
        Assert.False(await WindowsDodgeKeySink.WaitForFocusAsync(() => ++checks < 2, () => false));
        Assert.False(await WindowsDodgeKeySink.WaitForFocusAsync(() => false, () => true));
    }

    [Fact]
    public void EncodesPhysicalScanCodesAndExtendedArrowKeys()
    {
        Assert.Equal((ushort)0x4B, WindowsDodgeKeySink.Encode("LEFT", false).Scan);
        foreach (var key in new[] { "LEFT", "RIGHT", "UP", "DOWN", "A", "D", "W", "S" })
        {
            var down = WindowsDodgeKeySink.Encode(key, false);
            var up = WindowsDodgeKeySink.Encode(key, true);
            Assert.InRange(down.Scan, (ushort)1, (ushort)255);
            Assert.Equal(down.Scan, up.Scan);
            Assert.NotEqual(0u, down.Flags & 8u);
            Assert.Equal(down.Flags | 2u, up.Flags);
            Assert.Equal(key.Length > 1, (down.Flags & 1u) != 0);
        }
    }

    [Fact]
    public async Task AllowsOnlyFreshBoundedInputWithPolicyAndFocus()
    {
        var sink = new FakeSink();
        var pulse = new DodgeInputPulse(sink, () => true);
        var result = await pulse.RunAsync(Target, Client, "LEFT", 20, FrameTimeline.TimestampMs(), CancellationToken.None);
        Assert.Equal("dispatched", result.Status);
        Assert.Equal(new[] { "down:LEFT", "up:LEFT" }, sink.Events);
        Assert.True(result.UpMs >= result.DownMs);
        sink.Events.Clear();
        Assert.Equal("invalid-action", (await pulse.RunAsync(Target, Client, "ENTER", 20, FrameTimeline.TimestampMs(), CancellationToken.None)).Status);
        Assert.Equal("invalid-action", (await pulse.RunAsync(Target, Client, "LEFT", 201, FrameTimeline.TimestampMs(), CancellationToken.None)).Status);
        var stale = await pulse.RunAsync(Target, Client, "LEFT", 20, FrameTimeline.TimestampMs() - 201, CancellationToken.None);
        Assert.Equal("rejected", stale.Status);
        Assert.Equal("stale-or-invalid-frame", stale.Reason);
        sink.IsReady = false;
        Assert.Equal("rejected", (await pulse.RunAsync(Target, Client, "LEFT", 20, FrameTimeline.TimestampMs(), CancellationToken.None)).Status);
        sink.IsReady = true;
        sink.Held = true;
        Assert.Equal("rejected", (await pulse.RunAsync(Target, Client, "LEFT", 20, FrameTimeline.TimestampMs(), CancellationToken.None)).Status);
        sink.Held = false;
        sink.Stop = true;
        Assert.Equal("rejected", (await pulse.RunAsync(Target, Client, "LEFT", 20, FrameTimeline.TimestampMs(), CancellationToken.None)).Status);
        sink.Stop = false;
        Assert.Equal("rejected", (await new DodgeInputPulse(sink, () => false).RunAsync(Target, Client, "LEFT", 20, FrameTimeline.TimestampMs(), CancellationToken.None)).Status);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public async Task FocusLossAndCancellationReleaseHeldKey()
    {
        foreach (var useCancellation in new[] { false, true })
        {
            var sink = new FakeSink();
            using var cancel = new CancellationTokenSource();
            // Trigger takeover at the known key-down boundary, rather than racing
            // two wall-clock delays under parallel test-runner load.
            sink.OnDown = () => { if (useCancellation) cancel.Cancel(); else sink.IsReady = false; };
            var pulse = new DodgeInputPulse(sink, () => true);
            var result = await pulse.RunAsync(Target, Client, "UP", 200, FrameTimeline.TimestampMs(), cancel.Token);
            Assert.Equal("interrupted", result.Status);
            Assert.Equal(new[] { "down:UP", "up:UP" }, sink.Events);
        }
    }

    [Fact]
    public async Task RevokingSessionPermissionDuringPulseReleasesKey()
    {
        var sink = new FakeSink();
        var permission = 1;
        // Revoke at the actual press boundary; a delayed test thread can otherwise
        // wake after the entire pulse under load and never exercise revocation.
        sink.OnDown = () => Interlocked.Exchange(ref permission, 0);
        var task = new DodgeInputPulse(sink, () => Volatile.Read(ref permission) == 1)
            .RunAsync(Target, Client, "W", 200, FrameTimeline.TimestampMs(), CancellationToken.None);
        var result = await task;
        Assert.Equal("interrupted", result.Status); Assert.Equal(new[] { "down:W", "up:W" }, sink.Events);
        Assert.NotNull(result.UpMs);
    }

    [Fact]
    public async Task ReleaseFailureIsRetriedAndRecorded()
    {
        var sink = new FakeSink { FailRelease = true };
        var result = await new DodgeInputPulse(sink, () => true).RunAsync(Target, Client, "LEFT", 1, FrameTimeline.TimestampMs(), CancellationToken.None);
        Assert.Equal("release-failed", result.Status);
        Assert.NotNull(result.DownMs);
        Assert.Null(result.UpMs);
        Assert.Equal(new[] { "down:LEFT", "up:LEFT", "up:LEFT" }, sink.Events);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("target")]
    public async Task SlowPermissionOrTargetCheckCannotDispatchAnAgedFrame(string delayAt)
    {
        double now = 100;
        var sink = new FakeSink();
        if (delayAt == "target") sink.OnReady = () => now = 251;
        var pulse = new DodgeInputPulse(sink, () => { if (delayAt == "permission") now = 251; return true; }, () => now);
        var result = await pulse.RunAsync(Target, Client, "LEFT", 100, 100, CancellationToken.None, maximumCaptureAgeMs: 150);
        Assert.Equal("rejected", result.Status);
        Assert.Equal("stale-or-invalid-frame", result.Reason);
        Assert.Null(result.DownMs); Assert.Null(result.UpMs);
        Assert.Empty(sink.Events);
    }

    [Theory]
    [InlineData(150, false)]
    [InlineData(200, true)]
    public async Task CallerCanTightenTheLegacyCaptureAgeLimit(double maximumAge, bool expectedDispatch)
    {
        double now = 275;
        var sink = new FakeSink();
        var pulse = new DodgeInputPulse(sink, () => true, () => sink.Events.Count == 0 ? now : now++);
        var result = await pulse.RunAsync(Target, Client, "UP", 1, 100, CancellationToken.None, maximumCaptureAgeMs: maximumAge);
        Assert.Equal(expectedDispatch ? "dispatched" : "rejected", result.Status);
        if (expectedDispatch) Assert.Equal(new[] { "down:UP", "up:UP" }, sink.Events);
        else Assert.Empty(sink.Events);
    }

    [Fact]
    public async Task DeadlineIsRecheckedAfterTargetValidation()
    {
        double now = 100;
        var sink = new FakeSink { OnReady = () => now = 150 };
        var result = await new DodgeInputPulse(sink, () => true, () => now)
            .RunAsync(Target, Client, "LEFT", 100, 100, CancellationToken.None, responseDeadlineMs: 200);
        Assert.Equal("rejected", result.Status);
        Assert.Equal("response-window-expired", result.Reason);
        Assert.Null(result.DownMs); Assert.Null(result.UpMs);
        Assert.Empty(sink.Events);
    }

    [Theory]
    [InlineData(199.999, false)]
    [InlineData(200, true)]
    [InlineData(99, false)]
    public async Task HoldCanFinishExactlyAtDeadlineButCannotStartTooLate(double deadline, bool expectedDispatch)
    {
        double now = 100;
        var sink = new FakeSink();
        sink.OnReady = () => { if (sink.Events.Count > 0) now = 200; };
        var result = await new DodgeInputPulse(sink, () => true, () => now)
            .RunAsync(Target, Client, "RIGHT", 100, 100, CancellationToken.None, responseDeadlineMs: deadline);
        Assert.Equal(expectedDispatch ? "dispatched" : "rejected", result.Status);
        if (expectedDispatch)
        {
            Assert.Equal(new[] { "down:RIGHT", "up:RIGHT" }, sink.Events);
            Assert.Equal(100, result.DownMs);
            Assert.Equal(200, result.UpMs);
        }
        else
        {
            Assert.Equal("response-window-expired", result.Reason);
            Assert.Empty(sink.Events);
            Assert.Null(result.DownMs); Assert.Null(result.UpMs);
        }
    }

    [Fact]
    public async Task DispatchLatencyDoesNotMoveTheAbsoluteReleaseDeadline()
    {
        double now = 100;
        var sink = new FakeSink { OnDown = () => now = 140 };
        sink.OnReady = () => { if (sink.Events.Count > 0) now = 200; };
        var result = await new DodgeInputPulse(sink, () => true, () => now)
            .RunAsync(Target, Client, "DOWN", 100, 100, CancellationToken.None, responseDeadlineMs: 200);
        Assert.Equal("dispatched", result.Status);
        Assert.Equal(new[] { "down:DOWN", "up:DOWN" }, sink.Events);
        Assert.Equal(140, result.DownMs);
        Assert.Equal(200, result.UpMs);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task InvalidAgeLimitCannotSendInput(double maximumAge)
    {
        var sink = new FakeSink();
        var result = await new DodgeInputPulse(sink, () => true, () => 100)
            .RunAsync(Target, Client, "UP", 100, 100, CancellationToken.None, maximumCaptureAgeMs: maximumAge);
        Assert.Equal("invalid-action", result.Status);
        Assert.Empty(sink.Events);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task InvalidAbsoluteDeadlineCannotSendInput(double deadline)
    {
        var sink = new FakeSink();
        var result = await new DodgeInputPulse(sink, () => true, () => 100)
            .RunAsync(Target, Client, "UP", 100, 100, CancellationToken.None, responseDeadlineMs: deadline);
        Assert.Equal("invalid-action", result.Status);
        Assert.Empty(sink.Events);
    }
}
