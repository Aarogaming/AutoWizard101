namespace ProjectMaelstrom.Utilities;

internal sealed record W101ModuleSample(W101UiTreeResult Reading, W101ModuleContext Context,
    string? OutcomeForProposalId = null, string? InteractionPrompt = null);
/// <summary>Trusted application callbacks; never loaded from a pack. Read must
/// independently correlate outcomes; Dispatch must verify current target/control
/// geometry, call ready before sending, and confirm releases before returning.</summary>
internal sealed record W101ModuleRuntime(Func<bool> Ready,
    Func<OperationContext, string?, CancellationToken, Task<W101ModuleSample>> Read,
    Func<OperationContext, ModuleDecision, Func<bool>, CancellationToken, Task<RoutineReceipt>> Dispatch,
    Func<double> Clock, Func<long> UtcTicks);

internal static class W101ModuleOperation
{
    internal static W101ModuleCardAdapter ForCards(WizwalkerTarget target, string geometryId,
        W101ModuleRuntime runtime, Func<string, bool> qualified)
    {
        string? Readiness(string mode, JobCardSpec card, JobFacts? facts) =>
            !qualified(mode) || !runtime.Ready() ? "W101 module binding is not qualified and ready." :
            facts is null || facts.GameId != "wizard101" || facts.TargetId != W101QuestStep.TargetBinding(target) ||
            runtime.UtcTicks() < facts.ObservedUtc.Ticks || runtime.UtcTicks() - facts.ObservedUtc.Ticks > TimeSpan.FromSeconds(2).Ticks
                ? "Fresh selected-target observations are required." : null;
        return new((mode, card, facts) =>
        {
            var blocked = Readiness(mode, card, facts);
            if (blocked != null) throw new InvalidOperationException(blocked);
            var bundle = W101ModuleBindings.Bundle(mode);
            if (mode == "menu" && card.Settings?.GetValueOrDefault("action", "open-menu") != "open-menu" ||
                mode == "native-idle" && card.Settings?.GetValueOrDefault("method", "native-idle") != "native-idle")
                throw new InvalidDataException("Only opening the game menu and native Idle entry are bound.");
            var binding = bundle.Bind("pid-" + target.ProcessId + "-start-" + target.StartedUtcTicks,
                geometryId, Guid.NewGuid().ToString("N"));
            if (!int.TryParse(card.Settings?.GetValueOrDefault("timeoutSeconds") ?? "30", out var timeout) || timeout is < 1 or > 60)
                throw new InvalidDataException("Module card timeout must be 1–60 seconds.");
            var prompt = mode == "sigil" ? card.Settings?.GetValueOrDefault("prompt") : null;
            if (mode == "sigil" && (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 160))
                throw new InvalidDataException("Select the exact interaction prompt.");
            return Prepare(mode, target, binding, true, runtime with { Ready = () => qualified(mode) && runtime.Ready() },
                timeoutSeconds: timeout, expectedPrompt: prompt);
        }, Readiness);
    }

    internal static PreparedDeckJob Prepare(string mode, WizwalkerTarget target, RoutineBinding binding,
        bool enabled, W101ModuleRuntime runtime, int maximumReadings = 24, int timeoutSeconds = 60, string? expectedPrompt = null)
    {
        if (!enabled) throw new InvalidOperationException("Enable the separately qualified W101 module adapter first.");
        ArgumentNullException.ThrowIfNull(runtime);
        if (maximumReadings is < 1 or > 24) throw new ArgumentOutOfRangeException(nameof(maximumReadings));
        if (timeoutSeconds is < 1 or > 60) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        if (binding.TargetId != "pid-" + target.ProcessId + "-start-" + target.StartedUtcTicks)
            throw new InvalidDataException("Bind the selected W101 process instance.");
        var source = W101ModuleBindings.Bundle(mode);
        var bundle = source with { TimeoutMs = Math.Min(source.TimeoutMs, timeoutSeconds * 1000d) };
        var kind = mode == "native-idle" ? "auto-idle" : mode;
        return ModuleJobAdapter.PrepareOwned(bundle, kind, binding,
            (context, token) => RunAsync(context, mode, target, bundle, binding, runtime, maximumReadings, expectedPrompt, token),
            Math.Min(timeoutSeconds, (int)Math.Ceiling(bundle.TimeoutMs / 1000) + 5), "wizard101");
    }

