using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class DodgeLearnedPolicyTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeLearned", name);
    private static DodgeGameContext Context() => DodgeLivePack.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContext", "pack.json")).Context!;

    [Fact]
    public void LocalInferenceMatchesTheOfflineTrainer()
    {
        using var modelJson = JsonDocument.Parse(File.ReadAllText(Fixture("model.json")));
        using var exampleJson = JsonDocument.Parse(File.ReadAllText(Fixture("examples.json")));
        var model = DodgeLearnedPolicy.Parse(modelJson.RootElement);
        foreach (var example in exampleJson.RootElement.EnumerateArray())
        {
            var observation = example.GetProperty("observation");
            var p = observation.GetProperty("before_player");
            var player = new PointF(p.GetProperty("X").GetSingle(), p.GetProperty("Y").GetSingle());
            var threats = observation.GetProperty("threats").EnumerateArray().Select(t =>
            {
                var pos = t.GetProperty("position"); var velocity = t.GetProperty("velocity_px_s");
                return new DodgeThreatMotion(new PointF(pos.GetProperty("X").GetSingle(), pos.GetProperty("Y").GetSingle()), new PointF(velocity.GetProperty("X").GetSingle(), velocity.GetProperty("Y").GetSingle()), t.GetProperty("velocity_known").GetBoolean());
            }).ToArray();
            var actual = DodgeLearnedPolicy.Features(player, threats);
            var expected = example.GetProperty("features").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            Assert.Equal(expected.Length, actual.Length);
            for (var i = 0; i < actual.Length; i++) Assert.InRange(Math.Abs(actual[i] - expected[i]), 0, .00001);
            var predictedAction = modelJson.RootElement.GetProperty("actions")[model.PredictIndex(actual)].GetString();
            Assert.Equal(example.GetProperty("prediction").GetString(), predictedAction);
        }
    }

    [Fact]
    public void LearnedActionsAreBoundedAndMalformedModelsAreRejected()
    {
        using var modelJson = JsonDocument.Parse(File.ReadAllText(Fixture("model.json")));
        var model = DodgeLearnedPolicy.Parse(modelJson.RootElement);
        Assert.Equal("Hold", model.Recommend(new PointF(.5f, .5f), [], Context(), 60));
        DodgeThreatMotion[] threat = [new(new PointF(.5f, .5f), new PointF(0, 0), false)];
        Assert.DoesNotContain("Left", model.Recommend(new PointF(5f / 480, .5f), threat, Context(), 60));
        var malformed = JsonNode.Parse(File.ReadAllText(Fixture("model.json")))!;
        malformed["scales"]![0] = 0;
        using var bad = JsonDocument.Parse(malformed.ToJsonString());
        Assert.Throws<InvalidDataException>(() => DodgeLearnedPolicy.Parse(bad.RootElement));
    }

    [Fact]
    public void AGamePackMustPairTheLearnedPolicyWithItsModel()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContext", "pack.json");
        var pack = JsonNode.Parse(File.ReadAllText(root))!;
        pack["context"]!["avoidance_policy"] = "directional-learned-v33";
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, pack.ToJsonString());
            Assert.Throws<InvalidDataException>(() => DodgeLivePack.Load(path));
            pack["experimental_model"] = JsonNode.Parse(File.ReadAllText(Fixture("model.json")));
            File.WriteAllText(path, pack.ToJsonString());
            var loaded = DodgeLivePack.Load(path);
            Assert.NotNull(loaded.LearnedPolicy);
            Assert.Equal("directional-learned-v33", loaded.Context!.AvoidancePolicy);
            pack["context"]!["avoidance_policy"] = "motion-v1";
            File.WriteAllText(path, pack.ToJsonString());
            Assert.Throws<InvalidDataException>(() => DodgeLivePack.Load(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AModelCanLearnToHoldWhenThreatsAreVisible()
    {
        var root = JsonNode.Parse(File.ReadAllText(Fixture("model.json")))!;
        root["actions"]!.AsArray().Add("hold");
        var row = new JsonArray();
        for (var i = 0; i < 52; i++) row.Add(0);
        row.Add(100);
        root["weights"]!.AsArray().Add(row);
        using var json = JsonDocument.Parse(root.ToJsonString());
        var model = DodgeLearnedPolicy.Parse(json.RootElement);
        DodgeThreatMotion[] threats = [new(new PointF(.6f, .5f), new PointF(0, 0), false)];
        Assert.Equal("Hold", model.Recommend(new PointF(.5f, .5f), threats, Context(), 60));
    }
}
