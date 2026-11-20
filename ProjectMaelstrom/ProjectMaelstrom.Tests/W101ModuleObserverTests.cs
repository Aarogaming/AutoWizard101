using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101ModuleObserverTests : IDisposable
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationOnlyAvoidsRecoveryWithTypedCleanupEvidence(bool verified)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var runtime = Runtime("fresh") with
        {
            Read = (_, _, _, _, token) => verified
                ? throw new W101UiObservationCancelledException("Confirmed restoration", token)
                : throw new OperationCanceledException("Missing cleanup receipt")
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => W101ModuleObserver.ReadAsync(
            Context(lease), _target, _launch, 0, Binding, _geometry, () => true, default, runtime));
        Assert.Equal(!verified, lease.RecoveryRequired);
    }

    [Fact]
    public async Task LoadingWithoutTreeIsPausedEvidenceNotTimestampFailure()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var original = Runtime("fresh");
        var runtime = original with
        {
            Read = async (owner, target, launch, index, token) =>
        {
            var reading = await original.Read(owner, target, launch, index, token);
            return reading with
            {
                Loading = true,
                StatesObservedUtcTicks = 0,
                States = reading.States.ToDictionary(pair => pair.Key, _ => (bool?)null),
                StableStates = reading.StableStates.ToDictionary(pair => pair.Key, _ => (bool?)null)
            };
        }
        };
        var result = await W101ModuleObserver.ReadAsync(Context(lease), _target, _launch, 0, Binding, _geometry, () => true, default, runtime);
        Assert.Equal("paused-for-loading", result.ReadinessReason);
        Assert.False(result.Sample.Context.GameMenuVerified);
        Assert.False(lease.RecoveryRequired);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnlyVerifiedCleanRejectionAvoidsRecoveryLatch(bool verified)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var runtime = Runtime("fresh") with
        {
            Read = (_, _, _, _, _) => verified ? throw new W101UiObservationUnavailableException("Unreviewed build; clean disconnect.") :
                throw new IOException("No cleanup receipt.")
        };
        await Assert.ThrowsAnyAsync<Exception>(() => W101ModuleObserver.ReadAsync(Context(lease), _target, _launch, 0,
            Binding, _geometry, () => true, default, runtime));
        Assert.Equal(!verified, lease.RecoveryRequired);
    }

    [Fact]
    public async Task CaptureOnlyDoesNotRunOcrOrGrantVisualControls()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var runtime = Runtime("fresh") with { Recognize = (_, _, _) => throw new Exception("OCR must not run.") };
        var result = await W101ModuleObserver.ReadAsync(Context(lease), _target, _launch, 0, Binding, _geometry,
            () => true, default, runtime, recognize: false);
        Assert.Equal("selected-window-capture", result.Recognition.Frame.TextOrigin);
        Assert.Equal("Unknown", result.Recognition.Scene);
        Assert.Empty(result.Recognition.Controls); Assert.Empty(result.Recognition.VisualFacts);
        Assert.False(result.Sample.Context.GameMenuVerified);
        Assert.False(lease.RecoveryRequired);
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "module-observer-" + Guid.NewGuid().ToString("N"));
    private readonly WizwalkerTarget _target = W101QuestConversationTests.Target;
    private readonly WizwalkerLaunch _launch = new("unused", "unused", "unused");
    private readonly Size _geometry = new(1280, 720);
    private RoutineBinding Binding => W101ModuleBindings.Bundle("menu").Bind(
        "pid-" + _target.ProcessId + "-start-" + _target.StartedUtcTicks, "test-geometry", "test-visit");

    [Theory]
    [InlineData("fresh", true)]
    [InlineData("old-image", false)]
    [InlineData("injected-ocr", false)]
    [InlineData("wrong-binding", false)]
    [InlineData("expired-tree", false)]
    public async Task ObservationsRetainEvidenceWithoutInventingOutsideOrOutcome(string scenario, bool menuVerified)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var context = Context(lease);
        var runtime = Runtime(scenario);
        var result = await W101ModuleObserver.ReadAsync(context, _target, _launch, 0, Binding, _geometry,
            () => true, default, runtime);
        Assert.Equal(menuVerified, result.Sample.Context.GameMenuVerified);
        Assert.False(result.Sample.Context.OutsideReady);
        Assert.False(result.Sample.Context.InteractionVerified);
        Assert.Null(result.Sample.OutcomeForProposalId);
        Assert.Equal(_geometry, result.Geometry);
        Assert.Single(result.Recognition.Controls);
        Assert.NotNull(result.Diagnostics);
        Assert.Equal("completed", result.Diagnostics.Stage);
        if (scenario == "expired-tree")
        {
            Assert.Equal("stale-ui-tree", result.ReadinessReason);
            Assert.Equal(2500, result.Diagnostics.TreeAgeMs);
            Assert.False(result.Sample.Context.IdleControlVerified);
        }
    }

    [Theory]
    [InlineData("wrong-target")]
    [InlineData("geometry")]
    [InlineData("future-image")]
    [InlineData("changed-hash")]
    public async Task InvalidOrExpiredEvidenceIsRejected(string scenario)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => W101ModuleObserver.ReadAsync(Context(lease), _target, _launch,
            0, Binding, _geometry, () => true, default, Runtime(scenario)));
        var diagnostics = Assert.IsType<W101ModuleDiagnostics>(error.Data["w101ModuleDiagnostics"]);
        Assert.Equal(1280, diagnostics.ExpectedWidth);
        Assert.Equal(720, diagnostics.ExpectedHeight);
        Assert.False(string.IsNullOrWhiteSpace(diagnostics.Stage));
        if (scenario == "future-image")
        {
            Assert.Equal("image-admission", diagnostics.Stage);
            Assert.Equal(-1, diagnostics.FrameAgeMs);
        }
    }

    [Fact]
    public async Task MissingCleanupBlocksCaptureAndRequiresRecovery()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var runtime = Runtime("cleanup") with
        {
            Capture = (_, _, _, _) => throw new InvalidOperationException("Capture must not start")
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => W101ModuleObserver.ReadAsync(Context(lease), _target, _launch,
            0, Binding, _geometry, () => true, default, runtime));
        Assert.True(lease.RecoveryRequired);
    }

    [Fact]
    public async Task TakeoverAfterReaderStopsBeforeCapture()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var ready = true;
        var runtime = Runtime("fresh");
        var read = runtime.Read;
        runtime = runtime with { Read = async (c, t, l, i, token) => { var result = await read(c, t, l, i, token); ready = false; return result; } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => W101ModuleObserver.ReadAsync(Context(lease), _target, _launch,
            0, Binding, _geometry, () => ready, default, runtime));
        Assert.False(lease.RecoveryRequired);
    }

    private W101ModuleObserverRuntime Runtime(string scenario)
    {
        const double now = 10000;
        return new((_, _, _, _, _) =>
        {
            var result = W101QuestConversationTests.Read(now, null) with
            {
                Target = scenario == "wrong-target" ? _target with { ProcessId = 999 } : _target,
                CleanupConfirmed = scenario != "cleanup",
                StatesObservedUtcTicks = W101QuestConversationTests.Stamp(scenario == "expired-tree" ? now - 2500 : now)
            };
            return Task.FromResult(result);
        }, (_, _, _, _) => Task.FromResult(new W101ModuleImage(new byte[33],
            scenario == "geometry" ? new(640, 480) : _geometry,
            scenario == "future-image" ? now + 1 : scenario == "old-image" ? now - 301 : now)),
        (_, frame, _) => Task.FromResult(new ModuleVisualRecognition(frame with
        {
            // This is explicitly synthetic provenance for pure adapter testing.
            TextOrigin = scenario == "injected-ocr" ? "injected-test-ocr" : "local-windows-ocr",
            Binding = scenario == "wrong-binding" ? frame.Binding with { GeometryId = "other" } : frame.Binding,
            ImageSha256 = scenario == "changed-hash" ? new string('a', 64) : frame.ImageSha256
        }, "MenuObserved", [new("game-menu.idle", new(20, 20, 100, 30))], ["wizard101.ui"], "synthetic-test")),
        () => now, () => W101QuestConversationTests.Stamp(now));
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("takeover-unclean")]
    public async Task ReaderUncertaintyRequiresRecoveryEvenDuringTakeover(string mode)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var ready = true;
        var runtime = Runtime("fresh");
        var read = runtime.Read;
        runtime = runtime with
        {
            Read = async (c, t, l, i, token) =>
        {
            if (mode == "throw") throw new IOException("Missing receipt");
            var result = await read(c, t, l, i, token);
            ready = false;
            return result with { HookRestored = false };
        }
        };
        await Assert.ThrowsAnyAsync<Exception>(() => W101ModuleObserver.ReadAsync(Context(lease), _target, _launch,
            0, Binding, _geometry, () => ready, default, runtime));
        Assert.True(lease.RecoveryRequired);
    }

    private OperationContext Context(GameInputLease lease) => new(new("run", "module", "v1", 60, true, Binding.TargetId,
        new Dictionary<string, string>()), _root, lease);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
