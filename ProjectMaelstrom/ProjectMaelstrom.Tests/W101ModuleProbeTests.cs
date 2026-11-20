using System.Drawing;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101ModuleProbeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "module-probe-" + Guid.NewGuid().ToString("N"));
    private readonly WizwalkerTarget _target;
    private readonly WizwalkerLaunch _launch;
    public W101ModuleProbeTests()
    {
        Directory.CreateDirectory(_root);
        string File(string name) { var path = Path.Combine(_root, name); System.IO.File.WriteAllText(path, "inert"); return path; }
        _launch = new(File("python.exe"), File("w101_ui_tree.py"), _root);
        File("wizwalker_adapter.py"); var image = File("WizardGraphicalClient.exe");
        _target = W101QuestConversationTests.Target with { ImagePath = image, ImageSha256 = GameExperiments.Digest(image) };
    }

    [Theory]
    [InlineData("success", "Completed")]
    [InlineData("takeover", "Failed")]
    [InlineData("unclean", "CleanupFailed")]
    [InlineData("stale", "Unknown")]
    public async Task ProbeUsesOneOwnerAndNeverClaimsGameplay(string scenario, string expected)
    {
        var taken = false; var reads = 0;
        var locks = Path.Combine(_root, "locks");
        var runtime = new W101ModuleProbeRuntime(_ => true, () => 42, (_, baseline) => baseline == 42 && !taken,
            _ => new Size(1280, 720), (context, target, _, index, binding, geometry, ready, _) =>
            {
                reads++;
                Assert.Same(context.Input, GameInputLease.Current);
                Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competitor", locks));
                Assert.Equal(binding.TargetId, context.Request.Target);
                Assert.Equal(25, context.Request.TimeoutSeconds);
                Assert.Contains(Path.Combine(_root, "wizwalker_adapter.py"), context.Request.Pins.Keys);
                Assert.True(ready()); Assert.Equal(0, index);
                var reading = W101QuestConversationTests.Read(10000, null) with
                { Target = target, CleanupConfirmed = scenario != "unclean" };
                taken = scenario == "takeover";
                return Task.FromResult(new W101ModuleObservation(new(reading, new()),
                    new(new(new string('a', 64), 10000, geometry, binding, "injected-test-ocr"),
                        "Unknown", [], [], "synthetic-test"), scenario == "stale" ? "stale-ui-tree" : "candidate-observation-only"));
            });
        await using var scheduler = new OperationScheduler(_root, locks);
        var result = await W101ModuleProbe.Submit(scheduler, new(_target, true), _launch, runtime).Completion;
        Assert.Equal(expected, result.State.ToString());
        Assert.Equal(1, reads);
        using var report = JsonDocument.Parse(System.IO.File.ReadAllText(Assert.Single(result.Artifacts)));
        Assert.Equal(0, report.RootElement.GetProperty("inputCount").GetInt32());
        Assert.False(report.RootElement.GetProperty("liveQualified").GetBoolean());
        Assert.False(report.RootElement.GetProperty("actionReady").GetBoolean());
    }

    [Fact]
    public async Task DisabledAdapterNeverStarts()
    {
        await using var scheduler = new OperationScheduler(_root, Path.Combine(_root, "locks"));
        var runtime = new W101ModuleProbeRuntime(_ => true, () => 42, (_, _) => true, _ => new(1280, 720),
            (_, _, _, _, _, _, _, _) => throw new InvalidOperationException("Must not read"));
        Assert.Throws<InvalidOperationException>(() => W101ModuleProbe.Submit(scheduler, new(_target), _launch, runtime));
    }

    [Fact]
    public async Task FailureReportPreservesBoundedMessageAndStageDiagnostics()
    {
        var details = new W101ModuleDiagnostics("image-admission", 12, 1280, 720, 1280, 720, 33, 10001, 10000, -1, 20);
        var runtime = new W101ModuleProbeRuntime(_ => true, () => 42, (_, _) => true, _ => new(1280, 720),
            (_, _, _, _, _, _, _, _) =>
            {
                var error = new InvalidDataException(new string('x', 600) + "\r\n");
                error.Data["w101ModuleDiagnostics"] = details;
                throw error;
            });
        await using var scheduler = new OperationScheduler(_root, Path.Combine(_root, "locks"));
        var receipt = await W101ModuleProbe.Submit(scheduler, new(_target, true), _launch, runtime).Completion;
        using var report = JsonDocument.Parse(System.IO.File.ReadAllText(Assert.Single(receipt.Artifacts)));
        Assert.Equal("InvalidDataException", report.RootElement.GetProperty("errorType").GetString());
        Assert.Equal(512, report.RootElement.GetProperty("errorMessage").GetString()!.Length);
        Assert.Equal("module-observation", report.RootElement.GetProperty("stage").GetString());
        Assert.Equal("image-admission", report.RootElement.GetProperty("diagnostics").GetProperty("Stage").GetString());
        Assert.Equal(-1, report.RootElement.GetProperty("diagnostics").GetProperty("FrameAgeMs").GetDouble());
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
