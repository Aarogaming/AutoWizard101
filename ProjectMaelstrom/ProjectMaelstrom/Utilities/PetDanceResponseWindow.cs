namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceResponseEstimate(bool Started, bool Expired, bool CanFinish,
    double RemainingMs, double ScheduleMs, double? EstimatedCompleteQpcMs);

/// <summary>Tracks one response deadline from the first fresh Go source timestamp.
/// The caller must admit fresh frames before Observe and retain its normal focus,
/// ownership and phase checks: a timing estimate never grants input authority.</summary>
internal sealed class PetDanceResponseWindow
{
    public const double BudgetMs = 5000;
    public const double KeyHoldMs = 100;
    public const double KeyGapMs = 220;
    private double? _lastSourceMs, _lastEstimateMs;
    private bool _watchSeen;

    public double? GoQpcMs { get; private set; }
    public double? DeadlineQpcMs => GoQpcMs + BudgetMs;
    public double? CompletedQpcMs { get; private set; }
    public double? CompletionDurationMs => CompletedQpcMs - GoQpcMs;
    public bool? CompletedWithinBudget => CompletedQpcMs is double end && DeadlineQpcMs is double deadline
        ? end <= deadline : null;

    /// <summary>Pass only admitted, fresh source frames in strictly increasing
    /// QPC milliseconds. Unknown and repeated Go preserve the original deadline.
    /// A new Watch after Go starts the next preview; loading/ready/results invalidate
    /// this round. Capture freshness is intentionally owned by the caller.</summary>
    public void Observe(double sourceQpcMs, DancePhase phase)
    {
        ValidateTime(sourceQpcMs, nameof(sourceQpcMs));
        if (_lastSourceMs is double previous && sourceQpcMs <= previous)
            throw new ArgumentOutOfRangeException(nameof(sourceQpcMs), "Fresh source timestamps must strictly increase.");
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        _lastSourceMs = sourceQpcMs;
        if (phase is DancePhase.Loading or DancePhase.Ready or DancePhase.Success or DancePhase.Failure)
        {
            ResetRound();
            _watchSeen = false;
        }
        else if (phase == DancePhase.Watch)
        {
            if (GoQpcMs != null) ResetRound();
            _watchSeen = true;
        }
        else if (phase == DancePhase.Go && _watchSeen && GoQpcMs == null)
            GoQpcMs = sourceQpcMs;
    }

    /// <summary>Estimate all remaining holds, intervening gaps, and any existing
    /// cooldown until earliestNextDownMs. Use actual current QPC time even while
    /// frames are missing or stale; processing delays never pause this deadline.
    /// Finishing exactly at the deadline fits, but starting at the deadline does not.</summary>
    public PetDanceResponseEstimate Estimate(double nowQpcMs, int remainingKeys, double earliestNextDownMs = 0)
    {
        ValidateTime(nowQpcMs, nameof(nowQpcMs));
        ValidateTime(earliestNextDownMs, nameof(earliestNextDownMs));
        if ((_lastEstimateMs is double last && nowQpcMs < last) || (_lastSourceMs is double source && nowQpcMs < source))
            throw new ArgumentOutOfRangeException(nameof(nowQpcMs), "Current time must not go backwards or precede the latest admitted source.");
        if (remainingKeys is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(remainingKeys));
        _lastEstimateMs = nowQpcMs;
        var schedule = remainingKeys == 0 ? 0 : Math.Max(0, earliestNextDownMs - nowQpcMs) +
            remainingKeys * KeyHoldMs + (remainingKeys - 1) * KeyGapMs;
        if (DeadlineQpcMs is not double deadline)
            return new(false, false, false, 0, schedule, null);
        var completion = nowQpcMs + schedule;
        var expired = nowQpcMs >= deadline;
        return new(true, expired, !expired && remainingKeys > 0 && CompletedQpcMs == null && completion <= deadline,
            Math.Max(0, deadline - nowQpcMs), schedule, completion);
    }

    /// <summary>Call once after the final key has a confirmed release receipt.
    /// Late completion is retained as late evidence, not changed into success.</summary>
    public void Complete(double releasedQpcMs)
    {
        ValidateTime(releasedQpcMs, nameof(releasedQpcMs));
        if (GoQpcMs is not double start) throw new InvalidOperationException("No response window has started.");
        if (CompletedQpcMs != null) throw new InvalidOperationException("Completion is already recorded for this response.");
        if (releasedQpcMs < start || (_lastSourceMs is double source && releasedQpcMs < source))
            throw new ArgumentOutOfRangeException(nameof(releasedQpcMs), "Release cannot precede the response or latest source frame.");
        CompletedQpcMs = releasedQpcMs;
    }

    private void ResetRound()
    {
        GoQpcMs = null;
        CompletedQpcMs = null;
    }

    private static void ValidateTime(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name, "Use finite, nonnegative QPC milliseconds.");
    }
}
