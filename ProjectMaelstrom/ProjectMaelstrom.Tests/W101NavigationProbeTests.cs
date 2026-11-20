using System.Drawing;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101NavigationProbeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "navigation-probe-" + Guid.NewGuid().ToString("N"));
    private readonly WizwalkerLaunch _launch;
    private readonly WizwalkerTarget _target;
    private readonly WizwalkerRuntime _validation = new(_ => true, (_, _, _, _) => throw new InvalidOperationException());

    public W101NavigationProbeTests()
    {
        Directory.CreateDirectory(_root);
        string Make(string name) { var path = Path.Combine(_root, name); File.WriteAllText(path, "inert"); return path; }
        _launch = new(Make("python.exe"), Make("w101_ui_tree.py"), _root);
        Make("wizwalker_adapter.py");
        var image = Make("WizardGraphicalClient.exe");
        _target = W101QuestConversationTests.Target with { ImagePath = image, ImageSha256 = GameExperiments.Digest(image) };
    }

    [Theory]
    [InlineData("fresh", 1, "Completed")]
    [InlineData("same-pixels", 1, "Completed")]
    [InlineData("nativeIdle", 0, "Failed")]
    [InlineData("menu", 0, "Failed")]
    [InlineData("dialogue", 0, "Failed")]
    [InlineData("loading", 0, "Failed")]
    [InlineData("unknown-state", 0, "Failed")]
    [InlineData("visual-menu", 0, "Failed")]
    [InlineData("stale-tree", 0, "Failed")]
    [InlineData("stale-image", 0, "Failed")]
    [InlineData("wrong-target", 0, "Failed")]
    [InlineData("wrong-binding", 0, "Failed")]
    [InlineData("takeover", 0, "Failed")]
    [InlineData("unreleased", 1, "CleanupFailed")]
    [InlineData("rejected", 1, "Failed")]
    [InlineData("old-after", 1, "Failed")]
    [InlineData("cached-after-tree", 1, "Failed")]
    [InlineData("unclean", 0, "CleanupFailed")]
    [InlineData("clean-read-error", 0, "Failed")]
    public async Task OneExplicitPulseCollectsEvidenceWithoutClaimingArrival(string scenario, int expectedInputs, string expectedState)
    {
        double now = 10000; int inputs = 0, reads = 0; bool ready = true;
        var geometry = new Size(1280, 720);
        var runtime = new W101NavigationProbeRuntime(() => ready, geometry,
            (context, index, binding, _) =>
            {
                Assert.Same(context.Input, GameInputLease.Current); reads++; now += 500;
                if (scenario == "clean-read-error") throw new IOException("Synthetic clean observation failure");
                if (scenario == "takeover") ready = false;
                var states = new Dictionary<string, bool?> { ["nativeIdle"] = false, ["menu"] = false, ["dialogue"] = false };
                if (states.ContainsKey(scenario)) states[scenario] = true;
                if (scenario == "unknown-state") states["menu"] = null;
                var stamp = scenario == "stale-tree" ? now - 2001 : scenario == "cached-after-tree" && index == 1 ? now - 550 : now;
                var reading = W101QuestConversationTests.Read(stamp, null) with
                {
                    Target = scenario == "wrong-target" ? _target with { ProcessId = 99 } : _target,
                    States = states,
                    StableStates = new(states),
                    Loading = scenario == "loading",
                    CleanupConfirmed = scenario != "unclean"
                };
                var captureTime = scenario == "stale-image" ? now - 301 : scenario == "old-after" && index == 1 ? now - 500 : now;
                var hash = scenario == "same-pixels" || index == 0 ? new string('a', 64) : new string('b', 64);
                var visual = new ModuleVisualRecognition(new(hash, captureTime, geometry,
                    scenario == "wrong-binding" ? binding with { ContextId = "other" } : binding, "local-windows-ocr"),
                    scenario == "visual-menu" ? "MenuObserved" : "Unknown", [], [], "synthetic-fixture");
                return Task.FromResult(new W101ModuleObservation(new(reading, new()), visual));
            }, (context, proposal, fresh, _) =>
            {
                Assert.True(fresh()); Assert.Same(context.Input, GameInputLease.Current);
                Assert.Equal("W", proposal.Action!.Key); Assert.Equal(100, proposal.Action.DurationMs);
                inputs++; now += 100;
                return Task.FromResult(new RoutineReceipt(proposal.ProposalId!, proposal.ActionId!, now,
                    scenario != "rejected", scenario != "unreleased"));
            }, () => now, () => W101QuestConversationTests.Stamp(now));
        var job = W101NavigationProbe.Prepare(new(_target, true), _launch, true, "W", 100, runtime, _validation);
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "runs"), Path.Combine(_root, "locks"));
        var result = await scheduler.Submit(job.Request, job.Execute).Completion;
        Assert.Equal(expectedState, result.State.ToString());
        Assert.Equal(expectedInputs, inputs);
        Assert.InRange(reads, 1, 2);
        using var report = JsonDocument.Parse(File.ReadAllText(Assert.Single(result.Artifacts)));
        Assert.False(report.RootElement.GetProperty("arrivalVerified").GetBoolean());
        Assert.False(report.RootElement.GetProperty("routeQualified").GetBoolean());
        if (scenario == "same-pixels") Assert.False(report.RootElement.GetProperty("imageChanged").GetBoolean());
    }

    [Theory]
    [InlineData("X", 40)]
    [InlineData("W", 0)]
    [InlineData("A", 301)]
    public void RejectsUnboundedOrUnreviewedActions(string key, int duration) =>
        Assert.Throws<InvalidDataException>(() => W101NavigationProbe.Prepare(new(_target, true), _launch, true, key, duration, validation: _validation));

    [Fact]
    public void RequiresExplicitEnablement() => Assert.Throws<InvalidOperationException>(() =>
        W101NavigationProbe.Prepare(new(_target, true), _launch, false, "W", 100, validation: _validation));

    public void Dispose() => Directory.Delete(_root, true);
}
