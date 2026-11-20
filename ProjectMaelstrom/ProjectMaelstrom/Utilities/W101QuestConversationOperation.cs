using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectMaelstrom.Utilities;

internal sealed record W101QuestConversationRuntime(Func<WizwalkerTarget, bool> IsCurrent, Func<uint> Activity,
    Func<WizwalkerTarget, uint, bool> Ready,
    Func<OperationContext, WizwalkerTarget, WizwalkerLaunch, int, CancellationToken, Task<W101UiTreeResult>> Read,
    Func<WizwalkerTarget, bool, bool> Send, Func<double> Clock, Func<long> UtcTicks);

/// <summary>One selected conversation through the existing scheduler/control owner.
/// All observations disconnect before an addressed Space pulse. No walking,
/// battle, purchase, generic command execution, repeat-quest or Idle cleanup.</summary>
internal static class W101QuestConversationOperation
{
    internal const string Id = "w101.quest-conversation";
    internal const string Version = "w101-quest-conversation-v1";
    private static readonly W101QuestConversationRuntime Runtime = new(t => t.IsCurrent(), W101DialogueInput.Activity,
        W101DialogueInput.Ready, W101UiTreeOperation.ReadOwnedAsync, W101DialogueInput.Send,
        FrameTimeline.TimestampMs, () => DateTime.UtcNow.Ticks);

