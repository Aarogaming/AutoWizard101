using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class HomeMovementTests
{
    private static readonly GameWindow Target = new((nint)123, 4, 5, "WizardGraphicalClient", "Wizard101");
    private static HomeMovementSignal Signal(double now = 5000) => new(now, true, false, 0, 0, false, true);
    private static HomeMovementGate Running(HomeMovementOptions? options = null)
    {
        var gate = new HomeMovementGate(options ?? new(), 0);
        Assert.False(gate.Evaluate(Signal(0))); Assert.True(gate.Evaluate(Signal())); return gate;
    }
    [Fact]
    public void RequiresStableFocusQuietInputAndEnabledSession()
    {
        var gate = new HomeMovementGate(new(), 0);
        Assert.False(gate.Evaluate(Signal(1000) with { TargetReady = false }));
        Assert.False(gate.Evaluate(Signal(2000)));
        Assert.False(gate.Evaluate(Signal(6000) with { LastActivityMs = 3000 }));
        Assert.False(gate.Evaluate(Signal(7000) with { LastActivityMs = 3000 }));
        Assert.True(gate.Evaluate(Signal(8000) with { LastActivityMs = 3000 }));
        Assert.False(gate.Evaluate(Signal(9000) with { Enabled = false }));
        Assert.Equal(HomeMovementState.Paused, gate.State);
    }
    [Fact]
    public void AnyTakeoverLatchesPauseAndNeverAutomaticallyResumes()
    {
        foreach (var changed in new[] { Signal(6000) with { ActivityRevision = 1 }, Signal(6000) with { TargetReady = false },
                     Signal(6000) with { KeysHeld = true }, Signal(6000) with { Escape = true }, Signal(6000) with { NowMs = double.NaN } })
        {
            var gate = Running(); Assert.False(gate.Evaluate(changed)); Assert.Equal(HomeMovementState.Paused, gate.State);
            Assert.False(gate.Evaluate(Signal(12000))); Assert.False(gate.PermitsPulse(0, true, true, false));
        }
    }
    [Fact]
    public void BoundsIntervalsArmingTimeSessionAndPairCount()
    {
        var gate = Running(); gate.CompletedPair(6000);
        Assert.False(gate.Evaluate(Signal(95_999))); Assert.True(gate.Evaluate(Signal(96_000)));
        var arming = new HomeMovementGate(new(), 0); arming.Evaluate(Signal(60_000)); Assert.Equal(HomeMovementState.Paused, arming.State);
        var limited = Running(new(SessionMinutes: 1)); Assert.False(limited.Evaluate(Signal(60_000))); Assert.Equal(HomeMovementState.Stopped, limited.State);
        var pairs = Running(); for (var i = 0; i < 20; i++) pairs.CompletedPair(6000);
        Assert.False(pairs.Evaluate(Signal(7000))); Assert.Equal(HomeMovementState.Stopped, pairs.State);
        Assert.Throws<ArgumentOutOfRangeException>(() => new HomeMovementOptions(1).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new HomeMovementOptions(SessionMinutes: 31).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new HomeMovementOptions(PulseMs: 151).Validate());
    }
    [Theory]
    [InlineData(false, 77, 77, true)]
    [InlineData(true, 0, 77, true)]
    [InlineData(true, 66, 77, true)]
    [InlineData(true, 77, 77, false)]
    [InlineData(true, 0, 0, true)]
    public void OnlyThisSessionsTaggedInjectedInputIsIgnored(bool injected, int extra, int tag, bool takeover) =>
        Assert.Equal(takeover, DesktopActivityMonitor.IsTakeover(injected, (nuint)extra, (nuint)tag));

    internal static Bitmap Scene(int change = 0)
    {
        var image = new Bitmap(640, 480);
        using var g = Graphics.FromImage(image); g.Clear(Color.FromArgb(60 + change, 80 + change, 100 + change));
        using var brush = new SolidBrush(Color.FromArgb(180 + change, 150 + change, 120 + change));
        for (var x = 0; x < image.Width; x += 32) g.FillRectangle(brush, x, 0, 16, image.Height);
        return image;
    }
    [Fact]
    public void VisualGuardRejectsMissingDetailResizeHudAndLocalSceneChanges()
    {
        using var original = Scene(); var reference = new HomeReference(HomeView.From(original)); Assert.True(reference.Usable);
        Assert.Null(reference.Reject(HomeView.From(original)));
        using var blank = new Bitmap(640, 480); Assert.False(new HomeReference(HomeView.From(blank)).Usable);
        Assert.False(new HomeReference(new(new Size(640, 480), [])).Usable);
        Assert.Equal("game-size-changed", reference.Reject(reference.View with { Size = new Size(800, 600) }));
        var pixels = reference.View.Pixels.ToArray(); Array.Fill<byte>(pixels, 0);
        Assert.Equal("home-hud-changed", reference.Reject(reference.View with { Pixels = pixels }));
        pixels = reference.View.Pixels.ToArray();
        for (var y = 14; y < 43; y++) for (var x = 20; x < 50; x++) for (var c = 0; c < 3; c++) pixels[(y * 160 + x) * 3 + c] = 255;
        Assert.Equal("home-scene-region-changed", reference.Reject(reference.View with { Pixels = pixels }));
    }
    [Fact]
    public void RealDormStepKeepsHudValidButMissingHudStillPauses()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "TestData", "HomeMovement");
        using var before = new Bitmap(Path.Combine(folder, "dorm-before.png"));
        using var after = new Bitmap(Path.Combine(folder, "dorm-after.png"));
        var reference = new HomeReference(HomeView.From(before), new Size(1280, 720));
        var current = HomeView.From(after);
        Assert.True(reference.Usable);
        Assert.Null(reference.Reject(current));
        Assert.True(reference.View.Difference(current, HomeReference.Scene) >= .002);
        // Remove the bottom HUD while keeping the scene: this is still a stop condition.
        using (var graphics = Graphics.FromImage(after)) graphics.FillRectangle(Brushes.Black, 0, 660, after.Width, after.Height - 660);
        Assert.Equal("home-hud-changed", reference.Reject(HomeView.From(after)));
    }
    [Fact]
    public void CaptureBorderSizeIsSeparateFromInputClientSize()
    {
        using var original = Scene();
        var reference = new HomeReference(HomeView.From(original), new Size(624, 441));
        Assert.Equal(new Size(640, 480), reference.View.Size);
        Assert.Equal(new Size(624, 441), reference.ClientSize);
        Assert.Null(reference.Reject(HomeView.From(original)));
    }
    [Fact]
    public void SessionLeaseExcludesOtherCopiesAndReleasesOnDispose()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-home-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (HomeMovementLease.Acquire(root)) Assert.Throws<InvalidOperationException>(() => HomeMovementLease.Acquire(root));
            using var again = HomeMovementLease.Acquire(root); Assert.True(again.IsHeld);
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class Runtime : IHomeMovementRuntime
    {
        public double Now; public long Activity; public int Pulses; public bool Changed = true, StaleBefore, StaleAfter, ChangedHud, Takeover, RejectInput, Cumulative;
        public readonly List<(string Key, double Time)> Calls = [];
        public HomeMovementSignal Signal => HomeMovementTests.Signal(Now) with { ActivityRevision = Activity };
        public bool Permitted(HomeMovementGate gate) => gate.PermitsPulse(Activity, true, true, false);
        public Task<HomeSample?> CaptureAsync(double? afterMs, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var bitmap = ChangedHud ? new Bitmap(640, 480) : Scene(Changed && Pulses % 2 == 1 ? 5 : 0);
            if (Cumulative)
            {
                using var g = Graphics.FromImage(bitmap);
                using var changedScene = Scene(Math.Min(60, Pulses * 12));
                var scene = new Rectangle(80, 75, 480, 309);
                g.DrawImage(changedScene, scene, scene, GraphicsUnit.Pixel);
            }
            return Task.FromResult<HomeSample?>(new(bitmap, Now - (afterMs != null && StaleAfter ? 1 : StaleBefore ? 201 : 0)));
        }
        public Task<DodgePulseResult> PulseAsync(string key, int milliseconds, double capturedMs, HomeMovementGate gate, CancellationToken token)
        {
            Assert.True(Permitted(gate)); Calls.Add((key, Now)); Pulses++; Now += milliseconds;
            if (Takeover) Activity++;
            return Task.FromResult(RejectInput ? new DodgePulseResult("rejected", null, null, "test-rejection") : new("dispatched", Now - milliseconds, Now));
        }
        public Task DelayAsync(int milliseconds, CancellationToken token) { token.ThrowIfCancellationRequested(); Now += milliseconds; return Task.CompletedTask; }
    }
    private static async Task<(HomeMovementRunResult Result, JsonElement Report)> Run(Runtime runtime, HomeMovementOptions? options = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-home-run-" + Guid.NewGuid().ToString("N"));
        using var original = Scene();
        try
        {
            var result = await HomeMovementRun.RunAsync(runtime, Target, new(HomeView.From(original)), options ?? new(SessionMinutes: 1), root, true, null, CancellationToken.None);
            using var report = JsonDocument.Parse(File.ReadAllText(result.Report)); return (result, report.RootElement.Clone());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public async Task CompleteBoundedCyclesReobserveEachKeyAndPersistReceiptsSeparatelyFromVisualEvidence()
    {
        var runtime = new Runtime(); var (result, report) = await Run(runtime, new(60, 3));
        Assert.Equal(HomeMovementState.Stopped, result.State); Assert.Equal("session-limit", result.Reason);
        Assert.Equal(3, result.CompletedCycles); Assert.Equal(new[] { "A", "A", "A" }, runtime.Calls.Select(c => c.Key));
        Assert.Equal("SpinLeft", report.GetProperty("motion").GetString());
        Assert.True(runtime.Calls[1].Time - runtime.Calls[0].Time >= 60_000);
        Assert.Equal(6, report.GetProperty("actions").GetArrayLength());
        Assert.Contains("not proof", report.GetProperty("note").GetString());
    }
    [Fact]
    public async Task StaleFramesChangedViewsAndInputRejectionsNeverContinueBlindly()
    {
        foreach (var runtime in new[] { new Runtime { StaleBefore = true }, new Runtime { ChangedHud = true }, new Runtime { RejectInput = true }, new Runtime { StaleAfter = true }, new Runtime { Takeover = true }, new Runtime { Changed = false } })
        {
            var (result, _) = await Run(runtime); Assert.Equal(HomeMovementState.Paused, result.State); Assert.Equal(0, result.CompletedCycles);
            Assert.True(runtime.Pulses <= 1); Assert.DoesNotContain(runtime.Calls, c => c.Key == "D");
        }
    }
    [Fact]
    public async Task EachModeKeepsItsOwnKeysAndNativeIdleNeverInjects()
    {
        foreach (var (mode, keys) in new[] { (HomeIdleMode.SpinRight, new[] { "D" }), (HomeIdleMode.OpposingTurns, new[] { "A", "D" }), (HomeIdleMode.WalkPairs, new[] { "W", "S" }) })
        {
            var runtime = new Runtime(); var (result, _) = await Run(runtime, new(SessionMinutes: 1, Mode: mode));
            Assert.Equal(1, result.CompletedCycles); Assert.Equal(keys, runtime.Calls.Select(c => c.Key));
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(new Runtime(), new(Mode: HomeIdleMode.NativeIdle)));
    }
    [Fact]
    public async Task OneWayTurnsAcceptGradualViewChangesWithoutRelaxingHudGuard()
    {
        var runtime = new Runtime { Cumulative = true };
        var (result, _) = await Run(runtime, new(60, 5));
        Assert.Equal(5, result.CompletedCycles); Assert.Equal("session-limit", result.Reason);
        Assert.All(runtime.Calls, call => Assert.Equal("A", call.Key));
    }
    [Fact]
    public async Task CustomSequenceVerifiesEachStepAndStopsBeforeNextOnTakeover()
    {
        var options = new HomeMovementOptions(SessionMinutes: 1, Mode: HomeIdleMode.Routine, RoutineActions: ["turn-right", "turn-right", "turn-left"]);
        var runtime = new Runtime(); var (result, _) = await Run(runtime, options);
        Assert.Equal(1, result.CompletedCycles); Assert.Equal(new[] { "D", "D", "A" }, runtime.Calls.Select(call => call.Key));
        runtime = new Runtime { Takeover = true }; (result, _) = await Run(runtime, options);
        Assert.Equal(HomeMovementState.Paused, result.State); Assert.Equal(0, result.CompletedCycles); Assert.Single(runtime.Calls);
    }
    [Fact]
    public void OptionsPersistWithoutSessionPermissionOrWindowIdentity()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-idle-options-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = new HomeMovementOptions(120, 20, 80, HomeIdleMode.SpinRight); options.Save(root);
            Assert.Equal(options, HomeMovementOptions.Load(root));
            var json = File.ReadAllText(Path.Combine(root, "auto-idle-settings.json"));
            Assert.DoesNotContain("Enabled", json); Assert.DoesNotContain("Handle", json); Assert.DoesNotContain("Keys", json);
            Assert.Throws<ArgumentOutOfRangeException>(() => new HomeMovementOptions(Mode: (HomeIdleMode)999).Validate());
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task CancellationPersistsStopWithoutDispatching()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-home-cancel-" + Guid.NewGuid().ToString("N"));
        using var original = Scene(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var runtime = new Runtime();
        try
        {
            var result = await HomeMovementRun.RunAsync(runtime, Target, new(HomeView.From(original)), new(), root, true, null, cancellation.Token);
            Assert.Equal(HomeMovementState.Stopped, result.State); Assert.Empty(runtime.Calls); Assert.True(File.Exists(result.Report));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task HomeConfirmationAndCorrectProcessAreRequiredBeforeCreatingReports()
    {
        using var original = Scene(); var reference = new HomeReference(HomeView.From(original));
        await Assert.ThrowsAsync<InvalidOperationException>(() => HomeMovementRun.RunAsync(new Runtime(), Target, reference, new(), "unused", false, null, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => HomeMovementRun.RunAsync(new Runtime(), Target with { ProcessName = "notepad" }, reference, new(), "unused", true, null, default));
    }
}