    private static async Task<OperationOutcome> RunAsync(OperationContext context, string mode,
        WizwalkerTarget target, OperationModuleBundle bundle, RoutineBinding binding,
        W101ModuleRuntime runtime, int maximumReadings, string? expectedPrompt, CancellationToken token)
    {
        var runner = new ModuleRunner(bundle, binding, context.Request.Id);
        var events = new List<object>(); var started = runtime.Clock();
        var report = Path.Combine(context.Directory, "w101-module.json");
        var state = OperationState.Unknown; var reason = "reading-budget-exhausted";
        string? pending = null;
        bool Ready() => !token.IsCancellationRequested && context.Input is { CanDispatch: true } &&
            ReferenceEquals(context.Input, GameInputLease.Current) && runtime.Ready() &&
            double.IsFinite(runtime.Clock()) && runtime.Clock() >= started && runtime.Clock() - started < bundle.TimeoutMs;
        try
        {
            for (var index = 0; index < maximumReadings; index++)
            {
                token.ThrowIfCancellationRequested();
                if (index > 0) await Task.Delay(50, token).ConfigureAwait(false);
                if (!Ready()) { state = OperationState.Failed; reason = "target-takeover-or-deadline"; break; }
                W101ModuleSample sample;
                try { sample = await runtime.Read(context, pending, token).ConfigureAwait(false); }
                catch
                {
                    context.Input!.RequireRecovery("W101 module reader failed without a cleanup receipt.");
                    throw;
                }
                if (!sample.Reading.CleanupConfirmed || !sample.Reading.HookRestored)
                {
                    context.Input!.RequireRecovery("W101 module reader did not confirm cleanup.");
                    state = OperationState.Failed; reason = "reader-cleanup-unconfirmed"; break;
                }
                if (!Ready()) { state = OperationState.Failed; reason = "target-takeover-or-deadline"; break; }
                var authority = new RoutineObservation(runtime.Clock(), "Unknown", binding, true, true, true, true, true,
                    OutcomeForProposalId: sample.OutcomeForProposalId);
                var frame = W101ModuleBindings.Map(target, sample.Reading, authority, sample.Context, runtime.UtcTicks());
                var decision = runner.Observe(frame);
                events.Add(new { kind = "observation", index, frame, decision });
                if (decision.Completed) { state = OperationState.Completed; reason = "observed-module-completion"; break; }
                if (decision.Stopped) { state = OperationState.Failed; reason = decision.Reason; break; }
                if (!decision.IsProposal) continue;
                if (mode == "sigil" && expectedPrompt is not null && sample.InteractionPrompt != expectedPrompt)
                { state = OperationState.Failed; reason = "selected-prompt-mismatch"; break; }
                bool FreshReady()
                {
                    if (!Ready()) return false;
                    var current = W101ModuleBindings.Map(target, sample.Reading,
                        authority with { TimeMs = runtime.Clock() }, sample.Context, runtime.UtcTicks());
                    return current.Observation.Fresh && current.Observation.ContextVerified && current.Observation.ControlVerified &&
                        current.Observation.Scene == frame.Observation.Scene && current.VerifiedControls!.Contains(decision.Action!.ControlId);
                }
                if (!FreshReady()) { state = OperationState.Failed; reason = "control-no-longer-ready"; break; }
                RoutineReceipt receipt;
                try { receipt = await runtime.Dispatch(context, decision, FreshReady, token).ConfigureAwait(false); }
                catch
                {
                    context.Input!.RequireRecovery("W101 module transport did not confirm input release.");
                    throw;
                }
                if (!receipt.InputReleased) context.Input!.RequireRecovery("W101 module input release unconfirmed.");
                runner.RecordReceipt(receipt); events.Add(new { kind = "receipt", receipt });
                pending = decision.ProposalId;
                if (runner.Status.Stopped) { state = OperationState.Failed; reason = runner.Status.Reason; break; }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        { state = OperationState.Cancelled; reason = "cancelled"; }
        catch (Exception error)
        { state = OperationState.Failed; reason = "runtime-error-" + error.GetType().Name; }
        finally
        {
            GameExperiments.WriteAtomic(report, new { schema_version = 1, mode, state = state.ToString(), reason,
                live_qualified = false, completed_modules = runner.CompletedModules, events });
        }
        return new(state, reason, [report]);
    }
}
