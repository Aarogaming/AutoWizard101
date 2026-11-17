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

/// <summary>Optional capture-owning entry binding. Prime capture before Play, then
/// synchronously bind the real released Play receipt and first qualified Watch
/// before sending response keys. Invoke the callback exactly once; its returned
/// teacher proposal owns the eventual specialist receipt. No second specialist,
/// capture session or input owner may be started by this composite operation.</summary>
internal interface IPrimedPetDanceVisitRuntime : IPetDanceVisitRuntime
{
    Task<PetDanceSpecialistReceipt> PlayPrimedAsync(OperationContext context, PetDanceVisitPlan playPlan,
        Func<PetDanceVisitDispatch, PetDanceVisitObservation, PetDanceVisitPlan> bindHandoff,
        CancellationToken token);
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
            "scheduled-visit-v2", (int)Math.Ceiling(options.TimeoutMs / 1000) + 20,
            runtime.UsesGameInput, options.TargetId, new Dictionary<string, string>(runtime.Pins));
        return new(request, (context, token) => RunAsync(options, runtime, context, token));
    }

    internal static PreparedDeckJob PrepareProduction(PetDanceVisitOptions options, GameWindow target, string profile, string output) =>
        Prepare(options, new PetDanceProductionRuntime(target, options, profile, output));

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
                        if (plan.Action == PetDanceLifecycleAction.Play && runtime is IPrimedPetDanceVisitRuntime primed)
                        {
                            var receipt = await RunPrimedAsync(options, primed, context, visit, plan, trace, token).ConfigureAwait(false);
                            trace.Add(new { specialist = receipt });
                            visit.RecordSpecialist(receipt);
                        }
                        else if (plan.Action == PetDanceLifecycleAction.TeacherHandoff)
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

    private static async Task<PetDanceSpecialistReceipt> RunPrimedAsync(PetDanceVisitOptions options,
        IPrimedPetDanceVisitRuntime runtime, OperationContext context, PetDanceVisit visit,
        PetDanceVisitPlan playPlan, List<object> trace, CancellationToken token)
    {
        var gate = new object();
        var accepting = true;
        var calls = 0;
        string? invalid = null;
        PetDanceVisitPlan? handoff = null;
        PetDanceVisitPlan Bind(PetDanceVisitDispatch dispatch, PetDanceVisitObservation watch)
        {
            lock (gate)
            {
                token.ThrowIfCancellationRequested();
                if (!accepting || ++calls != 1)
                {
                    invalid = "Primed Play handoff was repeated or arrived after completion.";
                    throw new InvalidOperationException(invalid);
                }
                if (dispatch is null || watch is null || dispatch.ProposalId != playPlan.ProposalId ||
                    dispatch.Action != PetDanceLifecycleAction.Play || !dispatch.Accepted || !dispatch.InputReleased ||
                    !double.IsFinite(dispatch.ReleasedMs) || dispatch.ReleasedMs < 0 ||
                    watch.Scene != PetDanceScene.Watch || !double.IsFinite(watch.TimeMs) || watch.TimeMs <= dispatch.ReleasedMs ||
                    !watch.Fresh || !watch.TargetReady || !watch.ContextVerified || !watch.InputOwned ||
                    !watch.WatchStartVerified || !watch.SpecialistQualified || watch.Cancelled || watch.UserActive || watch.FocusPaused ||
                    watch.TargetId != options.TargetId || watch.Geometry != options.Geometry || watch.ContextId != options.ContextId ||
                    !PetDanceEntryVision.IsImageHash(watch.ImageSha256))
                {
                    invalid = "Primed Play requires its released receipt and a fresh, bound first Watch.";
                    throw new InvalidOperationException(invalid);
                }
                trace.Add(new { dispatch });
                visit.RecordDispatch(dispatch.ProposalId, dispatch.Action, dispatch.ReleasedMs, dispatch.Accepted, dispatch.InputReleased);
                handoff = visit.Observe(watch);
                trace.Add(new { observation = watch, plan = handoff });
                if (!handoff.IsProposal || handoff.Action != PetDanceLifecycleAction.TeacherHandoff ||
                    handoff.Arena != options.Arena || string.IsNullOrWhiteSpace(handoff.ProposalId))
                {
                    invalid = "Primed Play did not produce the matching teacher handoff: " + handoff.Reason;
                    throw new InvalidOperationException(invalid);
                }
                return handoff;
            }
        }

        PetDanceSpecialistReceipt receipt;
        try { receipt = await runtime.PlayPrimedAsync(context, playPlan, Bind, token).ConfigureAwait(false); }
        finally { lock (gate) accepting = false; }
        lock (gate)
        {
            // A runtime cannot swallow a rejected/duplicate callback and then
            // promote a fabricated completion receipt to successful gameplay.
            if (invalid is not null) throw new InvalidOperationException(invalid);
            if (calls != 1 || handoff is null || receipt is null || receipt.ProposalId != handoff.ProposalId)
                throw new InvalidOperationException("Primed Play completed without exactly one matching teacher handoff.");
        }
        return receipt;
    }

}
