using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Connects module work to the existing sequential scheduler. Saved-screen
/// review has no input transport. The owned entry is trusted application code only;
/// loading a bundle cannot choose it or supply its callback.</summary>
internal static class ModuleOperation
{
    public const string OfflineId = "module.screen-review";
    public const string OwnedId = "module.owned-operation";
    public const string Version = "module-operation-v1";

    public static OperationTicket SubmitOffline(OperationScheduler scheduler, ModuleScreenReviewSnapshot snapshot,
        int timeoutSeconds = 60)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        var job = PrepareOffline(snapshot, timeoutSeconds);
        return scheduler.Submit(job.Request, job.Execute);
    }

    internal static PreparedDeckJob PrepareOffline(ModuleScreenReviewSnapshot snapshot, int timeoutSeconds = 60)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        CheckTimeout(timeoutSeconds);
        OperationModules.CheckId(snapshot.SessionId);
        var request = new OperationRequest(Guid.NewGuid().ToString("N"), OfflineId, Version, timeoutSeconds,
            false, "saved-screen-session:" + snapshot.SessionId,
            new Dictionary<string, string>(snapshot.Pins, StringComparer.OrdinalIgnoreCase), DelegatedInput: false);
        return new PreparedDeckJob(request, async (context, token) =>
        {
            if (context.Input != null || GameInputLease.Current != null || context.Request.WritesGame || context.Request.DelegatedInput)
                throw new InvalidOperationException("Offline module review must not have desktop input ownership.");
            var admission = Path.Combine(context.Directory, "module-operation.json");
            var report = Path.Combine(context.Directory, "module-screen-review.json");
            GameExperiments.WriteAtomic(admission, new
            {
                schema_version = 1, operation_id = context.Request.Id, session_id = snapshot.SessionId,
                mode = "offline-screen-review", desktop_dispatch_count = 0, live_qualified = false,
                input_lease_acquired = false, source_pins = context.Request.Pins
            });
            try
            {
                token.ThrowIfCancellationRequested();
                var result = await ModuleScreenReview.RunAsync(snapshot, report, token).ConfigureAwait(false);
                if (result.Completed && result.Stopped || result.Proposals < 0 || result.Receipts < 0 || result.Frames < 0 ||
                    !string.Equals(Path.GetFullPath(result.Report), report, StringComparison.OrdinalIgnoreCase) || !File.Exists(report))
                    throw new InvalidDataException("Module review returned an inconsistent result or missing report.");
                var state = result.Completed ? OperationState.Completed : result.Stopped ? OperationState.Failed : OperationState.Unknown;
                var message = result.Completed ? "Offline module contract completed; no game inputs sent." :
                    result.Stopped ? "Offline module review stopped: " + result.Reason : "Offline module review remains incomplete: " + result.Reason;
                return new(state, message, [admission, report]);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // The reviewer writes its partial report before propagating cancellation.
                // The scheduler retains the distinct cancellation/deadline terminal state.
                return new(OperationState.Cancelled, "Offline module review cancelled; no game inputs sent.",
                    File.Exists(report) ? [admission, report] : [admission]);
            }
        });
    }

    /// <summary>Admission for a trusted, separately qualified application adapter.
    /// This method provides the shared lease and journals the admitted plan; the
    /// callback must still enforce its frozen action bindings and every input guard.
    /// No UI, CLI, pack field, plugin or offline reviewer selects this entry.</summary>
    internal static OperationTicket SubmitOwned(OperationScheduler scheduler, OperationModuleBundle bundle,
        RoutineBinding binding, Func<OperationContext, CancellationToken, Task<OperationOutcome>> execute,
        int timeoutSeconds = 60)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        var job = PrepareOwned(bundle, binding, execute, timeoutSeconds);
        return scheduler.Submit(job.Request, job.Execute);
    }

    internal static PreparedDeckJob PrepareOwned(OperationModuleBundle bundle, RoutineBinding binding,
        Func<OperationContext, CancellationToken, Task<OperationOutcome>> execute, int timeoutSeconds = 60)
    {
        ArgumentNullException.ThrowIfNull(execute); CheckTimeout(timeoutSeconds);
        ModuleCatalog.Compile(bundle);
        var frozenJson = JsonSerializer.Serialize(bundle, OperationModules.Json);
        var frozen = ModuleCatalog.ReadBundle(frozenJson);
        _ = new ModuleRunner(frozen, binding);
        var request = new OperationRequest(Guid.NewGuid().ToString("N"), OwnedId, Version, timeoutSeconds,
            true, binding.TargetId, new Dictionary<string, string>(), DelegatedInput: false);
        return new PreparedDeckJob(request, async (context, token) =>
        {
            token.ThrowIfCancellationRequested();
            if (!context.Request.WritesGame || context.Request.DelegatedInput || context.Input is not { IsHeld: true } lease ||
                !ReferenceEquals(lease, GameInputLease.Current))
                throw new InvalidOperationException("Owned module work requires the exact scheduler-held input lease.");
            var plan = Path.Combine(context.Directory, "admitted-bundle.json");
            var admission = Path.Combine(context.Directory, "module-operation.json");
            using (var output = new FileStream(plan, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(output)) writer.Write(frozenJson);
            GameExperiments.WriteAtomic(admission, new
            {
                schema_version = 1, operation_id = context.Request.Id, mode = "trusted-owned-module-operation",
                bundle_id = frozen.Id, game_id = frozen.GameId, binding,
                bundle_sha256 = GameExperiments.Digest(plan), input_lease_acquired = true,
                live_qualified = false, qualification = "Admission alone does not qualify a game or its outcomes."
            });
            try
            {
                var outcome = await execute(context, token).ConfigureAwait(false);
                return outcome with { Artifacts = new[] { admission, plan }.Concat(outcome.Artifacts).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() };
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return new(OperationState.Cancelled, "Owned module work cancelled after its worker exited; inspect adapter evidence for outcomes.", [admission, plan]);
            }
        });
    }
    private static void CheckTimeout(int seconds)
    {
        if (seconds is < 1 or > 300) throw new InvalidDataException("Module operation timeout must be 1–300 seconds.");
    }
}

