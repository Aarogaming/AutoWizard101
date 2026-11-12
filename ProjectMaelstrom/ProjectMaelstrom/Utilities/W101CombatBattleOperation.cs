using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Budgets cover one encounter, including all fresh reads and turn decisions.
/// TurnLimits supplies the existing one-turn adapter's smaller local budgets.</summary>
internal sealed record W101CombatBattleLimits(int MaximumTurns = 6, int TimeoutSeconds = 180,
    int MaximumReads = 256, W101CombatTurnLimits? TurnLimits = null);

/// <summary>Composes the qualified one-turn adapter under one admitted scheduler job and
/// input lease. This never joins another battle, navigates, retries an uncertain input,
/// or infers loot/quest progress. Runtime callbacks remain trusted application bindings.</summary>
internal static class W101CombatBattleOperation
{
    internal const string Id = "w101.combat-battle";
    internal const string Version = "w101-combat-battle-v1";

    internal static PreparedDeckJob Prepare(WizwalkerTarget target, bool enableCombatActions,
        W101CombatTurnRuntime runtime, W101CombatBattleLimits? limits = null,
        IReadOnlyDictionary<string, string>? sourcePins = null,
        Func<W101CombatSnapshot, bool>? scope = null)
    {
        limits ??= new();
        if (limits.MaximumTurns is < 1 or > 12 || limits.TimeoutSeconds is < 1 or > 600 ||
            limits.MaximumReads is < 2 or > 1024)
            throw new InvalidDataException("Invalid battle turn, time or read limit.");
        var turnLimits = limits.TurnLimits ?? new();
        // Reuse admission validation and pin freezing. No child job is submitted.
        var turn = W101CombatTurnOperation.Prepare(target, enableCombatActions, runtime, turnLimits, sourcePins, scope);
        var request = OperationRequestSnapshot.Freeze(turn.Request with
        {
            Operation = Id,
            Version = Version,
            TimeoutSeconds = limits.TimeoutSeconds + 5
        });
        var frozenLimits = limits with { TurnLimits = turnLimits };
        return new(request, (context, token) => RunAsync(context, target, runtime, frozenLimits, scope, token));
    }

    private sealed record Observed(string Id, string? EncounterId, int? Round, string GeometryId,
        long ObservedUtcTicks, W101CombatPhase Phase);
    private sealed record TurnResult(int Number, string ExecutionId, string Report, OperationState State,
        string Reason, int Reads, int Dispatches, bool TurnVerified, bool VictoryObserved,
        bool CleanupConfirmed, Observed? FinalObservation);

