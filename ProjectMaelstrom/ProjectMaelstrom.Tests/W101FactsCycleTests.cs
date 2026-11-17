using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101FactsCycleTests
{
    [Fact]
    public void CycleConstantsAreBounded()
    {
        Assert.InRange(W101FactsCycle.CycleSeconds, 1, 60);
    }

    [Fact]
    public async Task FactoryRequiresSchedulerAndReturnsAwaitableCycle()
    {
        await using var scheduler = new OperationScheduler(
            Path.Combine(Path.GetTempPath(), "maelstrom-facts-cycle-" + Guid.NewGuid().ToString("N")));
        var target = new WizwalkerTarget(42, 123, 456, "WizardGraphicalClient", "image", new string('a', 64));
        var launch = new WizwalkerLaunch("python", "worker.py", "root");
        var cycle = W101FactsCycle.Create(scheduler, target, launch);
        Assert.NotNull(cycle);
    }
}
