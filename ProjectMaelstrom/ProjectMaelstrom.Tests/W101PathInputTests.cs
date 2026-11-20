using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101PathInputTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "path-input-" + Guid.NewGuid().ToString("N"));
    private static readonly WizwalkerTarget Target = new(1, 123, 456, "WizardGraphicalClient", "synthetic.exe", "synthetic-hash");
    private OperationContext Context(GameInputLease? lease) => new(new("run", "path-test", "1", 10, true,
        "pid-123-start-456", new Dictionary<string, string>()), _root, lease);
    private static PathMotion Motion(bool forward = true, int turn = 0, double expiry = 120) =>
        new("follow", "synthetic", forward, turn, ValidUntilMs: expiry, SequenceId: 1, IssuedMs: 100);

    [Theory]
    [InlineData(true, 0, "W")]
    [InlineData(false, 1, "A")]
    [InlineData(false, -1, "D")]
    [InlineData(true, 1, "A")]
    [InlineData(true, -1, "D")]
    public async Task MapsCanonicalTurnAndUsesRotationFirstWithPairedRelease(bool forward, int turn, string key)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var calls = new List<(string, bool)>();
        var result = await W101PathInput.DispatchAsync(Motion(forward, turn), Context(lease), Target, () => true,
            () => 100, default, (name, up) => { calls.Add((name, up)); return true; });
        Assert.Equal(new[] { (key, false), (key, true) }, calls);
        Assert.True(result.DispatchAccepted);
        Assert.Equal(100, result.DispatchedMs);
        Assert.Equal(100, result.ReleasedMs);
        Assert.Null(result.MotionObservedMs);
        Assert.Equal(key == "W", result.ForwardApplied);
    }

    [Fact]
    public async Task ExpiryDuringHoldReleasesAndRetainsActualDispatchEvidence()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        double now = 100;
        var calls = new List<bool>();
        var result = await W101PathInput.DispatchAsync(Motion(expiry: 200), Context(lease), Target, () => true,
            () => now, default, (_, up) => { calls.Add(up); if (!up) now = 201; return true; });
        Assert.Equal(new[] { false, true }, calls);
        Assert.True(result.DispatchAccepted);
        Assert.Equal(100, result.DispatchedMs);
        Assert.Equal(201, result.ReleasedMs);
        Assert.Null(result.MotionObservedMs);
        Assert.True(result.ForwardApplied);
    }

    [Fact]
    public async Task TakeoverAndCancellationReleaseWithoutFabricatingMotion()
    {
        foreach (var cancel in new[] { false, true })
        {
            using var lease = GameInputLease.Acquire("test", _root);
            using var scope = GameInputLease.EnterScope(lease);
            using var stop = new CancellationTokenSource();
            bool ready = true;
            var calls = new List<bool>();
            var result = await W101PathInput.DispatchAsync(Motion(expiry: 400), Context(lease), Target, () => ready,
                () => 100, stop.Token, (_, up) =>
                {
                    calls.Add(up);
                    if (!up) { if (cancel) stop.Cancel(); else ready = false; }
                    return true;
                });
            Assert.True(result.DispatchAccepted);
            Assert.Equal(new[] { false, true }, calls);
            Assert.NotNull(result.ReleasedMs);
            Assert.Null(result.MotionObservedMs);
        }
    }

    [Fact]
    public async Task RejectsExpiredUnownedWrongTargetAndCancelledBeforeInput()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        int calls = 0;
        bool Send(string _, bool up) { calls++; return true; }
        Assert.False((await W101PathInput.DispatchAsync(Motion(expiry: 101), Context(lease), Target, () => true, () => 101, default, Send)).DispatchAccepted);
        Assert.False((await W101PathInput.DispatchAsync(Motion(), Context(null), Target, () => true, () => 100, default, Send)).DispatchAccepted);
        Assert.False((await W101PathInput.DispatchAsync(Motion(), Context(lease), Target with { StartedUtcTicks = 789 }, () => true, () => 100, default, Send)).DispatchAccepted);
        Assert.False((await W101PathInput.DispatchAsync(Motion(), Context(lease), Target, () => true, () => 100, new CancellationToken(true), Send)).DispatchAccepted);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ReleaseRetriesKeepOnlyOneKeyDown()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        int down = 0, up = 0;
        var result = await W101PathInput.DispatchAsync(Motion(expiry: 101), Context(lease), Target, () => true,
            () => 100, default, (_, release) => { if (!release) { down++; return true; } return ++up == 2; });
        Assert.Equal(1, down);
        Assert.Equal(2, up);
        Assert.NotNull(result.ReleasedMs);
        Assert.False(lease.RecoveryRequired);
    }

    [Fact]
    public async Task ClockFailureStillPairsReleaseBeforeThrowing()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        double now = 100;
        var calls = new List<bool>();
        await Assert.ThrowsAsync<InvalidDataException>(() => W101PathInput.DispatchAsync(Motion(), Context(lease), Target,
            () => true, () => now, default, (_, up) => { calls.Add(up); if (!up) now = 99; return true; }));
        Assert.Equal(new[] { false, true }, calls);
        Assert.False(lease.RecoveryRequired);
    }

    [Fact]
    public async Task InvalidIntentFailsBeforeTransport()
    {
        int calls = 0;
        foreach (var motion in new[] { Motion() with { SequenceId = 0 }, Motion() with { IssuedMs = double.NaN },
            Motion() with { ValidUntilMs = double.PositiveInfinity }, Motion() with { Turn = 2 } })
            await Assert.ThrowsAsync<InvalidDataException>(() => W101PathInput.DispatchAsync(motion, Context(null), Target,
                () => true, () => 100, default, (_, _) => { calls++; return true; }));
        Assert.Equal(0, calls);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