    internal static W101QuestConversationSelection ReadSelection(string path) => ReadSelection(GameExperiments.BoundedBytes(path, 8192));
    private static W101QuestConversationSelection ReadSelection(byte[] bytes) =>
        JsonSerializer.Deserialize<W101QuestConversationSelection>(bytes,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
        ?? throw new InvalidDataException("Select one conversation.");

    internal static OperationTicket Submit(OperationScheduler scheduler, WizwalkerSession session, WizwalkerLaunch launch,
        string selectionPath, bool enableQuestActions, W101QuestConversationRuntime? runtime = null)
    {
        var prepared = Prepare(session, launch, selectionPath, enableQuestActions, runtime);
        return scheduler.Submit(prepared.Request, prepared.Execute);
    }

    internal static PreparedDeckJob Prepare(WizwalkerSession session, WizwalkerLaunch launch,
        string selectionPath, bool enableQuestActions, W101QuestConversationRuntime? runtime = null,
        Func<W101UiTreeResult, bool>? scope = null, int? maximumPages = null)
    {
        if (!enableQuestActions) throw new InvalidOperationException("Quest progression requires an explicit selected conversation and --enable-quest-actions.");
        var selectedBytes = GameExperiments.BoundedBytes(selectionPath, 8192);
        var selected = ReadSelection(selectedBytes);
        _ = W101QuestConversation.Compile(selected);
        if (maximumPages is < 1 or > 16) throw new InvalidDataException("The card's conversation page limit must be 1–16.");
        if (maximumPages.HasValue) selected = selected with { MaxPages = Math.Min(selected.MaxPages, maximumPages.Value) };
        var bundle = W101QuestConversation.Compile(selected);
        var helper = Path.Combine(Path.GetDirectoryName(launch.Worker)!, "wizwalker_adapter.py");
        if (runtime is null && !GameExperiments.Digest(helper).Equals(WizwalkerOperation.WorkerSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Reviewed quest observer helper changed.");
        var validation = runtime is null ? null : new WizwalkerRuntime(runtime.IsCurrent, (_, _, _, _) => throw new InvalidOperationException());
        var request = WizwalkerOperation.Prepare(session, launch, validation, Id, Version, W101UiTreeOperation.WorkerSha256);
        var pins = request.Pins.ToDictionary(p => p.Key, p => p.Value);
        pins[Path.GetFullPath(selectionPath)] = RoutineReplay.Digest(selectedBytes);
        pins[Path.GetFullPath(helper)] = GameExperiments.Digest(helper);
        request = request with { TimeoutSeconds = 90, Pins = pins };
        var body = new PreparedDeckJob(request, (context, token) => RunAsync(context, session.Target, launch, selected, bundle, runtime ?? Runtime, token, scope));
        // Both CLI submission and deck preparation use this production boundary.
        // Injected fixtures test the conversation body independently.
        return runtime is null ? W101RoutineIdle.Wrap(body, session.Target, launch) : body;
    }

    private static async Task<OperationOutcome> RunAsync(OperationContext context, WizwalkerTarget target, WizwalkerLaunch launch,
        W101QuestConversationSelection selected, OperationModuleBundle bundle, W101QuestConversationRuntime runtime, CancellationToken token,
        Func<W101UiTreeResult, bool>? scope = null)
    {
        var binding = bundle.Bind(W101QuestStep.TargetBinding(target), "qualified-ui-tree", context.Request.Id);
        var conversation = new W101QuestConversation(selected, target, binding, context.Request.Id);
        var report = Path.Combine(context.Directory, "quest-conversation.json");
        GameExperiments.WriteAtomic(Path.Combine(context.Directory, "selected-conversation.json"), selected);
        GameExperiments.WriteAtomic(Path.Combine(context.Directory, "admitted-bundle.json"), bundle);
        var events = new List<object>();
        var reason = "conversation-budget-exceeded";
        bool complete = false, released = true;
        uint baseline = runtime.Activity();
        var started = runtime.Clock();
        bool Ready() => !token.IsCancellationRequested && context.Input is { CanDispatch: true } &&
            ReferenceEquals(context.Input, GameInputLease.Current) && runtime.IsCurrent(target) && runtime.Ready(target, baseline);
        try
        {
            for (int index = 0; index < 24 && runtime.Clock() - started < W101QuestConversation.TimeoutMs; index++)
            {
                if (!Ready()) { reason = "user-activity-or-target-change"; break; }
                // ReadOwnedAsync restores/verifies its hook and disconnects before returning.
                var reading = await runtime.Read(context, target, launch, index, token).ConfigureAwait(false);
                if (!Ready()) { reason = "user-activity-or-target-change"; break; }
                if (runtime.Clock() - started >= W101QuestConversation.TimeoutMs) { reason = "conversation-budget-exceeded"; break; }
                var now = runtime.UtcTicks();
                var decision = conversation.Observe(reading, now,
                    new(runtime.Clock(), "Unknown", binding, Fresh: true, TargetReady: true, ContextVerified: true,
                        InputOwned: true, ControlVerified: true));
                events.Add(new { kind = "observation", reading.StatesObservedUtcTicks, reading.QuestContext, decision });
                if (decision.Completed) { complete = true; reason = "conversation-ended-and-objective-changed"; break; }
                if (decision.Stopped) { reason = decision.Reason; break; }
                if (!decision.IsProposal) continue;
                bool FreshReady() => Ready() && W101QuestStep.EvidenceReady(target, reading, runtime.UtcTicks()) && (scope?.Invoke(reading) ?? true);
                var previouslyReleased = released;
                released = false;
                var receipt = await W101DialogueInput.DispatchAsync(decision, context, FreshReady,
                    release => runtime.Send(target, release), runtime.Clock, token).ConfigureAwait(false);
                released = previouslyReleased && receipt.InputReleased;
                conversation.RecordReceipt(receipt);
                events.Add(new { kind = "input-receipt", receipt });
                if (!receipt.Accepted || !receipt.InputReleased) { reason = "dialogue-input-unconfirmed"; break; }
            }
        }
        catch (OperationCanceledException) { reason = "cancelled"; }
        catch (Exception error) { reason = "conversation-error: " + error.Message; }
        var cleanup = released && context.Input?.RecoveryRequired != true;
        GameExperiments.WriteAtomic(report, new { schemaVersion = 1, selected, binding, complete, reason,
            objectiveBefore = selected.ObjectiveBefore, objectiveAfter = conversation.ObjectiveAfter,
            actions = conversation.Actions, cleanupConfirmed = cleanup, inputReleased = released,
            elapsedMs = runtime.Clock() - started, events, idleCleanupAttempted = false,
            scope = "One selected conversation; objective transition is not whole-quest or chain completion." });
        return new(!cleanup ? OperationState.CleanupFailed : token.IsCancellationRequested ? OperationState.Cancelled : complete ? OperationState.Completed : OperationState.Failed,
            complete ? "Selected conversation ended and a new objective was observed. Stopped before further gameplay." : reason,
            [report, Path.Combine(context.Directory, "selected-conversation.json"), Path.Combine(context.Directory, "admitted-bundle.json")]);
    }
}
