using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class DesktopInputActuatorTests : IDisposable
{
    private const string Target = "pid-123-start-456";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "actuator-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }
        catch { }
    }

    private OperationContext CreateContext(GameInputLease? lease) =>
        new(new("run", "nav", "v1", 60, true, Target, new Dictionary<string, string>()), _root, lease);

    [Fact]
    public async Task DispatchSteering_WhenArrived_ReturnsImmediateSuccessWithoutKeyDispatch()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var owner = GameInputLease.EnterScope(lease);
        var context = CreateContext(lease);

        var keyEvents = new List<(string Key, bool Up)>();
        var runtime = new DesktopInputRuntime(
            (k, up) => { keyEvents.Add((k, up)); return true; },
            (_, _, _) => true,
            () => 100.0,
            (_, _) => Task.CompletedTask);

        var actuator = new DesktopInputActuator(runtime);
        var cmd = new SteeringCommandSummary(0, 0, 0, 0, 0.0, 0.0, true);

        var receipt = await actuator.DispatchSteeringAsync(cmd, context, Target, () => true, default);

        Assert.True(receipt.Accepted);
        Assert.True(receipt.InputReleased);
        Assert.Equal("steering.arrived", receipt.ProposalId);
        Assert.Empty(keyEvents);
    }

    [Fact]
    public async Task DispatchSteering_ForwardPulse_EmitsKeyDownAndKeyUp()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var owner = GameInputLease.EnterScope(lease);
        var context = CreateContext(lease);

        var keyEvents = new List<(string Key, bool Up)>();
        var runtime = new DesktopInputRuntime(
            (k, up) => { keyEvents.Add((k, up)); return true; },
            (_, _, _) => true,
            () => 100.0,
            (_, _) => Task.CompletedTask);

        var actuator = new DesktopInputActuator(runtime);
        var cmd = new SteeringCommandSummary(100, 0, 0, 0, 0.05, 0.0, false);

        var receipt = await actuator.DispatchSteeringAsync(cmd, context, Target, () => true, default);

        Assert.True(receipt.Accepted);
        Assert.True(receipt.InputReleased);
        Assert.Equal("game.move-forward", receipt.ActionId);
        Assert.Equal(new[] { ("W", false), ("W", true) }, keyEvents);
    }

    [Fact]
    public async Task DispatchSteering_TurnLeft_EmitsKeyDownAndKeyUp()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var owner = GameInputLease.EnterScope(lease);
        var context = CreateContext(lease);

        var keyEvents = new List<(string Key, bool Up)>();
        var runtime = new DesktopInputRuntime(
            (k, up) => { keyEvents.Add((k, up)); return true; },
            (_, _, _) => true,
            () => 100.0,
            (_, _) => Task.CompletedTask);

        var actuator = new DesktopInputActuator(runtime);
        var cmd = new SteeringCommandSummary(0, 80, 0, 0, 0.2, 0.5, false);

        var receipt = await actuator.DispatchSteeringAsync(cmd, context, Target, () => true, default);

        Assert.True(receipt.Accepted);
        Assert.True(receipt.InputReleased);
        Assert.Equal("game.turn-left", receipt.ActionId);
        Assert.Equal(new[] { ("A", false), ("A", true) }, keyEvents);
    }

    [Fact]
    public async Task DispatchSteering_Rejects_WhenNoLeaseOrWrongTarget()
    {
        var contextWithoutLease = CreateContext(null);

        var keyEvents = new List<(string Key, bool Up)>();
        var runtime = new DesktopInputRuntime(
            (k, up) => { keyEvents.Add((k, up)); return true; },
            (_, _, _) => true,
            () => 100.0,
            (_, _) => Task.CompletedTask);

        var actuator = new DesktopInputActuator(runtime);
        var cmd = new SteeringCommandSummary(100, 0, 0, 0, 0.0, 0.0, false);

        var receipt = await actuator.DispatchSteeringAsync(cmd, contextWithoutLease, Target, () => true, default);
        Assert.False(receipt.Accepted);
        Assert.True(receipt.InputReleased);
        Assert.Empty(keyEvents);
    }

    [Fact]
    public async Task DispatchSteering_Rejects_WhenReadyIsFalse()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var owner = GameInputLease.EnterScope(lease);
        var context = CreateContext(lease);

        var keyEvents = new List<(string Key, bool Up)>();
        var runtime = new DesktopInputRuntime(
            (k, up) => { keyEvents.Add((k, up)); return true; },
            (_, _, _) => true,
            () => 100.0,
            (_, _) => Task.CompletedTask);

        var actuator = new DesktopInputActuator(runtime);
        var cmd = new SteeringCommandSummary(100, 0, 0, 0, 0.0, 0.0, false);

        var receipt = await actuator.DispatchSteeringAsync(cmd, context, Target, () => false, default);
        Assert.False(receipt.Accepted);
        Assert.True(receipt.InputReleased);
        Assert.Empty(keyEvents);
    }

    [Fact]
    public async Task DispatchSteering_Cancellation_AbortsHoldAndReleasesKey()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var owner = GameInputLease.EnterScope(lease);
        var context = CreateContext(lease);

        using var cts = new CancellationTokenSource();
        var keyEvents = new List<(string Key, bool Up)>();
        var runtime = new DesktopInputRuntime(
            (k, up) => { keyEvents.Add((k, up)); return true; },
            (_, _, _) => true,
            () => 100.0,
            async (_, token) =>
            {
                cts.Cancel();
                token.ThrowIfCancellationRequested();
                await Task.CompletedTask;
            });

        var actuator = new DesktopInputActuator(runtime);
        var cmd = new SteeringCommandSummary(200, 0, 0, 0, 0.0, 0.0, false);

        var receipt = await actuator.DispatchSteeringAsync(cmd, context, Target, () => true, cts.Token);

        Assert.False(receipt.Accepted);
        Assert.True(receipt.InputReleased);
        Assert.Equal(new[] { ("W", false), ("W", true) }, keyEvents);
    }

    [Fact]
    public async Task DispatchSteering_UnconfirmedRelease_TripsRecoveryHold()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var owner = GameInputLease.EnterScope(lease);
        var context = CreateContext(lease);

        var runtime = new DesktopInputRuntime(
            (k, up) => !up, // Send down succeeds, send up returns false!
            (_, _, _) => true,
            () => 100.0,
            (_, _) => Task.CompletedTask);

        var actuator = new DesktopInputActuator(runtime);
        var cmd = new SteeringCommandSummary(50, 0, 0, 0, 0.0, 0.0, false);

        var receipt = await actuator.DispatchSteeringAsync(cmd, context, Target, () => true, default);

        Assert.False(receipt.InputReleased);
        Assert.True(lease.RecoveryRequired, "Unconfirmed release must trip recovery required");
    }

    [Fact]
    public async Task DispatchKinematicClick_ValidTarget_DispatchesMouseDownAndUp()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var owner = GameInputLease.EnterScope(lease);
        var context = CreateContext(lease);

        var mouseEvents = new List<(double X, double Y, bool Up)>();
        var runtime = new DesktopInputRuntime(
            (_, _) => true,
            (x, y, up) => { mouseEvents.Add((x, y, up)); return true; },
            () => 100.0,
            (_, _) => Task.CompletedTask);

        var actuator = new DesktopInputActuator(runtime);
        var plan = new KinematicPlanSummary(20, 250.0, 65.0, 640.0, 360.0);

        var receipt = await actuator.DispatchKinematicClickAsync(plan, context, Target, () => true, default);

        Assert.True(receipt.Accepted);
        Assert.True(receipt.InputReleased);
        Assert.Equal("mouse.click", receipt.ActionId);
        Assert.Equal(new[] { (640.0, 360.0, false), (640.0, 360.0, true) }, mouseEvents);
    }

    [Fact]
    public async Task DispatchKinematicClick_OutOfBounds_Rejected()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var owner = GameInputLease.EnterScope(lease);
        var context = CreateContext(lease);

        var mouseEvents = new List<(double X, double Y, bool Up)>();
        var runtime = new DesktopInputRuntime(
            (_, _) => true,
            (x, y, up) => { mouseEvents.Add((x, y, up)); return true; },
            () => 100.0,
            (_, _) => Task.CompletedTask);

        var actuator = new DesktopInputActuator(runtime);
        // Target is at X: 1500 (beyond 1280 maximum)
        var plan = new KinematicPlanSummary(20, 250.0, 65.0, 1500.0, 360.0);

        var receipt = await actuator.DispatchKinematicClickAsync(plan, context, Target, () => true, default);

        Assert.False(receipt.Accepted);
        Assert.True(receipt.InputReleased);
        Assert.Empty(mouseEvents);
    }

    [Fact]
    public async Task DispatchKinematicClick_CombatPassButton_DispatchesWithinBounds()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var owner = GameInputLease.EnterScope(lease);
        var context = CreateContext(lease);

        var mouseEvents = new List<(double X, double Y, bool Up)>();
        var runtime = new DesktopInputRuntime(
            (_, _) => true,
            (x, y, up) => { mouseEvents.Add((x, y, up)); return true; },
            () => 100.0,
            (_, _) => Task.CompletedTask);

        var actuator = new DesktopInputActuator(runtime);
        // Calibrated 1280x720 Pass Button: (406.4, 477.6)
        var plan = new KinematicPlanSummary(15, 300.0, 80.0, 406.4, 477.6);

        var receipt = await actuator.DispatchKinematicClickAsync(plan, context, Target, () => true, default);

        Assert.True(receipt.Accepted);
        Assert.True(receipt.InputReleased);
        Assert.Equal("mouse.click", receipt.ActionId);
        Assert.Equal(new[] { (406.4, 477.6, false), (406.4, 477.6, true) }, mouseEvents);
    }

    [Fact]
    public async Task DispatchKinematicClick_CombatCardAndTarget_DispatchesWithinBounds()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var owner = GameInputLease.EnterScope(lease);
        var context = CreateContext(lease);

        var mouseEvents = new List<(double X, double Y, bool Up)>();
        var runtime = new DesktopInputRuntime(
            (_, _) => true,
            (x, y, up) => { mouseEvents.Add((x, y, up)); return true; },
            () => 100.0,
            (_, _) => Task.CompletedTask);

        var actuator = new DesktopInputActuator(runtime);

        // Step 1: Card click at (688.0, 390.0)
        var cardPlan = new KinematicPlanSummary(25, 400.0, 90.0, 688.0, 390.0);
        var cardReceipt = await actuator.DispatchKinematicClickAsync(cardPlan, context, Target, () => true, default);

        Assert.True(cardReceipt.Accepted);
        Assert.True(cardReceipt.InputReleased);

        // Step 2: Target click at Enemy 0 (208.0, 60.0)
        var targetPlan = new KinematicPlanSummary(20, 350.0, 75.0, 208.0, 60.0);
        var targetReceipt = await actuator.DispatchKinematicClickAsync(targetPlan, context, Target, () => true, default);

        Assert.True(targetReceipt.Accepted);
        Assert.True(targetReceipt.InputReleased);

        Assert.Equal(4, mouseEvents.Count);
        Assert.Equal((688.0, 390.0, false), mouseEvents[0]);
        Assert.Equal((688.0, 390.0, true), mouseEvents[1]);
        Assert.Equal((208.0, 60.0, false), mouseEvents[2]);
        Assert.Equal((208.0, 60.0, true), mouseEvents[3]);
    }
}
