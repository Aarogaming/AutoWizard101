namespace ProjectMaelstrom.Utilities;

/// <summary>Read owns observation resource cleanup: it must latch the current
/// lease before throwing if hook or capture cleanup cannot be confirmed.</summary>
internal sealed record W101IdleCycleRuntime(Func<bool> Ready, Size Geometry,
    Func<OperationContext, int, RoutineBinding, CancellationToken, Task<W101ModuleObservation>> Read,
    Func<OperationContext, ModuleDecision, W101ModuleObservation, Func<bool>, CancellationToken, Task<RoutineReceipt>> Dispatch,
    Func<double> Clock, Func<long> UtcTicks, Func<string>? ReadinessDiagnostic = null, Func<CancellationToken, Task<bool>>? WaitForFocus = null);

/// <summary>One owned Idle/options/Idle visit. No navigation, quest advancement,
/// purchases or unbounded retry. Real clicks use the existing tagged UI transport.</summary>
internal static class W101IdleCycleOperation
{
    internal const string Id = "w101.idle-cycle";
    internal const string Version = "w101-idle-cycle-v1";

    internal static PreparedDeckJob Prepare(WizwalkerSession session, WizwalkerLaunch launch, bool enabled,
        W101IdleCycleRuntime? runtime = null, WizwalkerRuntime? validation = null, string mode = "round-trip", bool activateWhenQuiet = false)
    {
        if (!enabled) throw new InvalidOperationException("Enable this bounded module action check explicitly.");
        _ = W101IdleCycle.Graph(mode);
        var helper = Path.Combine(Path.GetDirectoryName(launch.Worker)!, "wizwalker_adapter.py");
        if (validation is null && !GameExperiments.Digest(helper).Equals(WizwalkerOperation.WorkerSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Reviewed UI helper changed.");
        var admitted = WizwalkerOperation.Prepare(session, launch, validation, Id, Version, W101UiTreeOperation.WorkerSha256);
        var pins = new Dictionary<string, string>(admitted.Pins, StringComparer.OrdinalIgnoreCase)
        { [Path.GetFullPath(helper)] = GameExperiments.Digest(helper) };
        var request = admitted with { TimeoutSeconds = 65, Pins = pins,
            Target = "pid-" + session.Target.ProcessId + "-start-" + session.Target.StartedUtcTicks };
        return new(request, async (context, token) =>
        {
            if (runtime != null) return await RunOwnedAsync(context, session.Target, runtime, token, mode).ConfigureAwait(false);
            var target = session.Target;
            await using var scope = await W101ControlScope.CreateAsync(context, target.Window, GameWindows.ClientSize(target.Window), token, activateWhenQuiet).ConfigureAwait(false);
            var geometry = WindowObserver.GetCaptureSize(target.Window);
            var live = CreateRuntime(target, launch, scope, geometry);
            return await RunOwnedAsync(context, target, live, token, mode).ConfigureAwait(false);
        });
    }

    /// <summary>Creates transports under an existing owner/control scope; never acquires or activates.</summary>
    internal static W101IdleCycleRuntime CreateRuntime(WizwalkerTarget target, WizwalkerLaunch launch,
        W101ControlScope scope, Size geometry)
    {
            var menu = new PetDanceMenuInput(scope.Tag);
            return new W101IdleCycleRuntime(scope.Ready, geometry,
                (owner, index, binding, cancellation) => W101ModuleObserver.ReadAsync(owner, target, launch, index, binding, geometry, scope.Ready, cancellation),
                async (owner, proposal, evidence, ready, cancellation) =>
                {
                    if (proposal.Action == W101IdleCycle.SelectIdle)
                    {
                        var controls = evidence.Recognition.Controls.Where(c => c.ControlId == "game-menu.idle").ToArray();
                        if (controls.Length != 1 || !ready())
                            return new(proposal.ProposalId!, proposal.ActionId!, FrameTimeline.TimestampMs(), false, true);
                        var receipt = await menu.DispatchAsync(new(PetDanceLifecycleAction.SelectIdle, true, "verified-options-idle",
                            controls[0].Bounds), target.Window, geometry, ready, cancellation, continuing: scope.Ready).ConfigureAwait(false);
                        return new(proposal.ProposalId!, proposal.ActionId!, FrameTimeline.TimestampMs(), receipt.Accepted, receipt.InputReleased);
                    }
                    return await W101ModuleInput.DispatchAsync(proposal, owner, ready,
                        (key, release) => W101ModuleInput.Send(target, key, release), FrameTimeline.TimestampMs, cancellation,
                        continuing: scope.Ready).ConfigureAwait(false);
                }, FrameTimeline.TimestampMs, () => DateTime.UtcNow.Ticks, () => scope.ReadinessReason, scope.WaitForFocusAsync);
    }

    internal static RoutineObservation Observe(WizwalkerTarget target, W101ModuleObservation sample,
        RoutineBinding binding, double clock, long utcTicks)
    {
        var reading = sample.Sample.Reading;
        bool State(string name, bool value) => reading.States?.GetValueOrDefault(name) == value && reading.StableStates?.GetValueOrDefault(name) == value;
        var valid = reading.Target == target && reading.SchemaVersion == 1 && reading.Operation == W101UiTreeOperation.Version &&
            reading.SourceSha256 == WizwalkerOperation.SourceSha256 && reading.Status == "observed" && reading.CleanupConfirmed && reading.HookRestored &&
            reading.TreeSamples >= 2 && reading.StatesObservedUtcTicks > 0 && reading.StatesObservedUtcTicks <= reading.ObservedUtcTicks &&
            reading.ObservedUtcTicks <= utcTicks && utcTicks - reading.StatesObservedUtcTicks <= TimeSpan.FromSeconds(2).Ticks;
        var scene = "Unknown"; var control = false;
        if (valid && reading.Loading == true) scene = "Loading";
        else if (valid && reading.Loading == false && State("dialogue", false))
        {
            if (State("nativeIdle", true) && State("menu", false)) { scene = "NativeIdle"; control = true; }
            else if (State("nativeIdle", false) && State("menu", false)) { scene = "AwakeCandidate"; control = true; }
            else if (State("nativeIdle", false) && State("menu", true))
            {
                var visual = sample.Recognition;
                var context = W101ModuleBindings.FromVisual(visual, binding, visual.Frame.Geometry, clock);
                if (context.GameMenuVerified && context.IdleControlVerified)
                { scene = "MenuObserved"; control = true; }
            }
        }
        return new(valid ? clock - (utcTicks - reading.StatesObservedUtcTicks) / (double)TimeSpan.TicksPerMillisecond : clock,
            scene, binding, valid, true, scene is not ("Unknown" or "Loading"), true, control);
    }

    /// <summary>Runs inside an existing scheduler/control owner. The caller owns
    /// the control scope and supplies a distinct context/evidence directory per stage.</summary>
    internal static async Task<OperationOutcome> RunOwnedAsync(OperationContext context, WizwalkerTarget target,
        W101IdleCycleRuntime runtime, CancellationToken token, string mode)
    {
        var binding = new RoutineBinding(context.Request.Target, "capture-" + runtime.Geometry.Width + "x" + runtime.Geometry.Height,
            "w101-idle-cycle.visit." + context.Request.Id);
        var cycle = new W101IdleCycle(binding, context.Request.Id, mode);
        var started = runtime.Clock(); var events = new List<object>();
        var initialPause = context.FocusBudget?.PausedDuration.TotalMilliseconds ?? 0;
        double PausedMs() => (context.FocusBudget?.PausedDuration.TotalMilliseconds ?? 0) - initialPause;
        RoutineObservation Logical(RoutineObservation observation) => observation with { TimeMs = observation.TimeMs - PausedMs() };
        var state = OperationState.Unknown; var reason = "observation-budget-exhausted";
        bool Ready() => !token.IsCancellationRequested && context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current) &&
            runtime.Ready() && double.IsFinite(runtime.Clock()) && runtime.Clock() >= started && runtime.Clock() - started - PausedMs() < 60000;
        try
        {
            for (var index = 0; index < 16; index++)
            {
                if (!Ready())
                {
                    if (runtime.WaitForFocus is null || !await runtime.WaitForFocus(token).ConfigureAwait(false) || !Ready())
                    { reason = "target-takeover-or-deadline"; break; }
                }
                // The observer owns hook/capture cleanup and latches uncertain
                // release itself. OCR, evidence-write and takeover failures after
                // confirmed cleanup must not create a false recovery lock.
                var sample = await runtime.Read(context, index, binding, token).ConfigureAwait(false);
                if (!sample.Sample.Reading.CleanupConfirmed || !sample.Sample.Reading.HookRestored)
                { context.Input!.RequireRecovery("Idle cycle hook cleanup is unconfirmed."); break; }
                if (sample.Geometry != runtime.Geometry || sample.Recognition.Frame.Binding != binding)
                { state = OperationState.Failed; reason = "capture-binding-or-geometry-changed"; break; }
                if (!Ready())
                {
                    if (runtime.WaitForFocus is not null && await runtime.WaitForFocus(token).ConfigureAwait(false) && Ready()) continue;
                    reason = "target-takeover-or-deadline"; break;
                }
                var observed = Observe(target, sample, binding, runtime.Clock(), runtime.UtcTicks());
                var decision = cycle.Observe(Logical(observed));
                events.Add(new { kind = "observation", sample, observed, decision });
                if (decision.Completed) { state = OperationState.Completed; reason = mode == "wake" ? "awake-state-independently-observed" : "idle-state-independently-observed"; break; }
                if (decision.Stopped) { state = OperationState.Failed; reason = decision.Reason; break; }
                if (!decision.IsProposal) continue;
                bool FreshReady()
                {
                    if (!Ready()) return false;
                    var current = Observe(target, sample, binding, runtime.Clock(), runtime.UtcTicks());
                    return current.Fresh && current.ControlVerified && current.Scene == observed.Scene;
                }
                if (!FreshReady()) { reason = "evidence-expired-before-input"; break; }
                RoutineReceipt receipt;
                try { receipt = await runtime.Dispatch(context, decision, sample, FreshReady, token).ConfigureAwait(false); }
                catch { context.Input!.RequireRecovery("Idle cycle transport returned no release receipt."); throw; }
                if (!receipt.InputReleased) context.Input!.RequireRecovery("Idle cycle input release is unconfirmed.");
                cycle.RecordReceipt(receipt with { TimeMs = receipt.TimeMs - PausedMs() }); events.Add(new { kind = "receipt", receipt });
                if (cycle.Status.Stopped) { state = OperationState.Failed; reason = cycle.Status.Reason; break; }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { state = OperationState.Cancelled; reason = "cancelled"; }
        catch (Exception error) { state = OperationState.Failed; reason = error.GetType().Name + ": " + error.Message; }
        if (context.Input?.RecoveryRequired == true) state = OperationState.CleanupFailed;
        // Preserve the concrete scope guard after an observer failure instead of
        // asking the user to repeat tests against an undifferentiated error.
        string? readinessDiagnostic = null;
        if (state != OperationState.Completed && runtime.ReadinessDiagnostic is { } diagnostic)
        {
            try
            {
                var detail = diagnostic();
                readinessDiagnostic = detail.Length <= 512 ? detail : detail[..512];
            }
            catch { readinessDiagnostic = "diagnostic-unavailable"; }
        }
        var report = Path.Combine(context.Directory, "idle-cycle.json");
        GameExperiments.WriteAtomic(report, new { schemaVersion = 1, mode, state = state.ToString(), reason, binding,
            actions = cycle.Actions, elapsedMs = runtime.Clock() - started, events, readinessDiagnostic,
            finalIdleVerified = state == OperationState.Completed && mode != "wake",
            finalAwakeVerified = state == OperationState.Completed && mode == "wake" });
        return new(state, reason, [report]);
    }
}
