using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class TeacherActionReplayTests
{
    private static JsonDocument Evidence(bool accepted = true, bool released = true, bool fresh = true, string initialScene = "NativeIdle")
    {
        var binding = new RoutineBinding("pid-1-start-2", "geometry", "visit");
        var before = new RoutineObservation(100, initialScene, binding, true, true, true, true, true);
        var action = new ModuleAction("wizard101.wake-idle", "keyboard-pulse", "ESCAPE", 40, "wake");
        return JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            finalIdleVerified = true,
            events = new object[] {
            new { kind = "observation", observed = before, sample = new { Sample = new { Reading = new { States = new { nativeIdle = true, menu = false }, StableStates = new { nativeIdle = true, menu = false } } } }, decision = new ModuleDecision("test", "start", "proposed", "original", action.ActionId, action) },
            new { kind = "receipt", receipt = new RoutineReceipt("original", action.ActionId, 150, accepted, released) },
            new { kind = "observation", observed = before with { TimeMs = 200, Scene = "MenuObserved", Fresh = fresh } }
        }
        }));
    }
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void UnconfirmedInputOrStaleOutcomeCannotTeach(bool accepted, bool released, bool fresh)
    {
        using var doc = Evidence(accepted, released, fresh);
        Assert.Throws<InvalidDataException>(() => TeacherActionReplay.Link(doc.RootElement));
    }
    [Theory]
    [InlineData("Unknown")]
    [InlineData("Loading")]
    public void AmbiguousBaselineCannotTeachAnActionOutcome(string scene)
    {
        using var doc = Evidence(initialScene: scene);
        Assert.Throws<InvalidDataException>(() => TeacherActionReplay.Link(doc.RootElement));
    }
    [Theory]
    [InlineData("UserActive")]
    [InlineData("Cancelled")]
    public void InterveningTakeoverCannotBeSkippedToTeachLaterOutcome(string flag)
    {
        using var doc = Evidence();
        var report = System.Text.Json.Nodes.JsonNode.Parse(doc.RootElement.GetRawText())!;
        var events = report["events"]!.AsArray();
        var interrupted = events[2]!.DeepClone();
        interrupted["observed"]![flag] = true;
        interrupted["observed"]!["TimeMs"] = 175;
        events.Insert(2, interrupted);
        using var changed = JsonDocument.Parse(report.ToJsonString());
        Assert.Throws<InvalidDataException>(() => TeacherActionReplay.Link(changed.RootElement));
    }
    [Fact]
    public void RecordedReceiptAndChangedStateProduceReplayWithSourceMapping()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            using var evidence = Evidence(); var report = Path.Combine(folder, "teacher.json"); File.WriteAllText(report, evidence.RootElement.GetRawText());
            var video = Path.Combine(folder, "capture.mp4"); File.WriteAllText(video, "synthetic-video-fixture-not-live");
            var inputs = Path.Combine(folder, "input-events.json");
            File.WriteAllText(inputs, JsonSerializer.Serialize(new
            {
                VideoCaptureOriginMs = 0,
                CaptureEndMs = 500,
                VideoVerified = true,
                Truncated = false,
                TargetProcessId = 1,
                TargetProcessStartUtcTicks = 2,
                VideoSha256 = GameExperiments.Digest(video)
            }));
            var output = Path.Combine(folder, "export"); TeacherActionReplay.Export(report, inputs, output);
            using var replay = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "replay.json")));
            Assert.True(replay.RootElement.GetProperty("completed").GetBoolean());
            Assert.Equal(1, replay.RootElement.GetProperty("receipt_count").GetInt32());
            Assert.False(replay.RootElement.GetProperty("live_qualified").GetBoolean());
            Assert.Contains("original", File.ReadAllText(Path.Combine(output, "provenance.json")));
            var card = new JobCardSpec("test", "review.routine", "Review", Conditions:
                [new("nativeIdle", "eq", JsonSerializer.SerializeToElement(true)), new("menu", "eq", JsonSerializer.SerializeToElement(false))]);
            Assert.True(RoutineReplay.Run(Path.Combine(output, "routine.json"), Path.Combine(output, "session.json"), Path.Combine(output, "conditional.json"), card));
            var blocked = card with { Conditions = [new("energy", "gte", JsonSerializer.SerializeToElement(1))] };
            Assert.False(RoutineReplay.Run(Path.Combine(output, "routine.json"), Path.Combine(output, "session.json"), Path.Combine(output, "unknown.json"), blocked));
            File.AppendAllText(video, "tamper");
            Assert.Throws<InvalidDataException>(() => TeacherActionReplay.Export(report, inputs, Path.Combine(folder, "tampered")));
        }
        finally { Directory.Delete(folder, true); }
    }
}
