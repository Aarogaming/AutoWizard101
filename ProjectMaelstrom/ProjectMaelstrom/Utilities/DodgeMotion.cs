namespace ProjectMaelstrom.Utilities;

internal sealed record DodgeThreatMotion(PointF Position, PointF VelocityPxPerSecond, bool VelocityKnown);

/// <summary>Do not assert a velocity when independent visual associations disagree.</summary>
internal static class DodgeMotionConsensus
{
    public static IReadOnlyList<DodgeThreatMotion> Merge(IReadOnlyList<DodgeThreatMotion> first, IReadOnlyList<DodgeThreatMotion> second)
    {
        if (first.Count != second.Count) throw new InvalidDataException("Visual trackers disagree on the observation count.");
        var result = new List<DodgeThreatMotion>(first.Count);
        for (var i = 0; i < first.Count; i++)
        {
            var a = first[i]; var b = second[i];
            if (a.Position != b.Position) throw new InvalidDataException("Visual trackers disagree on observation positions.");
            var dx = a.VelocityPxPerSecond.X - b.VelocityPxPerSecond.X;
            var dy = a.VelocityPxPerSecond.Y - b.VelocityPxPerSecond.Y;
            if (!a.VelocityKnown || !b.VelocityKnown || Math.Sqrt(dx * dx + dy * dy) > 80)
                result.Add(new(a.Position, PointF.Empty, false));
            else
                result.Add(new(a.Position, new PointF((a.VelocityPxPerSecond.X + b.VelocityPxPerSecond.X) / 2, (a.VelocityPxPerSecond.Y + b.VelocityPxPerSecond.Y) / 2), true));
        }
        return result;
    }
}

/// <summary>Short-lived, conservative nearest-neighbor associations from visual frames.</summary>
internal sealed class DodgeMotionTracker
{
    private PointF[]? _previous;
    private DodgeThreatMotion[]? _previousMotions;
    private double _previousMs;
    private readonly bool _predictive;
    private readonly bool _guardVelocity;
    private readonly bool _carryUncertainVelocity;
    private readonly bool _smoothVelocity;
    public bool HasHistory { get; private set; }

    public DodgeMotionTracker(bool predictive = false, bool guardVelocity = false, bool carryUncertainVelocity = false, bool smoothVelocity = false)
    {
        _predictive = predictive;
        _guardVelocity = predictive || guardVelocity || carryUncertainVelocity;
        _carryUncertainVelocity = carryUncertainVelocity;
        _smoothVelocity = predictive || smoothVelocity;
    }

    public IReadOnlyList<DodgeThreatMotion> Update(IReadOnlyList<PointF> current, double capturedMs, DodgeGameContext context)
    {
        var elapsed = capturedMs - _previousMs;
        var valid = _previous != null && double.IsFinite(elapsed) && elapsed >= 40 && elapsed <= context.MaxTrackAgeMs;
        var result = new List<DodgeThreatMotion>();
        var used = new HashSet<int>();
        foreach (var point in current)
        {
            var motion = new DodgeThreatMotion(point, PointF.Empty, false);
            if (valid)
            {
                var candidates = _previous!.Select((p, index) => new { Point = p, Index = index, Distance = Distance(p, point, context), PredictionError = PredictionError(p, point, index, elapsed, context) })
                    .Where(c => !used.Contains(c.Index) && c.Distance <= context.MaxThreatSpeed * elapsed / 1000 + 8)
                    .OrderBy(c => _predictive ? c.PredictionError : c.Distance).ToArray();
                // Close alternatives may represent crossing enemies; avoid asserting a velocity.
                if (candidates.Length > 0 && (candidates.Length == 1 || (_predictive ? candidates[1].PredictionError - candidates[0].PredictionError : candidates[1].Distance - candidates[0].Distance) >= 12))
                {
                    var match = candidates[0];
                    var vx = (point.X - match.Point.X) * context.ViewportWidth * 1000 / elapsed;
                    var vy = (point.Y - match.Point.Y) * context.ViewportHeight * 1000 / elapsed;
                    if (Math.Sqrt(vx * vx + vy * vy) <= context.MaxThreatSpeed)
                    {
                        used.Add(match.Index);
                        var previous = (_guardVelocity || _smoothVelocity) && _previousMotions != null ? _previousMotions[match.Index] : null;
                        var change = previous == null || !previous.VelocityKnown ? 0 : Math.Sqrt(Math.Pow(vx - previous.VelocityPxPerSecond.X, 2) + Math.Pow(vy - previous.VelocityPxPerSecond.Y, 2));
                        if (_guardVelocity && previous != null && previous.VelocityKnown && change > 120)
                            motion = new(point, _carryUncertainVelocity ? previous.VelocityPxPerSecond : PointF.Empty, false);
                        else
                        {
                            if (_smoothVelocity && previous != null && previous.VelocityKnown)
                            {
                                vx = previous.VelocityPxPerSecond.X * .65 + vx * .35;
                                vy = previous.VelocityPxPerSecond.Y * .65 + vy * .35;
                            }
                            motion = new(point, new PointF((float)vx, (float)vy), true);
                        }
                    }
                }
            }
            result.Add(motion);
        }
        HasHistory = valid;
        _previous = current.ToArray();
        _previousMotions = result.ToArray();
        _previousMs = capturedMs;
        return result;
    }