    private static async Task<OperationOutcome> RunAsync(OperationContext context, WizwalkerTarget target,
        W101CombatTurnRuntime runtime, W101CombatBattleLimits limits, Func<W101CombatSnapshot, bool>? scope,
        CancellationToken cancellation)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(limits.TimeoutSeconds));
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellation, deadline.Token);
        var token = bounded.Token;
        var started = runtime.Clock();
        var baseline = runtime.Activity();
        var totalReads = 0;
        var totalDispatches = 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        long lastObservedTicks = 0;
        string? encounter = null, geometry = null;
        int? expectedRound = null;
        var turns = new List<TurnResult>();
        var reason = "combat-battle-turn-budget-exceeded";
        var state = OperationState.Failed;
        var victoryObserved = false;
        var report = Path.Combine(context.Directory, "combat-battle.json");

        bool Ready(WizwalkerTarget current, uint _) => !token.IsCancellationRequested &&
            context.Request.WritesGame && !context.Request.DelegatedInput &&
            context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current) &&
            current == target && runtime.IsCurrent(target) && runtime.Activity() == baseline && runtime.Ready(target, baseline) &&
            double.IsFinite(started) && double.IsFinite(runtime.Clock()) && runtime.Clock() >= started &&
            runtime.Clock() - started < limits.TimeoutSeconds * 1000;

        try
        {
            for (var number = 1; number <= limits.MaximumTurns; number++)
            {
                token.ThrowIfCancellationRequested();
                if (!Ready(target, baseline)) throw new InvalidOperationException("combat-battle-target-owner-activity-or-deadline-changed");
                if (limits.MaximumReads - totalReads < 2) { reason = "combat-battle-read-budget-exceeded"; break; }
                var remainingSeconds = Math.Max(1, (int)Math.Ceiling(limits.TimeoutSeconds - (runtime.Clock() - started) / 1000));
                var localLimits = limits.TurnLimits! with
                {
                    TimeoutSeconds = Math.Min(limits.TurnLimits!.TimeoutSeconds, remainingSeconds),
                    MaximumReads = Math.Min(limits.TurnLimits.MaximumReads, limits.MaximumReads - totalReads)
                };
                var readsBefore = totalReads;
                var dispatchesBefore = totalDispatches;
                Observed? final = null;
                var firstPlanning = true;
                string? scopeFailure = null;

                bool InScope(W101CombatSnapshot snapshot)
                {
                    // This runs after the turn reader has validated freshness and confirmed
                    // disconnect, so a scope refusal cannot falsely imply failed cleanup.
                    string? failure = snapshot.Phase == W101CombatPhase.Unknown ? "combat-battle-state-unknown" :
                        !ids.Add(snapshot.Id) ? "combat-battle-observation-replayed" :
                        snapshot.ObservedUtcTicks <= lastObservedTicks ? "combat-battle-observation-not-newer" :
                        geometry is not null && snapshot.GeometryId != geometry ? "combat-battle-geometry-changed" :
                        snapshot.EncounterId is not null && encounter is not null && snapshot.EncounterId != encounter ? "combat-battle-encounter-changed" : null;
                    if (failure is null && snapshot.Phase == W101CombatPhase.Planning && firstPlanning &&
                        expectedRound is not null && snapshot.Round != expectedRound)
                        failure = "combat-battle-next-round-changed";
                    if (failure is null && !(scope?.Invoke(snapshot) ?? true)) failure = "combat-battle-card-scope-changed";
                    if (failure is not null) { scopeFailure = failure; return false; }
                    lastObservedTicks = snapshot.ObservedUtcTicks;
                    geometry ??= snapshot.GeometryId;
                    if (snapshot.Phase != W101CombatPhase.Loading) encounter ??= snapshot.EncounterId;
                    if (snapshot.Phase == W101CombatPhase.Planning) firstPlanning = false;
                    final = new(snapshot.Id, snapshot.EncounterId, snapshot.Round, snapshot.GeometryId,
                        snapshot.ObservedUtcTicks, snapshot.Phase);
                    return true;
                }

                var wrapped = runtime with
                {
                    Ready = Ready,
                    Read = (child, current, index, childToken) =>
                    {
                        totalReads++;
                        return runtime.Read(child, current, index, childToken);
                    },
                    Dispatch = (action, child, gate, childToken) =>
                    {
                        totalDispatches++;
                        return runtime.Dispatch(action, child, gate, childToken);
                    }
                };
                var turn = W101CombatTurnOperation.Prepare(target, true, wrapped, localLimits, context.Request.Pins, InScope);
                var folderName = $"turn-{number:D3}";
                var folder = Path.Combine(context.Directory, folderName);
                if (Directory.Exists(folder)) throw new InvalidDataException("combat-battle-turn-evidence-already-exists");
                Directory.CreateDirectory(folder);
                // Separate proposal IDs prevent a later turn from replaying a prior receipt.
                // All admitted pins, target, capability flags and the SAME lease are preserved.
                var child = context with { Directory = folder, Request = context.Request with { Id = turn.Request.Id } };
                var outcome = await turn.Execute(child, token).ConfigureAwait(false);
                var turnReport = Path.Combine(folder, "combat-turn.json");
                if (outcome.Artifacts.Length != 1 || !string.Equals(Path.GetFullPath(outcome.Artifacts[0]),
                    turnReport, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("combat-battle-turn-report-mismatch");
                using var document = JsonDocument.Parse(GameExperiments.BoundedBytes(turnReport, 1_000_000));
                var evidence = document.RootElement;
                var verified = evidence.GetProperty("turnVerified").GetBoolean();
                var victory = evidence.GetProperty("victoryObserved").GetBoolean();
                var cleanup = evidence.GetProperty("cleanupConfirmed").GetBoolean();
                if (evidence.GetProperty("schemaVersion").GetInt32() != 1 ||
                    evidence.GetProperty("reads").GetInt32() != totalReads - readsBefore ||
                    evidence.GetProperty("dispatches").GetInt32() != totalDispatches - dispatchesBefore)
                    throw new InvalidDataException("combat-battle-turn-report-count-mismatch");
                reason = scopeFailure ?? outcome.Message;
                turns.Add(new(number, child.Request.Id, folderName + "/combat-turn.json", outcome.State, reason,
                    totalReads - readsBefore, totalDispatches - dispatchesBefore, verified, victory, cleanup, final));
                if (!cleanup || context.Input?.RecoveryRequired == true) { state = OperationState.CleanupFailed; break; }
                if (final?.Phase == W101CombatPhase.Defeat) { state = OperationState.Failed; reason = "combat-battle-defeat-observed"; break; }
                if (outcome.State != OperationState.Completed) { state = outcome.State; break; }
                if (!verified || scopeFailure is not null || totalDispatches == dispatchesBefore || final is null ||
                    final.EncounterId != encounter || final.GeometryId != geometry)
                    throw new InvalidDataException("combat-battle-turn-outcome-unverified");
                if (victory)
                {
                    if (final.Phase != W101CombatPhase.Victory) throw new InvalidDataException("combat-battle-victory-evidence-missing");
                    victoryObserved = true; state = OperationState.Completed; reason = "combat-battle-victory-observed"; break;
                }
                if (final.Phase != W101CombatPhase.Planning) throw new InvalidDataException("combat-battle-next-round-evidence-missing");
                expectedRound = final.Round;
                state = OperationState.Unknown;
                reason = "combat-battle-turn-budget-exceeded";
            }
        }
        catch (OperationCanceledException) { state = OperationState.Cancelled; reason = "combat-battle-cancelled"; }
        catch (Exception error)
        {
            state = totalDispatches > 0 ? OperationState.Unknown : OperationState.Failed;
            reason = error.Message.Length <= 400 ? error.Message : error.Message[..400];
        }
        var cleanupConfirmed = context.Input?.RecoveryRequired != true;
        if (!cleanupConfirmed) state = OperationState.CleanupFailed;
        else if (deadline.IsCancellationRequested || (double.IsFinite(runtime.Clock()) && runtime.Clock() - started >= limits.TimeoutSeconds * 1000))
        { state = OperationState.TimedOut; reason = "combat-battle-deadline-exceeded"; }
        else if (cancellation.IsCancellationRequested) { state = OperationState.Cancelled; reason = "combat-battle-cancelled"; }
        if (state == OperationState.Failed && totalDispatches > 0 && reason != "combat-battle-defeat-observed") state = OperationState.Unknown;
        GameExperiments.WriteAtomic(report, new
        {
            schemaVersion = 1, parentOperationId = context.Request.Id, target, limits, encounterId = encounter,
            reason, state, reads = totalReads, dispatches = totalDispatches, turns,
            victoryObserved, battleVerified = state == OperationState.Completed && victoryObserved,
            questProgressVerified = false, cleanupConfirmed, elapsedMs = runtime.Clock() - started,
            scope = "One encounter. Explicit observed victory only; no damage, loot, collection or quest completion is inferred."
        });
        return new(state, reason, [report]);
    }
}
