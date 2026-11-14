using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class ConditionalRoutineReplayTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "conditional-replay-" + Guid.NewGuid().ToString("N"));
    public ConditionalRoutineReplayTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    [Theory]
    [InlineData("pass", true)]
    [InlineData("missing", false)]
    [InlineData("unmet", false)]
    [InlineData("stale", false)]
    [InlineData("future", false)]
    [InlineData("context", false)]
    [InlineData("late-unmet", false)]
    [InlineData("no-receipts", false)]
    public void OneReplayRequiresBothConditionsAndRecordedOutcomes(string mode, bool expected)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "TestData", "Routines", "dance");
        var manifest = Path.Combine(_root, "session.json"); File.Copy(Path.Combine(source, "session.json"), manifest);
        var graph = RoutineGraphs.BuiltIn("pet-dance-visit-draft");
        var graphPath = Path.Combine(_root, "routine.json"); File.WriteAllText(graphPath, JsonSerializer.Serialize(graph, RoutineGraphs.Json));
        var rows = new List<string>(); var index = 0;
        foreach (var line in File.ReadAllLines(Path.Combine(source, "events.jsonl")))
        {
            var row = JsonNode.Parse(line)!; var payload = row["payload"]!;
            if (mode == "no-receipts" && row["kind"]!.GetValue<string>() == "action_receipt") continue;
            if (payload["routine_observation"] is { } observation && mode != "missing")
            {
                var binding = observation["binding"]!; var time = row["time_ms"]!.GetValue<double>();
                payload["recorded_facts"] = new JsonObject
                {
                    ["game_id"] = graph.GameId,
                    ["target_id"] = binding["target_id"]!.GetValue<string>(),
                    ["context_id"] = mode == "context" ? "other" : binding["context_id"]!.GetValue<string>(),
                    ["observed_time_ms"] = mode == "stale" ? time - 2001 : mode == "future" ? time + 1 : time,
                    ["values"] = new JsonObject { ["energy"] = mode == "unmet" || mode == "late-unmet" && index > 1 ? 0 : 10 }
                }; index++;
            }
            rows.Add(row.ToJsonString());
        }
        File.WriteAllLines(Path.Combine(_root, "events.jsonl"), rows);
        var card = new JobCardSpec(Guid.NewGuid().ToString("N"), "review.routine", "Replay", Conditions:
            [new("energy", "gte", JsonSerializer.SerializeToElement(10))]);
        var output = Path.Combine(_root, "result.json");
        Assert.Equal(expected, RoutineReplay.Run(graphPath, manifest, output, card, JobFactCatalog.All));
        using var report = JsonDocument.Parse(File.ReadAllText(output));
        Assert.Equal(RecordedCardReview.Hash(card), report.RootElement.GetProperty("card_hash").GetString());
        Assert.True(report.RootElement.GetProperty("conditions_checked").GetBoolean());
        Assert.Equal(0, report.RootElement.GetProperty("desktop_dispatch_count").GetInt32());
        Assert.False(report.RootElement.GetProperty("live_qualified").GetBoolean());
    }
}