    private double PredictionError(PointF previous, PointF current, int index, double elapsedMs, DodgeGameContext context)
    {
        if (!_predictive || _previousMotions == null || !_previousMotions[index].VelocityKnown) return Distance(previous, current, context);
        var velocity = _previousMotions[index].VelocityPxPerSecond;
        var predicted = new PointF(previous.X + (float)(velocity.X * elapsedMs / 1000 / context.ViewportWidth), previous.Y + (float)(velocity.Y * elapsedMs / 1000 / context.ViewportHeight));
        return Distance(predicted, current, context);
    }

    private static double Distance(PointF a, PointF b, DodgeGameContext context) =>
        Math.Sqrt(Math.Pow((a.X - b.X) * context.ViewportWidth, 2) + Math.Pow((a.Y - b.Y) * context.ViewportHeight, 2));
}

internal static class DodgeMotionAvoidance
{
    public static string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs, bool collisionAware = false, bool edgeAware = false, bool diagonal = false, double edgePenalty = 0)
    {
        var px = player.X * context.ViewportWidth; var py = player.Y * context.ViewportHeight;
        if (threats.Count == 0)
        {
            if (!edgeAware) return "Hold";
            if (px < 110) return "Right";
            if (px > context.ViewportWidth - 110) return "Left";
            if (py < 110) return "Down";
            if (py > context.ViewportHeight - 110) return "Up";
            return "Hold";
        }
        const double diagonalUnit = 0.7071067811865476;
        var options = new List<(string Name, double X, double Y)> { ("Up", 0, -1), ("Down", 0, 1), ("Left", -1, 0), ("Right", 1, 0), ("Hold", 0, 0) };
        if (diagonal) options.AddRange(new[] { ("Up-Left", -diagonalUnit, -diagonalUnit), ("Up-Right", diagonalUnit, -diagonalUnit), ("Down-Left", -diagonalUnit, diagonalUnit), ("Down-Right", diagonalUnit, diagonalUnit) });
        var best = "Hold"; var bestScore = double.NegativeInfinity;
        foreach (var option in options)
        {
            var travel = context.PlayerSpeed * durationMs / 1000.0;
            var endX = px + option.X * travel; var endY = py + option.Y * travel;
            if (endX < context.EdgeMargin || endX > context.ViewportWidth - context.EdgeMargin || endY < context.EdgeMargin || endY > context.ViewportHeight - context.EdgeMargin) continue;
            var minimum = double.PositiveInfinity; var total = 0.0; var danger = 0.0;
            const int samples = 14;
            for (var step = 1; step <= samples; step++)
            {
                var seconds = context.HorizonMs / 1000.0 * step / samples;
                var movement = Math.Min(seconds, durationMs / 1000.0) * context.PlayerSpeed;
                var x = px + option.X * movement; var y = py + option.Y * movement;
                var clearance = threats.Min(t =>
                {
                    var tx = t.Position.X * context.ViewportWidth + t.VelocityPxPerSecond.X * seconds;
                    var ty = t.Position.Y * context.ViewportHeight + t.VelocityPxPerSecond.Y * seconds;
                    // An unmatched or crossing threat carries extra uncertainty; this is
                    // a heuristic penalty, not a guaranteed collision-free envelope.
                    var uncertainty = t.VelocityKnown ? 8 : 12 + Math.Min(context.MaxThreatSpeed * seconds * .25, 48);
                    return Math.Sqrt((x - tx) * (x - tx) + (y - ty) * (y - ty)) - context.PlayerRadius - context.ThreatRadius - uncertainty;
                });
                minimum = Math.Min(minimum, clearance); total += clearance;
                if (collisionAware) danger += ProximityPenalty(clearance) * (samples - step + 1) / samples;
            }
            var edge = Math.Min(Math.Min(endX, context.ViewportWidth - endX), Math.Min(endY, context.ViewportHeight - endY));
            var score = minimum + total / samples * .2 + Math.Min(edge, 100) * .08 - danger / samples;
            if (edgeAware) score -= Math.Max(0, 120 - edge) * 1.2;
            if (edgePenalty > 0) score -= Math.Max(0, 140 - edge) * edgePenalty;
            if (score > bestScore) { bestScore = score; best = option.Name; }
        }
        return best;
    }

    // Prioritize imminent narrow passes, not only the relative ranking of clearances.
    internal static double ProximityPenalty(double clearance) => Math.Max(0, 55 - clearance) * 1.5;
}

