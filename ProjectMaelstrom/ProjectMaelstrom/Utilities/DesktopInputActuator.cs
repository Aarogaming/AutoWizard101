using System.Diagnostics;

namespace ProjectMaelstrom.Utilities;

/// <summary>Summary representation of a SteeringCommand intent from MaelstromAutonomyBridge.</summary>
public sealed record SteeringCommandSummary(
    uint ForwardPulseMs,
    uint TurnLeftPulseMs,
    uint TurnRightPulseMs,
    uint ReversePulseMs,
    double Curvature,
    double AngleErrorRad,
    bool IsArrived);

/// <summary>Summary representation of a KinematicPlan intent from MaelstromAutonomyBridge.</summary>
public sealed record KinematicPlanSummary(
    int PointCount,
    double TotalDurationMs,
    double DwellMs,
    double TargetX,
    double TargetY);

/// <summary>Pluggable runtime transport seam for DesktopInputActuator testing and dispatch.</summary>
internal sealed record DesktopInputRuntime(
    Func<string, bool, bool> SendKey,
    Func<double, double, bool, bool> SendMouse,
    Func<double> Clock,
    Func<int, CancellationToken, Task> Delay);

/// <summary>
/// Physical desktop input actuator translating autonomy bridge proposals (steering and kinematics)
/// into verified OS input dispatches under the exclusive single input lease (GameInputLease).
/// Adheres strictly to Facts != Authority != Actions.
/// </summary>
internal sealed class DesktopInputActuator
{
    private const int MaximumForwardMs = 500;
    private const int MaximumTurnMs = 300;
    private const int MinimumDwellMs = 40;
    private const int MaximumDwellMs = 200;
    private const int MaximumReleaseWaitMs = 5000;

    private readonly DesktopInputRuntime _runtime;
    private readonly bool _isSimulation;

