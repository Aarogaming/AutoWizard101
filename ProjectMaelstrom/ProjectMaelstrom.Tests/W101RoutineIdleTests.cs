using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101RoutineIdleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "routine-idle-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("success", "Completed", 1)]
    [InlineData("body-failed", "Failed", 1)]
    [InlineData("idle-failed", "Failed", 3)]
    [InlineData("takeover", "Failed", 0)]
    [InlineData("unclean", "CleanupFailed", 0)]
    [InlineData("exception", "CleanupFailed", 0)]
    [InlineData("cancelled", "Cancelled", 0)]
    public async Task CompleteRoutineUsesOriginalOwnerAndPreservesBodyOutcome(string scenario, string expected, int attempts)
    {
        var ready = true; var calls = 0; var bodies = 0; GameInputLease? lease = null;
        var request = new OperationRequest(Guid.NewGuid().ToString("N"), "fixture-routine", "v1", 5,
            true, "fixture", new Dictionary<string, string>());
        var body = new PreparedDeckJob(request, (context, _) =>
        {
            bodies++; lease = context.Input;
            if (scenario == "exception") throw new IOException("synthetic escaped failure");
            if (scenario == "takeover") ready = false;
            return Task.FromResult(new OperationOutcome(scenario == "cancelled" ? OperationState.Cancelled : scenario == "unclean" ? OperationState.CleanupFailed :
                scenario == "body-failed" ? OperationState.Failed : OperationState.Completed, "body result", []));
        });
        var runtime = new W101RoutineIdleRuntime(() => ready, (context, _) =>
        {
            calls++; Assert.Same(lease, context.Input); Assert.Same(lease, GameInputLease.Current);
            Assert.NotEqual(request.Id, context.Request.Id);
            Assert.Equal(20, context.Request.TimeoutSeconds);
            Assert.Contains("idle-finalizer-", context.Directory);
            return Task.FromResult(new OperationOutcome(scenario == "idle-failed" ? OperationState.Failed : OperationState.Completed,
                "synthetic Idle observation", []));
        });
        var job = W101RoutineIdle.Wrap(body, W101QuestConversationTests.Target, new("unused", "unused", "unused"), runtime);
        Assert.Equal(50, job.Request.TimeoutSeconds);
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "runs"), Path.Combine(_root, "locks"));
        var result = await scheduler.Submit(job.Request, job.Execute).Completion;
        Assert.Equal(expected, result.State.ToString()); Assert.Equal(1, bodies); Assert.Equal(attempts, calls);
        using var report = JsonDocument.Parse(File.ReadAllText(Assert.Single(result.Artifacts)));
        Assert.Equal(scenario is "success" or "body-failed", report.RootElement.GetProperty("idleVerified").GetBoolean());
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
