using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101IdleFailsafeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "idle-failsafe-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("already-idle", 1)]
    [InlineData("exhausted", 3)]
    [InlineData("uncertain", 1)]
    [InlineData("cancelled", 0)]
    [InlineData("resume-denied", 0)]
    public async Task AnyOwnedRoutineCanUseBoundedFinalization(string scenario, int expectedAttempts)
    {
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "runs"), Path.Combine(_root, "locks"));
        var request = new OperationRequest(Guid.NewGuid().ToString("N"), "synthetic-routine", "v1", 5,
            true, "fixture", new Dictionary<string, string>());
        W101IdleFailsafeResult? result = null;
        var ticket = scheduler.Submit(request, async (context, token) =>
        {
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (scenario == "cancelled") cancel.Cancel();
            result = await W101IdleFailsafe.RunAsync(context, new(ResumeAfterQuiet: true),
                () => scenario != "resume-denied",
                (_, _) => Task.FromResult(new OperationOutcome(scenario switch
                {
                    "already-idle" => OperationState.Completed,
                    "uncertain" => OperationState.CleanupFailed,
                    _ => OperationState.Failed
                }, "synthetic observation", [])),
                (_, _) => Task.FromResult(false), cancel.Token);
            return new OperationOutcome(context.Input!.RecoveryRequired ? OperationState.CleanupFailed : OperationState.Completed, "fixture", []);
        });
        await ticket.Completion;
        Assert.NotNull(result); Assert.Equal(expectedAttempts, result.Attempts);
        if (scenario == "cancelled") Assert.Equal("parent-cancelled", result.SkippedReason);
        if (scenario == "uncertain") Assert.Equal("cleanup-uncertain", result.SkippedReason);
        if (scenario == "resume-denied") Assert.Equal("quiet-resume-unavailable", result.SkippedReason);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
