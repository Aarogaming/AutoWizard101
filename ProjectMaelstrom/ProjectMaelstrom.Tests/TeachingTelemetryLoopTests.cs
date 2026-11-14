using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class TeachingTelemetryLoopTests
{
    [Fact]
    public async Task CancellationAwaitsReaderCleanupAndPreservesCompletedBatch()
    {
        using var stop = new CancellationTokenSource(); var cleaned = false; var calls = 0;
        var result = await TeachingTelemetryLoop.CollectAsync(async token =>
        {
            calls++; stop.Cancel(); await Task.Delay(1); cleaned = true; return "receipt";
        }, stop.Token);
        Assert.True(cleaned); Assert.Equal(1, calls); Assert.Equal("receipt", Assert.Single(result));
    }

    [Fact]
    public async Task ObserverFailureDoesNotRetry()
    {
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(() => TeachingTelemetryLoop.CollectAsync(_ =>
        { calls++; throw new IOException("reader cleanup failed"); }, CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task AlreadyCancelledDoesNotStartObserver()
    {
        using var stop = new CancellationTokenSource(); stop.Cancel();
        var result = await TeachingTelemetryLoop.CollectAsync(_ => throw new Exception("must not start"), stop.Token);
        Assert.Empty(result);
    }

    [Fact]
    public async Task BudgetIsExplicitAndSequential()
    {
        var active = 0; var maximum = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => TeachingTelemetryLoop.CollectAsync(async _ =>
        { maximum = Math.Max(maximum, ++active); await Task.Delay(1); active--; return "receipt"; }, CancellationToken.None, 2));
        Assert.Equal(1, maximum); Assert.Equal(0, active);
    }

    [Fact]
    public void RecordingRequestRequiresExactTargetAndOptionalAdapterScope()
    {
        var root = Path.GetTempPath();
        var request = new TeachingRecordingRequest(new(1, 2, 3, "other-game"), root, 1, Path.Combine(root, Guid.NewGuid().ToString("N")));
        TeachingRecordingCli.Validate(request);
        Assert.Throws<InvalidDataException>(() => TeachingRecordingCli.Validate(request with { EnableW101Telemetry = true, Workspace = root }));
        Assert.Throws<ArgumentOutOfRangeException>(() => TeachingRecordingCli.Validate(request with { Minutes = 61 }));
    }

    [Fact]
    public async Task RecordingBatchesBecomeTimestampedEditableCardWithoutInventedReplaySuccess()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var target = new TeachingTarget(1, 2, 3, "WizardGraphicalClient");
            var session = new TeachingSession(1, "test", "wizard101", 0, "complete", [new("chunk-001", 0, 1000, "complete", 0)],
                [new("menu-test", "Menu visit", 100, 900, "unknown", "menu.open", "unknown", "Opened settings.")], target, 100000000);
            var path = Path.Combine(folder, "teaching-session.json"); LongTeachingSession.Save(path, session);
            var request = Path.Combine(folder, "ui-request.json");
            File.WriteAllText(request, JsonSerializer.Serialize(new { Target = new { ProcessId = 1, StartedUtcTicks = 2, Handle = 3, ProcessName = "WizardGraphicalClient" } }));
            File.WriteAllLines(Path.Combine(folder, "ui-observations.jsonl"), new[]
            {
                JsonSerializer.Serialize(new { kind = "tree", observedUtcTicks = 101000000, statesObservedUtcTicks = 101000000, stableStates = new { interactionPrompt = true }, nodes = Array.Empty<object>() }),
                JsonSerializer.Serialize(new { kind = "tree", observedUtcTicks = 109000000, statesObservedUtcTicks = 109000000, stableStates = new { menu = true }, nodes = Array.Empty<object>() })
            });
            using var stop = new CancellationTokenSource();
            var batches = await TeachingTelemetryLoop.CollectAsync(_ => { stop.Cancel(); return Task.FromResult<string?>(request); }, stop.Token);
            var track = TeachingTelemetryLoop.MergeW101(batches, path, session);
            LongTeachingSession.Save(path, session with { StateTrack = Path.GetFileName(track) });
            var proposal = Assert.Single(TeachingReviewCli.Review(path));
            Assert.Equal("interactionPrompt", proposal.Activity.Trigger); Assert.Equal("menu", proposal.Activity.Outcome);
            var graph = LongTeachingSession.Compile(proposal.Activity, session.Game);
            var annotationPath = Path.Combine(folder, "annotation.json"); File.WriteAllText(annotationPath, JsonSerializer.Serialize(proposal.Activity));
            TeachingReviewCli.Run(["--teaching-annotate", path, annotationPath]);
            var graphPath = Path.Combine(folder, "routine.json");
            TeachingReviewCli.Run(["--teaching-export-card", path, proposal.Activity.Id, graphPath]);
            Assert.Equal(graph.Id, RoutineGraphs.Read(File.ReadAllText(graphPath)).Id);
            var replay = Path.Combine(folder, "session.json");
            File.WriteAllText(replay, JsonSerializer.Serialize(new { schema_version = 1, session_id = "test", clock_origin = "fixture", events_file = "events.jsonl", routine_binding = new RoutineBinding("target", "geometry", "visit") }, RoutineGraphs.Json));
            File.WriteAllText(Path.Combine(folder, "events.jsonl"), "");
            Assert.False(RoutineReplay.Run(graphPath, replay, Path.Combine(folder, "replay.json")));
            Assert.Equal("Opened settings.", proposal.Activity.Commentary);
        }
        finally { Directory.Delete(folder, true); }
    }
}
