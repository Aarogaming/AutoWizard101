using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class RoutineReplayTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "maelstrom-routine-replay-" + Guid.NewGuid().ToString("N"));
    public RoutineReplayTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private (string Graph, string Manifest, string Events) Prepare(string game)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "TestData", "Routines", game);
        var manifest = Path.Combine(_directory, "session.json"); var events = Path.Combine(_directory, "events.jsonl");
        File.Copy(Path.Combine(source, "session.json"), manifest);
        File.Copy(Path.Combine(source, "events.jsonl"), events);
        var graph = RoutineGraphs.BuiltIn(game == "dance" ? "pet-dance-visit-draft" : "dodge-movement-draft");
        var graphPath = Path.Combine(_directory, "routine.json");
        File.WriteAllText(graphPath, JsonSerializer.Serialize(graph, RoutineGraphs.Json));
        return (graphPath, manifest, events);
    }

    [Theory]
    [InlineData("dance", 8)]
    [InlineData("dodge", 6)]
    public void SessionReplaysAreDeterministicHaveNoDispatchAndDoNotClaimLiveQualification(string game, int actions)
    {
        var files = Prepare(game);
        var first = Path.Combine(_directory, "first.json"); var second = Path.Combine(_directory, "second.json");
        Assert.True(RoutineReplay.Run(files.Graph, files.Manifest, first));
        Assert.True(RoutineReplay.Run(files.Graph, files.Manifest, second));
        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        using var report = JsonDocument.Parse(File.ReadAllText(first)); var data = report.RootElement;
        Assert.Equal(actions, data.GetProperty("proposal_count").GetInt32());
        Assert.Equal(actions, data.GetProperty("receipt_count").GetInt32());
        Assert.Equal(0, data.GetProperty("desktop_dispatch_count").GetInt32());
        Assert.False(data.GetProperty("live_qualified").GetBoolean());
        Assert.Equal(64, data.GetProperty("sha256_graph").GetString()!.Length);
        Assert.Throws<IOException>(() => RoutineReplay.Run(files.Graph, files.Manifest, first));
    }

    [Fact]
    public void MissingReceiptsNeverInventCompletion()
    {
        var files = Prepare("dance");
        File.WriteAllLines(files.Events, File.ReadAllLines(files.Events).Where(line => !line.Contains("action_receipt")));
        var output = Path.Combine(_directory, "incomplete.json");
        Assert.False(RoutineReplay.Run(files.Graph, files.Manifest, output));
        using var report = JsonDocument.Parse(File.ReadAllText(output));
        Assert.Equal(0, report.RootElement.GetProperty("receipt_count").GetInt32());
        Assert.False(report.RootElement.GetProperty("completed").GetBoolean());
    }

    [Fact]
    public void RejectedLastReceiptIsReportedEvenWithoutAnotherObservation()
    {
        var files = Prepare("dance");
        var lines = File.ReadAllLines(files.Events).Take(3).ToArray();
        lines[^1] = lines[^1].Replace("\"accepted\":true", "\"accepted\":false");
        File.WriteAllLines(files.Events, lines);
        var output = Path.Combine(_directory, "rejected.json");
        Assert.False(RoutineReplay.Run(files.Graph, files.Manifest, output));
        using var report = JsonDocument.Parse(File.ReadAllText(output));
        Assert.True(report.RootElement.GetProperty("stopped").GetBoolean());
        Assert.Equal("dispatch-unconfirmed", report.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public void UserActivityAfterCompletionCannotEraseTheRecordedOutcomeOrRestartActions()
    {
        var files = Prepare("dance");
        var observation = new RoutineObservation(1000000, "Unknown", new("new-window", "800x600", "different-visit"), UserActive: true);
        var tail = JsonSerializer.Serialize(new
        {
            event_id = "after-completion",
            sequence = 1000,
            time_ms = 1000000,
            kind = "observation",
            payload = new { routine_observation = observation }
        }, new JsonSerializerOptions(RoutineGraphs.Json) { WriteIndented = false });
        File.AppendAllText(files.Events, tail + Environment.NewLine);
        var output = Path.Combine(_directory, "completed-then-takeover.json");
        Assert.True(RoutineReplay.Run(files.Graph, files.Manifest, output));
        using var report = JsonDocument.Parse(File.ReadAllText(output));
        Assert.Equal(8, report.RootElement.GetProperty("proposal_count").GetInt32());
        Assert.False(report.RootElement.GetProperty("stopped").GetBoolean());
    }

    [Fact]
    public void DuplicateEventsAndEscapingEventPathsAreRejectedBeforeReports()
    {
        var files = Prepare("dance");
        var lines = File.ReadAllLines(files.Events); File.AppendAllText(files.Events, lines[0] + Environment.NewLine);
        var output = Path.Combine(_directory, "invalid.json");
        Assert.Throws<InvalidDataException>(() => RoutineReplay.Run(files.Graph, files.Manifest, output));
        Assert.False(File.Exists(output));
        File.WriteAllText(files.Manifest, File.ReadAllText(files.Manifest).Replace("events.jsonl", "../events.jsonl"));
        Assert.Throws<InvalidDataException>(() => RoutineReplay.Run(files.Graph, files.Manifest, output));
        Assert.False(File.Exists(output));
    }
}
