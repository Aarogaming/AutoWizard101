using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Experimental observational outcome or action-contrast model for private stepped Dodge tests only.</summary>
internal sealed class DodgeOutcomePolicy
{
    internal const string FeatureVersion = "dodge-visual-state-action-context-v2-with-hold";
    private const int Count = 71;
    private const double D = 0.7071067811865476;
    private static readonly (string Name, double X, double Y)[] Options =
    [
        ("Up", 0, -1), ("Down", 0, 1), ("Left", -1, 0), ("Right", 1, 0),
        ("Up-Left", -D, -D), ("Up-Right", D, -D), ("Down-Left", -D, D), ("Down-Right", D, D), ("Hold", 0, 0)
    ];
    private readonly double[] _means, _scales, _weights;
    private readonly int _featurePlayerSpeed;
    private DodgeOutcomePolicy(double[] means, double[] scales, double[] weights, int featurePlayerSpeed) { _means = means; _scales = scales; _weights = weights; _featurePlayerSpeed = featurePlayerSpeed; }
    internal bool MatchesContext(DodgeGameContext context) => _featurePlayerSpeed != 400 || (context.PlayerSpeed == 400 && context.PlayerRadius == 34 && context.ThreatRadius == 50 && context.MaxThreatSpeed == 350);
    internal sealed record Choice(string Advice, string BeforeClearanceAdvice, bool ClearanceOverride, int SafeOptions, double? SelectedClearance, string Fallback, string? ReferenceAdvice = null, bool ReferenceRetained = false);

    internal static DodgeOutcomePolicy Parse(JsonElement model)
    {
        if (model.ValueKind != JsonValueKind.Object || model.GetProperty("schema_version").GetInt32() != 1)
            throw new InvalidDataException("Unsupported experimental outcome model.");
        var algorithm = model.GetProperty("algorithm").GetString();
        var episode = algorithm == "linear-observational-episode-advantage";
        var horizon = model.GetProperty("horizon_ms").GetInt32();
        if (algorithm is not ("binary-logistic-observational-survival" or "linear-observational-action-advantage" or "linear-observational-episode-advantage") ||
            (episode ? horizon != 0 : horizon is not (1000 or 2000 or 5000)))
            throw new InvalidDataException("Unsupported experimental outcome model.");
        var version = model.GetProperty("feature_version").GetString();
        var featurePlayerSpeed = version == FeatureVersion ? 190 : version == FeatureVersion + "-speed400" ? 400 : 0;
        if ((algorithm is "linear-observational-action-advantage" or "linear-observational-episode-advantage") && featurePlayerSpeed != 400)
            throw new InvalidDataException("Action advantage models require calibrated features.");
        if (featurePlayerSpeed == 0 || (model.TryGetProperty("feature_player_speed", out var speed) && speed.GetInt32() != featurePlayerSpeed) || (featurePlayerSpeed == 400 && !model.TryGetProperty("feature_player_speed", out _)))
            throw new InvalidDataException("Outcome model feature speed does not match its version.");
        var means = Numbers(model.GetProperty("means"), Count, false);
        var scales = Numbers(model.GetProperty("scales"), Count, true);
        var weights = Numbers(model.GetProperty("weights"), Count + 1, false);
        if ((algorithm is "linear-observational-action-advantage" or "linear-observational-episode-advantage") &&
            (means.Any(value => value != 0) || scales.Any(value => value != 1) || weights.Take(52).Any(value => value != 0) || weights[Count] != 0))
            throw new InvalidDataException("Invalid action-contrast model representation.");
        return new(means, scales, weights, featurePlayerSpeed);
    }

