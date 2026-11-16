namespace ProjectMaelstrom.Utilities;

internal sealed record W101IdleResume(int Attempt, bool Resumed, int QuietSeconds);
internal sealed record W101IdleFailsafeResult(OperationOutcome? Outcome, string? SkippedReason,
    int Attempts, W101IdleResume[] Resumes);

/// <summary>Reusable finalizer for owned W101 routines. Call only after the body
/// has drained and released inputs. Attempts must observe fresh state, confirm
/// Idle, and latch uncertain cleanup. Does not create ownership or replay a body.</summary>
internal static class W101IdleFailsafe
{
    internal static async Task<W101IdleFailsafeResult> RunAsync(OperationContext context,
        W101VisitPolicy policy, Func<bool> ready,
        Func<int, CancellationToken, Task<OperationOutcome>> attempt,
        Func<int, CancellationToken, Task<bool>>? resume, CancellationToken token)
    {
        policy.Validate();
        var resumes = new List<W101IdleResume>();
        OperationOutcome? outcome = null;
        var attempts = 0;
        bool Owned() => context.Request.WritesGame && !context.Request.DelegatedInput &&
            context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current);
        string? StopReason() => token.IsCancellationRequested ? "parent-cancelled" :
            context.Input?.RecoveryRequired == true ? "cleanup-uncertain" : !Owned() ? "input-owner-unavailable" : null;
        using var fallbackBudget = context.FocusBudget is null ? new OperationFocusBudget() : null;
        using var budget = (context.FocusBudget ?? fallbackBudget!).CreateDeadline(TimeSpan.FromSeconds(W101VisitPolicy.IdleBudgetSeconds), token);
        try
        {
            for (var index = 0; index <= policy.IdleRetries; index++)
            {
                if (StopReason() is { } stopped) return new(outcome, stopped, attempts, resumes.ToArray());
                if (index > 0) await Task.Delay(W101VisitPolicy.BackoffMs(index), budget.Token).ConfigureAwait(false);
                if (StopReason() is { } changed) return new(outcome, changed, attempts, resumes.ToArray());
                if (!ready() && policy.ResumeAfterQuiet && resume is not null)
                {
                    var resumed = await resume(policy.QuietSeconds, budget.Token).ConfigureAwait(false);
                    resumes.Add(new(index, resumed, policy.QuietSeconds));
                    if (!resumed) return new(outcome, "quiet-resume-unavailable", attempts, resumes.ToArray());
                }
                if (!ready()) return new(outcome, "target-owner-or-takeover-changed", attempts, resumes.ToArray());
                budget.Token.ThrowIfCancellationRequested();
                attempts++;
                outcome = await attempt(index, budget.Token).ConfigureAwait(false);
                if (outcome.State == OperationState.CleanupFailed)
                    context.Input!.RequireRecovery("Idle finalizer reported uncertain cleanup.");
                if (outcome.State is OperationState.Completed or OperationState.CleanupFailed or OperationState.Cancelled) break;
            }
            return new(outcome, StopReason(), attempts, resumes.ToArray());
        }
        catch (OperationCanceledException)
        { return new(outcome, token.IsCancellationRequested ? "parent-cancelled" : "idle-budget-exhausted", attempts, resumes.ToArray()); }
    }
}
