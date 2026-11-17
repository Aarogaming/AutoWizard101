using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Pure mapping from one restored UI-tree observation to the shared fact store.
///
/// Only a completed, cleaned-up, non-loading observation with stable states produces
/// facts. Zone stays a candidate (archived layout, not a qualified location). No input,
/// no action authority, no fact promotion beyond the store's qualification rules.</summary>
internal static class W101FactsMapper
{
    internal const string ZoneFact = "zone.candidate";
    internal const string ZoneProducer = "w101.zone-reader";
    internal const string UiProducer = "w101.ui-tree";

    internal static readonly IReadOnlyList<JobFactDefinition> ProposedFacts =
        Array.AsReadOnly(new[]
        {
            new JobFactDefinition(ZoneFact, "string", "", ZoneProducer, false,
                "Candidate zone name from an archived layout reader; not a qualified location.", 5),
        });

    internal static int Map(W101UiTreeResult result, W101FactsStore store)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(store);
        if (result.Status != "observed" || !result.CleanupConfirmed || !result.HookRestored || result.Loading != false)
            return 0;
        var now = result.ObservedUtcTicks;
        var published = 0;
        foreach (var (name, key) in new[] { ("interactionPrompt", "interactionPrompt"), ("dialogue", "dialogue"),
                     ("menu", "menu"), ("nativeIdle", "nativeIdle") })
        {
            if (result.StableStates.TryGetValue(key, out var value) && value is not null)
            {
                if (store.Publish(new W101Fact(name, value.Value ? "true" : "false", UiProducer, now,
                        "r15-archived", W101FactQualification.Observed), now) is not null) published++;
            }
        }
        if (!string.IsNullOrWhiteSpace(result.StableQuestContext?.ObjectiveText))
        {
            if (store.Publish(new W101Fact("quest.objective", result.StableQuestContext.ObjectiveText, UiProducer,
                    now, "r15-archived", W101FactQualification.Observed), now) is not null) published++;
        }
        var zone = result.PoseCandidate?.ZoneCandidate;
        if (zone is { ValueKind: JsonValueKind.Object })
        {
            var name = zone.Value.TryGetProperty("zoneName", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() : null;
            var layout = result.PoseCandidate?.LayoutId ?? "archived-wizwalker-client-zone";
            if (!string.IsNullOrWhiteSpace(name) &&
                store.Publish(new W101Fact(ZoneFact, name!, ZoneProducer, now, layout,
                    W101FactQualification.Candidate, "candidate-zone"), now) is not null) published++;
        }
        return published;
    }
}

/// <summary>Bounded session that repeatedly invokes an injected observation cycle and
/// maps restored results into facts. Identity and recovery are rechecked every cycle;
/// a failed cycle invalidates all facts and stops. No timer, process, input or I/O lives
/// here — the host supplies the cycle and the delay.</summary>
internal sealed class W101FactsSession
{
    private readonly Func<CancellationToken, Task<W101UiTreeResult?>> _cycle;
    private readonly Func<(bool Valid, string Reason)> _identity;
    private readonly Func<bool> _recoveryRequired;
    private readonly int _maximumCycles;
    private readonly Func<int, CancellationToken, Task> _delay;

    internal W101FactsSession(Func<CancellationToken, Task<W101UiTreeResult?>> cycle,
        Func<(bool, string)> identity, Func<bool>? recoveryRequired = null,
        Func<int, CancellationToken, Task>? delay = null, int maximumCycles = 60)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(identity);
        if (maximumCycles is < 1 or > 7200) throw new ArgumentOutOfRangeException(nameof(maximumCycles));
        _cycle = cycle;
        _identity = identity;
        _recoveryRequired = recoveryRequired ?? (() => false);
        _delay = delay ?? ((_, token) => Task.Delay(500, token));
        _maximumCycles = maximumCycles;
    }

    internal async Task<string> RunAsync(W101FactsStore store, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(store);
        var mapped = 0;
        for (var i = 0; i < _maximumCycles; i++)
        {
            token.ThrowIfCancellationRequested();
            if (_recoveryRequired())
            {
                store.InvalidateTarget("recovery-hold");
                return "recovery-hold";
            }
            var identity = _identity();
            if (!identity.Valid)
            {
                store.InvalidateTarget(identity.Reason);
                return "target-changed";
            }
            try
            {
                var result = await _cycle(token).ConfigureAwait(false);
                if (result is null) throw new InvalidDataException("observation returned no result");
                mapped += W101FactsMapper.Map(result, store);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                store.InvalidateTarget("cancelled");
                return "cancelled";
            }
            catch
            {
                store.InvalidateTarget("observer-fault");
                return "observer-fault";
            }
            if (i + 1 < _maximumCycles) await _delay(500, token).ConfigureAwait(false);
        }
        return $"cycle-budget:{mapped}";
    }
}