/// <summary>Receding-horizon candidate: score short two-step paths, then execute only the first step.</summary>
internal static class DodgeResilientAvoidance
{
    private static readonly (string Name, int X, int Y)[] Options =
        { ("Up", 0, -1), ("Down", 0, 1), ("Left", -1, 0), ("Right", 1, 0), ("Hold", 0, 0) };

    public static string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs, string? previousAdvice)
    {
        if (threats.Count == 0) return "Hold";
        var px = player.X * context.ViewportWidth; var py = player.Y * context.ViewportHeight;
        var duration = durationMs / 1000.0;
        var nextStart = duration + .10; // Matches the minimum pause in the isolated loop.
        var horizon = context.HorizonMs / 1000.0;
        var best = "Hold"; var bestScore = double.NegativeInfinity;
        foreach (var first in Options)
        {
            var firstX = px + first.X * context.PlayerSpeed * duration;
            var firstY = py + first.Y * context.PlayerSpeed * duration;
            if (!Inside(firstX, firstY, context)) continue;
            foreach (var second in Options)
            {
                var endX = firstX + second.X * context.PlayerSpeed * duration;
                var endY = firstY + second.Y * context.PlayerSpeed * duration;
                if (!Inside(endX, endY, context)) continue;
                var minimum = double.PositiveInfinity; var total = 0.0; var aggregateRisk = 0.0;
                const int samples = 20;
                for (var step = 1; step <= samples; step++)
                {
                    var t = horizon * step / samples;
                    var firstTravel = Math.Min(t, duration) * context.PlayerSpeed;
                    var secondTravel = Math.Clamp(t - nextStart, 0, duration) * context.PlayerSpeed;
                    var x = px + first.X * firstTravel + second.X * secondTravel;
                    var y = py + first.Y * firstTravel + second.Y * secondTravel;
                    var nearest = double.PositiveInfinity;
                    foreach (var threat in threats)
                    {
                        var tx = threat.Position.X * context.ViewportWidth + threat.VelocityPxPerSecond.X * t;
                        var ty = threat.Position.Y * context.ViewportHeight + threat.VelocityPxPerSecond.Y * t;
                        var uncertainty = threat.VelocityKnown ? 10 : 15 + Math.Min(context.MaxThreatSpeed * t * .35, 85);
                        var clearance = Math.Sqrt((x - tx) * (x - tx) + (y - ty) * (y - ty)) - context.PlayerRadius - context.ThreatRadius - uncertainty;
                        nearest = Math.Min(nearest, clearance);
                        aggregateRisk += 1 / (Math.Max(clearance, 0) + 25);
                    }
                    minimum = Math.Min(minimum, nearest);
                    total += nearest;
                }
                var edge = Math.Min(Math.Min(endX, context.ViewportWidth - endX), Math.Min(endY, context.ViewportHeight - endY));
                var reversal = IsOpposite(first.Name, previousAdvice) ? 5 : 0;
                var score = minimum + total / samples * .15 - aggregateRisk / samples * 180 + Math.Min(edge, 110) * .22 - reversal;
                if (score > bestScore) { bestScore = score; best = first.Name; }
            }
        }
        return best;
    }

    private static bool Inside(double x, double y, DodgeGameContext context) =>
        x >= context.EdgeMargin && x <= context.ViewportWidth - context.EdgeMargin && y >= context.EdgeMargin && y <= context.ViewportHeight - context.EdgeMargin;

    private static bool IsOpposite(string current, string? previous) => (current, previous) is
        ("Left", "Right") or ("Right", "Left") or ("Up", "Down") or ("Down", "Up");
}

