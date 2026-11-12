using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Trusted reader rejection after independently checked disconnect.
/// It never grants input or converts unavailable evidence into an observation.</summary>
internal sealed class W101UiObservationUnavailableException(string message) : InvalidOperationException(message);

internal sealed record W101UiTreeRequest(int SchemaVersion, string RunId, string Operation, bool Enabled,
    bool NetworkAllowed, WizwalkerTarget Target, string SourceRoot, string SourceSha256, int TimeoutSeconds,
    int DurationSeconds, bool UiTreeEnabled, bool IncludeQuestContext = false, bool IncludeCombatContext = false,
    bool StopAfterTwoTrees = false, bool IncludePoseContext = false);
internal sealed record W101PosePoint(double X, double Y, double Z);
internal sealed record W101PoseCandidate(string LayoutId, string Status, bool LiveQualified, bool MovementAuthorized,
    W101PosePoint? Position, double? YawRadians, long ObservedUtcTicks, int ReadCalls, int ReadBytes);
// Structural candidates are deliberately not the actionable turn snapshot.
// Raw layout rectangles have no qualified conversion to client pixels yet.
internal sealed record W101CombatUiControl(int Index, int? Parent, string Name, string Kind,
    int[]? RawRectangle, bool? Visible, bool? Enabled);
internal sealed record W101CombatUiContext(int SchemaVersion, string Qualification, string GeometrySpace,
    bool? MetadataQualified, bool? CastabilityKnown, bool? PlanningVisible, bool? HandVisible, W101CombatUiControl[] Controls,
    W101CombatFieldCandidates? FieldCandidates = null, W101CombatDuelCandidates? DuelCandidates = null);
internal sealed record W101UiTreeResult(int SchemaVersion, string RunId, string Operation, string SourceSha256,
    WizwalkerTarget Target, string Status, string Code, bool? Loading, Dictionary<string, bool?> States,
    long ObservedUtcTicks, long StatesObservedUtcTicks, int DurationMs, bool CleanupConfirmed, bool HookRestored,
    int LoadingSamples, int TreeSamples, int TreeFailures, Dictionary<string, bool?> StableStates,
    W101QuestContext? QuestContext = null, W101QuestContext? StableQuestContext = null, W101CombatUiContext? CombatContext = null,
    string? InteractionTitle = null, string? StableInteractionTitle = null, W101PoseCandidate? PoseCandidate = null);
internal sealed record W101UiTreeRuntime(Func<WizwalkerTarget, bool> IsCurrent,
    Func<WizwalkerLaunch, W101UiTreeRequest, OperationContext, CancellationToken, Task<W101UiTreeResult>> Execute);

