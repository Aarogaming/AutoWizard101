namespace ProjectMaelstrom.Utilities;

internal sealed record PetDancePostGameFrameAdmission(bool Accepted, string Reason, double? CapturedMs,
    double ObservedMs, double? AgeMs, int AlignmentWaitMs = 0);

/// <summary>Retains an original capture timestamp while waiting briefly for the
/// monotonic clock to reach it. This does not retimestamp or relax frame freshness.
/// Callers may OCR only an admitted frame and must recheck readiness before input.</summary>
internal static class PetDancePostGameFrameGate
{
    internal const double MaximumAgeMs = 300;
    internal const double MaximumAlignmentWaitMs = 30;

    public static async Task<PetDancePostGameFrameAdmission> InspectAsync(bool hasFrame, double? capturedMs,
        double previousCaptureMs, Func<double> clock, Func<bool> ready, CancellationToken token,
        Func<int, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(ready);
        delay ??= (milliseconds, cancellation) => Task.Delay(milliseconds, cancellation);
        var observed = clock();
        var waited = 0;
        PetDancePostGameFrameAdmission Reject(string reason) => new(false, reason, Finite(capturedMs),
            double.IsFinite(observed) ? observed : 0, Age(capturedMs, observed), waited);

        if (token.IsCancellationRequested) return Reject("cancelled");
        if (!hasFrame) return Reject("missing-frame");
        if (capturedMs is null) return Reject("missing-timestamp");
        if (!double.IsFinite(capturedMs.Value) || capturedMs.Value < 0 || !double.IsFinite(observed) || observed < 0)
            return Reject("invalid-timestamp");
        if (capturedMs.Value <= previousCaptureMs) return Reject("repeated-timestamp");
        var ahead = capturedMs.Value - observed;
        if (ahead is > 0 and <= MaximumAlignmentWaitMs)
        {
            waited = (int)Math.Ceiling(ahead);
            try { await delay(waited, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { observed = clock(); return Reject("cancelled"); }
            observed = clock();
        }
        if (token.IsCancellationRequested) return Reject("cancelled");
        // In particular, never OCR a frame after the user took over during the wait.
        if (!ready()) return Reject("not-ready");
        if (!double.IsFinite(observed) || observed < 0) return Reject("invalid-timestamp");
        if (capturedMs.Value > observed) return Reject("future-timestamp");
        if (!DodgeLiveRun.IsFreshCapture(capturedMs.Value, observed, MaximumAgeMs)) return Reject("stale-frame");
        return new(true, "fresh-frame", capturedMs, observed, observed - capturedMs.Value, waited);
    }

    private static double? Finite(double? value) => value is { } number && double.IsFinite(number) ? number : null;
    private static double? Age(double? captured, double observed) =>
        captured is { } value && double.IsFinite(value) && double.IsFinite(observed) && double.IsFinite(observed - value)
            ? observed - value : null;
}