/// <summary>Only changes the baseline when the proposed first move has a clear short-horizon advantage.</summary>
internal static class DodgeGuardedAvoidance
{
    public static string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs, string? previousAdvice, string? baselineAdvice = null, double minimumGain = 12)
    {
        var baseline = baselineAdvice ?? DodgeMotionAvoidance.Recommend(player, threats, context, durationMs);
        var candidate = DodgeResilientAvoidance.Recommend(player, threats, context, durationMs, previousAdvice);
        if (candidate == baseline || threats.Count == 0) return baseline;
        var baselineClearance = MinimumClearance(player, threats, context, durationMs, baseline);
        var candidateClearance = MinimumClearance(player, threats, context, durationMs, candidate);
        return candidateClearance > baselineClearance + minimumGain ? candidate : baseline;
    }

    internal static double MinimumClearance(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs, string advice, double? horizonSeconds = null)
    {
        const double diagonal = 0.7071067811865476;
        var (dx, dy) = advice switch { "Left" => (-1.0, 0.0), "Right" => (1.0, 0.0), "Up" => (0.0, -1.0), "Down" => (0.0, 1.0), "Up-Left" => (-diagonal, -diagonal), "Up-Right" => (diagonal, -diagonal), "Down-Left" => (-diagonal, diagonal), "Down-Right" => (diagonal, diagonal), _ => (0.0, 0.0) };
        var px = player.X * context.ViewportWidth; var py = player.Y * context.ViewportHeight;
        var minimum = double.PositiveInfinity;
        for (var step = 1; step <= 14; step++)
        {
            var seconds = (horizonSeconds ?? context.HorizonMs / 1000.0) * step / 14;
            var travel = Math.Min(seconds, durationMs / 1000.0) * context.PlayerSpeed;
            var x = px + dx * travel; var y = py + dy * travel;
            foreach (var threat in threats)
            {
                var tx = threat.Position.X * context.ViewportWidth + threat.VelocityPxPerSecond.X * seconds;
                var ty = threat.Position.Y * context.ViewportHeight + threat.VelocityPxPerSecond.Y * seconds;
                var uncertainty = threat.VelocityKnown ? 8 : 12 + Math.Min(context.MaxThreatSpeed * seconds * .25, 48);
                minimum = Math.Min(minimum, Math.Sqrt((x - tx) * (x - tx) + (y - ty) * (y - ty)) - context.PlayerRadius - context.ThreatRadius - uncertainty);
            }
        }
        return minimum;
    }
}

/// <summary>Move toward open central space only when short-term clearance stays high.</summary>
internal static class DodgeCenterGuardAvoidance
{
    public static string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs)
    {
        var candidate = DodgeMotionAvoidance.Recommend(player, threats, context, durationMs, collisionAware: true, edgeAware: true, diagonal: true);
        if (threats.Count == 0) return candidate;
        var baseline = DodgeMotionAvoidance.Recommend(player, threats, context, durationMs, collisionAware: true, diagonal: true);
        if (candidate == baseline) return baseline;
        var candidateClearance = DodgeGuardedAvoidance.MinimumClearance(player, threats, context, durationMs, candidate, .35);
        var baselineClearance = DodgeGuardedAvoidance.MinimumClearance(player, threats, context, durationMs, baseline, .35);
        return candidateClearance >= 50 && candidateClearance >= baselineClearance - 5 ? candidate : baseline;
    }
}

/// <summary>Prefer room to maneuver only when that does not create a near-term hazard.</summary>
internal static class DodgeSafeEdgeAvoidance
{
    public static string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs)
    {
        var candidate = DodgeMotionAvoidance.Recommend(player, threats, context, durationMs, collisionAware: true, edgeAware: true);
        if (threats.Count == 0) return candidate;
        var baseline = DodgeMotionAvoidance.Recommend(player, threats, context, durationMs, collisionAware: true);
        if (candidate == baseline) return baseline;
        var safe = DodgeGuardedAvoidance.MinimumClearance(player, threats, context, durationMs, candidate);
        var original = DodgeGuardedAvoidance.MinimumClearance(player, threats, context, durationMs, baseline);
        return safe > 10 && safe >= original - 10 ? candidate : baseline;
    }
}