    internal DesktopInputActuator(DesktopInputRuntime runtime, bool isSimulation = false)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
        _isSimulation = isSimulation;
    }

    /// <summary>
    /// Creates a simulated DesktopInputActuator instance that records key and mouse events in memory
    /// without sending any physical input to external windows or moving the desktop cursor.
    /// </summary>
    public static DesktopInputActuator CreateSimulation(
        out List<(string Type, string Target, bool Up)> recordedEvents,
        Func<double>? clock = null)
    {
        var events = new List<(string Type, string Target, bool Up)>();
        recordedEvents = events;
        var time = clock ?? (() => DateTime.UtcNow.Ticks / 10000.0);
        var runtime = new DesktopInputRuntime(
            (key, up) =>
            {
                events.Add(("key", key, up));
                return true;
            },
            (x, y, up) =>
            {
                events.Add(("mouse", $"{x:F1},{y:F1}", up));
                return true;
            },
            time,
            (_, _) => Task.CompletedTask);
        return new DesktopInputActuator(runtime, isSimulation: true);
    }

    /// <summary>
    /// Dispatches a PurePursuit steering command pulse (W, A, D, or S) under the verified input lease.
    /// </summary>
    internal async Task<RoutineReceipt> DispatchSteeringAsync(
        SteeringCommandSummary cmd,
        OperationContext context,
        string targetId,
        Func<bool> ready,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ready);

        double clock() => _runtime.Clock();

        if (cmd.IsArrived)
        {
            return new("steering.arrived", "game.arrive", clock(), true, true);
        }

        bool Owned(bool beforePress = true) =>
            context.Request.WritesGame &&
            !context.Request.DelegatedInput &&
            context.Request.Target == targetId &&
            ValidTargetId(targetId) &&
            (_isSimulation || (context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current))) &&
            !token.IsCancellationRequested &&
            ready();

        if (!Owned())
        {
            return new("steering.rejected", "game.steer", clock(), false, true);
        }

        // Determine dominant action key and clamped duration
        string key;
        int durationMs;
        string actionId;

        if (cmd.TurnLeftPulseMs > 0)
        {
            key = "A";
            durationMs = (int)Math.Min(cmd.TurnLeftPulseMs, MaximumTurnMs);
            actionId = "game.turn-left";
        }
        else if (cmd.TurnRightPulseMs > 0)
        {
            key = "D";
            durationMs = (int)Math.Min(cmd.TurnRightPulseMs, MaximumTurnMs);
            actionId = "game.turn-right";
        }
        else if (cmd.ReversePulseMs > 0)
        {
            key = "S";
            durationMs = (int)Math.Min(cmd.ReversePulseMs, MaximumTurnMs);
            actionId = "game.move-backward";
        }
        else if (cmd.ForwardPulseMs > 0)
        {
            key = "W";
            durationMs = (int)Math.Min(cmd.ForwardPulseMs, MaximumForwardMs);
            actionId = "game.move-forward";
        }
        else
        {
            return new("steering.idle", "game.idle", clock(), true, true);
        }

        bool attempted = false;
        bool accepted = false;
        bool released = true;

        try
        {
            if (Owned())
            {
                attempted = true;
                var hold = Stopwatch.StartNew();
                accepted = _runtime.SendKey(key, false);

                while (accepted && hold.ElapsedMilliseconds < durationMs)
                {
                    if (!Owned(false))
                    {
                        accepted = false;
                        break;
                    }
                    var remaining = durationMs - hold.ElapsedMilliseconds;
                    if (remaining > 0)
                    {
                        await _runtime.Delay((int)Math.Min(10, remaining), token).ConfigureAwait(false);
                    }
                }
                accepted = accepted && Owned(false);
            }
        }
        catch (OperationCanceledException)
        {
            accepted = false;
        }
        catch
        {
            accepted = false;
        }
        finally
        {
            if (attempted)
            {
                released = await ReleaseKeyAsync(() => _runtime.SendKey(key, true)).ConfigureAwait(false);
                if (!released)
                {
                    context.Input?.RequireRecovery($"DesktopInputActuator steering key '{key}' release was not confirmed.");
                }
            }
        }

        return new("steering.dispatch", actionId, clock(), accepted, released);
    }

    /// <summary>
    /// Dispatches a humanized mouse click sequence from a KinematicPlan intent under the verified input lease.
    /// </summary>
    internal async Task<RoutineReceipt> DispatchKinematicClickAsync(
        KinematicPlanSummary plan,
        OperationContext context,
        string targetId,
        Func<bool> ready,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ready);

        double clock() => _runtime.Clock();

        bool Owned(bool beforePress = true) =>
            context.Request.WritesGame &&
            !context.Request.DelegatedInput &&
            context.Request.Target == targetId &&
            ValidTargetId(targetId) &&
            (_isSimulation || (context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current))) &&
            !token.IsCancellationRequested &&
            ready();

        // Viewport bounds validation (standard base resolution 1280x720)
        if (plan.TargetX < 0.0 || plan.TargetX > 1280.0 || plan.TargetY < 0.0 || plan.TargetY > 720.0 || !Owned())
        {
            return new("kinematic.rejected", "mouse.click", clock(), false, true);
        }

        int dwellMs = (int)Math.Clamp(plan.DwellMs, MinimumDwellMs, MaximumDwellMs);
        bool attempted = false;
        bool accepted = false;
        bool released = true;

        try
        {
            if (Owned())
            {
                attempted = true;
                var hold = Stopwatch.StartNew();
                // Send left mouse button down at target coordinate
                accepted = _runtime.SendMouse(plan.TargetX, plan.TargetY, false);

                while (accepted && hold.ElapsedMilliseconds < dwellMs)
                {
                    if (!Owned(false))
                    {
                        accepted = false;
                        break;
                    }
                    var remaining = dwellMs - hold.ElapsedMilliseconds;
                    if (remaining > 0)
                    {
                        await _runtime.Delay((int)Math.Min(10, remaining), token).ConfigureAwait(false);
                    }
                }
                accepted = accepted && Owned(false);
            }
        }
        catch (OperationCanceledException)
        {
            accepted = false;
        }
        catch
        {
            accepted = false;
        }
        finally
        {
            if (attempted)
            {
                released = await ReleaseMouseAsync(() => _runtime.SendMouse(plan.TargetX, plan.TargetY, true)).ConfigureAwait(false);
                if (!released)
                {
                    context.Input?.RequireRecovery($"DesktopInputActuator mouse button release at ({plan.TargetX}, {plan.TargetY}) was not confirmed.");
                }
            }
        }

        return new("kinematic.click", "mouse.click", clock(), accepted, released);
    }

    private static async Task<bool> ReleaseKeyAsync(Func<bool> release)
    {
        var elapsed = Stopwatch.StartNew();
        do
        {
            try { if (release()) return true; } catch { }
            if (elapsed.ElapsedMilliseconds >= MaximumReleaseWaitMs) break;
            await Task.Delay(100).ConfigureAwait(false);
        } while (elapsed.ElapsedMilliseconds < MaximumReleaseWaitMs);
        return false;
    }

    private static async Task<bool> ReleaseMouseAsync(Func<bool> release)
    {
        var elapsed = Stopwatch.StartNew();
        do
        {
            try { if (release()) return true; } catch { }
            if (elapsed.ElapsedMilliseconds >= MaximumReleaseWaitMs) break;
            await Task.Delay(100).ConfigureAwait(false);
        } while (elapsed.ElapsedMilliseconds < MaximumReleaseWaitMs);
        return false;
    }

    private static bool ValidTargetId(string value)
    {
        var parts = value.Split('-');
        return parts.Length == 4 && parts[0] == "pid" && parts[2] == "start" &&
            int.TryParse(parts[1], out var pid) && pid > 0 &&
            long.TryParse(parts[3], out var ticks) && ticks > 0;
    }
}