    private static double[] Numbers(JsonElement value, int count, bool positive)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != count) throw new InvalidDataException("Invalid outcome model dimensions.");
        var values = value.EnumerateArray().Select(x => x.GetDouble()).ToArray();
        if (values.Any(x => !double.IsFinite(x) || Math.Abs(x) > 10000 || (positive && x < .000001))) throw new InvalidDataException("Invalid outcome model value.");
        return values;
    }

    internal static double[] Features(double[] state, int action)
    {
        if (state.Length != 52 || state.Any(x => !double.IsFinite(x)) || action < 0 || action >= Options.Length)
            throw new InvalidDataException("Invalid outcome observation or action.");
        var values = new List<double>(Count);
        values.AddRange(state);
        for (var i = 0; i < Options.Length; i++) values.Add(i == action ? 1 : 0);
        for (var i = 0; i < 4; i++) values.Add(action < 8 ? state[20 + action * 4 + i] : 0);
        var (_, ax, ay) = Options[action];
        foreach (var index in new[] { 2, 8, 14 })
        {
            values.Add(ax * state[index] + ay * state[index + 1]);
            values.Add(ax * state[index + 2] + ay * state[index + 3]);
        }
        return values.ToArray();
    }

    internal double Score(double[] features)
    {
        if (features.Length != Count || features.Any(x => !double.IsFinite(x))) throw new InvalidDataException("Invalid outcome features.");
        var score = _weights[Count];
        for (var i = 0; i < Count; i++) score += _weights[i] * Math.Clamp((features[i] - _means[i]) / _scales[i], -5, 5);
        return score;
    }

    internal string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs) => Decide(player, threats, context, durationMs).Advice;

    internal Choice Decide(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs, bool turnAwareFallback = false, bool retainSafeReference = false, bool earlyEscape = false)
    {
        if (threats.Count == 0) return new("Hold", "Hold", false, 0, null, "none");
        if (!MatchesContext(context)) throw new InvalidDataException("Outcome model calibration does not match the game context.");
        var state = DodgeLearnedPolicy.Features(player, threats, _featurePlayerSpeed);
        var px = player.X * context.ViewportWidth; var py = player.Y * context.ViewportHeight;
        var travel = context.PlayerSpeed * durationMs / 1000.0;
        var candidates = new List<(string Name, double Score, double Clearance)>();
        for (var i = 0; i < Options.Length; i++)
        {
            var option = Options[i];
            if (!DodgeDirectionalAvoidance.AllowedAxis(px, px + option.X * travel, context.EdgeMargin, context.ViewportWidth - context.EdgeMargin) ||
                !DodgeDirectionalAvoidance.AllowedAxis(py, py + option.Y * travel, context.EdgeMargin, context.ViewportHeight - context.EdgeMargin)) continue;
            var nextX = px + option.X * travel; var nextY = py + option.Y * travel;
            var edge = Math.Min(Math.Min(nextX, context.ViewportWidth - nextX), Math.Min(nextY, context.ViewportHeight - nextY));
            var score = Score(Features(state, i)) - Math.Max(0, 90 - edge) * .03;
            candidates.Add((option.Name, score, Clearance(px, py, option.X, option.Y, threats, context, durationMs)));
        }
        if (candidates.Count == 0) return new("Hold", "Hold", false, 0, null, "none");
        // Outcome correlations are weak evidence for unseen choices. Keep the
        // model inside a short-horizon visual clearance guard; if every action
        // is dangerous, prefer the one with the most predicted room.
        var safer = candidates.Where(candidate => candidate.Clearance >= 10).ToArray();
        var unguarded = candidates.MaxBy(candidate => candidate.Score);
        var selected = safer.Length > 0 ? safer.MaxBy(candidate => candidate.Score) : candidates.MaxBy(candidate => candidate.Clearance);
        var fallback = safer.Length > 0 ? "none" : "clearance";
        string? referenceAdvice = null;
        var referenceRetained = false;
        if (retainSafeReference)
        {
            referenceAdvice = DodgeDirectionalAvoidance.Recommend(player, threats, context, durationMs);
            var referenceIndex = Array.FindIndex(safer, candidate => candidate.Name == referenceAdvice);
            if (referenceIndex >= 0)
            {
                selected = safer[referenceIndex];
                referenceRetained = true;
            }
        }
        // When few options remain, consider an earlier two-phase escape. Its
        // first move must still pass the same immediate visual clearance guard.
        if (earlyEscape && safer.Length is > 0 and <= 3)
        {
            var advice = DodgeTwoPhaseAvoidance.Recommend(player, threats, context, durationMs);
            var index = Array.FindIndex(safer, candidate => candidate.Name == advice);
            if (index >= 0 && safer[index].Name != selected.Name)
            {
                selected = safer[index];
                referenceRetained = false;
                fallback = "two-phase-early";
            }
        }
        if (safer.Length == 0 && turnAwareFallback)
        {
            var advice = DodgeTwoPhaseAvoidance.Recommend(player, threats, context, durationMs);
            var index = candidates.FindIndex(candidate => candidate.Name == advice);
            if (index >= 0) { selected = candidates[index]; fallback = "two-phase"; }
        }
        return new(selected.Name, unguarded.Name, selected.Name != unguarded.Name, safer.Length, selected.Clearance, fallback, referenceAdvice, referenceRetained);
    }

    private static double Clearance(double px, double py, double dx, double dy, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs)
    {
        var clearance = double.PositiveInfinity;
        foreach (var t in new[] { .075, .15, .225, .3 })
        {
            var travel = context.PlayerSpeed * Math.Min(t, durationMs / 1000.0);
            var x = px + dx * travel; var y = py + dy * travel;
            foreach (var threat in threats)
            {
                var tx = threat.Position.X * context.ViewportWidth + (threat.VelocityKnown ? threat.VelocityPxPerSecond.X * t : 0);
                var ty = threat.Position.Y * context.ViewportHeight + (threat.VelocityKnown ? threat.VelocityPxPerSecond.Y * t : 0);
                var uncertainty = threat.VelocityKnown ? 8 : 12;
                clearance = Math.Min(clearance, Math.Sqrt(Math.Pow(x - tx, 2) + Math.Pow(y - ty, 2)) - context.PlayerRadius - context.ThreatRadius - uncertainty);
            }
        }
        return clearance;
    }
}
