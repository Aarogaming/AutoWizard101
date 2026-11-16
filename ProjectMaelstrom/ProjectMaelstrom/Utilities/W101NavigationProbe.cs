namespace ProjectMaelstrom.Utilities;

/// <summary>The reader owns cleanup classification and must latch the current
/// lease before throwing if hook or capture cleanup cannot be confirmed.</summary>
internal sealed record W101NavigationProbeRuntime(Func<bool> Ready, Size Geometry,
    Func<OperationContext, int, RoutineBinding, CancellationToken, Task<W101ModuleObservation>> Read,
    Func<OperationContext, ModuleDecision, Func<bool>, CancellationToken, Task<RoutineReceipt>> Dispatch,
    Func<double> Clock, Func<long> UtcTicks);

/// <summary>One explicitly selected teaching pulse, with independent before/after
/// evidence. Excluding menus is not proof of a safe route: this does not grant
/// OutsideReady, infer arrival, retry, or automatically enter native Idle.</summary>
internal static class W101NavigationProbe
{
    internal const string Id = "w101.navigation-probe";
    internal const string Version = "w101-navigation-probe-v1";

    internal static PreparedDeckJob Prepare(WizwalkerSession session, WizwalkerLaunch launch, bool enabled,
        string key, int durationMs, W101NavigationProbeRuntime? runtime = null, WizwalkerRuntime? validation = null, bool assistedHold = false)
    {
        if (!enabled) throw new InvalidOperationException("Enable this explicitly selected teaching pulse.");
        var actionId = key switch
        {
            "W" => "game.move-forward", "S" => "game.move-backward", "A" => "game.turn-left", "D" => "game.turn-right",
            _ => throw new InvalidDataException("Choose W, A or D for one teaching pulse.")
        };
        if (durationMs < 1 || durationMs > (assistedHold && key is "W" or "S" ? 3000 : 300)) throw new InvalidDataException("Teaching pulses must last 1–300 ms.");
        var helper = Path.Combine(Path.GetDirectoryName(launch.Worker)!, "wizwalker_adapter.py");
        if (validation is null && !GameExperiments.Digest(helper).Equals(WizwalkerOperation.WorkerSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Reviewed UI helper changed.");
        var admitted = WizwalkerOperation.Prepare(session, launch, validation, Id, Version, W101UiTreeOperation.WorkerSha256);
        var pins = new Dictionary<string, string>(admitted.Pins, StringComparer.OrdinalIgnoreCase)
        { [Path.GetFullPath(helper)] = GameExperiments.Digest(helper) };
        var request = admitted with { TimeoutSeconds = 30, Pins = pins,
            Target = "pid-" + session.Target.ProcessId + "-start-" + session.Target.StartedUtcTicks };
        var action = new ModuleAction(actionId, "keyboard-pulse", key, durationMs, "w101.navigation");
        return new(request, async (context, token) =>
        {
            if (runtime is not null) return await RunOwnedAsync(context, session.Target, action, runtime, token).ConfigureAwait(false);
            var target = session.Target;
            await using var scope = await W101ControlScope.CreateAsync(context, target.Window, GameWindows.ClientSize(target.Window), token).ConfigureAwait(false);
            var geometry = WindowObserver.GetCaptureSize(target.Window);
            var live = CreateRuntime(target, launch, scope, geometry);
            return await RunOwnedAsync(context, target, action, live, token).ConfigureAwait(false);
        });
    }

    /// <summary>Uses an existing control scope; no additional lease, focus change or activity baseline.</summary>
    internal static W101NavigationProbeRuntime CreateRuntime(WizwalkerTarget target, WizwalkerLaunch launch,
        W101ControlScope scope, Size geometry)
    {
            return new W101NavigationProbeRuntime(scope.Ready, geometry,
                (owner, index, binding, cancellation) => W101ModuleObserver.ReadAsync(owner, target, launch, index, binding, geometry, scope.Ready, cancellation, recognize: false),
                async (owner, proposal, ready, cancellation) =>
                {
                    const string promptPrefix = "w101.walk-until-interaction:";
                    if (proposal.Action?.ControlId?.StartsWith(promptPrefix, StringComparison.Ordinal) == true)
                        return await W101NavigationPrompt.RunAsync(owner, target, launch, proposal.Action.ControlId[promptPrefix.Length..], proposal,
                            (before, continuing) => W101NavigationInput.DispatchAsync(proposal, owner, owner.Request.Target, before,
                                (pressed, release) => W101NavigationInput.Send(target, pressed, release), FrameTimeline.TimestampMs, cancellation, continuing),
                            scope.Ready, cancellation).ConfigureAwait(false);
                    var untilLoading = proposal.Action?.ControlId == "w101.walk-until-loading";
                    var home = proposal.Action == W101CharacterOperation.GoHome || proposal.Action == W101CharacterOperation.GoCommons;
                    var assisted = proposal.Action?.ControlId == "w101.assisted-navigation" || untilLoading || home;
                    var index = 0;
                    var loadingLaunch = launch with { Worker = Path.Combine(Path.GetDirectoryName(launch.Worker)!, "wizwalker_adapter.py") };
                    async Task<bool?> ReadLoading(CancellationToken ct)
                    {
                        var directory = Path.Combine(owner.Directory, "transition-" + (index++).ToString("D2"));
                        Directory.CreateDirectory(directory);
                        var outcome = await WizwalkerOperation.ReadOwnedAsync(owner with { Directory = directory }, target, loadingLaunch, null, ct).ConfigureAwait(false);
                        if (outcome.State != OperationState.Completed) return null;
                        var result = System.Text.Json.JsonSerializer.Deserialize<WizwalkerResult>(GameExperiments.BoundedBytes(Path.Combine(directory, "adapter-result.json"), 16384));
                        return result?.Loading;
                    }
                    Task<RoutineReceipt> Move(Func<bool> continuing) => W101NavigationInput.DispatchAsync(proposal, owner, owner.Request.Target, ready,
                        (pressed, release) => W101NavigationInput.Send(target, pressed, release), FrameTimeline.TimestampMs, cancellation, continuing);
                    if (proposal.Action is { ControlId: "w101.navigation", DurationMs: <= 150 } &&
                        owner.Request.Pins.Keys.Any(p => Path.GetFileName(p) == "w101_pose_reader.py"))
                    {
                        var trace = Path.Combine(owner.Directory, "read-23", "ui-observations.jsonl");
                        bool SamplingReady()
                        {
                            if (!scope.Ready() || !File.Exists(trace)) return false;
                            using var file = new FileStream(trace, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            if (file.Length > 4 * 1024 * 1024) return false;
                            using var text = new StreamReader(file);
                            return W101MovingProbe.WindowReady(text.ReadToEnd(), DateTime.UtcNow.Ticks);
                        }
                        return await W101MovingProbe.RunAsync(
                            async ct => { await W101UiTreeOperation.ReadOwnedAsync(owner, target, launch, 23, ct, true, movingPoseSeconds: 2).ConfigureAwait(false); },
                            SamplingReady, scope.Ready,
                            () => W101NavigationInput.DispatchAsync(proposal, owner, owner.Request.Target, SamplingReady,
                                (pressed, release) => W101NavigationInput.Send(target, pressed, release), FrameTimeline.TimestampMs, cancellation, scope.Ready),
                            new(proposal.ProposalId!, proposal.ActionId!, FrameTimeline.TimestampMs(), false, true), cancellation).ConfigureAwait(false);
                    }
                    W101NavigationTransition.WalkResult? walk = null;
                    RoutineReceipt receipt;
                    if (untilLoading)
                    {
                        walk = await W101NavigationTransition.WalkAsync(ReadLoading, Move, scope.Ready, FrameTimeline.TimestampMs, cancellation, initialClear: true).ConfigureAwait(false);
                        receipt = walk.Receipt;
                        GameExperiments.WriteAtomic(Path.Combine(owner.Directory, "loading-walk.json"), walk);
                        if (!walk.SawLoading || !receipt.InputReleased || !scope.Ready()) return receipt with { Accepted = false };
                    }
                    else if (home) receipt = await W101ModuleInput.DispatchAsync(proposal, owner, ready,
                        (pressed, release) => W101ModuleInput.Send(target, pressed, release), FrameTimeline.TimestampMs, cancellation, scope.Ready).ConfigureAwait(false);
                    else receipt = await Move(scope.Ready).ConfigureAwait(false);
                    if ((!untilLoading && !receipt.Accepted) || !receipt.InputReleased || !assisted) return receipt;
                    var transition = await W101NavigationTransition.WaitAsync(ReadLoading, scope.Ready, FrameTimeline.TimestampMs, cancellation, requireLoading: home).ConfigureAwait(false);
                    if (walk?.SawLoading == true) transition = transition with { SawLoading = true, Reason = transition.Ready ? "loading-cleared-destination-unverified" : transition.Reason };
                    GameExperiments.WriteAtomic(Path.Combine(owner.Directory, "navigation-transition.json"), transition);
                    return receipt with { Accepted = transition.Ready };

                },
                FrameTimeline.TimestampMs, () => DateTime.UtcNow.Ticks);
    }

    private static bool Fresh(W101ModuleObservation sample, WizwalkerTarget target, RoutineBinding binding,
        Size geometry, double now, long utc)
    {
        ModuleRecognition.Validate(sample.Recognition);
        var frame = sample.Recognition.Frame;
        var read = sample.Sample.Reading;
        var captureOnly = frame.TextOrigin == "selected-window-capture" && sample.Recognition.Scene == "Unknown" &&
            sample.Recognition.Controls.Length == 0 && sample.Recognition.VisualFacts.Length == 0;
        return frame.Binding == binding && frame.Geometry == geometry && (frame.TextOrigin == "local-windows-ocr" || captureOnly) &&
            double.IsFinite(now) && frame.CapturedMs <= now && now - frame.CapturedMs <= ModuleRecognition.MaximumAgeMs &&
            read.Target == target && read.SchemaVersion == 1 && read.Operation == W101UiTreeOperation.Version &&
            read.SourceSha256 == WizwalkerOperation.SourceSha256 && read.Status == "observed" &&
            read.CleanupConfirmed && read.HookRestored && read.TreeSamples >= 2 &&
            read.StatesObservedUtcTicks > 0 && read.StatesObservedUtcTicks <= read.ObservedUtcTicks &&
            read.ObservedUtcTicks <= utc && utc - read.StatesObservedUtcTicks <= TimeSpan.FromSeconds(2).Ticks;
    }

    private static bool PulseScene(W101ModuleObservation sample)
    {
        var read = sample.Sample.Reading;
        return read.Loading == false && new[] { "nativeIdle", "menu", "dialogue" }.All(name =>
            read.States?.GetValueOrDefault(name) == false && read.StableStates?.GetValueOrDefault(name) == false) &&
            sample.Recognition.Scene is "Unknown" or "OutsideCandidate";
    }

    /// <summary>Runs one pulse under an existing owner, without acquiring a scope.
    /// The caller supplies the stage identity and evidence directory.</summary>
    internal static async Task<OperationOutcome> RunOwnedAsync(OperationContext context, WizwalkerTarget target,
        ModuleAction action, W101NavigationProbeRuntime runtime, CancellationToken token)
    {
        var binding = new RoutineBinding(context.Request.Target, "capture-" + runtime.Geometry.Width + "x" + runtime.Geometry.Height,
            Id + ".visit." + context.Request.Id);
        W101ModuleObservation? before = null, after = null;
        RoutineReceipt? receipt = null;
        var started = runtime.Clock();
        var state = OperationState.Failed; var reason = "unqualified-before-observation";
        bool Ready() => !token.IsCancellationRequested && context.Input is { CanDispatch: true } &&
            ReferenceEquals(context.Input, GameInputLease.Current) && runtime.Ready() &&
            double.IsFinite(runtime.Clock()) && runtime.Clock() >= started && runtime.Clock() - started < (action.ControlId != "w101.navigation" ? 60000 : 26000);
        bool Clean(W101ModuleObservation sample)
        {
            if (sample.Sample.Reading.CleanupConfirmed && sample.Sample.Reading.HookRestored) return true;
            context.Input!.RequireRecovery("Navigation observation hook cleanup is unconfirmed.");
            return false;
        }
        try
        {
            if (Ready())
            {
                before = await runtime.Read(context, 0, binding, token).ConfigureAwait(false);
                bool FreshReady() => Ready() && Fresh(before, target, binding, runtime.Geometry, runtime.Clock(), runtime.UtcTicks()) && PulseScene(before);
                if (Clean(before) && FreshReady())
                {
                    var proposal = new ModuleDecision(Id, "pulse", "explicit-teaching-pulse", context.Request.Id + ".pulse", action.ActionId, action);
                    try { receipt = await runtime.Dispatch(context, proposal, FreshReady, token).ConfigureAwait(false); }
                    catch { context.Input!.RequireRecovery("Navigation transport returned no release receipt."); throw; }
                    var receiptUtcTicks = runtime.UtcTicks();
                    if (!receipt.InputReleased) context.Input!.RequireRecovery("Navigation key release is unconfirmed.");
                    reason = "dispatch-unconfirmed";
                    if (receipt.Accepted && receipt.InputReleased && receipt.ProposalId == proposal.ProposalId &&
                        receipt.ActionId == action.ActionId && double.IsFinite(receipt.TimeMs) && receipt.TimeMs <= runtime.Clock() &&
                        receipt.TimeMs >= before.Recognition.Frame.CapturedMs && Ready())
                    {
                        after = await runtime.Read(context, 1, binding, token).ConfigureAwait(false);
                        reason = "after-observation-unqualified";
                        if (Clean(after) && Ready() && Fresh(after, target, binding, runtime.Geometry, runtime.Clock(), runtime.UtcTicks()) &&
                            after.Recognition.Frame.CapturedMs > receipt.TimeMs &&
                            after.Sample.Reading.StatesObservedUtcTicks > receiptUtcTicks)
                        { state = OperationState.Completed; reason = "teaching-evidence-collected-arrival-not-verified"; }
                    }
                }
            }
            else reason = "target-takeover-or-deadline";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { state = OperationState.Cancelled; reason = "cancelled"; }
        catch (Exception error) { state = OperationState.Failed; reason = error.GetType().Name + ": " + error.Message; }
        if (context.Input?.RecoveryRequired == true) state = OperationState.CleanupFailed;
        var report = Path.Combine(context.Directory, "navigation-probe.json");
        GameExperiments.WriteAtomic(report, new { schemaVersion = 1, state = state.ToString(), reason, binding, action,
            before, receipt, after, arrivalVerified = false, routeQualified = false,
            imageChanged = before is not null && after is not null ? before.Recognition.Frame.ImageSha256 != after.Recognition.Frame.ImageSha256 : (bool?)null,
            elapsedMs = runtime.Clock() - started });
        return new(state, reason, [report]);
    }
}
