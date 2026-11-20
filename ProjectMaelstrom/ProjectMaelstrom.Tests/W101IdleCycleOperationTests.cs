using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101IdleCycleOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "idle-cycle-operation-" + Guid.NewGuid().ToString("N"));
    private readonly WizwalkerLaunch _launch;
    private readonly WizwalkerTarget _target;
    public W101IdleCycleOperationTests()
    {
        Directory.CreateDirectory(_root);
        string Make(string name) { var path = Path.Combine(_root, name); File.WriteAllText(path, "inert"); return path; }
        _launch = new(Make("python.exe"), Make("w101_ui_tree.py"), _root);
        Make("wizwalker_adapter.py"); var image = Make("WizardGraphicalClient.exe");
        _target = W101QuestConversationTests.Target with { ImagePath = image, ImageSha256 = GameExperiments.Digest(image) };
    }

    [Theory]
    [InlineData("direct", 2, true)]
    [InlineData("awake", 3, true)]
    [InlineData("ignored", 1, false)]
    [InlineData("takeover", 0, false)]
    [InlineData("geometry", 0, false)]
    [InlineData("dialogue", 0, false)]
    [InlineData("unreleased", 1, false)]
    public async Task OneOwnerVerifiesEntireCycle(string scenario, int expectedInputs, bool completed)
    {
        double time = 10000; int inputs = 0; bool ready = true; var geometry = new Size(1280, 720);
        var runtime = new W101IdleCycleRuntime(() => ready, geometry,
            (context, index, binding, _) =>
            {
                Assert.Same(context.Input, GameInputLease.Current); time += 500;
                if (scenario == "takeover") ready = false;
                var idle = inputs == 0 || scenario == "ignored" || inputs == (scenario == "awake" ? 3 : 2);
                var menu = !idle && (scenario != "awake" || inputs >= 2);
                var states = new Dictionary<string, bool?>
                {
                    ["nativeIdle"] = idle,
                    ["menu"] = menu,
                    ["dialogue"] = scenario == "dialogue",
                    ["interactionPrompt"] = false
                };
                var reading = W101QuestConversationTests.Read(time, null) with
                { Target = _target, States = states, StableStates = new(states), Loading = false };
                var size = scenario == "geometry" ? new Size(640, 480) : geometry;
                var visual = new ModuleVisualRecognition(new(new string('a', 64), time, size, binding, "local-windows-ocr"),
                    menu ? "MenuObserved" : idle ? "NativeIdle" : "Unknown",
                    menu ? [new("game-menu.idle", new(50, 50, 100, 30))] : [], [], "synthetic-fixture");
                return Task.FromResult(new W101ModuleObservation(new(reading, new()), visual));
            }, (context, proposal, _, fresh, _) =>
            {
                Assert.True(fresh()); Assert.Same(context.Input, GameInputLease.Current); inputs++; time += 40;
                return Task.FromResult(new RoutineReceipt(proposal.ProposalId!, proposal.ActionId!, time, true, scenario != "unreleased"));
            }, () => time, () => W101QuestConversationTests.Stamp(time));
        var validation = new WizwalkerRuntime(_ => true, (_, _, _, _) => throw new InvalidOperationException());
        var job = W101IdleCycleOperation.Prepare(new(_target, true), _launch, true, runtime, validation);
        await using var scheduler = new OperationScheduler(_root, Path.Combine(_root, "locks"));
        var result = await scheduler.Submit(job.Request, job.Execute).Completion;
        Assert.Equal(expectedInputs, inputs);
        Assert.Equal(completed, result.State == OperationState.Completed);
        Assert.Single(result.Artifacts);
        if (scenario == "unreleased") Assert.Equal(OperationState.CleanupFailed, result.State);
    }
    [Theory]
    [InlineData(false, "Failed")]
    [InlineData(true, "CleanupFailed")]
    public async Task ObservationFailurePreservesObserversCleanupDecision(bool uncertainCleanup, string expected)
    {
        var runtime = new W101IdleCycleRuntime(() => true, new Size(1280, 720),
            (context, _, _, _) =>
            {
                if (uncertainCleanup) context.Input!.RequireRecovery("Synthetic unconfirmed observer cleanup.");
                throw new IOException("Synthetic recognition failure after observation.");
            }, (_, _, _, _, _) => throw new InvalidOperationException("No input expected."),
            () => 10000, () => W101QuestConversationTests.Stamp(10000));
        var validation = new WizwalkerRuntime(_ => true, (_, _, _, _) => throw new InvalidOperationException());
        var job = W101IdleCycleOperation.Prepare(new(_target, true), _launch, true, runtime, validation);
        await using var scheduler = new OperationScheduler(_root, Path.Combine(_root, "locks"));
        var result = await scheduler.Submit(job.Request, job.Execute).Completion;
        Assert.Equal(expected, result.State.ToString());
        Assert.Single(result.Artifacts);
    }

    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedObservationKeepsBoundedDiagnosticWithoutMaskingFailure(bool diagnosticThrows)
    {
        var runtime = new W101IdleCycleRuntime(() => true, new Size(1280, 720),
            (_, _, _, _) => throw new IOException("Observation failed."),
            (_, _, _, _, _) => throw new InvalidOperationException("No input expected."),
            () => 10000, () => W101QuestConversationTests.Stamp(10000),
            () => diagnosticThrows ? throw new IOException("Diagnostic failed.") : "target-not-foreground;" + new string('x', 600));
        var validation = new WizwalkerRuntime(_ => true, (_, _, _, _) => throw new InvalidOperationException());
        var job = W101IdleCycleOperation.Prepare(new(_target, true), _launch, true, runtime, validation);
        await using var scheduler = new OperationScheduler(_root, Path.Combine(_root, "locks"));
        var result = await scheduler.Submit(job.Request, job.Execute).Completion;
        Assert.Equal(OperationState.Failed, result.State);
        Assert.Contains("Observation failed", result.Message);
        using var report = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Assert.Single(result.Artifacts)));
        var detail = report.RootElement.GetProperty("readinessDiagnostic").GetString()!;
        if (diagnosticThrows) Assert.Equal("diagnostic-unavailable", detail);
        else { Assert.Equal(512, detail.Length); Assert.StartsWith("target-not-foreground;", detail); }
    }
}
