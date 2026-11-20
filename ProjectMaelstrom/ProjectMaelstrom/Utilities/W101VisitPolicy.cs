namespace ProjectMaelstrom.Utilities;

/// <summary>Bounded end-of-routine policy. Retry Idle from fresh observations,
/// never replay gameplay. Quiet resumption is opt-in and never overrides Escape.</summary>
internal sealed record W101VisitPolicy(int IdleRetries = 2, bool ResumeAfterQuiet = false, int QuietSeconds = 2,
    bool ActivateWhenQuiet = false)
{
    internal const int IdleBudgetSeconds = 40;
    internal void Validate()
    {
        if (IdleRetries is < 0 or > 2 || QuietSeconds is < 2 or > 10)
            throw new ArgumentException("Idle retries must be 0–2; quiet period must be 2–10 seconds.");
    }
    internal static int BackoffMs(int retry) => retry switch
    { 1 => 250, 2 => 500, _ => throw new ArgumentOutOfRangeException(nameof(retry)) };
}

/// <summary>Initial focus admission only: five quiet seconds, original activity
/// baseline and every other control guard. Never rearms after takeover.</summary>
internal sealed class W101ForegroundQuietGate
{
    private readonly W101QuietPeriod _quiet = new();
    internal static bool Eligible(W101ControlReadiness state) => state.Code is "ready" or "target-not-foreground";
    internal bool Observe(W101ControlReadiness state, bool keysHeld) => Eligible(state) &&
        _quiet.Observe(state with { ForegroundHandle = state.TargetHandle }, keysHeld, 5);
}

/// <summary>Pure quiet detector. All non-activity scope checks remain mandatory;
/// caller must additionally verify no keys are held and recheck before rearming.</summary>
internal sealed class W101QuietPeriod
{
    private long? _revision;
    private double _since = double.NaN, _last = double.NaN;
    internal bool Observe(W101ControlReadiness state, bool keysHeld, int quietSeconds)
    {
        var candidate = state with { Baseline = state.ActivityRevision ?? -1 };
        if (!candidate.Ready || state.ActivityRevision is null || keysHeld ||
            quietSeconds is < 2 or > 10 || double.IsFinite(_last) && state.NowMs < _last)
        { _revision = null; _since = double.NaN; _last = state.NowMs; return false; }
        _last = state.NowMs;
        if (_revision != state.ActivityRevision || !double.IsFinite(_since))
        { _revision = state.ActivityRevision; _since = state.NowMs; }
        return state.NowMs - _since >= quietSeconds * 1000d;
    }
}
