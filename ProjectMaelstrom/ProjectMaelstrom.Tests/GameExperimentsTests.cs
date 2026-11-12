using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class GameExperimentsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-experiment-" + Guid.NewGuid().ToString("N"));
    private readonly GameExperiments _store;
    private readonly string _baseline, _candidate;
    public GameExperimentsTests()
    {
        Directory.CreateDirectory(_root);
        _store = new(_root);
        _baseline = Path.Combine(_root, "baseline.json"); _candidate = Path.Combine(_root, "candidate.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContextV12", "pack.json"), _baseline);
        var candidate = JsonNode.Parse(File.ReadAllText(_baseline))!; candidate["version"] = "0.99.0-test";
        File.WriteAllText(_candidate, candidate.ToJsonString());
        Directory.CreateDirectory(Path.Combine(_root, "tools"));
        foreach (var name in new[] { "wsl_dodge_launch.sh", "wsl_dodge_session.sh", "wsl_dodge_guest.py" }) File.WriteAllText(Path.Combine(_root, "tools", name), "test fixture; never executed");
        Directory.CreateDirectory(Path.Combine(_root, ".local", "wsl-dodge-step-game"));
        File.WriteAllText(Path.Combine(_root, ".local", "wsl-dodge-step-game", "project.godot"), "test");
        foreach (var name in new[] { "godot/Godot_v4.7.2-stable_linux.x86_64", "root-current/usr/bin/Xvfb", "root-current/usr/bin/xdotool" })
        {
            var path = Path.Combine(_root, ".local", "wsl-dodge-tools", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "fixture; never executed");
        }
    }
    private ExperimentPlan Create() => _store.Load(_store.CreateDodge("Test", "Does this improve?", _root, _baseline, _candidate, DodgeExperimentRunner.Fingerprints(_root)));

    [Fact]
    public void PlansFreezePacksAndReserveDisjointValidationBeforePlaying()
    {
        var plan = Create();
        Assert.Equal(28, plan.Trials.Count);
        Assert.Equal(24, plan.Trials.Select(t => t.Seed).Distinct().Count());
        var development = plan.Trials.Where(t => t.Stage == "development").ToArray();
        Assert.Equal(4, development.Where(t => t.Role == "baseline").Count());
        Assert.Equal(development.Where(t => t.Role == "baseline").Select(t => t.Seed), development.Where(t => t.Role == "candidate").Select(t => t.Seed));
        Assert.DoesNotContain(plan.Trials.Where(t => t.Stage != "development"), t => development.Any(d => d.Seed == t.Seed));
        var second = Create();
        Assert.DoesNotContain(second.Trials, t => plan.Trials.Any(p => p.Seed == t.Seed));
        using var registry = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, ".local", "dodge-holdouts", "registry.json")));
        Assert.Equal(48, registry.RootElement.GetProperty("used_seeds").GetArrayLength());
        File.WriteAllText(_candidate, "source changed");
        Assert.Equal(plan.Id, _store.Load(plan.Id).Id);
        File.AppendAllText(Path.Combine(_store.DirectoryPath(plan.Id), "candidate.json"), " ");
        Assert.Throws<InvalidDataException>(() => _store.Load(plan.Id));
    }

    [Fact]
    public void MissingAndInterruptedTrialsCannotProduceMeanOrAdvance()
    {
        var plan = Create(); var first = plan.Trials[0];
        _store.Begin(plan, first);
        var rows = _store.Results(plan);
        Assert.Null(_store.Next(plan, rows));
        Assert.Null(GameExperiments.Statistics(plan, rows, "development", "baseline").Mean);
        Assert.Contains("running", GameExperiments.Verdict(plan, rows));
        Assert.Throws<IOException>(() => _store.Begin(plan, first));
        var path = _store.Preserve(plan.Id, "Interrupted; retained for diagnosis.");
        Assert.True(File.Exists(path));
        Assert.Contains("No installed or active pack was changed", File.ReadAllText(path));
        Assert.Equal("interrupted", GameExperiments.OwnerStatus(rows[0] with { OwnerPid = -1 }));
        Assert.Equal("unfinished", GameExperiments.OwnerStatus(rows[0] with { OwnerPid = null }));
    }

    [Fact]
    public void DevelopmentAndTwoValidationSetsHaveSeparateGates()
    {
        var plan = Create();
        ExperimentResult Row(ExperimentTrial t, int score) => new(t.Number, "complete", score, "game-over", "test", new());
        var rows = plan.Trials.Take(8).Select(t => Row(t, t.Role == "baseline" ? 30 : 60)).ToList();
        Assert.True(GameExperiments.DevelopmentPassed(plan, rows));
        Assert.Equal("validation-1", _store.Next(plan, rows)!.Stage);
        rows.AddRange(plan.Trials.Skip(8).Take(10).Select(t => Row(t, 60)));
        Assert.Contains("pending", GameExperiments.Verdict(plan, rows));
        rows.AddRange(plan.Trials.Skip(18).Select(t => Row(t, 40)));
        Assert.Contains("below target", GameExperiments.Verdict(plan, rows));
        var skewed = new[] { 8, 8, 8, 8, 8, 8, 8, 8, 218, 218 };
        var stats = GameExperiments.Statistics(plan, plan.Trials.Skip(18).Select((t, i) => Row(t, skewed[i])).ToList(), "validation-2", "candidate");
        Assert.True(stats.Mean >= 50); Assert.Equal(8, stats.Median); Assert.Equal(2, stats.AtTarget);
    }

    [Fact]
    public void ReportAndAuditAreCopiedVerifiedAndPreservedIndependently()
    {
        var plan = Create(); var trial = plan.Trials[0]; var report = Report(plan, trial, 32);
        _store.Begin(plan, trial); _store.Finish(plan, trial, report);
        File.WriteAllText(report, "source removed");
        var row = Assert.Single(_store.Results(plan)); Assert.Equal(32, row.Score);
        var saved = Path.Combine(Path.GetDirectoryName(_store.ResultPath(plan, trial))!, "evaluation-audit.jsonl");
        File.AppendAllText(saved, "changed");
        Assert.Throws<InvalidDataException>(() => _store.Results(plan));
    }

    [Theory]
    [InlineData("seed")]
    [InlineData("speed")]
    [InlineData("audit")]
    [InlineData("pack")]
    public void MismatchedEvidenceIsRejected(string change)
    {
        var plan = Create(); var trial = plan.Trials[0]; var path = Report(plan, trial, 40);
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        if (change == "seed") json["seed"] = 0;
        if (change == "speed") json["training"]!["speed"] = 1;
        if (change == "audit") json["evaluation_audit_sha256"] = null;
        if (change == "pack") json["pack"]!["Sha256"] = new string('f', 64);
        File.WriteAllText(path, json.ToJsonString()); _store.Begin(plan, trial);
        Assert.ThrowsAny<Exception>(() => _store.Finish(plan, trial, path));
        Assert.Null(_store.Next(plan, _store.Results(plan)));
    }

    [Fact]
    public async Task RunnerUsesFrozenPackAndCompletesOnlyItsRequestedGame()
    {
        var plan = Create(); var called = 0;
        await DodgeExperimentRunner.RunAsync(_store, plan.Id, 1, 1, null, CancellationToken.None, (p, t, pack, token) =>
        {
            called++; Assert.Equal(Path.Combine(_store.DirectoryPath(p.Id), "baseline.json"), pack);
            return Task.FromResult(Report(p, t, 51));
        });
        Assert.Equal(1, called); Assert.Equal(51, Assert.Single(_store.Results(plan)).Score);
        Assert.Equal(1, _store.Next(plan, _store.Results(plan))!.Number);
    }

    [Fact]
    public async Task CancellingAnActiveTrialPersistsFailureAndReleasesStudyLock()
    {
        var plan = Create(); using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DodgeExperimentRunner.RunAsync(_store, plan.Id, 1, 1, null, cancellation.Token, async (p, t, pack, token) =>
        {
            cancellation.Cancel(); await Task.Delay(100, token); return "unreachable";
        }));
        Assert.Equal("incomplete", Assert.Single(_store.Results(plan)).Status);
        using var studyLock = DodgeExperimentRunner.AcquireStudy(_root);
    }

    [Fact]
    public async Task ChangedEnvironmentCannotStartAnotherGame()
    {
        var plan = Create(); File.AppendAllText(Path.Combine(_root, "tools", "wsl_dodge_guest.py"), "changed");
        await Assert.ThrowsAsync<InvalidDataException>(() => DodgeExperimentRunner.RunAsync(_store, plan.Id, 1, 1, null, CancellationToken.None));
        Assert.Empty(_store.Results(plan));
    }

    [Fact]
    public void PathTraversalAndIdenticalCandidatesAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => _store.DirectoryPath("../escape"));
        Assert.Throws<InvalidDataException>(() => GameExperiments.Child(_root, "..\\escape"));
        Assert.Throws<InvalidDataException>(() => _store.CreateDodge("Test", "Question", _root, _baseline, _baseline, new()));
    }

    [Fact]
    public void FootageAttachmentCopiesLabelsAndKeepsSourceEditsSeparate()
    {
        var plan = Create(); var recording = TeachingRecording.Start(_root, "Dodge", "video-import");
        using var bitmap = new System.Drawing.Bitmap(480, 720);
        recording.AddImportedFrame(bitmap, 0); recording.AddImportedFrame(bitmap, 250); recording.Finish(); recording.Label(0, "menu");
        var assessment = FootageAssessment.Attach(_store, plan.Id, recording, "Local recorded game; visual state examples.");
        var frozen = TeachingRecording.Open(Path.Combine(Path.GetDirectoryName(assessment)!, "recording.json"));
        Assert.Equal("menu", frozen.Frames[0].State);
        recording.Label(0, "changed"); Assert.Equal("menu", frozen.Frames[0].State);
        using var json = JsonDocument.Parse(File.ReadAllText(assessment));
        Assert.False(json.RootElement.GetProperty("quality").GetProperty("ActionTrainingEligible").GetBoolean());
        Assert.Equal("visual-examples-only", json.RootElement.GetProperty("use").GetString());
        Assert.Equal(2, FootageAssessment.Verify(assessment).Frames);
        File.AppendAllText(Path.Combine(Path.GetDirectoryName(assessment)!, "frame-0000.png"), "changed");
        Assert.Throws<InvalidDataException>(() => FootageAssessment.Verify(assessment));
    }

    [Fact]
    public async Task OtherGamesCanPreserveVisualStudiesWithoutEnablingControls()
    {
        var id = _store.CreateFootageStudy("Wizard101 minigames", "Which visual states are stable?", "wizard101");
        var plan = _store.Load(id); Assert.Empty(plan.Trials); Assert.Empty(plan.Packs);
        Assert.Null(_store.Next(plan, []));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DodgeExperimentRunner.RunAsync(_store, id, 1, 25, null, CancellationToken.None));
        var recording = TeachingRecording.Start(_root, "wizard101", "test");
        using var image = new System.Drawing.Bitmap(480, 720); recording.AddImportedFrame(image, 0); recording.Finish();
        FootageAssessment.Attach(_store, id, recording, "Synthetic fixture for workflow test, not a live game.");
        var receipt = _store.Preserve(id, "Retain visual examples for review.");
        Assert.Contains("Gameplay performance has not been tested", File.ReadAllText(receipt));
        Assert.Throws<InvalidDataException>(() => _store.CreateFootageStudy("Invalid", "Question", "../game"));
    }

    private string Report(ExperimentPlan plan, ExperimentTrial trial, int score)
    {
        var folder = Path.Combine(_root, "synthetic-evidence", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var image = new byte[54 + 480 * 720 * 4]; image[0] = (byte)'B'; image[1] = (byte)'M'; File.WriteAllBytes(Path.Combine(folder, "game-over.bmp"), image);
        var audit = Path.Combine(folder, "evaluation-audit.jsonl"); File.WriteAllText(audit, "Synthetic test evidence, not gameplay.\n");
        var pack = plan.Packs.Single(p => p.Role == trial.Role);
        var path = Path.Combine(folder, "report.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            schema_version = 1,
            mode = "wsl-private-endurance",
            seed = trial.Seed,
            training = new { mode = "stepped", speed = .25, benchmark = true, fixed_fps = 30, clock = "simulated", action_limit = 2400, request_limit = 9600, time_limit_seconds = 1200, observation_cadence = "advance-before-decision" },
            pack = new { pack.Id, pack.Version, pack.Sha256 },
            evaluation = new { score, alive = false, source = "private-game-evaluator-not-policy" },
            stop_reason = "game-over",
            survival_observation = "game-over-observed",
            last_unrecognized_frame = "game-over.bmp",
            evaluation_audit_sha256 = GameExperiments.Digest(audit)
        }));
        return path;
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
