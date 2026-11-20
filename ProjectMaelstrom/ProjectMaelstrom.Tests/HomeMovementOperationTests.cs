using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class HomeMovementOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "home-owner-" + Guid.NewGuid().ToString("N"));
    private static readonly GameWindow Target = new((nint)123, 4, 5, "WizardGraphicalClient", "Wizard101");

    [Theory]
    [InlineData("complete", "Completed", 1)]
    [InlineData("paused", "Failed", 1)]
    [InlineData("activity", "Failed", 0)]
    [InlineData("escape", "Failed", 0)]
    [InlineData("unarmed", "Failed", 0)]
    [InlineData("unclean", "CleanupFailed", 0)]
    [InlineData("idle-unclean", "CleanupFailed", 1)]
    [InlineData("idle-exception", "CleanupFailed", 1)]
    public async Task MovementAndFinalizerShareOwnerAndOriginalActivityBaseline(string scenario, string expected, int idleCalls)
    {
        var calls = 0; GameInputLease? lease = null;
        var job = HomeMovementOperation.Prepare(Target, new(SessionMinutes: 1), (context, _) =>
        {
            Assert.True(HomeMovementOperation.Owned(context, Target));
            Assert.False(HomeMovementOperation.Owned(context, Target with { StartedUtcTicks = 6 }));
            lease = context.Input;
            if (scenario == "unclean") lease!.RequireRecovery("synthetic failed release");
            var report = Path.Combine(context.Directory, "movement.json"); File.WriteAllText(report, "{}");
            return Task.FromResult(new HomeMovementRunResult(scenario == "paused" ? HomeMovementState.Paused : HomeMovementState.Stopped,
                scenario == "paused" ? "movement-unconfirmed" : "session-limit", 1, report, scenario == "unarmed" ? null : 7));
        }, () => new(60000, true, false, scenario == "activity" ? 8 : 7, 0, scenario == "escape", true),
        (context, baseline, _) =>
        {
            calls++; Assert.Equal(7, baseline); Assert.Same(lease, context.Input); Assert.Same(lease, GameInputLease.Current);
            if (scenario == "idle-exception") throw new IOException("Synthetic missing cleanup receipt");
            return Task.FromResult(new PetDanceNativeIdleResult(true, scenario != "idle-unclean", true, "fixture", "fixture-report"));
        });
        Assert.Equal(110, job.Request.TimeoutSeconds);
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "runs"), Path.Combine(_root, "locks"));
        var receipt = await scheduler.Submit(job.Request, job.Execute).Completion;
        Assert.Equal(expected, receipt.State.ToString()); Assert.Equal(idleCalls, calls);
        using var report = JsonDocument.Parse(File.ReadAllText(receipt.Artifacts.Last()));
        Assert.Equal(scenario is "complete" or "paused", report.RootElement.GetProperty("idleVerified").GetBoolean());
    }

    [Fact]
    public async Task ParentCancellationDoesNotEnterIdleCleanup()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var job = HomeMovementOperation.Prepare(Target, new(), async (context, token) =>
        {
            entered.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { }
            var report = Path.Combine(context.Directory, "movement.json"); File.WriteAllText(report, "{}");
            return new(HomeMovementState.Stopped, "stopped-by-user", 0, report, 7);
        }, () => throw new InvalidOperationException("No observation after cancellation"),
        (_, _, _) => throw new InvalidOperationException("No input after cancellation"));
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "runs"), Path.Combine(_root, "locks"));
        var ticket = scheduler.Submit(job.Request, job.Execute);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); ticket.Cancel();
        Assert.Equal(OperationState.Cancelled, (await ticket.Completion).State);
    }

    [Fact]
    public void GateRetainsTheArmedBaselineAfterTakeover()
    {
        var gate = new HomeMovementGate(new(), 0);
        Assert.Null(gate.ActivityBaseline);
        gate.Evaluate(new(0, true, false, 7, 0, false, true));
        Assert.True(gate.Evaluate(new(5000, true, false, 7, 0, false, true)));
        Assert.False(gate.Evaluate(new(6000, true, false, 8, 6000, false, true)));
        Assert.Equal(7, gate.ActivityBaseline);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
