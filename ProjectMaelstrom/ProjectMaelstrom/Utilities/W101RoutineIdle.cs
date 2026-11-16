namespace ProjectMaelstrom.Utilities;

internal sealed record W101RoutineIdleRuntime(Func<bool> Ready,
    Func<OperationContext, CancellationToken, Task<OperationOutcome>> EnsureIdle,
    Func<CancellationToken, Task<bool>>? WaitForFocus = null);

/// <summary>Adds the shared Idle finalizer to a complete routine, never a nested
/// motion/turn primitive. The scope spans body and finalization; no new lease,
/// focus acquisition or activity-baseline reset happens between them.</summary>
internal static class W101RoutineIdle
{
    internal static PreparedDeckJob Wrap(PreparedDeckJob body, WizwalkerTarget target, WizwalkerLaunch launch,
        W101RoutineIdleRuntime? runtime = null)
    {
        if (!body.Request.WritesGame || body.Request.DelegatedInput || body.Request.TimeoutSeconds > 1750)
            throw new InvalidDataException("Idle finalization requires a bounded directly owned routine.");
        var request = body.Request with { TimeoutSeconds = body.Request.TimeoutSeconds + 45,
            Version = body.Request.Version + ".idle-v1" };
        return new(request, async (context, token) =>
        {
            if (runtime is not null) return await RunAsync(body, context, runtime, token).ConfigureAwait(false);
            await using var scope = await W101ControlScope.CreateAsync(context, target.Window,
                GameWindows.ClientSize(target.Window), token, resumable: true).ConfigureAwait(false);
            var idle = W101IdleCycleOperation.CreateRuntime(target, launch, scope, WindowObserver.GetCaptureSize(target.Window));
            return await RunAsync(body, context, new(scope.Ready,
                (child, cancellation) => W101IdleCycleOperation.RunOwnedAsync(child, target, idle, cancellation, "idle"), scope.WaitForFocusAsync), token).ConfigureAwait(false);
        });
    }

    private static async Task<OperationOutcome> RunAsync(PreparedDeckJob body, OperationContext context,
        W101RoutineIdleRuntime runtime, CancellationToken token)
    {
        OperationOutcome outcome;
        using var fallbackBudget = context.FocusBudget is null ? new OperationFocusBudget() : null;
        var budgets = context.FocusBudget ?? fallbackBudget!;
        using (var deadline = budgets.CreateDeadline(TimeSpan.FromSeconds(body.Request.TimeoutSeconds), token))
        {
            try { outcome = await body.Execute(context, deadline.Token).ConfigureAwait(false); }
            catch (W101UiObservationCancelledException error) when (!token.IsCancellationRequested)
            {
                // This typed receipt confirms observer restoration. Finalize from
                // fresh state, without replaying an action whose outcome is unknown.
                outcome = new(OperationState.TimedOut, error.Message, []);
            }
            catch (Exception error)
            {
                // An escaping body exception provides no cleanup receipt.
                context.Input?.RequireRecovery("Routine escaped without a cleanup result.");
                outcome = new(OperationState.CleanupFailed, error.GetType().Name, []);
            }
            if (deadline.Token.IsCancellationRequested && !token.IsCancellationRequested && outcome.State != OperationState.CleanupFailed)
                outcome = outcome with { State = OperationState.TimedOut };
        }
        if (outcome.State == OperationState.CleanupFailed)
            context.Input?.RequireRecovery("Routine reported uncertain cleanup before Idle.");
        var result = outcome.State == OperationState.Cancelled
            ? new W101IdleFailsafeResult(null, "routine-cancelled", 0, [])
            : await W101IdleFailsafe.RunAsync(context, new() { ResumeAfterQuiet = runtime.WaitForFocus is not null }, runtime.Ready, async (index, cancellation) =>
        {
            var directory = Path.Combine(context.Directory, "idle-finalizer-" + index);
            if (Directory.Exists(directory)) throw new IOException("Idle evidence already exists.");
            Directory.CreateDirectory(directory);
            var child = context with { Directory = directory, Request = context.Request with
            { Id = Guid.NewGuid().ToString("N"), TimeoutSeconds = 20 } };
            using var deadline = budgets.CreateDeadline(TimeSpan.FromSeconds(20), cancellation);
            OperationOutcome attempt;
            try { attempt = await runtime.EnsureIdle(child, deadline.Token).ConfigureAwait(false); }
            catch (W101UiObservationCancelledException error) when (!cancellation.IsCancellationRequested)
            { attempt = new(OperationState.TimedOut, "Clean observation interrupted; retry from fresh state. " + error.Message, []); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            { throw; }
            catch (OperationCanceledException)
            {
                context.Input?.RequireRecovery("Idle cancellation escaped without a verified cleanup receipt.");
                attempt = new(OperationState.CleanupFailed, "Idle cancellation cleanup is unconfirmed.", []);
            }
            catch (Exception error)
            {
                context.Input?.RequireRecovery("Idle callback escaped without a cleanup result.");
                attempt = new(OperationState.CleanupFailed, error.GetType().Name, []);
            }
            return deadline.Token.IsCancellationRequested && attempt.State != OperationState.CleanupFailed
                ? attempt with { State = OperationState.TimedOut } : attempt;
        }, runtime.WaitForFocus is null ? null : (_, cancellation) => runtime.WaitForFocus(cancellation), token).ConfigureAwait(false);
        var verified = result.Outcome?.State == OperationState.Completed && result.SkippedReason is null;
        var state = context.Input?.RecoveryRequired == true ? OperationState.CleanupFailed :
            token.IsCancellationRequested ? OperationState.Cancelled :
            outcome.State == OperationState.Completed && !verified ? OperationState.Failed : outcome.State;
        var report = Path.Combine(context.Directory, "routine-idle-finalization.json");
        GameExperiments.WriteAtomic(report, new { body = outcome, finalization = result, idleVerified = verified });
        return new(state, outcome.Message + (verified ? " Native Idle verified." : " Native Idle not verified."),
            [.. outcome.Artifacts, report]);
    }
}
