namespace ProjectMaelstrom.Utilities;

/// <summary>Uses host-clock acquisition bounds already mapped by the trusted
/// reader. Context must bind target lifetime, loading epoch, assets and profile.
/// A pair proves bounded displacement only, not absence of motion between reads.</summary>
internal static class NavigationSettledPose
{
    internal static NavigationPoint? Validate(PathObservation before, PathObservation after,
        string zone, string context, double nowMs, double releasedMs, double maximumAgeMs,
        double minimumSpanMs, double maximumSpanMs, double positionNoise, double headingNoise)
    {
        if (string.IsNullOrWhiteSpace(zone) || string.IsNullOrWhiteSpace(context) ||
            !new[] { nowMs, releasedMs, maximumAgeMs, minimumSpanMs, maximumSpanMs, positionNoise, headingNoise }.All(double.IsFinite) ||
            releasedMs < 0 || maximumAgeMs <= 0 || minimumSpanMs <= 0 || maximumSpanMs < minimumSpanMs || positionNoise < 0 || headingNoise < 0)
            return null;
        bool Valid(PathObservation p) => p.Zone == zone && p.Context == context && p.Fresh &&
            !p.Loading && p.Focused && !p.Stopped && new[] { p.X, p.Y, p.Z, p.Heading, p.TimeMs }.All(double.IsFinite) &&
            p.TimeMs > releasedMs && p.TimeMs <= nowMs && nowMs - p.TimeMs <= maximumAgeMs;
        if (!Valid(before) || !Valid(after)) return null;
        double span = after.TimeMs - before.TimeMs;
        double displacement = Math.Sqrt(Math.Pow(after.X - before.X, 2) + Math.Pow(after.Y - before.Y, 2) + Math.Pow(after.Z - before.Z, 2));
        double turn = Math.Abs(Math.Atan2(Math.Sin(after.Heading - before.Heading), Math.Cos(after.Heading - before.Heading)));
        return span >= minimumSpanMs && span <= maximumSpanMs && displacement <= positionNoise && turn <= headingNoise
            ? new(after.X, after.Y, after.Z) : null;
    }
}
