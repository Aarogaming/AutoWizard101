namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceVisitDispatch(string ProposalId, PetDanceLifecycleAction Action,
    double ReleasedMs, bool Accepted, bool InputReleased);
internal sealed record PetDanceVisitCleanup(bool IdleVerified, bool InputReleased, string Reason);

/// <summary>Trusted application binding, never supplied by a saved card. Implementations
/// must use the supplied owner, recheck target/takeover before input and retain real receipts.</summary>
internal interface IPetDanceVisitRuntime
{
    bool UsesGameInput { get; }
    IReadOnlyDictionary<string, string> Pins { get; }
    Task<bool> CountdownAsync(OperationContext context, CancellationToken token);
    Task<PetDanceVisitObservation> ObserveAsync(OperationContext context, CancellationToken token);
    Task<PetDanceVisitDispatch> DispatchAsync(OperationContext context, PetDanceVisitPlan plan, CancellationToken token);
    Task<PetDanceSpecialistReceipt> PlayAsync(OperationContext context, PetDanceVisitPlan plan, CancellationToken token);
    Task<PetDanceVisitCleanup> FinishAsync(OperationContext context, bool idleAlreadyVerified, CancellationToken token);
}

/// <summary>Runs the existing receipt/outcome visit machine under one scheduler job.
/// Runtime availability and replay success are not live qualification or permission.</summary>
internal static class PetDanceScheduledVisit
{
    /// <summary>Production specialist handoff for a trusted runtime. This uses the
    /// parent's scheduler lease; it neither launches the standalone player nor
    /// treats successful round feedback as proof of the result screen.</summary>
    internal static async Task<PetDanceSpecialistReceipt> PlaySpecialistAsync(
        PetDanceVisitOptions options, OperationContext context, PetDanceVisitPlan plan,
        GameWindow target, string profile, string output, PetDanceSpecialistControl control,
        CancellationToken token)
    {
        if (!options.Enabled || !plan.IsProposal || plan.Action != PetDanceLifecycleAction.TeacherHandoff ||
            string.IsNullOrWhiteSpace(plan.ProposalId) || plan.Arena != options.Arena ||
            options.TargetId != ModuleOwnedInput.TargetId(target))
            throw new InvalidOperationException("Specialist requires the matching visit handoff.");
        var result = await PetDanceLiveTrial.RunSpecialistAsync(context, target, options.Geometry,
            profile, output, control, token).ConfigureAwait(false);
        return SpecialistReceipt(options, plan, result, FrameTimeline.TimestampMs());
    }

    internal static PetDanceSpecialistReceipt SpecialistReceipt(PetDanceVisitOptions options,
        PetDanceVisitPlan plan, PetDanceSpecialistResult result, double completedMs)
    {
        if (!plan.IsProposal || plan.Action != PetDanceLifecycleAction.TeacherHandoff ||
            string.IsNullOrWhiteSpace(plan.ProposalId) || plan.Arena != options.Arena ||
            !double.IsFinite(completedMs) || completedMs < 0)
            throw new InvalidOperationException("Invalid specialist receipt binding.");
        return new(plan.ProposalId, completedMs, options.TargetId, options.Geometry, options.ContextId,
            options.Arena, result.Armed, result.MayContinuePostGame, result.InputReleaseConfirmed);
    }

    internal static PreparedDeckJob Prepare(PetDanceVisitOptions options, IPetDanceVisitRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (!options.Enabled) throw new InvalidOperationException("Explicit visit enablement is required.");
        _ = new PetDanceVisit(options); // Validate before enqueuing or acquiring input.
        var request = new OperationRequest(Guid.NewGuid().ToString("N"), "w101.pet-dance-visit",
            "scheduled-visit-v1", (int)Math.Ceiling(options.TimeoutMs / 1000) + 20,
            runtime.UsesGameInput, options.TargetId, new Dictionary<string, string>(runtime.Pins));
        return new(request, (context, token) => RunAsync(options, runtime, context, token));
    }

    private static async Task<OperationOutcome> RunAsync(PetDanceVisitOptions options,
        IPetDanceVisitRuntime runtime, OperationContext context, CancellationToken token)
    {
        if (context.Request.Target != options.TargetId || context.Request.WritesGame != runtime.UsesGameInput ||
            context.Request.DelegatedInput || runtime.UsesGameInput &&
            (context.Input is not { CanDispatch: true } || !ReferenceEquals(context.Input, GameInputLease.Current)))
            throw new InvalidOperationException("Dance visit requires its matching shared owner.");
        var visit = new PetDanceVisit(options, context.Request.Id);
        var trace = new List<object>();
        var completed = false;
        var state = OperationState.Failed;
        var reason = "Observation budget exhausted.";
        var idle = false;
        PetDanceVisitCleanup? cleanup = null;
        try
        {
            if (!await runtime.CountdownAsync(context, token).ConfigureAwait(false))
                reason = "Countdown cancelled; no visit actions admitted.";
            else
            {
                for (var count = 0; count < 720; count++)
                {
                    token.ThrowIfCancellationRequested();
                    var observation = await runtime.ObserveAsync(context, token).ConfigureAwait(false);
                    var plan = visit.Observe(observation);
                    trace.Add(new { observation, plan });
                    if (plan.Completed) { completed = true; idle = observation.Scene == PetDanceScene.NativeIdle; break; }
                    if (plan.Stopped) { reason = plan.Reason; break; }
                    if (plan.IsProposal)
                    {
                        if (plan.Action == PetDanceLifecycleAction.TeacherHandoff)
                        {
                            var receipt = await runtime.PlayAsync(context, plan, token).ConfigureAwait(false);
                            trace.Add(new { specialist = receipt });
                            visit.RecordSpecialist(receipt);
                        }
                        else
                        {
                            var receipt = await runtime.DispatchAsync(context, plan, token).ConfigureAwait(false);
                            trace.Add(new { dispatch = receipt });
                            visit.RecordDispatch(receipt.ProposalId, receipt.Action, receipt.ReleasedMs,
                                receipt.Accepted, receipt.InputReleased);
                        }
                    }
                    else await Task.Delay(100, token).ConfigureAwait(false);
                }
                if (completed) { state = OperationState.Completed; reason = "Visit outcomes and native Idle observed."; }
            }
        }
        catch (OperationCanceledException) { state = OperationState.Cancelled; reason = "Visit cancelled."; }
        catch (Exception error) { reason = error.Message; }
        finally
        {
            // The runtime must release resources on cancellation but cannot evade takeover to restore Idle.
            try { cleanup = await runtime.FinishAsync(context, idle, token).ConfigureAwait(false); }
            catch (Exception error) { cleanup = new(false, false, "Cleanup unconfirmed: " + error.Message); }
        }
        if (!cleanup.InputReleased) { state = OperationState.CleanupFailed; reason = cleanup.Reason; }
        else if (completed && !cleanup.IdleVerified) { state = OperationState.Failed; reason = "Final Idle was not verified."; }
        var report = Path.Combine(context.Directory, "dance-visit.json");
        GameExperiments.WriteAtomic(report, new { schema_version = 1, context.Request.Id,
            mode = runtime.UsesGameInput ? "live-attempt" : "offline-runtime", live_qualified = false,
            completed = state == OperationState.Completed, state, reason, cleanup, trace });
        return new(state, reason, [report]);
    }
}
