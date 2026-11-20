namespace ProjectMaelstrom.Utilities;

/// <summary>One owner for a bounded home session and its visual Idle finalizer.</summary>
internal static class HomeMovementOperation
{
    internal static bool Owned(OperationContext context, GameWindow target) =>
        context.Request.WritesGame && !context.Request.DelegatedInput &&
        context.Request.Target == ModuleOwnedInput.TargetId(target) &&
        context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current);

    internal static PreparedDeckJob Prepare(GameWindow target, HomeMovementOptions options,
        Func<OperationContext, CancellationToken, Task<HomeMovementRunResult>> movement,
        Func<HomeMovementSignal> signal,
        Func<OperationContext, long, CancellationToken, Task<PetDanceNativeIdleResult>> idle)
    {
        options.Validate();
        if (!HomeMovementGate.IsWizard101(target) || target.ProcessId <= 0 || target.StartedUtcTicks <= 0 || target.Handle == 0)
            throw new InvalidDataException("Select the exact W101 process instance.");
        var timeout = Math.Min(1800, options.SessionMinutes * 60 + 50);
        var request = new OperationRequest(Guid.NewGuid().ToString("N"), "w101.auto-idle", "home-movement-v2",
            timeout, true, ModuleOwnedInput.TargetId(target), new Dictionary<string, string>());
        return new(request, async (context, token) =>
        {
            if (!Owned(context, target)) return new(OperationState.Failed, "Home input owner unavailable.", []);
            HomeMovementRunResult? body = null;
            PetDanceNativeIdleResult? finalization = null;
            string? skipped = null;
            using (var budget = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                budget.CancelAfter(TimeSpan.FromSeconds(timeout - 45));
                try { body = await movement(context, budget.Token).ConfigureAwait(false); }
                catch (Exception error)
                { context.Input!.RequireRecovery("Home movement escaped without a cleanup result: " + error.GetType().Name); }
            }
            if (token.IsCancellationRequested) skipped = "cancelled";
            else if (!Owned(context, target)) skipped = "owner-or-cleanup-unavailable";
            else if (body?.ActivityBaseline is not { } baseline) skipped = "movement-never-armed";
            else
            {
                var current = signal();
                if (!current.TargetReady || current.KeysHeld || current.Escape || !current.Enabled || current.ActivityRevision != baseline)
                    skipped = "takeover-or-target-changed";
                else
                {
                    try { finalization = await idle(context, baseline, token).ConfigureAwait(false); }
                    catch (Exception error)
                    {
                        context.Input!.RequireRecovery("Home Idle escaped without a cleanup result: " + error.GetType().Name);
                        skipped = "idle-cleanup-unconfirmed";
                    }
                }
            }
            var verified = finalization is { Confirmed: true, InputReleased: true, CaptureStopped: true };
            if (finalization is { InputReleased: false } or { CaptureStopped: false })
                context.Input!.RequireRecovery("Home Idle cleanup was not confirmed.");
            var state = context.Input?.RecoveryRequired == true ? OperationState.CleanupFailed :
                token.IsCancellationRequested ? OperationState.Cancelled :
                verified && body is { State: HomeMovementState.Stopped, Reason: "session-limit" }
                    ? OperationState.Completed : OperationState.Failed;
            var report = Path.Combine(context.Directory, "home-session.json");
            GameExperiments.WriteAtomic(report, new { body, finalization, idleVerified = verified, skipped,
                movementBudgetSeconds = timeout - 45, note = "Idle is distinct from verified motion or login retention." });
            return new(state, verified ? "Home session ended; native Idle verified." : "Home session ended; native Idle unverified.",
                body is null ? [report] : [body.Report, report]);
        });
    }
}
