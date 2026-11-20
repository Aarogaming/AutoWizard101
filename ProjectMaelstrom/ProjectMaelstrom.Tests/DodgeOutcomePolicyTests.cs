using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class DodgeOutcomePolicyTests
{
    private static JsonObject Model()
    {
        var means = new JsonArray(); var scales = new JsonArray(); var weights = new JsonArray();
        for (var i = 0; i < 71; i++) { means.Add(0); scales.Add(1); weights.Add(i == 65 ? -2 : 0); }
        weights.Add(0);
        return new JsonObject { ["schema_version"] = 1, ["feature_version"] = DodgeOutcomePolicy.FeatureVersion, ["algorithm"] = "binary-logistic-observational-survival", ["horizon_ms"] = 5000, ["means"] = means, ["scales"] = scales, ["weights"] = weights };
    }

    private static DodgeGameContext Context() => DodgeLivePack.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContext", "pack.json")).Context!;

    [Theory]
    [InlineData("outcome-features.json", 190)]
    [InlineData("outcome-features-speed400.json", 400)]
    public void InferenceFeaturesMatchOfflineTrainerForEveryAction(string filename, int featurePlayerSpeed)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeLearned", filename);
        using var examples = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var example in examples.RootElement.EnumerateArray())
        {
            var observation = example.GetProperty("observation");
            var p = observation.GetProperty("before_player");
            var player = new PointF(p.GetProperty("X").GetSingle(), p.GetProperty("Y").GetSingle());
            var threats = observation.GetProperty("threats").EnumerateArray().Select(t =>
            {
                var pos = t.GetProperty("position"); var velocity = t.GetProperty("velocity_px_s");
                return new DodgeThreatMotion(new PointF(pos.GetProperty("X").GetSingle(), pos.GetProperty("Y").GetSingle()), new PointF(velocity.GetProperty("X").GetSingle(), velocity.GetProperty("Y").GetSingle()), t.GetProperty("velocity_known").GetBoolean());
            }).ToArray();
            var state = DodgeLearnedPolicy.Features(player, threats, featurePlayerSpeed);
            for (var action = 0; action < 9; action++)
            {
                var actual = DodgeOutcomePolicy.Features(state, action);
                var expected = example.GetProperty("outcome_features")[action].EnumerateArray().Select(x => x.GetDouble()).ToArray();
                Assert.Equal(expected.Length, actual.Length);
                for (var i = 0; i < actual.Length; i++) Assert.InRange(Math.Abs(actual[i] - expected[i]), 0, .00001);
            }
        }
    }

    [Fact]
    public void FeaturesRespectActionAndCanReversePreferenceWhenThreatMoves()
    {
        var state = new double[52]; state[2] = .4; state[3] = .2; state[4] = .3; state[5] = -.1;
        state[28] = .8; state[29] = .7; state[30] = .6; state[31] = .5;
        var left = DodgeOutcomePolicy.Features(state, 2);
        Assert.Equal(71, left.Length); Assert.Equal(1, left[54]);
        Assert.Equal(.8, left[61]); Assert.Equal(-.4, left[65]); Assert.Equal(-.3, left[66]);
        var hold = DodgeOutcomePolicy.Features(state, 8);
        Assert.Equal(1, hold[60]); Assert.All(hold[61..], value => Assert.Equal(0, value));
        using var json = JsonDocument.Parse(Model().ToJsonString());
        var model = DodgeOutcomePolicy.Parse(json.RootElement);
        Assert.True(model.Score(left) > model.Score(DodgeOutcomePolicy.Features(state, 3)));
        state[2] = -.4;
        Assert.True(model.Score(DodgeOutcomePolicy.Features(state, 3)) > model.Score(DodgeOutcomePolicy.Features(state, 2)));
    }

    [Fact]
    public void ModelRespectsEdgesAndRejectsBadScales()
    {
        using var json = JsonDocument.Parse(Model().ToJsonString());
        var model = DodgeOutcomePolicy.Parse(json.RootElement);
        DodgeThreatMotion[] threats = [new(new PointF(.5f, .5f), PointF.Empty, false)];
        Assert.DoesNotContain("Left", model.Recommend(new PointF(5f / 480, .5f), threats, Context(), 60));
        Assert.Equal("Hold", model.Recommend(new PointF(.5f, .5f), [], Context(), 60));
        var bad = Model(); bad["scales"]![0] = 0;
        using var invalid = JsonDocument.Parse(bad.ToJsonString());
        Assert.Throws<InvalidDataException>(() => DodgeOutcomePolicy.Parse(invalid.RootElement));
        var speedModel = Model(); speedModel["feature_version"] = DodgeOutcomePolicy.FeatureVersion + "-speed400"; speedModel["feature_player_speed"] = 400;
        using var speedJson = JsonDocument.Parse(speedModel.ToJsonString());
        var calibrated = DodgeOutcomePolicy.Parse(speedJson.RootElement);
        Assert.False(calibrated.MatchesContext(Context()));
        Assert.True(calibrated.MatchesContext(Context() with { PlayerRadius = 34, ThreatRadius = 50 }));
        speedModel["feature_player_speed"] = 190;
        using var mismatch = JsonDocument.Parse(speedModel.ToJsonString());
        Assert.Throws<InvalidDataException>(() => DodgeOutcomePolicy.Parse(mismatch.RootElement));
        speedModel["feature_player_speed"] = 400; speedModel["horizon_ms"] = 1000;
        using var shortHorizon = JsonDocument.Parse(speedModel.ToJsonString());
        Assert.NotNull(DodgeOutcomePolicy.Parse(shortHorizon.RootElement));
        speedModel["algorithm"] = "linear-observational-action-advantage";
        using var advantage = JsonDocument.Parse(speedModel.ToJsonString());
        Assert.NotNull(DodgeOutcomePolicy.Parse(advantage.RootElement));
        speedModel["weights"]![0] = 1;
        using var malformedAdvantage = JsonDocument.Parse(speedModel.ToJsonString());
        Assert.Throws<InvalidDataException>(() => DodgeOutcomePolicy.Parse(malformedAdvantage.RootElement));
        speedModel["weights"]![0] = 0;
        speedModel["feature_version"] = DodgeOutcomePolicy.FeatureVersion;
        speedModel["feature_player_speed"] = 190;
        using var uncalibrated = JsonDocument.Parse(speedModel.ToJsonString());
        Assert.Throws<InvalidDataException>(() => DodgeOutcomePolicy.Parse(uncalibrated.RootElement));
    }

    [Fact]
    public void EpisodeModelUsesItsDistinctHorizonAndActionWeights()
    {
        var source = Model();
        source["algorithm"] = "linear-observational-episode-advantage";
        source["feature_version"] = DodgeOutcomePolicy.FeatureVersion + "-speed400";
        source["feature_player_speed"] = 400;
        source["horizon_ms"] = 0;
        source["weights"]![65] = 0;
        source["weights"]![52] = 0.5;
        using var valid = JsonDocument.Parse(source.ToJsonString());
        var model = DodgeOutcomePolicy.Parse(valid.RootElement);
        var state = new double[52];
        Assert.Equal(0.5, model.Score(DodgeOutcomePolicy.Features(state, 0)));
        Assert.Equal(0, model.Score(DodgeOutcomePolicy.Features(state, 1)));
        var pack = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContextV12", "pack.json")))!;
        pack["context"]!["avoidance_policy"] = "directional-outcome-reference-v37";
        pack["experimental_outcome_model"] = source.DeepClone();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try { File.WriteAllText(path, pack.ToJsonString()); Assert.NotNull(DodgeLivePack.Load(path).OutcomePolicy); }
        finally { File.Delete(path); }
        source["horizon_ms"] = 5000;
        using var wrongHorizon = JsonDocument.Parse(source.ToJsonString());
        Assert.Throws<InvalidDataException>(() => DodgeOutcomePolicy.Parse(wrongHorizon.RootElement));
        source["horizon_ms"] = 0;
        source["means"]![0] = 1;
        using var malformed = JsonDocument.Parse(source.ToJsonString());
        Assert.Throws<InvalidDataException>(() => DodgeOutcomePolicy.Parse(malformed.RootElement));
    }

    [Fact]
    public void OutcomePreferenceCannotOverrideImmediateClearance()
    {
        var unsafePreference = Model(); unsafePreference["weights"]![65] = 2;
        using var json = JsonDocument.Parse(unsafePreference.ToJsonString());
        var model = DodgeOutcomePolicy.Parse(json.RootElement);
        DodgeThreatMotion[] threats = [new(new PointF(.72f, .5f), PointF.Empty, true)];
        var player = new PointF(.5f, .5f);
        var state = DodgeLearnedPolicy.Features(player, threats);
        Assert.True(model.Score(DodgeOutcomePolicy.Features(state, 3)) > model.Score(DodgeOutcomePolicy.Features(state, 2)));
        var choice = model.Decide(player, threats, Context(), 120);
        Assert.NotEqual("Right", choice.Advice);
        Assert.Equal("Right", choice.BeforeClearanceAdvice);
        Assert.True(choice.ClearanceOverride);
        Assert.True(choice.SafeOptions > 0);
    }

    [Fact]
    public void OutcomePreferenceRetainsRoomNearAnEdge()
    {
        var modelData = Model(); modelData["weights"]![53] = .5; modelData["weights"]![65] = 0;
        using var json = JsonDocument.Parse(modelData.ToJsonString());
        var model = DodgeOutcomePolicy.Parse(json.RootElement);
        DodgeThreatMotion[] threats = [new(new PointF(.5f, .1f), PointF.Empty, true)];
        Assert.NotEqual("Down", model.Recommend(new PointF(.5f, .85f), threats, Context(), 120));
    }

    [Fact]
    public void TurnAwareFallbackIsUsedOnlyWhenEveryOptionIsDangerous()
    {
        using var json = JsonDocument.Parse(Model().ToJsonString());
        var model = DodgeOutcomePolicy.Parse(json.RootElement);
        var player = new PointF(.5f, .5f);
        DodgeThreatMotion[] threats = [new(player, PointF.Empty, true)];
        var original = model.Decide(player, threats, Context(), 60);
        var turnAware = model.Decide(player, threats, Context(), 60, turnAwareFallback: true);
        Assert.Equal(0, original.SafeOptions); Assert.Equal("clearance", original.Fallback);
        Assert.Equal(0, turnAware.SafeOptions); Assert.Equal("two-phase", turnAware.Fallback);
    }

    [Fact]
    public void ReferencePolicyRetainsSafeDirectionalChoiceInsteadOfModelPreference()
    {
        var modelData = Model(); modelData["weights"]![65] = 2;
        using var json = JsonDocument.Parse(modelData.ToJsonString());
        var model = DodgeOutcomePolicy.Parse(json.RootElement);
        var player = new PointF(.5f, .5f);
        DodgeThreatMotion[] threats = [new(new PointF(.8f, .5f), PointF.Empty, true)];
        var reference = DodgeDirectionalAvoidance.Recommend(player, threats, Context(), 60);
        var choice = model.Decide(player, threats, Context(), 60, retainSafeReference: true);
        Assert.True(choice.ReferenceRetained);
        Assert.Equal(reference, choice.ReferenceAdvice);
        Assert.Equal(reference, choice.Advice);
        Assert.NotEqual(choice.BeforeClearanceAdvice, choice.Advice);
        Assert.True(choice.SelectedClearance >= 10);
    }

    [Fact]
    public void ReferencePolicyUsesFallbackWhenNoMoveHasClearance()
    {
        using var json = JsonDocument.Parse(Model().ToJsonString());
        var model = DodgeOutcomePolicy.Parse(json.RootElement);
        var player = new PointF(.5f, .5f);
        DodgeThreatMotion[] threats = [new(player, PointF.Empty, true)];
        var choice = model.Decide(player, threats, Context(), 60, turnAwareFallback: true, retainSafeReference: true);
        Assert.NotNull(choice.ReferenceAdvice);
        Assert.False(choice.ReferenceRetained);
        Assert.Equal(0, choice.SafeOptions);
        Assert.Equal("two-phase", choice.Fallback);
    }

    [Fact]
    public void EarlyEscapeUsesOnlyAVisuallyAcceptableMoveOnRecordedDevelopmentScenes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeLearned", "early-escape-development.json");
        using var fixture = JsonDocument.Parse(File.ReadAllText(path));
        var model = DodgeOutcomePolicy.Parse(fixture.RootElement.GetProperty("model"));
        var context = Context() with { PlayerRadius = 34, ThreatRadius = 50, HorizonMs = 1400 };
        var changes = 0;
        foreach (var observation in fixture.RootElement.GetProperty("observations").EnumerateArray())
        {
            var p = observation.GetProperty("before_player");
            var player = new PointF(p.GetProperty("X").GetSingle(), p.GetProperty("Y").GetSingle());
            var threats = observation.GetProperty("threats").EnumerateArray().Select(t =>
            {
                var pos = t.GetProperty("position"); var velocity = t.GetProperty("velocity_px_s");
                return new DodgeThreatMotion(new PointF(pos.GetProperty("X").GetSingle(), pos.GetProperty("Y").GetSingle()), new PointF(velocity.GetProperty("X").GetSingle(), velocity.GetProperty("Y").GetSingle()), t.GetProperty("velocity_known").GetBoolean());
            }).ToArray();
            var reference = model.Decide(player, threats, context, 60, turnAwareFallback: true, retainSafeReference: true);
            var early = model.Decide(player, threats, context, 60, turnAwareFallback: true, retainSafeReference: true, earlyEscape: true);
            Assert.Equal(reference.SafeOptions, early.SafeOptions);
            if (early.Fallback != "two-phase-early") continue;
            changes++;
            Assert.InRange(early.SafeOptions, 1, 3);
            Assert.True(early.SelectedClearance >= 10);
            Assert.NotEqual(reference.Advice, early.Advice);
        }
        Assert.True(changes > 0, "The saved development scenes must exercise early escape.");
    }

    [Fact]
    public void OutcomePackRequiresModelAndMatchingPolicy()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContext", "pack.json");
        var pack = JsonNode.Parse(File.ReadAllText(source))!;
        pack["context"]!["avoidance_policy"] = "directional-outcome-v34";
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, pack.ToJsonString());
            Assert.Throws<InvalidDataException>(() => DodgeLivePack.Load(path));
            pack["experimental_outcome_model"] = Model();
            File.WriteAllText(path, pack.ToJsonString());
            Assert.NotNull(DodgeLivePack.Load(path).OutcomePolicy);
            pack["context"]!["avoidance_policy"] = "directional-outcome-turn-v36";
            File.WriteAllText(path, pack.ToJsonString());
            Assert.NotNull(DodgeLivePack.Load(path).OutcomePolicy);
            pack["context"]!["avoidance_policy"] = "directional-outcome-reference-v37";
            File.WriteAllText(path, pack.ToJsonString());
            Assert.NotNull(DodgeLivePack.Load(path).OutcomePolicy);
            pack["context"]!["avoidance_policy"] = "directional-outcome-escape-v38";
            File.WriteAllText(path, pack.ToJsonString());
            Assert.NotNull(DodgeLivePack.Load(path).OutcomePolicy);
            pack["context"]!["avoidance_policy"] = "motion-v1";
            File.WriteAllText(path, pack.ToJsonString());
            Assert.Throws<InvalidDataException>(() => DodgeLivePack.Load(path));
        }
        finally { File.Delete(path); }
    }
}
