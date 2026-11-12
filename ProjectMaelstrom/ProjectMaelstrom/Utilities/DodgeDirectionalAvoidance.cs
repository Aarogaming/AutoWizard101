namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Experimental short-horizon steering for the private stepped Dodge test.
/// Forecasts a repeated direction, but sends only one bounded action and then
/// observes again. It is a heuristic, not a guarantee of a collision-free path.
/// </summary>
internal static class DodgeDirectionalAvoidance
{
    private const double Diagonal = 0.7071067811865476;
    private static readonly (string Name, double X, double Y)[] Options =
    [
        ("Hold", 0, 0), ("Up", 0, -1), ("Down", 0, 1), ("Left", -1, 0), ("Right", 1, 0),
        ("Up-Left", -Diagonal, -Diagonal), ("Up-Right", Diagonal, -Diagonal),
        ("Down-Left", -Diagonal, Diagonal), ("Down-Right", Diagonal, Diagonal)
    ];

    public static string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs, bool configurable = false)
    {
        var px = (double)player.X * context.ViewportWidth;
        var py = (double)player.Y * context.ViewportHeight;
        var duration = durationMs / 1000.0;
        // The isolated loop advances one 15 Hz observation interval after each
        // action with keys released. This does not describe continuous play.
        var duty = duration / (duration + 1.0 / 15);
        var horizon = context.HorizonMs / 1000.0;
        var best = "Hold";
        var bestScore = double.NegativeInfinity;
        foreach (var option in Options)
        {
            var nextX = px + option.X * context.PlayerSpeed * duration;
            var nextY = py + option.Y * context.PlayerSpeed * duration;
            if (!AllowedAxis(px, nextX, context.EdgeMargin, context.ViewportWidth - context.EdgeMargin) ||
                !AllowedAxis(py, nextY, context.EdgeMargin, context.ViewportHeight - context.EdgeMargin)) continue;
            var totalRisk = 0.0;
            var x = px; var y = py;
            const int samples = 12;
            for (var step = 1; step <= samples; step++)
            {
                var seconds = horizon * step / samples;
                var travel = context.PlayerSpeed * (Math.Min(seconds, duration) + Math.Max(0, seconds - duration) * duty);
                x = Math.Clamp(px + option.X * travel, 0, context.ViewportWidth);
                y = Math.Clamp(py + option.Y * travel, 0, context.ViewportHeight);
                foreach (var threat in threats)
                {
                    var tx = threat.Position.X * context.ViewportWidth + threat.VelocityPxPerSecond.X * seconds;
                    var ty = threat.Position.Y * context.ViewportHeight + threat.VelocityPxPerSecond.Y * seconds;
                    var uncertainty = threat.VelocityKnown ? 8 : 12 + Math.Min(context.MaxThreatSpeed * seconds * .25, 48);
                    var clearance = Math.Sqrt((x - tx) * (x - tx) + (y - ty) * (y - ty)) - context.PlayerRadius - context.ThreatRadius - uncertainty;
                    var danger = Math.Max(0, 50 - clearance);
                    totalRisk += danger * danger / 50 / (.2 + seconds) / samples;
                }
            }
            var centerDistance = Math.Sqrt(Math.Pow(x - context.ViewportWidth / 2.0, 2) + Math.Pow(y - context.ViewportHeight / 2.0, 2));
            var edge = Math.Min(Math.Min(x, context.ViewportWidth - x), Math.Min(y, context.ViewportHeight - y));
            var score = Score(totalRisk, centerDistance, edge, context, configurable);
            if (score > bestScore) { bestScore = score; best = option.Name; }
        }
        return best;
    }

    internal static bool AllowedAxis(double current, double next, double lower, double upper) =>
        current < lower ? next >= current && next <= upper :
        current > upper ? next <= current && next >= lower : next >= lower && next <= upper;

    internal static double Score(double totalRisk, double centerDistance, double edge, DodgeGameContext context, bool configurable)
    {
        var riskWeight = configurable ? context.RiskWeightPercent / 100.0 : 1;
        var edgeWeight = configurable ? context.EdgeWeightPercent / 100.0 : 1;
        return -totalRisk * riskWeight - centerDistance * .02 - Math.Max(0, 80 - edge) * .3 * edgeWeight;
    }
}
