namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Experimental two-direction forecast for the private stepped Dodge loop.
/// Executes only the first bounded move and observes again; future turns are
/// possibilities, not promised actions or a guarantee of safety.
/// </summary>
internal static class DodgeTwoPhaseAvoidance
{
    private const double Diagonal = 0.7071067811865476;
    private static readonly (string Name, double X, double Y)[] Options =
    [
        ("Hold", 0, 0), ("Up", 0, -1), ("Down", 0, 1), ("Left", -1, 0), ("Right", 1, 0),
        ("Up-Left", -Diagonal, -Diagonal), ("Up-Right", Diagonal, -Diagonal),
        ("Down-Left", -Diagonal, Diagonal), ("Down-Right", Diagonal, Diagonal)
    ];

    public static string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs, bool pulseTiming = false)
    {
        var px = (double)player.X * context.ViewportWidth;
        var py = (double)player.Y * context.ViewportHeight;
        var duration = durationMs / 1000.0;
        var duty = duration / (duration + 1.0 / 15);
        var horizon = context.HorizonMs / 1000.0;
        var cycle = duration + 1.0 / 15;
        // The timing-aware candidate considers turns only at the next decision
        // boundary, after whole action-and-release cycles.
        var turnAt = pulseTiming ? cycle * Math.Max(1, Math.Floor(Math.Min(.3, horizon / 2) / cycle)) : Math.Min(.3, horizon / 2);
        var best = "Hold";
        var bestScore = double.NegativeInfinity;
        foreach (var first in Options)
        {
            var nextX = px + first.X * context.PlayerSpeed * duration;
            var nextY = py + first.Y * context.PlayerSpeed * duration;
            if (!DodgeDirectionalAvoidance.AllowedAxis(px, nextX, context.EdgeMargin, context.ViewportWidth - context.EdgeMargin) ||
                !DodgeDirectionalAvoidance.AllowedAxis(py, nextY, context.EdgeMargin, context.ViewportHeight - context.EdgeMargin)) continue;
            foreach (var second in Options)
            {
                var totalRisk = 0.0;
                var x = px; var y = py;
                const int samples = 12;
                for (var step = 1; step <= samples; step++)
                {
                    var seconds = horizon * step / samples;
                    var firstSeconds = Math.Min(seconds, turnAt);
                    var firstTravel = context.PlayerSpeed * (pulseTiming ? PulseSeconds(firstSeconds, duration, cycle) : Math.Min(firstSeconds, duration) + Math.Max(0, firstSeconds - duration) * duty);
                    var secondSeconds = Math.Max(0, seconds - turnAt);
                    var secondTravel = context.PlayerSpeed * (pulseTiming ? PulseSeconds(secondSeconds, duration, cycle) : secondSeconds * duty);
                    x = Math.Clamp(px + first.X * firstTravel + second.X * secondTravel, 0, context.ViewportWidth);
                    y = Math.Clamp(py + first.Y * firstTravel + second.Y * secondTravel, 0, context.ViewportHeight);
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
                var score = DodgeDirectionalAvoidance.Score(totalRisk, centerDistance, edge, context, configurable: false);
                if (score > bestScore) { bestScore = score; best = first.Name; }
            }
        }
        return best;
    }

    internal static double PulseSeconds(double seconds, double duration, double cycle)
    {
        if (seconds <= 0 || duration <= 0 || cycle < duration) return 0;
        var completedCycles = Math.Floor(seconds / cycle);
        return completedCycles * duration + Math.Min(duration, seconds - completedCycles * cycle);
    }
}
