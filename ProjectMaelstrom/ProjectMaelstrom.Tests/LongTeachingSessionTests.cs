using ProjectMaelstrom.Utilities;
using Xunit;
using System.Text.Json;

namespace ProjectMaelstrom.Tests;

public sealed class LongTeachingSessionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EditedCardReplaysOnlyWithObservedReceipt(bool includeReceipt)
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var graph = LongTeachingSession.Compile(new("test", "Interaction", 0, 10000, "ready", "interact", "done"), "any-game");
            var binding = new RoutineBinding("target", "geometry", "visit");
            var manifest = Path.Combine(folder, "session.json"); var graphPath = Path.Combine(folder, "routine.json");
            File.WriteAllText(graphPath, JsonSerializer.Serialize(graph, RoutineGraphs.Json));
            File.WriteAllText(manifest, JsonSerializer.Serialize(new { schema_version = 1, session_id = "recording", clock_origin = "synthetic", events_file = "events.jsonl", routine_binding = binding }, RoutineGraphs.Json));
            var options = new JsonSerializerOptions(RoutineGraphs.Json) { WriteIndented = false };
            var events = new List<string>();
            events.Add(JsonSerializer.Serialize(new { event_id = "start", sequence = 0, time_ms = 0, kind = "observation", payload = new { routine_observation = new RoutineObservation(0, "ready", binding, true, true, true, true) } }, options));
            if (includeReceipt) events.Add(JsonSerializer.Serialize(new { event_id = "receipt", sequence = 1, time_ms = 10, kind = "action_receipt", payload = new { routine_receipt = new RoutineReceipt("recording/test/1", "interact", 10, true, true) } }, options));
            events.Add(JsonSerializer.Serialize(new { event_id = "outcome", sequence = 2, time_ms = 20, kind = "observation", payload = new { routine_observation = new RoutineObservation(20, "done", binding, true, true, true, true) } }, options));
            File.WriteAllLines(Path.Combine(folder, "events.jsonl"), events);
            Assert.Equal(includeReceipt, RoutineReplay.Run(graphPath, manifest, Path.Combine(folder, "report.json")));
        }
        finally { Directory.Delete(folder, true); }
    }
    [Fact]
    public void HourIsThirtyBoundedChunks()
    {
        var plan = LongTeachingSession.Plan(60);
        Assert.Equal(30, plan.Length);
        Assert.All(plan, p => Assert.Equal(TimeSpan.FromMinutes(2), p));
        Assert.Equal(TimeSpan.FromMinutes(1), LongTeachingSession.Plan(3)[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => LongTeachingSession.Plan(61));
    }

    [Fact]
    public void CardUsesSharedGraphAndRejectsScripts()
    {
        var activity = new TeachingActivity("test", "Visit", 0, 10000, "ready", "interact", "done");
        var graph = LongTeachingSession.Compile(activity, "test-game");
        Assert.Equal("interact", graph.Nodes[0].Edges[0].ActionId);
        Assert.Equal(1, graph.MaxActions);
        Assert.Throws<InvalidDataException>(() => LongTeachingSession.Compile(activity with { Action = "cmd /c" }, "test-game"));
    }

    [Fact]
    public void RecoveryPreservesCompletedChunksAndClockGaps()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "teaching-session.json");
            var session = new TeachingSession(1, "test", "any-game", 100000, "recording",
                [new("chunk-001", 0, 120000, "complete", 100050), new("chunk-002", 121000, 121000, "recording", null)],
                [new("note", "Loading", 100, 200, "ready", "interact", "done", "I entered the portal here.")], new(123, 456, 789, "test-game"));
            LongTeachingSession.Save(path, session);
            var read = LongTeachingSession.Read(path);
            Assert.Equal("complete", read.Chunks[0].Status);
            Assert.Equal("I entered the portal here.", read.Activities[0].Commentary);
            Assert.Equal(456, read.Target!.StartedUtcTicks);
            Assert.Equal(1000, read.Chunks[1].StartMs - read.Chunks[0].EndMs);
            Assert.Throws<InvalidDataException>(() => LongTeachingSession.Save(path, session with
            { Chunks = [new("../escape", 0, 1, "complete", 0)] }));
            Assert.Equal(2, LongTeachingSession.Read(path).Chunks.Length);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void AnnotationOutsideEvidenceIsRejected()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var session = new TeachingSession(1, "test", "game", 0, "complete", [new("chunk-001", 0, 100, "complete", 0)],
                [new("a", "Beyond recording", 0, 101, "ready", "interact", "done")]);
            Assert.Throws<InvalidDataException>(() => LongTeachingSession.Save(Path.Combine(folder, "index.json"), session));
        }
        finally { Directory.Delete(folder, true); }
    }
}
