using System.Text;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal enum W101CombatPhase { Unknown, Loading, Planning, Targeting, Resolving, Victory, Defeat }
internal enum W101CombatControlRole { Card, Enemy, Self, Pass }
internal enum W101CombatPlanKind { Card, Pass }

/// <summary>Id is snapshot-local; SubjectId identifies a card instance or combatant.
/// Bounds are normalized to the exact GeometryId's client area, never desktop pixels.</summary>
internal sealed record W101CombatControl(string Id, string SubjectId, W101CombatControlRole Role,
    RectangleF Bounds, bool Enabled, bool? Alive = null);
internal sealed record W101CombatSnapshot(string Id, WizwalkerTarget Target, string? EncounterId,
    int? Round, string GeometryId, long ObservedUtcTicks, W101CombatPhase Phase,
    W101CombatControl[] Controls, string? SelectedCardSubjectId = null,
    bool CleanupConfirmed = false, JsonElement PlannerInput = default);
internal sealed record W101CombatTurnPlan(string SnapshotId, W101CombatPlanKind Kind,
    string ControlSubjectId, string? TargetSubjectId = null);
internal sealed record W101CombatDispatch(string ProposalId, string ActionId, string SnapshotId,
    string GeometryId, W101CombatControl Control);
internal sealed record W101CombatTurnLimits(int TimeoutSeconds = 60, int MaximumReads = 80,
    int PollMilliseconds = 200, int MaximumAgeMilliseconds = 1500);

/// <summary>Trusted application bindings, not user scripts or pack-provided delegates.
/// Read must disconnect before returning and honor cancellation while retaining ownership.
/// Dispatch must check the supplied gate immediately before each input and release it.
/// There is deliberately no default live implementation or implicit capability grant.</summary>
internal sealed record W101CombatTurnRuntime(Func<WizwalkerTarget, bool> IsCurrent,
    Func<uint> Activity, Func<WizwalkerTarget, uint, bool> Ready,
    Func<OperationContext, WizwalkerTarget, int, CancellationToken, Task<W101CombatSnapshot>> Read,
    Func<W101CombatSnapshot, CancellationToken, Task<W101CombatTurnPlan?>> Plan,
    Func<W101CombatDispatch, OperationContext, Func<bool>, CancellationToken, Task<RoutineReceipt>> Dispatch,
    Func<double> Clock, Func<long> UtcTicks, Func<int, CancellationToken, Task> Delay);

/// <summary>One bounded, observed combat turn under the existing scheduler/control owner.
/// The caller must qualify the observation, planner and input bindings independently.
/// A turn result is not proof of damage, a collected item or whole-quest completion.</summary>
internal static class W101CombatTurnOperation
{
    internal const string Id = "w101.combat-turn";
    internal const string Version = "w101-combat-turn-v1";
    internal const string SelectCard = "combat.select-card";
    internal const string SelectTarget = "combat.select-target";
    internal const string Pass = "combat.pass";
    private const int MaximumControls = 32, MaximumPlannerBytes = 65536;