/// <summary>Evaluate two short movement choices, then execute only the first and observe again.</summary>
internal static class DodgeLookaheadAvoidance
{
    private const double Diagonal = 0.7071067811865476;
    private static readonly (string Name, double X, double Y)[] Options =
    [
        ("Up", 0, -1), ("Down", 0, 1), ("Left", -1, 0), ("Right", 1, 0), ("Hold", 0, 0),
        ("Up-Left", -Diagonal, -Diagonal), ("Up-Right", Diagonal, -Diagonal),
        ("Down-Left", -Diagonal, Diagonal), ("Down-Right", Diagonal, Diagonal)
    ];

    public static string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs)
    {
        if (threats.Count == 0) return "Hold";
        var px = player.X * context.ViewportWidth; var py = player.Y * context.ViewportHeight;
        var duration = durationMs / 1000.0;
        var secondStart = duration + 1.0 / 15;
        var horizon = context.HorizonMs / 1000.0;
        var best = "Hold"; var bestScore = double.NegativeInfinity;
        foreach (var first in Options)
        {
            var x1 = px + first.X * context.PlayerSpeed * duration; var y1 = py + first.Y * context.PlayerSpeed * duration;
            if (!Inside(x1, y1, context)) continue;
            foreach (var second in Options)
            {
                var x2 = x1 + second.X * context.PlayerSpeed * duration; var y2 = y1 + second.Y * context.PlayerSpeed * duration;
                if (!Inside(x2, y2, context)) continue;
                var minimum = double.PositiveInfinity; var total = 0.0; var imminent = 0.0;
                const int samples = 16;
                for (var step = 1; step <= samples; step++)
                {
                    var time = horizon * step / samples;
                    var a = Math.Min(time, duration) * context.PlayerSpeed;
                    var b = Math.Clamp(time - secondStart, 0, duration) * context.PlayerSpeed;
                    var x = px + first.X * a + second.X * b; var y = py + first.Y * a + second.Y * b;
                    var clearance = threats.Min(t =>
                    {
                        var tx = t.Position.X * context.ViewportWidth + t.VelocityPxPerSecond.X * time;
                        var ty = t.Position.Y * context.ViewportHeight + t.VelocityPxPerSecond.Y * time;
                        var uncertainty = t.VelocityKnown ? 8 : 12 + Math.Min(context.MaxThreatSpeed * time * .25, 48);
                        return Math.Sqrt((x - tx) * (x - tx) + (y - ty) * (y - ty)) - context.PlayerRadius - context.ThreatRadius - uncertainty;
                    });
                    minimum = Math.Min(minimum, clearance); total += clearance;
                    imminent += DodgeMotionAvoidance.ProximityPenalty(clearance) * (samples - step + 1) / samples;
                }
                var edge = Math.Min(Math.Min(x2, context.ViewportWidth - x2), Math.Min(y2, context.ViewportHeight - y2));
                var score = minimum + total / samples * .2 - imminent / samples - Math.Max(0, 120 - edge) * .25;
                if (score > bestScore) { bestScore = score; best = first.Name; }
            }
        }
        return best;
    }

    private static bool Inside(double x, double y, DodgeGameContext context) =>
        x >= context.EdgeMargin && x <= context.ViewportWidth - context.EdgeMargin && y >= context.EdgeMargin && y <= context.ViewportHeight - context.EdgeMargin;
}

/// <summary>Prefer the center when a short visual prediction leaves sufficient room.</summary>
internal static class DodgeCenterFirstAvoidance
{
    private const double Diagonal = 0.7071067811865476;
    private static readonly (string Name, double X, double Y)[] Options =
    [
        ("Hold", 0, 0), ("Up", 0, -1), ("Down", 0, 1), ("Left", -1, 0), ("Right", 1, 0),
        ("Up-Left", -Diagonal, -Diagonal), ("Up-Right", Diagonal, -Diagonal),
        ("Down-Left", -Diagonal, Diagonal), ("Down-Right", Diagonal, Diagonal)
    ];

