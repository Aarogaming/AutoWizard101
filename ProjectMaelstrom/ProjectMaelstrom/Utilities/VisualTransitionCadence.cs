namespace ProjectMaelstrom.Utilities;

internal sealed record VisualTransitionCadencePlan(bool Analyze, int DelayMs, string Reason);

/// <summary>Separates bounded fresh-frame reacquisition from expensive visual
/// analysis. Call Plan after frame admission, analyze only when permitted, then
/// await its delay with the operation's cancellation token. Frames rejected or
/// rate-limited by the caller are discarded, never held across the delay.</summary>
internal sealed class VisualTransitionCadence
{
    public const int MinimumAnalysisIntervalMs = 500;
    public const int QuickRetryDelayMs = 25;
    public const int MaximumQuickRetries = 8;
    public const int MaximumReacquisitionMs = 250;
    public const int CooldownMs = 500;

    private double _lastTime = -1;
    private double _nextAnalysis;
    private double? _reacquisitionStarted;
    private int _quickRetries;
    public int AnalysisCount { get; private set; }
    public int QuickRetryCount { get; private set; }
    public int CooldownCount { get; private set; }
    public int RateLimitedCount { get; private set; }

    public VisualTransitionCadencePlan Plan(double nowMs, bool freshFrame, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!double.IsFinite(nowMs) || nowMs < 0 || nowMs < _lastTime || nowMs > double.MaxValue - MinimumAnalysisIntervalMs)
            throw new ArgumentOutOfRangeException(nameof(nowMs), "Use an increasing finite observation clock.");
        _lastTime = nowMs;
        if (nowMs < _nextAnalysis)
        {
            RateLimitedCount++;
            return new(false, Math.Max(1, (int)Math.Ceiling(_nextAnalysis - nowMs)), "analysis-rate-limit");
        }
        if (freshFrame)
        {
            _nextAnalysis = nowMs + MinimumAnalysisIntervalMs;
            _reacquisitionStarted = null;
            _quickRetries = 0;
            AnalysisCount++;
            return new(true, MinimumAnalysisIntervalMs, "analyze-fresh-frame");
        }

        _reacquisitionStarted ??= nowMs;
        if (_quickRetries < MaximumQuickRetries &&
            nowMs - _reacquisitionStarted.Value <= MaximumReacquisitionMs - QuickRetryDelayMs)
        {
            _quickRetries++;
            QuickRetryCount++;
            return new(false, QuickRetryDelayMs, "reacquire-fresh-frame");
        }
        // A capture outage cannot create an indefinitely fast polling loop. The
        // next attempt starts a new bounded burst only after this cooldown.
        _reacquisitionStarted = null;
        _quickRetries = 0;
        _nextAnalysis = nowMs + CooldownMs;
        CooldownCount++;
        return new(false, CooldownMs, "reacquisition-budget-cooldown");
    }
}
