namespace ProjectMaelstrom.Utilities;

/// <summary>Pure, fail-closed activity-flag evaluator. Produces CANDIDATE TRIGGERS only:
/// never dispatches input, never qualifies gameplay, and never promotes a fact to
/// available. Implements the accepted UI-SCHEMA-CATALOG-01 predicate for
/// activity.diego.visible (3 consecutive visible samples over >=600 ms with target,
/// image, loading and Idle gates). Hidden nodes are never activity evidence.</summary>
internal sealed class W101ActivityFlags
{
    internal const int RequiredConsecutiveSamples = 3;
    internal const long MinimumSpanMs = 600;

    private readonly Dictionary<string, Streak> _streaks = new(StringComparer.Ordinal);

    private sealed class Streak
    {
        internal int Consecutive;
        internal long FirstMs;
        internal long LastMs;
    }

    /// <summary>Evaluates one fresh tree reading for a named flag. Returns true only
    /// on the sample that completes a qualifying streak; downstream consumers must
    /// treat it as a trigger, not as an implemented activity player.</summary>
    internal bool Observe(string flag, IReadOnlyDictionary<string, bool> visibleNodes,
        IReadOnlyCollection<string> requiredVisibleNodes, bool targetBindingVerified,
        bool clientImageMatched, bool loading, bool nativeIdle, long nowUtcTicks)
    {
        ArgumentNullException.ThrowIfNull(flag);
        ArgumentNullException.ThrowIfNull(visibleNodes);
        ArgumentNullException.ThrowIfNull(requiredVisibleNodes);
        if (requiredVisibleNodes.Count == 0 || !targetBindingVerified || !clientImageMatched || loading || nativeIdle)
        {
            Reset(flag);
            return false;
        }
        foreach (var node in requiredVisibleNodes)
        {
            if (!visibleNodes.TryGetValue(node, out var visible) || !visible)
            {
                Reset(flag);
                return false;
            }
        }
        if (!_streaks.TryGetValue(flag, out var streak))
        {
            streak = new Streak();
            _streaks[flag] = streak;
        }
        var nowMs = nowUtcTicks / TimeSpan.TicksPerMillisecond;
        if (streak.Consecutive == 0 || nowMs - streak.LastMs > 2000)
        {
            streak.Consecutive = 1;
            streak.FirstMs = nowMs;
        }
        else if (nowMs >= streak.LastMs)
        {
            streak.Consecutive++;
        }
        streak.LastMs = nowMs;
        return streak.Consecutive >= RequiredConsecutiveSamples &&
               streak.LastMs - streak.FirstMs >= MinimumSpanMs;
    }

    internal void Reset(string flag) => _streaks.Remove(flag);
}