    public static string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs, double horizonSeconds = .35, double safeClearance = 40, bool edgeAwareFallback = false)
        => RecommendSafe(player, threats, context, durationMs, horizonSeconds, safeClearance)
            ?? DodgeMotionAvoidance.Recommend(player, threats, context, durationMs, collisionAware: true, edgeAware: edgeAwareFallback, diagonal: true);

    internal static string? RecommendSafe(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs, double horizonSeconds, double safeClearance = 40)
    {
        var px = player.X * context.ViewportWidth; var py = player.Y * context.ViewportHeight;
        var distance = context.PlayerSpeed * durationMs / 1000.0;
        var best = "Hold"; var bestScore = double.NegativeInfinity;
        foreach (var option in Options)
        {
            var x = px + option.X * distance; var y = py + option.Y * distance;
            if (x < context.EdgeMargin || x > context.ViewportWidth - context.EdgeMargin || y < context.EdgeMargin || y > context.ViewportHeight - context.EdgeMargin) continue;
            var clearance = threats.Count == 0 ? 150 : DodgeGuardedAvoidance.MinimumClearance(player, threats, context, durationMs, option.Name, horizonSeconds);
            if (clearance < safeClearance) continue;
            var centerDistance = Math.Sqrt(Math.Pow(x - context.ViewportWidth / 2.0, 2) + Math.Pow(y - context.ViewportHeight / 2.0, 2));
            var score = -centerDistance * .5 + Math.Min(clearance, 120) * .12;
            if (score > bestScore) { bestScore = score; best = option.Name; }
        }
        return double.IsNegativeInfinity(bestScore) ? null : best;
    }
}

/// <summary>Use the longer forecast only when it offers a safe first step.</summary>
internal static class DodgeAdaptiveHorizonAvoidance
{
    public static string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs)
    {
        var longHorizon = context.HorizonMs / 1000.0;
        return DodgeCenterFirstAvoidance.RecommendSafe(player, threats, context, durationMs, longHorizon)
            ?? DodgeCenterFirstAvoidance.RecommendSafe(player, threats, context, durationMs, Math.Min(.35, longHorizon))
            ?? DodgeMotionAvoidance.Recommend(player, threats, context, durationMs, collisionAware: true, diagonal: true);
    }
}

/// <summary>Prefer more room at the edge only when near-term clearance is comparable.</summary>
internal static class DodgeGuardedEdgeFallback
{
    public static string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs)
    {
        var safe = DodgeCenterFirstAvoidance.RecommendSafe(player, threats, context, durationMs, .35);
        if (safe != null) return safe;
        var baseline = DodgeMotionAvoidance.Recommend(player, threats, context, durationMs, collisionAware: true, diagonal: true);
        var edge = DodgeMotionAvoidance.Recommend(player, threats, context, durationMs, collisionAware: true, edgeAware: true, diagonal: true);
        if (edge == baseline) return baseline;
        return HasComparableClearance(player, threats, context, durationMs, baseline, edge) ? edge : baseline;
    }

    internal static bool HasComparableClearance(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs, string baseline, string edge)
    {
        var originalClearance = DodgeGuardedAvoidance.MinimumClearance(player, threats, context, durationMs, baseline, .2);
        var edgeClearance = DodgeGuardedAvoidance.MinimumClearance(player, threats, context, durationMs, edge, .2);
        return edgeClearance >= originalClearance - 5;
    }
}

/// <summary>Replan short actions using a bounded three-segment visual-motion forecast.</summary>
internal static class DodgeRolloutAvoidance
{
    private const double Diagonal = 0.7071067811865476;
    private static readonly (string Name, double X, double Y)[] Options =
    [
        ("Hold", 0, 0), ("Up", 0, -1), ("Down", 0, 1), ("Left", -1, 0), ("Right", 1, 0),
        ("Up-Left", -Diagonal, -Diagonal), ("Up-Right", Diagonal, -Diagonal),
        ("Down-Left", -Diagonal, Diagonal), ("Down-Right", Diagonal, Diagonal)
    ];