/// <summary>Explicit observation only. One optional scoped root-window hook under
/// the existing control owner. States are candidates until independently qualified;
/// no action, focus, model, network or arbitrary memory command is exposed.</summary>
internal static class W101UiTreeOperation
{
    internal static bool SupportsClientImage(string imageSha256) =>
        string.Equals(imageSha256, "544ab726ef7b920a5ccb67aa3d06b245db7e0012317d2fe7856d58c17c31cf7e", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(imageSha256, "d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a", StringComparison.OrdinalIgnoreCase);
    internal const string Id = "w101.ui-tree-observe";
    internal const string Version = "w101-ui-tree-v1";
    internal const int MaximumResultBytes = 262144;
    internal const string WorkerSha256 = "d23dd3cb2c16a1c7d0191fe9e3a939edd87c081e816911fc4893bd55b6a528c1";
    internal const string PoseReaderSha256 = "a6c9957f9aa209f6a14f1c24df0ca81e6035e7d400037416427f1ccc45baab77";
    private static readonly string[] StateNames = ["interactionPrompt", "dialogue", "menu", "nativeIdle"];
    private static readonly W101UiTreeRuntime Runtime = new(target => target.IsCurrent(), (launch, request, context, token) =>
        WizwalkerWorker.ExecuteJsonAsync<W101UiTreeResult>(launch, request, context, token, request.DurationSeconds + 18, MaximumResultBytes));

    internal static OperationTicket Submit(OperationScheduler scheduler, WizwalkerSession session, WizwalkerLaunch launch,
        bool uiTreeEnabled, int durationSeconds = 15, W101UiTreeRuntime? runtime = null, bool includeQuestContext = false,
        bool includeCombatContext = false, bool includePoseContext = false)
    {
        if (!uiTreeEnabled) throw new InvalidOperationException("Enable the optional UI-tree reader for this check first.");
        if (durationSeconds is < 1 or > 60) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        WizwalkerWorker.ValidateLimits(durationSeconds + 18, MaximumResultBytes);
        var helper = Path.Combine(Path.GetDirectoryName(launch.Worker)!, "wizwalker_adapter.py");
        if (runtime is null && !GameExperiments.Digest(helper).Equals(WizwalkerOperation.WorkerSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The reviewed UI observation helper changed.");
        var validation = runtime is null ? null : new WizwalkerRuntime(runtime.IsCurrent, (_, _, _, _) => throw new InvalidOperationException("Validation only."));
        var request = WizwalkerOperation.Prepare(session, launch, validation, Id, Version, WorkerSha256);
        var pins = request.Pins.ToDictionary(pair => pair.Key, pair => pair.Value);
        pins[Path.GetFullPath(helper)] = GameExperiments.Digest(helper);
        if (includePoseContext)
        {
            foreach (var (name, expected) in new[] { ("w101_pose_reader.py", PoseReaderSha256), ("w101_combat_reader.py", W101CombatRuntime.FieldReaderSha256) })
            {
                var path = Path.Combine(Path.GetDirectoryName(launch.Worker)!, name);
                if (!GameExperiments.Digest(path).Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Reviewed pose dependency changed.");
                pins[Path.GetFullPath(path)] = expected;
            }
        }
        if (includeCombatContext)
        {
            var fields = Path.Combine(Path.GetDirectoryName(launch.Worker)!, "w101_combat_reader.py");
            if (runtime is null && !GameExperiments.Digest(fields).Equals(W101CombatRuntime.FieldReaderSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The reviewed combat field reader changed.");
            pins[Path.GetFullPath(fields)] = GameExperiments.Digest(fields);
            var duel = Path.Combine(Path.GetDirectoryName(launch.Worker)!, "w101_duel_reader.py");
            if (runtime is null && !GameExperiments.Digest(duel).Equals(W101CombatRuntime.DuelReaderSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The reviewed duel reader changed.");
            pins[Path.GetFullPath(duel)] = GameExperiments.Digest(duel);
        }
        request = request with { TimeoutSeconds = durationSeconds + 30, Pins = pins };
        return scheduler.Submit(request, (context, token) => RunAsync(context, session.Target, launch, durationSeconds, includeQuestContext, runtime ?? Runtime, token, includeCombatContext, includePoseContext: includePoseContext));
    }

    internal static async Task<OperationOutcome> RunAsync(OperationContext context, WizwalkerTarget target, WizwalkerLaunch launch,
        int durationSeconds, bool includeQuestContext, W101UiTreeRuntime runtime, CancellationToken token, bool includeCombatContext = false,
        bool stopAfterTwoTrees = false, bool includePoseContext = false)
    {
        token.ThrowIfCancellationRequested();
        if (context.Input is not { CanDispatch: true } || !ReferenceEquals(context.Input, GameInputLease.Current) || !runtime.IsCurrent(target))
            throw new InvalidOperationException("The selected client or control ownership changed.");
        var request = new W101UiTreeRequest(1, context.Request.Id, Version, true, false, target,
            launch.SourceRoot, WizwalkerOperation.SourceSha256, 8, durationSeconds, true, includeQuestContext, includeCombatContext, stopAfterTwoTrees, includePoseContext);
        var requestPath = Path.Combine(context.Directory, "ui-request.json");
        GameExperiments.WriteAtomic(requestPath, request);
        W101UiTreeResult result;
        try { result = await runtime.Execute(launch, request, context, token).ConfigureAwait(false); }
        catch
        {
            context.Input.RequireRecovery("UI observer ended without verified hook restoration and disconnect.");
            throw;
        }
        if (!result.CleanupConfirmed || result.RunId != request.RunId || result.Target != target ||
            result.SchemaVersion != 1 || result.Operation != Version || result.SourceSha256 != WizwalkerOperation.SourceSha256 ||
            result.Status is not ("observed" or "unavailable" or "cancelled") || result.Status == "observed" && !result.HookRestored)
        {
            context.Input.RequireRecovery("UI observation restoration or result identity is unconfirmed.");
            return new(OperationState.CleanupFailed, "UI hook restoration or disconnect needs review; gameplay remains blocked.", [requestPath]);
        }
        var resultPath = Path.Combine(context.Directory, "ui-result.json");
        GameExperiments.WriteAtomic(resultPath, result);
        var trace = Path.Combine(context.Directory, "ui-observations.jsonl");
        string[] artifacts = File.Exists(trace) ? [requestPath, resultPath, trace] : [requestPath, resultPath];
        if (result.Status == "cancelled") return new(OperationState.Cancelled, "UI observation stopped; cleanup confirmed. No game inputs sent.", artifacts);
        if (result.Status != "observed")
        {
            var message = result.Code is "root-signature-unavailable" or "root-instruction-changed" or "scratch-signature-unavailable" or "client-build-unreviewed"
                ? "This W101 client does not match the reviewed UI reader. Hook setup was withheld; disconnected cleanly. Visual observation remains available."
                : "No complete UI tree was obtained; cleanup confirmed. Visual observation remains available.";
            return new(OperationState.Failed, message, artifacts);
        }
        var now = DateTime.UtcNow.Ticks;
        bool ValidStates(Dictionary<string, bool?>? values) => values is { Count: 4 } && StateNames.All(values.ContainsKey);
        if (!runtime.IsCurrent(target) || !ValidStates(result.States) || !ValidStates(result.StableStates) ||
            result.TreeSamples is < 1 or > 120 || result.LoadingSamples is < 0 or > 1000 || result.TreeFailures is < 0 or > 120 ||
            result.DurationMs < 0 || result.DurationMs > (durationSeconds + 20) * 1000 ||
            result.ObservedUtcTicks <= 0 || result.ObservedUtcTicks > now || now - result.ObservedUtcTicks > TimeSpan.FromSeconds(5).Ticks ||
            result.StatesObservedUtcTicks > now || result.StatesObservedUtcTicks > result.ObservedUtcTicks ||
            result.States.Values.Any(value => value is not null) && (result.StatesObservedUtcTicks <= 0 || now - result.StatesObservedUtcTicks > TimeSpan.FromSeconds(5).Ticks) ||
            result.StableStates.Any(pair => pair.Value is not null && result.States[pair.Key] != pair.Value) ||
            !ValidQuestContext(result, includeQuestContext, now) || !ValidCombatContext(result, includeCombatContext, now) ||
            !ValidInteractionTitle(result, now) || !ValidPose(result.PoseCandidate, includePoseContext, now))
            return new(OperationState.Failed, "UI state evidence is stale, inconsistent or unavailable; no action is authorized.", artifacts);
        if (includeCombatContext)
        {
            var readiness = Path.Combine(context.Directory, "combat-readiness.json");
            GameExperiments.WriteAtomic(readiness, W101CombatRuntime.Assess(result, now));
            artifacts = [.. artifacts, readiness];
        }
        return new(OperationState.Completed,
            $"Observed {result.TreeSamples} UI trees and {result.LoadingSamples} loading samples; root hook restored and disconnected. Candidate states only; no game inputs sent.", artifacts);
    }

    internal static bool ValidPose(W101PoseCandidate? pose, bool included, long now)
    {
        if (pose is null) return true;
        if (!included || pose.LiveQualified || pose.MovementAuthorized || pose.LayoutId != "archived-wizwalker-actor-body" ||
            pose.ObservedUtcTicks <= 0 || pose.ObservedUtcTicks > now || now - pose.ObservedUtcTicks > TimeSpan.FromSeconds(5).Ticks ||
            pose.ReadCalls is < 0 or > 6 || pose.ReadBytes is < 0 or > 72) return false;
        if (pose.Status == "unknown") return pose.Position is null && pose.YawRadians is null;
        return pose.Status == "candidate" && pose.ReadCalls == 6 && pose.ReadBytes == 72 && pose.Position is { } point &&
            new[] { point.X, point.Y, point.Z }.All(value => double.IsFinite(value) && Math.Abs(value) <= 10000000) &&
            pose.YawRadians is { } yaw && double.IsFinite(yaw) && Math.Abs(yaw) <= 2 * Math.PI + .01;
    }

    /// <summary>Bounded serial observation for a trusted parent operation, reusing
    /// its current lease. The parent must admit the same worker/helper/image pins.</summary>
    internal static Task<W101UiTreeResult> ReadOwnedAsync(OperationContext parent, WizwalkerTarget target,
        WizwalkerLaunch launch, int index, CancellationToken token) => ReadOwnedAsync(parent, target, launch, index, token, false);

    internal static async Task<W101UiTreeResult> ReadOwnedAsync(OperationContext parent, WizwalkerTarget target,
        WizwalkerLaunch launch, int index, CancellationToken token, bool includePoseContext)
    {
        if (index is < 0 or > 31) throw new InvalidDataException("Quest observation budget exceeded.");
        if (!parent.Request.WritesGame || parent.Request.DelegatedInput)
            throw new InvalidOperationException("Owned UI reads require the existing exclusive scheduler admission.");
        foreach (var path in new[] { launch.Worker, Path.Combine(Path.GetDirectoryName(launch.Worker)!, "wizwalker_adapter.py"), target.ImagePath })
            if (!parent.Request.Pins.TryGetValue(Path.GetFullPath(path), out var digest) ||
                !GameExperiments.Digest(path).Equals(digest, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Parent UI observer pins changed or are missing.");
        if (includePoseContext)
            foreach (var name in new[] { "w101_pose_reader.py", "w101_combat_reader.py" })
            {
                var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(launch.Worker)!, name));
                if (!parent.Request.Pins.TryGetValue(path, out var pin) || GameExperiments.Digest(path) != pin)
                    throw new InvalidDataException("Parent pose observer pins changed or are missing.");
            }
        var directory = Path.Combine(parent.Directory, "read-" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture));
        if (Directory.Exists(directory)) throw new IOException("Observation folder already exists.");
        Directory.CreateDirectory(directory);
        var outcome = await RunAsync(parent with { Directory = directory }, target, launch, 2, true, Runtime, token,
            stopAfterTwoTrees: true, includePoseContext: includePoseContext).ConfigureAwait(false);
        if (outcome.State != OperationState.Completed)
        {
            var resultPath = Path.Combine(directory, "ui-result.json");
            if (outcome.State == OperationState.Failed && File.Exists(resultPath))
            {
                var rejected = JsonSerializer.Deserialize<W101UiTreeResult>(GameExperiments.BoundedBytes(resultPath, MaximumResultBytes), WizwalkerOperation.Json);
                if (rejected is { Status: "unavailable", CleanupConfirmed: true, HookRestored: true } &&
                    rejected.Target == target && rejected.RunId == parent.Request.Id && rejected.SchemaVersion == 1 &&
                    rejected.Operation == Version && rejected.SourceSha256 == WizwalkerOperation.SourceSha256)
                    throw new W101UiObservationUnavailableException(outcome.Message);
            }
            throw new InvalidOperationException(outcome.Message);
        }
        return JsonSerializer.Deserialize<W101UiTreeResult>(GameExperiments.BoundedBytes(Path.Combine(directory, "ui-result.json"), MaximumResultBytes), WizwalkerOperation.Json)
            ?? throw new InvalidDataException("Missing UI observation.");
    }

    /// <summary>Bounded source-backed prompt evidence, not permission to press X.
    /// An absent title remains unknown; a stable title must agree with this reading.</summary>
    internal static bool ValidInteractionTitle(W101UiTreeResult result, long now)
    {
        if (result.InteractionTitle is null) return result.StableInteractionTitle is null;
        return W101QuestStep.ValidText(result.InteractionTitle, 160, optional: false) &&
            (result.StableInteractionTitle is null || result.StableInteractionTitle == result.InteractionTitle &&
                result.TreeSamples >= 2 && result.StableStates.GetValueOrDefault("interactionPrompt") == true) &&
            result.States.GetValueOrDefault("interactionPrompt") == true && result.Loading == false &&
            result.StatesObservedUtcTicks > 0 && result.StatesObservedUtcTicks <= result.ObservedUtcTicks &&
            result.ObservedUtcTicks <= now && now - result.StatesObservedUtcTicks <= TimeSpan.FromSeconds(2).Ticks;
    }

    internal static bool ValidQuestContext(W101UiTreeResult result, bool included, long now)
    {
        if (result.QuestContext is null) return result.StableQuestContext is null;
        return included && W101QuestStep.ValidText(result.QuestContext.ObjectiveText, 256, optional: true) &&
            W101QuestStep.ValidText(result.QuestContext.DialogueText, 2048, optional: true) &&
            (result.StableQuestContext is null || result.StableQuestContext == result.QuestContext) &&
            result.Loading == false && result.StatesObservedUtcTicks > 0 && result.StatesObservedUtcTicks <= now &&
            result.StatesObservedUtcTicks <= result.ObservedUtcTicks && now - result.StatesObservedUtcTicks <= TimeSpan.FromSeconds(5).Ticks;
    }

    internal static bool ValidCombatContext(W101UiTreeResult result, bool included, long now)
    {
        var combat = result.CombatContext;
        if (combat is null) return true;
        if (!included || combat.SchemaVersion != 1 || combat.Qualification != "structural-unqualified" ||
            combat.GeometrySpace != "raw-layout-unqualified" || combat.MetadataQualified != false || combat.CastabilityKnown != false ||
            combat.PlanningVisible is null || combat.HandVisible is null || combat.HandVisible == true && combat.PlanningVisible != true ||
            combat.Controls is null || combat.Controls.Length > 64 || result.Loading != false ||
            result.StatesObservedUtcTicks <= 0 || result.StatesObservedUtcTicks > now ||
            result.StatesObservedUtcTicks > result.ObservedUtcTicks || now - result.StatesObservedUtcTicks > TimeSpan.FromSeconds(5).Ticks)
            return false;
        var ids = new HashSet<int>();
        foreach (var control in combat.Controls)
        {
            if (control is null || control.Index is < 0 or >= 384 || !ids.Add(control.Index) ||
                control.Parent is < 0 or >= 384 || control.Parent == control.Index || control.Name is null ||
                control.Name.Length > 128 || control.Name.Any(char.IsControl) || control.Visible != true || control.Enabled is null ||
                control.Kind is not ("planning-container" or "hand-container" or "hand-descendant" or "combatant-container" or
                    "combatant-label" or "done-container" or "button-candidate")) return false;
            if (control.RawRectangle is { } rect && (rect.Length != 4 || rect.Any(v => v is < -65536 or > 65536) ||
                rect[0] >= rect[2] || rect[1] >= rect[3])) return false;
        }
        return W101CombatRuntime.ValidCandidates(combat) &&
            combat.Controls.Count(c => c.Kind == "planning-container") == (combat.PlanningVisible == true ? 1 : 0) &&
            combat.Controls.Count(c => c.Kind == "hand-container") == (combat.HandVisible == true ? 1 : 0);
    }
}

