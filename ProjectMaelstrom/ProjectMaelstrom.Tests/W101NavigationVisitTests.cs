using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101NavigationVisitTests : IDisposable
{
    [Theory]
    [InlineData("moving", 1, true, 5)]
    [InlineData("stuck", 1, true, 0)]
    [InlineData("missing-start", 0, false, 0)]
    [InlineData("missing-end", 1, false, 0)]
    [InlineData("waypoint-hit", 1, true, 40)]
    [InlineData("waypoint-miss", 1, false, 0)]
    public async Task MeasuresActualDisplacementWithoutInferringArrival(string scenario, int expectedPulses, bool valid, double distance)
    {
        var pulses = 0;
        var prepared = W101NavigationVisit.Prepare(new(_target, true), _launch, true, "W", 150,
            validation: _validation);
        var now = DateTime.UtcNow.Ticks;
        Task<W101UiTreeResult> Read(int index, CancellationToken token)
        {
            var end = index == 25;
            var point = end && scenario == "moving" ? new W101PosePoint(3, 4, 0) : new(0, 0, 0);
            if (end && scenario == "waypoint-hit") point = new(0, -40, 0);
            var missing = scenario == "missing-start" && !end || scenario == "missing-end" && end;
            return Task.FromResult(W101QuestConversationTests.Read(0, null) with
            {
                Target = _target,
                PoseCandidate = missing ? null : new("archived-wizwalker-actor-body", "candidate", false, false,
                    point, 0, now - (end ? 1 : 2), 6, 72)
            });
        }
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "runs"), Path.Combine(_root, "locks"));
        var outcome = await scheduler.Submit(prepared.Request, (context, token) => W101NavigationVisit.MeasureAsync(context,
            () => true, Read, _ => { pulses++; return Task.FromResult(new OperationOutcome(OperationState.Completed, "pulse", [])); }, token,
            scenario.StartsWith("waypoint-", StringComparison.Ordinal) ? 40 : 0)).Completion;
        Assert.Equal(expectedPulses, pulses);
        Assert.Equal(valid, outcome.State == OperationState.Completed);
        if (expectedPulses == 1)
        {
            using var report = JsonDocument.Parse(File.ReadAllText(Assert.Single(outcome.Artifacts)));
            Assert.False(report.RootElement.GetProperty("arrivalVerified").GetBoolean());
            Assert.Equal(scenario == "waypoint-hit", report.RootElement.GetProperty("waypointCandidateReached").GetBoolean());
            if (valid) Assert.Equal(distance, report.RootElement.GetProperty("planarDistance").GetDouble());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdleRetriesAreFreshBoundedStagesAndNeverReplayGameplay(bool resume)
    {
        var ready = true; var pulses = 0; var idleCalls = 0; var resumes = 0;
        var runtime = new W101NavigationVisitRuntime(() => ready,
            (_, mode, _) =>
            {
                if (mode == "idle" && ++idleCalls == 1)
                    return Task.FromResult(new OperationOutcome(OperationState.Failed, "transient-reading", []));
                return Task.FromResult(new OperationOutcome(OperationState.Completed, "observed", []));
            }, (_, _) =>
            {
                pulses++; if (resume) ready = false;
                return Task.FromResult(new OperationOutcome(OperationState.Completed, "observed-pulse", []));
            }, (_, _) => { resumes++; ready = true; return Task.FromResult(true); });
        var job = W101NavigationVisit.Prepare(new(_target, true), _launch, true, "A", 100,
            runtime, _validation, new(ResumeAfterQuiet: resume));
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "runs"), Path.Combine(_root, "locks"));
        var result = await scheduler.Submit(job.Request, job.Execute).Completion;
        Assert.Equal(OperationState.Completed, result.State);
        Assert.Equal(1, pulses); Assert.Equal(2, idleCalls); Assert.Equal(resume ? 1 : 0, resumes);
        using var report = JsonDocument.Parse(File.ReadAllText(Assert.Single(result.Artifacts)));
        Assert.Equal(4, report.RootElement.GetProperty("stages").GetArrayLength());
    }

    [Theory]
    [InlineData("between-stages", true, 1)]
    [InlineData("before-idle", true, 1)]
    [InlineData("keyboard-stop", false, 0)]
    public async Task FocusPauseResumesReleasedCheckpointsWithoutReplayingBody(string scenario, bool expectedComplete, int expectedIdle)
    {
        var ready = scenario != "keyboard-stop";
        var pulses = 0; var idleCalls = 0; var waits = 0; var resets = 0;
        var runtime = new W101NavigationVisitRuntime(() => ready,
            (_, mode, _) =>
            {
                if (mode == "wake" && scenario == "between-stages") ready = false;
                if (mode == "idle") idleCalls++;
                return Task.FromResult(OperationOutcome.Completed("fresh observation"));
            }, (_, _) =>
            {
                pulses++;
                if (scenario == "before-idle") ready = false;
                return Task.FromResult(OperationOutcome.Completed("released pulse"));
            }, (_, _) => { resets++; return Task.FromResult(true); }, _ =>
            {
                waits++;
                if (scenario == "keyboard-stop") return Task.FromResult(false);
                ready = true; return Task.FromResult(true);
            });
        var job = W101NavigationVisit.Prepare(new(_target, true), _launch, true, "A", 100,
            runtime, _validation, new(IdleRetries: 0));
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "runs"), Path.Combine(_root, "locks"));
        var result = await scheduler.Submit(job.Request, job.Execute).Completion;
        Assert.Equal(expectedComplete, result.State == OperationState.Completed);
        Assert.Equal(expectedComplete ? 1 : 0, pulses);
        Assert.Equal(expectedIdle, idleCalls);
        Assert.True(waits >= 1); Assert.Equal(0, resets);
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "navigation-visit-" + Guid.NewGuid().ToString("N"));
    private readonly WizwalkerLaunch _launch;
    private readonly WizwalkerTarget _target;
    private readonly WizwalkerRuntime _validation = new(_ => true, (_, _, _, _) => throw new InvalidOperationException());

    public W101NavigationVisitTests()
    {
        Directory.CreateDirectory(_root);
        string Make(string name) { var path = Path.Combine(_root, name); File.WriteAllText(path, "inert"); return path; }
        _launch = new(Make("python.exe"), Make("w101_ui_tree.py"), _root);
        Make("wizwalker_adapter.py");
        var image = Make("WizardGraphicalClient.exe");
        _target = W101QuestConversationTests.Target with { ImagePath = image, ImageSha256 = GameExperiments.Digest(image) };
    }

    [Theory]
    [InlineData("success", "Completed", true, 3)]
    [InlineData("pulse-rejected", "Failed", true, 3)]
    [InlineData("pulse-clean-error", "Failed", true, 3)]
    [InlineData("pulse-timeout", "Failed", true, 3)]
    [InlineData("wake-rejected", "Failed", true, 2)]
    [InlineData("idle-rejected", "Failed", false, 3)]
    [InlineData("takeover", "Failed", false, 2)]
    [InlineData("unclean-pulse", "CleanupFailed", false, 2)]
    [InlineData("unclean-wake", "CleanupFailed", false, 1)]
    public async Task VisitUsesOneOwnerAndRestoresIdleOnlyWithinOriginalAuthority(string scenario, string expectedState,
        bool expectedIdle, int expectedStages)
    {
        var ready = true;
        GameInputLease? owner = null;
        var contexts = new List<OperationContext>();
        var modes = new List<string>();
        void Inspect(OperationContext context, string mode, CancellationToken token)
        {
            Assert.True(token.CanBeCanceled); Assert.False(token.IsCancellationRequested);
            owner ??= context.Input;
            Assert.NotNull(owner); Assert.Same(owner, context.Input); Assert.Same(owner, GameInputLease.Current);
            Assert.Equal("pid-" + _target.ProcessId + "-start-" + _target.StartedUtcTicks, context.Request.Target);
            Assert.Equal(mode == "pulse" ? 12 : 20, context.Request.TimeoutSeconds);
            contexts.Add(context); modes.Add(mode);
            // The same filename must be available independently to each stage.
            File.WriteAllText(Path.Combine(context.Directory, "same-evidence-name.json"), "synthetic");
        }
        var runtime = new W101NavigationVisitRuntime(() => ready,
            (context, mode, token) =>
            {
                Inspect(context, mode, token);
                if (scenario == "unclean-wake" && mode == "wake")
                    return Task.FromResult(new OperationOutcome(OperationState.CleanupFailed, "Synthetic uncertain hook cleanup", []));
                return Task.FromResult(new OperationOutcome(scenario == mode + "-rejected" ? OperationState.Failed : OperationState.Completed,
                    "Synthetic " + mode + " result", []));
            }, (context, token) =>
            {
                Inspect(context, "pulse", token);
                if (scenario == "takeover") ready = false;
                if (scenario == "unclean-pulse")
                {
                    context.Input!.RequireRecovery("Synthetic unreleased input");
                    return Task.FromResult(new OperationOutcome(OperationState.CleanupFailed, "Synthetic uncertain release", []));
                }
                if (scenario == "pulse-clean-error") throw new IOException("Synthetic clean capture failure");
                if (scenario == "pulse-timeout") throw new OperationCanceledException("Synthetic exhausted stage budget");
                return Task.FromResult(new OperationOutcome(scenario == "pulse-rejected" ? OperationState.Failed : OperationState.Completed,
                    "Synthetic pulse evidence, not arrival", []));
            });
        var job = W101NavigationVisit.Prepare(new(_target, true), _launch, true, "A", 100, runtime, _validation, new(IdleRetries: 0));
        Assert.Equal(85, job.Request.TimeoutSeconds);
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "runs"), Path.Combine(_root, "locks"));
        var result = await scheduler.Submit(job.Request, job.Execute).Completion;
        Assert.Equal(expectedState, result.State.ToString());
        Assert.Equal(expectedStages, contexts.Count);
        Assert.Equal(contexts.Count, contexts.Select(c => c.Request.Id).Distinct().Count());
        Assert.Equal(contexts.Count, contexts.Select(c => c.Directory).Distinct().Count());
        Assert.All(contexts, context =>
        {
            Assert.NotEqual(job.Request.Id, context.Request.Id);
            // Submission freezes the caller's pins; all stages share that frozen snapshot.
            Assert.Same(contexts[0].Request.Pins, context.Request.Pins);
            Assert.Equal(job.Request.Pins.OrderBy(pin => pin.Key), context.Request.Pins.OrderBy(pin => pin.Key));
            Assert.True(File.Exists(Path.Combine(context.Directory, "visit-stage.json")));
        });
        Assert.Equal("wake", modes[0]);
        if (expectedIdle) Assert.Equal("idle", modes[^1]);
        if (scenario == "wake-rejected") Assert.DoesNotContain("pulse", modes);
        using var report = JsonDocument.Parse(File.ReadAllText(Assert.Single(result.Artifacts)));
        Assert.Equal(expectedIdle, report.RootElement.GetProperty("idleVerified").GetBoolean());
        Assert.False(report.RootElement.GetProperty("arrivalVerified").GetBoolean());
        Assert.False(report.RootElement.GetProperty("routeQualified").GetBoolean());
        if (scenario == "takeover") Assert.Equal("target-owner-or-takeover-changed", report.RootElement.GetProperty("idleSkippedReason").GetString());
        if (scenario.StartsWith("unclean", StringComparison.Ordinal)) Assert.Equal("cleanup-uncertain", report.RootElement.GetProperty("idleSkippedReason").GetString());
    }

    [Fact]
    public async Task ParentCancellationNeverStartsIdleCleanup()
    {
        var pulseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowPulseExit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var modes = new List<string>();
        var runtime = new W101NavigationVisitRuntime(() => true,
            (_, mode, _) => { modes.Add(mode); return Task.FromResult(OperationOutcome.Completed("Synthetic wake")); },
            async (_, token) =>
            {
                pulseEntered.SetResult();
                await allowPulseExit.Task;
                token.ThrowIfCancellationRequested();
                return OperationOutcome.Completed("Unreachable");
            });
        var job = W101NavigationVisit.Prepare(new(_target, true), _launch, true, "W", 100, runtime, _validation, new(IdleRetries: 0));
        await using var scheduler = new OperationScheduler(Path.Combine(_root, "runs"), Path.Combine(_root, "locks"));
        var ticket = scheduler.Submit(job.Request, job.Execute);
        await pulseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        ticket.Cancel(); allowPulseExit.SetResult();
        var result = await ticket.Completion;
        Assert.Equal(OperationState.Cancelled, result.State);
        Assert.Equal(new[] { "wake" }, modes);
        using var report = JsonDocument.Parse(File.ReadAllText(Assert.Single(result.Artifacts)));
        Assert.Equal("parent-cancelled", report.RootElement.GetProperty("idleSkippedReason").GetString());
    }

    [Theory]
    [InlineData("X", 40)]
    [InlineData("W", 301)]
    public void SelectionUsesExistingNavigationAdmission(string key, int duration) => Assert.Throws<InvalidDataException>(() =>
        W101NavigationVisit.Prepare(new(_target, true), _launch, true, key, duration, validation: _validation));

    public void Dispose() => Directory.Delete(_root, true);
}