    public static string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs)
    {
        if (threats.Count == 0) return DodgeCenterFirstAvoidance.Recommend(player, threats, context, durationMs);
        var px = (double)player.X * context.ViewportWidth; var py = (double)player.Y * context.ViewportHeight;
        var firstDistance = context.PlayerSpeed * durationMs / 1000.0;
        var duty = durationMs / (durationMs + 1000.0 / 15);
        var forecastSpeed = context.PlayerSpeed * duty;
        var best = "Hold"; var bestScore = double.NegativeInfinity;
        foreach (var first in Options)
        {
            var firstX = px + first.X * firstDistance; var firstY = py + first.Y * firstDistance;
            if (!Inside(firstX, firstY, context)) continue;
            foreach (var second in Options)
                foreach (var third in Options)
                {
                    var x = px; var y = py; var minimum = 250.0; var total = 0.0; var danger = 0.0; var wall = 0.0;
                    const int samples = 9; const double stepSeconds = .1;
                    for (var step = 1; step <= samples; step++)
                    {
                        var direction = step <= 3 ? first : step <= 6 ? second : third;
                        x = Math.Clamp(x + direction.X * forecastSpeed * stepSeconds, context.EdgeMargin, context.ViewportWidth - context.EdgeMargin);
                        y = Math.Clamp(y + direction.Y * forecastSpeed * stepSeconds, context.EdgeMargin, context.ViewportHeight - context.EdgeMargin);
                        var seconds = step * stepSeconds;
                        var nearest = 250.0;
                        foreach (var threat in threats)
                        {
                            var tx = threat.Position.X * context.ViewportWidth + threat.VelocityPxPerSecond.X * seconds;
                            var ty = threat.Position.Y * context.ViewportHeight + threat.VelocityPxPerSecond.Y * seconds;
                            var uncertainty = threat.VelocityKnown ? 8 : 12 + Math.Min(context.MaxThreatSpeed * seconds * .25, 48);
                            nearest = Math.Min(nearest, Math.Sqrt((x - tx) * (x - tx) + (y - ty) * (y - ty)) - context.PlayerRadius - context.ThreatRadius - uncertainty);
                        }
                        minimum = Math.Min(minimum, nearest); total += nearest;
                        danger += Math.Max(0, 35 - nearest) * (samples - step + 1) / samples;
                        var edge = Math.Min(Math.Min(x, context.ViewportWidth - x), Math.Min(y, context.ViewportHeight - y));
                        wall += Math.Max(0, 100 - edge);
                    }
                    var centerDistance = Math.Sqrt(Math.Pow(x - context.ViewportWidth / 2.0, 2) + Math.Pow(y - context.ViewportHeight / 2.0, 2));
                    var score = minimum + total / samples * .15 - danger / samples * 1.3 - wall / samples * .25 - centerDistance * .03;
                    if (score > bestScore) { bestScore = score; best = first.Name; }
                }
        }
        return best;
    }

    private static bool Inside(double x, double y, DodgeGameContext context) =>
        x >= context.EdgeMargin && x <= context.ViewportWidth - context.EdgeMargin && y >= context.EdgeMargin && y <= context.ViewportHeight - context.EdgeMargin;
}

/// <summary>Keep a safe route toward the center, including uncertainty about newly seen threats.</summary>
internal static class DodgeAdaptiveCenterAvoidance
{
    private const double Diagonal = 0.7071067811865476;
    private static readonly (string Name, double X, double Y)[] Options =
    [
        ("Hold", 0, 0), ("Up", 0, -1), ("Down", 0, 1), ("Left", -1, 0), ("Right", 1, 0),
        ("Up-Left", -Diagonal, -Diagonal), ("Up-Right", Diagonal, -Diagonal),
        ("Down-Left", -Diagonal, Diagonal), ("Down-Right", Diagonal, Diagonal)
    ];

    internal static double RequiredClearance(double bestClearance) => Math.Min(40, Math.Max(0, bestClearance - 10));

    public static string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs)
    {
        var px = player.X * context.ViewportWidth; var py = player.Y * context.ViewportHeight;
        var distance = context.PlayerSpeed * durationMs / 1000.0;
        var candidates = new List<(string Name, double CenterDistance, double Clearance)>();
        foreach (var option in Options)
        {
            var x = px + option.X * distance; var y = py + option.Y * distance;
            if (x < context.EdgeMargin || x > context.ViewportWidth - context.EdgeMargin || y < context.EdgeMargin || y > context.ViewportHeight - context.EdgeMargin) continue;
            var center = Math.Sqrt(Math.Pow(x - context.ViewportWidth / 2.0, 2) + Math.Pow(y - context.ViewportHeight / 2.0, 2));
            var clearance = threats.Count == 0 ? 150 : MinimumClearance(px, py, option.X, option.Y, threats, context, durationMs);
            candidates.Add((option.Name, center, clearance));
        }
        if (candidates.Count == 0) return "Hold";
        var bestClearance = candidates.Max(c => c.Clearance);
        if (bestClearance <= 0) return DodgeMotionAvoidance.Recommend(player, threats, context, durationMs, collisionAware: true, diagonal: true);
        var threshold = RequiredClearance(bestClearance);
        return candidates.Where(c => c.Clearance >= threshold)
            .OrderByDescending(c => -c.CenterDistance * .5 + Math.Min(c.Clearance, 120) * .12)
            .First().Name;
    }

    private static double MinimumClearance(double px, double py, double dx, double dy, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs)
    {
        var minimum = double.PositiveInfinity;
        for (var step = 1; step <= 14; step++)
        {
            var seconds = .35 * step / 14;
            var travel = Math.Min(seconds, durationMs / 1000.0) * context.PlayerSpeed;
            var x = px + dx * travel; var y = py + dy * travel;
            foreach (var threat in threats)
            {
                var tx = threat.Position.X * context.ViewportWidth + threat.VelocityPxPerSecond.X * seconds;
                var ty = threat.Position.Y * context.ViewportHeight + threat.VelocityPxPerSecond.Y * seconds;
                var uncertainty = threat.VelocityKnown ? 8 : 12 + Math.Min(context.MaxThreatSpeed * seconds * .6, 80);
                minimum = Math.Min(minimum, Math.Sqrt((x - tx) * (x - tx) + (y - ty) * (y - ty)) - context.PlayerRadius - context.ThreatRadius - uncertainty);
            }
        }
        return minimum;
    }
}

