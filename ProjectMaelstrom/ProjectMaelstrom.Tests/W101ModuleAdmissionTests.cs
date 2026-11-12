using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101ModuleAdmissionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "module-admission-" + Guid.NewGuid().ToString("N"));
    private readonly WizwalkerLaunch _launch;
    private readonly WizwalkerTarget _target;
    public W101ModuleAdmissionTests()
    {
        Directory.CreateDirectory(_root);
        string Make(string name) { var path = Path.Combine(_root, name); File.WriteAllText(path, "inert"); return path; }
        _launch = new(Make("python.exe"), Make("w101_ui_tree.py"), _root);
        Make("wizwalker_adapter.py"); var image = Make("WizardGraphicalClient.exe");
        _target = W101QuestConversationTests.Target with { ImagePath = image, ImageSha256 = GameExperiments.Digest(image) };
    }
    private PreparedDeckJob Prepare(bool enabled = true, bool current = true, W101RoutineIdleRuntime? idle = null)
    {
        var bundle = W101ModuleBindings.Bundle("menu");
        var binding = bundle.Bind("pid-" + _target.ProcessId + "-start-" + _target.StartedUtcTicks, "test", "test");
        var runtime = new W101ModuleRuntime(() => true, (_, _, _) => throw new InvalidOperationException("Must not read"),
            (_, _, _, _) => throw new InvalidOperationException("Must not send"), () => 100, () => DateTime.UtcNow.Ticks);
        return W101ModuleAdmission.Prepare("menu", new(_target, true), _launch, binding, enabled, runtime,
            validation: new(_ => current, (_, _, _, _) => throw new InvalidOperationException("Validation only")), idleRuntime: idle);
    }
    [Fact]
    public void FullModuleAdmissionReservesIdleTimeWithoutExecutingDuringPreparation()
    {
        var body = Prepare();
        var wrapped = Prepare(idle: new(() => throw new InvalidOperationException("Must not inspect"),
            (_, _) => throw new InvalidOperationException("Must not run")));
        Assert.Equal(body.Request.TimeoutSeconds + 45, wrapped.Request.TimeoutSeconds);
        Assert.EndsWith(".idle-v1", wrapped.Request.Version);
        Assert.Equal(body.Request.Pins.OrderBy(p => p.Key), wrapped.Request.Pins.OrderBy(p => p.Key));
    }

    [Fact]
    public void PinsReaderHelperPythonAndExactGameImageWithoutReadingGame()
    {
        var job = Prepare();
        Assert.Equal(4, job.Request.Pins.Count);
        Assert.All(job.Request.Pins, p => Assert.Equal(GameExperiments.Digest(p.Key), p.Value));
        Assert.Equal("pid-" + _target.ProcessId + "-start-" + _target.StartedUtcTicks, job.Request.Target);
        Assert.True(job.Request.WritesGame); Assert.False(job.Request.DelegatedInput);
    }
    [Fact]
    public void DisabledAndReplacedTargetsCannotBeAdmitted()
    {
        Assert.Throws<InvalidOperationException>(() => Prepare(enabled: false));
        Assert.Throws<InvalidOperationException>(() => Prepare(current: false));
    }
    [Theory]
    [InlineData("w101_ui_tree.py")]
    [InlineData("wizwalker_adapter.py")]
    [InlineData("python.exe")]
    [InlineData("WizardGraphicalClient.exe")]
    public async Task ChangedPinnedFileIsRejectedBySchedulerBeforeCallback(string file)
    {
        var job = Prepare(); File.AppendAllText(Path.Combine(_root, file), "changed");
        await using var scheduler = new OperationScheduler(_root, Path.Combine(_root, "locks"));
        var executed = false;
        var result = await scheduler.Submit(job.Request, (_, _) =>
        {
            executed = true;
            return Task.FromResult(OperationOutcome.Completed("Unexpected execution"));
        }).Completion;
        Assert.NotEqual(OperationState.Completed, result.State);
        Assert.False(executed);
    }
    public void Dispose() => Directory.Delete(_root, true);
}
