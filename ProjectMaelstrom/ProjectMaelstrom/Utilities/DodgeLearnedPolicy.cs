using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Small local behavioral-cloning model. The surrounding isolated player
/// still owns observation freshness, identity, action and boundary checks.
/// </summary>
internal sealed class DodgeLearnedPolicy
{
    internal const string FeatureVersion = "dodge-relative-and-clearance-v2";
    private const int FeatureCount = 52;
    private const double Diagonal = 0.7071067811865476;
    private static readonly string[] Actions = ["move-up", "move-down", "move-left", "move-right", "move-up-left", "move-up-right", "move-down-left", "move-down-right"];
    private static readonly string[] ActionsWithHold = [.. Actions, "hold"];
    private static readonly (double X, double Y)[] Directions = [(0, -1), (0, 1), (-1, 0), (1, 0), (-Diagonal, -Diagonal), (Diagonal, -Diagonal), (-Diagonal, Diagonal), (Diagonal, Diagonal)];
    private readonly double[] _means;
    private readonly double[] _scales;
    private readonly double[][] _weights;
    private readonly string[] _actions;

    private DodgeLearnedPolicy(double[] means, double[] scales, double[][] weights, string[] actions) { _means = means; _scales = scales; _weights = weights; _actions = actions; }

    internal static DodgeLearnedPolicy Parse(JsonElement model)
    {
        if (model.ValueKind != JsonValueKind.Object || model.GetProperty("schema_version").GetInt32() != 1 || model.GetProperty("feature_version").GetString() != FeatureVersion)
            throw new InvalidDataException("Unsupported learned Dodge model.");
        var actions = model.GetProperty("actions");
        if (actions.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Learned Dodge action list does not match the registry.");
        var names = actions.EnumerateArray().Select(x => x.GetString()).ToArray();
        if (!names.SequenceEqual(Actions) && !names.SequenceEqual(ActionsWithHold)) throw new InvalidDataException("Learned Dodge action list does not match the registry.");
        var means = Numbers(model.GetProperty("means"), FeatureCount, false);
        var scales = Numbers(model.GetProperty("scales"), FeatureCount, true);
        var weightData = model.GetProperty("weights");
        if (weightData.ValueKind != JsonValueKind.Array || weightData.GetArrayLength() != names.Length)
            throw new InvalidDataException("Invalid learned Dodge model weights.");
        var weights = weightData.EnumerateArray().Select(row => Numbers(row, FeatureCount + 1, false)).ToArray();
        return new(means, scales, weights, names.Select(x => x!).ToArray());
    }

    private static double[] Numbers(JsonElement array, int count, bool positive)
    {
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() != count) throw new InvalidDataException("Invalid learned Dodge model dimensions.");
        var values = array.EnumerateArray().Select(x => x.GetDouble()).ToArray();
        if (values.Any(x => !double.IsFinite(x) || Math.Abs(x) > 10000 || (positive && x < .000001))) throw new InvalidDataException("Invalid learned Dodge model value.");
        return values;
    }

    internal static double[] Features(PointF player, IReadOnlyList<DodgeThreatMotion> threats, int featurePlayerSpeed = 190)
    {
        if (featurePlayerSpeed is not (190 or 400) || !float.IsFinite(player.X) || !float.IsFinite(player.Y) || player.X is < 0 or > 1 || player.Y is < 0 or > 1 ||
            threats.Any(t => !float.IsFinite(t.Position.X) || !float.IsFinite(t.Position.Y) || !float.IsFinite(t.VelocityPxPerSecond.X) || !float.IsFinite(t.VelocityPxPerSecond.Y)))
            throw new InvalidDataException("Invalid visual features for learned Dodge model.");
        var px = (double)player.X; var py = (double)player.Y;
        var nearest = threats.Select(t => new { Dx = (double)t.Position.X - px, Dy = (double)t.Position.Y - py, Vx = (double)t.VelocityPxPerSecond.X, Vy = (double)t.VelocityPxPerSecond.Y, Known = t.VelocityKnown })
            .OrderBy(t => Math.Sqrt(Math.Pow(t.Dx * 480, 2) + Math.Pow(t.Dy * 720, 2))).Take(3).ToArray();
        var features = new List<double>(FeatureCount) { px, py };
        for (var i = 0; i < 3; i++)
        {
            if (i >= nearest.Length) { features.AddRange([0, 0, 0, 0, 0, 0]); continue; }
            var t = nearest[i];
            features.AddRange([Math.Clamp(t.Dx, -1, 1), Math.Clamp(t.Dy, -1, 1), Math.Clamp(t.Vx / 350, -1, 1), Math.Clamp(t.Vy / 350, -1, 1), t.Known ? 1 : 0, 1]);
        }
        foreach (var (ax, ay) in Directions)
        {
            foreach (var seconds in new[] { .2, .6, 1.0 })
            {
                var clearance = nearest.Length == 0 ? 200 : nearest.Min(t =>
                {
                    var tx = t.Dx * 480 + (t.Known ? t.Vx : 0) * seconds;
                    var ty = t.Dy * 720 + (t.Known ? t.Vy : 0) * seconds;
                    return Math.Sqrt(Math.Pow(ax * featurePlayerSpeed * seconds - tx, 2) + Math.Pow(ay * featurePlayerSpeed * seconds - ty, 2)) - 84;
                });
                features.Add(Math.Clamp(clearance / 200, -1, 1));
            }
            var x = px * 480 + ax * featurePlayerSpeed; var y = py * 720 + ay * featurePlayerSpeed;
            features.Add(Math.Clamp(Math.Min(Math.Min(x, 480 - x), Math.Min(y, 720 - y)) / 120, -1, 1));
        }
        return features.ToArray();
    }

    internal int PredictIndex(double[] features)
    {
        if (features.Length != FeatureCount || features.Any(x => !double.IsFinite(x))) throw new InvalidDataException("Invalid learned Dodge observation.");
        var best = 0; var bestScore = double.NegativeInfinity;
        for (var i = 0; i < _weights.Length; i++)
        {
            var score = _weights[i][FeatureCount];
            for (var j = 0; j < FeatureCount; j++) score += _weights[i][j] * Math.Clamp((features[j] - _means[j]) / _scales[j], -5, 5);
            if (score > bestScore) { bestScore = score; best = i; }
        }
        return best;
    }

    internal string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs)
    {
        // The initial training data contains movement actions only; do not
        // invent movement in an empty scene merely because Hold was absent.
        if (threats.Count == 0) return "Hold";
        var features = Features(player, threats);
        var scores = new double[_actions.Length];
        for (var i = 0; i < scores.Length; i++)
        {
            scores[i] = _weights[i][FeatureCount];
            for (var j = 0; j < FeatureCount; j++) scores[i] += _weights[i][j] * Math.Clamp((features[j] - _means[j]) / _scales[j], -5, 5);
        }
        var px = player.X * context.ViewportWidth; var py = player.Y * context.ViewportHeight;
        var travel = context.PlayerSpeed * durationMs / 1000.0;
        foreach (var index in Enumerable.Range(0, scores.Length).OrderByDescending(i => scores[i]))
        {
            if (_actions[index] == "hold") return "Hold";
            var (dx, dy) = Directions[index];
            if (DodgeDirectionalAvoidance.AllowedAxis(px, px + dx * travel, context.EdgeMargin, context.ViewportWidth - context.EdgeMargin) &&
                DodgeDirectionalAvoidance.AllowedAxis(py, py + dy * travel, context.EdgeMargin, context.ViewportHeight - context.EdgeMargin))
                return string.Join('-', _actions[index][5..].Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        }
        return "Hold";
    }
}