/// <summary>Choose a short move by forecasting its continuing path and boundary clearance.</summary>
internal static class DodgeTrajectoryAvoidance
{
    private const double Diagonal = 0.7071067811865476;
    private static readonly (string Name, double X, double Y)[] Options =
    [
        ("Hold", 0, 0), ("Up", 0, -1), ("Down", 0, 1), ("Left", -1, 0), ("Right", 1, 0),
        ("Up-Left", -Diagonal, -Diagonal), ("Up-Right", Diagonal, -Diagonal),
        ("Down-Left", -Diagonal, Diagonal), ("Down-Right", Diagonal, Diagonal)
    ];

    public static string Recommend(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs)
    {
        var px = player.X * context.ViewportWidth; var py = player.Y * context.ViewportHeight;
        var firstTravel = context.PlayerSpeed * durationMs / 1000.0;
        var best = "Hold"; var bestScore = double.NegativeInfinity;
        foreach (var option in Options)
        {
            var firstX = px + option.X * firstTravel; var firstY = py + option.Y * firstTravel;
            if (firstX < context.EdgeMargin || firstX > context.ViewportWidth - context.EdgeMargin || firstY < context.EdgeMargin || firstY > context.ViewportHeight - context.EdgeMargin) continue;
            var minimum = 250.0; var total = 0.0; var risk = 0.0; var wall = 0.0;
            const int samples = 12;
            for (var step = 1; step <= samples; step++)
            {
                var seconds = .8 * step / samples;
                var duration = durationMs / 1000.0;
                var duty = duration / (duration + 1.0 / 15);
                var travel = context.PlayerSpeed * (Math.Min(seconds, duration) + Math.Max(0, seconds - duration) * duty);
                var x = Math.Clamp(px + option.X * travel, context.EdgeMargin, context.ViewportWidth - context.EdgeMargin);
                var y = Math.Clamp(py + option.Y * travel, context.EdgeMargin, context.ViewportHeight - context.EdgeMargin);
                var edge = Math.Min(Math.Min(x, context.ViewportWidth - x), Math.Min(y, context.ViewportHeight - y));
                wall += Math.Max(0, 100 - edge);
                var clearance = 250.0;
                foreach (var threat in threats)
                {
                    var tx = threat.Position.X * context.ViewportWidth + threat.VelocityPxPerSecond.X * seconds;
                    var ty = threat.Position.Y * context.ViewportHeight + threat.VelocityPxPerSecond.Y * seconds;
                    var uncertainty = threat.VelocityKnown ? 8 : 12 + Math.Min(context.MaxThreatSpeed * seconds * .35, 60);
                    clearance = Math.Min(clearance, Math.Sqrt((x - tx) * (x - tx) + (y - ty) * (y - ty)) - context.PlayerRadius - context.ThreatRadius - uncertainty);
                }
                minimum = Math.Min(minimum, clearance); total += clearance;
                risk += Math.Max(0, 35 - clearance) * (samples - step + 1) / samples;
            }
            var centerDistance = Math.Sqrt(Math.Pow(firstX - context.ViewportWidth / 2.0, 2) + Math.Pow(firstY - context.ViewportHeight / 2.0, 2));
            var score = minimum + total / samples * .2 - risk / samples - wall / samples * .4 - centerDistance * .04;
            if (score > bestScore) { bestScore = score; best = option.Name; }
        }
        return best;
    }
}