    internal static PreparedDeckJob Prepare(WizwalkerTarget target, bool enableCombatActions,
        W101CombatTurnRuntime runtime, W101CombatTurnLimits? limits = null,
        IReadOnlyDictionary<string, string>? sourcePins = null,
        Func<W101CombatSnapshot, bool>? scope = null)
    {
        if (!enableCombatActions) throw new InvalidOperationException("Combat actions are disabled; enable one qualified turn explicitly.");
        ArgumentNullException.ThrowIfNull(runtime);
        limits ??= new();
        if (limits.TimeoutSeconds is < 1 or > 90 || limits.MaximumReads is < 2 or > 128 ||
            limits.PollMilliseconds is < 100 or > 2000 || limits.MaximumAgeMilliseconds is < 100 or > 2000)
            throw new InvalidDataException("Invalid combat read, timing or freshness limit.");
        if (target.Handle <= 0 || target.ProcessId <= 0 || target.StartedUtcTicks <= 0 ||
            target.ProcessName != "WizardGraphicalClient" || !Path.IsPathFullyQualified(target.ImagePath) ||
            target.ImageSha256 is not { Length: 64 } || !target.ImageSha256.All(Uri.IsHexDigit) || !runtime.IsCurrent(target))
            throw new InvalidOperationException("Select the exact current Wizard101 process instance.");
        var pins = sourcePins?.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase) ?? new(StringComparer.OrdinalIgnoreCase);
        pins[Path.GetFullPath(target.ImagePath)] = target.ImageSha256;
        var request = OperationRequestSnapshot.Freeze(new(Guid.NewGuid().ToString("N"), Id, Version,
            limits.TimeoutSeconds + 5, true, W101QuestStep.TargetBinding(target), pins));
        var frozenLimits = limits;
        return new(request, (context, token) => RunAsync(context, target, frozenLimits, runtime, scope, token));
    }

    private static async Task<OperationOutcome> RunAsync(OperationContext context, WizwalkerTarget target,
        W101CombatTurnLimits limits, W101CombatTurnRuntime runtime, Func<W101CombatSnapshot, bool>? scope, CancellationToken token)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(limits.TimeoutSeconds));
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        token = bounded.Token;
        var events = new List<object>();
        var readIds = new HashSet<string>(StringComparer.Ordinal);
        var report = Path.Combine(context.Directory, "combat-turn.json");
        var started = runtime.Clock();
        var baseline = runtime.Activity();
        var reads = 0;
        long lastObservedTicks = 0;
        var dispatches = 0;
        var planningCalls = 0;
        var complete = false;
        var victoryObserved = false;
        var reason = "combat-turn-budget-exceeded";
        W101CombatSnapshot? opening = null;
        W101CombatTurnPlan? plan = null;
        RoutineRunner? runner = null;
        RoutineReceipt? lastReceipt = null;
        bool finalActionDispatched = false;

        bool Owned() => context.Request.WritesGame && !context.Request.DelegatedInput &&
            context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current);
        bool Ready() => !token.IsCancellationRequested && Owned() && runtime.IsCurrent(target) &&
            runtime.Ready(target, baseline) && double.IsFinite(runtime.Clock()) && runtime.Clock() >= started &&
            runtime.Clock() - started < limits.TimeoutSeconds * 1000;
        void RequireReady()
        {
            token.ThrowIfCancellationRequested();
            if (!Ready()) throw new InvalidOperationException("combat-target-owner-activity-or-deadline-changed");
        }
        async Task<W101CombatSnapshot> ReadAsync()
        {
            RequireReady();
            if (reads >= limits.MaximumReads) throw new InvalidOperationException("combat-read-budget-exceeded");
            W101CombatSnapshot read;
            try { read = await runtime.Read(context, target, reads++, token).ConfigureAwait(false); }
            catch
            {
                context.Input!.RequireRecovery("Combat observer did not return confirmed disconnect evidence.");
                throw;
            }
            if (read is null || !read.CleanupConfirmed)
            {
                context.Input!.RequireRecovery("Combat observer disconnect is unconfirmed.");
                throw new InvalidDataException("combat-observer-cleanup-unconfirmed");
            }
            read = Freeze(read);
            RequireReady();
            Validate(read, target, runtime.UtcTicks(), limits.MaximumAgeMilliseconds);
            if (!readIds.Add(read.Id)) throw new InvalidDataException("combat-observation-replayed");
            if (read.ObservedUtcTicks <= lastObservedTicks) throw new InvalidDataException("combat-observation-not-newer");
            lastObservedTicks = read.ObservedUtcTicks;
            if (opening is not null && read.GeometryId != opening.GeometryId)
                throw new InvalidDataException("combat-geometry-changed");
            if (read.Phase is not (W101CombatPhase.Loading or W101CombatPhase.Unknown) && opening is not null &&
                read.EncounterId != opening.EncounterId) throw new InvalidDataException("combat-encounter-changed");
            if (!(scope?.Invoke(Freeze(read)) ?? true)) throw new InvalidDataException("combat-card-scope-changed");
            var name = $"combat-observation-{reads:D3}.json";
            GameExperiments.WriteAtomic(Path.Combine(context.Directory, name), read);
            events.Add(new { kind = "observation", file = name, read.Id, read.Phase, read.Round });
            return read;
        }
        try
        {
            RequireReady();
            while (reads < limits.MaximumReads && Ready())
            {
                var read = await ReadAsync().ConfigureAwait(false);
                if (read.Phase is W101CombatPhase.Loading or W101CombatPhase.Unknown)
                {
                    await runtime.Delay(limits.PollMilliseconds, token).ConfigureAwait(false);
                    continue;
                }
                if (read.Phase == W101CombatPhase.Defeat) { reason = "combat-defeat-observed"; break; }
                if (opening is null)
                {
                    if (read.Phase != W101CombatPhase.Planning)
                    {
                        await runtime.Delay(limits.PollMilliseconds, token).ConfigureAwait(false);
                        continue;
                    }
                    opening = read;
                    // The planner receives a separate copy; mutation cannot alter admitted evidence.
                    planningCalls++;
                    plan = await runtime.Plan(Freeze(opening), token).ConfigureAwait(false);
                    RequireReady();
                    ValidatePlan(plan, opening);
                    var graph = Compile(plan!, limits);
                    GameExperiments.WriteAtomic(Path.Combine(context.Directory, "combat-plan.json"), plan!);
                    GameExperiments.WriteAtomic(Path.Combine(context.Directory, "combat-routine.json"), graph);
                    runner = new(graph, new(W101QuestStep.TargetBinding(target), opening.GeometryId,
                        opening.EncounterId + ".round." + opening.Round), new HashSet<string> { SelectCard, SelectTarget, Pass }, context.Request.Id);
                }
                bool newRound = read.Phase == W101CombatPhase.Planning && read.Round == opening.Round + 1;
                bool victory = read.Phase == W101CombatPhase.Victory;
                bool verified = finalActionDispatched && lastReceipt is { Accepted: true, InputReleased: true } &&
                    read.ObservedUtcTicks > opening.ObservedUtcTicks && (newRound || victory);
                if (!verified && read.Round != opening.Round) throw new InvalidDataException("combat-round-changed-before-verification");
                var scene = verified ? "TurnVerified" : read.Phase == W101CombatPhase.Targeting &&
                    read.SelectedCardSubjectId == plan!.ControlSubjectId ? "CardSelected" :
                    read.Phase == W101CombatPhase.Planning ? "Ready" : "Unknown";
                var binding = new RoutineBinding(W101QuestStep.TargetBinding(target), opening.GeometryId,
                    opening.EncounterId + ".round." + opening.Round);
                var observedTime = runtime.Clock() - TimeSpan.FromTicks(runtime.UtcTicks() - read.ObservedUtcTicks).TotalMilliseconds;
                var decision = runner!.Observe(new(observedTime, scene, binding, Fresh: true, TargetReady: true,
                    ContextVerified: true, InputOwned: Owned(), ControlVerified: true,
                    OutcomeForProposalId: lastReceipt?.ProposalId));
                events.Add(new { kind = "decision", decision });
                if (decision.Completed)
                {
                    complete = true; victoryObserved = victory; reason = victory ? "battle-victory-observed-after-turn" : "next-round-observed"; break;
                }
                if (decision.Stopped) { reason = decision.Reason; break; }
                if (!decision.IsProposal)
                {
                    await runtime.Delay(limits.PollMilliseconds, token).ConfigureAwait(false);
                    continue;
                }
                // A fresh observation resolves semantic identities to this snapshot's geometry.
                // Never reuse the initial hand index or previous target coordinates.
                var fresh = await ReadAsync().ConfigureAwait(false);
                while (fresh.Phase is W101CombatPhase.Loading or W101CombatPhase.Unknown)
                {
                    await runtime.Delay(limits.PollMilliseconds, token).ConfigureAwait(false);
                    fresh = await ReadAsync().ConfigureAwait(false);
                }
                if (fresh.Round != opening.Round || fresh.EncounterId != opening.EncounterId || fresh.ObservedUtcTicks <= read.ObservedUtcTicks)
                    throw new InvalidDataException("combat-dispatch-context-is-not-newer-and-matching");
                var selectingTarget = decision.ActionId == SelectTarget;
                if (selectingTarget ? fresh.Phase != W101CombatPhase.Targeting || fresh.SelectedCardSubjectId != plan!.ControlSubjectId :
                    fresh.Phase != W101CombatPhase.Planning)
                    throw new InvalidDataException("combat-selection-state-changed");
                if (!selectingTarget)
                {
                    // A matching button alone does not renew an old decision: HP, pips,
                    // effects or metadata may have changed while the control stayed put.
                    planningCalls++;
                    var confirmedPlan = await runtime.Plan(Freeze(fresh), token).ConfigureAwait(false);
                    RequireReady();
                    ValidatePlan(confirmedPlan, fresh);
                    events.Add(new { kind = "plan-revalidation", fresh.Id, confirmedPlan });
                    if (confirmedPlan!.Kind != plan!.Kind || confirmedPlan.ControlSubjectId != plan.ControlSubjectId ||
                        confirmedPlan.TargetSubjectId != plan.TargetSubjectId)
                        throw new InvalidDataException("combat-plan-changed-before-input");
                    GameExperiments.WriteAtomic(Path.Combine(context.Directory, "combat-confirmed-plan.json"), confirmedPlan);
                }
                var subject = selectingTarget ? plan!.TargetSubjectId! : plan!.ControlSubjectId;
                var control = UniqueControl(fresh, subject);
                var expectedRole = selectingTarget ? control.Role == UniqueControl(opening, plan!.TargetSubjectId!).Role :
                    control.Role == (plan.Kind == W101CombatPlanKind.Pass ? W101CombatControlRole.Pass : W101CombatControlRole.Card);
                if (!expectedRole || !control.Enabled) throw new InvalidDataException("combat-control-role-or-availability-changed");
                if (selectingTarget && control.Alive != true) throw new InvalidDataException("combat-target-not-confirmed-alive");
                if (dispatches >= 2) throw new InvalidOperationException("combat-dispatch-budget-exceeded");
                bool Gate() => Ready() && runtime.UtcTicks() >= fresh.ObservedUtcTicks &&
                    runtime.UtcTicks() - fresh.ObservedUtcTicks <= TimeSpan.FromMilliseconds(limits.MaximumAgeMilliseconds).Ticks;
                if (!Gate()) throw new InvalidOperationException("combat-dispatch-evidence-expired");
                var action = new W101CombatDispatch(decision.ProposalId!, decision.ActionId!, fresh.Id, fresh.GeometryId, control);
                dispatches++;
                var dispatchedAt = runtime.Clock();
                RoutineReceipt receipt;
                try { receipt = await runtime.Dispatch(action, context, Gate, token).ConfigureAwait(false); }
                catch
                {
                    context.Input!.RequireRecovery("Combat input did not return a confirmed release receipt.");
                    throw;
                }
                if (receipt is null || !receipt.InputReleased)
                {
                    context.Input!.RequireRecovery("Combat input release is unconfirmed.");
                    throw new InvalidDataException("combat-input-release-unconfirmed");
                }
                events.Add(new { kind = "input-receipt", action, receipt });
                if (!double.IsFinite(receipt.TimeMs) || receipt.TimeMs < dispatchedAt || receipt.TimeMs > runtime.Clock())
                { reason = "combat-receipt-time-invalid"; break; }
                runner.RecordReceipt(receipt);
                if (runner.Status.Stopped || !receipt.Accepted) { reason = "combat-dispatch-unconfirmed"; break; }
                lastReceipt = receipt;
                finalActionDispatched = selectingTarget || plan.TargetSubjectId is null;
            }
        }
        catch (OperationCanceledException) { reason = deadline.IsCancellationRequested ? "combat-turn-deadline-exceeded" : "combat-turn-cancelled"; }
        catch (Exception error) { reason = error.Message.Length <= 400 ? error.Message : error.Message[..400]; }
        var cleanup = context.Input?.RecoveryRequired != true;
        var outcomeUnverified = dispatches > 0 && !complete;
        GameExperiments.WriteAtomic(report, new { schemaVersion = 1, target, limits, openingSnapshotId = opening?.Id,
            plan, reason, reads, planningCalls, dispatches, turnVerified = complete, victoryObserved, questProgressVerified = false,
            outcomeUnverified, cleanupConfirmed = cleanup, elapsedMs = runtime.Clock() - started, events,
            scope = "One observed turn. No damage, loot, collection count or whole-quest completion is inferred." });
        return new(!cleanup ? OperationState.CleanupFailed : deadline.IsCancellationRequested ? OperationState.TimedOut : token.IsCancellationRequested ? OperationState.Cancelled :
            complete ? OperationState.Completed : outcomeUnverified ? OperationState.Unknown : OperationState.Failed,
            reason, [report]);
    }

    private static W101CombatSnapshot Freeze(W101CombatSnapshot source)
    {
        if (source.Controls is null || source.Controls.Length > MaximumControls) throw new InvalidDataException("combat-control-budget-exceeded");
        var payload = source.PlannerInput.ValueKind == JsonValueKind.Undefined ? JsonSerializer.SerializeToElement<object?>(null) : source.PlannerInput.Clone();
        if (Encoding.UTF8.GetByteCount(payload.GetRawText()) > MaximumPlannerBytes) throw new InvalidDataException("combat-planner-payload-budget-exceeded");
        return source with { Controls = source.Controls.ToArray(), PlannerInput = payload };
    }
    private static void Validate(W101CombatSnapshot read, WizwalkerTarget target, long now, int maximumAge)
    {
        static bool Text(string? value) => W101QuestStep.ValidText(value, 128);
        if (read.Target != target) throw new InvalidDataException("combat-target-changed");
        if (!Text(read.Id) || !Text(read.GeometryId) || !Enum.IsDefined(read.Phase) || read.ObservedUtcTicks <= 0 ||
            now < read.ObservedUtcTicks || now - read.ObservedUtcTicks > TimeSpan.FromMilliseconds(maximumAge).Ticks)
            throw new InvalidDataException("combat-observation-invalid-or-stale");
        if (read.Phase is not (W101CombatPhase.Loading or W101CombatPhase.Unknown) &&
            (!Text(read.EncounterId) || read.Round is < 1 or > 10000 || read.Round is null))
            throw new InvalidDataException("combat-encounter-or-round-unknown");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var subjects = new HashSet<string>(StringComparer.Ordinal);
        foreach (var control in read.Controls)
        {
            if (control is null || !Text(control.Id) || !Text(control.SubjectId) || !ids.Add(control.Id) || !subjects.Add(control.SubjectId) ||
                !Enum.IsDefined(control.Role) || !ValidBounds(control.Bounds)) throw new InvalidDataException("combat-control-ambiguous-or-invalid");
        }
        if (read.SelectedCardSubjectId is not null && !Text(read.SelectedCardSubjectId)) throw new InvalidDataException("combat-selected-card-invalid");
    }
    private static bool ValidBounds(RectangleF r) => float.IsFinite(r.X) && float.IsFinite(r.Y) &&
        float.IsFinite(r.Width) && float.IsFinite(r.Height) && r.X >= 0 && r.Y >= 0 && r.Width > 0 && r.Height > 0 &&
        r.Right <= 1 && r.Bottom <= 1;
    private static W101CombatControl UniqueControl(W101CombatSnapshot snapshot, string subject) =>
        snapshot.Controls.SingleOrDefault(c => c.SubjectId == subject) ?? throw new InvalidDataException("combat-selected-control-unavailable");
    private static void ValidatePlan(W101CombatTurnPlan? plan, W101CombatSnapshot snapshot)
    {
        if (plan is null || !Enum.IsDefined(plan.Kind) || plan.SnapshotId != snapshot.Id)
            throw new InvalidDataException("combat-plan-missing-or-wrong-snapshot");
        var control = UniqueControl(snapshot, plan.ControlSubjectId);
        if (!control.Enabled || control.Role != (plan.Kind == W101CombatPlanKind.Pass ? W101CombatControlRole.Pass : W101CombatControlRole.Card))
            throw new InvalidDataException("combat-plan-control-unavailable");
        if (plan.TargetSubjectId is not null)
        {
            var target = UniqueControl(snapshot, plan.TargetSubjectId);
            if (plan.Kind != W101CombatPlanKind.Card || target.Alive != true || target.Role is not (W101CombatControlRole.Enemy or W101CombatControlRole.Self))
                throw new InvalidDataException("combat-plan-target-unavailable");
        }
    }
    private static RoutineGraph Compile(W101CombatTurnPlan plan, W101CombatTurnLimits limits)
    {
        double timeout = limits.TimeoutSeconds * 1000;
        var target = plan.TargetSubjectId is not null;
        var first = new RoutineNode("choose", [new("choose", "Ready", plan.Kind == W101CombatPlanKind.Pass ? Pass : SelectCard,
            [target ? "CardSelected" : "TurnVerified"], target ? "target" : "done", timeout, true, true)]);
        var done = new RoutineNode("done", [], "TurnVerified");
        RoutineNode[] nodes = target ? [first, new("target", [new("target", "CardSelected", SelectTarget,
            ["TurnVerified"], "done", timeout, true, true)]), done] : [first, done];
        return new(1, "combat-one-turn", "wizard101", "choose", timeout, target ? 2 : 1, ["Loading", "Unknown"], nodes);
    }
}
