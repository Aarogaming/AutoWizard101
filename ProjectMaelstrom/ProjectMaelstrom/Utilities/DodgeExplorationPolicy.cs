namespace ProjectMaelstrom.Utilities;

/// <summary>Bounded random exploration in the owned private Dodge environment.</summary>
internal static class DodgeExplorationPolicy
{
    private const double D = 0.7071067811865476;
    internal const double ExplorationRate = 0.2;
    private static readonly (string Name, double X, double Y)[] Options =
    [
        ("Hold", 0, 0), ("Up", 0, -1), ("Down", 0, 1), ("Left", -1, 0), ("Right", 1, 0),
        ("Up-Left", -D, -D), ("Up-Right", D, -D), ("Down-Left", -D, D), ("Down-Right", D, D)
    ];

    internal sealed record Choice(string Advice, bool Explored, double Probability, int EligibleCount);

    internal static Choice Select(PointF player, IReadOnlyList<DodgeThreatMotion> threats, DodgeGameContext context, int durationMs, Random random)
    {
        var baseline = DodgeDirectionalAvoidance.Recommend(player, threats, context, durationMs);
        if (threats.Count == 0) return new(baseline, false, 1, 1);
        var px = player.X * context.ViewportWidth; var py = player.Y * context.ViewportHeight;
        var travel = context.PlayerSpeed * durationMs / 1000.0;
        var eligible = Options.Where(option =>
            DodgeDirectionalAvoidance.AllowedAxis(px, px + option.X * travel, context.EdgeMargin, context.ViewportWidth - context.EdgeMargin) &&
            DodgeDirectionalAvoidance.AllowedAxis(py, py + option.Y * travel, context.EdgeMargin, context.ViewportHeight - context.EdgeMargin)).ToArray();
        if (eligible.Length == 0 || !eligible.Any(option => option.Name == baseline))
            throw new InvalidDataException("Exploration baseline is outside the bounded action set.");
        var explored = random.NextDouble() < ExplorationRate;
        var advice = explored ? eligible[random.Next(eligible.Length)].Name : baseline;
        var probability = ExplorationRate / eligible.Length + (advice == baseline ? 1 - ExplorationRate : 0);
        return new(advice, explored, probability, eligible.Length);
    }
}
