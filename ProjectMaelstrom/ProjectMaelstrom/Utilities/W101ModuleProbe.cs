namespace ProjectMaelstrom.Utilities;

internal sealed record W101ModuleProbeRuntime(Func<WizwalkerTarget, bool> IsCurrent, Func<uint> Activity,
    Func<WizwalkerTarget, uint, bool> Ready, Func<WizwalkerTarget, Size> Geometry,
    Func<OperationContext, WizwalkerTarget, WizwalkerLaunch, int, RoutineBinding, Size, Func<bool>, CancellationToken,
        Task<W101ModuleObservation>> Read);

/// <summary>Explicit one-shot module evidence qualification. Reuses one scheduler
/// owner and the reviewed optional reader. Never sends inputs or marks bindings
/// live-qualified merely because the observer returned a candidate.</summary>
internal static class W101ModuleProbe
{
    internal const string Id = "w101.module-observe";
    internal const string Version = "w101-module-observe-v1";
    private static readonly W101ModuleProbeRuntime Runtime = new(target => target.IsCurrent(), W101DialogueInput.Activity,
        W101DialogueInput.Ready, target => WindowObserver.GetCaptureSize(target.Window),
        (context, target, launch, index, binding, geometry, ready, token) =>
            W101ModuleObserver.ReadAsync(context, target, launch, index, binding, geometry, ready, token));

    internal static OperationTicket Submit(OperationScheduler scheduler, WizwalkerSession session, WizwalkerLaunch launch,
        W101ModuleProbeRuntime? runtime = null)
    {
        var helper = Path.Combine(Path.GetDirectoryName(launch.Worker)!, "wizwalker_adapter.py");
        if (runtime is null && !GameExperiments.Digest(helper).Equals(WizwalkerOperation.WorkerSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The reviewed UI helper changed.");
        var validation = runtime is null ? null : new WizwalkerRuntime(runtime.IsCurrent,
            (_, _, _, _) => throw new InvalidOperationException("Validation only."));
        var request = WizwalkerOperation.Prepare(session, launch, validation, Id, Version, W101UiTreeOperation.WorkerSha256);
        var pins = request.Pins.ToDictionary(pair => pair.Key, pair => pair.Value);
        pins[Path.GetFullPath(helper)] = GameExperiments.Digest(helper);
        request = request with { Pins = pins, TimeoutSeconds = 25,
            Target = "pid-" + session.Target.ProcessId + "-start-" + session.Target.StartedUtcTicks };
        return scheduler.Submit(request, (context, token) => RunAsync(context, session.Target, launch, runtime ?? Runtime, token));
    }

    private static async Task<OperationOutcome> RunAsync(OperationContext context, WizwalkerTarget target,
        WizwalkerLaunch launch, W101ModuleProbeRuntime runtime, CancellationToken token)
    {
        var report = Path.Combine(context.Directory, "module-observation.json");
        var state = OperationState.Failed; var reason = "observation-not-started";
        var stage = "target-readiness";
        var started = FrameTimeline.TimestampMs();
        Size? geometry = null;
        string? errorType = null, errorMessage = null;
        W101ModuleDiagnostics? diagnostics = null;
        W101ModuleObservation? observation = null;
        try
        {
            var baseline = runtime.Activity();
            bool Ready() => !token.IsCancellationRequested && runtime.IsCurrent(target) && runtime.Ready(target, baseline) &&
                context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current);
            if (!Ready()) throw new InvalidOperationException("Selected window or user activity is not ready.");
            stage = "capture-geometry";
            geometry = runtime.Geometry(target);
            if (!ModuleRecognition.ValidGeometry(geometry.Value)) throw new InvalidDataException("Capture geometry is invalid.");
            var binding = W101ModuleBindings.Bundle("menu").Bind(context.Request.Target,
                "capture-" + geometry.Value.Width + "x" + geometry.Value.Height, context.Request.Id);
            stage = "module-observation";
            observation = await runtime.Read(context, target, launch, 0, binding, geometry.Value, Ready, token).ConfigureAwait(false);
            stage = "post-read-cleanup";
            if (!observation.Sample.Reading.CleanupConfirmed || !observation.Sample.Reading.HookRestored)
                context.Input!.RequireRecovery("Module probe returned unconfirmed reader cleanup.");
            token.ThrowIfCancellationRequested();
            if (!Ready()) throw new InvalidOperationException("Read ended after takeover or target change.");
            state = OperationState.Completed;
            stage = "completed";
            reason = "Read-only module evidence recorded; no input sent and no action qualification granted.";
            if (observation.ReadinessReason is "stale-ui-tree" or "stale-or-invalid-image-clock")
            {
                state = OperationState.Unknown;
                reason = "Candidate evidence recorded but stale; current module readiness is unknown.";
            }
        }
        catch (Exception error)
        {
            errorType = error.GetType().Name;
            errorMessage = new string(error.Message.Where(c => !char.IsControl(c)).Take(512).ToArray());
            diagnostics = error.Data["w101ModuleDiagnostics"] as W101ModuleDiagnostics;
            if (error is OperationCanceledException && token.IsCancellationRequested)
            { state = OperationState.Cancelled; reason = "cancelled"; }
            else reason = "observation-error-" + errorType;
        }
        if (context.Input?.RecoveryRequired == true)
        { state = OperationState.CleanupFailed; reason = "Observation cleanup needs recovery; further gameplay remains blocked."; }
        GameExperiments.WriteAtomic(report, new { schemaVersion = 1, inputCount = 0, liveQualified = false, actionReady = false,
            state = state.ToString(), reason, cleanupConfirmed = context.Input?.RecoveryRequired != true, observation,
            stage, elapsedMs = FrameTimeline.TimestampMs() - started,
            geometry = geometry is { } size ? new { width = size.Width, height = size.Height } : null,
            errorType, errorMessage, diagnostics });
        return new(state, reason, [report]);
    }
}
